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

// currentCategory tracks which subcommand is running so helpers can branch
// between human-facing (styled stderr) and AI-facing (plain JSON) output.
var currentCategory string

// humanCategories are subcommands invoked by humans at a terminal. Everything
// else is assumed to be called by an AI agent (Claude Code CLI / Codex) where
// stderr decoration is just token cost.
var humanCategories = map[string]bool{
	"install":   true,
	"uninstall": true,
	"status":    true,
	"update":    true,
	"doctor":    true,
	"help":      true,
	"--help":    true,
	"-h":        true,
	"version":   true,
	"--version": true,
	"-v":        true,
}

// isHumanCommand reports whether the current subcommand is run by a human.
// Used to gate styled stderr decoration, update notices, progress messages,
// and other output that costs tokens when consumed by an AI agent.
func isHumanCommand() bool {
	return humanCategories[currentCategory]
}

// shouldCompactJSON reports whether printResponse should emit compact JSON.
// True when the user explicitly requested it (--compact-json / env), or when
// the current subcommand is an AI-target command. Human commands keep
// indented JSON for readability.
func shouldCompactJSON() bool {
	return flagCompactJSON || !isHumanCommand()
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
		fmt.Fprintf(os.Stderr, "flag parse error: %v\n", err)
		os.Exit(1)
	}

	if len(cmdArgs) == 0 {
		printHelp()
		return nil
	}

	checkBinaryPath()

	client.Debug = flagDebug

	category := cmdArgs[0]
	subArgs := cmdArgs[1:]
	currentCategory = category

	// --help / -h on any command
	for _, a := range subArgs {
		if a == "--help" || a == "-h" {
			printTopicHelp(category)
			return nil
		}
	}

	switch category {
	case "help", "--help", "-h":
		if len(subArgs) > 0 {
			printTopicHelp(subArgs[0])
		} else {
			printHelp()
		}
		return nil
	case "version", "--version", "-v":
		fmt.Println("hera-agent-unity " + Version)
		return nil
	case "update":
		return updateCmd(subArgs)
	case "install":
		return installCmd()
	case "uninstall":
		return uninstallCmd()
	case "status":
		inst, err := discoverStatusInstance(flagProject, flagPort)
		if err != nil {
			return err
		}
		statusErr := statusCmd(inst)
		printUpdateNotice()
		return statusErr
	case "ping":
		return pingCmd(flagProject, flagPort)
	case "asset-config":
		return assetConfigCmd(subArgs)
	case "doctor":
		return doctorCmd(subArgs)
	}

	inst, err := client.DiscoverInstance(flagProject, flagPort)
	if err != nil {
		return err
	}

	targetProject := flagProject
	if flagPort == 0 && targetProject == "" {
		targetProject = inst.ProjectPath
	}

	resolve := func() (*client.Instance, error) {
		if flagPort > 0 {
			return client.DiscoverInstance("", flagPort)
		}
		return client.DiscoverInstance(targetProject, 0)
	}

	if _, err := waitForAlive(resolve, flagTimeout); err != nil {
		return err
	}

	timeout := flagTimeout
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		inst, err := resolve()
		if err != nil {
			return nil, err
		}
		if command == "exec" && (isHumanCommand() || flagVerbose) {
			fmt.Fprintln(os.Stderr, "[hera-agent-unity] compiling...")
		}
		return sendWithProgress(inst, command, params, timeout, flagVerbose)
	}

	var resp *client.CommandResponse

	switch category {
	case "batch":
		return batchCmd(ctx, subArgs, client.SendBatch, resolve)
	case "editor":
		resp, err = editorCmd(subArgs, send, resolve)
	case "test":
		currentInst, resolveErr := resolve()
		if resolveErr != nil {
			return resolveErr
		}
		testSend := func(command string, params interface{}) (*client.CommandResponse, error) {
			return client.Send(currentInst, command, params, 0)
		}
		resp, err = testCmd(subArgs, testSend, currentInst.Port)
	case "manage_packages":
		currentInst, resolveErr := resolve()
		if resolveErr != nil {
			return resolveErr
		}
		// Package operations can outlast the global flag timeout (60s default)
		// because the C# list/add path may run up to 60s on its own and add/
		// remove poll a file for up to 10m. Send with no HTTP timeout — the
		// start response (list result or "running" envelope) returns quickly.
		pkgSend := func(command string, params interface{}) (*client.CommandResponse, error) {
			return client.Send(currentInst, command, params, 0)
		}
		resp, err = managePackagesCmd(subArgs, pkgSend, currentInst.Port)
	case "exec":
		subArgs, err = readExecFileIfPresent(subArgs)
		if err != nil {
			return err
		}
		subArgs = readStdinIfPiped(subArgs)
		var params map[string]interface{}
		params, err = buildParams(subArgs, nil)
		if err == nil {
			// --check is the human-facing flag for compile-only mode; the
			// wire payload uses compile_only to match the C# ToolParams reader.
			if v, ok := params["check"].(bool); ok && v {
				params["compile_only"] = true
				delete(params, "check")
			}
			resp, err = send("exec", params)
		}
	default:
		var params map[string]interface{}
		params, err = buildParams(subArgs, nil)
		if err == nil {
			resp, err = send(category, params)
		}
	}

	if err != nil {
		return err
	}

	printResponse(resp)

	if flagVerbose {
		printTimings(resp)
	}

	printUpdateNotice()

	if !resp.Success {
		os.Exit(1)
	}

	return nil
}

// sendWithProgress wraps client.Send. When verbose, prints a 1-second-cadence
// progress line to stderr so harnesses see liveness while Unity is busy.
func sendWithProgress(inst *client.Instance, command string, params interface{}, timeoutMs int, verbose bool) (*client.CommandResponse, error) {
	if !verbose {
		return client.Send(inst, command, params, timeoutMs)
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
	resp, err := client.Send(inst, command, params, timeoutMs)
	close(done)
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

// sendFn is the function signature for sending a command to Unity.
// Injected into each command function so they can be tested without a real Unity connection.
type sendFn func(command string, params interface{}) (*client.CommandResponse, error)

// sendBatchFn is the function signature for sending a batch command to Unity.
// Injected so batchCmd can be tested without a real Unity connection.
type sendBatchFn func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error)

func printResponse(resp *client.CommandResponse) {
	if !resp.Success {
		// AI-target commands: emit a plain JSON error envelope to stderr so
		// the agent can parse code / suggestions / data without scraping
		// styled boxes. Human commands keep the styled panel for terminal
		// readability.
		if !isHumanCommand() {
			var b []byte
			if shouldCompactJSON() {
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
		if flagQuiet {
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
				if shouldCompactJSON() {
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

// parseSubFlags parses --key value and --flag (boolean) pairs from subcommand args.
// Non-flag args (no "--" prefix) are silently ignored.
func parseSubFlags(args []string) map[string]string {
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
		}
	}
	return flags
}

// buildParams parses --flag value pairs and positional args from args and merges with base params.
func buildParams(args []string, base map[string]interface{}) (map[string]interface{}, error) {
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
			return nil, fmt.Errorf("invalid JSON in --params: %w", jsonErr)
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

	return params, nil
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
	hasPositional := false
	for _, a := range out {
		if !strings.HasPrefix(a, "--") {
			hasPositional = true
			break
		}
	}
	if hasPositional {
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
	for _, a := range args {
		if !strings.HasPrefix(a, "--") {
			return args
		}
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

func printHelp() {
	fmt.Print(`hera-agent-unity ` + Version + ` — Control Unity Editor from the command line

Usage: hera-agent-unity <command> [subcommand] [options]

Editor Control:
  editor play [--wait]          Enter play mode (--wait blocks until fully entered)
  editor stop                   Exit play mode
  editor pause                  Toggle pause/resume (play mode only)
  editor refresh [--force]      Refresh asset database (blocked in play mode unless forced)
  editor refresh --compile      Recompile scripts and wait until done

Console:
  console                       Read error & warning logs (default)
  console --lines 20            Limit to N entries
  console --type error,warning,log   Filter by log types (comma-separated)
  console --stacktrace full     Stack trace: none, user (default), full
  console --clear               Clear console

Execute C#:
  exec "<code>"                 Run C# code in Unity (return required for output)
  echo '<code>' | exec          Pipe code via stdin (avoids shell escaping)
  exec --file path.cs           Load code from file (positional/stdin take precedence)
  exec "<code>" --usings x,y    Add extra using directives
  exec "<code>" --no-cache      Skip compile/assembly cache (debug only)

Log:
  log "<message>"               Write to Unity console (no compile cost)
  log "<msg>" --level warning   Levels: log (default), warning, error

  Examples:
    exec "Time.time"
    exec "GameObject.FindObjectsOfType<Camera>().Length"
    exec "var go = new GameObject(\"Test\"); return go.name;"

  Note: identical code is served from the in-memory assembly cache
  (warm calls skip csc). First call per Unity session is the cold path.

Scene:
  scene info                    Active scene name/path/dirty + loaded scene list
  scene load <path|name>        Open scene (default mode: single)
  scene load <path> --mode additive
                                Load additively without unloading current
  scene save [<path|name>]      Save active scene (or named scene if specified)
  scene list                    Build Settings registered scenes
  scene close <path|name>       Unload a non-active loaded scene (additive)

  Examples:
    scene info
    scene load Assets/Scenes/Main.unity
    scene load Main --mode additive
    scene save
    scene close Lobby

Packages (Package Manager):
  manage_packages list                              List every resolved package (sync)
  manage_packages add <id>                          Install — registry / git URL / file:.. (async, job_id)
  manage_packages remove <name>                     Uninstall by package name (async, job_id)
  manage_packages embed <name>                      Copy cached package into Packages/ (async, job_id)

  Examples:
    manage_packages list
    manage_packages add com.unity.ai.navigation
    manage_packages add https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
    manage_packages remove com.unity.ai.navigation
    manage_packages embed com.unity.test-framework

GameObjects (find):
  find_gameobjects                                  All scene GameObjects (paged, limit=50)
  find_gameobjects --name Player                    Substring filter on name (case-insensitive)
  find_gameobjects --tag Enemy                      Exact tag match
  find_gameobjects --layer UI                       Layer name or integer index
  find_gameobjects --component Rigidbody            Has the given component
  find_gameobjects --path_glob /Root/**/Pickup      Glob on hierarchy path (* segment, ** multiple)
  find_gameobjects --include_inactive false         Active in hierarchy only (default includes inactive)
  find_gameobjects --limit 100 --offset 50          Pagination

GameObjects:
  manage_gameobject create --name <n> [--primitive <kind>] [--parent <id|path>] [--position x,y,z]
  manage_gameobject destroy --instance_id <N>|--path </R/C>
  manage_gameobject move --instance_id <N>|--path </R/C> --position x,y,z [--space world|local]
  manage_gameobject set_parent --instance_id <N>|--path </R/C> --parent <id|path|none>
  manage_gameobject set_active --instance_id <N>|--path </R/C> --active true|false
  manage_gameobject set_name --instance_id <N>|--path </R/C> --name <new>
  manage_gameobject get_transform --instance_id <N>|--path </R/C>

  Primitives: cube, sphere, capsule, cylinder, plane, quad (omit for empty GameObject)

  Examples:
    manage_gameobject create --name Player
    manage_gameobject create --name Cube --primitive cube --position 0,1,0
    manage_gameobject set_parent --path /Player --parent /Root
    manage_gameobject get_transform --path /Root/Player

Menu:
  menu "<path>"                 Execute Unity menu item by path

  Examples:
    menu "File/Save Project"
    menu "Assets/Refresh"

Screenshot:
  screenshot                          Capture scene view (default)
  screenshot --view game              Capture game view
  screenshot --output_path <path>     Custom output path

Reserialize:
  reserialize [path...]          Force reserialize (no args = entire project)

  Examples:
    reserialize                                                    Reserialize entire project
    reserialize Assets/Scenes/Main.unity
    reserialize Assets/Prefabs/A.prefab Assets/Prefabs/B.prefab

Tests:
  test                            Run EditMode tests (default)
  test --mode PlayMode            Run PlayMode tests
  test --filter <name>            Filter by namespace, class, or full test name

Profiler:
  profiler hierarchy              Top-level profiler samples (last frame)
  profiler hierarchy --depth 5    Recursive drill-down (0=unlimited)
  profiler hierarchy --root Name  Set root by name (substring match)
  profiler hierarchy --frames 30  Average over last 30 frames
  profiler hierarchy --parent 5   Drill into item by ID
  profiler hierarchy --min 0.5    Filter items below 0.5ms
  profiler hierarchy --sort self  Sort by self time
  profiler enable                Start profiler recording
  profiler disable               Stop profiler recording
  profiler status                Show profiler state
  profiler clear                 Clear all captured frames

Custom Tools:
  list                          List tools (slim: name + description + schema)
  list --names                  Names only (one entry per tool — token-efficient)
  list --tool <name>            Full schema for a single tool (params + output + metadata)
  <name>                        Call a custom tool directly
  <name> --params '{"k":"v"}'   Call with JSON parameters

Status:
  status                        Show Unity Editor state (ready, compiling, etc.)
  ping                          Token-cheap liveness probe (heartbeat read only, no HTTP)

Diagnostics:
  doctor                        Self-check: binary path, PATH resolution,
                                duplicate installs, shell hints, Unity instances
  doctor --json                 Same data, JSON envelope for agent parsing
  doctor --agent-rules          Print Quick Rules + Pitfalls from AGENT.md
                                (append to CLAUDE.md / AGENTS.md / etc.)

Batch:
  batch --file <path>           Execute multiple commands from a JSON file
  batch                         Pipe JSON via stdin (no file needed)
  batch --dry-run               Preview the plan without execution

Update:
  update                        Update to the latest version
  update --check                Check for updates without installing

Install:
  install                       Register the binary on PATH (self-installer)
  uninstall                     Completely remove from PATH and delete configs

Asset Config:
  asset-config                  Interactive checkbox UI (Space to toggle)
  asset-config list             List all assets with status
  asset-config enable <id>      Enable an asset
  asset-config disable <id>     Disable an asset
  asset-config detect           Auto-detect installed assets (requires Unity)
  asset-config --json           Output enabled assets as JSON (for AI agents)

Global Options:
  --port <N>          Select Unity instance by active heartbeat port
  --project <path>    Select Unity instance by project path
  --timeout <ms>      Request timeout in ms (default: 60000)
  --verbose           Print progress + per-phase timings to stderr
  --quiet             Suppress decorative progress messages (env: HERA_AGENT_QUIET)
  --debug             Print HTTP request/response bodies + discovery info
                      to stderr (env: HERA_AGENT_DEBUG)
  --compact-json      Emit JSON without indentation — smaller AI payloads
                      (env: HERA_AGENT_COMPACT_JSON)
  --narrate           Print waitForAlive/waitForReady progress even on
                      tool commands (env: HERA_AGENT_NARRATE)

Use "hera-agent-unity <command> --help" for more information about a command.

Notes:
  - Unity must be open with the Connector package installed
  - Multiple Unity instances: use --port or --project to select
  - Custom tools: any [HeraTool] class is auto-discovered
  - Run 'list' to see all available tools
`)
}

func printTopicHelp(topic string) {
	switch topic {
	case "editor":
		fmt.Print(`Usage: hera-agent-unity editor <play|stop|pause|refresh> [options]

Subcommands:
  play [--wait]       Enter play mode
                      --wait blocks until Unity fully enters play mode.
                      Without --wait, returns immediately after requesting.
  stop                Exit play mode. No effect if not playing.
  pause               Toggle pause. Only works during play mode.
  refresh             Refresh AssetDatabase (reimport changed assets).
                      Blocked in play mode unless --force is set.
    --compile         Recompile scripts and wait until compilation finishes.
    --force           Allow refresh during play mode and force asset update.

Examples:
  hera-agent-unity editor play --wait
  hera-agent-unity editor stop
  hera-agent-unity editor refresh --compile
  hera-agent-unity editor refresh --force
`)
	case "console":
		fmt.Print(`Usage: hera-agent-unity console [options]

Read Unity console log entries.

Options:
  --lines <N>          Limit to N entries
  --type <types>       Comma-separated log types: error, warning, log (default: error,warning,log)
  --stacktrace <mode>  none: first line only
                        user: with stack trace, internal frames filtered (default)
                        full: raw message including all frames
  --clear              Clear console

Examples:
  hera-agent-unity console
  hera-agent-unity console --lines 20 --type error,warning,log
  hera-agent-unity console --stacktrace user
  hera-agent-unity console --type error --stacktrace full
  hera-agent-unity console --clear
`)
	case "exec":
		fmt.Print(`Usage: hera-agent-unity exec "<code>" [options]

Execute C# code inside Unity Editor. Full access to UnityEngine,
UnityEditor, and all loaded assemblies.

Use 'return' to get output. Add --usings for types outside default namespaces.

Options:
  --usings <ns1,ns2>   Add extra using directives
  --file <path>        Load code from file. Positional/stdin take precedence.
  --csc <path>         Path to csc compiler (csc.dll or csc.exe). Auto-detected if omitted.
  --dotnet <path>      Path to dotnet runtime. Auto-detected if omitted.
  --no-cache           Skip compile/assembly cache; force a fresh csc invocation.
  --check              Compile-only: validate syntax/types and return without
                       executing. Useful for dry-run validation before a real
                       exec — no side effects, no Invoke. Returns success on
                       clean compile, EXEC_COMPILE_ERROR otherwise.
  --stacktrace <mode>  Format of EXEC_RUNTIME_ERROR stack traces:
                         none — exception_type only, no frames
                         user — drop framework frames, collapse the synthetic
                                wrapper to "(your snippet)" (default)
                         full — raw inner.StackTrace verbatim
  --strict             Capture Debug.LogError / LogException / LogAssert
                       raised by the snippet and surface them as
                       EXEC_LOGGED_ERROR even if Execute() returned normally.
                       Without --strict, a 'Debug.LogError(...); return null;'
                       looks identical to a clean run at the exit-code layer.

Default usings: System, System.Collections.Generic, System.IO, System.Linq,
  System.Reflection, System.Threading.Tasks, UnityEngine,
  UnityEngine.SceneManagement, UnityEditor, UnityEditor.SceneManagement,
  UnityEditorInternal

Examples:
  hera-agent-unity exec "return 1+1;"
  hera-agent-unity exec "return Application.dataPath;"
  echo 'return EditorSceneManager.GetActiveScene().name;' | hera-agent-unity exec
  echo 'Debug.Log("hello"); return null;' | hera-agent-unity exec
  hera-agent-unity exec "return World.All.Count;" --usings Unity.Entities
  hera-agent-unity exec --file scripts/probe.cs
  hera-agent-unity exec "var x = MyType.MaybeRenamed();" --check    # validate only

Stdin:
  Pipe code via stdin to avoid shell escaping issues.
  echo '<code>' | hera-agent-unity exec [--usings ns1,ns2]

File:
  --file <path> reads code from disk. Skipped if stdin or a positional code
  argument is already present (those win over --file).

Notes:
  - Use 'return' for output, 'return null;' for void operations
`)
	case "scene":
		fmt.Print(`Usage: hera-agent-unity scene <action> [target] [options]

Actions:
  info                          Show active scene + all loaded scenes (name, path, dirty)
  load <path|name> [--mode M]   Open a scene. Modes: single (default), additive, additive_without_loading
  save [<path|name>]            Save the active scene, or a named loaded scene
  list                          List scenes registered in Build Settings
  close <path|name>             Unload a loaded scene (cannot close the only one)

Target resolution:
  Accepts an asset path (Assets/.../Foo.unity) or a bare scene name.
  Names are resolved through AssetDatabase by exact filename match.

Examples:
  hera-agent-unity scene info
  hera-agent-unity scene load Assets/Scenes/Main.unity
  hera-agent-unity scene load Main --mode additive
  hera-agent-unity scene save
  hera-agent-unity scene close Lobby

Notes:
  - load --mode single fails if the active scene has unsaved changes.
  - close fails if the target scene is dirty; save first.
`)
	case "find_gameobjects":
		fmt.Print(`Usage: hera-agent-unity find_gameobjects [filters] [pagination]

Searches every loaded-scene GameObject (active + inactive by default) and
returns a shallow entry per match. Filters combine with AND.

Filters:
  --name <substr>            Name substring (case-insensitive).
  --tag <name>               Exact tag match (Unity tag system).
  --layer <name|index>       Layer name ('UI') or integer index (0..31).
  --component <type>         Has the given component. Short name ('Rigidbody')
                             or fully-qualified ('UnityEngine.Rigidbody').
  --path_glob <glob>         Hierarchy path glob.
                                *   matches a single segment (no '/')
                                **  matches multiple segments
                                ?   matches a single non-'/' char
                             Path form: '/Root/Child/Name'.
  --include_inactive <bool>  Default true. False = activeInHierarchy only.

Pagination:
  --limit  <N>               Max results (default 50). 0 = no cap.
  --offset <N>               Skip the first N matches (default 0).

Results are sorted by hierarchy path for stable pagination across calls.

Output:
  {
    "total":     <count after filtering, before pagination>,
    "returned":  <count in this page>,
    "offset":    <echoed offset>,
    "limit":     <echoed limit>,
    "has_more":  <true if more pages remain>,
    "results": [
      { "instance_id": ..., "name": "...", "path": "/Root/...",
        "scene": "...", "active": true|false }
    ]
  }

Notes:
  - Prefab assets and HideFlags.HideInHierarchy objects are stripped — only
    things a user would see in the Hierarchy window are returned.
  - Combine with manage_gameobject by feeding 'instance_id' back in
    (survives renames and reparenting; preferred over path).

Examples:
  hera-agent-unity find_gameobjects --name Player
  hera-agent-unity find_gameobjects --tag Enemy --include_inactive false
  hera-agent-unity find_gameobjects --component Rigidbody --limit 20
  hera-agent-unity find_gameobjects --path_glob /Root/**/Pickup
  hera-agent-unity find_gameobjects --layer UI
  hera-agent-unity find_gameobjects --limit 50 --offset 100
`)
	case "manage_packages":
		fmt.Print(`Usage: hera-agent-unity manage_packages <action> [identifier] [flags]

Actions:
  list                            List every package the project resolves to (synchronous).
  add <identifier>                Install a package. identifier accepts any
                                  Client.Add string:
                                    com.unity.x            Latest from registry
                                    com.unity.x@1.2.3      Pinned version
                                    https://.../repo.git   Git URL
                                    https://.../repo.git?path=Subdir
                                    file:../local-path     Local path
                                  Returns a job_id; the CLI polls the
                                  package-result file for up to 10m.
  remove <name>                   Uninstall by package name (async, job_id).
  embed <name>                    Copy a cached package out of
                                  Library/PackageCache into Packages/ so it
                                  becomes locally editable (async, job_id).

Output:
  list — { packages:[{ name, version, source, resolved_path,
                       is_direct_dependency, display_name }] }
  add / remove / embed start — { job_id, port, action, identifier } + message "running"
  add / remove / embed complete (after polling) —
      success: { action, identifier, package:{...} } (no package on remove)
      failure: code PACKAGE_<ACTION>_FAILED + error message

Polling:
  Result file: ~/.hera-agent-unity/status/package-result-<port>-<job_id>.json
  Deleted on read. Domain reloads triggered by the package resolver are
  bridged via [InitializeOnLoad] + a Client.List verifier — the result file
  is still written after the reload settles.

Safety:
  Direct manifest.json edits race the resolver and skip git-URL validation.
  add / remove route through UnityEditor.PackageManager.Client which owns
  the project lock — prefer this over hand-editing manifest.

Examples:
  hera-agent-unity manage_packages list
  hera-agent-unity manage_packages add com.unity.ai.navigation
  hera-agent-unity manage_packages add com.unity.cinemachine@2.9.7
  hera-agent-unity manage_packages add https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
  hera-agent-unity manage_packages remove com.unity.ai.navigation
  hera-agent-unity manage_packages embed com.unity.test-framework

OpenUPM:
  Not directly supported in v0.0.6 — OpenUPM packages require a scoped
  registry entry in manifest.json before Client.Add can resolve them.
  Add the scoped registry once via the Package Manager UI, then use
  'manage_packages add com.author.package' here.
`)
	case "manage_gameobject":
		fmt.Print(`Usage: hera-agent-unity manage_gameobject <action> [flags]

Actions:
  create          Make a new GameObject (empty or primitive).
  destroy         Delete the target GameObject.
  move            Set position (world default; --space local for local).
  set_parent      Reparent or unparent (--parent none).
  set_active      Toggle GameObject.SetActive.
  set_name        Rename.
  get_transform   Read position / rotation (euler) / scale.

Target (required for every action except create):
  --instance_id <N>     Preferred. Survives renames and duplicates.
  --path </Root/Child>  Hierarchy path. Fallback walk covers inactive subtrees.

Flags:
  --name <str>                       Name for create / set_name.
  --primitive <kind>                 cube, sphere, capsule, cylinder, plane, quad.
                                     Omit for an empty GameObject.
  --parent <id|path>                 Parent reference. 'none' or empty unparents
                                     (set_parent only).
  --position x,y,z                   World position for create / move.
                                     Also accepts JSON [x,y,z] or {x,y,z}
                                     via --params.
  --space <world|local>              Coordinate space for move (default: world).
  --active <true|false>              Active state for set_active.
  --world_position_stays <bool>      Match Transform.SetParent flag (default: true).

Return shape (depth 1, every action):
  { instance_id, name, path, scene, scene_path, active,
    transform: { position:{x,y,z}, rotation:{x,y,z}, scale:{x,y,z} } }

Examples:
  hera-agent-unity manage_gameobject create --name Player
  hera-agent-unity manage_gameobject create --name Cube --primitive cube --position 0,1,0
  hera-agent-unity manage_gameobject set_parent --path /Player --parent /Root
  hera-agent-unity manage_gameobject set_parent --path /Player --parent none
  hera-agent-unity manage_gameobject set_active --path /Player --active false
  hera-agent-unity manage_gameobject move --instance_id 12345 --position 5,0,0
  hera-agent-unity manage_gameobject get_transform --path /Root/Player

Notes:
  - Every action marks the scene dirty — save the scene to persist changes.
  - Edits register Undo entries (Ctrl+Z in the editor).
  - GameObjects created in play mode are discarded on play exit (Unity behavior).
`)
	case "menu":
		fmt.Print(`Usage: hera-agent-unity menu "<path>"

Execute a Unity menu item by its path.

Examples:
  hera-agent-unity menu "File/Save Project"
  hera-agent-unity menu "Assets/Refresh"
  hera-agent-unity menu "Window/General/Console"

Note: File/Quit is blocked for safety.
`)
	case "screenshot":
		fmt.Print(`Usage: hera-agent-unity screenshot [options]

Capture a screenshot of the Unity editor.

Options:
  --view <mode>      scene (default), game
  --width <N>        Image width in pixels (default: 1920)
  --height <N>       Image height in pixels (default: 1080)
  --output_path <path>  Output path, absolute or relative to project root
                        (default: Screenshots/screenshot.png)

Examples:
  hera-agent-unity screenshot
  hera-agent-unity screenshot --view game
  hera-agent-unity screenshot --view scene --width 3840 --height 2160
  hera-agent-unity screenshot --output_path captures/my_scene.png
`)
	case "reserialize":
		fmt.Print(`Usage: hera-agent-unity reserialize [path...]

Force Unity to reserialize assets through its own YAML serializer.
Run after editing .prefab, .unity, .asset, or .mat files as text.
No arguments = reserialize the entire project.

Examples:
  hera-agent-unity reserialize
  hera-agent-unity reserialize Assets/Prefabs/Player.prefab
  hera-agent-unity reserialize Assets/Scenes/Main.unity Assets/Scenes/Lobby.unity
`)
	case "profiler":
		fmt.Print(`Usage: hera-agent-unity profiler <subcommand> [options]

Subcommands:
  hierarchy             Top-level profiler samples (last frame)
    --depth <N>         Recursive depth (0=unlimited, default: 1)
    --root <name>       Set root by name (substring match, searches full tree)
    --frames <N>        Average over last N frames (flat output, sorted by time)
    --from <N>          Start frame index for range average
    --to <N>            End frame index for range average
    --parent <ID>       Drill into item by ID
    --min <ms>          Filter items below threshold
    --sort <col>        Sort by: total (default), self, calls
    --max <N>           Max children per level (default: 30)
    --frame <N>         Specific frame index
    --thread <N>        Thread index (0=main)
  enable                Start profiler recording
  disable               Stop profiler recording
  status                Show profiler state
  clear                 Clear all captured frames

Examples:
  hera-agent-unity profiler hierarchy --depth 3
  hera-agent-unity profiler hierarchy --root SimulationSystem --depth 3
  hera-agent-unity profiler hierarchy --frames 30 --min 0.5 --sort self
  hera-agent-unity profiler enable
`)
	case "test":
		fmt.Print(`Usage: hera-agent-unity test [options]

Run Unity tests via the Test Runner API.

Options:
  --mode <EditMode|PlayMode>    Test mode (default: EditMode)
  --filter <name>               Filter by namespace, class, or full test name
                                Must be the full path (e.g. MyNamespace.MyClass)

EditMode tests hold the connection open and return results directly.
PlayMode tests return immediately and poll a results file (domain reload safe).

Requires the Unity Test Framework package (com.unity.test-framework).

Examples:
  hera-agent-unity test
  hera-agent-unity test --mode PlayMode
  hera-agent-unity test --filter MyNamespace.MyTests
  hera-agent-unity test --mode EditMode --filter MyNamespace.MyTests.SpecificTest
`)
	case "log":
		fmt.Print(`Usage: hera-agent-unity log "<message>" [--level <log|warning|error>]

Write a message to the Unity console. Faster than 'exec "Debug.Log(...)"'
because there's no C# compile step.

Options:
  --level <log|warning|error>    Log level (default: log)

Examples:
  hera-agent-unity log "checkpoint A"
  hera-agent-unity log "missing prefab" --level warning
  hera-agent-unity log "build failed" --level error
`)
	case "list":
		fmt.Print(`Usage: hera-agent-unity list [options]

List registered tools (built-in + custom).

Default output is slim — one entry per tool with name, description, and schema.
For full per-tool detail (output_schema + metadata) use --tool <name>.

Options:
  --names            Names only — one line per tool (most token-efficient)
  --tool <name>      Full schema for a single tool

Examples:
  hera-agent-unity list
  hera-agent-unity list --names
  hera-agent-unity list --tool exec
`)
	case "ping":
		fmt.Print(`Usage: hera-agent-unity ping

Token-cheap liveness probe. Reads the heartbeat file only — no Unity HTTP
round-trip and no instance discovery beyond filesystem scan.

Output: single line, e.g. "port=8090 alive=1 state=ready age_ms=42".
Exit code: 0 when alive within 3s; 1 otherwise.

Use 'status' for the richer human-readable view.

Examples:
  hera-agent-unity ping
  hera-agent-unity ping --port 8090
`)
	case "status":
		fmt.Print(`Usage: hera-agent-unity status

Show the current Unity Editor state: port, project path, version, PID.
Reports "not responding" if heartbeat is older than 3 seconds.

Example:
  hera-agent-unity status
`)
	case "doctor":
		fmt.Print(`Usage: hera-agent-unity doctor

Run a self-diagnostic. Reports the running binary path, what 'hera-agent-unity'
resolves to on PATH, duplicate installs, shell-specific gotchas, and any
Unity instances visible to the Connector.

Does NOT require Unity to be running. Use this first when 'hera-agent-unity' is
not found, points at the wrong copy, or can't see your Unity Editor.

Options:
  --json                   Emit structured envelope (binary, shell, unity)
                           for agents.
  --agent-rules            Print the Quick Rules + Pitfalls subset of AGENT.md
                           so you can append it to your project's AI rules file
                           (CLAUDE.md / AGENTS.md / .cursor/rules / ...).
  --format <name>          Output format for --agent-rules:
                             markdown (default) — Claude / Codex / Copilot /
                                                  Continue.dev all accept it
                             cursor             — prepends YAML frontmatter so
                                                  the file is a valid .mdc
                                                  Cursor will actually load

Examples:
  hera-agent-unity doctor
  hera-agent-unity doctor --json
  hera-agent-unity doctor --agent-rules >> CLAUDE.md
  hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc

Environment:
  HERA_AGENT_NO_PATH_CHECK=1   Silence the implicit per-command PATH warning.
`)
	case "update":
		fmt.Print(`Usage: hera-agent-unity update [options]

Update the CLI binary to the latest release from GitHub.

Options:
  --check              Check for updates without installing

Examples:
  hera-agent-unity update
  hera-agent-unity update --check
`)
	case "install":
		fmt.Print(`Usage: hera-agent-unity install

Register the running binary on PATH so subsequent shells can find it.
Copies the binary to the canonical install directory for this OS and
patches PATH (Unix) or relies on WindowsApps (Windows).

  Linux / macOS: ~/.local/bin/hera-agent-unity
  Windows:       %LOCALAPPDATA%\Microsoft\WindowsApps\hera-agent-unity.exe

Legacy install locations from earlier hera-agent / hera-agent-pro
versions are scrubbed automatically.

Examples:
  ./hera-agent-unity-linux-amd64 install
  .\hera-agent-unity-windows-amd64.exe install
`)
	case "batch":
		fmt.Print(`Usage: hera-agent-unity batch [--file <path.json>] [--dry-run]

Execute multiple commands in a single HTTP request to Unity. The whole
batch round-trips together so the response stays atomic and ordered.

Design constraint: batch is for simple sequential execution only. For
conditional branching, data passing between commands, or complex
workflows, use individual CLI calls from a shell script or AI agent.

JSON format:
  {
    "commands": [
      {"command": "manage_editor", "params": {"action": "play"}},
      {"command": "exec",          "params": {"code": "return ..."}}
    ],
    "options": { "fail_fast": true }
  }

Options:
  --file <path>    Path to the JSON batch file. If omitted, reads stdin.
  --dry-run        Print the plan without executing it.

fail_fast:
  true   Stop on first failure; return results up to that point.
  false  Execute every command regardless of individual failures.

Examples:
  hera-agent-unity batch --file ./play_and_test.json
  echo '{"commands":[{"command":"refresh_unity","params":{"compile":"request"}}]}' \
    | hera-agent-unity batch
  hera-agent-unity batch --file ./plan.json --dry-run
`)
	case "asset-config":
		printAssetConfigHelp()
	case "custom-tools", "custom", "tools":
		fmt.Print(`How to write custom tools for hera-agent-unity

Custom tools are C# classes that run inside Unity Editor. The CLI
discovers them automatically via reflection.

Create a static class with [HeraTool] in any Editor assembly:

    using HeraAgent;
    using Newtonsoft.Json.Linq;

    [HeraTool(Description = "Spawn an enemy at a position")]
    public static class SpawnEnemy
    {
        public class Parameters
        {
            [ToolParameter("X world position", Required = true)]
            public float X { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            float x = parameters["x"]?.Value<float>() ?? 0;
            var go = Object.Instantiate(prefab, new Vector3(x, 0, 0), Quaternion.identity);
            return new SuccessResponse("Spawned", new { name = go.name });
        }
    }

Rules:
  - Class must be static
  - Must have: public static object HandleCommand(JObject parameters)
  - Return SuccessResponse(message, data) or ErrorResponse(message)
  - Add Parameters class with [ToolParameter] for discoverability
  - Class name auto-converts to snake_case (SpawnEnemy → spawn_enemy)
  - Override name: [HeraTool(Name = "my_name")]
  - Runs on Unity main thread — all Unity APIs are safe
  - Discovered on Editor start and after every script recompilation
  - Duplicate tool names are detected and logged as errors (first wins)

Namespace gotchas (bare names that collide with 'using System;' + 'using UnityEditor;'):
  - Object       -> qualify as UnityEngine.Object, or 'using Object = UnityEngine.Object;'
  - PackageInfo  -> 'using PackageInfo = UnityEditor.PackageManager.PackageInfo;'
  - Random/Debug -> alias if you reach for them (System vs UnityEngine semantics differ)
  Grep for bare 'Object' / 'PackageInfo' / 'Random' / 'Debug' before the first compile
  to skip a CS0104 hotfix round-trip.
`)
	case "setup":
		fmt.Print(`Installation and Unity setup

CLI Installation:
  # Linux / macOS
  curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | sh

  # Windows (PowerShell)
  irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex

  # Go install (any platform)
  go install github.com/NotNull92/hera-agent-unity@latest

Unity Setup:
  1. Window → Package Manager → + → Add package from git URL
  2. Paste: https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
  The Connector starts automatically when Unity opens.

Verify:
  hera-agent-unity list
`)
	default:
		fmt.Printf("Unknown help topic: %s\n\nUse \"hera-agent-unity --help\" for available commands.\n", topic)
	}
}
