package cmd

import (
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"sort"
	"strconv"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
)

var Version = "dev"

var (
	flagPort        int
	flagProject     string
	flagTimeout     int
	flagVerbose     bool
	flagQuiet       bool
	flagDebug       bool
	flagCompactJSON bool
	flagNarrate     bool
)

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

func envBool(key string, fallback bool) bool {
	if v := os.Getenv(key); v != "" {
		if b, err := strconv.ParseBool(v); err == nil {
			return b
		}
	}
	return fallback
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
	"help":      {},
	"--help":    {},
	"-h":        {},
	"version":   {},
	"--version": {},
	"-v":        {},
}

// isHumanCommand reports whether the given subcommand is run by a human.
// Used to gate styled stderr decoration, update notices, progress messages,
// and other output that costs tokens when consumed by an AI agent.
func isHumanCommand(category string) bool {
	_, ok := humanCategories[category]
	return ok
}

// ResponsePrinter holds output-configuration state so that print logic can be
// tested without mutating package-level flag variables.
type ResponsePrinter struct {
	Quiet       bool
	CompactJSON bool
}

func (rp *ResponsePrinter) shouldCompactJSON(category string) bool {
	return rp.CompactJSON || !isHumanCommand(category)
}

// isUserCodeDiagnostic reports whether a structured error code describes a
// failure in the user's C# snippet rather than a hera-agent-unity or
// environment failure. Used to reframe the CLI output prefix so snippet
// failures don't read as tool failures.
func isUserCodeDiagnostic(code string) bool {
	switch code {
	case "EXEC_COMPILE_ERROR",
		"EXEC_RUNTIME_ERROR",
		"EXEC_LOGGED_ERROR",
		"EXEC_COMPILE_TIMEOUT":
		return true
	}
	return false
}

// runStandaloneCommand handles commands that do not need a live Unity connection.
// Returns (true, nil) if the command was handled, (false, nil) if it needs Unity.
func runStandaloneCommand(category string, subArgs []string) (bool, error) {
	switch category {
	case "help", "--help", "-h":
		if len(subArgs) > 0 {
			printTopicHelp(subArgs[0])
		} else {
			printHelp()
		}
		return true, nil
	case "version", "--version", "-v":
		fmt.Println("hera-agent-unity " + Version)
		return true, nil
	case "update":
		return true, updateCmd(subArgs)
	case "install":
		return true, installCmd()
	case "uninstall":
		return true, uninstallCmd()
	case "status":
		inst, err := discoverStatusInstance(flagProject, flagPort)
		if err != nil {
			return true, err
		}
		statusErr := statusCmd(inst)
		printUpdateNotice(category)
		return true, statusErr
	case "ping":
		return true, pingCmd(flagProject, flagPort)
	case "asset-config":
		return true, assetConfigCmd(subArgs)
	case "doctor":
		return true, doctorCmd(subArgs)
	}
	return false, nil
}

// runUnityCommand handles commands that require a live Unity connection.
func runUnityCommand(ctx context.Context, category string, subArgs []string, send SendFunc, resolve instanceResolver) (*client.CommandResponse, error) {
	var resp *client.CommandResponse
	var err error

	switch category {
	case "batch":
		return nil, batchCmd(ctx, subArgs, client.SendBatch, resolve)
	case "editor":
		resp, err = editorCmd(subArgs, send, resolve, category)
	case "test":
		resp, err = testCmd(subArgs, send, resolve)
	case "manage_packages":
		resp, err = managePackagesCmd(subArgs, send, resolve)
	case "unity_docs":
		resp, err = unityDocsCmd(subArgs, send)
	case "ui_doc":
		resp, err = uiDocCmd(subArgs, send)
	case "exec":
		subArgs, err = readExecFileIfPresent(subArgs)
		if err != nil {
			return nil, err
		}
		subArgs = readStdinIfPiped(subArgs)
		var params map[string]interface{}
		params, _, err = buildParams(subArgs, nil)
		if err == nil {
			if v, ok := params["check"].(bool); ok && v {
				params["compile_only"] = true
				delete(params, "check")
			}
			resp, err = send("exec", params)
		}
	default:
		var params map[string]interface{}
		params, _, err = buildParams(subArgs, nil)
		if err == nil {
			resp, err = send(category, params)
		}
	}

	return resp, err
}

func Execute(ctx context.Context) error {
	flag.IntVar(&flagPort, "port", envInt("HERA_AGENT_PORT", 0), "Select Unity instance by active heartbeat port")
	flag.StringVar(&flagProject, "project", envString("HERA_AGENT_PROJECT", ""), "Select Unity instance by project path")
	flag.IntVar(&flagTimeout, "timeout", envInt("HERA_AGENT_TIMEOUT_MS", 60000), "Request timeout in milliseconds")
	flag.BoolVar(&flagVerbose, "verbose", envBool("HERA_AGENT_VERBOSE", false), "Print progress + per-phase timings to stderr")
	flag.BoolVar(&flagQuiet, "quiet", envBool("HERA_AGENT_QUIET", false), "Suppress decorative progress messages (errors still printed plain)")
	flag.BoolVar(&flagDebug, "debug", envBool("HERA_AGENT_DEBUG", false), "Print HTTP request/response bodies and discovery info to stderr")
	flag.BoolVar(&flagCompactJSON, "compact-json", envBool("HERA_AGENT_COMPACT_JSON", false), "Output JSON without indentation (smaller responses for AI agents)")
	flag.BoolVar(&flagNarrate, "narrate", envBool("HERA_AGENT_NARRATE", false), "Print waitForAlive/waitForReady progress messages even on tool commands (default: human-only)")

	flag.Usage = func() { printHelp() }

	args := os.Args[1:]
	flagArgs, cmdArgs := splitArgs(args)
	if err := flag.CommandLine.Parse(flagArgs); err != nil {
		return fmt.Errorf("flag parse error: %w", err)
	}

	if len(cmdArgs) == 0 {
		printHelp()
		return nil
	}

	checkBinaryPath()
	client.DefaultClient.Debug = flagDebug

	category := cmdArgs[0]
	subArgs := cmdArgs[1:]

	// --help / -h on any command
	for _, a := range subArgs {
		if a == "--help" || a == "-h" {
			printTopicHelp(category)
			return nil
		}
	}

	handled, err := runStandaloneCommand(category, subArgs)
	if err != nil {
		return err
	}
	if handled {
		return nil
	}

	inst, err := client.DiscoverInstance(flagProject, flagPort)
	if err != nil {
		return err
	}

	resolve := makeResolver(inst, flagProject, flagPort)
	if _, err := waitForAlive(resolve, flagTimeout, category); err != nil {
		return err
	}

	send := prepareSend(resolve, category, flagTimeout, flagVerbose)
	resp, err := runUnityCommand(ctx, category, subArgs, send, resolve)
	if err != nil {
		return err
	}

	printer := &ResponsePrinter{
		Quiet:       flagQuiet,
		CompactJSON: flagCompactJSON,
	}
	printer.Print(resp, category)

	if flagVerbose {
		printTimings(resp)
	}

	printUpdateNotice(category)

	if !resp.Success {
		return fmt.Errorf("command failed: %s", resp.Message)
	}

	return nil
}

// makeResolver returns an instanceResolver that follows the same project even
// if Unity rebinds to a new port during reload.
func makeResolver(inst *client.Instance, project string, port int) instanceResolver {
	targetProject := project
	if port == 0 && targetProject == "" {
		targetProject = inst.ProjectPath
	}
	return func() (*client.Instance, error) {
		if port > 0 {
			return client.DiscoverInstance("", port)
		}
		return client.DiscoverInstance(targetProject, 0)
	}
}

// prepareSend builds the SendFunc closure injected into command handlers.
// It resolves the current instance on every call so that port rebinds during
// domain reload are followed transparently.
func prepareSend(resolve instanceResolver, category string, timeoutMs int, verbose bool) SendFunc {
	return func(command string, params interface{}) (*client.CommandResponse, error) {
		inst, err := resolve()
		if err != nil {
			return nil, err
		}
		if command == "exec" && (isHumanCommand(category) || verbose) {
			fmt.Fprintln(os.Stderr, "[hera-agent-unity] compiling...")
		}
		return sendWithProgress(inst, command, params, timeoutMs, verbose)
	}
}

// withProgress runs fn while printing a 1-second-cadence progress line to
// stderr when verbose is true. This keeps harnesses from timing out while
// Unity is busy compiling or executing a long command.
func withProgress(command string, verbose bool, fn func()) {
	if !verbose {
		fn()
		return
	}
	done := make(chan struct{})
	start := time.Now()
	go func() {
		ticker := time.NewTicker(1 * time.Second)
		defer ticker.Stop()
		for {
			select {
			case <-done:
				return
			case t := <-ticker.C:
				elapsed := int(t.Sub(start).Seconds())
				fmt.Fprintf(os.Stderr, "[hera-agent-unity] %s in progress... (%ds)\n", command, elapsed)
			}
		}
	}()
	fn()
	close(done)
}

// sendWithProgress wraps client.Send with progress output.
func sendWithProgress(inst *client.Instance, command string, params interface{}, timeoutMs int, verbose bool) (*client.CommandResponse, error) {
	var resp *client.CommandResponse
	var err error
	withProgress(command, verbose, func() {
		resp, err = client.Send(inst, command, params, timeoutMs)
	})
	return resp, err
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

// SendFunc is the function signature for sending a command to Unity.
// Injected into each command function so they can be tested without a real Unity connection.
type SendFunc func(command string, params interface{}) (*client.CommandResponse, error)

// SendBatchFunc is the function signature for sending a batch command to Unity.
// Injected so batchCmd can be tested without a real Unity connection.
type SendBatchFunc func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error)

func (rp *ResponsePrinter) Print(resp *client.CommandResponse, category string) {
	if !resp.Success {
		// AI-target commands: emit a plain JSON error envelope to stderr so
		// the agent can parse code / suggestions / data without scraping
		// styled boxes. Human commands keep the styled panel for terminal
		// readability.
		if !isHumanCommand(category) {
			var b []byte
			if rp.shouldCompactJSON(category) {
				b, _ = json.Marshal(resp)
			} else {
				b, _ = json.MarshalIndent(resp, "", "  ")
			}
			fmt.Fprintln(os.Stderr, string(b))
			return
		}

		msg := resp.Message
		if msg == "" {
			msg = "unknown error"
		}
		if resp.Code != "" {
			if isUserCodeDiagnostic(resp.Code) {
				msg = fmt.Sprintf("[user-code %s] %s", resp.Code, msg)
			} else {
				msg = fmt.Sprintf("[%s] %s", resp.Code, msg)
			}
		}
		hasDetails := len(resp.Data) > 0 && string(resp.Data) != "null"
		var suggestions string
		if len(resp.Suggestions) > 0 {
			suggestions = "\n\nSuggestions:\n- " + strings.Join(resp.Suggestions, "\n- ")
		}
		if rp.Quiet {
			if hasDetails {
				fmt.Fprintf(os.Stderr, "%s\n%s%s\n", msg, string(resp.Data), suggestions)
			} else {
				fmt.Fprintln(os.Stderr, msg+suggestions)
			}
		} else {
			body := msg + suggestions
			if hasDetails {
				body = fmt.Sprintf("%s\n\nDetails:\n%s%s", msg, string(resp.Data), suggestions)
			}
			fmt.Fprintln(os.Stderr, tui.ErrorPanel("Failed", body))
		}
		return
	}

	if resp.AgentHint != "" {
		fmt.Fprintln(os.Stderr, "hint: "+resp.AgentHint)
	}

	if len(resp.Data) > 0 && string(resp.Data) != "null" {
		var pretty interface{}
		if json.Unmarshal(resp.Data, &pretty) == nil {
			// If data is a plain string, print it raw (preserves newlines for tree output etc.)
			if s, ok := pretty.(string); ok {
				fmt.Println(s)
			} else {
				var b []byte
				if rp.shouldCompactJSON(category) {
					b, _ = json.Marshal(pretty)
				} else {
					b, _ = json.MarshalIndent(pretty, "", "  ")
				}
				fmt.Println(string(b))
			}
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
		if jsonErr := json.Unmarshal([]byte(raw), &params); jsonErr != nil {
			return nil, nil, fmt.Errorf("invalid JSON in --params: %w", jsonErr)
		}
	}
	for k, v := range flags {
		if k == "params" {
			continue
		}
		if _, exists := params[k]; exists {
			continue
		}
		if n, err := strconv.Atoi(v); err == nil {
			params[k] = n
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

// readStdinIfPiped reads stdin when piped and prepends it as the first positional arg.
//
// Stdin is only consumed when:
//   - no positional arg is already present (positional takes precedence per docs), AND
//   - stdin looks like a real data source: a named pipe (`echo ... | hera-agent-unity`)
//     or a regular file redirect (`hera-agent-unity exec < code.cs`).
//
// In non-TTY shells where stdin is open but will never deliver data — Cursor's
// shell, bash `$(...)` capture, compound `cmd1; hera-agent-unity exec ...`, CI
// runners with detached stdin — io.ReadAll(os.Stdin) would otherwise block forever
// waiting for EOF. The mode guard prevents that.
func readStdinIfPiped(args []string) []string {
	// Positional arg (the code) wins over stdin per documented precedence,
	// so there is no reason to even probe stdin if one is already present.
	_, positional, _ := buildParams(args, nil)
	if len(positional) > 0 {
		return args
	}

	info, err := os.Stdin.Stat()
	if err != nil {
		return args
	}
	mode := info.Mode()
	if mode&os.ModeCharDevice != 0 {
		return args // interactive terminal, not piped
	}
	// Only read when stdin has an actual data source: a pipe or a regular file.
	// Anything else (detached, /dev/null on some platforms, closed socket) is
	// treated as "no stdin" rather than blocked on indefinitely.
	if mode&os.ModeNamedPipe == 0 && !mode.IsRegular() {
		return args
	}
	data, err := io.ReadAll(os.Stdin)
	if err != nil || len(data) == 0 {
		return args
	}
	code := strings.TrimRight(string(data), "\n\r")
	return append([]string{code}, args...)
}

// splitArgs separates global flags (--port, --project, --timeout, --verbose)
// from subcommand args. Global flags must be parsed by flag.CommandLine before
// the subcommand runs.
func splitArgs(args []string) (flags, commands []string) {
	for i := 0; i < len(args); i++ {
		switch args[i] {
		case "--port", "--project", "--timeout":
			flags = append(flags, args[i])
			if i+1 < len(args) {
				i++
				flags = append(flags, args[i])
			}
		case "--verbose", "--quiet", "--debug", "--compact-json", "--narrate":
			flags = append(flags, args[i])
		default:
			commands = append(commands, args[i])
		}
	}
	return
}
