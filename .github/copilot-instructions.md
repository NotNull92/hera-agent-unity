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

## Ultra Hera

Ultra Hera is Hera's verification guidance for AI-assisted Unity work. Hera does not do the AI work by itself; it tells the agent how carefully to check Unity after using Hera.

The saved setting is `asset-config.json` → `loopEngineeringMode`:

- `off`: no extra checking rule.
- `light` (default): apply a light check to every Unity coding, Editor, and Inspector task.
- `ultra`: apply Light to every task, then upgrade strict or important work to deeper verification.

Light check: confirm the goal, read only needed state, make the change, verify compile or state, read recent console errors, re-read the changed target, retry once or twice if needed, and report short evidence.

Ultra check: compile, confirm console errors are 0, re-read Inspector/GameObject/asset state, run PlayMode or Unity tests, capture screenshot or `ui_doc` output when needed, and report evidence plus remaining risk.

Use Ultra when the user asks for strict verification, for example "play it and confirm", "match the UI", "check the Inspector too", or `정확히 검증해줘`.

## Token-Saving Defaults

- Use `hera-agent-unity list --compact` for repeated discovery.
- Use `hera-agent-unity list --tool <name>` only when one full schema is needed.
- Use `find_gameobjects --ids` when the next command only needs object IDs.
- Use `find_gameobjects --fields instance_id,name,path` only when duplicate names require hierarchy context.
- Avoid broad reads like plain `list`, `find_gameobjects --fields all`, or `console --lines 0` unless the extra data is needed.

## Version Vocabulary

- `hera-agent-unity version` reports the Go CLI release tag (`vX.Y.Z`).
- Unity Package Manager reports the connector package version from `AgentConnector/package.json` (`0.0.N`).
- Do not call the UPM package `vX.Y.Z`, do not assume CLI and connector version numbers match, and do not treat a git lock hash as the package version. The hash only identifies the resolved connector source commit.

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
