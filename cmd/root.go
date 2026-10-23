package cmd

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"math/big"
	"os"
	"sort"
	"strconv"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

var Version = "dev"

// ErrCommandFailed signals that a tool command returned success=false. The
// compact JSON error envelope has already been printed to stderr, so main()
// exits non-zero on this sentinel without printing a duplicate error line.
var ErrCommandFailed = errors.New("command failed")

func envInt(key string, fallback int) int {
	if v := os.Getenv(key); v != "" {
		if n, err := strconv.Atoi(v); err == nil {
			return n
		}
	}
	return fallback
}

func envString(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func envBool(key string) bool {
	if v := os.Getenv(key); v != "" {
		if b, err := strconv.ParseBool(v); err == nil {
			return b
		}
	}
	return false
}

// humanCategories are subcommands invoked by humans at a terminal. Everything
// else is assumed to be called by an AI agent (Claude Code CLI / Codex) where
// stderr decoration is just token cost.
var humanCategories = map[string]struct{}{
	"install":   {},
	"uninstall": {},
	"status":    {},
	"update":    {},
	"doctor":    {},
}

// isHumanCommand reports whether the given subcommand is run by a human.
// Used to gate styled stderr decoration, update notices, progress messages,
// and other output that costs tokens when consumed by an AI agent.
func isHumanCommand(category string) bool {
	_, ok := humanCategories[category]
	return ok
}

// ResponsePrinter emits tool responses as compact JSON.
type ResponsePrinter struct{}

func operationOutcomeResponse(err error) *client.CommandResponse {
	var unknown *client.OperationOutcomeUnknownError
	var unresponsive *client.TargetUnresponsiveError
	blocked := errors.As(err, &unresponsive) && unresponsive.State == "blocked_by_dialog"
	response := &client.CommandResponse{Success: false}
	data := map[string]any{}
	if errors.As(err, &unknown) {
		response.Code, response.Message = unknown.Code, unknown.Error()
		data["operation_id"], data["tool"] = string(unknown.OperationID), unknown.Command
		data["project"], data["port"] = unknown.Project, unknown.Port
	} else if blocked {
		response.Code, response.Message = "EDITOR_BLOCKED_BY_DIALOG", unresponsive.Error()
		data["project"], data["port"] = unresponsive.Project, unresponsive.Port
	} else {
		return nil
	}
	if blocked {
		response.State = "blocked_by_dialog"
		data["dialog_title"] = unresponsive.DialogTitle
		response.Message += "; a modal Editor dialog is blocking the main thread; a person must close it"
	}
	if unknown != nil && unknown.OperationID != "" {
		response.Message += fmt.Sprintf(`; check it with: hera-agent-unity --project "%s" task status %s`, unknown.Project, unknown.OperationID)
	}
	response.Data, _ = json.Marshal(data)
	return response
}

func Execute(ctx context.Context) error {
	config, cmdArgs, err := parseGlobalConfig(os.Args[1:])
	if err != nil {
		return err
	}
	if len(cmdArgs) == 0 {
		printHelp()
		return nil
	}

	checkBinaryPath()
	client.DefaultClient.Debug = config.Debug

	category := cmdArgs[0]
	subArgs := cmdArgs[1:]

	// --help / -h on any command
	for _, a := range subArgs {
		if a == "--help" || a == "-h" {
			printTopicHelp(category)
			return nil
		}
	}

	var testOutput testOptions
	if category == "test" {
		testOutput, err = parseTestOptions(subArgs)
		if err != nil {
			(&ResponsePrinter{}).Print(&client.CommandResponse{Code: "INVALID_ARGUMENT", Message: err.Error()}, category)
			return ErrCommandFailed
		}
	}
	handled, err := (standaloneRunner{config: config}).Run(ctx, category, subArgs)
	if err != nil {
		return err
	}
	if handled {
		return nil
	}

	initialResolve := func() (*client.Instance, error) {
		return client.DiscoverInstanceFresh(config.Project, config.Port)
	}
	inst, err := waitForInstance(
		ctx,
		initialResolve,
		initialDiscoveryTimeoutMs(config.TimeoutMillis()),
	)
	if err != nil {
		return err
	}

	freshResolve := makeFreshResolver(inst, config.Project, config.Port)
	if !isTestResume(category, subArgs) {
		inst, err = config.Wait(category).WaitForAlive(ctx, freshResolve)
		if err != nil {
			return err
		}
	}

	send := prepareSend(ctx, inst, category, config.TimeoutMillis(), config.Verbose)
	resp, err := (unityCommandRunner{
		config:   config,
		send:     send,
		instance: inst,
		resolve:  freshResolve,
	}).Run(ctx, category, subArgs)
	if err != nil {
		if outcome := operationOutcomeResponse(err); outcome != nil {
			(&ResponsePrinter{}).Print(outcome, category)
			return ErrCommandFailed
		}
		return err
	}

	printer := &ResponsePrinter{}
	written := false
	if category == "test" {
		written, err = writeTestOutput(resp, testOutput, os.Stdout, os.Stderr)
		if err != nil {
			return err
		}
	}
	if !written {
		printer.Print(resp, category)
	}

	if config.Verbose {
		printTimings(resp)
	}

	printUpdateNoticeWithConfig(category, config.Quiet)

	if !resp.Success {
		// For AI-target (tool) commands the printer already wrote the compact
		// JSON error envelope to stderr; returning the message again would
		// double it in the agent's captured output (and waste tokens). Signal
		// failure with a sentinel that main() exits on without re-printing.
		// Human commands keep the readable "Error: command failed: …" line.
		if !isHumanCommand(category) {
			return ErrCommandFailed
		}
		return fmt.Errorf("command failed: %s", resp.Message)
	}

	return nil
}

func printTimings(resp *client.CommandResponse) {
	if resp == nil || len(resp.Timings) == 0 {
		return
	}
	keys := make([]string, 0, len(resp.Timings))
	for k := range resp.Timings {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	parts := make([]string, 0, len(keys))
	for _, k := range keys {
		parts = append(parts, fmt.Sprintf("%s=%dms", k, resp.Timings[k]))
	}
	fmt.Fprintf(os.Stderr, "[hera-agent-unity] timings: %s\n", strings.Join(parts, " "))
}

func (rp *ResponsePrinter) Print(resp *client.CommandResponse, category string) {
	if !resp.Success {
		// Timings are emitted separately only with --verbose.
		envelope := *resp
		envelope.Timings = nil
		b, _ := json.Marshal(envelope)
		fmt.Fprintln(os.Stderr, string(b))
		return
	}

	if resp.AgentHint != "" {
		fmt.Fprintln(os.Stderr, "hint: "+resp.AgentHint)
	}

	if len(resp.Data) > 0 && string(resp.Data) != "null" {
		// If data is a plain JSON string, print it raw (preserves newlines for tree output etc.)
		if len(resp.Data) >= 2 && resp.Data[0] == '"' && resp.Data[len(resp.Data)-1] == '"' {
			var s string
			if err := json.Unmarshal(resp.Data, &s); err == nil {
				fmt.Println(s)
				return
			}
		}
		var compact bytes.Buffer
		if err := json.Compact(&compact, resp.Data); err == nil {
			fmt.Println(compact.String())
		} else {
			fmt.Println(string(resp.Data))
		}
	} else if resp.Message != "" {
		fmt.Println(resp.Message)
	}
}

// buildParams parses --flag value pairs and positional args from args and merges with base params.
// It returns the parsed params, any positional arguments, and an error.
func buildParams(args []string, base map[string]interface{}) (map[string]interface{}, []string, error) {
	params := map[string]interface{}{}
	for k, v := range base {
		params[k] = v
	}

	var positional []string
	flags := map[string]string{}
	for i := 0; i < len(args); i++ {
		a := args[i]
		if strings.HasPrefix(a, "--") {
			key := a[2:]
			if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
				flags[key] = args[i+1]
				i++
			} else {
				flags[key] = "true"
			}
		} else {
			positional = append(positional, a)
		}
	}

	if raw, ok := flags["params"]; ok {
		if jsonErr := decodeJSONNumbers([]byte(raw), &params); jsonErr != nil {
			return nil, nil, fmt.Errorf("invalid JSON in --params: %w", jsonErr)
		}
	}
	for k, v := range flags {
		if k == "params" {
			continue
		}
		if n, err := strconv.ParseInt(v, 10, 64); err == nil {
			params[k] = n
		} else if n, ok := new(big.Int).SetString(v, 10); ok {
			// Normalize optional '+' and leading zeroes to valid JSON syntax.
			params[k] = json.Number(n.String())
		} else if v == "true" {
			params[k] = true
		} else if v == "false" {
			params[k] = false
		} else {
			params[k] = v
		}
	}

	if len(positional) > 0 {
		params["args"] = positional
	}

	return params, positional, nil
}

func decodeJSONNumbers(data []byte, target any) error {
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.UseNumber()
	if err := decoder.Decode(target); err != nil {
		return err
	}
	var trailing any
	if err := decoder.Decode(&trailing); err != io.EOF {
		if err == nil {
			return errors.New("multiple JSON values")
		}
		return err
	}
	return nil
}

// readExecFileIfPresent strips --file <path> and prepends file contents as the
// first positional arg. Returns args unchanged when --file is absent. Lets
// agents avoid shell-escaping long code blocks.
// Precedence (handled by caller): positional / stdin > --file. So if positional
// already provides code, --file is silently ignored (still stripped from args).
func readExecFileIfPresent(args []string) ([]string, error) {
	var out []string
	var filePath string
	for i := 0; i < len(args); i++ {
		if args[i] == "--file" {
			if i+1 >= len(args) {
				return nil, fmt.Errorf("--file requires a path argument")
			}
			filePath = args[i+1]
			i++
			continue
		}
		out = append(out, args[i])
	}
	if filePath == "" {
		return out, nil
	}
	_, positional, _ := buildParams(out, nil)
	if len(positional) > 0 {
		return out, nil
	}
	data, err := os.ReadFile(filePath)
	if err != nil {
		return nil, fmt.Errorf("read --file %s: %w", filePath, err)
	}
	code := strings.TrimRight(string(data), "\n\r")
	return append([]string{code}, out...), nil
}

// splitArgs separates global flags from subcommand arguments so each Execute
// invocation can parse them with its own isolated FlagSet.
func splitArgs(args []string) (flags, commands []string) {
	for i := 0; i < len(args); i++ {
		switch args[i] {
		case "--port", "--project", "--timeout":
			flags = append(flags, args[i])
			if i+1 < len(args) {
				i++
				flags = append(flags, args[i])
			}
		case "--verbose", "--quiet", "--debug", "--compact-json", "--narrate", "--yes":
			flags = append(flags, args[i])
		default:
			commands = append(commands, args[i])
		}
	}
	return
}
