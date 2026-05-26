# Project Rule Templates

Ready-to-copy files that teach your AI coding agent to reach for `hera-agent-unity` when working in a Unity project. Pick the one that matches your agent and drop it into the target path.

| Template                           | Copy to (in your Unity project)          | Agent                |
|------------------------------------|------------------------------------------|----------------------|
| `cursor.mdc`                       | `.cursor/rules/hera-agent-unity.mdc`     | Cursor               |
| `copilot-instructions.md`          | `.github/copilot-instructions.md`        | GitHub Copilot       |
| `continuerules`                    | `.continuerules`                         | Continue.dev         |
| `AGENTS.md`                        | `AGENTS.md` (project root)               | OpenAI Codex         |
| `CLAUDE.md`                        | `CLAUDE.md` (project root)               | Claude Code CLI      |

## Why a template for each agent?

Most agents accept plain markdown — `CLAUDE.md`, `AGENTS.md`, `copilot-instructions.md`, and `.continuerules` are byte-for-byte identical. **Cursor is the exception**: its `.mdc` rule files require YAML frontmatter (`description`, `globs`, `alwaysApply`) or the rule is parsed but never activated. The `cursor.mdc` template includes that frontmatter.

## Two ways to populate

**Static (copy the template here).** Fastest, works offline, ships with the rule body baked in.

**Dynamic (have the CLI emit it).** Stays in sync with `AGENT.md` over time:

```bash
# Claude Code / Codex / Copilot / Continue.dev — plain markdown
hera-agent-unity doctor --agent-rules >> CLAUDE.md

# Cursor — adds frontmatter automatically
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
```

Either path works. The CLI output is a strict superset of the static templates (it pulls the full Quick Rules + Pitfalls sections from `AGENT.md`).
