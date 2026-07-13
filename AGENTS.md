# hera-agent-unity — Guide for AI Agents

> You are an AI coding agent operating in a Unity project that has `hera-agent-unity` available as a CLI. This document tells you how to use it efficiently. It is meant to be loaded into your project's rules file so every session has it without spending tokens to discover it.
>
> **Where to put this content.** `AGENTS.md` at the project root is the canonical cross-tool agent rules file, standardized by the Agentic AI Foundation (AAIF) under the Linux Foundation (Dec 2025) and adopted by 60,000+ open-source repositories. OpenAI Codex, Claude Code, Cursor, GitHub Copilot, Gemini CLI, and 30+ tools read this file by default.
>
> For multi-tool projects, the cleanest pattern is **`AGENTS.md` as the single source of truth** plus a one-line stub in tool-specific paths. Tool-specific files only matter when a tool requires a different format (Cursor's `.mdc` YAML frontmatter is the main case).
>
> **Recommended layout — multi-tool projects**:
>
> 1. Drop the full guide (or its lean subset) into `AGENTS.md` at the project root.
> 2. For tools that need their own format, drop a short stub that defers to `AGENTS.md`:
>     - `CLAUDE.md` → `> See AGENTS.md.` (Claude Code reads `AGENTS.md` natively since late 2025)
>     - `.cursor/rules/hera-agent-unity.mdc` → frontmatter + the same body (Cursor also supports plain `AGENTS.md` as a fallback)
>     - `.github/copilot-instructions.md` → repository-wide pointer to `AGENTS.md` (Copilot uses nearest-file precedence; `.github/skills/` is for Agent Skills)
>     - `GEMINI.md` + `.agents/agents.md` + `.agents/skills/hera-agent-unity/SKILL.md` → AntiGravity project entry + workspace handoff + on-demand skill
>     - `.continuerules` → identical body
>
> **Per-tool target paths (2026-current)**:
>
> | Tool | Canonical path | Notes |
> |---|---|---|
> | OpenAI Codex / AGENTS.md-aware tools | `AGENTS.md` | Cross-tool standard. Supports layering: `~/.codex/AGENTS.md` → repo root → subtree → `AGENTS.override.md`. |
> | Claude Code | `AGENTS.md` (or `CLAUDE.md`) | Reads `AGENTS.md` natively. `CLAUDE.md` still works for path-scoped rules and imports. |
> | Cursor | `.cursor/rules/*.mdc` | YAML frontmatter required for activation. `.cursorrules` (single-file) is **deprecated** and ignored by Agent mode. |
> | GitHub Copilot | `.github/copilot-instructions.md` | Nearest-file precedence. Optional: `.github/instructions/*.instructions.md` with `applyTo` frontmatter; `.github/skills/` for Agent Skills. |
> | Google AntiGravity | `GEMINI.md`, `.agents/agents.md`, `.agents/skills/*/SKILL.md` | `.agents/` is the native workspace extension directory for agents, workflows, and skills. |
> | Continue.dev | `.continuerules` | Plain markdown. |
> | Other | Tool-specific rules file | Most accept plain markdown. |
>
> **Two ways to populate the target file**:
>
> 1. **Static** — copy the matching stub from [`examples/rules/`](examples/rules/) (one file per tool, already formatted).
> 2. **Dynamic** — let the CLI generate it from this guide:
>     ```bash
>     # AGENTS.md / CLAUDE.md / Copilot / Continue.dev — plain markdown
>     hera-agent-unity doctor --agent-rules >> AGENTS.md
>
>     # Cursor — frontmatter prepended automatically
>     hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
>
>     # AntiGravity — project rule + on-demand skill
>     hera-agent-unity doctor --agent-rules >> GEMINI.md
>     hera-agent-unity doctor --agent-rules --format antigravity > .agents/skills/hera-agent-unity/SKILL.md
>     ```

`hera-agent-unity` is a CLI that drives a running Unity Editor over HTTP. Common uses: execute C# inside the Editor, read console logs, query the active scene, run tests, capture screenshots, batch several commands in one round-trip. Each call is a tool round-trip; response bytes become your input tokens, so reads cost as much as your own writes.

Quick links:
- Full command catalog → [`docs/COMMANDS.md`](docs/COMMANDS.md)
- Architecture → [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Custom tool authoring → `hera-agent-unity help custom-tools`
- Setup / install → [README](README.md)

---

## 0. Bootstrap on Discovery

When the user invites you to engage hera-agent-unity — in any language, any phrasing — do **not** ask follow-up questions and do **not** stop at "yes, it looks installed". Run the discovery sequence and report.

**Trigger phrases** (treat as equivalent):

- `find hera-agent-unity` · `hera-agent-unity 찾아봐`
- `is hera-agent-unity installed?` · `설치돼 있어?`
- `check the editor connection` · `에디터 붙어있어?`
- `connect to unity` · `unity에 연결해줘`
- Anything mentioning **hera-agent-unity** or **hera-agent** in a check / discovery / "are you set up?" context.

**Sequence — run all three, in order, without prompting:**

1. `hera-agent-unity doctor --json` — verifies the binary is on PATH, no duplicate installs, and the connector can see at least one Unity instance. JSON envelope is parseable.
2. `hera-agent-unity status` — confirms the active editor's port, project path, Unity version, PID, and current state (`ready`, `compiling`, …).
3. `hera-agent-unity list --compact` — discovers what tools (built-in + custom `[HeraTool]` classes) this project exposes with the smallest practical payload, so subsequent prompts can be answered without re-scanning.

**Report shape** (one line first, then optional details):

```
Connected: <project name> · port=<N> · unity=<version> · state=<ready|compiling> · tools=<count>
```

If a step fails, do not silently skip — surface the failure verbatim:

- `doctor` says binary missing → tell the user to install it (`curl … | sh` or the README link), do not proceed.
- `doctor` says Unity is unreachable → tell the user to open the Editor with the UPM package, do not proceed.
- `status` returns no instances → same.

After a successful bootstrap, you may proceed to whatever the user actually wanted. The bootstrap output replaces "let me check if it's installed" — that line costs tokens and tells the user nothing they can act on.

---

## 1. Quick Rules (must-follow)

Numbered so you can grep "[Rule N]" when in doubt.

**[Rule 1]** Default to no return (or `return null;`) in `exec`. Side-effecting code (create objects, set properties, save scenes) should not return a verbose status string. The OK response is 3 bytes (`OK\n`); a hand-crafted summary string ships hundreds of bytes back into your context. The trailing `return` is **optional** — snippets without one resolve to `null` automatically.

```cs
// Bad — your status string costs ~200 tokens
return $"Created Canvas with {n} buttons under {parent.name}";

// Good — same work, 3 bytes
return null;

// Also good — omit the return entirely
new GameObject("X");
```

> Caveat: `return;` (no value) still does NOT compile because `Execute()` returns `object`. Write `return null;` for early exits, or `throw new Exception("...")` for hard failures (see Rule 8).

> The CLI emits compact JSON automatically for non-human commands (anything outside `install/uninstall/status/update/doctor/help/version`). Pass `--compact-json` or set `HERA_AGENT_COMPACT_JSON=1` to force compact on a TTY too.

**[Rule 2]** Never return a `UnityEngine.Object` directly. `Transform`, `GameObject`, `Component`, `Scene`, `Material`, etc. expand to thousands of bytes of reflected properties.

```cs
// Bad — Transform serializes to 9KB+ (position, rotation, matrices, ...)
return GameObject.Find("Canvas").transform;

// Good — name + id is what you actually wanted
var go = GameObject.Find("Canvas");
return new { name = go.name, instanceID = go.GetInstanceID() };
```

Default `--depth` is `3`, which fully reflects Unity Objects in the response. Pass `--depth 1` (or `2`) when you want the leanest payload — depths 1–2 collapse Unity Objects to the shallow form `{name, type, instanceID}`. Set `--depth 3` only when you have a specific reason to inspect the property tree.

**[Rule 3]** Branch on the `code` field of error responses, not on the message text. Messages get tweaked across versions; `code` is the stable enum-like contract.

```jsonc
// Error envelope shape
{
  "success": false,
  "code": "EXEC_COMPILE_ERROR",   // <-- branch on this
  "message": "Your C# snippet did not compile. L1 CS1525: ... (+2 more)",
  "data": { "compile_errors": [ ... ] }
}
```

**[Rule 4]** Batch related operations into a single `exec` call. Each call has fixed envelope + HTTP overhead; one `exec` that creates Canvas + 3 buttons is cheaper than three separate calls. For coarser composition (`editor refresh` + `console`, etc.) use the `batch` command.

**[Rule 5]** For console reads, the default `--lines` is `20` (sane cap). Use `--lines 0` only when you actually need every entry. Use `--type error` when you don't care about warnings/logs.

**[Rule 6]** Runtime errors from `exec` return user-filtered stack traces by default. If you suspect the framework itself is the cause (e.g. Unity internal exception), pass `--stacktrace full` to see all frames.

**[Rule 7]** Use the right tool for the job — see §2. `exec` is the universal hammer but it costs csc compile time and is harder to inspect. Dedicated commands (`scene info`, `console`, `status`, `describe_type`, `find_method`) are faster and cheaper.

**[Rule 8]** When you want a logical failure to be visible at the CLI exit-code layer, either **throw** (`throw new System.Exception("missing")` → `EXEC_RUNTIME_ERROR`, exit non-zero) or run with `--strict` so that any `Debug.LogError/LogException/LogAssert` raised during the snippet flips the response to `EXEC_LOGGED_ERROR`. Without one of these, `Debug.LogError("..."); return null;` is indistinguishable from a clean run by exit code — only the Unity console sees it.

```cs
// Off (default) — agent sees success even though "missing" was logged
// hera-agent-unity exec "Debug.LogError(\"missing\"); return null;"
//   → exit 0, success

// On — same code surfaces as EXEC_LOGGED_ERROR
// hera-agent-unity exec "Debug.LogError(\"missing\"); return null;" --strict
//   → exit 1, code=EXEC_LOGGED_ERROR
```

**[Rule 9]** Do not put your current machine's absolute paths in shared docs,
rules, examples, generated Markdown, or checked-in scripts. Prefer
repo-relative paths, documented environment variables, or explicit CLI flags.
For Unity Hub editor inventory, write paths with `%UNITY_HUB_EDITOR%` and state
that the default Windows resolver is `%ProgramFiles%\Unity\Hub\Editor`; let
users override the real root with `-HubRoot` or their own environment.

**[Rule 10]** Keep CLI and UPM connector versions separate. `hera-agent-unity
version` reports the Go CLI release tag (`vX.Y.Z`). Unity Package Manager reports
the connector package version from `AgentConnector/package.json` (for example
`0.0.N`). Do not call the UPM package `vX.Y.Z`, do not assume the two numbers
match, and do not use a git lock hash as the package version. A lock hash only
identifies the connector source commit that Unity resolved.

**[Rule 11]** Separate Unity EventSystem input QA from physical OS click QA.
Use `input state` / `input inspect` / `input click` / `input submit` / `input
scroll` / `input drag` to verify uGUI behavior when Computer Use cannot obtain a
Unity screenshot state. These commands synthesize Unity EventSystem events; they
do **not** prove that a physical OS/window click worked. If Computer Use still
cannot capture Unity screenshot state and no native OS/window input backend is
available, record the physical-click criterion as **BLOCKED** even when
EventSystem input QA passes.

## 1.5 Ultra Hera

Ultra Hera helps AI check its Unity work. Hera does not do the AI work by itself. This setting tells AI agents how carefully they should check Unity work after using Hera.

Modes are saved in `asset-config.json` as `loopEngineeringMode`:

- `off`: AI does not have to check again after using Hera.
- `light` (default): use the Light loop for every Unity coding, Editor, and Inspector task.
- `ultra`: use the Light loop for every task, then upgrade to the Ultra loop for strict requests or important Unity work.

Light loop:

1. Confirm the goal in one sentence.
2. Observe only the needed current state in a compact way.
3. Change code, scene, or Inspector state.
4. Verify compile or state.
5. Check console errors.
6. Re-read only the changed target.
7. If it failed, fix and repeat up to 1-2 times.
8. Report short final evidence.

Representative Light commands:

```bash
hera-agent-unity status
hera-agent-unity console --type error --lines 20
hera-agent-unity editor refresh --compile
hera-agent-unity find_gameobjects --ids
hera-agent-unity manage_components get ...
hera-agent-unity exec --depth 1 ...
```

Light Mode's goal is: do not finish in a wrong state. PlayMode, screenshots, and full tests are not required by default.

Ultra loop:

1. Split the goal into success criteria.
2. Take a before-change state snapshot.
3. Apply the change.
4. Compile.
5. Confirm console errors are 0.
6. Re-read Inspector, GameObject, or asset state.
7. Run PlayMode or Unity tests.
8. If needed, capture a screenshot or `ui_doc` capture.
9. Classify the failure cause and repeat.
10. Report final evidence and remaining risk.

Representative Ultra commands:

```bash
hera-agent-unity editor refresh --compile
hera-agent-unity console --type error --lines 50
hera-agent-unity test --mode EditMode
hera-agent-unity test --mode PlayMode
hera-agent-unity editor play --wait
hera-agent-unity screenshot --view game
hera-agent-unity ui_doc capture --out ...
```

Use Ultra when the user asks for strict verification, for example `정확히 검증해줘`, `플레이해서 확인해줘`, `UI 맞춰줘`, or `인스펙터까지 확실히 봐줘`.

---

## 2. Tool Selection Cheatsheet

When you can do something with a dedicated command, use it instead of `exec`. Dedicated commands skip csc compilation (5–15s cold, ~500ms warm).

| You want to … | Use | Notes |
|---|---|---|
| Active scene name / path / dirty | `scene info` | Returns active + loaded scenes in one shot. |
| Open / save / close a scene | `scene load <path>` / `scene save` / `scene close` | Modes: single (default), additive. |
| Read recent console errors | `console --type error` | Default 20 entries; pagination via `--lines` + `--since`. |
| Clear console | `console --clear` | Idempotent. |
| Check if Editor is in play mode | `status` | Returns state field (ready/compiling/playing/paused). |
| Enter / exit play mode | `editor play [--wait]` / `editor stop` | `--wait` blocks until fully entered. |
| Force recompile | `editor refresh --compile` | Waits until compile finishes or `--timeout` (60s default) elapses — raise `--timeout` for big projects, or use `refresh_unity --compile request` to fire-and-forget. |
| Trigger a menu item | `menu "Window/General/Console"` | `File/Quit` is blocked for safety. |
| Capture screenshot | `screenshot [--view game]` / `screenshot --isolated --target /Player` | Default scene view, 1920×1080; isolated mode renders one GameObject. |
| Drive Unity UI input for QA | `input state` / `input inspect --path ...` / `input click --path ...` | Sends uGUI EventSystem events inside Unity. This is not a physical OS click; use it when Computer Use coordinates are blocked but UI logic still needs Play Mode QA. |
| Run EditMode / PlayMode tests | `test [--mode PlayMode] [--filter ...]` | Filter by namespace, class, or full test name. |
| Profiler hierarchy snapshot | `profiler hierarchy --depth N` | Sort by self/total/calls, filter by `--min ms`. |
| Liveness probe (no Unity round-trip) | `ping` | Cheaper than `status` — heartbeat file only. |
| List all tools | `list --compact` or `list --names` | 30s in-memory + on-disk cache. Both forms return a flat names array; use `list` only when you need one-line descriptions. |
| Run multiple commands in one HTTP round-trip | `batch --file <path.json>` or pipe JSON | Sequential. `fail_fast` on first error by default. |
| Compile-check without executing | `exec --check "<code>"` | Returns success on clean compile, `EXEC_COMPILE_ERROR` otherwise. No side effects. |
| List loaded assemblies | `list_assemblies [--filter <substr>] [--include_system] [--include_version]` | Returns bare name strings by default; `--filter` to scope, `--include_version` for `{name, version}` objects. |
| Inspect a type's signature + known Unity pitfalls | `describe_type <name> [--members methods] [--limit N]` | Cheaper than `exec` reflection. |
| Search methods across assemblies by name | `find_method <pattern> [--namespace ns] [--limit N]` | Pattern is a substring; `--limit` defaults to 50. |
| Find / create / move project assets | `manage_assets find --type Texture2D --filter icon` / `manage_assets create --type GameConfig --path Assets/Config/Game.asset` | Compact `AssetDatabase` operations constrained to `Assets/`; `create` authors a ScriptableObject `.asset` (optional `--params '{"properties":{...}}'`). Use before falling back to `exec` for basic asset work. |
| Ground an HTML→uGUI design on the real UI | `ui_doc export --path </path>` | Returns the compact uGUI `ui_doc/2` IR (defaults omitted). UI Toolkit v1 emits from an input document instead. |
| Build a UI from a JSON design | `ui_doc apply --file design.json [--parent ...] [--mode upsert]` | `ui_system=ugui` builds the existing GameObject tree; `ui_system=uitk` requires `backend:"uitk"` and emits validated UXML/USS/PanelSettings/UIDocument scaffolding. |
| Bake a procedural sprite | `ui_doc gen_sprite --spec '{...}' --out Assets/...` | Tier-1: `solid` / `rounded_rect` / `gradient` / `nine_slice` (border for 9-slice). No external dependency. |
| Measure colors off a reference image | `ui_doc sample --image ref.png --at "x,y" [--region "x,y,w,h"]` | Normalized [0,1] top-left coords (`;`-separate many). Returns measured `hex`/`rgba`. Measure colors — don't eyeball them. CLI-side, no Unity needed. |
| See what you built (verify) | `ui_doc capture --out /tmp/built.png` | Renders live uGUI overlays to PNG (a normal `screenshot` misses overlay canvases). UITK v1 verifies generated assets/UIDocument through state reads or game screenshots. |
| Anything else (read prop, custom C#) | `exec "<code>"` | Falls back here when no dedicated command exists. |

**Compile-check only** (validate syntax/types without executing):
```bash
hera-agent-unity exec "var x = SomeType.SomeMethod();" --check
```
Useful when you're not sure a refactor compiles before issuing a destructive call.

**Input QA (`input`)** — use this when you need to verify Unity UI behavior and Computer Use cannot obtain a Unity screenshot state. `input` resolves targets by hierarchy path, instance ID, or normalized screen/canvas position, then dispatches uGUI EventSystem events (`click`, `pointer_down`, `pointer_up`, `submit`, `scroll`, `drag`). Start with `input state` to confirm an EventSystem + raycasters exist, then `input inspect --path /Canvas/Button --details true` before sending events. Evidence should be classified precisely:

- **Unity EventSystem input QA:** PASS/FAIL based on `input` results, console logs, state reads, Play Mode tests, or UI callbacks.
- **Physical OS click QA:** BLOCKED if Computer Use still cannot capture Unity screenshot state and Hera has no native OS/window input backend for that action.

**ui_doc IR (`ui_doc/2`)** — `ui_system` chooses one fully separate backend per build. The default `ugui` contract below uses live export plus RectTransform anchors. `uitk` requires `asset-config ui-system uitk` and a `backend:"uitk"` document with exact runtime element names, reflected UXML attributes, and reflected USS properties. Full reference: `docs/UI_DOC_IR.md`:

```jsonc
{ "schema": "ui_doc/2", "backend": "ugui",
  "root": { "name": "Panel", "element": "panel",   // canvas|panel|image|button|text|empty
    "rect": { "anchor": "stretch", "size": [400, 600] },
    "image": { "color": "#1A1A2EFF", "sprite": { "gen": { "kind": "rounded_rect", "radius": 12 } } },
    "children": [ { "name": "PlayBtn", "element": "button",
      "rect": { "anchor": "top-center", "pos": [0, -40], "size": [240, 64] },
      "text": { "value": "Play", "engine": "auto" } } ] } }
```

`image.sprite` = `{ "asset": "Assets/..." }` or `{ "gen": {<spec>} }` (baked on apply; `nine_slice` auto-sets Image type Sliced). `text` takes `value` + optional `engine` (auto/tmp/legacy), `color` (#hex), `align` (center/left/right/top-left), `font` (asset path to a TMP/legacy font — also the icon-font-glyph path). With Game Feel UI Mode (Beta) on, `apply` returns per-element-type juice recipes as an `agent_hint`.

**UI Toolkit v1** — `ui_doc apply` emits `.uxml` + shared `.hera-*` `.uss` + a screen-space `PanelSettings` and wired `UIDocument` into `Assets/HeraGenerated/UI`. It rejects unknown runtime elements/UXML attributes, warns and omits unsupported USS properties, and keeps MVVM data binding out of scope. World-space is allowed only when the **live runtime** is Unity `6000.2+`; never infer that from the docs bucket. `manage_ui create` follows the same backend choice; `get_rect`, `set_anchor`, and `set_rect` are uGUI-only.

**Icons** (no SVG gen): reference an existing sprite via `image.sprite.asset`, or use an icon-font glyph — a `text` element whose `value` is the glyph char, then assign the icon TMP font with `manage_components set --property m_fontAsset --value <font.asset>`. See COMMANDS.md → ui_doc → Icons.

**Reproducing a reference image faithfully** — rules that matter for a close match:
- **Run the verify loop.** `ui_doc sample` the reference for exact colors → author the IR → `apply` → `ui_doc capture` → Read the PNG and compare it to the reference → fix the largest discrepancy → repeat until it stops improving. The tools exist so you measure and correct instead of eyeballing and rationalizing.
- **Measure, don't guess.** Derive each element's position/size/color from the reference (the canvas is a known px space, e.g. 1080×1920). Use `ui_doc sample` for colors rather than guessing hex. Never eyeball a position and then rationalize it. If you can't place a detail accurately, omit it — a wrong/misplaced element is worse than a missing one.
- **Progress bars / fills:** anchor the fill to the track's *start edge* (not centered) and size it = `fraction × track length`, kept inside the track. A center-anchored fill overflows the track.
- **Text inside a container** (button / chip / pill): give the text the *same rect* as the container + `align: center`. A smaller or offset text rect clips (e.g. "x1") or de-centers.
- **Sub-icon rows** (runes / gems / stars under a slider): place them under their owning element as an evenly-spaced row, and match the count from the reference.
- **Bespoke art** (coins, weapons, trophies, stat icons) can't be procedurally generated — use real sprite assets or an icon font; only fall back to a clearly-stylized placeholder, and state that it is one. Don't fabricate detail you can't match.

---

## 3. Common Patterns (Cookbook)

Each pattern is the shortest viable form. Compose, don't copy whole blocks.

### 3.1 Inspect scene state in one call

```bash
hera-agent-unity scene info
```

Returns `{ active: {name, path, isDirty}, loaded: [...] }`. Don't `exec` this — `scene info` is dedicated.

### 3.2 Create N GameObjects with consistent naming

```bash
hera-agent-unity exec "
var root = new GameObject(\"MyRoot\");
for (int i = 0; i < 50; i++)
    new GameObject(\"Item_\" + i).transform.SetParent(root.transform, false);
return null;
"
```

Bulk creation is one `exec`. Don't loop the CLI.

### 3.3 Bulk-modify existing children

```bash
hera-agent-unity exec "
var parent = GameObject.Find(\"MyRoot\");
foreach (Transform t in parent.transform) t.position += new Vector3(0, 1, 0);
return null;
"
```

Note: `GameObject.Find` ignores **inactive** objects. If you `SetActive(false)` then `Find`, you get null and `NullReferenceException`. Use `Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "X")` if you need inactive lookup.

### 3.4 Pipe code via stdin (avoid shell escape hell)

```bash
echo 'return Application.dataPath;' | hera-agent-unity exec
```

Or load from a file:
```bash
hera-agent-unity exec --file scripts/probe.cs
```

Positional / stdin / `--file` precedence: positional > stdin > `--file`.

### 3.5 Read just the most recent error

```bash
hera-agent-unity console --type error --lines 5
```

If empty, no errors. If you need the full stack of one entry, re-run with `--stacktrace full`.

### 3.6 Compile-check before a risky exec

```bash
hera-agent-unity exec --check "var x = MyType.MaybeRenamed();"
```

On `EXEC_COMPILE_ERROR`, fix and retry. No side effects on success — you still need a separate `exec` (without `--check`) to actually run.

### 3.7 Run several commands in one HTTP round-trip

```bash
echo '{"commands":[
  {"command":"editor", "params":{"action":"refresh", "compile":true}},
  {"command":"console", "params":{"type":"error", "lines":10}}
], "options":{"fail_fast":true}}' | hera-agent-unity batch
```

`fail_fast: true` (default) stops at the first failing step. Use `fail_fast: false` when you want every step attempted. Batch is sequential — no branching, no result piping between steps. For control flow, use one larger `exec` or chain CLI calls.

### 3.8 Inspect before you exec

When unsure about a Unity API method's signature, ask the connector instead of guessing:

```bash
hera-agent-unity describe_type UnityEditor.AssetDatabase --members methods --limit 30
hera-agent-unity find_method "Refresh" --namespace UnityEditor --limit 20
```

Costs a fraction of a wrong `exec` round-trip plus a stack trace.

---

## 4. Pitfalls

### 4.1 `GameObject.Find` is active-only

`Find` walks the active object graph. An object you just `SetActive(false)`'d is invisible to it; subsequent `Find` returns `null` → `NullReferenceException` on the next member access.

Workaround: `Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "X" && g.scene.IsValid())`.

### 4.2 Cold csc compile is slow

The first `exec` per Unity session pays csc startup (5–15s on Windows). Subsequent unique `exec` bodies are ~500ms–2s (csc warm in OS cache). Identical bodies hit the in-memory assembly cache and skip compile entirely (~10ms). Don't infer the tool is broken from one slow first call.

### 4.3 Domain reload cancels in-flight HTTP

If your `exec` triggers a script recompile or asset import that causes a domain reload, the HTTP connection drops. `hera-agent-unity` auto-retries the request transparently (up to ~5s) but a connection that was mid-execute may complete *after* you get a disconnection message. Use `editor refresh --compile` explicitly when you need to be sure compilation is done before continuing.

### 4.4 `--params` JSON shape

When using `--params '{"k":"v"}'`, explicit `--k v` flags override the JSON. Don't set the same key in both.

### 4.5 Cursor / bash `$(...)` / CI: no `$null |` workaround needed

In non-TTY shells, stdin is open but never delivers EOF. The CLI's stdin reader detects this (`os.ModeNamedPipe` + `IsRegular` guard) and skips the read, so you can call `exec` exactly like you would in an interactive terminal:

```bash
hera-agent-unity exec "return Application.productName;"
```

No `$null |` prefix. No `</dev/null` redirect. If you do see `exec` hang in Cursor or CI, you're on an outdated binary — run `hera-agent-unity update`.

### 4.6 `console --clear` cannot be undone

Logs cleared can't be recovered. If you're debugging, read first, then clear.

### 4.7 Custom tools must be in an Editor assembly

`[HeraTool]` classes only auto-register if their assembly is loaded by the Editor. Runtime-only assemblies are invisible. If `list` doesn't show your new tool, check that the file lives in an `Editor/` folder or that the asmdef has `Editor` in `includePlatforms`.

### 4.8 The `agent_hint` field on stderr

Tools occasionally emit a one-line `hint:` to stderr when there's a non-obvious next action (e.g. "scene is dirty; save before close"). Read stderr alongside stdout — `2>&1` merges them.

### 4.9 `humanCategories` whitelist drives output mode

The CLI classifies commands as human-target (`install` / `uninstall` / `status` / `update` / `doctor` / `help` / `version`) or AI-target (everything else). AI-target commands automatically emit compact JSON and suppress decorative stderr. If you author a new top-level command and add it to `humanCategories`, agents will get indented output for it — usually unintended.

### 4.10 `batch` has no conditional or data passing

By design. Each step's result is reported but not piped to the next step. If you need "do X only if Y succeeded", issue separate calls. Don't try to encode logic into batch JSON. `fail_fast: true` (default) is the only branching primitive.

### 4.11 `list_assemblies` without `--filter` returns hundreds of entries

Use `--filter Unity` / `--filter MyGame` / etc. to scope. Same for `find_method` — always pass `--limit` and `--namespace` when you can. The connector won't paginate; oversized responses just inflate your context.

### 4.12 `return;` (no value) does not compile in `exec`

Your snippet is wrapped in `static object Execute() { ... }`. A bare `return;` triggers `CS0126` ("an object of a type convertible to 'object' is required"). Use `return null;` for early exits, or omit the return entirely (it falls through to `null`). `throw` also works and is preferred when the early exit represents a failure (exit non-zero via `EXEC_RUNTIME_ERROR`).

```cs
// Bad — CS0126
if (canvas == null) { Debug.LogError("missing"); return; }

// Good — explicit null
if (canvas == null) { Debug.LogError("missing"); return null; }

// Better when this is actually a failure — surfaces as EXEC_RUNTIME_ERROR
if (canvas == null) throw new System.Exception("BootCanvas not found");
```

### 4.13 PowerShell `exec` quoting

PowerShell's `'single quotes'` do **not** interpret backslash escapes — bash-style `\"` lands as a literal `\` in the snippet, then csc reports `CS1056: Unexpected character '\\'`. PowerShell's `"double quotes"` interpret `$`, backtick, and a few others as PowerShell syntax, so most C# snippets don't survive that either. Three patterns always work in PowerShell:

```powershell
# 1. Multi-line, or contains quotes — stdin pipe + here-string (preferred)
@'
var iap = AssetDatabase.FindAssets("t:IAPRewardEntry").Length;
return iap;
'@ | hera-agent-unity exec

# 2. Short, single-line — single-quoted string, write " directly (no escaping)
hera-agent-unity exec 'return AssetDatabase.FindAssets("t:IAPRewardEntry").Length;'

# 3. Long or reusable — load from disk
hera-agent-unity exec --file scripts\probe.cs
```

Anti-patterns that fail in PowerShell:

- `hera-agent-unity exec "var x = ...; return x;"` — PowerShell interprets `$`, backtick, `;` inside double quotes.
- `hera-agent-unity exec 'var s = \"a\";'` — `\"` is **literal** inside single quotes; csc rejects the `\`.
- `$code = @'...'@` in one shell call, then `hera-agent-unity exec $code` in a **separate** shell call (e.g. agents that issue each command as a fresh PowerShell process) — `$code` evaporates between calls. Chain inside one invocation: `$code = @'...'@; hera-agent-unity exec $code`.

bash equivalent: same idea, replace `@'...'@` with `<<'EOF'` heredoc or single-quoted `'...'` string. Avoid `\"` unless the outer wrapper is `"..."`.

### 4.14 Custom `[HeraTool]` namespace collisions

When you author a new tool under `AgentConnector/Editor/Tools/`, two type-name collisions reliably trigger `CS0104` on Unity's compiler the moment both `using System;` and `using UnityEditor;` are in scope (which most tools need):

- **`Object`** — `System.Object` vs `UnityEngine.Object`. Qualify destroys as `UnityEngine.Object.Destroy(...)` / `UnityEngine.Object.DestroyImmediate(...)`, or alias once: `using Object = UnityEngine.Object;`.
- **`PackageInfo`** — `UnityEditor.PackageInfo` (legacy AssetStore type) vs `UnityEditor.PackageManager.PackageInfo`. Alias once: `using PackageInfo = UnityEditor.PackageManager.PackageInfo;`.

Other pairs worth aliasing pre-emptively when you reach for them: `Random` (`System.Random` vs `UnityEngine.Random` — different semantics) and `Debug` (`System.Diagnostics.Debug` vs `UnityEngine.Debug`). Grep for bare `Object` / `PackageInfo` / `Random` / `Debug` once before triggering the first compile to skip a hotfix round-trip.

### 4.15 PowerShell `--params` JSON quoting

The same shell-escape failure mode as §4.13 hits `--params '{...}'` payloads on PowerShell. The JSON inside must reach the CLI with **raw double-quotes intact** — PowerShell does not let you bash-style escape them, and the bash-style attempt silently produces `invalid JSON in --params: invalid character '\\' ...`.

```powershell
# Works — single-quoted outer string keeps " literal
hera-agent-unity manage_components set --component_id 12345 `
  --params '{"property":"m_CenterOfMass","value":[0,1,0]}'

# Fails — backslash-escaped " survive into the JSON as literal '\"'
hera-agent-unity manage_components set --component_id 12345 `
  --params "{\"property\":\"m_CenterOfMass\",\"value\":[0,1,0]}"
```

bash equivalent: same pattern — single-quoted outer is the safe form, no `\"` rewriting.

Or sidestep `--params` entirely for simple values by splitting the keys: `--property m_CenterOfMass --value 0,1,0` ships the same Vector3 through the scalar-friendly flag (comma strings are accepted alongside JSON arrays for Vector2/3/4, Quaternion, Color, Vector2Int, Vector3Int). Reserve `--params` for nested envelopes that the scalar flags cannot represent — `{"value": {"asset_path": "..."}}`, `{"value": {"instance_id": -12345}}`, deeply-nested arrays.

---

## 5. Reference (skim on demand)

### 5.1 Major commands

| Command | Purpose | Key flags |
|---|---|---|
| `exec <code>` | Run C# in Editor | `--usings`, `--check`, `--depth N`, `--stacktrace {none\|user\|full}`, `--strict`, `--no-cache` |
| `console` | Read/clear log entries | `--type error,warning,log`, `--lines N`, `--stacktrace`, `--clear`, `--since N` |
| `scene info` / `load` / `save` / `close` / `list` | Scene management | `--mode single\|additive\|additive_without_loading` (load) |
| `editor play \| stop \| pause \| refresh` | Editor lifecycle | `--wait` (play), `--compile`, `--force` (refresh) |
| `menu "<path>"` | Execute menu item | (none) |
| `screenshot` | Capture view or isolated target | `--view scene\|game`, `--isolated`, `--target`, `--angles`, `--width`, `--height`, `--output_path` |
| `test` | Run tests | `--mode EditMode\|PlayMode`, `--filter <ns.class>` |
| `profiler hierarchy` | Profiler sample | `--depth`, `--root`, `--frames`, `--min ms`, `--sort total\|self\|calls` |
| `reserialize [paths...]` | Force YAML reserialize | (no args = whole project) |
| `log "<msg>"` | Write to Unity console | `--level log\|warning\|error` |
| `list` | List registered tools (names → name+desc → schema) | `--names` / `--compact` (names only), `--tool <name>` (full schema) |
| `batch` | Run multiple commands in one HTTP request | `--file path.json`, or pipe JSON; `options.fail_fast` |
| `list_assemblies` | List loaded assembly names | `--filter`, `--include_system`, `--include_version` |
| `manage_assets` | AssetDatabase file/folder operations + ScriptableObject authoring | `find`, `mkdir`, `create`, `copy`, `move`, `delete` |
| `describe_type <name>` | Type info + Unity-pitfalls | `--members fields\|properties\|methods\|all`, `--limit N` |
| `find_method <pat>` | Search methods across assemblies | `--namespace`, `--limit` (default 50) |
| `asset-config ui-system [ugui\|uitk]` | Select the top-level UI backend (default `ugui`) | `--json` includes `ui_system` |
| `asset-config set-csc <path>` / `set-dotnet <path>` | Persist a default csc / dotnet path | (no flags) |
| `status` / `ping` | Editor state / liveness | (none) |
| `doctor` | Self-diagnostic | `--json`, `--agent-rules` (this guide's TL;DR subset) |

### 5.2 Response envelope

Every command returns this JSON over HTTP (the CLI then prints just `data` to stdout for compactness):

```jsonc
{
  "success": true,
  "message": "human-readable summary",
  "code": "OPTIONAL_STABLE_ENUM",     // present on errors; absent on most successes
  "data": <command-specific>,         // null for void ops (return null;)
  "suggestions": ["next step", ...],  // optional, on errors
  "agent_hint": "one-line nudge",     // optional, written to stderr by CLI
  "timings": { "compile_ms": 12, "execute_ms": 3, "serialize_ms": 1 }
}
```

Common `code` values you might branch on:
- `EXEC_COMPILE_ERROR` — `data.compile_errors: [{line, col, error_code, message}, ...]`. `line` is relative to the user snippet (1-based); errors that fall inside the internal wrapper (e.g. a bad `--usings` namespace) report the raw csc line as a fallback.
- `EXEC_RUNTIME_ERROR` — `data.exception_type`, `data.stack_trace` (user-filtered unless `--stacktrace full`). The synthetic wrapper frame is collapsed to `at (your snippet)` in user-filtered mode; pass `--stacktrace full` to see the raw `__CliDynamic.Execute` frame.
- `EXEC_LOGGED_ERROR` — `--strict` mode only. `data.logged_errors: [{type, message}, ...]`, `data.returned` is the value the snippet would have returned.
- `EXEC_CSC_NOT_FOUND` / `EXEC_DOTNET_NOT_FOUND` — `suggestions[]` tells the user how to recover
- `EXEC_COMPILE_TIMEOUT` — 30s csc timeout
- `UNKNOWN_COMMAND` — typo'd command name. `data.did_you_mean: [...]` lists up to 3 commands within Levenshtein distance 2; act on the first match before re-running `list --compact`.
- `READCONSOLE_INIT_FAILED` — Unity internal API drift; `data.unity_version` for triage

### 5.3 Environment variables

Most have a `--flag` equivalent (column 2).

| Variable | Equivalent flag / effect |
|---|---|
| `HERA_AGENT_PORT=N` | `--port N` |
| `HERA_AGENT_PROJECT=<path>` | `--project <path>` |
| `HERA_AGENT_TIMEOUT_MS=N` | `--timeout N` (default 60000) |
| `HERA_AGENT_QUIET=1` | `--quiet` |
| `HERA_AGENT_DEBUG=1` | `--debug` |
| `HERA_AGENT_COMPACT_JSON=1` | `--compact-json` |
| `HERA_AGENT_VERBOSE=1` | `--verbose` |
| `HERA_AGENT_NARRATE=1` | `--narrate` |
| `HERA_AGENT_NO_PATH_CHECK=1` | Silence per-command PATH-mismatch warning (useful from wrapper binaries). |
| `GITHUB_TOKEN` | Auth token for `update` from a private release repo. |

### 5.4 Output-control flags (pro-only)

The default for AI-target commands (§4.9) is already the quietest path: compact JSON to stdout, nothing to stderr unless there's a real error. Per-call overrides:

- `--quiet` — Suppress decorative stderr (banners, progress). For when you want only the envelope.
- `--verbose` — Add per-phase timings + progress lines to stderr. For triaging slow calls.
- `--debug` — Dump full HTTP request/response bodies + discovery info. Wire-level detail; noisy.
- `--narrate` — Force `waitForAlive` progress messages on AI-target commands. For cold-start triage.

### 5.5 What `--depth` actually does

Controls how deep `exec`'s return-value serializer walks an object graph.

| `--depth` | Behavior |
|---|---|
| `1` | Primitives + one level of fields/properties. Unity Objects → shallow `{name, type, instanceID}`. |
| `2` | Adds nested fields. Unity Objects still shallow. |
| `3` (default) | Full reflection on Unity Objects too. Use sparingly — Transform at depth 3 is ~9KB. |
| `8` | Hard maximum. |

If you find yourself wanting `--depth 3` for a Transform, ask whether you really need `transform.position` etc. — usually returning the specific fields (`return new { x = t.position.x, ... }`) is both clearer and an order of magnitude cheaper.

---

## 6. When this doc is wrong

If something here contradicts what `hera-agent-unity <cmd> --help` says, trust `--help`. This guide is a curated subset, not the authoritative reference. The catalog at `docs/COMMANDS.md` is also authoritative for flag tables.

If you find a real bug or want to suggest a pattern, file an issue at `https://github.com/NotNull92/hera-agent-unity/issues`.

---

## 7. Developing hera-agent-unity itself (co-development)

> **Scope:** this section is ONLY for AI agents *building* hera-agent-unity (its Go
> CLI + C# connector). It is NOT about *using* the CLI in your own Unity project —
> if you copied this `AGENTS.md` into a downstream project to use hera, ignore this
> section; everything above still applies.

hera-agent-unity is **co-developed by Claude (Claude Code) and Codex.** The two
agents collaborate to build one polished tool — one catches what the other misses:

- **git history is the shared handoff channel.** Codex tracks project state through
  git commits, not chat context. So development work is committed with clear
  conventional-commit messages, and larger features leave a `docs/CODEX_HANDOFF_*.md`
  doc so the next agent can pick up.
- **cross-verification:** one agent implements, the other reviews/verifies. Neither
  self-approves its own work.
- both follow `CLAUDE.md` (design intent, checklists, the locked "이미 처리된 항목"
  table) and this `AGENTS.md`, and respect 🔒 locked design decisions.
- accuracy over guesses: verify against the live Editor via `hera-agent-unity`, and
  pull per-version facts from binary reflection rather than assuming Unity behavior.
