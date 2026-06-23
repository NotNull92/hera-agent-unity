# C# Connector Internals

This document describes the Unity Editor-side C# codebase that receives CLI commands over HTTP and executes them.

---

## Directory Structure

```
AgentConnector/
└── Editor/
    ├── HttpServer.cs                    # localhost HTTP listener
    ├── CommandRouter.cs                 # command dispatch + locking + batch
    ├── ToolDiscovery.cs                 # reflection-based tool scanning and schemas
    ├── Heartbeat.cs                     # instance state file writer
    ├── HeraAgentAssetConfigWindow.cs    # Settings window (Hera > Settings, Ultra Hera + asset config)
    ├── HeraAgentAssetConfigWindow.Model.cs
    ├── HeraAgentAssetConfigWindow.View.cs
    ├── Attributes/
    │   ├── HeraToolAttribute.cs          # [HeraTool], [ToolParameter]
    │   └── HeraActionAttribute.cs        # [HeraAction] action handler marker
    ├── Core/
    │   ├── Response.cs                  # SuccessResponse, ErrorResponse, ResponseTimings
    │   ├── ParamCoercion.cs             # bool coercion from JSON tokens
    │   ├── ToolParams.cs                # typed parameter access helpers + Result<T>
    │   ├── StringCaseUtility.cs          # PascalCase ↔ snake_case
    │   ├── ToolMetadata.cs              # schema metadata registry
    │   ├── SchemaUtility.cs             # C# type → JSON Schema type mapping
    │   ├── SerializedPropertyValue.cs   # JSON ↔ SerializedProperty bridge
    │   ├── ComponentTypeResolver.cs     # short/full component name → System.Type
    │   ├── HierarchyPath.cs             # Transform path build/find (inactive fallback)
    │   ├── TargetResolver.cs            # resolve GameObject/Component/Transform targets
    │   ├── EntityIdCompat.cs            # Unity 6000.5 EntityId shim
    │   ├── GameObjectComponents.cs      # stable component name list helper
    │   ├── Levenshtein.cs               # edit distance for "did you mean"
    │   ├── UnityDocsStore.cs            # bundled ScriptReference lookup data
    │   ├── UnityPitfalls.cs             # curated Unity API pitfalls for describe_type
    │   ├── HeraSettings.cs              # asset-config.json reader (juicy mode, csc/dotnet paths)
    │   ├── PackageJobState.cs           # async package job survival across domain reloads
    │   ├── AssetRefresh.cs              # AssetDatabase.Refresh + script compile request
    │   ├── AssetDetector.cs             # third-party asset detection + config sync
    │   ├── AssetReserializer.cs         # ForceReserializeAssets helper
    │   ├── ProceduralSprite.cs          # solid/rounded/gradient/nine_slice sprite baking
    │   └── UiDocSchema.cs               # ui_doc/2 IR export/apply/layout engine
    ├── Data/
    │   └── unity_docs_*.jsonl.gz.bytes   # bundled Unity ScriptReference indexes
    ├── Tools/
    │   ├── ManageEditor.cs              # play, stop, pause, tags, layers
    │   ├── ExecuteCsharp.cs             # exec tool entry point (partial class)
    │   ├── ExecuteCsharp.SourceBuilder.cs   # snippet wrapping + using hoisting
    │   ├── ExecuteCsharp.Compilation.cs     # csc/dotnet invocation + error parsing
    │   ├── ExecuteCsharp.AssemblyLoader.cs  # collectible ALC assembly loading
    │   ├── ExecuteCsharp.Serializer.cs      # return value serialization + runtime error shaping
    │   ├── ExecuteMenuItem.cs           # Unity menu execution
    │   ├── ReadConsole.cs               # console log reading/clearing
    │   ├── RefreshUnity.cs              # asset database refresh + compile request
    │   ├── EditorScreenshot.cs          # screenshot capture
    │   ├── DetectAssets.cs              # auto-detect project assets / update asset config
    │   ├── ReserializeAssets.cs         # asset reserialization
    │   ├── ManageProfiler.cs            # profiler control
    │   ├── ManageScene.cs               # scene info/load/save/list/close
    │   ├── ManageComponents.cs          # component CRUD via SerializedProperty
    │   ├── ManageGameObject.cs          # GameObject CRUD + transform ops
    │   ├── ManageMaterial.cs            # material asset CRUD
    │   ├── ManagePrefab.cs              # prefab asset operations
    │   ├── ManageAssetImport.cs         # AssetImporter get/set
    │   ├── ManageUI.cs                  # uGUI create/get_rect/set_anchor/set_rect
    │   ├── ManagePackages.cs            # UPM list/add/remove/embed with async jobs
    │   ├── FindGameObjects.cs           # filtered scene search with pagination
    │   ├── FindMethod.cs                # method search across loaded assemblies
    │   ├── ListAssemblies.cs            # loaded assembly listing
    │   ├── DescribeType.cs              # loaded type introspection + pitfalls
    │   ├── DescribeShader.cs            # shader property inspection/search
    │   ├── UnityDocs.cs                 # offline ScriptReference lookup
    │   ├── UiDoc.cs                     # HTML→Unity UI pipeline dispatch
    │   └── LogToConsole.cs              # write to Unity console
    └── TestRunner/
        ├── RunTests.cs                  # Unity Test Framework execution
        └── TestRunnerState.cs           # PlayMode test result persistence
```

---

## HttpServer.cs

### Role
Lightweight HTTP server on localhost. Receives CLI commands as POST `/command`, batch commands as POST `/commands`, dispatches via `CommandRouter`, returns JSON responses.

### Key Characteristics
- Uses `ConcurrentQueue` + `EditorApplication.update` for main-thread marshaling
- Commands execute even when Unity is unfocused
- Survives domain reloads via `[InitializeOnLoad]`

### Port Selection

```csharp
const int DEFAULT_PORT = 8090;
const int FALLBACK_PORT = 8091;
const int MAX_PORT_ATTEMPTS = 10;
```

Tries 8090, then 8091, 8092, ... up to 10 attempts. First available port wins.

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

### Endpoints

| Path | Purpose |
|:---|:---|
| `POST /command` | Single command execution |
| `POST /commands` | Batch command execution (sequential, `fail_fast`) |

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
static readonly TimeSpan s_LockTimeout = TimeSpan.FromSeconds(120);

public static async Task<object> Dispatch(string command, JObject parameters)
{
    if (!await s_Lock.WaitAsync(s_LockTimeout))
        return new ErrorResponse("COMMAND_LOCK_TIMEOUT",
            "[Hera] I waited 120s for the command lock but another command is still running.");
    // ... dispatch
}
```

The 120-second timeout is the lock-acquisition timeout, not the per-command execution budget. Long-running operations are polled from the CLI side via heartbeat files.

### Dispatch Flow

1. If `command == "list"` → return names, summaries, or one full schema via `ToolDiscovery.GetToolNames()`, `GetToolSummaries()`, or `GetToolSchema(tool)`
2. Extract `action` from parameters (`action` field or first positional arg)
3. Resolve handler: action handler first (`ToolDiscovery.FindActionHandler`), then default `HandleCommand` (`ToolDiscovery.FindDefaultHandler`). Both `[HeraAction]` methods and legacy implicit action methods are considered.
4. If handler is static → invoke directly; if instance → create via `Activator.CreateInstance()`
5. If result is `Task<object>` → await it; if `Task` → await and return success message
6. Return result (or success message if null)

### Batch Dispatch

`DispatchBatch` holds the same lock while running a sequence of commands. This saves one HTTP round-trip per command and avoids releasing/re-acquiring the work queue between steps. `fail_fast` stops at the first `ErrorResponse`.

### Error Handling

All exceptions are caught, logged via `Debug.LogException`, and returned as `ErrorResponse`. Structured errors carry a stable `code` field (e.g. `EXEC_COMPILE_ERROR`, `UNKNOWN_COMMAND`, `MISSING_PARAM`).

CommandRouter-level tool dispatch codes:

| Code | When |
|---|---|
| `COMMAND_LOCK_TIMEOUT` | Lock acquisition timed out (another command is stuck) |
| `UNKNOWN_COMMAND` | No tool or action handler matched the command |
| `UNKNOWN_TOOL` | `list --tool <name>` referenced a missing tool |
| `TOOL_TYPE_NOT_FOUND` | Handler's declaring type could not be resolved |
| `TOOL_MISSING_CONSTRUCTOR` | Tool class lacks a public parameterless constructor |
| `TOOL_CONSTRUCTOR_INACCESSIBLE` | Tool constructor is not public |
| `TOOL_INSTANCE_CREATE_FAILED` | `Activator.CreateInstance` returned null |
| `TOOL_ACTION_FAILED` | Action-level handler threw an exception (an action was specified) |
| `TOOL_FAILED` | Default handler threw an exception (no action specified) |

### Common Tool Error Codes

All tools now return stable `code` values. Branch on these rather than parsing `message` text.

| Code | Typical Cause |
|---|---|
| `MISSING_PARAM` | A required parameter is missing or null |
| `INVALID_PARAM` | A parameter was supplied but malformed or out of range |
| `UNKNOWN_ACTION` | The tool has no matching action for the requested `action` |
| `TARGET_NOT_FOUND` | `instance_id`/`path` resolved to nothing |
| `OBJECT_NOT_FOUND` | The supplied `instance_id` no longer points to a live object |
| `NOT_A_GAMEOBJECT` | `instance_id` exists but is not a `GameObject` |
| `NOT_A_COMPONENT` | `component_id` exists but is not a `Component` |
| `INVALID_INSTANCE_ID` | `instance_id` could not be parsed as an integer |
| `INVALID_COMPONENT_ID` | `component_id` could not be parsed as an integer |
| `COMPONENT_NOT_FOUND` | Target GameObject does not have the requested component |
| `COMPONENT_INDEX_OUT_OF_RANGE` | `index` exceeds the number of matching components |
| `UNKNOWN_COMPONENT_TYPE` | `type` string does not resolve to a known component type |
| `TRANSFORM_NOT_ADDABLE` | Tried to `AddComponent<Transform>` |
| `ADD_COMPONENT_FAILED` | Unity threw while adding a component |
| `ADD_COMPONENT_NULL` | AddComponent returned null (likely `DisallowMultipleComponent`) |
| `TRANSFORM_NOT_REMOVABLE` | Tried to remove the required `Transform` |
| `REMOVE_COMPONENT_FAILED` | Unity threw while removing a component |
| `PROPERTY_NOT_FOUND` | `SerializedProperty` path does not exist on the component |
| `VALUE_COERCION_FAILED` | Could not convert the supplied value to the property type |
| `SCENE_NOT_FOUND` | `scene load` target does not exist |
| `SCENE_NOT_LOADED` | Target scene is not currently loaded |
| `SCENE_DIRTY` | Scene has unsaved changes and the operation requires a clean state |
| `SCENE_CLOSE_FORBIDDEN` | Attempted to close the only loaded scene |
| `PREFAB_NOT_FOUND` | Prefab asset path does not exist |
| `PREFAB_SAVE_FAILED` | Unity could not save the prefab |
| `INSTANTIATE_FAILED` | Unity could not instantiate the prefab |
| `MATERIAL_NOT_FOUND` | Material asset path does not exist |
| `SHADER_NOT_FOUND` | Named shader is not loaded |
| `SHADER_PROPERTY_NOT_FOUND` | Material/shader does not expose the named property |
| `VALUE_PARSE_ERROR` | Could not parse the value for a material property |
| `UI_MISSING_UGUI` | Required uGUI component could not be added |
| `UI_MISSING_EVENTSYSTEM` | `EventSystem` type is unavailable |
| `UI_EVENTSYSTEM_CREATE_FAILED` | Could not create an `EventSystem` |
| `TMP_NOT_INSTALLED` | Forced TextMeshPro but the package is missing |
| `INVALID_PRESET` | Unrecognized anchor preset name |
| `SCREENSHOT_FAILED` | Could not capture the requested view |
| `SCENEVIEW_NOT_FOUND` / `SCENEVIEW_CAMERA_NULL` / `CAMERA_NOT_FOUND` | Scene view / camera unavailable for screenshot |
| `PROFILER_NO_DATA` | Profiler has no captured data |
| `PROFILER_NO_FRAME_DATA` | Profiler frame/thread view is invalid |
| `PROFILER_ITEM_NOT_FOUND` | `--root` name not present in the hierarchy |
| `PROFILER_NO_FRAMES_IN_RANGE` | Requested frame range is empty |
| `EXEC_COMPILE_ERROR` | C# snippet did not compile |
| `EXEC_RUNTIME_ERROR` | C# snippet threw an exception |
| `EXEC_LOGGED_ERROR` | `--strict` mode and `Debug.LogError/LogException/LogAssert` was emitted |
| `EXEC_LOAD_FAILED` | Compiled assembly could not be loaded |
| `EXEC_INTERNAL_ERROR` | Unexpected failure inside the exec pipeline |
| `MENU_BLOCKED` | Menu item is on the safety blocklist |
| `MENU_EXECUTION_FAILED` | `EditorApplication.ExecuteMenuItem` returned false |
| `READCONSOLE_INIT_FAILED` | Unity console reader could not initialize |
| `PACKAGE_LIST_TIMEOUT` | Package list request timed out |
| `PACKAGE_JOB_START_FAILED` | Could not start a package manager async job |
| `DOCS_BUNDLE_UNAVAILABLE` | Bundled Unity docs data is missing or unreadable |
| `DOC_NOT_FOUND` | Query did not match any indexed docs entry |
| `INVALID_LAYER_INDEX` | `layer` integer is outside 0..31 |
| `UNKNOWN_LAYER_NAME` | `layer` string is not a defined layer |
| `INVALID_PATH_GLOB` | `path_glob` regex conversion failed |
| `INVALID_DEST` / `DEST_CREATE_FAILED` | `ui_doc import` destination invalid or not creatable |
| `SPRITE_GEN_FAILED` / `CAPTURE_FAILED` | Procedural sprite / UI capture failed |
| `TYPE_NOT_FOUND` | `describe_type` could not resolve the type |
| `TESTS_FAILED` | One or more tests failed |
| `PLAYMODE_REFRESH_BLOCKED` | `refresh_unity` refused because Unity is entering/ in play mode |
| `METHOD_NOT_ALLOWED` / `NOT_FOUND` / `INTERNAL_ERROR` | HTTP routing errors |

---

## ToolDiscovery.cs

### Role
Finds `[HeraTool]` handlers via reflection. Result is cached per assembly-reload — a fresh scan happens only when Unity reloads the domain, which is also when new tools could appear.

### Tool Name Resolution

| C# Class Name | Tool Name |
|:---|:---|
| `ManageEditor` | `manage_editor` |
| `ExecuteCsharp` | `exec` (explicit `Name =`) |
| `EditorScreenshot` | `screenshot` (explicit `Name =`) |
| `ManageUI` | `manage_ui` |
| `UiDoc` | `ui_doc` |
| Custom: `[HeraTool(Name = "my_tool")]` | `my_tool` (explicit) |

No explicit `Name=` → `StringCaseUtility.ToSnakeCase(ClassName)`.

### Action-Level Handlers

Action handlers are public static methods on a `[HeraTool]` class that take a single `JObject` parameter and return `object` or `Task<object>`. They are registered under `<tool>:<snake_case_method_name>`.

Explicit registration (preferred):

```csharp
[HeraAction]
public static object GetRect(JObject raw) { ... }
```

Legacy implicit registration is still supported for backward compatibility: any public static method with the right signature that is **not** named `Handle` or `HandleCommand` is auto-registered. New code should use `[HeraAction]` to make intent explicit and avoid accidental registration of helper methods.

The CLI sends `manage_ui get_rect` directly without a monolithic `HandleCommand` switch.

### Schema Generation

`GetToolSchema()` returns JSON schema for a discovered tool, including:
- Tool name, description, group(s), examples
- Parameter schema (from the nested `Parameters` class + `[ToolParameter]` attributes)
- Output schema
- Metadata flags (enum support, default support, custom types)

`GetToolSummaries()` returns name + description only (cheap). `GetToolNames()` returns names only (cheapest) and is also used for `list --compact`.

### "Did you mean"

`SuggestSimilarCommands()` uses `Levenshtein.DistanceBounded()` to suggest up to 3 tool names within edit distance 2 of a typo'd command.

---

## ExecuteCsharp

The `exec` tool is implemented as a `partial` static class split across five files under `Tools/`:

| File | Responsibility |
|---|---|
| `ExecuteCsharp.cs` | `[HeraTool]` entry point, `Parameters`, `HandleCommand`, `PreWarmCompiler`, `CompileAndExecute` orchestration |
| `ExecuteCsharp.SourceBuilder.cs` | Default usings, snippet wrapping, leading-`using` hoisting, line-offset math |
| `ExecuteCsharp.Compilation.cs` | `CompileToBytes`, csc/dotnet/Mono launcher, error parsing/formatting, temp-file cleanup |
| `ExecuteCsharp.AssemblyLoader.cs` | Collectible `AssemblyLoadContext` load with `Assembly.Load` fallback |
| `ExecuteCsharp.Serializer.cs` | Return-value serialization, `--stacktrace` modes, `--strict` log capture |

Splitting keeps each file under ~300 lines and makes the compile/load/invoke/serialize pipeline easier to navigate. The public contract (`HandleCommand`, `PreWarmCompiler`) does not change.

---

## Heartbeat.cs

### Role
Writes the instance state JSON file every 0.5 seconds so the Go CLI can discover and monitor Unity.

### File Location

```csharp
~/.hera-agent-unity/instances/<md5(projectPath).Substring(0,16)>.json
```

Example: `~/.hera-agent-unity/instances/a1b2c3d4e5f67890.json`

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

Certain operations force a temporary state to prevent the CLI from seeing premature "ready":

| Event | Forced State | Duration |
|:---|:---|:---|
| `beforeAssemblyReload` | `"reloading"` | Until next tick |
| `ExitingEditMode` | `"entering_playmode"` | Until next tick |
| `MarkCompileRequested()` | `"compiling"` | 3-second grace period |

### Instance File Format

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

`compileErrors` is read from `EditorUtility.scriptCompilationFailed` and lets `waitForReady()` report compilation errors without an extra console read.

---

## Core Utilities

### Response.cs

- `SuccessResponse` — `success`, `message`, `data`, optional `agent_hint`, `timings`
- `ErrorResponse` — `success=false`, `message`, optional `code`, `suggestions`, `data`, `timings`
- `ResponseTimings` — attaches `compile_ms` / `execute_ms` / `serialize_ms` / `total_ms` to responses

### ToolParams.cs + ParamCoercion.cs

`ToolParams` wraps a `JObject` and provides typed accessors: `Get`, `GetRequired`, `GetInt`, `GetFloat`, `GetBool`, `GetRaw`. `ParamCoercion` handles permissive bool parsing (`true`/`1`/`yes`/`on`, etc.).

### SerializedPropertyValue.cs

JSON ↔ `SerializedProperty` bridge used by `manage_components`, `manage_asset_import`, and any future tool that reads or sets typed Unity object properties. Supports:
- Primitives, enums, colors, vectors, quaternions, rects, bounds
- Object references via InstanceID, asset path, or `{instance_id|asset_path}` envelope
- Public parsers: `TryParseFloats`, `TryParseColor`

### ComponentTypeResolver.cs

Resolves short (`Rigidbody`) or fully-qualified (`UnityEngine.Rigidbody`) component names. The derived-type scan is snapshotted into dictionaries after each domain reload so repeated lookups avoid walking `TypeCache` every time. Provides `SuggestSimilar()` for "did you mean" hints.

### HierarchyPath.cs

`Build(Transform)` → `/Root/Child`. `Find(string)` → `GameObject.Find` first, then a fallback walk over loaded scenes including inactive roots/children.

### TargetResolver.cs

Shared target resolution: `instance_id` (highest priority) or `path`, with optional `altPathKey`. Also resolves `Transform` from a raw string and generic `GetComponent<T>`.

### EntityIdCompat.cs

Unity 6000.5 renamed `InstanceIDToObject`/`GetInstanceID` to `EntityIdToObject`/`GetEntityId` and made the old API obsolete-as-error. This shim chooses the right API per compile-time Unity version and preserves the existing int-based `instance_id` contract.

### GameObjectComponents.cs

`GameObjectComponents.GetNames(GameObject)` returns the type names of all non-null components on a GameObject, in `GetComponents` order and with missing scripts skipped. Shared by `manage_ui` and `manage_prefab` so both tools report component lists the same way.

### Levenshtein.cs

Edit-distance helper with a bounded early-exit variant used by command/type/docs suggesters.

### UnityDocsStore.cs

Selects the bundled `unity_docs_<version>.jsonl.gz.bytes` file for the current Unity version, falling back to the 6000.0 bundle when an exact bucket is not present. Loads it into a dictionary keyed by class/property/method name. Provides exact lookup and prefix-bucketed Levenshtein suggestions.

### UnityPitfalls.cs

Curated catalog of Unity API pitfalls attached to `describe_type` responses. Entries can carry a minimum docs bucket so Unity 6-only advice is hidden on 2022.3/2023.2.

### HeraSettings.cs

Reads `~/.hera-agent-unity/asset-config.json` by last-write-time cache. Exposes:
- `JuicyMode` → drives `manage_ui` / `ui_doc` juice hints
- `DotweenPreferred` → tween backend hint
- `DefaultCscPath` / `DefaultDotnetPath` → compiler defaults for `exec`

### PackageJobState.cs

Survives domain reloads for async `manage_packages add/remove/embed` operations. Writes a result file to `~/.hera-agent-unity/status/package-result-<port>-<job_id>.json` that the CLI polls.

### AssetRefresh.cs

Wrapper around `AssetDatabase.Refresh` and `CompilationPipeline.RequestScriptCompilation`. Used by `refresh_unity`; returns a structured result so the tool layer only has to build the response envelope.

### AssetDetector.cs

Scans the project for known third-party assets (Odin, DOTween, etc.) by directory and loaded-assembly checks, then mirrors the installed flags into `~/.hera-agent-unity/asset-config.json`. Used by `detect_assets`.

### AssetReserializer.cs

Thin wrapper around `AssetDatabase.ForceReserializeAssets`. Handles the "whole project" vs "specific paths" branching and logging. Used by `reserialize`.

### ProceduralSprite.cs

Tier-1 procedural sprite baking: `solid`, `rounded_rect`, `gradient`, `nine_slice`. Writes PNGs under `Assets/HeraGenerated/` and imports them as Sprites. Zero external dependency.

### UiDocSchema.cs

The `ui_doc/2` IR engine:
- `ExportNode` — serializes a uGUI subtree to compact IR
- `ApplyNode` — realizes IR under a parent (create or upsert)
- Layout group / layout element / content size fitter support
- Anchor preset grid (replicated from `ManageUI` pending Core extraction)

---

## Built-in Tools Summary

| Tool | Class | Key Actions |
|:---|:---|:---|
| `manage_editor` | `ManageEditor.cs` | play, stop, pause, set_active_tool, add_tag, remove_tag, add_layer, remove_layer |
| `exec` | `ExecuteCsharp.*.cs` | Compile and run C# code inside Unity (partial class split) |
| `menu` | `ExecuteMenuItem.cs` | Execute Unity menu items by path (`File/Quit` blocked) |
| `console` | `ReadConsole.cs` | Read/filter/clear console logs |
| `refresh_unity` | `RefreshUnity.cs` | AssetDatabase.Refresh, optional compile request (→ `AssetRefresh.cs`) |
| `screenshot` | `EditorScreenshot.cs` | Capture scene/game view |
| `detect_assets` | `DetectAssets.cs` | Auto-detect project assets / update asset config (→ `AssetDetector.cs`) |
| `reserialize` | `ReserializeAssets.cs` | Force asset reserialization (→ `AssetReserializer.cs`) |
| `profiler` | `ManageProfiler.cs` | enable/disable/capture profiler data |
| `run_tests` | `RunTests.cs` | Execute Unity Test Framework tests |
| `scene` | `ManageScene.cs` | info, load, save, list, close |
| `manage_components` | `ManageComponents.cs` | add, remove, list, get, set via SerializedProperty |
| `manage_gameobject` | `ManageGameObject.cs` | create, destroy, move, set_parent, set_active, set_name, get_transform |
| `manage_material` | `ManageMaterial.cs` | create, get, set, set_shader |
| `manage_prefab` | `ManagePrefab.cs` | create, instantiate, add_component, remove_component |
| `manage_asset_import` | `ManageAssetImport.cs` | get/set AssetImporter properties |
| `manage_ui` | `ManageUI.cs` | create, get_rect, set_anchor, set_rect |
| `manage_packages` | `ManagePackages.cs` | list, add, remove, embed (async job file) |
| `find_gameobjects` | `FindGameObjects.cs` | filtered scene search with pagination |
| `find_method` | `FindMethod.cs` | method search across loaded assemblies |
| `list_assemblies` | `ListAssemblies.cs` | loaded assembly listing |
| `describe_type` | `DescribeType.cs` | type introspection + Unity pitfalls |
| `describe_shader` | `DescribeShader.cs` | shader property inspection/search |
| `unity_docs` | `UnityDocs.cs` | offline ScriptReference lookup |
| `ui_doc` | `UiDoc.cs` | export, apply, import, gen_sprite, capture (sample/catalog are CLI-side) |
| `log` | `LogToConsole.cs` | write to Unity console |

---

## Data Bundle

`AgentConnector/Editor/Data/unity_docs_<version>.jsonl.gz.bytes` files are gzipped JSONL imported as `TextAsset`s. The current checkout still includes the legacy `unity_docs_6.0.jsonl.gz.bytes` file, which `UnityDocsStore` treats as the 6000.0 fallback. Regenerate a versioned bundle with:

```bash
go run ./tools/build-unity-docs \
    --in  <path-to-Documentation/en> \
    --out AgentConnector/Editor/Data/unity_docs_6000.0.jsonl.gz.bytes \
    --unity-version 6000.0
```

---

## TestRunner

`RunTests.cs` dispatches to the Unity Test Framework. EditMode tests run synchronously; PlayMode tests run asynchronously and persist results via `TestRunnerState` so the CLI can poll after a domain reload.

---

## Domain Reload Notes

When Unity compiles scripts, the entire AppDomain is reloaded:
- All static variables reset
- All instances destroyed
- HTTP listener must be stopped before reload, restarted after

Components marked `[InitializeOnLoad]` automatically re-initialize after reload. This is why `HttpServer`, `Heartbeat`, `TestRunnerState`, `ToolDiscovery`, `ExecCompileCache`, and `PackageJobState` all use this attribute or subscribe to `AssemblyReloadEvents`.

---

## Related Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) — Writing new tools
- [`COMMANDS.md`](COMMANDS.md) — Command reference
