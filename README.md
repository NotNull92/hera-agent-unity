<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.24-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-6000.0%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**Measurement, not guessing — give AI hands on the live Editor.**

<sub>One Go CLI · one C# UPM package · zero runtime dependencies · MIT.</sub>

<br>

[Install](#installation) · [Quick Start](#quick-start) · [Showcase](#showcase) · [Commands](#commands) · [Batch](#batch--scripted-workflows) · [Custom Tools](#custom-tools) · [Architecture](#architecture) · [FAQ](#faq)

**English** · [한국어](README.ko.md)

</div>

---

## Why hera-agent-unity

LLMs don't know your project. They remember last year's Unity API and generalized patterns — and you pay that gap every week, in tokens and in time.

**hera-agent-unity** stands between them.

Before AI guesses your code, run it in the Editor and return the result. Before AI assumes a console error, fetch the actual log filtered by type. Before AI hypothesizes a Play Mode outcome, enter it and wait until it finishes. Before AI invents an API that doesn't exist in your Unity version, reflect on the live assembly.

No middleware. No Python, no WebSocket, no JSON-RPC. One Go binary, localhost HTTP, one C# UPM package. When Unity Editor opens, Hera is already there.

Hera responds to commands — never inferring, never assuming. It returns what your Unity is, right now, exactly as it is.

> **Guessing is expensive. Measurement is the command.**

```
┌─────────────┐       HTTP        ┌──────────────────┐
│   Terminal  │ ◄──────────────► │   Unity Editor   │
│  (1 binary) │   localhost:8090  │  (auto-starts)   │
└─────────────┘                   └──────────────────┘
```

**A small Go CLI, a single C# UPM package, zero runtime dependencies.**

> Tests, TUI, batch engine, and asset-config layer sit on top — but the engine that talks to Unity stays lean.

---

## Showcase

A game prototype built primarily with hera-agent-unity — the AI agent drove the live Editor through Hera (scene setup, component wiring, Play Mode iteration), not blind code generation.

<div align="center">

<video src="https://github.com/NotNull92/hera-agent-unity/raw/main/docs/sample/NoMoreRolls_Play.mp4" controls muted loop playsinline width="80%"></video>

<sub><b>NoMoreRolls</b> — solo-developed Unity game (3-tier architecture, 9-soul combat system). Play Mode capture.</sub>

</div>

---

## Installation

### CLI

**macOS / Linux**
```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | sh
```

**Windows** (PowerShell)
```powershell
irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex
```

<details>
<summary>Other installation methods</summary>

**`go install`** (any platform)
```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**Manual** — download a release binary from [Releases](https://github.com/NotNull92/hera-agent-unity/releases), then run it once with `install` to register it on PATH:

```bash
chmod +x ./hera-agent-unity-<platform>
./hera-agent-unity-<platform> install
```

</details>

### Unity Connector

**Package Manager → Add package from git URL**
```
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

Or add to `Packages/manifest.json`:
```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

> The connector starts automatically. No configuration. Requires Unity 6 (6000.0+).

---

## Quick Start

You only ever type four commands at this CLI. Everything else — `exec`, `editor`, `console`, `scene`, `batch`, `profiler`, `describe_type`, … — is what your **AI agent** calls on your behalf once you've handed it the wheel.

```bash
# 1. Install the CLI (one-time, see Installation above)
# 2. Open Unity with the AgentConnector UPM package — the heartbeat appears

# Is Unity connected?
hera-agent-unity status

# Later, as maintenance:
hera-agent-unity update           # pull the latest CLI release
hera-agent-unity uninstall        # remove the CLI from PATH
```

That's the **human** loop — install, verify, occasionally update. The next section is where the real work happens: hand the agent a one-line trigger and it drives the rest.

---

## Hand the Wheel to Your AI Agent

Open Claude Code, Codex, Cursor — any agent that can run a shell command. Ask:

> **"Check if hera-agent-unity is installed and explore its capabilities."**

The agent will discover the CLI, run `list`, and start driving Unity.

### Compatibility

hera-agent-unity is a plain CLI returning JSON. Any coding agent that can run shell commands works. The ecosystem is converging on **`AGENTS.md` at the project root** as the canonical multi-tool rules file — start there.

| Agent                  | Canonical path                                  | Template                                                  | Notes                                                       |
|------------------------|-------------------------------------------------|-----------------------------------------------------------|-------------------------------------------------------------|
| **OpenAI Codex** + AGENTS.md-aware tools | `AGENTS.md` (project root)         | [`examples/rules/AGENTS.md`](examples/rules/AGENTS.md)    | Cross-tool standard. Lead with this.                        |
| **Claude Code CLI**    | `CLAUDE.md` (or `AGENTS.md`)                    | [`examples/rules/CLAUDE.md`](examples/rules/CLAUDE.md)    | Reads `CLAUDE.md`; expanding to also recognise `AGENTS.md`. |
| **Cursor**             | `.cursor/rules/hera-agent-unity.mdc`            | [`examples/rules/cursor.mdc`](examples/rules/cursor.mdc)  | Per-rule files with YAML frontmatter. `.cursorrules` is **deprecated**. |
| **GitHub Copilot**     | `.github/copilot-instructions.md`               | [`examples/rules/copilot-instructions.md`](examples/rules/copilot-instructions.md) | Optional: `.github/instructions/*.instructions.md` with `applyTo` frontmatter for file-pattern-specific guidance. |
| **Continue.dev**       | `.continuerules`                                | [`examples/rules/continuerules`](examples/rules/continuerules) | Plain markdown.                                             |

For multi-tool projects, the cleanest pattern is **`AGENTS.md` as the single source** plus a one-liner stub in tool-specific paths (`> See AGENTS.md.`). Cursor is the one exception — its `.mdc` files want the full body inline because the frontmatter is what makes the rule active.

### One-time setup per project (strongly recommended)

**Static** — copy the template that matches your agent:

```bash
cp examples/rules/AGENTS.md <your-unity-project>/AGENTS.md
cp examples/rules/cursor.mdc <your-unity-project>/.cursor/rules/hera-agent-unity.mdc
```

**Dynamic** — let the CLI emit the lean rule body straight into your rules file:

```bash
# AGENTS.md / CLAUDE.md / Copilot / Continue.dev — plain markdown
hera-agent-unity doctor --agent-rules >> AGENTS.md

# Cursor — frontmatter prepended automatically
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
```

Either path locks in the core instruction and the auto-bootstrap protocol — once installed, saying *"find hera-agent-unity"* (or the Korean equivalent) makes the agent run `doctor` + `status` and report in one line, without asking.

> **Cursor note** — Cursor's `.mdc` rule files **require YAML frontmatter** (`description`, `globs`, `alwaysApply`) or the rule is parsed but never activated. Use the template or the `--format cursor` flag — a plain markdown paste will silently no-op.

---

## Commands

Grouped by what they touch. Run `hera-agent-unity <cmd> --help` for the full flag list of any entry, or `list` to inspect every registered tool (including custom ones) at runtime.

### Editor & runtime

| Command | What it does |
|---|---|
| `editor`  | Play / stop / pause / refresh / recompile. |
| `exec`    | Run arbitrary C# inside Unity — full editor & runtime access. |
| `log`     | Write to Unity console without the csc compile cost. |

### Scene & GameObjects

| Command | What it does |
|---|---|
| `scene`              | Info / load / save / list / close. |
| `manage_gameobject`  | Create / destroy / move / re-parent / set_active / rename / get_transform. |
| `manage_components`  | Component CRUD on a GameObject: `add` / `remove` / `list` / `get` / `set`. Property paths are raw `SerializedProperty` paths. |
| `find_gameobjects`   | Filter scene GameObjects (name / tag / layer / component / path glob) with pagination. |
| `manage_prefab`      | Prefab asset ops: `create` (GameObject → prefab) / `instantiate` / headless `add_component` / `remove_component`. |
| `manage_ui`          | uGUI authoring: `create` (UI element + auto Canvas/EventSystem) / `get_rect` / `set_anchor` (named preset grid) / `set_rect`. RectTransform anchor/pivot math without raw `m_` paths. |

### Assets, materials & shaders

| Command | What it does |
|---|---|
| `manage_material`     | Material asset CRUD: `create` (with a shader) / `get` / `set` (one property) / `set_shader`. |
| `manage_asset_import` | Read / write an asset's import settings via its `AssetImporter`, then reimport. |
| `describe_shader`     | Inspect a shader's properties (name / type / range) or search shader names (`--list`). |

### Packages

| Command | What it does |
|---|---|
| `manage_packages` | `list` (sync) / `add` / `remove` / `embed` (async — returns a `job_id`, CLI polls the result file). |

### Console, tests, capture

| Command | What it does |
|---|---|
| `console`     | Read, filter, clear logs. |
| `test`        | Run EditMode / PlayMode tests. |
| `menu`        | Execute any menu item by path. |
| `screenshot`  | Capture Scene or Game view. |
| `profiler`    | Read hierarchy, toggle recording. |
| `reserialize` | Force Unity to re-serialize YAML after text edits. |

### Introspection

| Command | What it does |
|---|---|
| `describe_type`   | Reflect a live type — members, signatures, **Unity pitfalls** + Manual links. |
| `find_method`     | Search method names across loaded assemblies. |
| `list_assemblies` | List loaded assemblies (skips `System.*` noise by default). |
| `unity_docs`      | Offline Unity ScriptReference lookup — `title / signature / summary / manual_url`. |

### Workflow

| Command | What it does |
|---|---|
| `batch` | Execute multiple commands atomically in one HTTP round-trip. |
| `list`  | List registered tools — slim (default), `--names`, or `--tool <name>` for full schema. |

### Status & maintenance

| Command | What it does |
|---|---|
| `status`       | Connection & project info. |
| `ping`         | Token-cheap liveness probe (heartbeat read only — no HTTP round-trip). |
| `doctor`       | Self-diagnose: PATH, installs, shell, Unity reachability (`--json` / `--agent-rules` for agents). |
| `asset-config` | Toggle optional asset integrations (TUI / `list` / `enable` / `disable` / `detect`). |
| `update`       | Self-update from GitHub Releases. |
| `install` / `uninstall` | Register or remove the CLI on PATH. |

Stuck? Run `hera-agent-unity doctor`, or open [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

---

## What's New (post-v0.0.6 capability queue)

The five-tool queue locked-in at 2026-05-28 is complete. Each entry filled a gap where `exec`'s csc warm-up cost or the syntactic overhead of wrapping a Unity API call in C# was too high for a clean AI agent workflow.

| Tool | Connector | What it does |
|---|---|---|
| `manage_gameobject` | v0.0.5 | Create / destroy / move / re-parent / set_active / rename / get_transform. Shallow `instance_id` return survives renames and reparenting. |
| `manage_packages`   | v0.0.6 | `Client.Add` / `Remove` / `Embed` / `List` driver. Async jobs (`job_id`) ride through the resolver's domain reload via an `[InitializeOnLoad]` watcher + a `Client.List` post-reload verifier. |
| `find_gameobjects`  | v0.0.7 | Filter scene GameObjects by name / tag / layer / component / path glob, with stable pagination over a hierarchy-sorted result set. |
| `manage_components` | v0.0.8 | Component CRUD via raw `SerializedProperty` paths (`m_Mass`, `m_Materials.Array.data[0]`). Reference fields accept an InstanceID, an asset path, or a `{instance_id\|asset_path}` envelope. Establishes the property-set pattern every future `manage_*` will reuse. |
| `unity_docs`        | v0.0.10 → **v0.0.12** | Offline Unity 6 ScriptReference lookup. **31,581 entries ship inside the UPM package itself** as a 1.2 MiB gzipped JSONL — no docs folder on the user's machine, no network access, no rate limits. |
| `describe_shader` · `manage_material` · `manage_prefab` · `manage_asset_import` | **v0.0.14** | Asset-editing set. `describe_shader` (inspect/search shaders) pairs with `manage_material` (material CRUD, reuses `SerializedPropertyValue`); `manage_prefab` edits prefab assets headlessly via `LoadPrefabContents`; `manage_asset_import` drives import settings through `AssetImporter` (the `manage_components` pattern). |
| `manage_ui` | **v0.0.15** | uGUI authoring. `create` spins up UI elements (canvas / panel / image / button / text / empty) with auto Canvas + EventSystem scaffolding; `set_anchor` exposes Unity's named anchor-preset grid and keeps the rect visually fixed (or `--snap` for Alt+Shift fill); `get_rect` / `set_rect` round out RectTransform editing. UI/TMP types resolve via `TypeCache`, so the connector still compiles in projects without com.unity.ugui. Element property edits stay in `manage_components`. |

### `unity_docs` — design + benchmarks (v0.0.12)

The same generic shape every "RAG-style" lookup tool has — query → keyed retrieval → minimal context back to the agent — except the data set is small enough (~10 MiB of HTML → 1.2 MiB of gzipped JSONL) that it ships *inside the connector*. No embedding model, no vector DB, no external API. A single `Dictionary<string, Entry>` carries the whole Unity 6 ScriptReference; a lazy three-layer pre-filter keeps "did you mean" suggestions cheap on miss.

**Build path** (one-time per Unity version, maintainer-run):

```bash
go run ./tools/build-unity-docs \
    --in  <path-to-Documentation/en> \
    --out AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes
```

The Go script applies the same compiled regex set the connector used to evaluate per call (`h1.heading inherit`, `signature-CS sig-block`, `h3 Description`, `switch-link` anchor, Unity version) and emits sorted JSONL. Gzip is auto-applied when the output path contains `.gz`. The committed artefact is what every CLI install picks up via the UPM package.

**Runtime path** — the connector lazy-loads the bundle the first time `unity_docs` is invoked, then everything is O(1) dict lookup with a Levenshtein fallback for misses:

| Path | Connector `total_ms` |
|:---|:---:|
| Happy lookup (dict hit) | **< 1 ms** |
| Miss in-bucket — `Rigidbod` (1-char typo) | 2 ms |
| Miss in-bucket — `MonoBehavior` (2-char typo) | 4 ms |
| Miss with bucket fallback — `Wigidbody` (first-char typo) | 2 ms |
| Miss no-match — `XyzNonsenseAbc` | 5 ms |

Cold first call (gzip decompress + JSONL parse of 31,581 entries + dict + prefix-bucket build) is ~1.7 s and runs exactly once per Unity domain — domain reloads reset the state, the next call rebuilds.

The 220 – 280 ms CLI end-to-end you see in a terminal is **Go binary startup + HTTP roundtrip + Unity queue dispatch** — shared by every hera tool, not unique to `unity_docs`.

**Response shape is intentionally minimal:**

```json
{ "title": "Rigidbody.mass",
  "signature": "public float mass;",
  "summary": "The mass of the rigidbody." }
```

~100 – 185 bytes per reply, ~33 input tokens to the agent. Down ~53 % from a verbose shape that echoed `query_normalized`, `scriptreference_url`, `unity_version`, etc. that the caller could already derive.

**Miss-path scan** uses three layered cheap pre-filters before paying for a Levenshtein DP table:

1. **Prefix bucket** — keys grouped by lowercase first letter (lazy, built once after `EnsureLoaded`). A typo missing only an internal character lands in the right bucket and scans ~1/26 of the corpus.
2. **Length filter** — `abs(len(a) - len(b)) > maxDistance` is a lower bound on the edit distance, so length-incompatible pairs are rejected without populating a DP table.
3. **Bounded Levenshtein** — the DP scan bails the moment a row min exceeds the budget; returns a `maxDistance + 1` sentinel so the caller branches on a single comparison.

If the bucket yields zero results (first-character typo) the full key set is rescanned as a fallback — the `Wigidbody` row above is that path firing, and it still returns `Rigidbody, Rigidbody2D` within 2 ms.

---

## `exec` — Runtime C#

The most powerful command. Full editor + runtime access. Zero boilerplate.

```bash
# Evaluate
hera-agent-unity exec "return Application.dataPath;"
hera-agent-unity exec "return GameObject.FindObjectsOfType<Camera>().Length;"

# Modify the scene
hera-agent-unity exec "var go = new GameObject(\"Temp\"); return go.name;"

# ECS / custom assemblies
hera-agent-unity exec "return World.All.Count;" --usings Unity.Entities

# Pipe complex code via stdin — no shell escaping
echo '
var scene = EditorSceneManager.GetActiveScene();
return scene.GetRootGameObjects().Length;
' | hera-agent-unity exec

# Or load from file
hera-agent-unity exec --file scripts/probe.cs
```

| Flag                  | Purpose                                                                                                                                  |
|-----------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| `--usings ns,...`     | Add extra using directives                                                                                                               |
| `--file <path>`       | Load code from disk (stdin and positional arg take precedence)                                                                           |
| `--csc <path>`        | Override the C# compiler path                                                                                                            |
| `--dotnet <path>`     | Override the dotnet runtime path                                                                                                         |
| `--no-cache`          | Skip the compiled-assembly cache (debug only)                                                                                            |
| `--depth N`           | Limit response object graph depth (default 3, max 8)                                                                                     |
| `--check`             | Compile-only dry run. Returns `SuccessResponse` on a clean compile, `EXEC_COMPILE_ERROR` otherwise. No `Execute()` call, no side effects.|
| `--stacktrace <mode>` | Shape of `EXEC_RUNTIME_ERROR` stack traces: `none` (exception_type only), `user` (default — drop framework frames), `full` (raw).        |
| `--strict`            | Capture `Debug.LogError` / `LogException` / `LogAssert` raised during the snippet and surface them as `EXEC_LOGGED_ERROR`.                |

**How it works.** Code is wrapped in a static method, compiled with the system's Roslyn (`csc`) into a temporary DLL, loaded into a collectible `AssemblyLoadContext` (no leaks), invoked via reflection, and the result is JSON-serialized. Identical source code is served from an in-memory cache — warm calls skip csc entirely.

Default usings include `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Reflection`, `System.Threading.Tasks`, `UnityEngine`, `UnityEngine.SceneManagement`, `UnityEditor`, `UnityEditor.SceneManagement`, `UnityEditorInternal`.

---

## Introspection — `describe_type`, `find_method`, `list_assemblies`

These three commands read **what's actually loaded in your Unity project**, not what an LLM half-remembers from its training cutoff.

```bash
# What's loaded? (skips System.* by default)
hera-agent-unity list_assemblies
hera-agent-unity list_assemblies --filter Unity.Entities

# Inspect a type — signatures + curated Unity pitfalls
hera-agent-unity describe_type UnityEditor.EditorApplication
hera-agent-unity describe_type AssetDatabase --members methods --limit 50

# Search method names across the live AppDomain
hera-agent-unity find_method Refresh --namespace UnityEditor
```

`describe_type` returns a `pitfalls` array alongside the schema — short, stable notes paired with Unity 6 Manual links. Coverage: **Editor API**, **MonoBehaviour lifecycle**, **uGUI** (Canvas, RectTransform, EventSystem, LayoutGroup, ScrollRect, Selectable, Mask, CanvasGroup, …). Each entry is `{ text, doc_url }` so an agent gets signature + gotcha + fetchable doc in a single round-trip.

Pair with `exec` for a tight dry-run loop:

```
describe_type → write code → exec → fix → exec
```

---

## Batch — Scripted Workflows

Run multi-step pipelines in a single CLI invocation. Ideal for CI, scripted automation, or large agentic plans.

```bash
hera-agent-unity batch --file workflow.json
```

```json
{
  "commands": [
    { "command": "refresh_unity", "params": { "compile": "request" } },
    { "command": "exec",          "params": { "code": "return EditorSceneManager.GetActiveScene().name;" } },
    { "command": "console",       "params": { "type": "error", "lines": 10 } }
  ],
  "options": { "fail_fast": true }
}
```

Pipe JSON via stdin if you'd rather not write a file:

```bash
echo '{"commands":[{"command":"refresh_unity","params":{"compile":"request"}}]}' \
  | hera-agent-unity batch
```

`--dry-run` previews the plan without execution. `fail_fast` short-circuits on the first error and reports which step failed.

---

## Profiler

Read the live profiler from your terminal — no UI required.

```bash
hera-agent-unity profiler enable                       # start recording
hera-agent-unity profiler hierarchy                    # top-level samples (last frame)
hera-agent-unity profiler hierarchy --depth 3          # recursive drill-down
hera-agent-unity profiler hierarchy --root PlayerLoop --depth 5
hera-agent-unity profiler hierarchy --frames 30 --min 0.5 --sort self
hera-agent-unity profiler disable                      # stop recording
hera-agent-unity profiler status
hera-agent-unity profiler clear
```

| Flag              | Purpose                                              |
|-------------------|------------------------------------------------------|
| `--depth N`       | Recursion depth (0 = unlimited, default: 1)          |
| `--root <name>`   | Substring-match root sample                          |
| `--frames N`      | Average over the last N frames                       |
| `--from N --to N` | Average over an explicit frame range                 |
| `--parent ID`     | Drill into an item by ID                             |
| `--min <ms>`      | Filter items below threshold                         |
| `--sort <col>`    | `total` (default), `self`, `calls`                   |
| `--thread N`      | Thread index (0 = main)                              |

---

## Custom Tools

Drop a C# class anywhere in your Editor assembly. It is discovered automatically — no registration, no codegen.

```csharp
using HeraAgent;
using Newtonsoft.Json.Linq;
using UnityEngine;

[HeraTool(Name = "spawn", Group = "gameplay", Description = "Spawn a prefab at a position")]
public static class SpawnEnemy
{
    public class Parameters
    {
        [ToolParameter("X world position", Required = true)] public float  X      { get; set; }
        [ToolParameter("Y world position", Required = true)] public float  Y      { get; set; }
        [ToolParameter("Z world position", Required = true)] public float  Z      { get; set; }
        [ToolParameter("Prefab name",      Default = "Enemy")] public string Prefab { get; set; }
    }

    public static object HandleCommand(JObject args)
    {
        var p      = new ToolParams(args);
        var prefab = Resources.Load<GameObject>(p.Get("prefab", "Enemy"));
        var inst   = Object.Instantiate(prefab,
                        new Vector3(p.GetFloat("x"), p.GetFloat("y"), p.GetFloat("z")),
                        Quaternion.identity);
        return new SuccessResponse("Spawned", new { name = inst.name });
    }
}
```

Call it:
```bash
hera-agent-unity spawn --x 1 --y 0 --z 5 --prefab Goblin
```

**Rules**
- Decorate with `[HeraTool]`
- Expose `public static object HandleCommand(JObject parameters)` (instance methods also work)
- Return `SuccessResponse(message, data)` or `ErrorResponse(message)`
- Use `{ get; set; }` properties in `Parameters` — fields are invisible to the schema generator
- Class name auto-converts to `snake_case` (`SpawnEnemy` → `spawn_enemy`); override with `Name =`
- Discovered on Editor start and after every script recompile
- Runs on Unity's main thread — every API is safe
- Duplicate tool names are flagged in the console; first registration wins

`hera-agent-unity list` exposes the parameter schema so agents can discover and call your tools without reading source.

---

## Architecture

```
┌──────────────────┐           ┌──────────────────────────────────┐
│    CLI (Go)      │           │         Unity Editor             │
│                  │   HTTP    │                                  │
│  ┌────────────┐  │  POST     │  ┌────────────┐                  │
│  │  Discover  │──┼──/command─┼─►│ HttpServer │ (localhost:8090+)│
│  │  Instance  │  │           │  └─────┬──────┘                  │
│  └──────┬─────┘  │           │        │ ConcurrentQueue         │
│         │ read   │           │  ┌─────▼──────┐                  │
│  ┌──────▼─────┐  │           │  │  Command   │ EditorApplication│
│  │ Heartbeat  │  │           │  │  Router    │ .update           │
│  │   files    │◄─┼───write───┼──│            │ (main thread)    │
│  │ (instance) │  │           │  └─────┬──────┘                  │
│  └────────────┘  │           │        │ SemaphoreSlim(1,1)      │
│                  │           │  ┌─────▼──────┐                  │
│  ┌────────────┐  │           │  │    Tool    │ [HeraTool]       │
│  │  Backoff   │  │           │  │ Discovery  │ reflection       │
│  │  Polling   │  │           │  └─────┬──────┘                  │
│  └────────────┘  │           │  ┌─────▼──────┐                  │
│                  │           │  │  Handlers  │ exec, editor,    │
│                  │           │  │            │ test, profiler…  │
└──────────────────┘           │  └────────────┘                  │
                               └──────────────────────────────────┘
```

| Principle                          | What it means                                                                                          |
|------------------------------------|--------------------------------------------------------------------------------------------------------|
| **Stateless**                      | Every request is independent. No sessions, no reconnect logic.                                         |
| **Auto-discovery**                 | Scans `~/.hera-agent-unity/instances/` heartbeat files. Matches by CWD, project, or port.              |
| **Domain-reload safe**             | `[InitializeOnLoad]` + assembly-reload events survive script recompiles.                               |
| **Main-thread execution**          | All tool handlers marshal through `ConcurrentQueue` + `EditorApplication.update`.                      |
| **Filesystem cross-process bus**   | Heartbeat + test-result files survive HTTP server tear-down during domain reloads.                     |
| **Atomic writes**                  | Heartbeats are written to `.tmp` then renamed — readers never see a half-written JSON.                 |

### Engineered for AI agents

- **Function-typed DI in Go.** Command handlers receive `sendFn` and `instanceResolver` as injected functions. The `resolve` closure re-discovers the instance on every call, so domain reloads that rebind the HTTP port are absorbed transparently.
- **Three-phase orchestration.** Compile-triggering commands do `waitForAlive` → send → `waitForReady`. Polling uses a 1.5× backoff (100ms → 2s cap) — finer than the usual 2× because Unity often returns to ready faster than that.
- **Compile grace period.** `editor refresh --compile` keeps `state == "compiling"` pinned for 3 s so polling doesn't latch onto a stale `"ready"` before Unity actually starts the compile.
- **Domain-reload-safe async jobs.** PlayMode tests and Package Manager `add` / `remove` / `embed` all trigger a domain reload that destroys the in-flight HTTP listener and Request handle. Each writes its outcome to `~/.hera-agent-unity/status/{test,package}-result-*.json` and the CLI polls at 500 ms. `[InitializeOnLoad]` hooks reattach watchers after the reload settles (and, for packages, run a verifying `Client.List` to reconstruct the outcome) so the result file always materialises.
- **Atomic self-update.** GitHub Releases → backup → rename → cleanup with rollback. Windows defers `.bak` deletion to a spawned PowerShell because the running `.exe` is locked.

---

## Compared to MCP

|                       | MCP integrations                  | hera-agent-unity                          |
|-----------------------|-----------------------------------|-------------------------------------------|
| **Install**           | Python + uv + FastMCP + config    | Single binary                             |
| **Runtime deps**      | WebSocket relay, persistent proc  | None                                      |
| **Protocol**          | JSON-RPC 2.0 over stdio           | Direct HTTP POST                          |
| **Setup**             | Generate config, restart client   | Add UPM package, done                     |
| **Domain reload**     | Complex reconnect logic           | Stateless (filesystem bus)                |
| **Custom tools**      | `[Attribute]` pattern             | Same `[Attribute]` pattern                |
| **Compatibility**     | MCP clients only                  | Any shell, any agent, any script          |
| **Multi-instance**    | Manual setup                      | CWD / project / port auto-discovery       |

---

## Global Flags & Environment

```bash
--port <N>          # Select Unity instance by active heartbeat port
--project <path>    # Select Unity instance by project path (substring match)
--timeout <ms>      # Request timeout in ms (default: 60000)
--verbose           # Per-phase timings + progress messages to stderr
--quiet             # Suppress decorative progress messages (errors stay plain)
--debug             # Dump HTTP request / response bodies + discovery info to stderr
--compact-json      # Emit JSON without indentation — smaller payloads for AI agents
--narrate           # Force waitForAlive / waitForReady narration even on tool commands
                    # (default: narration is on only for human-target commands)
```

Each global flag has a `HERA_AGENT_*` environment-variable counterpart that the
CLI reads when the flag is omitted. Setting these once in your shell profile
lets you keep the flag off the command line.

| Environment variable            | Equivalent flag                                                                |
|---------------------------------|--------------------------------------------------------------------------------|
| `HERA_AGENT_PORT`               | `--port <N>`                                                                   |
| `HERA_AGENT_PROJECT`            | `--project <path>`                                                             |
| `HERA_AGENT_TIMEOUT_MS`         | `--timeout <ms>`                                                               |
| `HERA_AGENT_VERBOSE`            | `--verbose`                                                                    |
| `HERA_AGENT_QUIET`              | `--quiet`                                                                      |
| `HERA_AGENT_DEBUG`              | `--debug`                                                                      |
| `HERA_AGENT_COMPACT_JSON`       | `--compact-json`                                                               |
| `HERA_AGENT_NARRATE`            | `--narrate`                                                                    |
| `HERA_AGENT_NO_PATH_CHECK=1`    | Silence the per-command PATH-mismatch warning (no flag equivalent)             |
| `GITHUB_TOKEN`                  | Auth used by `update` when the release lives on a private GitHub mirror        |

---

## FAQ

<details>
<summary><strong>Unity says "port 8090 is taken."</strong></summary>

The connector probes 8090, 8091, 8092, … up to 10 attempts. If all are occupied, look for zombie Unity processes or another local service holding the port. The CLI reads the real port from the heartbeat file — port numbers are transparent to you.

</details>

<details>
<summary><strong>Commands hang when Unity is minimized.</strong></summary>

They shouldn't — the connector calls `RepaintAllViews()` after every enqueue so the update loop runs even unfocused. If it still happens, check that the UPM package is installed and the Unity console shows `[Hera] HTTP server started on port XXXX`.

</details>

<details>
<summary><strong>`exec` fails with "Cannot find csc compiler."</strong></summary>

The compiler is auto-detected from .NET SDK, Visual Studio, or Unity's bundled Roslyn. If none are visible, install the [.NET SDK](https://dotnet.microsoft.com/download) or point at the compiler explicitly:

```bash
hera-agent-unity exec "return 1+1;" --csc "C:\Program Files\dotnet\sdk\8.0.100\Roslyn\bincore\csc.dll"
```

</details>

<details>
<summary><strong>How does it pick an instance when multiple Unity editors are open?</strong></summary>

Priority order:
1. `--port` flag (explicit)
2. `--project` flag (path substring match)
3. Current working directory matches a known project path
4. Most recent heartbeat timestamp (fallback)

</details>

<details>
<summary><strong>Is it safe to use in CI?</strong></summary>

Yes — `batch` was designed for it. Exit codes propagate per command; `fail_fast` short-circuits on the first error. The `update` command and version notice can be silenced for non-interactive runs.

</details>

</details>

---

## Projects Using Hera

| Project                                                          | Description                                                                                       |
|------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| **NoMoreRolls**                                                  | Solo-developed Unity game — 3-tier architecture, 9-soul combat system. Built with hera-agent-unity. |

> Want yours listed? Open an issue or PR.

---

## Author

**Victor** — Unity/C# developer, 6+ years of live-service MMORPG production.
Building NoMoreRolls solo with [hera-agent-unity](https://github.com/NotNull92/hera-agent-unity) · [IndieAlchemist](https://www.youtube.com/@IndieAlchemist) on YouTube.

[![GitHub](https://img.shields.io/badge/@NotNull92-181717?logo=github&logoColor=white&style=flat-square)](https://github.com/NotNull92)
[![Email](https://img.shields.io/badge/fatiger92@gmail.com-EA4335?logo=gmail&logoColor=white&style=flat-square)](mailto:fatiger92@gmail.com)

## Sponsors

hera-agent-unity is free and open source. If it saves you time or tokens, consider [sponsoring](https://github.com/sponsors/NotNull92).

Your support directly funds:
- New engine support (Godot, Unreal)
- Deeper agent integrations (Cursor, Windsurf, …)
- Documentation, video tutorials, sample projects

## License

MIT — see [LICENSE](LICENSE).
