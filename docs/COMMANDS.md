# Command Reference

Complete reference of all `hera-agent-unity` commands, flags, and parameters.

---

## Global Flags

These flags work with any command:

| Flag | Description | Default | Example |
|:---|:---|:---|:---|
| `--port` | Select Unity instance by active heartbeat port | Auto-discover | `--port 8091` |
| `--project` | Select Unity instance by project path | Auto-discover | `--project /path/to/project` |
| `--timeout` | Request timeout in milliseconds | `60000` (1 min) | `--timeout 300000` |
| `--verbose` | Print progress + per-phase timings to stderr | `false` | `--verbose` |

---

## editor

Control Unity Editor play mode and asset database.

```bash
hera-agent-unity editor <action> [flags]
```

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
hera-agent-unity editor stop
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
| `--no-cache` | Skip compile/assembly cache (debug only) | `false` |

```bash
# Basic execution
hera-agent-unity exec "return 1+1;"

# Unity API access
hera-agent-unity exec "return Application.dataPath;"

# Pipe to avoid shell escaping
echo 'return EditorSceneManager.GetActiveScene().name;' | hera-agent-unity exec

# Custom usings for ECS
hera-agent-unity exec "return World.All.Count;" --usings Unity.Entities
```

**Default usings**: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Reflection`, `System.Threading.Tasks`, `UnityEngine`, `UnityEngine.SceneManagement`, `UnityEditor`, `UnityEditor.SceneManagement`, `UnityEditorInternal`

**Note**: Use `return` for output. Use `return null;` for void operations.

**Caching**: Compiled assemblies are cached in `Library/HeraAgentCache/` and held in memory keyed by source hash. The first call per Unity session is the cold path (csc invocation); identical follow-up calls skip both compile and load. Cache invalidates automatically on assembly reload. Use `--no-cache` to bypass.

---

## console

Read, filter, and clear Unity console logs.

```bash
hera-agent-unity console [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--lines` | Limit to N entries | All |
| `--type` | Comma-separated: `error`, `warning`, `log` | `error,warning,log` |
| `--stacktrace` | `none`, `user`, `full` | `user` |
| `--clear` | Clear console after reading | `false` |

```bash
hera-agent-unity console
hera-agent-unity console --lines 20 --type error
hera-agent-unity console --stacktrace full
hera-agent-unity console --clear
```

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
| `load <path\|name>` | Open a scene by asset path or bare filename. |
| `save [<path\|name>]` | Save the active scene, or a named loaded scene if specified. |
| `list` | List scenes registered in Build Settings. |
| `close <path\|name>` | Unload a loaded scene. Cannot close the only loaded scene. |

### Flags

| Flag | Description | Default | Applies to |
|:---|:---|:---|:---|
| `--mode` | `single`, `additive`, or `additive_without_loading` | `single` | `load` |

### Examples

```bash
hera-agent-unity scene info
hera-agent-unity scene load Assets/Scenes/Main.unity
hera-agent-unity scene load Main --mode additive
hera-agent-unity scene save
hera-agent-unity scene close Lobby
hera-agent-unity scene list
```

**Notes**:
- `load --mode single` refuses to run if the active scene is dirty — save it first or load additively.
- `close` refuses if the target scene is dirty.
- Name resolution uses `AssetDatabase.FindAssets` with an exact filename match (case-insensitive).

---

## manage_packages

Drive `UnityEditor.PackageManager.Client` from the CLI. Replaces hand-editing `Packages/manifest.json` — the Package Manager API owns the project lock and validates git URLs that a manual edit would mishandle.

```bash
hera-agent-unity manage_packages <action> [identifier]
```

### Actions

| Action | Sync? | Description |
|:---|:---:|:---|
| `list` | ✅ | Every package the project currently resolves to (incl. indirect dependencies). |
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
{ "job_id": "pkg-9c4f12a8", "port": 8090, "action": "add", "identifier": "com.unity.ai.navigation" }
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

## unity_docs

Offline Unity ScriptReference lookup. Returns a slim, JSON-ready shape suitable for AI agents who need to verify an API exists at this Unity version before running it through `exec`. No network, no rate limits.

The data set (~31k ScriptReference entries, gzipped ~1.2 MiB) **ships inside the UPM connector package itself**, under `AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes`. Installing the connector is the only prerequisite — there is no docs folder to point at, no environment variable, no asset-config entry. The CLI passes the query straight through; the connector loads the bundled data once per domain and serves every subsequent lookup from an in-memory dictionary.

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
- On miss the response carries `data.did_you_mean[]` populated from a Levenshtein scan of the 31k-key index.

### Return shape

```json
{
  "query": "Rigidbody.mass",
  "query_normalized": "Rigidbody.mass",
  "title": "Rigidbody.mass",
  "signature": "public float mass;",
  "summary": "The mass of the rigidbody.",
  "manual_url": "Manual/class-Rigidbody.html",
  "scriptreference_url": "ScriptReference/Rigidbody-mass.html",
  "unity_version": "6.0"
}
```

Typically 250–400 bytes per call.

### Errors

| Code | Meaning |
|:---|:---|
| `DOCS_BUNDLE_UNAVAILABLE` | The bundled data file is missing or unreadable on this connector install. Reinstall the UPM package; or in a local checkout, rerun `go run ./tools/build-unity-docs`. |
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
    --out AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes
```

Run this only when Unity ships a new docs revision (or a new version line — `unity_docs_6.1.jsonl.gz.bytes` etc.), commit the result, cut a new connector release.

**Notes**:
- `describe_type` intentionally stays separate: it returns the project's *loaded* type schema + curated Unity pitfalls, while `unity_docs` reads the *static* docs page. Pair them when you need both.
- The bundled file is gzipped JSONL with the `.bytes` suffix so Unity imports it as a `TextAsset`; the connector decompresses via `GZipStream` on first access.

---

## manage_components

Component CRUD on a target GameObject. Property paths are raw `SerializedProperty` paths (`m_Name`, `m_LocalScale.x`, `m_Materials.Array.data[0]`) — no friendly-name mapping. Reference fields accept an InstanceID, an asset path, or a `{instance_id|asset_path}` envelope.

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

Search every loaded-scene GameObject and return a shallow entry per match. Filters combine with AND; results are sorted by hierarchy path so pagination is stable across calls.

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

### Return shape

```json
{
  "total":    137,
  "returned": 50,
  "offset":   0,
  "limit":    50,
  "has_more": true,
  "results": [
    { "instance_id": -12345, "name": "Player",
      "path": "/Root/Player", "scene": "Main", "active": true }
  ]
}
```

### Examples

```bash
hera-agent-unity find_gameobjects --name Player
hera-agent-unity find_gameobjects --tag Enemy --include_inactive false
hera-agent-unity find_gameobjects --component Rigidbody --limit 20
hera-agent-unity find_gameobjects --path_glob /Root/**/Pickup
hera-agent-unity find_gameobjects --layer UI
hera-agent-unity find_gameobjects --limit 50 --offset 100
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
| `destroy` | Delete the target GameObject (`DestroyImmediate` in edit mode, `Destroy` in play mode). |
| `move` | Set position. World by default, `--space local` for local. |
| `set_parent` | Reparent to another GameObject or unparent (`--parent none`). |
| `set_active` | Toggle `GameObject.SetActive`. |
| `set_name` | Rename. |
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
| `--active <true\|false>` | Active state. | `set_active` |
| `--world_position_stays <true\|false>` | Match `Transform.SetParent` flag. | `set_parent` (default `true`) |

### Examples

```bash
hera-agent-unity manage_gameobject create --name Player
hera-agent-unity manage_gameobject create --name Cube --primitive cube --position 0,1,0
hera-agent-unity manage_gameobject move --instance_id 12345 --position 5,0,0
hera-agent-unity manage_gameobject set_parent --path /Player --parent /Root
hera-agent-unity manage_gameobject set_parent --path /Player --parent none
hera-agent-unity manage_gameobject set_active --path /Player --active false
hera-agent-unity manage_gameobject set_name --instance_id 12345 --name Hero
hera-agent-unity manage_gameobject get_transform --path /Root/Player
```

### Return shape

All actions return a depth-1 snapshot:

```json
{
  "instance_id": 12345,
  "name": "Player",
  "path": "/Root/Player",
  "scene": "Main",
  "scene_path": "Assets/Scenes/Main.unity",
  "active": true,
  "transform": {
    "position": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale":    { "x": 1.0, "y": 1.0, "z": 1.0 }
  }
}
```

**Notes**:
- Every action calls `EditorSceneManager.MarkSceneDirty` — save the scene afterward to persist changes.
- All edits register `Undo` entries so the user can `Ctrl+Z` your AI agent.
- `create` in play mode produces a runtime GameObject that Unity discards on play exit — expected behavior, not a bug.

---

## menu

Execute a Unity menu item by path.

```bash
hera-agent-unity menu "<path>"
```

```bash
hera-agent-unity menu "File/Save Project"
hera-agent-unity menu "Assets/Refresh"
hera-agent-unity menu "Window/General/Console"
```

**Note**: `File/Quit` is blocked for safety.

---

## screenshot

Capture a screenshot of the Unity editor.

```bash
hera-agent-unity screenshot [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--view` | `scene` or `game` | `scene` |
| `--width` | Image width in pixels | `1920` |
| `--height` | Image height in pixels | `1080` |
| `--output_path` | Output path (absolute or relative to project) | `Screenshots/screenshot.png` |

```bash
hera-agent-unity screenshot
hera-agent-unity screenshot --view game
hera-agent-unity screenshot --width 3840 --height 2160
hera-agent-unity screenshot --output_path captures/my_scene.png
```

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

| Flag | Description | Default |
|:---|:---|:---|
| `--mode` | `EditMode` or `PlayMode` | `EditMode` |
| `--filter` | Filter by namespace, class, or full test name | `""` |
| `--wait` | Wait for PlayMode tests to complete | `false` (EditMode: always waits) |

```bash
# EditMode tests (synchronous)
hera-agent-unity test

# PlayMode tests (asynchronous, requires --wait)
hera-agent-unity test --mode PlayMode --wait

# Filtered tests
hera-agent-unity test --filter MyNamespace.MyClass
```

**PlayMode behavior**: Returns `"running"` immediately. Results are written to `~/.hera-agent-unity/status/test-results-<port>.json`. The CLI polls this file when `--wait` is set.

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

List all registered tools with their parameter schemas.

```bash
hera-agent-unity list
```

Useful for discovering custom tools added to the project.

---

## status

Show current Unity Editor state.

```bash
hera-agent-unity status
```

**Output example**:
```json
{
  "state": "ready",
  "compiling": false,
  "compileErrors": false,
  "projectPath": "/Users/admin/Unity/MyProject",
  "processId": 12345,
  "port": 8090
}
```

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

Manage asset configuration (interactive TUI or command-based).

```bash
hera-agent-unity asset-config <subcommand>
```

| Subcommand | Description |
|:---|:---|
| (no args) | Interactive checkbox UI |
| `list` | List all assets with status |
| `enable <id>` | Enable an asset |
| `disable <id>` | Disable an asset |
| `detect` | Auto-detect installed assets (requires Unity) |

| Flag | Description | Default |
|:---|:---|:---|
| `--json` | Output enabled assets as JSON | `false` |

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
