<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.25-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-2022.3%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**Low-token Unity Editor control for AI coding agents.**

<sub>Let Codex, Claude, Cursor, Copilot, and AntiGravity inspect and change your live Unity project — no MCP setup, no Python server.</sub>

<br>

[What it is](#what-it-is) · [Why it helps](#why-it-helps) · [Quick Start](#quick-start) · [Install](#install) · [Commands](#commands) · [Token Saving](#token-saving) · [UI Juicy Mode](#ui-juicy-mode) · [Ultra Hera](#ultra-hera) · [Unity Versions](#unity-versions) · [Agent Rules](#add-project-rules-for-agents) · [Projects](#projects-using-hera) · [FAQ](#faq)

**English** · [한국어](README.ko.md)

</div>

---

## What It Is

`hera-agent-unity` is a low-token CLI that lets AI coding agents control a running Unity Editor.

Think of it like a remote control for the live Editor:

| You want the AI to... | Hera lets it... |
|:---|:---|
| See if Unity is open | ask the real Editor |
| Run C# code | run it inside your loaded project |
| Check console errors | read the actual Unity Console |
| Enter Play Mode | press Play and wait |
| Create or edit objects | use Unity APIs safely |
| Build UI | create real Unity UI objects and capture the result |

The AI does not need to guess from stale training data. It can inspect the real Editor, act on it, and check the result.

```text
AI agent  ->  hera-agent-unity  ->  Unity Editor
```

---

## Why It Helps

AI often makes mistakes in Unity because it cannot see your Editor.

It may guess:

- which scene is open;
- which objects exist;
- which Unity API exists in your version;
- whether Play Mode works;
- what error is in the console.

Hera fixes that by letting the AI ask Unity directly.

```bash
hera-agent-unity status
hera-agent-unity console --type error
hera-agent-unity exec "return Application.unityVersion;"
hera-agent-unity editor play --wait
```

No Python server. No generated MCP config. No special agent plugin. If an agent can run a shell command, it can use Hera.

---

## Release Highlights

This release focuses on three simple things: more Unity versions, fewer tokens, and safer agent verification.

| Highlight | Simple meaning |
|:---|:---|
| **NEW: Unity 2022.3 LTS support** | Teams do not need to upgrade to Unity 6 first. |
| **NEW: Unity 2023.2 support** | The connector and docs lookup work below Unity 6. |
| **Unity 6000.3 / 6000.5 checked separately** | Unity 6 minor versions can differ, so they are tested separately. |
| **93-token tool list** | `list --compact` is small enough to use often. |
| **49-55-token object handoff** | `find_gameobjects --ids` returns only the IDs an agent needs for the next command. |
| **Signature: UI Juicy Mode** | Hera can tell the agent how to make generated UI feel alive, not static. |
| **NEW: Ultra Hera** | Agents can use light checks by default and upgrade to strict Unity verification when the task asks for it. |

Measured versions:

| Unity Editor | `list --compact` | `find_gameobjects --ids` | Details |
|:---|---:|---:|:---|
| 2022.3.62f2 | **93 T** | **54 T** | [benchmark](docs/benchmarks/token-reduction/2022.3.62f2.md) |
| 2023.2.22f1 | **93 T** | **54 T** | [benchmark](docs/benchmarks/token-reduction/2023.2.22f1.md) |
| 6000.3.5f2 | **93 T** | **49 T** | [benchmark](docs/benchmarks/token-reduction/6000.3.5f2.md) |
| 6000.5.0f1 | **93 T** | **55 T** | [benchmark](docs/benchmarks/token-reduction/6000.5.0f1.md) |

Full benchmark notes: [docs/benchmarks/token-reduction/README.md](docs/benchmarks/token-reduction/README.md)

---

## Quick Start

### 1. Open Unity

Open a Unity project that has the Hera Unity package installed.

### 2. Check the connection

```bash
hera-agent-unity status
```

You should see the project name, Unity version, port, and state.

### 3. Ask your AI agent to use it

Example prompt:

```text
Use hera-agent-unity. Check the Unity console, enter Play Mode, reproduce the issue, and fix it.
```

The agent can then run commands like:

```bash
hera-agent-unity console --type error
hera-agent-unity editor play --wait
hera-agent-unity exec "return EditorSceneManager.GetActiveScene().name;"
hera-agent-unity test --mode PlayMode
```

---

## Install

There are two parts:

1. the CLI program on your computer;
2. the Unity package inside your project.

### CLI

**Windows PowerShell**

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex"
```

Open a new terminal after install, then check:

```powershell
hera-agent-unity version
```

**macOS / Linux**

```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | bash
```

**Go install**

```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**Manual**

Download a binary from [Releases](https://github.com/NotNull92/hera-agent-unity/releases), then run:

```bash
hera-agent-unity install
```

### Unity Package

In Unity:

```text
Window -> Package Manager -> Add package from git URL
```

Use this URL:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

Or add this to `Packages/manifest.json`:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

The connector starts by itself when Unity opens.

---

## Commands

Here are the commands most agents use first.

| Command | What it does |
|:---|:---|
| `status` | Shows which Unity Editor is connected. |
| `doctor --json` | Checks install, PATH, and Unity connection. |
| `list --compact` | Lists tools with a small response. |
| `console --type error` | Reads real Unity errors. |
| `exec "..."` | Runs C# inside Unity. |
| `editor play --wait` | Enters Play Mode and waits. |
| `editor stop` | Stops Play Mode. |
| `scene info` | Shows the active scene. |
| `find_gameobjects` | Finds objects in the loaded scenes. |
| `manage_gameobject` | Creates, moves, renames, parents, or deletes GameObjects. |
| `manage_components` | Adds, removes, reads, or edits components. |
| `ui_doc` | Builds and captures Unity UI. |
| `test` | Runs Unity tests. |
| `screenshot` | Captures Scene or Game view. |
| `batch` | Runs several commands in one request. |

Full command list: [docs/COMMANDS.md](docs/COMMANDS.md)

---

## Token Saving

Hera is built for agents, so small answers matter.

Big answers become input tokens. Input tokens cost money and fill context. So common Hera commands return small data by default.

Good default path:

```bash
hera-agent-unity list --compact
hera-agent-unity find_gameobjects --name Player --ids
hera-agent-unity list --tool manage_gameobject
```

Use bigger output only when needed:

```bash
hera-agent-unity list
hera-agent-unity find_gameobjects --fields all
hera-agent-unity console --lines 0 --stacktrace full
```

---

## Unity UI From a Screenshot

Unity UI is hard for AI because anchors, pivots, and layout groups are easy to guess wrong.

Hera gives the AI a loop:

1. read the current UI;
2. build real Unity UI objects;
3. capture what Unity rendered;
4. compare and fix.

```bash
hera-agent-unity ui_doc export --path /Canvas/HUD
hera-agent-unity ui_doc sample --image hud_ref.png --region "0,0,1,0.2"
hera-agent-unity ui_doc apply --file hud.json --parent /Canvas --mode upsert
hera-agent-unity ui_doc capture --out hud_built.png
```

This is the main idea: do not guess the UI. Measure it.

---

## UI Juicy Mode

AI can make a button that works. UI Juicy Mode helps it make a button that feels like a game.

When this mode is on, Hera adds an `agent_hint` to UI creation results. The hint gives concrete game-feel recipes: hover scale, press squash, release bounce, popup overshoot, count-up numbers, damage text motion, haptics, and reduce-motion reminders.

It is guidance, not runtime bloat. Hera does not attach heavy gameplay components for you. The agent receives the recipe, then applies the animation or feedback through normal Unity edits.

Turn it on in Unity:

```text
HeraAgent -> Hera Settings -> UI Juicy Mode
```

If DOTween is enabled in the same Hera Settings panel, the hint suggests DOTween-style tweens. If not, it falls back to coroutine or lerp-style guidance.

Common recipes:

| UI element | Juicy guidance |
|:---|:---|
| Button | Hover grow, press squash, release bounce, click sound, haptic. |
| Popup / panel | Pop-in entrance, screen dim, fast quiet exit. |
| Text | Staggered text, count-up numbers, floating damage text. |
| Image / reward | Pop-in, rarity pulse, glow, hover lift. |
| Bar | Instant fill drop, delayed chip bar, low-value pulse, segment ticks. |

Detailed command docs: [docs/COMMANDS.md](docs/COMMANDS.md#ui_doc)

---

## Ultra Hera

<div align="center">

<img src="docs/assets/ultra_hera_logo.png" width="42%" alt="Ultra Hera">

<br>

**Hera's signature verification mode for AI-assisted Unity work.**

<sub>Ultra Hera helps an AI agent check its Unity work before it says "done".</sub>

</div>

Ultra Hera is Hera's safety belt for AI Unity work.

When an AI changes code, a scene, or the Inspector, it can be wrong in small ways: Unity may not compile, the Console may have errors, a GameObject may not have the component you expected, or Play Mode may fail after the edit.

Ultra Hera gives the agent a simple rule:

```text
Do the work. Check the work. Only then report the result.
```

It does not replace the AI. It tells the AI how carefully to verify Unity work after using Hera.

Find it here:

```text
HeraAgent -> Hera Settings -> Ultra Hera
```

Modes:

| Mode | Simple meaning |
|:---|:---|
| `Off` | No extra checking rule. |
| `Light` | Default. The agent does a small check after every Unity task, so it does not finish in a clearly wrong state. |
| `Ultra` | The agent uses Light checks for every task, then upgrades important requests to stronger checks like tests, Play Mode, Inspector reads, screenshots, or `ui_doc` capture. |

Think of the modes like this:

| Mode | Like a... | What it checks |
|:---|:---|:---|
| `Light` | Quick seatbelt check | "Did Unity compile? Are there Console errors? Did the thing I changed really change?" |
| `Ultra` | Full pre-flight check | "Does it compile, run, look right, and match the user's request with evidence?" |

Use Light for everyday coding and Inspector edits. Use Ultra when the user says things like "verify exactly", "play it and confirm", "match the UI", or "check the Inspector too".

What Ultra Hera makes agents do better:

- Check Unity instead of guessing.
- Read only the state they need.
- Compile after edits.
- Read real Console errors.
- Re-check the changed GameObject, component, asset, or UI.
- Use Play Mode, tests, screenshots, or `ui_doc` capture when the task needs stronger proof.
- Report short evidence instead of a vague "it should work".

Representative Light commands:

```bash
hera-agent-unity status
hera-agent-unity console --type error --lines 20
hera-agent-unity editor refresh --compile
hera-agent-unity find_gameobjects --ids
hera-agent-unity exec --depth 1 ...
```

Representative Ultra commands:

```bash
hera-agent-unity test --mode EditMode
hera-agent-unity test --mode PlayMode
hera-agent-unity editor play --wait
hera-agent-unity screenshot --view game
hera-agent-unity ui_doc capture --out ...
```

The goal is simple: the agent should not close the task while Unity is still broken.

---

## Unity Versions

| Unity version | Status | Notes |
|:---|:---|:---|
| 2022.3 LTS | NEW · Supported | Verified on `2022.3.62f2`. |
| 2023.2 | NEW · Supported | Verified on `2023.2.22f1`. |
| 6000.0 - 6000.4 | Supported | Unity 6. |
| 6000.5+ | Supported | Uses Unity's newer object ID system when needed. |
| Older than 2022.3 | Not supported | Minimum supported version is Unity 2022.3. |

---

## Add Project Rules For Agents

Put Hera rules in your Unity project so agents know how to use it before they start guessing.

This repository includes ready-to-use rule files for the main coding agents:

| Agent | File to add | Why |
|:---|:---|:---|
| Codex / Claude / Gemini CLI / most agents | `AGENTS.md` | One shared guide for shell-based agents. |
| Cursor | `.cursor/rules/hera-agent-unity.mdc` | Cursor needs `.mdc` frontmatter to activate project rules. |
| GitHub Copilot | `.github/copilot-instructions.md` | Repo-wide Copilot instructions. |
| GitHub Copilot, file-specific | `.github/instructions/hera-agent-unity.instructions.md` | Applies Hera rules to Unity files like `.cs`, `.prefab`, `.unity`, and `Assets/**`. |
| Google AntiGravity | `GEMINI.md`, `.agents/agents.md`, `.agents/skills/hera-agent-unity/SKILL.md` | Project entry rule, workspace handoff, and on-demand skill. |
| Continue.dev | `.continuerules` | Plain markdown rules. |

Fast setup for the common shared file:

```bash
hera-agent-unity doctor --agent-rules >> AGENTS.md
```

Cursor setup:

```bash
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
```

Copilot, AntiGravity, and Continue templates are in [examples/rules](examples/rules). This repo also contains live examples at [.github/copilot-instructions.md](.github/copilot-instructions.md), [.github/instructions/hera-agent-unity.instructions.md](.github/instructions/hera-agent-unity.instructions.md), [GEMINI.md](GEMINI.md), and [.agents/skills/hera-agent-unity/SKILL.md](.agents/skills/hera-agent-unity/SKILL.md).

The most important rules are:

- use `list --compact` to find available tools;
- use `find_gameobjects --ids` when the next command only needs object IDs;
- return `null` from side-effecting `exec` calls;
- do not return big Unity objects directly;
- read `console --type error` instead of guessing errors.

---

## How It Works

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
        | Unity main thread
        v
Scene, Console, Play Mode, Assets, UI
```

The Unity package starts a small local HTTP server. The CLI sends commands to it. The command runs inside the Editor.

Architecture details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

---

## FAQ

### Is this MCP?

No. It is a normal CLI. That is why it works with Codex, Claude Code, Cursor, and any agent that can run shell commands.

### Does it need Python?

No.

### Does it work when several Unity Editors are open?

Yes. Use `--project` or `--port` when you need to choose one.

```bash
hera-agent-unity --project MyGame status
hera-agent-unity --port 8091 status
```

### What should I do when it cannot connect?

Run:

```bash
hera-agent-unity doctor --json
```

Also check that the Unity package is installed and Unity has finished compiling.

### Where are the detailed docs?

- [docs/COMMANDS.md](docs/COMMANDS.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/CSHARP_CONNECTOR.md](docs/CSHARP_CONNECTOR.md)
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

---

## Projects Using Hera

| Project | Notes |
|:---|:---|
| **NoMoreRolls** | Solo-developed Unity game. Built with AI driving the Editor through Hera. |

<div align="center">

![NoMoreRolls Play Mode preview](docs/sample/NoMoreRolls_Play_preview.gif)

<sub><b>NoMoreRolls</b> — Play Mode preview from a Unity game built with Hera-assisted editor work. [Watch the full video on GitHub](https://github.com/NotNull92/hera-agent-unity/blob/main/docs/sample/NoMoreRolls_Play_full_preview.mp4).</sub>

</div>

---

## Author

**Victor** — Unity/C# developer with 6+ years of live-service MMORPG production experience.

GitHub: [@NotNull92](https://github.com/NotNull92)

---

## License

MIT. See [LICENSE](LICENSE).
