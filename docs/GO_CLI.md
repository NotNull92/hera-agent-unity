# Go CLI Internals

This document describes the Go CLI codebase structure, execution flow, and key functions.

---

## Directory Structure

```
cmd/                  # Cobra-free command implementation
  root.go             # Entry point, flag/arg parsing, response printing
  dispatch.go         # Standalone / Unity-backed command routing
  editor.go           # editor command (waitForReady polling)
  test.go             # test command (PlayMode result polling)
  status.go           # status, waitForAlive, waitForReady, ping
  update.go           # self-update from GitHub releases
  version_check.go    # periodic update notice (12h interval)
  asset_config.go     # asset-config subcommand
  batch.go            # batch command execution
  ui_doc.go           # ui_doc dispatch + CLI-side sample/catalog
  manage_packages.go  # async package job polling
  unity_docs.go       # unity_docs passthrough
  doctor.go           # self-diagnostic
  install.go          # install hook
  uninstall.go        # uninstall hook
  help.go             # embedded help topic loader
  *_test.go           # Unit tests for each command

internal/
  client/
    client.go              # HTTP client, instance discovery, batch sending
    client_test.go         # Unit tests
    client_integration_test.go  # Integration tests (requires Unity)
    process_unix.go        # Unix PID alive check
    process_windows.go     # Windows PID check
  assetconfig/
    config.go              # asset-config.json read/write
  tui/
    assetconfig.go         # bubbletea TUI for asset-config
  poll/
    poll.go                # async job result polling (tests, packages)
  paths/
    paths.go               # hera state/config path helpers
  unitystate/
    state.go               # Unity state constants
  logutil/
    suppress.go            # log suppression helpers
```

---

## Execution Flow (root.go + dispatch.go)

```go
// main.go → cmd.Execute()
func Execute() error {
    // 1. Parse global flags (--port, --project, --timeout, --verbose, ...)
    flagArgs, cmdArgs := splitArgs(os.Args[1:])
    flag.CommandLine.Parse(flagArgs)

    // 2. Extract category and sub-args
    category := cmdArgs[0]   // e.g., "editor", "exec", "test", "status"
    subArgs  := cmdArgs[1:]

    // 3. Handle special commands that don't need Unity
    switch category {
        case "status":    statusCmd(inst)
        case "update":    updateCmd(subArgs)
        case "asset-config": assetConfigCmd(subArgs)
        case "version":   print version
        case "help":      print help
        case "ping":      pingCmd(...)
        case "doctor":    doctorCmd(subArgs)
        case "install":   installCmd()
        case "uninstall": uninstallCmd()
    }

    // 4. Discover Unity instance from instance files
    inst, _ := client.DiscoverInstance(flagProject, flagPort)

    // 5. Wait for Unity to be alive
    waitForAlive(resolve, flagTimeout, category)

    // 6. Send command via HTTP (or special-case batch/editor/test/manage_packages/unity_docs/ui_doc)
    resp, err := runUnityCommand(ctx, category, subArgs, send, resolve)

    // 7. Print response + update notice
    printer.Print(resp, category)
    printUpdateNotice(category)
}
```

---

## Key Functions in root.go

| Function | Role |
|:---|:---|
| `Execute()` | Entry point. Parses flags → discovers instance → dispatches command → prints response. |
| `runStandaloneCommand()` | Handles commands that don't need a live Unity connection. Lives in `dispatch.go`. |
| `runUnityCommand()` | Handles commands that require a live Unity connection (including batch and file-injection for exec/ui_doc). Lives in `dispatch.go`. |
| `ResponsePrinter.Print()` | Formats Unity JSON response for terminal. Plain strings print raw. Objects print indented or compact JSON depending on command category. |
| `printTimings()` | Prints per-phase timings to stderr when `--verbose` is set. |
| `buildParams()` | Converts `--key value` pairs into a map. Supports `--params '{"k":"v"}'` for raw JSON. |
| `splitArgs()` | Separates global flags (`--port`, `--project`, `--timeout`, `--verbose`, etc.) from subcommand args. |
| `readStdinIfPiped()` | Reads stdin when piped (e.g., `echo 'code' \| hera-agent-unity exec`). Detects pipe via `os.ModeCharDevice`. |
| `readExecFileIfPresent()` | Strips `--file <path>` and prepends file contents as the first positional arg. |
| `prepareSend()` | Builds a `SendFunc` closure that re-resolves the instance on every call so port rebinds during domain reload are followed transparently. |
| `makeResolver()` | Returns an `instanceResolver` that follows the same project even if Unity rebinds to a new port during reload. |
| `isHumanCommand()` / `shouldNarrate()` | Gate styled stderr, progress messages, and wait narration by command category. |
| `isUserCodeDiagnostic()` | Reframes exec-related error output so snippet failures don't read as tool failures. |

### Parameter Type Coercion

`buildParams()` converts string values to Go types:

| Input String | Output Type |
|:---|:---|
| `"123"` | `int` (if `strconv.Atoi` succeeds) |
| `"true"` / `"false"` | `bool` |
| `"hello"` | `string` |
| `"1.5"` | `string` (float is not auto-converted) |

> **Note**: Floats are sent as strings to Unity. C# side uses `float.TryParse` to convert.

---

## Command Files

### editor.go

| Action | What it sends | Notes |
|:---|:---|:---|
| `play` | `manage_editor` + `action=play` | `--wait` polls the heartbeat via `waitForState("playing", "paused")` after send — C# can't confirm `EnteredPlayMode` over HTTP because the domain reload stops the listener mid-response |
| `stop` | `manage_editor` + `action=stop` | No `--wait` — stop is fire-and-forget |
| `pause` | `manage_editor` + `action=pause` | Toggle pause/resume |
| `refresh` | `refresh_unity` + mode/force | `--force` allows refresh during play mode |
| `refresh --compile` | `refresh_unity` + `compile=request` | Triggers compilation, then `waitForReady()` bounded by `--timeout` |

### status.go

| Function | Role |
|:---|:---|
| `statusCmd()` | Reads instance file and prints JSON state |
| `pingCmd()` | Token-cheap liveness probe; reads heartbeat file directly (no Unity HTTP round-trip) |
| `waitForAlive()` | Polls instance files until Unity is alive (or timeout) |
| `waitForReady()` | Polls instance files until `state == "ready"`. Returns `compileErrors` status. |
| `waitForState()` | Polls heartbeat until state matches one of the target values. |

### test.go

| Mode | Flow |
|:---|:---|
| EditMode | Synchronous execution. Direct response. |
| PlayMode | Asynchronous. Returns `"running"` immediately. CLI polls `~/.hera-agent-unity/status/test-results-<port>.json` for results. |

### manage_packages.go

Dispatches to `manage_packages` on the connector. `list` is synchronous; `add` / `remove` / `embed` return a `job_id` and the CLI polls `~/.hera-agent-unity/status/package-result-<port>-<job_id>.json` via `internal/poll.WaitForAsyncJob`.

### batch.go

Reads batch JSON from `--file` or stdin, unmarshals into `client.BatchCommandRequest`, and sends it to `POST /commands`. Results are printed per step; `fail_fast` stops at the first failure.

### ui_doc.go

`export` / `apply` / `import` / `gen_sprite` / `capture` are simple passthroughs to the connector. `apply` and `import` read their document from `--file` so the potentially large doc never rides inline in the agent's context. `sample` and `catalog` are handled entirely CLI-side: they decode reference images and scan UI-asset folders with no Unity round-trip.

### unity_docs.go

Simple passthrough to the `unity_docs` connector tool for offline Unity ScriptReference lookup.

### doctor.go

Self-diagnostic: binary path checks, duplicate-install detection, connector visibility, and (with `--agent-rules`) AGENTS.md content generation.

### update.go

1. Calls GitHub API `releases/latest`
2. Downloads asset for current OS/arch
3. Backs up current binary
4. Atomically renames new binary
5. Removes old binary on success

### version_check.go

- Checks GitHub API every 12 hours
- Caches result in `~/.hera-agent-unity/version-check.json`
- Prints update notice to stderr if newer version exists

---

## Instance Discovery (internal/client/client.go)

### Instance Struct

```go
type Instance struct {
    State         string `json:"state"`
    ProjectPath   string `json:"projectPath"`
    Port          int    `json:"port"`
    PID           int    `json:"pid"`
    UnityVersion  string `json:"unityVersion,omitempty"`
    Timestamp     int64  `json:"timestamp,omitempty"`
    CompileErrors bool   `json:"compileErrors,omitempty"`
}
```

### Discovery Priority

1. If `--port N` given → find active instance on that exact port
2. If `--project <path>` given → find instance whose project path contains the substring
3. If current working directory matches a project path → use that instance
4. Otherwise → return the most recently updated active instance

### Dead Instance Cleanup

`ScanInstances()` checks each instance's PID via OS-specific `checkProcessDead()`. If the process is confirmed dead, the JSON file is deleted.

---

## HTTP Sending (client.Send)

```go
func Send(inst *Instance, command string, params interface{}, timeoutMs int) (*CommandResponse, error)
```

1. Marshals `CommandRequest{Command, Params}` to JSON
2. POSTs to `http://127.0.0.1:<port>/command`
3. HTTP client timeout = `timeoutMs` milliseconds
4. Transparently retries while Unity's HTTP listener is down between domain reloads (`doWithReloadRetry`)
5. If response is not JSON → wraps it in `SuccessResponse`

### CommandResponse

```go
type CommandResponse struct {
    Success     bool            `json:"success"`
    Message     string          `json:"message"`
    Code        string          `json:"code,omitempty"`
    Suggestions []string        `json:"suggestions,omitempty"`
    AgentHint   string          `json:"agent_hint,omitempty"`
    Data        json.RawMessage `json:"data,omitempty"`
    Timings     map[string]int64 `json:"timings,omitempty"`
}
```

| Field | Meaning |
|:---|:---|
| `Success` | Whether the command succeeded |
| `Message` | Human-readable message |
| `Code` | Stable error enum (e.g. `EXEC_COMPILE_ERROR`, `UNKNOWN_COMMAND`) |
| `Suggestions` | Next-step hints on errors |
| `AgentHint` | One-line operational nudge for agent consumers |
| `Data` | Tool-specific JSON data (raw, unmarshal into your type) |
| `Timings` | Optional phase measurements (compile_ms, execute_ms, serialize_ms, total_ms) |

---

## Testing

Unit tests use injected `sendFn` and `instanceResolver` functions to avoid real Unity connections:

```go
func TestEditorPlay(t *testing.T) {
    mockSend := func(cmd string, params interface{}) (*client.CommandResponse, error) {
        return &client.CommandResponse{Success: true, Message: "OK"}, nil
    }
    resp, err := editorCmd([]string{"play"}, mockSend, nil, "editor")
    // assertions...
}
```

Integration tests (require Unity open) are tagged with `//go:build integration`.

---

## Related Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`COMMANDS.md`](COMMANDS.md) — Command reference
