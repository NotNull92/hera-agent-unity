# Go CLI Internals

This document describes the Go CLI codebase structure, execution flow, and key functions.

---

## Directory Structure

```
cmd/                  # Cobra-free command implementation
  root.go             # Entry point, flag/arg parsing, default passthrough
  editor.go           # editor command (waitForReady polling)
  test.go             # test command (PlayMode result polling)
  status.go           # status, waitForAlive, heartbeat reading
  update.go           # self-update from GitHub releases
  version_check.go    # periodic update notice (12h interval)
  asset_config.go     # asset-config subcommand
  *_test.go           # Unit tests for each command

internal/
  client/
    client.go              # HTTP client, instance discovery
    client_test.go         # Unit tests
    client_integration_test.go  # Integration tests (requires Unity)
    process_unix.go        # Unix PID alive check
    process_windows.go     # Windows PID alive check
  assetconfig/
    config.go              # asset-config.json read/write
  tui/
    assetconfig.go         # bubbletea TUI for asset-config
```

---

## Execution Flow (root.go)

```go
// main.go → cmd.Execute()
func Execute() error {
    // 1. Parse global flags (--port, --project, --timeout)
    flagArgs, cmdArgs := splitArgs(os.Args[1:])
    flag.CommandLine.Parse(flagArgs)

    // 2. Extract category and sub-args
    category := cmdArgs[0]   // e.g., "editor", "exec", "test", "status"
    subArgs  := cmdArgs[1:]

    // 3. Handle special commands that don't need Unity
    switch category {
        case "status":   statusCmd(inst)
        case "update":   updateCmd(subArgs)
        case "asset-config": assetConfigCmd(subArgs)
        case "version":  print version
        case "help":     print help
    }

    // 4. Discover Unity instance from instance files
    inst, _ := client.DiscoverInstance(flagProject, flagPort)

    // 5. Wait for Unity to be alive
    waitForAlive(resolve, flagTimeout)

    // 6. Send command via HTTP
    resp, err := send(category, params)

    // 7. Print response + update notice
    printResponse(resp)
    printUpdateNotice()
}
```

---

## Key Functions in root.go

| Function | Role |
|:---|:---|
| `Execute()` | Entry point. Parses flags → discovers instance → dispatches command → prints response. |
| `printResponse()` | Formats Unity JSON response for terminal. Plain strings print raw. Objects print indented JSON. |
| `buildParams()` | Converts `--key value` pairs into a map. Supports `--params '{"k":"v"}'` for raw JSON. |
| `parseSubFlags()` | Extracts `--flag` and `--flag value` pairs from subcommand args. Boolean flags get `"true"`. |
| `splitArgs()` | Separates global flags (`--port`, `--project`, `--timeout`) from subcommand args. |
| `readStdinIfPiped()` | Reads stdin when piped (e.g., `echo 'code' \| hera-agent-unity-unity exec`). Detects pipe via `os.ModeCharDevice`. |

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
| `play` | `manage_editor` + `action=play` + `wait_for_completion` | `--wait` blocks until `EnteredPlayMode` |
| `stop` | `manage_editor` + `action=stop` | `--wait` blocks until `EnteredEditMode` |
| `pause` | `manage_editor` + `action=pause` | Toggle pause/resume |
| `refresh` | `refresh_unity` + mode/force | `--force` allows refresh during play mode |
| `refresh --compile` | `refresh_unity` + `compile=request` | Triggers compilation, then `waitForReady()` |

### status.go

| Function | Role |
|:---|:---|
| `statusCmd()` | Reads instance file and prints JSON state |
| `waitForAlive()` | Polls instance files until Unity is alive (or timeout) |
| `waitForReady()` | Polls instance files until `state == "ready"`. Returns `compileErrors` status. |

### test.go

| Mode | Flow |
|:---|:---|
| EditMode | Synchronous execution. Direct response. |
| PlayMode | Asynchronous. Returns `"running"` immediately. CLI polls `~/.hera-agent-unity-unity/status/test-results-<port>.json` for results. |

### update.go

1. Calls GitHub API `releases/latest`
2. Downloads asset for current OS/arch
3. Backs up current binary
4. Atomically renames new binary
5. Removes old binary on success

### version_check.go

- Checks GitHub API every 12 hours
- Caches result in `~/.hera-agent-unity-unity/version-check.json`
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
4. If response body is empty (connection closed early) → returns error (some commands like play mode entry had this bug on Unity side)
5. If response is not JSON → wraps it in `SuccessResponse`

### CommandResponse

```go
type CommandResponse struct {
    Success bool            `json:"success"`
    Message string          `json:"message"`
    Data    json.RawMessage `json:"data,omitempty"`
}
```

| Field | Meaning |
|:---|:---|
| `Success` | Whether the command succeeded |
| `Message` | Human-readable message |
| `Data` | Tool-specific JSON data (raw, unmarshal into your type) |

---

## Testing

Unit tests use injected `sendFn` and `instanceResolver` functions to avoid real Unity connections:

```go
func TestEditorPlay(t *testing.T) {
    mockSend := func(cmd string, params interface{}) (*client.CommandResponse, error) {
        return &client.CommandResponse{Success: true, Message: "OK"}, nil
    }
    resp, err := editorCmd([]string{"play"}, mockSend, nil)
    // assertions...
}
```

Integration tests (require Unity open) are tagged with `//go:build integration`.

---

## Related Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`COMMANDS.md`](COMMANDS.md) — Command reference
