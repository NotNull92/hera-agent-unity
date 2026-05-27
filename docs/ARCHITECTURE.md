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

1. Unity Editor opens → `HttpServer` starts on an available localhost port (8090 default, falls back to 8092+)
2. `Heartbeat` writes `~/.hera-agent-unity/instances/<md5(projectPath)>.json` every 0.5 seconds
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
     ▷  ③ client.DiscoverInstance() → reads instance JSON files
     │
     ▷  ④ waitForAlive() → polls instance files until Unity is alive
     │
     ▷  ⑤ editorCmd() → build params: {"action":"play","wait_for_completion":true}
     │
     ▷  ⑥ client.Send() → HTTP POST /command (JSON body)
     │
     ▷  ⑦ Unity HttpServer.HandleRequest() → enqueue WorkItem to ConcurrentQueue
     │
     ▷  ⑧ EditorApplication.update(ProcessQueue) → CommandRouter.Dispatch()
     │
     ▷  ⑨ ToolDiscovery.FindHandler("manage_editor") → ManageEditor.HandleCommand
     │
     ▷  ⑩ ManageEditor.play → EditorApplication.isPlaying = true
     │
     ▷  ⑪ PlayModeStateChange.EnteredPlayMode event → TCS.SetResult()
     │
     ▷  ⑫ JSON response returned to Go → printResponse()
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
compiling → compiling_error : Compile failure
compiling_error → compiling : Fix and recompile
ready → entering_playmode : editor play
entering_playmode → playing : EnteredPlayMode event
playing → paused      : editor pause
paused → playing      : editor pause (toggle)
playing → ready       : editor stop
ready → refreshing    : AssetDatabase.Refresh
refreshing → ready    : Complete
ready → stopped      : Unity exits
```

States are written to the instance JSON file by `Heartbeat.cs`. The Go CLI polls this file via `waitForAlive()` and `waitForReady()`.

---

## Domain Reload Survival

Unity's script compilation / domain reload resets static variables and instances. Critical components survive via `[InitializeOnLoad]` + `AssemblyReloadEvents`.

| Component | Survival Mechanism | Notes |
|:---|:---|:---|
| `HttpServer` | `[InitializeOnLoad]` + `afterAssemblyReload += Start` | Auto-restarts after domain reload |
| `Heartbeat` | `[InitializeOnLoad]` + `afterAssemblyReload += Tick` | Continues writing state files |
| `TestRunnerState` | `[InitializeOnLoad]` + `afterAssemblyReload += OnAfterAssemblyReload` | Preserves PlayMode test results |
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
  "timestamp": 1714372800000,
  "compileErrors": false
}
```

| Field | Source | Notes |
|:---|:---|:---|
| `state` | `Heartbeat.GetState()` | ready / compiling / entering_playmode / playing / paused / refreshing / stopped |
| `projectPath` | `Application.dataPath.Replace("/Assets","")` | Project root directory |
| `port` | `HttpServer.Port` | Actual listening port |
| `pid` | `Process.GetCurrentProcess().Id` | Unity process ID |
| `unityVersion` | `Application.unityVersion` | Unity version string |
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
