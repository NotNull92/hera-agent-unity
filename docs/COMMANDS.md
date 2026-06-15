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

Material asset CRUD. Property names are shader property names (`_BaseColor`, `_Metallic`, `_MainTex`) — run `describe_shader` first to discover them.

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

Prefab asset operations. `add_component` / `remove_component` edit the prefab asset **headlessly** (`PrefabUtility.LoadPrefabContents` → edit → save → unload — no prefab stage, no open-scene side effects) and target the prefab root.

```bash
hera-agent-unity manage_prefab <action> --path <Assets/...prefab> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `create` | `--source </Root/Child>` or `--instance_id <id>` | Save a scene GameObject as a new prefab asset. |
| `instantiate` | `[--parent </path> or <id>]` | Drop the prefab into the active scene. |
| `add_component` | `--component <Type>` | Add a component to the prefab root. |
| `remove_component` | `--component <Type>` | Remove a component from the prefab root. |

```bash
hera-agent-unity manage_prefab create --source /Player --path Assets/Prefabs/Player.prefab
hera-agent-unity manage_prefab add_component --path Assets/Prefabs/Player.prefab --component Rigidbody
hera-agent-unity manage_prefab instantiate --path Assets/Prefabs/Player.prefab --parent /Spawns
```

---

## manage_asset_import

Read or change an asset's import settings through its `AssetImporter` (`TextureImporter`, `ModelImporter`, `AudioImporter`, …). Same SerializedObject pattern as `manage_components`, applied to the importer; property paths are raw SerializedProperty paths.

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

uGUI authoring. The value-add over `manage_components`'s raw `m_` paths is the **RectTransform anchor/pivot math**: named anchor presets and visual-position-preserving re-anchoring. UI and TextMeshPro types resolve through `TypeCache`, so the connector still compiles in a project without `com.unity.ugui` installed. Element *property* edits (Image color, Button colors, Text font) stay in `manage_components`.

```bash
hera-agent-unity manage_ui <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `create` | `--element <kind>` `[--name <n>]` `[--content <text>]` `[--text tmp\|legacy]` `[--parent </path> or <id>]` | Create a UI element. Kinds: `canvas`, `panel`, `image`, `button`, `text`, `empty`. Auto-creates a Canvas + EventSystem when one is missing; non-canvas elements default to the existing/auto Canvas as parent. |
| `get_rect` | `--instance_id <id>` or `--path </path>` | Read the full RectTransform (anchors, pivot, offsets, size) + detected preset. |
| `set_anchor` | `--preset <name>` or `--anchor_min x,y --anchor_max x,y`; `[--snap true]` `[--pivot x,y]` | Re-anchor. By default the rect stays visually fixed (offsets recomputed); `--snap` zeroes offsets / fills and moves the pivot to match (Unity's Alt+Shift click). |
| `set_rect` | `[--anchored_position x,y]` `[--size_delta x,y]` `[--pivot x,y]` `[--offset_min x,y]` `[--offset_max x,y]` | Set any subset of RectTransform fields directly. |

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

**UI Juicy Mode** — when enabled (Hera Settings window, or `asset-config juicy on`), each `create` response carries an `agent_hint` with concrete Game UI/UX Bible juice recipes for the element just made (hover/press/release easing, squash & stretch, popup overshoot, damage-number/count-up timing, haptics). The recipe is DOTween-aware: with DOTween enabled in Hera Settings it suggests `DOScale`-based tweens, otherwise a coroutine/lerp fallback. The hint is advisory — element property edits still go through `manage_components`. When the mode is off, no hint is added.

---

## ui_doc

HTML→Unity UI pipeline (uGUI). The agent is fluent in HTML/CSS but weak at uGUI; `ui_doc` closes the gap by giving it two **deterministic** endpoints plus a compact JSON IR (`ui_doc/1`) as the contract:

- **`export`** serializes a live UI subtree to the IR (defaults omitted) — *grounding* so the agent maps an HTML design onto the project's real structure instead of guessing.
- **`apply`** builds an IR document under a parent (always-create) and reports a compact summary.
- **`gen_sprite`** bakes a Tier-1 procedural sprite (CSS-shape vocabulary) and imports it — **no external dependency**.

> The agent owns the creative middle (image/text → HTML mockup → IR); `ui_doc` owns the Unity-side read/write. Bitmap art (illustrations) is out of scope — it would require an external image model.

```bash
hera-agent-unity ui_doc <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `export` | `--path </path>` or `--instance_id <id>`; `[--depth N]` | Serialize the subtree to the `ui_doc/1` IR. Depth defaults to 8. |
| `apply` | `--file <doc.json>`; `[--parent </path> or <id>]`; `[--mode create\|upsert]` | Realize the IR under the parent (default: existing/auto Canvas). `create` (default) always makes new objects; `upsert` matches existing children by name and updates rect/graphic/text in place (no duplicates, no deletes). Pass the doc via `--file` so it never rides inline in context. |
| `gen_sprite` | `--spec '{...}'` or `--kind/--size/--color/...`; `[--out Assets/...]` | Bake + import a sprite. Kinds: `solid`, `rounded_rect`, `gradient`, `nine_slice` (rounded box + 9-slice `border [l,b,r,t]`, default = radius). Default out: `Assets/HeraGenerated/`. |

**IR shape (`ui_doc/1`)** — a node tree; defaults are omitted on export (`anchor` uses the same preset names as `manage_ui set_anchor`, else `anchor_min`/`anchor_max`):

```jsonc
{ "schema": "ui_doc/1", "backend": "ugui",
  "root": {
    "name": "Panel", "element": "panel",            // canvas|panel|image|button|text|empty
    "rect": { "anchor": "stretch", "size": [400, 600] },
    "image": { "color": "#1A1A2EFF", "sprite": { "gen": { "kind": "rounded_rect", "radius": 12 } } },
    "children": [
      { "name": "PlayBtn", "element": "button",
        "rect": { "anchor": "top-center", "pos": [0, -40], "size": [240, 64] },
        "text": { "value": "Play", "engine": "auto" } }
    ] } }
```

`image.sprite` is either `{ "asset": "Assets/..." }` (existing) or `{ "gen": {<spec>} }` (baked on apply). `text.engine` is `auto` / `tmp` / `legacy`.

```bash
# Ground on the current UI, hand the IR to the agent
hera-agent-unity ui_doc export --path /Canvas/HUD

# Apply an agent-authored design
hera-agent-unity ui_doc apply --file design.json --parent /Canvas

# Bake a button background
hera-agent-unity ui_doc gen_sprite --spec '{"kind":"rounded_rect","size":[240,64],"color":"#1A1A2EFF","radius":12}' --out Assets/UI/btn_bg.png
```

**UI Juicy Mode** — when enabled, `apply` adds an `agent_hint` with the Game UI/UX Bible juice recipes for each *distinct* element type in the doc (deduped once, not per element — strong signature, lean tokens). Guidance only; no runtime components are attached.

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
| `toggle <id>` | Flip an asset ON/OFF |
| `juicy [on\|off]` | Show or set UI Juicy Mode (drives `manage_ui` juice guidance) |
| `detect` | Auto-detect installed assets (requires Unity) |
| `get <id>` | Show a single asset's state |
| `path` | Print the config file path |

| Flag | Description | Default |
|:---|:---|:---|
| `--json` | Output enabled assets + `ui_juicy_mode` + `dotween_preferred` as JSON | `false` |

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
