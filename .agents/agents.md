# AntiGravity Workspace Agents

This workspace is the `hera-agent-unity` project: a Go CLI plus Unity Editor connector for low-token Unity automation.

## Default Agent

Use `AGENTS.md` as the full project guide and `GEMINI.md` as the AntiGravity entry rule.

When a task touches Unity Editor behavior, scenes, GameObjects, prefabs, materials, UI, Play Mode, tests, console logs, Unity API compatibility, or the connector package, use the local skill:

```text
@hera-agent-unity
```

## Required Unity Bootstrap

Before real Unity work, run:

1. `hera-agent-unity doctor --json`
2. `hera-agent-unity status`
3. `hera-agent-unity list --compact`

Report:

```text
Connected: <project> · port=<N> · unity=<version> · state=<state> · tools=<N>
```

If the Editor is unreachable, tell the user to open Unity with the UPM connector package and stop.

## Token-Saving Defaults

- Use `list --compact` for discovery.
- Use `list --tool <name>` only for one full schema.
- Use `find_gameobjects --ids` for object handoff.
- Use `find_gameobjects --fields instance_id,name,path` only when duplicate names need hierarchy context.
- Avoid plain `list`, `console --lines 0`, and `find_gameobjects --fields all` unless the extra data is needed.
- In side-effecting `exec`, return `null` or omit the return.
- Never return Unity objects directly; return IDs or primitive fields.
