<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![GitHub stars](https://img.shields.io/github/stars/NotNull92/hera-agent-unity?style=flat-square&logo=github&label=stars&color=181717)](https://github.com/NotNull92/hera-agent-unity/stargazers)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.25-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-2022.3%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

# hera-agent-unity

**Give your AI coding agent hands, eyes, and a checklist inside Unity.**

<sub>Codex, Claude, Cursor, Copilot, AntiGravity, and other shell-capable agents can inspect your live Unity Editor, change it, run it, test it, see the result, and keep fixing until the evidence says it is done.</sub>

<br>

[Start in 3 steps](#how-do-i-use-it) · [What can it do?](#what-can-it-actually-do) · [Why Hera?](#why-use-hera) · [How good is it?](#how-good-is-it) · [Examples](#what-can-i-do-with-it) · [Commands](#command-overview)

[English](README.md) · [한국어](README.ko.md)

</div>

---

## Hera in one minute

An AI can write Unity C# without Hera, but it cannot reliably know what your **live Editor** looks like after the code is written.

Normally the loop looks like this:

```text
You ask the AI
   ↓
AI writes code
   ↓
You open Unity and wait for compile
   ↓
You copy the error back to the AI
   ↓
AI fixes it
   ↓
You press Play and explain what happened
   ↓
repeat...
```

Hera closes that loop:

```text
You ask the AI
   ↓
AI uses Hera
   ↓
Unity compiles, runs, tests, clicks UI, captures the result
   ↓
Hera gives the real result back to the AI
   ↓
AI fixes what failed and checks again
   ↓
verified result
```

A simple way to think about it:

> **The AI is the brain. Hera gives that brain hands to operate Unity, eyes to inspect the result, and a checklist so it does not stop while the project is still broken.**

Hera does not replace Unity and it does not replace the coding agent. It connects the two so the agent can work with facts from your actual project instead of guessing from source code alone.

---

## What problem does it solve?

Unity development has a feedback loop that lives outside the source files.

A change can look correct in code and still fail because:

- Unity did not compile it;
- the wrong Scene is open;
- a GameObject or Component is missing;
- the Inspector has a different serialized value;
- a Unity API changed in your Editor version;
- the Console contains an exception;
- Play Mode behaves differently from Edit Mode;
- a button is visually present but cannot receive input;
- a UI layout is technically valid but looks wrong;
- the AI says "done" before it has actually checked any of those things.

Hera lets the agent ask the real Editor instead.

```bash
hera-agent-unity status
hera-agent-unity console --type error
hera-agent-unity scene info
hera-agent-unity editor play --wait
hera-agent-unity test --mode PlayMode
```

The important part is not the command names. The important part is that the agent can **observe → change → run → verify → repair** without making you act as the courier between the AI and Unity.

---

## What can it actually do?

You can use Hera for tiny one-line checks or for a full AI-assisted Unity workflow.

| What you want the AI to do | What Hera gives it |
|:---|:---|
| Check whether Unity is healthy | Live Editor status, version, project, compile state, Console errors |
| Understand the current Scene | Scene info, GameObject search, Component and Inspector reads |
| Change the Scene | Create, duplicate, rename, parent, move, or delete GameObjects |
| Edit Components | Add, remove, inspect, and change serialized Component values |
| Work with project assets | Find, create, copy, move, or delete assets under `Assets/` |
| Run project-specific C# | Execute C# inside the loaded Editor with access to Unity APIs and project assemblies |
| Make animations | Author AnimationClips and AnimatorController state machines |
| Test a feature | Run EditMode and PlayMode tests and keep the result across domain reloads |
| Play the game | Enter Play Mode, wait for the real state change, inspect, then stop |
| See what Unity rendered | Capture Scene/Game views, isolated objects, or live uGUI overlays |
| Test Unity input | Inspect uGUI raycasts through EventSystem, or synthesize optional Input System keyboard/mouse state in Play Mode |
| Build UI | Author uGUI or UI Toolkit layouts and verify the generated result |
| Recreate a reference UI | Measure a reference, build the real Unity UI, capture it, compare, and iterate |
| Improve game feel | Give the agent recipes for shake, hit stop, feedback, camera, sound, rewards, and accessibility |
| Clean up generated-looking UI | Detect common spacing, hierarchy, typography, color, and decoration problems |
| Create your own studio commands | Add project-specific `[HeraTool]` actions that appear automatically |
| Work with several open Editors | Target the intended project and keep that project identity through port changes |
| Require approval for risky work | Preflight destructive operations and continue only with the matching approval token |

In short, Hera is not just a "press Play" remote. It is a bridge for the **whole edit-and-check loop** around a running Unity project.

---

## Why use Hera?

### 1. The AI can check its own work

Without Editor access, the agent often ends with:

> "This should work."

With Hera, it can finish with evidence such as:

```text
compile: passed
console errors: 0
EditMode tests: 18/18
PlayMode tests: 6/6
button click: verified through EventSystem
final Game View: captured
```

That difference is the main reason Hera exists.

### 2. You stop being the copy-and-paste bridge

You no longer need to repeatedly:

1. copy code from the AI;
2. switch to Unity;
3. wait for compile;
4. copy errors back;
5. explain the Scene hierarchy;
6. press Play;
7. describe what happened.

The AI can perform most of that loop itself.

### 3. It works with the tools you already use

The normal production path is a CLI. Any agent that can run shell commands can use it.

- Codex
- Claude Code
- Cursor
- GitHub Copilot
- AntiGravity
- scripts and CI jobs
- your own automation

No Python server is required. MCP is optional, not mandatory.

### 4. It is designed for AI context, not just humans

A giant tool response becomes more input for the model to read. Hera therefore gives common commands compact views such as IDs-only GameObject searches and on-demand tool schemas.

The goal is simple: **send the agent the smallest amount of Unity state that is enough to make the next decision.**

### 5. It knows that "request sent" is not the same as "work finished"

Unity recompiles scripts, reloads domains, changes ports, enters Play Mode, runs tests, and sometimes drops a connection while doing it.

Hera tracks these workflows instead of treating a successful HTTP send as proof that Unity is finished.

### 6. It can grow with your project

Start with the built-in commands. Later you can add a `[HeraTool]` for the workflow your own project repeats every day: build a dungeon room, validate a quest graph, bake a table, spawn a test battle, or check your studio-specific asset rules.

---

## How good is it?

Hera avoids vague "AI magic" claims. The repository keeps concrete measurements and compatibility evidence instead.

### Small responses for common agent reads

Measured low-token baselines for `list --compact` are **about 93 estimated tokens** across the tested Unity versions. `find_gameobjects --ids` measured **49 to 55 estimated tokens** in the retained cross-version fixtures.

| Unity Editor | `list --compact` | `find_gameobjects --ids` |
|:---|---:|---:|
| 2022.3.62f2 | **93 T** | **54 T** |
| 2023.2.22f1 | **93 T** | **54 T** |
| 6000.3.5f2 | **93 T** | **49 T** |
| 6000.5.0f1 | **93 T** | **55 T** |

`T` is a simple `ceil(UTF-8 bytes / 4)` estimate for Hera's CLI payload only. It is not provider billing telemetry. Full methodology: [token-reduction benchmark](docs/benchmarks/token-reduction/README.md).

### Compatibility is checked across Unity generations

The current Connector source is **0.0.82** and the current CLI release is **v0.1.4**. The Connector's exact source passed the release compile gate in these representative Editors:

| Unity Editor | Result |
|:---|:---:|
| 2022.3.62f2 | PASS |
| 2023.2.22f1 | PASS |
| 6000.0.35f1 | PASS |
| 6000.3.5f2 | PASS |
| 6000.5.6f1 | PASS |

CLI and Connector versions are intentionally separate.

### A real game-creation run reached a verified playable result

The retained Crystal Forge scenario asked an AI to author code and tests, build UI, compile, drive Unity EventSystem input, run tests, capture the rendered result, and leave the Editor clean.

**Final result: PASS after repair. First attempt: FAIL.**

The measured execution window was **15 minutes 52 seconds**. The run is useful because the failures were kept instead of being edited out. It showed why a closed verification loop matters: hidden state was correct before the UI was actually visible, and the agent had to observe, repair, and verify again.

This is not a claim that Hera makes every task succeed on the first try, and it is not an "X% smarter AI" benchmark. It demonstrates something more practical: **Hera can give an agent enough real Editor feedback to find and repair integration failures instead of stopping at the first plausible answer.**

Full evidence: [Crystal Forge real-world benchmark](docs/benchmarks/user-scenario/crystal-forge-6000.3.5f2.md).

---

## How do I use it?

There are only two pieces:

```text
your computer                 your Unity project
──────────────                ──────────────────
Hera CLI        <---------->  Hera Unity Connector
```

### Step 1. Install the CLI

The simplest cross-platform option is npm:

```bash
npm install --global hera-agent-unity
```

Or use the native installer.

**Windows PowerShell**

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex"
```

**macOS / Linux**

```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | bash
```

Check it:

```bash
hera-agent-unity version
```

<details>
<summary>Other CLI installation methods</summary>

**Go install**

```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**Manual**

Download a binary from [GitHub Releases](https://github.com/NotNull92/hera-agent-unity/releases), then run:

```bash
hera-agent-unity install
```

</details>

### Step 2. Add the Unity package

In Unity:

```text
Window -> Package Manager -> Add package from git URL
```

Paste:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

Or add it to `Packages/manifest.json`:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

The Connector starts automatically when the Editor opens.

To pin an existing Connector tag:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-<version>"
```

### Step 3. Open Unity and check the connection

```bash
hera-agent-unity doctor --json
hera-agent-unity status
```

You should see the real project path, Unity version, Editor state, and connection information.

Now you can simply tell your agent:

```text
Use hera-agent-unity for this Unity project.
Check the current Editor state first.
Make the requested change.
Compile it, read the real Console errors, verify the changed object or UI,
and do not say it is finished until Unity is in a clean state.
```

That is the normal Hera workflow.

---

## What can I do with it?

### Fix a compiler or runtime error

```text
Use Hera. Read the Unity Console, find the actual error, fix the code,
compile again, and keep repeating until the error Console is clean.
```

Typical commands behind the scenes:

```bash
hera-agent-unity console --type error --lines 20
hera-agent-unity editor refresh --compile
hera-agent-unity console --type error --lines 20
```

### Build a feature and prove it works

```text
Implement the inventory filter.
Use Hera to compile it, run the relevant EditMode and PlayMode tests,
enter Play Mode if needed, and report the final evidence.
```

### Reproduce a gameplay bug

```text
Open the correct Scene, enter Play Mode, inspect the related objects,
reproduce the bug, fix it, then reproduce the same path again to prove the fix.
```

### Build UI from a reference image

Hera can give the agent a measurement loop instead of an eyeballing loop:

```text
reference image
   ↓ sample colors / measure layout
Unity UI
   ↓ capture
compare
   ↓ fix
capture again
```

Example commands:

```bash
hera-agent-unity ui_doc export --path /Canvas/HUD
hera-agent-unity ui_doc sample --image hud_ref.png --region "0,0,1,0.2"
hera-agent-unity ui_doc apply --file hud.json --parent /Canvas --mode upsert
hera-agent-unity ui_doc capture --out hud_built.png
```

Hera supports both **uGUI** and **runtime UI Toolkit**. The selected UI system is explicit, so the agent does not silently mix the two.

### Verify buttons without guessing screen coordinates

```bash
hera-agent-unity input state
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
```

This verifies Unity's EventSystem path. It is not a physical Windows/macOS mouse click, so Hera reports those two kinds of evidence separately.

Projects that already use the optional Input System package can also verify gameplay input without adding a Hera dependency:

```bash
hera-agent-unity input state --backend inputsystem
hera-agent-unity input keyboard --key space --mode press
hera-agent-unity input mouse --mode click --button left --position 640,360
hera-agent-unity call input --json '{"action":"sequence","steps":[{"action":"keyboard","key":"space","mode":"down"},{"action":"keyboard","key":"space","mode":"up"}]}'
hera-agent-unity call input --json '{"action":"record","mode":"start"}'
hera-agent-unity call input --json '{"action":"record","mode":"stop"}'
hera-agent-unity call input --json '{"action":"replay","path":"Library/HeraAgent/Recordings/input.json"}'
```

Keyboard, mouse, bounded sequence, recording capture, and replay require Play Mode. Recordings use the bounded `hera.input-recording/1` JSON format under the project or system temp directory; replay validates the complete file before mutation and reuses sequence-owned cleanup. Hera resolves the package at runtime, never creates devices, and releases any held controls when Play Mode exits.

### Automate repetitive Scene work

You can ask an agent to:

- create a test arena;
- place prefabs under a new root;
- add and configure Components;
- create ScriptableObject assets;
- wire Animator states;
- save the Scene;
- run validation afterwards.

### Create studio-specific tools

If your project has a repeated workflow, expose it as a custom `[HeraTool]`. The tool appears in Hera's live catalog automatically.

Examples:

- `build_test_battle`
- `validate_item_database`
- `spawn_quest_fixture`
- `bake_localization_table`
- `check_prefab_rules`

Hera can therefore start as a generic Unity bridge and gradually become a CLI for **your own game production pipeline**.

---

## Ultra Hera: make "done" mean checked

<div align="center">

<img src="docs/assets/ultra_hera_logo.png" width="42%" alt="Ultra Hera">

<br>

**Do the work. Check the work. Only then report the result.**

</div>

Ultra Hera is a verification rule for AI-assisted Unity work. It does not write the feature by itself. It tells the agent how carefully to check the work it just did through Hera.

Find it in:

```text
HeraAgent -> Hera Settings -> Ultra Hera
```

| Mode | Easy meaning |
|:---|:---|
| `Off` | No extra verification rule. |
| `Light` | Default. Compile/check state, read errors, and re-read the changed target before finishing. |
| `Ultra` | For important work. Add stronger evidence such as tests, Play Mode, Inspector reads, screenshots, or `ui_doc` capture. |

Think of `Light` as a seatbelt check and `Ultra` as a pre-flight inspection.

Use Ultra when the request sounds like:

- "verify it exactly";
- "play it and confirm";
- "match this UI";
- "check the Inspector too";
- "do not finish until all tests pass".

The goal is simple: **the agent should not close the task while Unity is still broken.**

---

## More than Editor control

Hera includes optional guidance and authoring systems that help the agent do more than change raw objects.

### UI systems

| Backend | Best for | Hera creates |
|:---|:---|:---|
| `ugui` | Canvas-based UI | GameObjects, RectTransforms, Components |
| `uitk` | Runtime UI Toolkit | validated UXML, USS, `PanelSettings`, `UIDocument` |

Choose explicitly:

```bash
hera-agent-unity asset-config ui-system uitk
```

Hera validates the selected backend instead of guessing from the Scene.

### Game Feel Mode (Beta)

Helps the agent think about how gameplay **feels**, not only whether it functions: screen shake, hit stop, knockback, camera, control feel, sound, reward presentation, haptics, and accessibility constraints.

```bash
hera-agent-unity asset-config gamefeel on
hera-agent-unity game_feel hit-stop
```

The knowledge is guidance. Hera does not secretly add heavy runtime systems to your game.

### Game Feel UI Mode (Beta)

Adds practical UI feedback recipes such as hover scale, press squash, popup entrance, count-up text, health-bar response, cooldown feedback, and accessibility baselines.

```bash
hera-agent-unity asset-config gamefeel-ui on
```

### Unity De-slop Mode (Beta)

Helps agents catch common generated-looking UI habits: unnecessary decoration, weak spacing systems, box-in-box layouts, decorative italics, inconsistent colors, and other visual tells.

```bash
hera-agent-unity asset-config uislop on
hera-agent-unity ui_slop box-in-box
```

The rules include exceptions so functional game UI such as inventory cells is not flattened just because it is repetitive.

---

## Command overview

You do not need to memorize these. They are here so you can understand the surface Hera gives an agent.

| Command | Plain-language purpose |
|:---|:---|
| `doctor --json` | "Is Hera installed and can it reach Unity?" |
| `status` / `ping` | Check Editor state and liveness. |
| `list --compact` | Discover available built-in and project-specific tools cheaply. |
| `call <tool>` | Validate a strict live tool contract, then call it. |
| `console` | Read or clear the real Unity Console. |
| `scene` | Inspect, load, save, list, or close Scenes. |
| `find_gameobjects` | Search the loaded Scene hierarchy. |
| `manage_gameobject` | Create and edit GameObjects. |
| `manage_components` | Read, add, remove, or modify Components. |
| `manage_assets` | Work with project assets under `Assets/`. |
| `manage_animation` | Author AnimationClips and AnimatorController state machines. |
| `exec` | Run arbitrary project-aware C# inside the Editor. |
| `editor` | Play, stop, pause, refresh, and compile. |
| `test` | Run or resume Unity tests. |
| `task` | Inspect durable test/package work without contacting Unity. |
| `screenshot` | Capture Scene/Game views or isolated objects; optionally return bounded uGUI identity/blocking coordinates, including metadata-only mode. |
| `ui_doc` | Inspect, build, sample, and capture Unity UI. |
| `input` | Test uGUI or optional Input System keyboard/mouse/sequence/record/replay state. |
| `profiler` | Read profiler hierarchy snapshots. |
| `game_feel` | Query game-feel guidance. |
| `ui_slop` | Query UI cleanup guidance. |
| `batch` | Run several operations in one request. |
| custom `[HeraTool]` | Call tools defined by your own Unity project. |

Full reference: [docs/COMMANDS.md](docs/COMMANDS.md).

---

## Teach your AI agent to use Hera automatically

You can put Hera's operating rules in the project so the agent knows to inspect Unity before guessing.

### Codex plugin

```bash
codex plugin marketplace add NotNull92/hera-agent-unity --ref main
```

Then open `/plugins`, choose **Hera Agent Unity**, and enable **Hera Unity**.

### Standalone Agent Skill

```bash
npx skills add NotNull92/hera-agent-unity --skill hera-agent-unity --agent codex
```

### Shared `AGENTS.md`

```bash
hera-agent-unity doctor --agent-rules --compact >> AGENTS.md
```

The compact default guidance is intentionally small. Its reviewed baseline is **2,277 UTF-8 bytes** and contains the important rules for bootstrap, targeting, approvals, safety, and verification. The full guide is available on demand.

This repository also ships templates for Cursor, Copilot, AntiGravity, Continue, and other agent environments under [examples/rules](examples/rules).

---

## Safety and reliability in plain language

Hera can make real changes to a Unity project, so "fast" is not enough. It also needs to know when to stop.

### It identifies the project, not just a port number

Unity can change its local port after a domain reload or restart. Hera prefers the normalized full project path as the Editor identity and treats the port as a temporary endpoint.

If several Editors are open, use:

```bash
hera-agent-unity --project /full/path/to/project status
```

Ambiguous targeting fails instead of guessing.

### Risky operations can require approval

Approval-gated work is preflighted first. The returned token is tied to that exact request and is single-use. Changing the target or arguments invalidates the approval.

### It does not blindly repeat an uncertain mutation

If a response disappears during a reload or timeout, Hera checks fresh Editor ownership/state before an eligible retry. A mutation is not resent merely because the network response was unclear.

### Slow tests can be resumed instead of started again

A long Test Runner job can outlive a normal request window. Hera stores durable run state so an agent can resume waiting for the same run instead of accidentally starting another test execution.

---

## Unity versions

| Unity version | Status | Representative verification |
|:---|:---|:---|
| 2022.3 LTS | Supported | `2022.3.62f2` |
| 2023.2 | Supported | `2023.2.22f1` |
| 6000.0 - 6000.4 | Supported | Unity 6 compatibility buckets |
| 6000.5+ | Supported | `6000.5.6f1` release gate |
| Older than 2022.3 | Not supported | Minimum is Unity 2022.3 |

Version-specific behavior is checked against live Editors rather than assumed from one Unity version.

---

## CLI first, MCP optional

The production default is the normal CLI.

```text
AI / terminal -> Hera CLI -> localhost Connector -> Unity Editor
```

That means any shell-capable coding agent can use Hera without configuring MCP.

CLI `v0.1.0+` also ships an **experimental, default-off, stdio-only MCP adapter** for hosts that intentionally want MCP discovery and invocation. It uses the same Hera execution core instead of creating a second Unity backend.

```text
AI with MCP -> optional Hera MCP adapter -> same Hera execution core -> Unity
```

MCP does not magically make the model smarter. It is another way to expose the same Unity capabilities. Hera keeps the CLI path as the default because it remains simple, explicit, and broadly compatible.

MCP setup and compatibility boundaries: [docs/MCP.md](docs/MCP.md).

---

## Current release

- CLI: **v0.1.4**
- Unity Connector source: **0.0.82**
- License: **Apache-2.0**

The two version numbers are separate on purpose. The CLI and the Unity package can evolve independently while keeping their compatibility contract explicit.

v0.1.4 focuses on safer multi-Editor targeting, a bounded always-loaded agent context, catalog growth review, and reproducible release/package evidence.

For release-by-release engineering detail, read [CHANGELOG.md](CHANGELOG.md) instead of treating the main README as a migration log.

---

<details>
<summary><strong>How does Hera work internally?</strong></summary>

```text
Terminal / AI agent
        |
        | hera-agent-unity command
        v
Go CLI
        |
        | localhost HTTP
        v
Unity Editor package
        |
        | serialized Unity main-thread work
        v
Scene, Console, Play Mode, Assets, Tests, UI
```

The Unity package opens a local HTTP listener. The CLI selects the intended Editor from local heartbeat state and sends the command. Unity work is marshaled to the Editor main thread.

Domain reloads and long-running operations use filesystem-backed state so compilation, tests, and recovery can survive the HTTP listener being recreated.

Architecture details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

</details>

<details>
<summary><strong>Advanced exec, token, and async notes</strong></summary>

- Prefer dedicated commands over arbitrary `exec` when one exists.
- Use small projections such as `find_gameobjects --ids` when IDs are enough.
- Side-effecting `exec` snippets should normally return `null` or nothing rather than a large status object.
- Do not return a full `UnityEngine.Object` unless you truly need its reflected graph.
- Use `--strict` or throw an exception when a logged error must fail the CLI operation.
- Use `exec --check` when you want to compile-check a snippet without executing it.
- Long asynchronous workflows are better represented as tracked `[HeraTool]` actions or durable task/test operations than as detached work inside a one-shot `exec`.

The complete agent operating guide is [AGENTS.md](AGENTS.md).

</details>

---

## FAQ

### Does Hera make the AI smarter?

No. Hera gives the AI better access to **your real Unity state** and better ways to verify the result. The coding model still makes the design and implementation decisions.

### Does it need Python?

No. The normal install is one native CLI plus one Unity package.

### Do I need MCP?

No. The CLI is the production default. MCP is optional and default-off.

### Can it control more than one open Unity Editor?

Each command targets one Editor. If several are open, use the full `--project` path for the clearest selection. Hera tracks the selected project identity even if the local port changes.

### Can it physically click the Unity window?

The `input` command sends Unity EventSystem events for uGUI QA and can synthesize optional Input System keyboard/mouse state in Play Mode. Both prove Unity-level behavior, not a physical operating-system click. Physical click evidence must be reported separately.

### Can it build UI Toolkit as well as uGUI?

Yes. Hera has separate uGUI and runtime UI Toolkit backends. Select the backend explicitly instead of mixing them.

### What should I do if it cannot connect?

```bash
hera-agent-unity doctor --json
```

Also check that the Unity package is installed and the Editor has finished compiling.

### Where are the detailed docs?

- [Commands](docs/COMMANDS.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Architecture](docs/ARCHITECTURE.md)
- [C# Connector](docs/CSHARP_CONNECTOR.md)
- [MCP adapter](docs/MCP.md)
- [UI document contract](docs/UI_DOC_IR.md)
- [Agent operating guide](AGENTS.md)

---

## Projects using Hera

| Project | Notes |
|:---|:---|
| **NoMoreRolls** | Solo-developed Unity game. Built with AI driving the Editor through Hera. |

<div align="center">

https://github.com/user-attachments/assets/15d353e4-b7bb-4534-bbca-c27de0792147

<sub><b>NoMoreRolls</b> - full Play Mode video from a Unity game built with Hera-assisted Editor work.</sub>

</div>

---

## Author

**Victor** - Unity/C# developer with 6+ years of live-service MMORPG production experience.

GitHub: [@NotNull92](https://github.com/NotNull92)

Discord: [Join the Hera community](https://discord.gg/QBzEVuYwK)

---

## Support

Hera is free and licensed under Apache-2.0. If it saves you time, you can support development:

[![Support on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/notnull92)

---

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
