# System Architecture

This document describes how the Go CLI and C# Unity connector communicate, how state is managed, and the data flow for every operation.

---

## Overall Architecture

```
├──────────────────────┐          HTTP POST         ┌───────────────────────────┐
│   Go CLI Binary        │  ▷───────────◁  │   Unity Editor (C#)         │
│   (thin core)          │  localhost:8090+   │   - HttpServer                │
│                        │                   │   - CommandRouter             │
│  • cmd/               │                   │   - ToolDiscovery             │
│  • internal/          │                   │   - Heartbeat                 │
│  • tools/ (registry)  │                   │   - [HeraTool] classes        │
└──────────────────────┘                   └───────────────────────────┘
           ▲                                                 │
           │                                                 │
           │         ~/.hera-agent-unity/instances/*.json     │
           └──────────────────────────────────────────────┘
```

---

## Data Flow

### 1. Initial Connection

1. Unity Editor opens → `HttpServer` starts on an available localhost port (8090 default, then 8091–8099)
2. `Heartbeat` writes `~/.hera-agent-unity/instances/<md5(projectPath)>.json` every 1.0 second
3. CLI scans the instances directory via `internal/client.ScanInstances()`
4. CLI discovers the Unity instance and connects

### 2. Command Execution

```
[Terminal]    hera-agent-unity editor play --wait
     │
     ▷  ① root.go: splitArgs() → strip --port, --project, --timeout flags
     │
     ▷  ② root.go: category="editor", subArgs=["play","--wait"]
     │
     ▷  ③ client.DiscoverInstance() → cached one-shot read of instance JSON files
     │
     ▷  ④ waitForAlive() → fresh-polls instance files until Unity is alive
     │
     ▷  ⑤ editorCmd() → build params: {"action":"play"}  // --wait handled Go-side via waitForState
     │
     ▷  ⑥ client.Send(ctx, ...) → HTTP POST /command (JSON body)
     │
     ▷  ⑦ Unity HttpServer.HandleRequest() → enqueue WorkItem to ConcurrentQueue
     │
     ▷  ⑧ EditorApplication.update(ProcessQueue) → CommandRouter.Dispatch()
     │
     ▷  ⑨ ToolDiscovery.FindHandler("manage_editor") → ManageEditor.HandleCommand
     │
     ▷  ⑩ ManageEditor.play → EditorApplication.isPlaying = true; SuccessResponse returned immediately
     │
     ▷  ⑪ JSON response returned to Go (HTTP listener may die mid-response due to domain reload — retry absorbs it)
     │
     ▷  ⑫ Go: if --wait, waitForState(resolve, 60000, "playing", "paused") polls heartbeat until isPlaying observed
     │
     ▷  ⑬ printResponse() with "Entered play mode (confirmed)." message
```

---

## Core Components

| Component | Role | File/Folder |
|:---|:---|:---|
| Go CLI | Command parsing, HTTP request, response output | `cmd/`, `internal/` |
| HTTP Client | Unity instance discovery, polling, timeout handling | `internal/client/client.go` |
| HttpServer | Unity-side localhost HTTP listener | `AgentConnector/Editor/HttpServer.cs` |
| CommandRouter | Prevents concurrent execution (SemaphoreSlim), dispatches to handlers | `AgentConnector/Editor/CommandRouter.cs` |
| ToolDiscovery | Reflection-based tool scanning and schema generation | `AgentConnector/Editor/ToolDiscovery.cs` |
| Heartbeat | Writes instance state JSON files, survives domain reloads | `AgentConnector/Editor/Heartbeat.cs` |

---

## Unity State Machine

```
[*] → ready          : Unity starts
ready → compiling     : Script modified/added
compiling → ready     : Compile success
compiling → ready     : Compile finishes (inspect `compileErrors` for failure)
ready → entering_playmode : editor play
entering_playmode → playing : EnteredPlayMode event
playing → paused      : editor pause
paused → playing      : editor pause (toggle)
playing → ready       : editor stop
ready → refreshing    : AssetDatabase.Refresh
refreshing → ready    : Complete
ready → stopped      : Unity exits
* → reloading         : beforeAssemblyReload forced heartbeat
```

States are written to the instance JSON file by `Heartbeat.cs`. The Go CLI fresh-polls this file via `waitForAlive()` and `waitForReady()` during transitions; one-shot command setup keeps the short-lived instance cache.

---

## Domain Reload Survival

Unity's script compilation / domain reload resets static variables and instances. Critical components survive via `[InitializeOnLoad]` + `AssemblyReloadEvents`.

| Component | Survival Mechanism | Notes |
|:---|:---|:---|
| `HttpServer` | `[InitializeOnLoad]` + `afterAssemblyReload += Start` | Auto-restarts after domain reload |
| `Heartbeat` | `[InitializeOnLoad]` re-registers the update callback after reload | Resumes writing state files; writes `reloading` before reload |
| `TestRunnerState` | `[InitializeOnLoad]` + `afterAssemblyReload += OnAfterAssemblyReload` | Preserves the asynchronous test-result path across reloads |
| `CommandRouter` | Static class, no state | Re-created each dispatch, uses SemaphoreSlim |

---

## Instance File Format

`~/.hera-agent-unity/instances/<hash>.json`:

```json
{
  "state": "ready",
  "projectPath": "/Users/admin/Unity/MyProject",
  "port": 8090,
  "pid": 12345,
  "unityVersion": "2022.3.45f1",
  "docsVersion": "2022.3",
  "compiler": {
    "cscPath": "/Unity/Editor/Data/DotNetSdkRoslyn/csc.dll",
    "cscKind": "unity_dotnet_sdk_roslyn",
    "cscFound": true,
    "dotnetPath": "/Unity/Editor/Data/NetCoreRuntime/dotnet",
    "dotnetKind": "unity_netcore_runtime",
    "dotnetFound": true
  },
  "timestamp": 1714372800000,
  "compileErrors": false
}
```

| Field | Source | Notes |
|:---|:---|:---|
| `state` | `s_ForcedState ?? Heartbeat.GetState()` | ready / compiling / entering_playmode / playing / paused / refreshing / reloading / stopped |
| `projectPath` | `Application.dataPath.Replace("/Assets","")` | Project root directory |
| `port` | `HttpServer.Port` | Actual listening port |
| `pid` | `Process.GetCurrentProcess().Id` | Unity process ID |
| `unityVersion` | `Application.unityVersion` | Unity version string |
| `docsVersion` | `UnityVersionCompat.CurrentDocsVersion()` | Connector documentation bucket |
| `compiler` | `Heartbeat.GetCompilerSummary()` | Resolved csc/dotnet paths, kinds, and availability |
| `timestamp` | `DateTimeOffset.UtcNow` | Unix epoch milliseconds |
| `compileErrors` | `EditorUtility.scriptCompilationFailed` | True if last compilation failed |

Stale files (PID not running) are auto-deleted by `client.ScanInstances()`.

---

## Concurrent Execution Prevention

`CommandRouter` uses a static `SemaphoreSlim(1, 1)` to serialize all commands:

```csharp
static readonly SemaphoreSlim s_Lock = new(1, 1);

public static async Task<object> Dispatch(string command, JObject parameters)
{
    await s_Lock.WaitAsync();
    try { return await DispatchInternal(command, parameters); }
    finally { s_Lock.Release(); }
}
```

This prevents race conditions when multiple CLI agents or parallel scripts access the same Unity instance.

## HTTP Ingress Contract

The listener remains loopback-only and supports one Editor command stream. It bounds ingress before the main-thread queue: `/command` accepts up to 1 MiB, `/commands` up to 4 MiB and 50 command items, and at most 64 requests may be pending execution. Bodies are read incrementally, so an unknown-length request cannot bypass the byte limit.

Ingress rejections are JSON `ErrorResponse` envelopes with stable `HTTP_*` codes and their matching HTTP status (`400`, `403`, `404`, `405`, `413`, `429`, or `500`). The Go client decodes those non-200 envelopes into its normal response types, preserving `success`, `code`, `message`, and `data`; a malformed non-200 body remains a transport error. Tool-level command failures continue to use their existing response contract. The router's 120-second command-lock acquisition timeout is unchanged.

## Test Result Flow

`test --mode EditMode` and `test --mode PlayMode` both start the Unity Test
Framework asynchronously. Their final envelope is persisted as
`~/.hera-agent-unity/status/test-results-<port>-<run_id>.json`; the Go CLI polls that
file until it is available. This unifies the modes and makes the result
delivery resilient to a PlayMode domain reload. There is no test-specific
`--wait` flag; the global `--timeout` bounds the polling interval.

During CLI/connector upgrades, the connector also writes the legacy
port-scoped result file for PlayMode clients that do not yet understand a
`run_id`; current clients prefer the run-scoped file.
Current CLIs opt into asynchronous EditMode results with `async_results=true`;
without it, the connector preserves the legacy synchronous EditMode contract.

---

## Security Considerations

| Layer | Protection |
|:---|:---|
| **Network** | Only binds to `127.0.0.1` (localhost). No remote access. |
| **CORS** | Browser `Origin` headers are rejected with HTTP 403. Only CLI HTTP clients work. |
| **File** | Instance files written to user's home directory. No privileged paths. |
| **Command** | `File/Quit` menu item is explicitly blocked in `ExecuteMenuItem.cs`. |

---

## Related Documentation

- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`COMMANDS.md`](COMMANDS.md) — Command reference
- [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) — Extending with custom tools
