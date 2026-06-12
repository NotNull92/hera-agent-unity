---
name: hera-agent-unity
description: Control the running Unity Editor via the hera-agent-unity CLI — execute C#, read the console, drive Play Mode, run tests, inspect live types
---

# hera-agent-unity skill

Drive a **running Unity Editor** from the terminal over localhost HTTP. Use this instead of guessing Unity APIs from training data — measure against the real Editor.

## When to use

- Manipulating the Editor: scenes, GameObjects, components, prefabs, materials, UI (`scene`, `manage_gameobject`, `manage_components`, `manage_prefab`, `manage_material`, `manage_ui`).
- Reading the **real** console: errors, warnings, stack traces (`console`).
- Running EditMode / PlayMode tests (`test`).
- Driving Play Mode (`editor play` / `stop`).
- Executing arbitrary C# inside the Editor when no dedicated command fits (`exec`).
- Inspecting live types / methods (`describe_type`, `find_method`, `list`).

## Pre-flight

Before any real work, confirm the Editor is reachable:

```bash
hera-agent-unity status                 # port, project, unity version, state
hera-agent-unity doctor --json          # binary on PATH? Unity reachable?
```

If `status` returns no instances, tell the user to open Unity with the UPM connector package and stop — don't retry blindly.

## Command rules

- **Call sequentially, never in parallel.** The connector serializes every command on the Unity main thread; concurrent calls just queue.
- **Pass `--compact-json`** on every tool call — AntiGravity consumes the JSON, so keep it small.
- **`exec`: default to `return null;`** (or omit the return). A verbose status string is wasted tokens; the OK response is 3 bytes.
- **`exec`: never return a `UnityEngine.Object`** (`Transform`, `GameObject`, `Material`, …). They expand to thousands of bytes. Return `new { name = go.name, instanceID = go.GetInstanceID() }`.
- **Branch on the `code` field**, not the message: `EXEC_COMPILE_ERROR`, `EXEC_RUNTIME_ERROR`, `UNKNOWN_COMMAND` (has `data.did_you_mean`), `EXEC_CSC_NOT_FOUND`, etc.
- **Domain reloads** (recompiles/imports) drop the HTTP connection — the CLI auto-retries ~5s. For big projects raise `--timeout 120000`. Use `editor refresh --compile` to force compile before continuing.
- **Prefer dedicated commands over `exec`** — `scene info`, `console`, `describe_type` skip csc compile (5–15s cold).

## PowerShell / multi-line C#

PowerShell quoting mangles C#. Always pipe a here-string to stdin:

```powershell
@'
var mats = AssetDatabase.FindAssets("t:Material").Length;
return mats;
'@ | hera-agent-unity exec --compact-json
```

bash equivalent: a `<<'EOF'` heredoc, or `--file scripts/probe.cs` for anything long or reusable.

## Examples

```bash
# 1. Active scene state in one call (don't exec this — it's dedicated)
hera-agent-unity scene info --compact-json

# 2. Probe a value
hera-agent-unity exec "return EditorSceneManager.GetActiveScene().name;" --compact-json

# 3. Read only the most recent errors
hera-agent-unity console --type error --lines 5 --compact-json

# 4. Bulk create in one round-trip (return null;)
echo 'var root = new GameObject("MyRoot");
for (int i = 0; i < 10; i++) new GameObject("Item_" + i).transform.SetParent(root.transform, false);
return null;' | hera-agent-unity exec --compact-json

# 5. Run a focused test suite
hera-agent-unity test --mode PlayMode --filter MyGame.Tests --compact-json

# 6. Inspect a type instead of guessing its API
hera-agent-unity describe_type UnityEditor.AssetDatabase --members methods --limit 30 --compact-json

# 7. Drive Play Mode and read what broke
hera-agent-unity editor play --wait && hera-agent-unity console --type error --compact-json
```

## When this skill is wrong

If anything here contradicts `hera-agent-unity <cmd> --help`, trust `--help`. The full guide (Tool Selection, Cookbook, Pitfalls, Reference) is at <https://github.com/NotNull92/hera-agent-unity/blob/main/AGENTS.md>.
