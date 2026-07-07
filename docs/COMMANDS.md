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

## manage_assets

Compact `AssetDatabase` operations for common file, folder, and asset-authoring work. Paths are constrained to `Assets/`.

```bash
hera-agent-unity manage_assets <action> [flags]
```

| Action | Required flags | Description |
|:---|:---|:---|
| `find` | `--filter`, `--type`, or both | Search assets and return compact `{path,guid,name,type}` entries. |
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
| `--params '{"properties":{...}}'` | `create` only: raw SerializedProperty name → value map applied to the new asset. The response reports `applied` / `failed` per field. | |

```bash
hera-agent-unity manage_assets find --type Texture2D --filter icon --limit 20
hera-agent-unity manage_assets mkdir --path Assets/Generated/UI
hera-agent-unity manage_assets create --type GameConfig --path Assets/Config/Game.asset
hera-agent-unity manage_assets create --type EnemyStats --path Assets/Data/Goblin.asset --params '{"properties":{"m_MaxHealth":30}}'
hera-agent-unity manage_assets copy --path Assets/A.prefab --new_path Assets/B.prefab
hera-agent-unity manage_assets move --path Assets/Old.asset --new_path Assets/New.asset
hera-agent-unity manage_assets delete --path Assets/Generated/Temp.asset
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

Offline Game Feel / Juice design knowledge base. Returns implementation-ready recipes — concrete px / seconds / % / Hz parameters — curated from the *Game Feel & Juice Bible*, the *Ethical Engagement Game Feel Framework*, the *UI Feedback Design Guide*, and *UI/UX Visual Theory & Trends*, with the ethical and accessibility constraints built into each topic (Honest Juice: presentation intensity must match real achievement).

The data set **ships inside the UPM connector package**, under `AgentConnector/Editor/Data/game_feel_1.0.jsonl.gz.bytes` (~38 KiB gzipped, 54 topics). The tool is always available; **Game Feel Mode (Beta)** (Hera Settings, or `asset-config gamefeel on`) additionally makes `doctor --agent-rules` and tool responses (e.g. `manage_components add` for Camera / ParticleSystem / AudioSource / Rigidbody / Light / Animator) point agents at the relevant topics via `agent_hint`. The `ui` category is also the deep layer behind **Game Feel UI Mode (Beta)** — `manage_ui create` / `ui_doc apply` hints end with a per-element pointer into it.

```bash
hera-agent-unity game_feel              # topic index, grouped by category (ethics first)
hera-agent-unity game_feel <topic>      # one topic body
```

### Topic categories

| Category | Topics |
|:---|:---|
| `ethics` (listed first — apply while building, not after) | `engagement_core`, `engagement_loop`, `value_preservation`, `anticipation_reward`, `balanced_hurdles`, `community_synergy`, `cognitive_comfort`, `salience_balance`, `copywriting_framing`, `information_transparency`, `engagement_scenarios`, `friendly_signals`, `engagement_validation`, `ethics_checklist` |
| `theory` | `juice_definition`, `game_feel_structure`, `feedback_loop`, `control_feel` |
| `technique` | `tweening_easing`, `squash_stretch`, `particles`, `screen_shake`, `hit_stop`, `knockback`, `camera`, `sound`, `haptics`, `permanence`, `personality`, `dynamic_lighting` |
| `ui` | `ui_button`, `ui_popup`, `ui_number_change`, `ui_bar`, `ui_notification`, `ui_inventory`, `ui_screen_transition`, `ui_microinteractions`, `ui_multimodal`, `ui_choice_symmetry`, `ecn_dmn_framework`, `cognitive_load`, `ui_trends_2026`, `glassmorphism_neumorphism`, `accessibility_baseline` |
| `workflow` | `workflow_phases` |
| `anti_pattern` | `golden_rule`, `honest_juice`, `anti_patterns`, `balancing_principles` |
| `checklist` | `checklist_all`, `checklist_action`, `checklist_casual`, `checklist_mobile` |

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
| `--fields <csv>` | Return only selected object fields: `instance_id`, `name`, `path`, `scene`, `active`, or `all`. Default: `instance_id,name`. |

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
  "transform": {
    "position": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale":    { "x": 1.0, "y": 1.0, "z": 1.0 }
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

Capture a screenshot of the Unity editor or an isolated GameObject.

```bash
hera-agent-unity screenshot [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--view` | `scene` or `game` | `scene` |
| `--width` | Image width in pixels | `1920` |
| `--height` | Image height in pixels | `1080` |
| `--output_path` | Output path (absolute or relative to project) | `Screenshots/screenshot.png` |
| `--isolated` | Render only one target GameObject through a temporary camera | `false` |
| `--target` / `--path` | Hierarchy path for isolated capture | |
| `--instance_id` | InstanceID for isolated capture | |
| `--angles` | Comma-separated `iso`, `front`, `back`, `left`, `right`, `top`, `bottom`; multiple angles become one contact sheet | `iso` |
| `--background` | `#RRGGBB`, `#RRGGBBAA`, or `transparent` | `#2B2B2BFF` |
| `--padding` | Isolated camera padding fraction | `0.15` |

```bash
hera-agent-unity screenshot
hera-agent-unity screenshot --view game
hera-agent-unity screenshot --width 3840 --height 2160
hera-agent-unity screenshot --output_path captures/my_scene.png
hera-agent-unity screenshot --isolated --target /Player --output_path captures/player.png
hera-agent-unity screenshot --isolated --target /Player --angles front,right,top --background transparent
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

**Game Feel UI Mode (Beta)** — when enabled (Hera Settings window, or `asset-config gamefeel-ui on`), each `create` response carries an `agent_hint` with concrete juice recipes for the element just made, curated from the Game Feel & Juice Bible, the UI Feedback Design Guide, UI/UX Visual Theory & Trends, and the Ethical Engagement Framework: hover/press/release easing, popup overshoot with symmetric choice buttons (ethics built in), rarity-laddered reward presentation, damage-number/count-up timing with critical specs, dual-response bars with charge/cooldown patterns, ECN-DMN density guidance and accessibility baselines at the canvas level. The recipe is DOTween-aware: with DOTween enabled in Hera Settings it suggests `DOScale`-based tweens, otherwise a coroutine/lerp fallback. Each hint ends with a pointer into the `game_feel` knowledge base (`ui` category) for the full tables and theory. The hint is advisory — element property edits still go through `manage_components`. When the mode is off, no hint is added.

---

## input

Unity input QA for uGUI. This command is for the case where an external automation surface cannot acquire Unity screenshot state and therefore cannot safely click physical screen coordinates. It does **not** claim to be a physical OS click. It verifies Unity's UI event path by using `EventSystem.RaycastAll` and `ExecuteEvents` pointer handlers inside the running Editor.

```bash
hera-agent-unity input <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `state` | `[--backend eventsystem]` | Report EventSystem, input module, raycaster, InputSystem availability, and native-Windows backend status. |
| `inspect` | `--path </path>` or `--instance_id <id>` or `--target <path\|id>`; `[--position x,y]`; `[--normalized x,y]`; `[--offset x,y]`; `[--details true]` | Resolve the target point, raycast through the EventSystem, and report top hit, blocker, handlers, and interactability. |
| `click` | same target/point flags; `[--button left\|right\|middle]`; `[--click_count N]`; `[--hold_ms N]`; `[--settle_frames N]`; `[--strict true\|false]`; `[--details true]` | Drive pointer enter/down/up/click through `ExecuteEvents`. In strict mode, fails if another object blocks the target or the expected click handler is not reached. |
| `pointer_down` | same target/point flags | Drive pointer enter/down without a matching up. Useful for press-state QA; no cross-command press state is retained. |
| `pointer_up` | same target/point flags | Drive pointer up at the target point. Useful as a standalone handler check; no cross-command press state is retained. |
| `submit` | `--path </path>` or `--instance_id <id>` or `--target <path\|id>`; `[--settle_frames N]`; `[--strict true\|false]` | Select the target and execute `ISubmitHandler` through `ExecuteEvents.submitHandler`. |
| `scroll` | same target/point flags; `[--scroll_delta x,y]` or `[--delta x,y]`; `[--settle_frames N]`; `[--strict true\|false]` | Execute `IScrollHandler` through `ExecuteEvents.ExecuteHierarchy`. Default scroll delta is `0,-1`. |
| `drag` | same target/point flags; `--to_position x,y` or `--to x,y` or `--to_normalized x,y`; `[--steps N]`; `[--settle_frames N]`; `[--strict true\|false]` | Execute initialize-potential-drag, begin-drag, drag steps, and end-drag handlers. Default steps: 8. |

```bash
hera-agent-unity input state
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
hera-agent-unity input submit --path /Canvas/StartButton
hera-agent-unity input scroll --path /Canvas/ScrollRect --scroll_delta 0,-3
hera-agent-unity input drag --path /Canvas/Slider/Handle --to_normalized 0.8,0.5
```

**Evidence classification** — report this separately from OS-level click QA:

```text
Physical OS click QA: BLOCKED if Computer Use cannot acquire Unity screenshot state.
Unity EventSystem input QA: PASS when input inspect/click reaches the target through EventSystem.RaycastAll and ExecuteEvents.
```

Current backend status:

| Backend | Status |
|:---|:---|
| `eventsystem` | Implemented for `state`, `inspect`, `click`, `pointer_down`, `pointer_up`, `submit`, `scroll`, and `drag`. |
| `inputsystem` | Planned; not selected by `auto` yet. |
| `native-win32` | Planned optional fallback; never a default backend. |

---

## ui_doc

HTML→Unity UI pipeline (uGUI). The agent is fluent in HTML/CSS but weak at uGUI; `ui_doc` closes the gap by giving it **deterministic** endpoints plus a compact JSON IR (`ui_doc/2`) as the contract:

- **`export`** serializes a live UI subtree to the IR (defaults omitted) — *grounding* so the agent maps an HTML design onto the project's real structure instead of guessing.
- **`apply`** builds or upserts an IR document under a parent and reports a compact summary plus the active official uGUI docs bucket, deterministic fixes, and diagnostics.
- **`import`** copies your own sprite files (absolute paths — a downloaded UI kit, exported art) into the project as `Sprite` assets so `apply` can reference them by `Assets/` path. Optional per-sprite 9-slice `border`, `ppu`, `filter`, `pivot`. GIFs are skipped (Unity has no GIF→Sprite import).
- **`gen_sprite`** bakes a Tier-1 procedural sprite (CSS-shape vocabulary) and imports it — **no external dependency**.
- **`capture`** renders the live UI to a PNG so the agent can *see* what it built and compare it to the reference. ScreenSpaceOverlay canvases are composited after the camera, so a normal `screenshot` misses them; `capture` routes every root non-world canvas through a throwaway camera + RenderTexture.
- **`sample`** reads measured hex colors out of a reference image (point or region averages). Lets the agent *measure* colors instead of eyeballing them. Runs CLI-side — no Unity round-trip — since it only reads a static file.
- **`catalog`** scans a folder of UI sprites into a manifest (size, alpha, dominant palette, a conservative 9-slice border suggestion, a filename-derived element guess). The vision-capable agent then reads the listed PNGs to classify them and compose a mockup from your own art. CLI-side — no Unity round-trip. GIFs are catalogued reference-only.

> The agent owns the creative middle (image/text → HTML mockup → IR); `ui_doc` owns the Unity-side read/write. Bitmap art (illustrations) is out of scope — it would require an external image model.
>
> **Measure-don't-guess loop**: `sample` the reference for exact colors → author the IR → `apply` → `capture` → compare to the reference → fix the largest discrepancy → repeat. This turns "eyeball and rationalize" into "measure and correct".

```bash
hera-agent-unity ui_doc <action> [flags]
```

| Action | Flags | Description |
|:---|:---|:---|
| `export` | `--path </path>` or `--instance_id <id>`; `[--depth N]` | Serialize the subtree to the `ui_doc/2` IR. Depth defaults to 8. |
| `apply` | `--file <doc.json>`; `[--parent </path> or <id>]`; `[--mode create\|upsert]` | Realize the IR under the parent (default: existing/auto Canvas). `create` (default) always makes new objects; `upsert` matches existing children by name and updates rect/graphic/text in place (no duplicates, no deletes). Before realizing, runs the official uGUI docs fixer for the current Unity bucket. Pass the doc via `--file` so it never rides inline in context. |
| `import` | `--src <abs path>` **or** `--file <imports.json>`; `[--into Assets/...]`; `[--border l,b,r,t]`; `[--ppu N]`; `[--filter point\|bilinear]`; `[--pivot x,y]` | Copy external sprite file(s) into the project as `Sprite` assets. Single sprite via `--src` + shared flags; many (with per-sprite settings) via `--file` `{into?, items:[{src, name?, border?, ppu?, filter?, pivot?}]}`. Default dest: `Assets/HeraImported/`. A `border` sets `Image.type = Sliced` (FullRect mesh). GIFs are skipped. Returns `{into, imported:[{src,asset,instance_id,sliced}], skipped, errors, count}`. |
| `gen_sprite` | `--spec '{...}'` or `--kind/--size/--color/...`; `[--out Assets/...]` | Bake + import a sprite. Kinds: `solid`, `rounded_rect`, `gradient`, `nine_slice` (rounded box + 9-slice `border [l,b,r,t]`, default = radius). Default out: `Assets/HeraGenerated/`. |
| `capture` | `[--out <file.png>]`; `[--width N] [--height N]`; `[--bg #RRGGBBAA]`; `[--canvas </path> or <id>]` | Render the live overlay UI to a PNG. Size defaults to the canvas pixel size (current game view); `bg` defaults to opaque dark (`alpha 0` = transparent); without `--canvas` it captures all root non-world canvases. Default out: a temp file. Returns `{path,width,height,bytes,canvases}`. |
| `sample` | `--image <ref.png>`; `--at "x,y"` and/or `--region "x,y,w,h"`; `[--kernel N]` | Read measured colors from a reference image. Coordinates are **normalized [0,1], top-left origin**; `;`-separate several (`--at "0.5,0.5;0.1,0.2"`). Points are averaged over a `±kernel` px box (default 2) to shrug off antialiasing. Returns each as `{at/region, px, hex, rgba}`. CLI-side — no Unity needed. |
| `catalog` | `--dir <abs folder>`; `[--max N]` | Recursively scan a folder of UI sprites into a manifest. Per image: `{path, format, w, h, aspect, has_alpha, opaque_bounds, palette, nine_slice_hint, name_hint}` (defaults omitted). `nine_slice_hint` is `[left,bottom,right,top]`, ready to pass to `import --border`. GIFs get `{animated, frames, reference_only}` (not importable). Undecodable Unity-only formats (tga/psd/exr…) are listed with `decoded:false`. `--max` caps the count (default 300). CLI-side — no Unity needed. |

**IR shape (`ui_doc/2`)** — a node tree; defaults are omitted on export (`anchor` uses the same preset names as `manage_ui set_anchor`, else `anchor_min`/`anchor_max`). Full reference: [`UI_DOC_IR.md`](UI_DOC_IR.md).

```jsonc
{ "schema": "ui_doc/2", "backend": "ugui",
  "root": {
    "name": "Panel", "element": "panel",            // canvas|panel|image|button|text|empty
    "rect": { "anchor": "stretch", "size": [400, 600] },
    "image": { "color": "#1A1A2EFF", "sprite": { "gen": { "kind": "rounded_rect", "radius": 12 } } },
    "children": [
      { "name": "PlayBtn", "element": "button",
        "rect": { "anchor": "top-center", "pos": [0, -40], "size": [240, 64] },
        "text": { "value": "Play", "engine": "auto", "color": "#FFFFFFFF", "align": "center" } }
    ] } }
```

`image.sprite` is either `{ "asset": "Assets/..." }` (existing) or `{ "gen": {<spec>} }` (baked on apply; a `nine_slice` border auto-sets `Image.type = Sliced`). `text.engine` is `auto` / `tmp` / `legacy`; `text.color` is `#hex` or `r,g,b[,a]`; `text.align` is `center` / `left` / `right` / `top-left` / `top-center`; `text.font` is an asset path to a TMP_FontAsset (or legacy Font) — also how you set an icon-font glyph.

```bash
# Ground on the current UI, hand the IR to the agent
hera-agent-unity ui_doc export --path /Canvas/HUD

# Apply an agent-authored design
hera-agent-unity ui_doc apply --file design.json --parent /Canvas

# Scan your own UI kit, then bring the chosen sprites into the project
hera-agent-unity ui_doc catalog --dir /Users/me/Downloads/SciFiUIKit
hera-agent-unity ui_doc import --src .../btn_blue.png --into Assets/UI --border 16,16,16,16

# Bake a button background
hera-agent-unity ui_doc gen_sprite --spec '{"kind":"rounded_rect","size":[240,64],"color":"#1A1A2EFF","radius":12}' --out Assets/UI/btn_bg.png

# Measure exact colors off the reference before authoring the IR
hera-agent-unity ui_doc sample --image ref.png --at "0.5,0.12;0.5,0.95" --region "0.1,0.8,0.3,0.05"

# Render what you built and compare it to the reference
hera-agent-unity ui_doc capture --out /tmp/built.png
```

`apply` returns the current docs bucket and the fixer result:

```jsonc
{
  "created": 4,
  "updated": 0,
  "sprites": 1,
  "docs_version": "6000.5",
  "ugui_package": "com.unity.ugui@2.5",
  "manual_url": "https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/index.html",
  "fixes": [
    { "rule": "image.fill_type", "path": "/Canvas/HP", "message": "Set Image.type to Filled because image.fill is present." }
  ],
  "diagnostics": [
    { "rule": "canvas_scaler.no_reference_resolution", "severity": "warning", "path": "/", "message": "..." }
  ],
  "errors": [],
  "root_id": 12345
}
```

The fixer mutates only deterministic IR shape problems backed by the official
uGUI manuals, such as stretched RectTransforms missing offsets or filled Images
missing `type:"filled"`. Ambiguous structure is reported in `diagnostics`.
Rule details live in [`UGUI_VERSION_RULES.md`](UGUI_VERSION_RULES.md).

**Game Feel UI Mode (Beta)** — when enabled, `apply` adds an `agent_hint` with the juice recipes for each *distinct* element type in the doc (deduped once, not per element — strong signature, lean tokens), plus one combined pointer into the `game_feel` knowledge base's `ui` category for deep specs. Guidance only; no runtime components are attached.

### Icons (no SVG needed)

`gen_sprite` covers CSS-shape backgrounds (solid / rounded_rect / gradient / nine_slice); it deliberately does **not** rasterize SVG (that needs the `com.unity.vectorgraphics` package — a runtime dependency hera avoids). For icons, use one of two zero-dependency, in-model patterns instead:

1. **Existing icon sprite** — reference it straight from the IR. One step, no extra calls:
   ```jsonc
   { "name": "PlayIcon", "element": "image",
     "rect": { "anchor": "middle-center", "size": [48, 48] },
     "image": { "sprite": { "asset": "Assets/Icons/play.png" } } }
   ```

2. **Icon font glyph** (recommended — scalable, tintable, themeable). Create a `text` element whose `value` is the glyph character for the icon (e.g. Material Icons `U+E037` = play), then assign the icon TMP font asset via `manage_components` (font assignment is a component property edit, so it stays out of `ui_doc`):
   ```bash
   # 1. ui_doc apply creates the text element with the glyph as its value (engine tmp)
   # 2. point it at the icon font:
   hera-agent-unity manage_components set --path /Canvas/PlayIcon \
     --type TextMeshProUGUI --property m_fontAsset --value "Assets/Fonts/MaterialIcons SDF.asset"
   ```

Prefer (1) when the project ships icon sprites, (2) when it uses an icon font (one font asset serves every glyph, and the icon inherits text color/size). Reserve actual bitmap art for externally-authored assets referenced via `sprite.asset`.

---

## html-to-uidoc

Convert an inline-style HTML mockup to `ui_doc/2` JSON. This is a **CLI-side**
command — no Unity round-trip — so agents can turn a pixel-perfect HTML design
into the uGUI IR inside a single shell pipeline.

```bash
hera-agent-unity html-to-uidoc --file <html> [--out <json>] [--width <N>] [--height <N>]
```

| Flag | Default | Meaning |
|---|---|---|
| `--file` | required | Input HTML file |
| `--out` | stdout | Output JSON file |
| `--width` | 1080 | HTML design canvas width in pixels |
| `--height` | 1920 | HTML design canvas height in pixels |

The generated IR sets `canvas.reference_resolution` to `[width, height]` and
uses `Scale With Screen Size`, so **1 HTML pixel maps to 1 uGUI canvas unit**.
HTML `top` (downward positive) is converted to uGUI `anchoredPosition.y`
(upward positive) automatically.

Supported HTML:
- Inline `style` attributes only (CSS classes / `<style>` blocks are not parsed).
- `position:absolute; left:<px>; top:<px>; width:<px>; height:<px>`.
- `background-color:<hex|rgb|name>`.
- `border-radius:<px>` → `rounded_rect` procedural sprite.
- Tags: `<div>` (panel), `<button>` (button), `<img>` (image), `<span>` (text).

```bash
hera-agent-unity html-to-uidoc --file design.html --out ui_doc.json --width 1920 --height 1080
hera-agent-unity html-to-uidoc --file design.html | hera-agent-unity ui_doc apply
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

List registered tools. Three detail levels, cheapest first — the per-tool
parameter schema is the bulk of the bytes, so it's opt-in rather than dumped
up front:

| Form | Returns | Use when |
|:---|:---|:---|
| `list --names` | flat array of tool names only | cheapest discovery |
| `list --compact` | same as `list --names` | compact catalogue discovery from agents or scripts (the AGENTS.md bootstrap runs this) |
| `list` | `{name, description}` per tool, no schema | you want a one-line hint per tool |
| `list --tool <name>` | full parameter + output schema + metadata for one tool | you're about to call that tool |

```bash
hera-agent-unity list --compact
hera-agent-unity list --names
hera-agent-unity list
hera-agent-unity list --tool exec
```

Useful for discovering custom tools added to the project.

`metadata.safety` describes the whole tool. Multi-action tools may also expose
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
| `gamefeel [on\|off]` | Show or set Game Feel Mode (Beta) (gameplay game-feel guidance via `game_feel` + agent rules) |
| `gamefeel-ui [on\|off]` | Show or set Game Feel UI Mode (Beta) (drives `manage_ui` juice guidance); `juicy` is a legacy alias |
| `detect` | Auto-detect installed assets (requires Unity) |
| `get <id>` | Show a single asset's state |
| `path` | Print the config file path |

| Flag | Description | Default |
|:---|:---|:---|
| `--json` | Output enabled assets + `loop_engineering_mode` + `game_feel_mode` + `game_feel_ui_mode` + `dotween_preferred` as JSON | `false` |

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
