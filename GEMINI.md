# hera-agent-unity — Project Rule (AntiGravity)

`hera-agent-unity` is a CLI that drives the **running Unity Editor** over localhost HTTP: execute C# inside the Editor, read the real console, drive Play Mode, run tests, inspect live types, and capture screenshots. For any Unity Editor task in this project, reach for it first instead of guessing APIs from training data.

The full cross-tool guide lives in [`AGENTS.md`](AGENTS.md). This file adds AntiGravity-specific entry rules. The on-demand skill lives at [`.agents/skills/hera-agent-unity/SKILL.md`](.agents/skills/hera-agent-unity/SKILL.md).

## Bootstrap on discovery

When the user says anything like "find hera-agent-unity", "hera-agent-unity 찾아봐", "is it installed?", "check the editor connection", "에디터 붙어있어?", "connect to unity", or "unity에 연결해줘", run this without asking:

1. `hera-agent-unity doctor --json`
2. `hera-agent-unity status`
3. `hera-agent-unity list --compact`

Report one line:

```text
Connected: <project> · port=<N> · unity=<version> · state=<state> · tools=<N>
```

If any step fails, surface the error verbatim and stop.

## AntiGravity rules

- Use the terminal for `hera-agent-unity`; it is a CLI, not an MCP server.
- Run Hera calls sequentially. The Unity connector serializes commands on the Editor main thread.
- Pass `--compact-json` when AntiGravity will consume command output.
- Prefer `list --compact` for discovery and `list --tool <name>` only for one full schema.
- Prefer `find_gameobjects --ids` when the next command only needs object references.
- Use `describe_type`, `find_method`, and `unity_docs` before guessing Unity API signatures.
- Use `console --type error --lines 20` before assuming why Unity failed.
- In side-effecting `exec`, return `null` or omit the return.
- Never return `UnityEngine.Object`, `GameObject`, `Transform`, `Component`, `Material`, or `Scene` directly.
- Branch on the JSON `code` field, not human message text.

## PowerShell C# quoting

PowerShell single quotes pass `\"` as a literal backslash and double quotes interpret `$`, backticks, and semicolons. Pipe a here-string to stdin for multi-line C#:

```powershell
@'
return UnityEngine.Application.unityVersion;
'@ | hera-agent-unity exec --compact-json
```

## Skill

For deeper command patterns, invoke the local skill:

```text
@hera-agent-unity
```

If this file conflicts with `hera-agent-unity <cmd> --help`, trust `--help`.
