---
name: hera-agent-unity
description: Control the running Unity Editor via the hera-agent-unity CLI — execute C#, read the console, drive Play Mode, run tests, inspect live types
---

# hera-agent-unity skill

Drive a **running Unity Editor** from the terminal over localhost HTTP. Use this instead of guessing Unity APIs from training data — measure against the real Editor.

## When to use

- Manipulating the Editor: scenes, GameObjects, components, prefabs, materials, UI (`scene`, `manage_gameobject`, `manage_components`, `manage_prefab`, `manage_material`, `manage_ui`).
- Building UI from an HTML design: `ui_doc export` (live UI → compact JSON to ground on), `ui_doc apply --file <doc.json>` (JSON → UI; `--mode create` default or `upsert` to edit existing in place), `ui_doc gen_sprite` (procedural sprite — no external dep), `ui_doc capture` (render the live UI → PNG to verify against a reference). Design in HTML, export to ground, apply the `ui_doc/2` IR.
- Reading the **real** console: errors, warnings, stack traces (`console`).
- Running EditMode / PlayMode tests (`test`).
- Driving Play Mode (`editor play` / `stop`).
- Executing arbitrary C# inside the Editor when no dedicated command fits (`exec`).
- Inspecting live types / methods (`describe_type`, `find_method`, `list --compact`).

## Pre-flight

Before any real work, confirm the Editor is reachable:

```bash
hera-agent-unity status                 # port, project, unity version, state
hera-agent-unity doctor --json          # binary on PATH? Unity reachable?
hera-agent-unity list --compact         # cheapest tool discovery
```

If `status` returns no instances, tell the user to open Unity with the UPM connector package and stop — don't retry blindly.

## Command rules

- **Call sequentially, never in parallel.** The connector serializes every command on the Unity main thread; concurrent calls just queue.
- **Pass `--compact-json`** on every tool call — AntiGravity consumes the JSON, so keep it small.
- **Use compact discovery.** Prefer `list --compact`; use `list --tool <name>` only when one full schema is required.
- **Use IDs for object handoff.** Prefer `find_gameobjects --ids`; add `--fields instance_id,name,path` only when duplicate names need context.
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

# 7. Handoff object IDs cheaply
hera-agent-unity find_gameobjects --ids --compact-json

# 8. Drive Play Mode and read what broke
hera-agent-unity editor play --wait && hera-agent-unity console --type error --compact-json
```

## When this skill is wrong

If anything here contradicts `hera-agent-unity <cmd> --help`, trust `--help`. The full guide (Tool Selection, Cookbook, Pitfalls, Reference) is at <https://github.com/NotNull92/hera-agent-unity/blob/main/AGENTS.md>.
