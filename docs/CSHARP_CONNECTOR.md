# C# Connector Internals

This document describes the Unity Editor-side C# codebase that receives CLI commands over HTTP and executes them.

---

## Directory Structure

```
AgentConnector/
└── Editor/
    ├── HttpServer.cs                    # localhost HTTP listener
    ├── CommandRouter.cs                 # command dispatch + locking
    ├── ToolDiscovery.cs                 # reflection-based tool scanning
    ├── Heartbeat.cs                     # instance state file writer
    ├── Core/
    │   ├── Response.cs                  # SuccessResponse, ErrorResponse
    │   ├── ParamCoercion.cs             # parameter type conversion
    │   ├── ToolParams.cs                # typed parameter access helpers
    │   ├── StringCaseUtility.cs          # PascalCase ↔ snake_case
    │   ├── ToolMetadata.cs              # schema metadata registry
    │   └── HeraToolInterfaces.cs        # IHeraTool, BaseHeraTool
    ├── Attributes/
    │   └── HeraToolAttribute.cs          # [HeraTool], [ToolParameter]
    ├── Tools/
    │   ├── ManageEditor.cs              # play, stop, pause, tags, layers
    │   ├── ExecuteCsharp.cs             # C# code execution
    │   ├── ExecuteMenuItem.cs           # Unity menu execution
    │   ├── ReadConsole.cs               # console log reading
    │   ├── RefreshUnity.cs              # asset database refresh
    │   ├── EditorScreenshot.cs          # screenshot capture
    │   ├── DetectAssets.cs              # asset detection
    │   ├── ReserializeAssets.cs         # asset reserialization
    │   └── ManageProfiler.cs            # profiler control
    └── TestRunner/
        ├── RunTests.cs                  # test execution
        └── TestRunnerState.cs           # test result persistence
```

---

## HttpServer.cs

### Role
Lightweight HTTP server on localhost. Receives CLI commands as POST `/command`, dispatches via `CommandRouter`, returns JSON responses.

### Key Characteristics
- Uses `ConcurrentQueue` + `EditorApplication.update` for main-thread marshaling
- Commands execute even when Unity is unfocused
- Survives domain reloads via `[InitializeOnLoad]`

### Port Selection

```csharp
const int DEFAULT_PORT = 8090;
const int FALLBACK_PORT = 8092;
const int MAX_PORT_ATTEMPTS = 10;
```

Tries 8090, then 8092, 8093, ... up to 10 attempts. First available port wins.

### Request Handling Flow

```
ListenLoop (background thread)
    → await GetContextAsync()
    → HandleRequest()
        → Parse JSON body
        → Extract command + parameters
        → Enqueue WorkItem to ConcurrentQueue
        → ForceEditorUpdate() (triggers EditorApplication.update)
        → await TCS.Task (blocks until main thread processes)
        → Serialize result to JSON
        → Write HTTP response
```

### Domain Reload Survival

```csharp
static HttpServer()
{
    Start();
    EditorApplication.quitting += Stop;
    AssemblyReloadEvents.beforeAssemblyReload += StopListener;
    AssemblyReloadEvents.afterAssemblyReload += Start;
    EditorApplication.update += ProcessQueue;
}
```

- `beforeAssemblyReload` → stops the HTTP listener
- `afterAssemblyReload` → restarts the HTTP listener
- `ProcessQueue` runs on every `EditorApplication.update` tick

### Security
- Binds only to `127.0.0.1`
- Rejects browser CORS requests (HTTP 403 if `Origin` header present)
- Blocks `OPTIONS` requests

---

## CommandRouter.cs

### Role
Routes incoming command requests to the appropriate tool handler. Serializes all requests through a single lock to prevent race conditions.

### Locking

```csharp
static readonly SemaphoreSlim s_Lock = new(1, 1);

public static async Task<object> Dispatch(string command, JObject parameters)
{
    await s_Lock.WaitAsync();
    try { return await DispatchInternal(command, parameters); }
    finally { s_Lock.Release(); }
}
```

### Dispatch Flow

1. If `command == "list"` → return tool schemas from `ToolDiscovery.GetToolSchemas()`
2. Find handler via `ToolDiscovery.FindHandler(command)`
3. If handler is static → invoke directly
4. If handler is instance method → create instance via `Activator.CreateInstance()` → invoke
5. If result is `Task<object>` → await it
6. If result is `Task` → await it, return success message
7. Return result (or success message if null)

### Error Handling

All exceptions are caught, logged via `Debug.LogException`, and returned as `ErrorResponse`:

```csharp
catch (Exception ex)
{
    var inner = ex.InnerException ?? ex;
    Debug.LogException(inner);
    return new ErrorResponse($"{command} failed: {inner.Message}");
}
```

---

## ToolDiscovery.cs

### Role
Finds `[HeraTool]` handlers on demand via reflection. No caching, no registration — every call scans live.

### Finding a Handler

```csharp
public static MethodInfo FindHandler(string command)
{
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        foreach (var type in assembly.GetTypes())
        {
            var attr = type.GetCustomAttribute<HeraToolAttribute>();
            if (attr == null) continue;

            var name = attr.Name ?? StringCaseUtility.ToSnakeCase(type.Name);
            if (name != command) continue;

            // Prefer static HandleCommand, fallback to instance
            var method = type.GetMethod("HandleCommand", ... Static ...)
                      ?? type.GetMethod("HandleCommand", ... Instance ...);
            return method;
        }
    }
}
```

### Tool Name Resolution

| C# Class Name | Tool Name |
|:---|:---|
| `ManageEditor` | `manage_editor` |
| `ExecuteCsharp` | `execute_csharp` |
| `EditorScreenshot` | `editor_screenshot` |
| Custom: `[HeraTool(Name = "my_tool")]` | `my_tool` (explicit) |

### Schema Generation

`GetToolSchemas()` returns JSON schema for all discovered tools, including:
- Tool name, description, group
- Parameter types, descriptions, required flags
- Output schema

This is what the CLI `list` command returns.

---

## Heartbeat.cs

### Role
Writes the instance state JSON file every 0.5 seconds so the Go CLI can discover and monitor Unity.

### File Location

```csharp
~/.hera-agent/instances/<md5(projectPath).Substring(0,16)>.json
```

Example: `~/.hera-agent/instances/a1b2c3d4e5f67890.json`

### State Determination

```csharp
static string GetState()
{
    if (EditorApplication.isCompiling) return "compiling";
    if (EditorApplication.isUpdating) return "refreshing";
    if (EditorApplication.isPlaying)
        return EditorApplication.isPaused ? "paused" : "playing";
    return "ready";
}
```

### Forced States

Certain operations force a temporary state to prevent CLI from seeing premature "ready":

| Event | Forced State | Duration |
|:---|:---|:---|
| `beforeAssemblyReload` | `"reloading"` | Until next tick |
| `ExitingEditMode` | `"entering_playmode"` | Until next tick |
| `MarkCompileRequested()` | `"compiling"` | 3-second grace period |

---

## Response Types (Core/Response.cs)

### SuccessResponse

```csharp
public class SuccessResponse
{
    public bool success = true;
    public string message;
    public object data;
}
```

### ErrorResponse

```csharp
public class ErrorResponse
{
    public bool success = false;
    public string message;
    public object data = null;
}
```

All tool handlers return either `SuccessResponse`, `ErrorResponse`, or a raw object (which gets JSON-serialized).

---

## Built-in Tools Summary

| Tool | Class | Key Actions |
|:---|:---|:---|
| `manage_editor` | `ManageEditor.cs` | play, stop, pause, set_active_tool, add_tag, remove_tag, add_layer, remove_layer |
| `execute_csharp` | `ExecuteCsharp.cs` | Compile and run C# code inside Unity |
| `execute_menu_item` | `ExecuteMenuItem.cs` | Execute Unity menu items by path |
| `read_console` | `ReadConsole.cs` | Read/filter/clear console logs |
| `refresh_unity` | `RefreshUnity.cs` | AssetDatabase.Refresh |
| `editor_screenshot` | `EditorScreenshot.cs` | Capture scene/game view |
| `detect_assets` | `DetectAssets.cs` | Auto-detect project assets |
| `reserialize_assets` | `ReserializeAssets.cs` | Force asset reserialization |
| `manage_profiler` | `ManageProfiler.cs` | Enable/disable/capture profiler data |
| `run_tests` | `RunTests.cs` | Execute Unity Test Framework tests |

---

## Domain Reload Notes

When Unity compiles scripts, the entire AppDomain is reloaded:
- All static variables reset
- All instances destroyed
- HTTP listener must be stopped before reload, restarted after

Components marked `[InitializeOnLoad]` automatically re-initialize after reload. This is why `HttpServer`, `Heartbeat`, and `TestRunnerState` all use this attribute.

---

## Related Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) — Writing new tools
- [`COMMANDS.md`](COMMANDS.md) — Command reference
