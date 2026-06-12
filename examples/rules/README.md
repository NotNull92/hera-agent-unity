# Project Rule Templates

Ready-to-copy files that teach your AI coding agent to reach for `hera-agent-unity` when working in a Unity project. Pick the one that matches your agent and drop it into the target path.

The agent ecosystem is converging on **`AGENTS.md` at the project root** as the cross-tool standard — Codex reads it, recent Cursor builds read it, Claude Code is expanding to recognise it alongside `CLAUDE.md`. **For most projects, copying `AGENTS.md` is enough.** The rest of this table is for tools that ignore `AGENTS.md` or require their own format.

| Template                           | Copy to (in your Unity project)          | Agent                | Notes                                              |
|------------------------------------|------------------------------------------|----------------------|----------------------------------------------------|
| `AGENTS.md`                        | `AGENTS.md` (project root)               | Codex + AGENTS-aware | **Start here.** Single source of truth.            |
| `CLAUDE.md`                        | `CLAUDE.md` (project root)               | Claude Code CLI      | Or stub it to `> See AGENTS.md.`                   |
| `cursor.mdc`                       | `.cursor/rules/hera-agent-unity.mdc`     | Cursor               | `.cursorrules` is deprecated — use this.           |
| `copilot-instructions.md`          | `.github/copilot-instructions.md`        | GitHub Copilot       | Repo-wide. Pair with `.github/instructions/*.instructions.md` for file-pattern rules. |
| `continuerules`                    | `.continuerules`                         | Continue.dev         | Plain markdown.                                    |
| `GEMINI.md`                        | `GEMINI.md` (project root)               | Google AntiGravity   | Reads `AGENTS.md` too, but `GEMINI.md` wins. Add the on-demand skill at `.agents/skills/hera-agent-unity/SKILL.md`. |

**AntiGravity** (VS Code fork, Gemini-based, Agent-first) reads `GEMINI.md` at the project root with higher priority than `AGENTS.md`. Copy [`GEMINI.md`](GEMINI.md) there. It also supports on-demand skills — drop [`../../.agents/skills/hera-agent-unity/SKILL.md`](../../.agents/skills/hera-agent-unity/SKILL.md) into `.agents/skills/hera-agent-unity/SKILL.md` so the agent can invoke it with `@hera-agent-unity`. (`.agents/skills/` is the current default; AntiGravity still reads the legacy singular `.agent/skills/` for backward compatibility.)

## Why a template for each agent?

Most agents accept plain markdown — `AGENTS.md`, `CLAUDE.md`, `copilot-instructions.md`, and `.continuerules` share an identical body. **Cursor is the exception**: its `.mdc` rule files require YAML frontmatter (`description`, `globs`, `alwaysApply`) or the rule is parsed but never activated. The `cursor.mdc` template includes that frontmatter.

## Two ways to populate

**Static (copy the template here).** Fastest, works offline, ships with the rule body baked in.

**Dynamic (have the CLI emit it).** Stays in sync with `AGENT.md` over time:

```bash
# Claude Code / Codex / Copilot / Continue.dev — plain markdown
hera-agent-unity doctor --agent-rules >> CLAUDE.md

# Cursor — adds frontmatter automatically
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc

# AntiGravity — GEMINI.md is plain markdown; the on-demand skill needs SKILL.md frontmatter
hera-agent-unity doctor --agent-rules >> GEMINI.md
hera-agent-unity doctor --agent-rules --format antigravity > .agents/skills/hera-agent-unity/SKILL.md
```

Either path works. The CLI output is a strict superset of the static templates (it pulls the full Quick Rules + Pitfalls sections from `AGENT.md`).
