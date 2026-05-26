# Command Reference

Complete reference of all `hera-agent` commands, flags, and parameters.

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
hera-agent editor <action> [flags]
```

### play

Enter play mode.

| Flag | Description | Default |
|:---|:---|:---|
| `--wait` | Block until fully entered play mode | `false` |

```bash
hera-agent editor play --wait
```

### stop

Exit play mode.

| Flag | Description | Default |
|:---|:---|:---|
| `--wait` | Block until fully exited play mode | `false` |

```bash
hera-agent editor stop
```

### pause

Toggle pause/resume (play mode only).

```bash
hera-agent editor pause
```

### refresh

Refresh the AssetDatabase.

| Flag | Description | Default |
|:---|:---|:---|
| `--force` | Allow refresh during play mode | `false` |
| `--compile` | Recompile scripts and wait until done | `false` |

```bash
hera-agent editor refresh --force
hera-agent editor refresh --compile
```

**Note**: `refresh` is blocked in play mode unless `--force` is set.

---

## exec

Execute arbitrary C# code inside Unity Editor.

```bash
hera-agent exec "<code>" [flags]
echo '<code>' | hera-agent exec [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--usings` | Add extra using directives (comma-separated) | `""` |
| `--csc` | Path to csc compiler | Auto-detected |
| `--dotnet` | Path to dotnet runtime | Auto-detected |
| `--no-cache` | Skip compile/assembly cache (debug only) | `false` |

```bash
# Basic execution
hera-agent exec "return 1+1;"

# Unity API access
hera-agent exec "return Application.dataPath;"

# Pipe to avoid shell escaping
echo 'return EditorSceneManager.GetActiveScene().name;' | hera-agent exec

# Custom usings for ECS
hera-agent exec "return World.All.Count;" --usings Unity.Entities
```

**Default usings**: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Reflection`, `System.Threading.Tasks`, `UnityEngine`, `UnityEngine.SceneManagement`, `UnityEditor`, `UnityEditor.SceneManagement`, `UnityEditorInternal`

**Note**: Use `return` for output. Use `return null;` for void operations.

**Caching**: Compiled assemblies are cached in `Library/HeraAgentCache/` and held in memory keyed by source hash. The first call per Unity session is the cold path (csc invocation); identical follow-up calls skip both compile and load. Cache invalidates automatically on assembly reload. Use `--no-cache` to bypass.

---

## console

Read, filter, and clear Unity console logs.

```bash
hera-agent console [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--lines` | Limit to N entries | All |
| `--type` | Comma-separated: `error`, `warning`, `log` | `error,warning,log` |
| `--stacktrace` | `none`, `user`, `full` | `user` |
| `--clear` | Clear console after reading | `false` |

```bash
hera-agent console
hera-agent console --lines 20 --type error
hera-agent console --stacktrace full
hera-agent console --clear
```

---

## scene

Inspect and manage Unity scenes.

```bash
hera-agent scene <action> [target] [flags]
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
hera-agent scene info
hera-agent scene load Assets/Scenes/Main.unity
hera-agent scene load Main --mode additive
hera-agent scene save
hera-agent scene close Lobby
hera-agent scene list
```

**Notes**:
- `load --mode single` refuses to run if the active scene is dirty — save it first or load additively.
- `close` refuses if the target scene is dirty.
- Name resolution uses `AssetDatabase.FindAssets` with an exact filename match (case-insensitive).

---

## menu

Execute a Unity menu item by path.

```bash
hera-agent menu "<path>"
```

```bash
hera-agent menu "File/Save Project"
hera-agent menu "Assets/Refresh"
hera-agent menu "Window/General/Console"
```

**Note**: `File/Quit` is blocked for safety.

---

## screenshot

Capture a screenshot of the Unity editor.

```bash
hera-agent screenshot [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--view` | `scene` or `game` | `scene` |
| `--width` | Image width in pixels | `1920` |
| `--height` | Image height in pixels | `1080` |
| `--output_path` | Output path (absolute or relative to project) | `Screenshots/screenshot.png` |

```bash
hera-agent screenshot
hera-agent screenshot --view game
hera-agent screenshot --width 3840 --height 2160
hera-agent screenshot --output_path captures/my_scene.png
```

---

## reserialize

Force reserialize assets (rewrite YAML/JSON with current Unity version).

```bash
hera-agent reserialize [path...]
```

```bash
# Reserialize entire project
hera-agent reserialize

# Reserialize specific assets
hera-agent reserialize Assets/Scenes/Main.unity
hera-agent reserialize Assets/Prefabs/A.prefab Assets/Prefabs/B.prefab
```

---

## test

Run Unity Test Framework tests.

```bash
hera-agent test [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--mode` | `EditMode` or `PlayMode` | `EditMode` |
| `--filter` | Filter by namespace, class, or full test name | `""` |
| `--wait` | Wait for PlayMode tests to complete | `false` (EditMode: always waits) |

```bash
# EditMode tests (synchronous)
hera-agent test

# PlayMode tests (asynchronous, requires --wait)
hera-agent test --mode PlayMode --wait

# Filtered tests
hera-agent test --filter MyNamespace.MyClass
```

**PlayMode behavior**: Returns `"running"` immediately. Results are written to `~/.hera-agent/status/test-results-<port>.json`. The CLI polls this file when `--wait` is set.

---

## profiler

Control the Unity Profiler.

```bash
hera-agent profiler <action> [flags]
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
hera-agent profiler hierarchy
hera-agent profiler hierarchy --depth 5 --frames 30
hera-agent profiler enable
```

---

## list

List all registered tools with their parameter schemas.

```bash
hera-agent list
```

Useful for discovering custom tools added to the project.

---

## status

Show current Unity Editor state.

```bash
hera-agent status
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
hera-agent update [flags]
```

| Flag | Description | Default |
|:---|:---|:---|
| `--check` | Check for updates without installing | `false` |

```bash
hera-agent update --check
hera-agent update
```

---

## version

Show CLI version.

```bash
hera-agent version
```

---

## asset-config

Manage asset configuration (interactive TUI or command-based).

```bash
hera-agent asset-config <subcommand>
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
hera-agent my_custom_tool

# Call with parameters
hera-agent my_custom_tool --params '{"key":"value"}'
```

Use `hera-agent list` to discover available tools.

---

## Related Documentation

- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) — Writing custom tools
