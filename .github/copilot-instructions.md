# GitHub Copilot Instructions

This repository builds `hera-agent-unity`: a Go CLI plus Unity Editor UPM connector. The full cross-tool guide is [`AGENTS.md`](../AGENTS.md); use it as the source of truth.

## Use Hera First For Unity Work

For Unity Editor work, do not guess scene state, console errors, Play Mode state, or Unity API availability. Use the running Editor through `hera-agent-unity`.

When asked to check or use Hera/Unity, run:

1. `hera-agent-unity doctor --json`
2. `hera-agent-unity status`
3. `hera-agent-unity list --compact`

Report:

```text
Connected: <project> · port=<N> · unity=<version> · state=<state> · tools=<N>
```

If any step fails, surface the failure and stop.

## Token-Saving Defaults

- Use `hera-agent-unity list --compact` for repeated discovery.
- Use `hera-agent-unity list --tool <name>` only when one full schema is needed.
- Use `find_gameobjects --ids` when the next command only needs object IDs.
- Use `find_gameobjects --fields instance_id,name,path` only when duplicate names require hierarchy context.
- Avoid broad reads like plain `list`, `find_gameobjects --fields all`, or `console --lines 0` unless the extra data is needed.

## Exec Rules

- Prefer dedicated commands (`scene info`, `console`, `editor play`, `test`, `describe_type`, `unity_docs`, `ui_doc`) before custom `exec`.
- In side-effecting `exec`, return `null` or omit the return.
- Never return `UnityEngine.Object`, `GameObject`, `Transform`, `Component`, `Material`, or `Scene` directly.
- Branch on response `code`, not human message text.
- Use `--strict` or throw exceptions when logical failures should fail the CLI command.

## Unity API Checks

Before writing C# that depends on Unity API details, use:

```bash
hera-agent-unity unity_docs Rigidbody.AddForce
hera-agent-unity describe_type UnityEditor.AssetDatabase --members methods --limit 30
hera-agent-unity find_method Refresh --namespace UnityEditor --limit 20
```

Unity 2022.3, 2023.2, 6000.3, and 6000.5 can differ. Do not assume Unity 6 behavior applies to older editors.
