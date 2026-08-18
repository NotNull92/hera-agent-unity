# Command Reference

Complete reference of all `hera-agent-unity` commands, flags, and parameters.

---

## Global Flags

These flags work with any command:

| Flag | Description | Default | Example |
|:---|:---|:---|:---|
| `--port` | Select Unity instance by active heartbeat port | Auto-discover | `--port 8091` |
| `--project` | Select Unity instance by project path | Auto-discover | `--project /path/to/project` |
| `--timeout` | Request timeout in milliseconds | `60000` (1 min) | `--timeout 120000` (2 min) |
| `--verbose` | Print progress + per-phase timings to stderr | `false` | `--verbose` |
| `--yes` | Answer approval preflights in the same invocation (env: `HERA_AGENT_APPROVE`) | `false` | `--yes` |

`--timeout` is always milliseconds: `--timeout 120000` means two minutes,
while `--timeout 120` means 120 milliseconds.

Ports are dynamic connection endpoints selected from `8090`–`8099`, not Editor
identities. With multiple Editors, prefer the full project path. Exact normalized
paths take precedence; a partial path must match exactly one live project. When
both `--project` and `--port` are present they must resolve to the same heartbeat.
Transport failures and request timeouts trigger a fresh heartbeat ownership
check before any retry.

The CLI and the Unity Connector are versioned independently, so a CLI can know
an action the installed Connector does not. When the Connector rejects the
`action` argument itself, Hera returns `CONNECTOR_UPDATE_REQUIRED` with the tool
and action it could not run, rather than the raw validation failure. Update the
`com.notnull92.hera-agent-unity` package in that project; retrying the same
command against the same Connector cannot succeed.

---

## mcp (experimental)

Published CLI `v0.1.0` includes the default-off stdio MCP adapter. Configure it
as described in [`MCP.md`](MCP.md), then start it with:

```bash
HERA_MCP_ENABLED=1 hera-agent-unity mcp --transport stdio
HERA_MCP_ENABLED=1 hera-agent-unity mcp --exposure profile --profile core
```

Compact is the normal MCP exposure and registers only
`tool_search`/`tool_describe`/`tool_call`. Profile and Full are explicit opt-ins.
Search returns action names without schemas; describe returns a compact action
overview until a specific action is selected. The
`advanced` profile additionally requires `--allow-arbitrary-code`; every risky
operation still needs approval. See [`MCP.md`](MCP.md) for client configuration,
flags, Tasks fallback, compatibility, result resources, and security boundaries.

---

## call

Validate a JSON request object against the selected tool's live strict contract,
then invoke the canonical tool name.

```bash
hera-agent-unity call <tool> --json '{"action":"info"}'
hera-agent-unity call <tool> --file request.json
echo '{"action":"info"}' | hera-agent-unity call scene
```

Use exactly one input source. If none is supplied, the request is `{}`;
combining `--json`, `--file`, and stdin is an error.

| Flag | Description | Default |
|:---|:---|:---|
| `--json` | Inline JSON request object | none |
| `--file` | Read the JSON request object from a file | none |
| `--profile` | Require membership in the named live profile | none |
| `--operation-id` | Reuse the operation ID bound to an approval or safe retry | generated |
| `--approve` | Continue the identical request with a short-lived preflight token | none |
| `--validate-only` | Validate without invoking the target tool | `false` |
| `--explain` | Report canonical action, profile, contract mode, and resolved safety without invoking | `false` |

`--explain` reports the current safety and policy projection without dispatching
the command. Normal `call` execution enforces approval and operation-ledger
requirements before risky work reaches the Connector.

Validation uses the resolved action schema, so an action-specific object such as
`{"action":"set_rect","path":"/Canvas","size_delta":"300,60"}` is checked against `manage_ui/set_rect`, not only the
tool's top-level dispatcher shape. In a non-interactive shell, an approval-gated
request returns `APPROVAL_REQUIRED`; repeat the exact same typed or established
command with `--approve <token>`, carrying the original input again because the
token is bound to the exact arguments. Changing its project, tool, action,
arguments, or operation ID invalidates the single-use token.

`--yes` (env: `HERA_AGENT_APPROVE=1`) belongs to an operator's own shell or CI
job: it answers the preflight in the same invocation rather than returning
`APPROVAL_REQUIRED`. Preflight, token binding, and the Connector operation
ledger are unchanged; only the terminal question is skipped.

---

## editor

Start or restart an exact Unity project, control play mode, and refresh the
asset database.

```bash
hera-agent-unity editor <action> [flags]
```

### launch / restart

Start the exact project selected by `--project`, or stop only that project's
heartbeat PID and start it again. Both actions read
`ProjectSettings/ProjectVersion.txt`, resolve the matching Unity Hub Editor,
launch Unity with normal Package Manager behavior, and return after the new PID
publishes the selected project's heartbeat.

| Flag | Description | Default |
|:---|:---|:---|
| `--project PATH` | Exact Unity project path; required global flag | none |
| `--hub-root PATH` | Unity Hub Editor root | `UNITY_HUB_EDITOR`, then the platform Hub default |
| `--timeout MS` | Total stop/start/heartbeat deadline | `60000` |

```bash
hera-agent-unity --project C:/Projects/Game editor launch
hera-agent-unity --project C:/Projects/Game editor restart
hera-agent-unity --project C:/Projects/Game editor launch --hub-root D:/Unity/Hub/Editor
```

`--port` is rejected because ports do not identify an Editor before startup.
`restart` terminates the selected process, so save pending work first. These
commands do not pass `-noUpm`. On Windows, they restore `ALLUSERSPROFILE` from
`ProgramData` for the Unity child when the invoking agent shell omitted it;
this prevents UPM path initialization from receiving an undefined common
profile. They do not rewrite package metadata or repair unrelated Package
Manager failures. After the heartbeat appears, use the normal `status`,
`console`, and other Connector commands.

### play

Enter play mode.

| Flag | Description | Default |
|:---|:---|:---|
| `--wait` | Block until fully entered play mode | `false` |

```bash
hera-agent-unity editor play --wait
```

### stop

Exit play mode.

| Flag | Description | Default |
|:---|:---|:---|
| `--wait` | Block until fully exited play mode | `false` |

```bash
hera-agent-unity editor stop --wait
```

### pause

Toggle pause/resume (play mode only).

```bash
hera-agent-unity editor pause
```

### refresh

Refresh the AssetDatabase.

| Flag | Description | Default |
|:---|:---|:---|
| `--force` | Allow refresh during play mode | `false` |
| `--compile` | Recompile scripts and wait until done | `false` |

```bash
hera-agent-unity editor refresh --force
hera-agent-unity editor refresh --compile
```

**Note**: `refresh` is blocked in play mode unless `--force` is set.

---

## exec

Execute arbitrary C# code inside Unity Editor.

```bash
hera-agent-unity exec "<code>" [flags]
echo '<code>' | hera-agent-unity exec [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--usings` | Add extra using directives (comma-separated) | `""` |
| `--csc` | Path to csc compiler | Auto-detected |
| `--dotnet` | Path to dotnet runtime | Auto-detected |
| `--no-cache` | Bypass exec caches; do not read or write cached assemblies or disk DLLs | `false` |
| `--security-mode` | `full` preserves unrestricted access; `restricted` enables source, pre-load metadata, and post-load IL validation | `full` |
| `--depth` | Maximum returned object-graph depth. At depths `1` and `2`, every `UnityEngine.Object` is the compact `{name, type, instanceID}` shape; depth `3` (default) and above reflect its public members. | `3` (max `8`) |

```bash
# Basic execution
hera-agent-unity exec "return 1+1;"

# Unity API access
hera-agent-unity exec "return Application.dataPath;"

# Pipe to avoid shell escaping
echo 'return EditorSceneManager.GetActiveScene().name;' | hera-agent-unity exec

# Custom usings for ECS
hera-agent-unity exec "return World.All.Count;" --usings Unity.Entities

# Opt in to defense-in-depth restrictions for a platform-only inspection
hera-agent-unity exec "return Application.unityVersion;" --security-mode restricted
```

**Default usings**: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Reflection`, `System.Threading.Tasks`, `UnityEngine`, `UnityEngine.SceneManagement`, `UnityEditor`, `UnityEditor.SceneManagement`, `UnityEditorInternal`

**Note**: Use `return` for output. Use `return null;` for void operations.

**Return-size control**: Prefer `--depth 1` or `--depth 2` when returning a `GameObject`, `Transform`, `Component`, or other `UnityEngine.Object`. Both depths preserve only its name, runtime type, and instance ID; use depth `3` only when the reflected member graph is required.

**Restricted mode**: `--security-mode restricted` is an explicit defense-in-depth mode. It rejects dangerous source constructs before compilation, rejects native interop and non-platform assembly references from compiled metadata before loading, and validates actual IL call targets after loading but before `Execute()`. File, network, process, reflection, threading, dynamic-loading, `UnityEditor`, and user/third-party assembly access are denied. Returned `UnityEngine.Object` values are forced to the shallow depth-2 shape. The normal arbitrary-code permission, approval, operation ledger, and strict tool contract still apply; Restricted does not replace them. Full Access remains the default for compatibility.

**Caching**: Compiled assemblies are cached in `Library/HeraAgentCache/` and held in memory. The cache key includes the source, reference-set hash, language version, and a versioned compiler/compilation fingerprint, so incompatible compiler inputs cannot reuse an old DLL. The first call per Unity session is the cold path (csc invocation); identical follow-up calls skip both compile and load. Cache invalidates automatically on assembly reload. `--no-cache` bypasses every exec cache read and write: it compiles against transient reference arguments, does not load or store cached assemblies, and does not persist a DLL — including with `--check`.

---

## console

Read, filter, and clear Unity console logs.

```bash
hera-agent-unity console [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--lines` | Limit to N entries; `0` returns all; negative values are rejected | `20` |
| `--type` | Comma-separated: `error`, `warning`, `log` | `error,warning,log` |
| `--stacktrace` | `none`, `user`, `full` | `user` |
| `--clear` | Clear console after reading | `false` |
| `--since` | Resume from a prior response's `last_cursor`; negative values are rejected | `0` |

```bash
hera-agent-unity console
hera-agent-unity console --lines 20 --type error
hera-agent-unity console --stacktrace full
hera-agent-unity console --clear
```

Responses include `returned`, `matched`, `last_cursor`, and `truncated` for
pagination. Reuse `last_cursor` as `--since`: when `truncated` is `true`, it
resumes immediately after the final returned entry; otherwise it advances to
the current end of the console. A cursor beyond the current console length
(for example, after clearing the console) restarts from index `0`.

---

## scene

Inspect and manage Unity scenes.

```bash
hera-agent-unity scene <action> [target] [flags]
```

### Actions

| Action | Description |
|:---|:---|
| `info` | Active scene + every loaded scene (name, path, dirty, root count). |
| `create <path>` | Create and save a scene. `--mode single\|additive`; `--template empty\|default`. |
| `load <path\|name>` | Open a scene by asset path or bare filename. |
| `save [<path\|name>]` | Save the active scene, or a named loaded scene if specified. |
| `save_all` | Save every dirty loaded scene. |
| `list` | List scenes registered in Build Settings. |
| `set_active <path\|name>` | Make one loaded scene active. |
| `close <path\|name>` | Unload a loaded scene. Cannot close the only loaded scene. |
| `hierarchy` | Dump the GameObject tree of every loaded scene (or one subtree) as nested nodes with instance_id, name, and active state. |

### Flags

| Flag | Description | Default | Applies to |
|:---|:---|:---|:---|
| `--mode` | `single`, `additive`, or `additive_without_loading` | `single` | `load` |
| `--template` | `empty` or `default` initial contents | `empty` | `create` |
| `--root` | Scope the dump to one subtree (instance_id or hierarchy path) | all loaded scenes | `hierarchy` |
| `--depth` | Limit tree depth; `0` = unlimited | `0` | `hierarchy` |
| `--max_nodes` | Node budget; the result reports `truncated=true` when hit | `500` (cap 5000) | `hierarchy` |
| `--components` | Include short component type names per node | off | `hierarchy` |

### Examples

```bash
hera-agent-unity scene info
hera-agent-unity scene create Assets/Scenes/Generated.unity --template default
hera-agent-unity scene load Assets/Scenes/Main.unity
hera-agent-unity scene load Main --mode additive
hera-agent-unity scene save
hera-agent-unity scene save_all
hera-agent-unity scene set_active Main
hera-agent-unity scene close Lobby
hera-agent-unity scene list
hera-agent-unity scene hierarchy --depth 2
hera-agent-unity scene hierarchy --root /GameCanvas --components
```

**Notes**:
- `load --mode single` refuses to run if the active scene is dirty — save it first or load additively.
- `close` refuses if the target scene is dirty.
- Name resolution uses `AssetDatabase.FindAssets` with an exact filename match (case-insensitive).

---

## build

Player builds for the active build target. The Editor blocks for the whole build, so `start` queues it and returns immediately; the compact report lands on the file bus. `build start --wait` polls that file (floor 15 minutes; a larger `--timeout` extends it). The queued build maps the persisted `development`, `allow_debugging`, and `build_scripts_only` settings into `BuildPlayerOptions`.

```bash
hera-agent-unity build <action> [options]
```

### Actions

| Action | Description |
|:---|:---|
| `start [--wait] [--output_path P]` | Queue the build (approval-token flow). Default output `Builds/<target>/<product><ext>`; refuses paths inside `Assets/`, play mode, an already-running build, and an empty enabled-scene list. |
| `status` | `idle` \| `queued` \| `building`, plus the last report. |
| `get_settings` | Active target/group, development flags, scene list. |
| `set_settings` | `development` / `allow_debugging` / `build_scripts_only` via `--params`; `"dry_run": true` previews. |
| `add_scene --path P [--enabled false]` | Add or update a Build Settings scene (idempotent). |
| `remove_scene --path P` | Remove a scene from the list (idempotent). |
| `list_targets` | BuildTarget values with group and installed build support. |

Report shape: `{result, output_path, target, size_bytes, total_seconds, error_count, warning_count, errors[<=20]}`.

### Examples

```bash
hera-agent-unity build get_settings
hera-agent-unity build add_scene --path Assets/Scenes/Main.unity
hera-agent-unity build start --wait
```

## bake

Scene bakes by area. `start` triggers the async bake and returns immediately; poll `status` until it reports `idle`. Status is computed from live Editor state, so it survives reconnects and domain reloads.

```bash
hera-agent-unity bake <action> --area <lighting|navmesh|navmesh_surfaces|occlusion>
```

### Actions

| Action | Description |
|:---|:---|
| `start` | Trigger the async bake. Refuses on an untitled scene (`SCENE_NOT_SAVED`) and while that area is already baking (`ALREADY_BAKING`). Lighting reports the GI workflow mode. |
| `status` | `idle` \| `baking`, plus `has_baked_data`; lighting adds `progress` while baking, occlusion adds `data_size_bytes`. |
| `cancel` | Cancel an in-progress bake (no-op success when idle). |
| `clear` | Delete the area's baked data. Requires the approval-token flow. |

`--area navmesh` bakes the built-in scene NavMesh. `--area navmesh_surfaces` bakes the `NavMeshSurface` components of the AI Navigation package, which write their own `NavMeshData` assets. They are separate values on purpose: the artifacts differ, and `bake` is approval-gated so an agent should know which one it is changing. `navmesh_surfaces` needs `com.unity.ai.navigation`; without it every call returns `PACKAGE_NOT_INSTALLED` rather than falling back to the built-in mesh. `--target <path|instance_id|handle>` restricts `start`, `status`, and `clear` to the surfaces under one object. `cancel --area navmesh_surfaces` returns `CANCEL_UNSUPPORTED` — the package offers no cancellation for surface bakes.

### Examples

```bash
hera-agent-unity bake start --area lighting
hera-agent-unity bake status --area lighting
hera-agent-unity bake clear --area navmesh
```

## manage_settings

Read and change project settings by area. `set_*` applies only the fields you pass, previews with `"dry_run": true` (approval-free), reports `{applied, skipped}` with per-field reasons, and otherwise requires the approval-token flow because settings changes are project-wide and not undoable.

```bash
hera-agent-unity manage_settings <action> [--params '{...}']
```

### Actions

| Action | Fields |
|:---|:---|
| `get_physics` / `set_physics` | `gravity` `[x,y,z]`, `default_solver_iterations`, `default_solver_velocity_iterations`, `bounce_threshold`, `default_contact_offset`, `sleep_threshold` |
| `get_time` / `set_time` | `fixed_delta_time`, `maximum_delta_time`, `time_scale` |
| `get_quality` / `set_quality` | `level` or `level_name` (one of the two), `vsync_count` (0-4), `anti_aliasing` (0\|2\|4\|8); `get` also lists the project's level names |
| `get_player` / `set_player` | `company_name`, `product_name`, `bundle_version`, `scripting_backend` (`mono2x`\|`il2cpp`), `api_compatibility_level` (`net_standard`\|`net_framework`) |
| `get_audio` / `set_audio` | `volume` (0-1), `doppler_factor`, `rolloff_scale` — the persisted project audio configuration |
| `get_graphics` / `set_graphics` | `render_pipeline_asset` (Assets path or durable handle; null/empty selects the built-in render pipeline) |
| `get_input` / `set_input` | Bounded legacy Input Manager axes; one exact axis' `sensitivity`, `gravity`, `dead` |
| `get_lighting` / `set_lighting` | Active `LightingSettings`: GI, sample, bounce, lightmap, compression, directionality, AO, and filtering fields |
| `get_navmesh` / `set_navmesh` | Legacy built-in NavMesh agent radius/height/slope/climb, region area, and voxel settings |

### Examples

```bash
hera-agent-unity manage_settings get_physics
hera-agent-unity manage_settings set_physics --params '{"gravity":[0,-19.62,0]}'
hera-agent-unity manage_settings set_time --params '{"fixed_delta_time":0.01,"dry_run":true}'
hera-agent-unity manage_settings set_quality --params '{"level_name":"High"}'
hera-agent-unity manage_settings set_player --params '{"scripting_backend":"il2cpp"}'
hera-agent-unity manage_settings get_graphics
hera-agent-unity manage_settings get_input --params '{"limit":25}'
hera-agent-unity manage_settings set_input --params '{"axis":"Horizontal","dead":0.1,"dry_run":true}'
hera-agent-unity manage_settings get_lighting
hera-agent-unity manage_settings get_navmesh
```

`get_player` reports the backend and API level exactly as Unity stores them, so a
project can read back a name the write side does not accept — `NET_Standard` and
`NET_Standard_2_0` are the same value under two names, and older projects may hold
a .NET profile that no longer builds. The write side takes only the values Unity 6
actually supports.

Changing `api_compatibility_level` swaps the assemblies editor scripts compile
against: Hera answers first, then Unity recompiles and the Editor is unreachable
for a few seconds. `set_player` reports `recompile_triggered` so you know which
calls do this, and `dry_run` reports it before anything changes. Changing
`scripting_backend` only affects what a player build produces and never recompiles.
Both apply to the active build target, named in the response as `build_target`.

Related: `manage_editor get_tags_layers` lists tags and named layers before `add_tag` / `add_layer` / `manage_gameobject set_tag`.

`manage_editor focus` focuses one already loaded Editor window. Pass exactly one
of `--type <exact type or full type name>` or `--title <exact title>`; zero or
ambiguous matches fail without opening a new window.

## manage_packages

Drive `UnityEditor.PackageManager.Client` from the CLI. Replaces hand-editing `Packages/manifest.json` — the Package Manager API owns the project lock and validates git URLs that a manual edit would mishandle.

```bash
hera-agent-unity manage_packages <action> [identifier]
```

### Actions

| Action | Sync? | Description |
|:---|:---:|:---|
| `list` | ✅ | Every package the project currently resolves to (incl. indirect dependencies). |
| `search --filter <text>` | ✅ | Registry packages whose name, display name, description, or keywords contain the text, with the versions this Editor accepts. `--limit` defaults to 25. |
| `add <identifier>` | ❌ | Install. `identifier` accepts any `Client.Add` form: `com.x.y`, `com.x.y@1.2.3`, `https://.../repo.git`, `https://.../repo.git?path=Sub`, `file:..`. |
| `remove <name>` | ❌ | Uninstall by package name. |
| `embed <name>` | ❌ | Copy a cached package out of `Library/PackageCache` into `Packages/` so it becomes locally editable. |

`async` actions return immediately with a `job_id`. The CLI polls
`~/.hera-agent-unity/status/package-result-<port>-<job_id>.json`
for up to 10 minutes and deletes it once consumed.

### Identifier forms (`add`)

| Form | Example |
|:---|:---|
| Registry, latest | `com.unity.ai.navigation` |
| Registry, pinned | `com.unity.cinemachine@2.9.7` |
| Git URL | `https://github.com/Cysharp/UniTask.git` |
| Git URL, subdir | `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| Local path | `file:../local-package` |

### Return shape

**list**:

```json
{
  "packages": [
    {
      "name": "com.unity.collab-proxy",
      "version": "2.4.0",
      "source": "Registry",
      "resolved_path": "Library/PackageCache/...",
      "is_direct_dependency": true,
      "display_name": "Version Control"
    }
  ]
}
```

**add / remove / embed (start)**:

```json
{ "job_id": "pkg-9c4f12a8b7e64d2a91c63c35cfab47d0", "port": 8090, "action": "add", "identifier": "com.unity.ai.navigation" }
```

**add / remove / embed (completion, written to package-result file)**:

```json
{
  "success": true,
  "message": "add 'com.unity.ai.navigation' completed.",
  "data": {
    "action": "add",
    "identifier": "com.unity.ai.navigation",
    "package": { "name": "...", "version": "...", "source": "Registry", ... }
  }
}
```

Failure carries a structured `code`: `PACKAGE_ADD_FAILED`, `PACKAGE_REMOVE_FAILED`, `PACKAGE_EMBED_FAILED`, `PACKAGE_LIST_TIMEOUT`, `PACKAGE_TIMEOUT` (job idle >10m), or `PACKAGE_RESUME_LIST_FAILED` / `PACKAGE_RESUME_VERIFY_FAILED` (post-reload verification fell over).

### Examples

```bash
hera-agent-unity manage_packages list
hera-agent-unity manage_packages add com.unity.ai.navigation
hera-agent-unity manage_packages add com.unity.cinemachine@2.9.7
hera-agent-unity manage_packages add https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
hera-agent-unity manage_packages remove com.unity.ai.navigation
hera-agent-unity manage_packages embed com.unity.test-framework
```

**Notes**:
- The package resolver triggers a domain reload after most `add` / `remove` operations. The CLI bridges this via `[InitializeOnLoad]` — a `Client.List` verifier runs after the reload and writes the result file even though the original `Request` handle is gone.
- OpenUPM packages need their scoped registry registered first (Package Manager UI → ⊕ → Add scoped registry). Once registered, `manage_packages add com.author.pkg` resolves them like any registry package.
- Embedding into `Packages/` puts the package under version control — commit the new folder if you want others to see your local edits.

---

## manage_assets

**Durable handles** (all asset tools): any `--path` that names an asset which
already exists also accepts `guid:<32hex>`, `guid:<32hex>:<fileId>` for one
object inside a container, or a `GlobalObjectId_V1-…` string — the same grammar
`find` and `deps` already report back. A handle survives moves and renames that
break a recorded path, and `guid:<guid>:<fileId>` reaches sub-assets a path
cannot name at all when a container holds several of the same type. Resolution
is addressing only: the action's usual containment rule then runs on the
resolved path, so a `Packages/` handle stays refused wherever a `Packages/`
path was, and the refusal names what the handle resolved to. Parameters that
name a **new** file — `create --path`, `copy`/`move --new_path`, `mkdir` —
reject handles, because a handle names something that already exists.

Compact `AssetDatabase` operations for common file, folder, and asset-authoring work. Paths are constrained to `Assets/`.

```bash
hera-agent-unity manage_assets <action> [flags]
```

| Action | Required flags | Description |
|:---|:---|:---|
| `find` | `--filter`, `--type`, or both | Search assets and return compact `{path,guid,name,type}` entries. |
| `deps` | `--path`, `--direction` | `forward` lists what the asset uses; `reverse` lists what uses it. |
| `mkdir` | `--path Assets/...` | Create an `Assets/` folder recursively. Existing folders succeed with `created:false`. |
| `create` | `--type`, `--path Assets/....asset` | Instantiate a ScriptableObject subclass as a new `.asset` (`.asset` is appended if omitted). Optional initial serialized fields via `--params '{"properties":{...}}'`. |
| `copy` | `--path`, `--new_path` | Copy one asset file. |
| `move` | `--path`, `--new_path` | Move or rename one asset file. |
| `delete` | `--path` | Delete one asset file or folder. Refuses to delete `Assets`. |

| Flag | Description | Default |
|:---|:---|:---|
| `--filter` | AssetDatabase search text for `find` | |
| `--type` | `find`: asset type filter (`Texture2D`, `Material`, `Prefab`). `create`: the ScriptableObject subclass to instantiate — short name (`GameConfig`) or fully-qualified (`My.Namespace.GameConfig`). | |
| `--limit` | Maximum `find` results | `50` (max `500`) |
| `--include_folders` | Include folders in `find` output | `false` |
| `--direction` | `deps`: `forward` (what this uses) or `reverse` (what uses this). Required — the two answer opposite questions. | |
| `--recursive` | `deps forward`: follow dependencies transitively | `false` |
| `--scope` | `deps reverse`: `assets` scans `Assets/`; `all` also scans `Packages/` | `assets` |
| `--params '{"properties":{...}}'` | `create` only: raw SerializedProperty name → value map applied to the new asset. All fields are validated before creation; an invalid field returns `INVALID_INITIAL_PROPERTIES` and creates no asset. | |

```bash
hera-agent-unity manage_assets find --type Texture2D --filter icon --limit 20
hera-agent-unity manage_assets deps --path Assets/Prefabs/Player.prefab --direction forward
hera-agent-unity manage_assets deps --path Assets/Prefabs/Player.prefab --direction forward --recursive true
hera-agent-unity manage_assets deps --path Assets/Art/Hero.mat --direction reverse
hera-agent-unity manage_assets mkdir --path Assets/Generated/UI
hera-agent-unity manage_assets create --type GameConfig --path Assets/Config/Game.asset
hera-agent-unity manage_assets create --type EnemyStats --path Assets/Data/Goblin.asset --params '{"properties":{"m_MaxHealth":30}}'
hera-agent-unity manage_assets copy --path Assets/A.prefab --new_path Assets/B.prefab
hera-agent-unity manage_assets move --path Assets/Old.asset --new_path Assets/New.asset
hera-agent-unity manage_assets delete --path Assets/Generated/Temp.asset
```

**Dependencies**: `deps forward` returns `{path, direction, recursive, total, returned, truncated, assets}`; `deps reverse` adds `scope`, `scanned`, and `elapsed_ms`. The queried asset never appears in its own result.

Both directions read `AssetDatabase`, not Unity Search. Search answers the reverse question in milliseconds, but its index lags the asset database *intermittently* — a create-then-query probe during the release gate returned the correct reference on `6000.0.35f1` and **zero** on `6000.3.5f2` and `6000.5.6f1`, while the AssetDatabase scan was correct in all three. An empty reverse result is what reads as "safe to delete", so it has to be trustworthy. The scan's cost is reported rather than hidden: scoped to `Assets/` it is tens of milliseconds on the gate fixtures, and `--scope all` ran 1.5–3.7 s over ~10,000 assets.

A truncated reverse list is flagged and carries an `agent_hint` — treat it as "at least N", not "N". A `--path` with no asset returns `ASSET_NOT_FOUND` rather than an empty list.

---

## manage_animation

Author animation assets directly, without `exec` boilerplate. Clips use `.anim`, controllers `.controller`; paths are constrained to `Assets/`.

```bash
hera-agent-unity manage_animation <action> [flags]
```

| Action | Required flags | Description |
|:---|:---|:---|
| `create_clip` | `--path Assets/....anim` | Create an `AnimationClip`. `--frame_rate` (default 60), `--loop`. |
| `set_curve` | `--path`, `--type`, `--property`, `--params '{"keys":[...]}'` | Set one float curve on a clip. |
| `remove_curve` | `--path`, `--type`, `--property` | Remove one float curve; optional `--relative_path`. |
| `create_controller` | `--path Assets/....controller` | Create an `AnimatorController` (base layer + state machine). |
| `add_parameter` | `--path`, `--name`, `--type` | Add a `float`/`int`/`bool`/`trigger` parameter (optional `--params '{"default":...}'`). |
| `add_layer` | `--path`, `--name` | Add an override/additive layer; optional weight (`0..1`) and blending via `--params`. |
| `add_state` | `--path`, `--name` | Add a base-layer state. `--motion <clip path>`, `--default`. |
| `add_transition` | `--path`, `--from`, `--to` | Add a transition. Conditions via `--params`. |
| `get_clip` | `--path` | Read a clip's metadata and every curve binding; `--include_keys` adds keyframes (time/value/tangents). |
| `get_controller` | `--path` | Read a controller's parameters, layers, states (motion, default), and transitions with conditions. |

| Flag | Description | Default |
|:---|:---|:---|
| `--frame_rate` | `create_clip` sampling rate (fps) | `60` |
| `--loop` | `create_clip` loops the clip | `false` |
| `--type` | `set_curve`: animated component type. `add_parameter`: `float`\|`int`\|`bool`\|`trigger` | |
| `--property` | `set_curve` animated property, e.g. `localPosition.y` | |
| `--relative_path` | `set_curve` / `remove_curve` GameObject path relative to the Animator root | `""` (root) |
| `--name` | `add_parameter` / `add_state` name | |
| `--motion` | `add_state` motion clip asset path | |
| `--default` | `add_state` makes it the base-layer default state | `false` |
| `--from` / `--to` | `add_transition` source / destination state names | |
| `--params` | `set_curve` `keys` `[{time,value[,in_tangent,out_tangent]}]`; `add_parameter` `default`; `add_transition` `conditions` `[{parameter,mode,threshold}]` / `has_exit_time` / `duration` | |

Condition `mode` is one of `If`, `IfNot`, `Greater`, `Less`, `Equals`, `NotEqual`. `add_state` validates an optional motion and `add_transition` validates every condition (including referenced controller parameters) before changing the controller.

```bash
hera-agent-unity manage_animation create_clip --path Assets/Anim/Bob.anim --frame_rate 60 --loop true
hera-agent-unity manage_animation set_curve --path Assets/Anim/Bob.anim --type Transform --property localPosition.y --params '{"keys":[{"time":0,"value":0},{"time":0.5,"value":0.3},{"time":1,"value":0}]}'
hera-agent-unity manage_animation remove_curve --path Assets/Anim/Bob.anim --type Transform --property localPosition.y
hera-agent-unity manage_animation create_controller --path Assets/Anim/Player.controller
hera-agent-unity manage_animation add_parameter --path Assets/Anim/Player.controller --name Speed --type float
hera-agent-unity manage_animation add_layer --path Assets/Anim/Player.controller --name UpperBody --params '{"weight":1,"blending":"override"}'
hera-agent-unity manage_animation add_state --path Assets/Anim/Player.controller --name Run --motion Assets/Anim/Bob.anim --default true
hera-agent-unity manage_animation add_transition --path Assets/Anim/Player.controller --from Idle --to Run --params '{"conditions":[{"parameter":"Speed","mode":"Greater","threshold":0.1}]}'
```

---

## manage_timeline

Create and inspect Timeline assets, then add validated tracks and clips. This tool uses reflection so `com.unity.timeline` stays optional; projects without it receive `PACKAGE_NOT_INSTALLED`.

```bash
hera-agent-unity manage_timeline <action> [--params '{...}']
```

| Action | Required fields | Description |
|:---|:---|:---|
| `create` | `path` | Create a `.playable` asset; `frame_rate` defaults to 60. |
| `get` | `path` | Read metadata, tracks, and clips; `limit` defaults to 100 and caps at 500 combined entries. |
| `add_track` | `path`, `type` | Add an Animation, Audio, Activation, Control, Playable, Signal, Marker, or Group track; optional `name` and parent track name. |
| `add_clip` | `path`, `track`, `start`, `duration` | Add a clip to an exact track name; optional source `asset` and display `name`. |

```bash
hera-agent-unity manage_timeline create --params '{"path":"Assets/Cinematics/Intro.playable","frame_rate":60}'
hera-agent-unity manage_timeline add_track --params '{"path":"Assets/Cinematics/Intro.playable","type":"Animation","name":"Hero"}'
hera-agent-unity manage_timeline add_clip --params '{"path":"Assets/Cinematics/Intro.playable","track":"Hero","asset":"Assets/Anim/Hero.anim","start":0,"duration":2}'
hera-agent-unity manage_timeline get --params '{"path":"Assets/Cinematics/Intro.playable","limit":100}'
```

---

## unity_docs

Offline Unity ScriptReference lookup. Returns a slim, JSON-ready shape suitable for AI agents who need to verify an API exists at this Unity version before running it through `exec`. No network, no rate limits.

The data set **ships inside the UPM connector package itself**, under `AgentConnector/Editor/Data/unity_docs_<version>.jsonl.gz.bytes`. The connector selects the current Unity docs bucket (`2022.3`, `2023.2`, `6000.0`, `6000.3`, `6000.5`) and falls back to the 6000.0 bundle when an exact bucket is not present. Installing the connector is the only prerequisite — there is no docs folder to point at, no environment variable, no asset-config entry. The CLI passes the query straight through; the connector loads the bundled data once per domain and serves every subsequent lookup from an in-memory dictionary.

```bash
hera-agent-unity unity_docs <query>
```

### Query → dictionary key mapping

| Query | Resolves to key |
|:---|:---|
| `Rigidbody` | `Rigidbody` |
| `Rigidbody.mass` | `Rigidbody-mass` (property) |
| `Rigidbody.AddForce` | `Rigidbody.AddForce` (method) |
| `Vector3.zero` | `Vector3-zero` |
| `UnityEditor.AssetDatabase.Refresh` | `AssetDatabase.Refresh` |

- Leading `UnityEngine.` / `UnityEditor.` is stripped (docs keys omit those namespaces).
- The literal query (methods + classes) is tried first; if that key doesn't exist, the last `.` is replaced with `-` (properties).
- On miss the response carries `data.did_you_mean[]` populated from a Levenshtein scan of the selected version bucket.

### Return shape

```json
{
  "title": "Rigidbody.mass",
  "signature": "public float mass;",
  "summary": "The mass of the rigidbody.",
  "unity_version": "6000.0",
  "docs_version": "6000.0"
}
```

Typically 250–400 bytes per call.

### Errors

| Code | Meaning |
|:---|:---|
| `DOCS_BUNDLE_UNAVAILABLE` | The bundled data file is missing or unreadable on this connector install. Reinstall the UPM package; or in a local checkout, rerun `go run ./tools/build-unity-docs --unity-version 6000.0`. |
| `DOC_NOT_FOUND` | Query did not map to any key. `data.did_you_mean[]` holds up to 5 nearest keys; `suggestions[]` carries them as ready-to-run CLI calls. |

### Examples

```bash
hera-agent-unity unity_docs Rigidbody
hera-agent-unity unity_docs Rigidbody.mass
hera-agent-unity unity_docs GameObject.AddComponent
hera-agent-unity unity_docs Vector3.zero
hera-agent-unity unity_docs UnityEditor.AssetDatabase.Refresh
```

### Regenerating the data set

The data file is generated by a Go script that mirrors the C# regex set the connector used to apply per-call:

```bash
go run ./tools/build-unity-docs \
    --in  <path-to-Documentation/en> \
    --out AgentConnector/Editor/Data/unity_docs_<bucket>.jsonl.gz.bytes \
    --unity-version <bucket>
```

Run this only when Unity ships a new docs revision or when adding a new version bucket, commit the result, cut a new connector release.

**Notes**:
- `describe_type` intentionally stays separate: it returns the project's *loaded* type schema + curated Unity pitfalls, while `unity_docs` reads the *static* docs page. Pair them when you need both.
- The bundled file is gzipped JSONL with the `.bytes` suffix so Unity imports it as a `TextAsset`; the connector decompresses via `GZipStream` on first access.

---

## game_feel

Offline Game Feel / Juice design knowledge base. Returns implementation-ready recipes — concrete px / seconds / % / Hz parameters, plus the Unity site each one applies at (`Update` vs `FixedUpdate` vs `LateUpdate`, `Rigidbody.interpolation`, `Time.timeScale`, `Selectable` transitions, Canvas rebuild cost) — with the ethical and accessibility constraints built into each topic (Honest Juice: presentation intensity must match real achievement).

The data set **ships inside the UPM connector package**, under `AgentConnector/Editor/Data/game_feel_1.0.jsonl.gz.bytes` (~40 KiB gzipped, 67 topics). The tool is always available; **Game Feel Mode (Beta)** (Hera Settings, or `asset-config gamefeel on`) additionally makes `doctor --agent-rules` and tool responses (e.g. `manage_components add` for Camera / ParticleSystem / AudioSource / Rigidbody / Light / Animator) point agents at the relevant topics via `agent_hint`. The `ui` category is also the deep layer behind **Game Feel UI Mode (Beta)** — `manage_ui create` hints end with a per-element pointer into it.

```bash
hera-agent-unity game_feel              # topic index, grouped by category (ethics first)
hera-agent-unity game_feel <topic>      # one topic body
```

### Topic categories

| Category | Topics |
|:---|:---|
| `ethics` (listed first — apply while building, not after) | `anticipation_reward`, `balanced_hurdles`, `cognitive_comfort`, `community_synergy`, `copywriting_framing`, `engagement_core`, `engagement_loop`, `engagement_scenarios`, `engagement_validation`, `ethical_boundary`, `ethics_checklist`, `friendly_signals`, `information_transparency`, `salience_balance`, `value_preservation` |
| `theory` | `context_space`, `control_feel`, `experience_arc`, `feedback_loop`, `feel_stack`, `game_feel_structure`, `input_forgiveness`, `input_response`, `juice_definition`, `metaphor_treatment`, `unity_frame_loops` |
| `technique` | `camera`, `dynamic_lighting`, `haptics`, `hit_stop`, `juice_intensity_scale`, `knockback`, `multi_layer_feedback`, `particles`, `perceived_properties`, `permanence`, `personality`, `screen_shake`, `sound`, `squash_stretch`, `tweening_easing` |
| `ui` | `accessibility_baseline`, `cognitive_load`, `diegetic_ui`, `ecn_dmn_framework`, `ui_bar`, `ui_button`, `ui_choice_symmetry`, `ui_inventory`, `ui_microinteractions`, `ui_multimodal`, `ui_notification`, `ui_number_change`, `ui_popup`, `ui_screen_transition`, `ui_visual_trends`, `visual_hierarchy` |
| `workflow` | `feel_lab`, `workflow_phases` |
| `anti_pattern` | `anti_patterns`, `golden_rule`, `honest_juice` |
| `checklist` | `checklist_action`, `checklist_all`, `checklist_casual`, `checklist_mobile`, `checklist_strategy` |

### Return shape

```json
{
  "key": "screen_shake",
  "category": "technique",
  "title": "Screen Shake",
  "body": "Definition\n... | Intensity | ... | 2–5px | 5–15px | 15–30px |\n..."
}
```

Topic bodies are a few hundred tokens each — query on demand instead of loading everything.

### Errors

| Code | Meaning |
|:---|:---|
| `GAME_FEEL_BUNDLE_UNAVAILABLE` | The bundled data file is missing or unreadable on this connector install. Reinstall the UPM package; or in a local checkout, run `go run ./tools/build-game-feel-docs`. |
| `TOPIC_NOT_FOUND` | Query did not map to any topic key. `data.did_you_mean[]` holds nearest keys; `suggestions[]` carries them as ready-to-run CLI calls. |

### Examples

```bash
hera-agent-unity game_feel
hera-agent-unity game_feel screen_shake
hera-agent-unity game_feel control_feel
hera-agent-unity game_feel honest_juice
hera-agent-unity game_feel ethics_checklist
```

### Regenerating the data set

The checked-in source of truth is `tools/build-game-feel-docs/game_feel.jsonl`. After editing it:

```bash
go run ./tools/build-game-feel-docs
```

Commit both files, cut a new connector release.

---

## ui_slop

Looks up a Unity UI-slop tell — a statistical flaw that makes generated UI look generated — together with the check that detects it and the fix that removes it.

The data set **ships inside the UPM connector package**, under `AgentConnector/Editor/Data/ui_slop_1.0.jsonl.gz.bytes`, and loads through `Core/UiSlopStore`. The tool is always available; **Unity De-slop Mode (Beta)** (Hera Settings, or `asset-config uislop on`) additionally makes `doctor --agent-rules` inject the de-slop discipline and `manage_components add` point at the relevant tell via `agent_hint` for Shadow/Outline, Image/RawImage, and TMP/Text.

```bash
hera-agent-unity ui_slop                # taxonomy index, grouped by area, with the live tell count
hera-agent-unity ui_slop <id>           # one tell
```

Tells are grouped into five areas. Inspection can run in parallel, but fixes land in area order, so an upstream fix dissolves the conflicts a downstream one would otherwise hit.

| Area | Covers |
|:---|:---|
| `A` | Decorative sweep — gradient orbs, glow, glassmorphism, sparkles, emoji icons |
| `B` | Layout, RectTransform, containers, anchors, Raycast Target, CanvasScaler |
| `C` | Spacing — the derived ladder, density, grouping, dead whitespace |
| `D` | Typography — italics, font roles, type scale, Hangul typesetting |
| `E` | Color — semantic roles, palette discipline, WCAG contrast |

### Response fields

| Field | Meaning |
|:---|:---|
| `check` | The uGUI predicate, ready to measure against the live scene |
| `exception` | Functional cases that must **not** be treated as slop (inventory slots, interactive surfaces, dense panels) |
| `fix` | The mechanical repair |
| `borrow` | A quantitative target when the tell owns one (spacing base, type scale, palette rule, WCAG thresholds); `null` otherwise |

A few tells state plainly that they need visual or semantic judgement rather than posing as measurable predicates.

| Error code | Meaning |
|:---|:---|
| `UI_SLOP_BUNDLE_UNAVAILABLE` | The bundled data file is missing or unreadable on this connector install. Reinstall the UPM package; or in a local checkout, run `go run ./tools/build-ui-slop-docs`. |
| `TELL_NOT_FOUND` | No tell matches that id. The response carries `did_you_mean` suggestions. |

```bash
hera-agent-unity ui_slop box-in-box
hera-agent-unity ui_slop unscaled-spacing-ladder
hera-agent-unity ui_slop low-contrast-text
hera-agent-unity ui_slop tmp-italic
```

### Regenerating the data set

The checked-in source of truth is `tools/build-ui-slop-docs/ui_slop.jsonl`. After editing it:

```bash
go run ./tools/build-ui-slop-docs
```

The builder validates ids, areas, severities, `deep_topic` values, and the presence of every required field before writing. Commit both files, cut a new connector release.

---

## manage_components

Component CRUD on a target GameObject. Property paths are raw `SerializedProperty` paths (`m_Name`, `m_LocalScale.x`, `m_Materials.Array.data[0]`) — no friendly-name mapping. Reference fields accept an InstanceID, an asset path, a `guid:<32hex>[:<fileId>]` handle (the `:fileId` form addresses a sub-asset such as a sprite inside a sliced sheet), a `GlobalObjectId_V1-…` string, or a `{instance_id|asset_path}` envelope.

This tool establishes the property-set pattern reused by every future `manage_*` (material / animation / vfx / scriptable objects / prefab properties).

```bash
hera-agent-unity manage_components <action> [flags]
```

### Actions

| Action | Description |
|:---|:---|
| `add`    | Attach a component. `--type` required. |
| `remove` | Detach. By `--component_id`, or by GameObject + `--type` [+ `--index`]. |
| `list`   | Every component on a GameObject (shallow). |
| `get`    | Read a component. Omit `--property` for the full property dump, or pass `--property <path>` for one. |
| `set`    | Write a single property. `--property` + `--value` required. |

### Targeting

GameObject (required for `add` / `list`, and for `remove` / `get` / `set` unless `--component_id` is given):

| Flag | Description |
|:---|:---|
| `--instance_id <N>`   | Preferred. Survives renames and reparenting. |
| `--path </Root/Child>` | Hierarchy path; fallback walk covers inactive subtrees. |

Component:

| Flag | Description |
|:---|:---|
| `--type <name>`        | Short (`Rigidbody`) or fully-qualified (`UnityEngine.Rigidbody`). Required for `add`. Used with the GameObject target for `remove` / `get` / `set`. |
| `--index <N>`          | When the GameObject has multiple of the same type, pick one (default `0`). Ignored with `--component_id`. |
| `--component_id <N>`   | Target the component directly by InstanceID — skips type + index resolution. |

Property (`get` / `set`):

| Flag | Description |
|:---|:---|
| `--property <path>` | Raw `SerializedProperty` path. For `get`, omit to dump every visible top-level property. |
| `--value <scalar>`  | Scalar value for `set`. For arrays / objects / reference envelopes use `--params '{"value": ...}'`. |

### Value shapes accepted by `set`

`SerializedPropertyValue` coerces JSON into the property type Unity expects:

| Type | Accepted JSON shapes |
|:---|:---|
| Integer / LayerMask / ArraySize | number, numeric string |
| Boolean | `true` / `false` / `"true"` / `"on"` / `1` |
| Float | number, numeric string |
| String | any (toString) |
| Character | single-char string |
| Color | `"#RRGGBB"` / `"#RRGGBBAA"` / `[r,g,b]` / `[r,g,b,a]` / `{r,g,b,a}` / `"r,g,b[,a]"` |
| Vector2 / 3 / 4 / Quaternion | `[x,y,z(,w)]` / `{x,y,z(,w)}` / `"x,y,z(,w)"` |
| Vector2Int / Vector3Int | same shapes as float vectors, int components |
| Enum | display-name string (case-insensitive) or integer index |
| ObjectReference | `123` (InstanceID), `"Assets/Mat.mat"` (asset path), `{"instance_id": N}`, `{"asset_path": "..."}` |

Reference-field set is the one to study — every future `manage_*` reuses this resolution path.

### Return shapes

`add` / `get` (full) — `{ instance_id, component: { component_id, type, type_short, enabled?, properties: { m_X: ..., ... } } }`

`get` (single property) / `set` — `{ instance_id, component_id, type, property, property_type, value }`

`list` — `{ instance_id, components: [{ component_id, type, type_short, enabled? }, ...] }`

`remove` — `{ instance_id, removed: { component_id, type, type_short, enabled? } }`

### Examples

```bash
hera-agent-unity manage_components add --path /Player --type Rigidbody
hera-agent-unity manage_components list --instance_id 12345
hera-agent-unity manage_components get --path /Player --type Rigidbody
hera-agent-unity manage_components get --path /Player --type Transform --property m_LocalScale
hera-agent-unity manage_components set --path /Player --type Rigidbody --property m_Mass --value 5
hera-agent-unity manage_components set --path /Player --type MeshRenderer --property m_Materials.Array.data[0] --value Assets/Mat.mat
hera-agent-unity manage_components set --instance_id -12345 --type Rigidbody --params '{"property":"m_CenterOfMass","value":[0,1,0]}'
hera-agent-unity manage_components remove --component_id -67890
```

**Notes**:
- `Transform` cannot be added or removed.
- After `set`, the response re-reads the property through a fresh `SerializedObject` so the returned value reflects whatever Unity actually accepted (clamps, normalisation, enum-bit canonicalisation).
- Every edit registers an `Undo` entry and marks the scene dirty.
- `PROPERTY_NOT_FOUND` errors include the list of top-level property names that *do* exist on the target component — pipe that into your next `set`.

---

## find_gameobjects

Search every loaded-scene GameObject and return a lean entry per match. Filters combine with AND; results are sorted by hierarchy path so pagination is stable across calls. The default projection is `{instance_id, name}` to keep AI discovery payloads small.

```bash
hera-agent-unity find_gameobjects [filters] [pagination]
```

### Filters

| Flag | Description |
|:---|:---|
| `--name <substr>` | Name substring, case-insensitive. |
| `--tag <name>` | Exact tag match (Unity tag system). |
| `--layer <name\|index>` | Layer name (`UI`) or integer index (`0..31`). |
| `--component <type>` | Has the given component. Short name (`Rigidbody`) or fully-qualified (`UnityEngine.Rigidbody`). |
| `--path_glob <glob>` | Hierarchy path glob. `*` = single segment, `**` = multiple segments, `?` = single non-`/` char. |
| `--include_inactive <bool>` | Default `true`. `false` = `activeInHierarchy` only. |

### Pagination

| Flag | Description | Default |
|:---|:---|:---|
| `--limit` | Max results to return. `0` = no cap. | `50` |
| `--offset` | Skip the first N matches. | `0` |

### Output projection

| Flag | Description |
|:---|:---|
| `--ids` | Return `results` as bare instance IDs. Lowest-token handoff to `manage_*` tools. |
| `--names` | Return `results` as bare names. |
| `--fields <csv>` | Return only selected object fields: `instance_id`, `name`, `path`, `scene`, `active`, `global_id` (durable handle that survives domain reloads), or `all` (all except `global_id`). Default: `instance_id,name`. |

### Return shape

```json
{
  "total":    137,
  "returned": 50,
  "offset":   0,
  "limit":    50,
  "has_more": true,
  "results": [
    { "instance_id": -12345, "name": "Player" }
  ]
}
```

`--fields all` returns the legacy verbose object shape with `path`, `scene`, and `active`.

### Examples

```bash
hera-agent-unity find_gameobjects --name Player
hera-agent-unity find_gameobjects --tag Enemy --include_inactive false
hera-agent-unity find_gameobjects --component Rigidbody --limit 20
hera-agent-unity find_gameobjects --path_glob /Root/**/Pickup
hera-agent-unity find_gameobjects --layer UI
hera-agent-unity find_gameobjects --limit 50 --offset 100
hera-agent-unity find_gameobjects --component Rigidbody --ids
hera-agent-unity find_gameobjects --name Pickup --fields instance_id,name,path
```

**Notes**:
- Prefab assets and `HideFlags.HideInHierarchy` objects are stripped — only items visible in the Hierarchy window are returned.
- Feed an `instance_id` from a result back into `manage_gameobject` for follow-up edits — it survives renames and reparenting (path can change underneath you).
- `--component` resolves through `TypeCache.GetTypesDerivedFrom<Component>()` so user-defined `MonoBehaviour`s work too.

---

## manage_gameobject

GameObject CRUD inside the active scene(s). Target by `instance_id` (preferred — survives renames and duplicates) or hierarchy `path` (`/Root/Child/...`).

```bash
hera-agent-unity manage_gameobject <action> [flags]
```

### Actions

| Action | Description |
|:---|:---|
| `create` | Make a new GameObject (empty or primitive). |
| `duplicate` | Copy the target `--count` times. Editor-fidelity (the Ctrl+D path): prefab connection, property overrides and child objects survive — unlike `Object.Instantiate`. |
| `destroy` | Delete the target GameObject (`DestroyImmediate` in edit mode, `Destroy` in play mode). |
| `move` | Set position. World by default, `--space local` for local. |
| `set_parent` | Reparent to another GameObject or unparent (`--parent none`). |
| `set_active` | Toggle `GameObject.SetActive`. |
| `set_name` | Rename. |
| `set_transform` | Set any combination of position, euler rotation, and local scale; `space` defaults to `local`. |
| `set_tag` | Assign an existing project tag. |
| `set_layer` | Assign an existing layer by name or index (`0..31`). |
| `get_transform` | Read position / rotation (euler) / scale + scene info. |

### Flags

| Flag | Description | Applies to |
|:---|:---|:---|
| `--instance_id <N>` | Target by InstanceID. Preferred. | all except `create` |
| `--path </Root/Child>` | Target by hierarchy path. Fallback walk covers inactive subtrees. | all except `create` |
| `--name <str>` | New GameObject name / rename target. | `create`, `set_name` |
| `--primitive <kind>` | `cube`, `sphere`, `capsule`, `cylinder`, `plane`, `quad`. Omit for an empty GameObject. | `create` |
| `--parent <id\|path>` | Parent reference. `none` or empty unparents (`set_parent`). | `create`, `set_parent` |
| `--position x,y,z` | World position. Also accepts JSON `[x,y,z]` or `{x,y,z}` via `--params`. | `create`, `move` |
| `--space <world\|local>` | Coordinate space. | `move` (default `world`) |
| `--rotation x,y,z` | Euler rotation. | `set_transform` |
| `--scale x,y,z` | Local scale. | `set_transform` |
| `--tag <name>` | Existing project tag. | `set_tag` |
| `--layer <name\|0..31>` | Existing project layer. | `set_layer` |
| `--active <true\|false>` | Active state. | `set_active` |
| `--world_position_stays <true\|false>` | Match `Transform.SetParent` flag. | `set_parent` (default `true`) |
| `--count <N>` | Number of copies (default `1`, max `100`). With `--name`, copies are suffixed ` (1)`, ` (2)`, … | `duplicate` |

### Examples

```bash
hera-agent-unity manage_gameobject create --name Player
hera-agent-unity manage_gameobject create --name Cube --primitive cube --position 0,1,0
hera-agent-unity manage_gameobject duplicate --path /Enemies/Goblin --count 5 --name Goblin
hera-agent-unity manage_gameobject move --instance_id 12345 --position 5,0,0
hera-agent-unity manage_gameobject set_parent --path /Player --parent /Root
hera-agent-unity manage_gameobject set_parent --path /Player --parent none
hera-agent-unity manage_gameobject set_active --path /Player --active false
hera-agent-unity manage_gameobject set_name --instance_id 12345 --name Hero
hera-agent-unity manage_gameobject set_transform --path /Player --params '{"rotation":[0,90,0],"scale":[2,2,2]}'
hera-agent-unity manage_gameobject set_tag --path /Player --tag Player
hera-agent-unity manage_gameobject set_layer --path /Player --layer Characters
hera-agent-unity manage_gameobject get_transform --path /Root/Player
```

### Return shape

All actions except `duplicate` return a depth-1 snapshot:

```json
{
  "instance_id": 12345,
  "name": "Player",
  "path": "/Root/Player",
  "scene": "Main",
  "scene_path": "Assets/Scenes/Main.unity",
  "active": true,
  "tag": "Player",
  "layer": 0,
  "layer_name": "Default",
  "transform": {
    "position": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale":    { "x": 1.0, "y": 1.0, "z": 1.0 },
    "local_position": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "local_rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "local_scale":    { "x": 1.0, "y": 1.0, "z": 1.0 }
  }
}
```

`duplicate` returns the source plus the clones it made:

```json
{
  "source": { "instance_id": 12345, "name": "Goblin" },
  "count": 5,
  "clones": [
    { "instance_id": 12350, "name": "Goblin (1)", "path": "/Enemies/Goblin (1)" }
  ]
}
```

**Notes**:
- Every action calls `EditorSceneManager.MarkSceneDirty` — save the scene afterward to persist changes.
- All edits register `Undo` entries so the user can `Ctrl+Z` your AI agent.
- `duplicate` uses Unity's own duplicate command, so it clobbers the editor copy/paste buffer (same as pressing `Ctrl+D`). The prior selection is restored afterward.
- `create` in play mode produces a runtime GameObject that Unity discards on play exit — expected behavior, not a bug.

---

## menu

Execute a Unity menu item by path, or discover available items with `menu list`.

```bash
hera-agent-unity menu "<path>"
hera-agent-unity menu list [--filter <substr>] [--limit <N>]
```

```bash
hera-agent-unity menu "File/Save Project"
hera-agent-unity menu "Assets/Refresh"
hera-agent-unity menu "Window/General/Console"
```

### menu list

Discover menu items declared with the `[MenuItem]` attribute.

| Flag | Description | Default |
|:---|:---|:---|
| `--filter <substr>` | Case-insensitive substring match on the menu path. Omit to get top-level groups instead of a flat list. | |
| `--limit <N>` | Max items returned when filtering. | `300` |

Without `--filter`, the response is the **top-level groups and their counts**, not a flat list — a project can declare hundreds of items (the bundled Unity 6 editor alone exposes ~300), so the grouped view keeps the payload tiny and never silently truncates the agent's context. Drill in with `--filter`.

```bash
hera-agent-unity menu list                  # -> { total, groups: [ { name, count } ] }
hera-agent-unity menu list --filter Assets  # -> { total, returned, truncated, items: [...] }
hera-agent-unity menu list --filter "Tools/" --limit 50
```

**Notes**:
- Only `[MenuItem]`-attributed items are listed. Native built-in menus (e.g. `File/Save`) carry no attribute and are not enumerated, but can still be executed by path.
- When a filtered result is capped at `--limit`, the response sets `truncated: true` and an `agent_hint` so a partial list is never mistaken for a complete one.
- `File/Quit` is blocked for execution for safety.

---

## screenshot

Capture a screenshot of the Unity editor, active ScreenSpaceOverlay canvases,
or an isolated GameObject. Game View captures can also return bounded,
identity-first uGUI or visible 3D collider metadata, or return that metadata
alone without rendering or writing PNG pixels.

```bash
hera-agent-unity screenshot [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--view` | `scene` or `game` | `scene` |
| `--overlay` | Render active non-world root canvases instead of a Scene/Game view; cannot be combined with isolated or annotation modes | `false` |
| `--width` | Image width in pixels | `1920` |
| `--height` | Image height in pixels | `1080` |
| `--output_path` | Output path (absolute or relative to project) | unique PNG under `Screenshots/` |
| `--overwrite` | Approval-gated replacement of an existing PNG under the project or system temp directory; existing external files are never overwritten | `false` |
| `--isolated` | Render only one target GameObject through a temporary camera | `false` |
| `--target` / `--path` | Hierarchy path for isolated capture | |
| `--instance_id` | InstanceID for isolated capture | |
| `--angles` | Comma-separated `iso`, `front`, `back`, `left`, `right`, `top`, `bottom`; multiple angles become one contact sheet | `iso` |
| `--background` | `#RRGGBB`, `#RRGGBBAA`, or `transparent` | `#2B2B2BFF` |
| `--padding` | Isolated camera padding fraction | `0.15` |
| `--annotate_ui` | Add active uGUI Selectable identity, interaction, blocking, and coordinate metadata to a Game View capture | `false` |
| `--annotations_only` | Return UI metadata without rendering or writing PNG pixels; implies Game View annotation and rejects PNG output/overwrite flags | `false` |
| `--max_annotations` | Maximum number of UI elements returned (`1..100`) | `32` |
| `--annotate_physics` | Add visible 3D collider identity and coordinates sampled through the live `Camera.main` | `false` |
| `--physics_only` | Return 3D physics evidence without rendering or writing PNG pixels; implies Game View physics annotation and rejects PNG output/overwrite flags | `false` |
| `--physics_grid_size` | Square sampling density (`1..16`, so at most 256 rays) | `9` |
| `--max_physics_hits` | Maximum clustered collider candidates returned (`1..100`) | `32` |
| `--physics_layer_mask` | Signed 32-bit physics layer mask, intersected with `Camera.main.cullingMask` | camera culling mask |
| `--physics_max_distance` | Positive ray distance, at most 100000 world units | camera far clip plane |
| `--physics_query_triggers` | `use_global`, `ignore`, or `collide` | `use_global` |
| `--editor_ui_only` | Return bounded UI Toolkit metadata for one loaded `EditorWindow`, without pixels or file output | `false` |
| `--editor_window` | Exact loaded window type, full type name, or title | required in editor UI mode |
| `--editor_selector` | Optional exact `#name` or `VisualElement` type | all elements |
| `--max_editor_elements` | Maximum returned elements (`1..500`) | `100` |

```bash
hera-agent-unity screenshot
hera-agent-unity screenshot --view game
hera-agent-unity screenshot --overlay --output_path captures/overlay.png
hera-agent-unity screenshot --view game --annotate_ui --max_annotations 50
hera-agent-unity screenshot --annotations_only
hera-agent-unity screenshot --physics_only --physics_grid_size 9
hera-agent-unity screenshot --view game --annotate_physics --physics_layer_mask -1
hera-agent-unity screenshot --editor_ui_only --editor_window InspectorWindow --max_editor_elements 50
hera-agent-unity screenshot --width 3840 --height 2160
hera-agent-unity screenshot --output_path captures/my_scene.png
hera-agent-unity screenshot --isolated --target /Player --output_path captures/player.png
hera-agent-unity screenshot --isolated --target /Player --angles front,right,top --background transparent
```

UI annotation entries identify each target with `instance_id` and
`hierarchy_path`, then report `interactable`, `blocked_by`, target raycast state,
and point/bounds coordinates. `input_point` / `input_bounds` use Unity screen
pixels with a bottom-left origin and Y increasing upward. `image_point` /
`image_bounds` use Game View pixels with a top-left origin and Y increasing
downward. The response repeats these names and dimensions under
`coordinate_spaces`; PNG editor-window dimensions are kept separate because a
captured Game View window can include editor chrome. Results are path-sorted,
bounded by `max_annotations`, and report total/skipped/truncated counts. An
active `EventSystem` is required. Isolated capture cannot be combined with UI
annotation.

Editor UI metadata mode traverses one loaded UI Toolkit window and returns each
element's exact hierarchy path, visibility, enabled state, and finite layout
rectangle. It does not capture pixels and rejects all pixel/evidence/isolated
capture options. `--editor_selector` accepts only an exact `#name` or exact
element type name.

Physics entries identify both the hit GameObject and its 3D `Collider`, then
report layer, representative hit distance/point/normal, sample count, and
input/image point and sampled coverage bounds. Hera casts one nearest-hit ray
per bounded grid cell, intersects the requested physics mask with the live
camera culling mask, groups samples by collider, sorts by sample count and
stable identity, and truncates only after clustering. `physics_raycast` reports
the camera identity, requested/camera/effective masks, grid and ray counts,
distance, and trigger policy. An active camera tagged `MainCamera` is required.
This path does not scan scene colliders and does not include Physics2D.

---

## describe_shader

Inspect a shader's properties, or search shader names. Read-only — pair it with `manage_material` ("learn the properties, then set them").

```bash
hera-agent-unity describe_shader "<name>"          # describe one shader
hera-agent-unity describe_shader --list [--filter <substr>]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--list` | List/search shader names instead of describing one. | off |
| `--filter <substr>` | (list) Case-insensitive name filter. | — |
| `--limit <n>` | get: max properties. list: max shaders. | 60 / 50 |
| `--include_builtin <bool>` | (list) Include built-in shaders. | `true` |

`get` returns `{ name, property_count, truncated, properties: [{ name, type, display?, range? }] }` — `type` is `Color/Float/Range/Vector/TexEnv/Int`; `range` is `[min, max]` for Range. A missing shader returns `SHADER_NOT_FOUND` with `did_you_mean` suggestions.

```bash
hera-agent-unity describe_shader "Universal Render Pipeline/Lit"
hera-agent-unity describe_shader --list --filter URP
```

---

## manage_material

Material asset CRUD. Paths must be under `Assets/`; `create` requires a new `.mat` destination with an existing parent folder. Property names are shader property names (`_BaseColor`, `_Metallic`, `_MainTex`) — run `describe_shader` first to discover them.

```bash
hera-agent-unity manage_material <action> --path <Assets/...mat> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `create` | `--shader <name>` | Create a material bound to a shader. |
| `get` | `[--property <name>]` | Dump all property values, or one. |
| `set` | `--property <name> --value <v>` | Set one property and save. |
| `set_shader` | `--shader <name>` | Swap the shader. |

Values reuse the `manage_components` forms: `1,0,0,1` or `#RRGGBB` for colors, a number for floats, `x,y,z,w` for vectors, and an asset path or InstanceID for textures.

```bash
hera-agent-unity manage_material create --path Assets/Mats/Player.mat --shader "Universal Render Pipeline/Lit"
hera-agent-unity manage_material set --path Assets/Mats/Player.mat --property _BaseColor --value 1,0,0,1
hera-agent-unity manage_material set --path Assets/Mats/Player.mat --property _MainTex --value Assets/Tex/skin.png
```

---

## manage_prefab

Prefab asset and instance operations. Asset actions take `--path` (under `Assets/`; `create` requires a new `.prefab` destination with an existing parent folder). Instance actions take `--target`, a scene prefab instance.

`add_component` / `remove_component` edit the prefab asset **headlessly** (`PrefabUtility.LoadPrefabContents` → edit → save → unload — no prefab stage, no open-scene side effects).

```bash
hera-agent-unity manage_prefab <action> [--path <Assets/...prefab> | --target <instance>] [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `create` | `--source </Root/Child>` or `--instance_id <id>` | Save a scene GameObject as a new prefab asset. Saving from a prefab instance produces a Variant; the result reports `asset_type`. |
| `instantiate` | `[--parent </path> or <id>]` | Drop the prefab into the active scene. |
| `add_component` | `--component <Type> [--child </Root/Child>]` | Add a component to the prefab root, or to a descendant. |
| `remove_component` | `--component <Type> [--child </Root/Child>]` | Remove a component from the prefab root, or from a descendant. |
| `list_overrides` | `--target <instance> [--include_default]` | How the instance differs from its asset. |
| `apply` | `--target <instance>` | Write the instance's overrides into the asset. Approval-gated. |
| `revert` | `--target <instance>` | Discard the instance's overrides. Approval-gated. |
| `unpack` | `--target <instance> --mode <outermost\|completely>` | Break the prefab link. Approval-gated. |

**Overrides**: `list_overrides` returns `{instance_root, asset_path, asset_type, status, has_overrides, include_default, object_overrides, added_components, removed_components, added_gameobjects, removed_gameobjects}`. Unity classifies the instance root's own Transform and name as *default overrides* and hides them, so without `--include_default` an empty list does **not** mean the instance matches its asset — a moved root reports nothing until the flag is passed.

**Targets**: `--target` accepts a hierarchy path, an InstanceID, or a durable handle, and resolves to the outermost prefab instance root because Unity's apply/revert/unpack APIs reject a child. Every response reports the `instance_root` it acted on. A target outside any prefab instance returns `NOT_A_PREFAB_INSTANCE`.

**unpack --mode** is required. `outermost` keeps nested prefab instances connected; `completely` unpacks them too. The difference is invisible in a flat project and destructive in a nested one, so there is no default.

```bash
hera-agent-unity manage_prefab create --source /Player --path Assets/Prefabs/Player.prefab
hera-agent-unity manage_prefab add_component --path Assets/Prefabs/Player.prefab --component Rigidbody
hera-agent-unity manage_prefab add_component --path Assets/Prefabs/Player.prefab --child /Player/Arm --component BoxCollider
hera-agent-unity manage_prefab instantiate --path Assets/Prefabs/Player.prefab --parent /Spawns
hera-agent-unity manage_prefab list_overrides --target /Player
hera-agent-unity manage_prefab apply --target /Player
hera-agent-unity manage_prefab revert --target /Player
hera-agent-unity manage_prefab unpack --target /Player --mode outermost
```

---

## manage_asset_import

Read or change an asset's import settings through its `AssetImporter` (`TextureImporter`, `ModelImporter`, `AudioImporter`, …). The target path must be under `Assets/`. Same SerializedObject pattern as `manage_components`, applied to the importer; property paths are raw SerializedProperty paths.

```bash
hera-agent-unity manage_asset_import <action> --path <Assets/...> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `get` | `[--property <m_X>]` | Dump all import settings, or one (run with no `--property` to discover names). |
| `set` | `--property <m_X> --value <v>` | Set one setting, then `SaveAndReimport`. |

```bash
hera-agent-unity manage_asset_import get --path Assets/Tex/icon.png
hera-agent-unity manage_asset_import set --path Assets/Tex/icon.png --property m_sRGBTexture --value 0
hera-agent-unity manage_asset_import set --path Assets/Tex/icon.png --property m_EnableMipMap --value false
```

---

## manage_ui

uGUI authoring. The value-add over `manage_components`'s raw `m_` paths is
**RectTransform anchor/pivot math**, plus Canvas and EventSystem scaffolding.

```bash
hera-agent-unity manage_ui <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `create` | `--element <kind>` `[--name <n>]` `[--content <text>]` `[--text tmp\|legacy]` `[--parent </path> or <id>]` | Create `canvas`, `panel`, `image`, `button`, `text`, or `empty`, auto-creating Canvas + EventSystem. The shared EventSystem policy adds the input module selected by Unity's active input-handling defines and, in an exclusive mode, disables an incompatible built-in module already present. |
| `get_rect` | `--instance_id <id>` or `--path </path>` | Read the full RectTransform. |
| `set_anchor` | `--preset <name>` or `--anchor_min x,y --anchor_max x,y`; `[--snap true]` `[--pivot x,y]` | Re-anchor a RectTransform. |
| `set_rect` | `[--anchored_position x,y]` `[--size_delta x,y]` `[--pivot x,y]` `[--offset_min x,y]` `[--offset_max x,y]` | Set RectTransform fields. |

**Text engine** — `create text` / `create button` use TextMeshPro when the package is present, else the legacy `UnityEngine.UI.Text`; force either with `--text tmp` / `--text legacy`.

**Anchor presets** — `<vertical>-<horizontal>` where vertical ∈ {`top`, `middle`, `bottom`, `stretch`} and horizontal ∈ {`left`, `center`, `right`, `stretch`}: `top-left`, `top-center`, `top-right`, `middle-left`, `middle-center`, `middle-right`, `bottom-left`, `bottom-center`, `bottom-right`, `top-stretch`, `middle-stretch`, `bottom-stretch`, `stretch-left`, `stretch-center`, `stretch-right`, and `stretch` (full).

```bash
hera-agent-unity manage_ui create --element button --name PlayBtn --content Play
hera-agent-unity manage_ui create --element text --name Title --content Hello --text legacy
hera-agent-unity manage_ui get_rect --path /Canvas/PlayBtn
hera-agent-unity manage_ui set_anchor --path /Canvas/Title --preset top-center
hera-agent-unity manage_ui set_anchor --path /Canvas/Bg --preset stretch --snap true
hera-agent-unity manage_ui set_rect --path /Canvas/Title --anchored_position 0,-40 --size_delta 300,60
```

**Game Feel UI Mode (Beta)** — when enabled (Hera Settings window, or `asset-config gamefeel-ui on`), each `create` response carries an `agent_hint` with concrete juice recipes for the element just made: hover/press/release easing, popup overshoot with symmetric choice buttons (ethics built in), rarity-laddered reward presentation, damage-number/count-up timing with critical specs, dual-response bars with charge/cooldown patterns, ECN-DMN density guidance and accessibility baselines at the canvas level. The recipe is DOTween-aware: with DOTween enabled in Hera Settings it suggests `DOScale`-based tweens, otherwise a coroutine/lerp fallback. Each hint ends with a pointer into the `game_feel` knowledge base (`ui` category) for the full tables and theory. The hint is advisory — element property edits still go through `manage_components`. When the mode is off, no hint is added.

---

## input

Unity-level input QA for uGUI and projects using the optional Input System package. This command is for the case where an external automation surface cannot acquire Unity screenshot state and therefore cannot safely click physical screen coordinates. It does **not** claim to be a physical OS click. EventSystem actions verify Unity's UI event path through `EventSystem.RaycastAll` and `ExecuteEvents`; Input System actions synthesize current keyboard and mouse device state during Play Mode.

```bash
hera-agent-unity input <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `state` | `[--backend eventsystem\|inputsystem\|auto]`; `[--max_results N]` | Report EventSystem/raycaster status, or optional Input System availability, package version, current devices, and controls held by Hera. |
| `inspect` | `--path </path>` or `--instance_id <id>` or `--target <path\|id>`; `[--position x,y]`; `[--normalized x,y]`; `[--offset x,y]`; `[--details true]`; `[--max_results N]` | Resolve the target point, raycast through the EventSystem, and report top hit, blocker, handlers, and interactability. |
| `click` | same target/point flags; `[--button left\|right\|middle]`; `[--click_count N]`; `[--hold_ms N]`; `[--settle_frames N]`; `[--strict true\|false]`; `[--details true]`; `[--max_results N]` | Drive pointer enter/down/up/click through `ExecuteEvents`. In strict mode, fails if another object blocks the target or the expected click handler is not reached. |
| `pointer_down` | same target/point flags | Drive pointer enter/down without a matching up. Useful for press-state QA; no cross-command press state is retained. |
| `pointer_up` | same target/point flags | Drive pointer up at the target point. Useful as a standalone handler check; no cross-command press state is retained. |
| `submit` | `--path </path>` or `--instance_id <id>` or `--target <path\|id>`; `[--settle_frames N]`; `[--strict true\|false]`; `[--max_results N]` | Select the target and execute `ISubmitHandler` through `ExecuteEvents.submitHandler`. |
| `scroll` | same target/point flags; `[--scroll_delta x,y]` or `[--delta x,y]`; `[--settle_frames N]`; `[--strict true\|false]`; `[--max_results N]` | Execute `IScrollHandler` through `ExecuteEvents.ExecuteHierarchy`. Default scroll delta is `0,-1`. |
| `drag` | same target/point flags; `--to_position x,y` or `--to x,y` or `--to_normalized x,y`; `[--steps N]`; `[--settle_frames N]`; `[--strict true\|false]`; `[--max_results N]` | Execute initialize-potential-drag, begin-drag, drag steps, and end-drag handlers. Default steps: 8. |
| `keyboard` | `--key <InputSystem Key>`; `[--mode press\|down\|up]`; `[--hold_ms N]`; `[--settle_frames N]`; `[--backend inputsystem\|auto]` | Press/release a current Input System keyboard key in Play Mode. `down` remains held until the matching `up`; `press` releases automatically. |
| `mouse` | `[--mode move\|click\|down\|up\|delta\|scroll]`; `[--button left\|right\|middle]`; `[--position x,y]`; `[--delta x,y]`; `[--scroll_delta x,y]`; `[--hold_ms N]`; `[--settle_frames N]`; `[--backend inputsystem\|auto]` | Move, click, hold/release, or set delta/scroll on the current Input System mouse in Play Mode. |
| `sequence` | strict `steps` JSON array through `call input --json ...` | Execute 1..32 PlayMode Input System `keyboard`/`mouse` steps in one Unity request. Nested sequences, read actions, and EventSystem actions are rejected. |
| `record` | `--mode start\|stop\|status`; start: `[--path <file.json>]` | Sample real current Input System keyboard/mouse state after configured Input System updates. Start and capture require Play Mode; stop/status remain available after Play Mode exits. |
| `replay` | `--path <file.json>` | Validate and replay a `hera.input-recording/1` file in Play Mode with recorded frame timing and sequence-owned cleanup. |

```bash
hera-agent-unity input state
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
hera-agent-unity input submit --path /Canvas/StartButton
hera-agent-unity input scroll --path /Canvas/ScrollRect --scroll_delta 0,-3
hera-agent-unity input drag --path /Canvas/Slider/Handle --to_normalized 0.8,0.5
hera-agent-unity input state --backend inputsystem
hera-agent-unity input keyboard --key space --mode press
hera-agent-unity input mouse --mode click --button left --position 640,360
hera-agent-unity call input --json '{"action":"sequence","steps":[{"action":"keyboard","key":"space","mode":"down"},{"action":"keyboard","key":"space","mode":"up"}]}'
hera-agent-unity call input --json '{"action":"record","mode":"start"}'
hera-agent-unity call input --json '{"action":"record","mode":"stop"}'
hera-agent-unity call input --json '{"action":"replay","path":"Library/HeraAgent/Recordings/input-20260810-120000-abcd1234.json"}'
```

**Windows Git Bash** — MSYS path conversion treats a Unity hierarchy path that
starts with `/` as a filesystem path. Preserve it with `MSYS_NO_PATHCONV=1`:

```bash
MSYS_NO_PATHCONV=1 hera-agent-unity input inspect --path /Canvas/StartButton --details true
```

**Input limits** — numeric values are validated before dispatch: `hold_ms` is `0..5000`, `settle_frames` is `0..120`, drag `steps` is `1..120`, `click_count` is `1..3`, and `max_results` is `1..100` (default `50`). Oversized or malformed values return `INPUT_INVALID_PARAM`. `raycasters_total` / `raycasters_truncated` and detailed `hits_total` / `hits_truncated` make a capped EventSystem diagnostic explicit.

**Input sequences** — `sequence` is PlayMode-only and accepts 1..32 strict Input System keyboard/mouse step objects. The Connector validates the complete JSON shape, action-specific requirements, device/control availability, aggregate hold time (`<=30000 ms`), aggregate awaited frames (`<=600`), and sequence-local down/up ownership before the first mutation. A sequence has a 45-second wall-clock deadline, fails at the first leaf error, rejects any pre-existing Hera-held control, and releases controls acquired by that sequence in `finally`. The compact result reports `completed_count`, `failed_step_index`, `cause_code`, cleanup details, and `held_after`. A response-loss outcome remains operation-ledger protected and is never blindly retried.

**Input recordings** — `record start` samples after the project's configured Input System update (`dynamic`, `fixed`, or `manual`) and stores only keyboard/button transitions plus changed mouse position and non-zero delta/scroll. The `hera.input-recording/1` JSON format is capped at 256 events, 600 relative frames, 30 seconds, and 512 KiB. A default output is a unique file under `Library/HeraAgent/Recordings/`; an explicit path must be a new `.json` file under the project or system temp directory. Existing files are never overwritten. Play Mode exit stops capture, while `record stop` writes the pending file; an active recording is also saved before script reload. `replay` reads and validates the entire bounded file before mutation, preserves captured frame gaps, uses the sequence preflight/ownership rules, and always reports cleanup plus `held_after`. Replaying the same balanced file repeatedly does not retain Hera-held state. Unsupported or unloaded Input System packages fail with `INPUTSYSTEM_UNAVAILABLE`; Hera does not add the package as a dependency.

**Optional Input System** — the Connector resolves `Unity.InputSystem` through reflection and does not add `com.unity.inputsystem` to the package manifest or asmdef. `input state --backend inputsystem` remains queryable without the package and reports `available:false`; keyboard/mouse/sequence mutations return `INPUTSYSTEM_UNAVAILABLE`. Mutations require active, unpaused Play Mode and an existing current device. Hera never creates a device. Held keys/buttons are owned across commands, reject duplicate down or unowned up, and are released when Play Mode exits or scripts reload.

**Evidence classification** — report this separately from OS-level click QA:

```text
Physical OS click QA: BLOCKED if Computer Use cannot acquire Unity screenshot state.
Unity EventSystem input QA: PASS when input inspect/click reaches the target through EventSystem.RaycastAll and ExecuteEvents.
Unity Input System QA: PASS when keyboard/mouse output and device state confirm the synthesized gameplay input.
```

Current backend status:

| Backend | Status |
|:---|:---|
| `eventsystem` | Implemented for `state`, `inspect`, `click`, `pointer_down`, `pointer_up`, `submit`, `scroll`, and `drag`. |
| `inputsystem` | Implemented for `state`, `keyboard`, `mouse`, bounded `sequence`, `record`, and `replay`. Keyboard/mouse/sequence/record/replay select it by default; EventSystem actions do not auto-switch to it. |
| `native-win32` | Planned optional fallback; never a default backend. |

---

## reserialize

Force reserialize assets (rewrite YAML/JSON with current Unity version).

```bash
hera-agent-unity reserialize [path...]
```

```bash
# Reserialize entire project
hera-agent-unity reserialize

# Reserialize specific assets
hera-agent-unity reserialize Assets/Scenes/Main.unity
hera-agent-unity reserialize Assets/Prefabs/A.prefab Assets/Prefabs/B.prefab
```

---

## test

Run Unity Test Framework tests.

```bash
hera-agent-unity test [flags]
```

| Subcommand | Description |
|:---|:---|
| *(none)* | Run the selected tests and wait for results |
| `list` | List the tests that exist; runs nothing |
| `cancel` | End the active run and release its pending-run lock |

| Flag | Description | Default |
|:---|:---|:---|
| `--mode` | `EditMode` or `PlayMode` | `EditMode` |
| `--filter` | Run: NUnit test/group name. List: substring matched against each full test name | `""` |
| `--category` | Comma-separated NUnit `[Category]` names | `""` |
| `--assembly` | Comma-separated test assembly names (no `.dll`) | `""` |
| `--limit` | `list` only: maximum tests returned | `200` |
| `--resume` | Continue waiting for an existing `run_id` without starting tests again | `""` |

```bash
# EditMode tests
hera-agent-unity test

# PlayMode tests
hera-agent-unity test --mode PlayMode

# What exists, without running anything
hera-agent-unity test list
hera-agent-unity test list --category Smoke

# Selected tests
hera-agent-unity test --filter MyNamespace.MyClass
hera-agent-unity test --category Smoke
hera-agent-unity test --assembly MyGame.Tests

# Release a hung run
hera-agent-unity test cancel

# Resume a slow existing run; no second run_tests request is sent
hera-agent-unity --port 8094 test --resume <run_id> --timeout 300000
```

**Selectors**: `--filter`, `--category`, and `--assembly` intersect. A run
narrowed by any of them that matches nothing returns `NO_TESTS_MATCHED`
rather than reporting a pass, because a selector typo is not a green build.
An unfiltered run of a project with no tests remains a success. Run
`test list` first to see the exact full names, assemblies, and categories
that exist.

**Discovery payload**: without a selector, `test list` returns per-assembly
and per-category counts so a large project cannot flood the response; with a
selector it returns `{mode, total, returned, truncated, tests:[{full_name,
assembly, categories}]}`. `Uncategorized` never appears — it is the test
framework's placeholder, not a selectable category.

**Cancellation**: `test cancel` asks the test framework to stop the run using
the guid `TestRunnerApi.Execute` returned (persisted in the pending-run
record, so it survives the PlayMode domain reload) and always clears that
record. Clearing it is what releases `TEST_RUN_ALREADY_RUNNING`; a client
already waiting on the run receives `TEST_RUN_CANCELLED`.

**Test-run behavior**: Both modes start asynchronously and persist their final
result to `~/.hera-agent-unity/status/test-results-<port>-<run_id>.json`. The CLI polls
that file until completion for every `test` invocation; there is no `--wait`
flag. Use the global `--timeout` to bound the wait. If that deadline arrives
while the Connector's `test-pending-<port>-<run_id>.json` record still exists,
the CLI returns the stable error code `TEST_RUN_PENDING` with the exact `port`
and `run_id`. This is a resumable "result not written yet" state, not evidence
that the Editor is unresponsive. Run `test --resume <run_id>` against that
same project or port with a longer timeout. Resume only polls the existing
result and never starts a duplicate test run.

For CLI/connector version transitions, the connector also writes the legacy
`test-results-<port>.json` result file. A current CLI falls back to that file
when an older connector returns `{ port }` without `run_id`. A current CLI
sends the internal `async_results=true` capability. Without that capability,
the connector preserves the legacy synchronous EditMode response, while
PlayMode remains compatible through the legacy result file.

---

## task

Inspect the same durable test and Package Manager state used by MCP Tasks,
without sending an HTTP request to Unity or waiting for a fresh heartbeat.

```bash
hera-agent-unity --project <full-path> task list
hera-agent-unity --project <full-path> task status <task_id>
```

| Action | Description |
|:---|:---|
| `list` | List active pending tasks owned by the selected project and current port. Returns reusable opaque `task_id` values. |
| `status <task_id>` | Return `working` or `completed` and include the durable result when available. Accepts IDs returned by either CLI `task list` or negotiated MCP Tasks. |

`task list` is intentionally an active-work discovery command. Unity removes a
pending record after writing its result, so completed work may disappear from
subsequent lists. Keep the returned `task_id`; `task status` can still resolve
the matching result after the pending record is gone. Both actions are local,
read-only, and project-scoped. They do not start, repeat, or cancel Unity work.

---

## profiler

Control the Unity Profiler.

```bash
hera-agent-unity profiler <action> [flags]
```

| Action | Description |
|:---|:---|
| `hierarchy` | Show top-level profiler samples |
| `enable` | Start profiler recording |
| `disable` | Stop profiler recording |
| `status` | Show profiler state |
| `clear` | Clear all captured frames |
| `stats` | One-call render/memory/frame snapshot without a capture: draw calls, setPass calls, batches, triangles/vertices, frame/render ms, and allocated/reserved/mono/graphics memory. `render_available` reports whether render statistics could be read. |

**Hierarchy flags**:

| Flag | Description | Default |
|:---|:---|:---|
| `--depth` | Recursive depth (0=unlimited) | `1` |
| `--root` | Set root by name (substring match) | `""` |
| `--frames` | Average over last N frames | `1` |
| `--parent` | Drill into item by ID | `0` |
| `--min` | Filter items below threshold (ms) | `0` |
| `--sort` | `total` or `self` | `total` |

```bash
hera-agent-unity profiler hierarchy
hera-agent-unity profiler hierarchy --depth 5 --frames 30
hera-agent-unity profiler enable
```

---

## list

List registered tools. Three detail levels, cheapest first — the per-tool
parameter schema is the bulk of the bytes, so it's opt-in rather than dumped
up front:

| Form | Returns | Use when |
|:---|:---|:---|
| `list --names` | flat array of tool names only | cheapest discovery |
| `list --compact` | same as `list --names` | compact catalogue discovery from agents or scripts (the AGENTS.md bootstrap runs this) |
| `list` | `{name, description}` per tool, no schema | you want a one-line hint per tool |
| `list --tool <name>` | full parameter + output schema + metadata + action descriptors for one tool | you're about to call that tool |

```bash
hera-agent-unity list --compact
hera-agent-unity list --names
hera-agent-unity list
hera-agent-unity list --tool exec
```

Useful for discovering custom tools added to the project.

`actions` is an ordinal-sorted list of discovered action descriptors (`name`,
`description`). It contains only handlers with the supported `public static
JObject -> object|Task<object>|Task` contract. `metadata.safety` describes the whole tool. Multi-action tools may also expose
`metadata.action_safety`, so agents can treat a read-only action such as
`manage_assets find` differently from destructive actions such as
`manage_assets move` or `manage_assets delete`. This detail is only returned by
`list --tool <name>`; `list --compact` stays names-only.

---

## status

Show current Unity Editor state from the heartbeat file.

```bash
hera-agent-unity status
```

**Output example**:
```text
Unity (port 8090): ready
  Project: /Users/admin/Unity/MyProject
  Version: 6000.0.35f1
  Docs:    6000.0
  Compiler: csc=unity_dotnet_sdk_roslyn dotnet=unity_netcore_runtime
  PID:     12345
```

`Docs` is the Hera docs bucket selected for the running Editor. `Compiler`
summarizes the resolved C# compiler/runtime source by kind; full paths are
available in `doctor --json`.

---

## update

Self-update the CLI binary from GitHub releases.

```bash
hera-agent-unity update [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--check` | Check for updates without installing | `false` |

```bash
hera-agent-unity update --check
hera-agent-unity update
```

---

## version

Show CLI version.

```bash
hera-agent-unity version
```

---

## asset-config

Manage asset preferences, verification/guidance modes, and default compiler
paths through the interactive TUI or command-based interface.

```bash
hera-agent-unity asset-config <subcommand>
```

| Subcommand | Description |
|:---|:---|
| (no args) | Interactive checkbox UI |
| `list` | List all assets with status |
| `enable <id>` | Enable an asset |
| `disable <id>` | Disable an asset |
| `toggle <id>` | Flip an asset ON/OFF |
| `gamefeel [on\|off]` | Show or set Game Feel Mode (Beta) (gameplay game-feel guidance via `game_feel` + agent rules) |
| `gamefeel-ui [on\|off]` | Show or set Game Feel UI Mode (Beta) (drives `manage_ui` juice guidance); `juicy` is a legacy alias |
| `uislop [on\|off]` | Show or set Unity De-slop Mode (Beta) (static UI-slop cleanup guidance via `ui_slop` + agent rules) |
| `set-csc <path>` | Persist the default C# compiler path used by `exec` when `--csc` is omitted |
| `set-dotnet <path>` | Persist the default dotnet host path used by `exec` when `--dotnet` is omitted |
| `detect` | Auto-detect installed assets (requires Unity) |
| `get <id>` | Show a single asset's state |
| `path` | Print the config file path |

| Flag | Description | Default |
|:---|:---|:---|
| `--json` | Output enabled assets with descriptions/documentation URLs + `loop_engineering_mode` + `game_feel_mode` + `game_feel_ui_mode` + `ui_slop_mode` + `dotween_preferred` as JSON | `false` |

Asset configuration updates are serialized through a local lock and atomically
replace the JSON file. Unknown fields and asset entries are retained. If two
clients change the same recognized setting concurrently, the last completed
write wins. Enabling a known asset means "prefer this API when installed"; it
does not install the asset. Enabled preferences are included in generated
`doctor --agent-rules` output. TUI quit waits for a successful save and leaves
the UI open with the error when persistence fails.

---

## batch

Execute several commands in one HTTP round trip to Unity. The batch travels and
returns together, so the response stays atomic and ordered.

```bash
hera-agent-unity batch [--file <path.json>] [--dry-run]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--file` | JSON file describing the commands; stdin when omitted | |
| `--dry-run` | Print the parsed plan without sending it to Unity | `false` |

`batch` is for straight sequential execution. Conditional branching, passing
data between steps, or anything resembling a workflow belongs in individual
calls driven by a shell script or the agent itself.

---

## log

Write a message to the Unity console. Cheaper than `exec "Debug.Log(...)"`
because there is no C# compile step.

```bash
hera-agent-unity log "<message>" [--level <log|warning|error>]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--level` | `log`, `warning`, or `error` | `log` |

---

## ping

Token-cheap liveness probe. Reads the heartbeat file only — no Unity HTTP round
trip and no instance discovery beyond a filesystem scan.

```bash
hera-agent-unity ping
```

Output is a single line, e.g. `port=8090 alive=1 state=ready age_ms=42`. Exit
code is `0` when alive within 3s and `1` otherwise. Use `status` for the richer
human-readable view.

---

## doctor

Self-diagnostic. Reports the running binary path, what `hera-agent-unity`
resolves to on PATH, duplicate installs, shell-specific gotchas, and any Unity
instances visible to the Connector.

```bash
hera-agent-unity doctor [--json] [--agent-rules]
```

| Flag | Description |
|:---|:---|
| `--json` | Structured envelope (binary, shell, unity) |
| `--agent-rules` | Print the embedded agent guide, including the Ultra Hera verification loop at the configured level |

Does not require Unity to be running. Reach for this first when the binary is
not found, resolves to the wrong copy, or cannot see your Editor.

---

## install / uninstall

```bash
hera-agent-unity install
hera-agent-unity uninstall
```

`install` copies the running binary to the canonical install directory for the
platform and makes it reachable on PATH — `~/.local/bin` on Linux and macOS,
`%LOCALAPPDATA%\Microsoft\WindowsApps` on Windows. Install locations left by
earlier `hera-agent` / `hera-agent-pro` versions are scrubbed automatically.

`uninstall` removes the installed binary and the CLI configuration files. On
Windows, files still locked by the running process are cleaned up on the next
run. Neither command touches the Unity UPM package in your projects.

---

## Custom Tool Invocation

Any `[HeraTool]` class can be called directly by its snake_case name:

```bash
# Call a custom tool directly
hera-agent-unity my_custom_tool

# Call with parameters
hera-agent-unity my_custom_tool --params '{"key":"value"}'
```

Use `hera-agent-unity list` to discover available tools.

---

## Related Documentation

- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) — Writing custom tools
