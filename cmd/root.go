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
)

var Version = "dev"

var (
	flagPort    int
	flagProject string
	flagTimeout int
	flagVerbose bool
)

func Execute() error {
	flag.IntVar(&flagPort, "port", 0, "Select Unity instance by active heartbeat port")
	flag.StringVar(&flagProject, "project", "", "Select Unity instance by project path")
	flag.IntVar(&flagTimeout, "timeout", 60000, "Request timeout in milliseconds")
	flag.BoolVar(&flagVerbose, "verbose", false, "Print progress + per-phase timings to stderr")

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

	category := cmdArgs[0]
	subArgs := cmdArgs[1:]

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
		if command == "exec" {
			fmt.Fprintln(os.Stderr, "[hera-agent-unity] compiling...")
		}
		return sendWithProgress(inst, command, params, timeout, flagVerbose)
	}

	var resp *client.CommandResponse

	switch category {
	case "batch":
		return batchCmd(context.Background(), subArgs, client.SendBatch, resolve)
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
	case "exec":
		subArgs, err = readExecFileIfPresent(subArgs)
		if err != nil {
			return err
		}
		subArgs = readStdinIfPiped(subArgs)
		var params map[string]interface{}
		params, err = buildParams(subArgs, nil)
		if err == nil {
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
		msg := resp.Message
		if msg == "" {
			msg = "unknown error"
		}
		prefix := "Error"
		if resp.Code != "" {
			prefix = "Error [" + resp.Code + "]"
		}
		if len(resp.Data) > 0 && string(resp.Data) != "null" {
			fmt.Fprintf(os.Stderr, "%s: %s\nDetails: %s\n", prefix, msg, string(resp.Data))
		} else {
			fmt.Fprintf(os.Stderr, "%s: %s\n", prefix, msg)
		}
		for _, s := range resp.Suggestions {
			fmt.Fprintf(os.Stderr, "  Hint: %s\n", s)
		}
		return
	}
	if resp.AgentHint != "" {
		fmt.Fprintf(os.Stderr, "[hera-agent-unity] hint: %s\n", resp.AgentHint)
	}

	if len(resp.Data) > 0 && string(resp.Data) != "null" {
		var pretty interface{}
		if json.Unmarshal(resp.Data, &pretty) == nil {
			// If data is a plain string, print it raw (preserves newlines for tree output etc.)
			if s, ok := pretty.(string); ok {
				fmt.Println(s)
			} else {
				b, _ := json.MarshalIndent(pretty, "", "  ")
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
func readStdinIfPiped(args []string) []string {
	info, err := os.Stdin.Stat()
	if err != nil {
		return args
	}
	if info.Mode()&os.ModeCharDevice != 0 {
		return args // interactive terminal, not piped
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
		case "--verbose":
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

Update:
  update                        Update to the latest version
  update --check                Check for updates without installing

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
  --json   Emit structured envelope (binary, shell, unity) for agents.

Examples:
  hera-agent-unity doctor
  hera-agent-unity doctor --json

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
`)
	case "setup", "install":
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
