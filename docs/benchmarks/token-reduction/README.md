# Token Reduction Benchmarks

This directory tracks token-cost measurements for the v0.0.43 low-token discovery changes across Unity Editor versions.

Each Unity version gets its own benchmark file because the connector behavior, package resolution, loaded assemblies, scene state, and Unity API surface can differ by version. Do not merge different Unity versions into one result table unless the table is explicitly a cross-version summary.

## Scope

The benchmark focuses on CLI output payload size for commands that frequently come back into an AI agent context:

- `list --names`
- `list --compact`
- default `list`
- `find_gameobjects` default projection
- `find_gameobjects --ids`
- `find_gameobjects --names`
- `find_gameobjects --fields ...`

The current v0.0.43 changes being measured:

- `list --compact` now aliases `list --names`.
- `find_gameobjects` defaults to `{instance_id, name}`.
- `find_gameobjects` supports explicit projections: `--ids`, `--names`, `--fields`.

## Measurement Method

For each Unity version:

1. Open that exact Unity Editor version.
2. Resolve `com.notnull92.hera-agent-unity` to the local `AgentConnector` under test.
3. Run `editor refresh --compile --wait`.
4. Confirm `console --type error --lines 50` returns no connector compile errors.
5. Build or reuse the same realistic smoke scene shape for that Unity version.
6. Measure combined stdout/stderr UTF-8 bytes for each CLI command.
7. Estimate tokens as `ceil(bytes / 4)`.

PowerShell measurement sketch:

```powershell
$out = & hera-agent-unity find_gameobjects --name Pickup --limit 20 --ids --compact-json 2>&1
$text = ($out | Out-String).TrimEnd("`r", "`n")
$bytes = [Text.Encoding]::UTF8.GetByteCount($text)
$tokens = [Math]::Ceiling($bytes / 4)
```

The estimate intentionally measures CLI payload bytes only. It does not include AI-client-specific tool-call wrapper overhead.

## Version Results

| Unity version | File | CLI | Connector | Status | Key result |
|:---|:---|:---|:---|:---|:---|
| `2022.3.62f2` | [`2022.3.62f2.md`](2022.3.62f2.md) | `v0.0.29` | `0.0.43` local | measured | `list --compact` ~1844T -> 93T; `find_gameobjects --ids` 54T |
| `2023.2.22f1` | [`2023.2.22f1.md`](2023.2.22f1.md) | `v0.0.29` | `0.0.43` local | measured | `list --compact` 93T; `find_gameobjects --ids` 54T |
| `6000.0.x` | pending | pending | pending | not measured | pending |
| `6000.3.5f2` | [`6000.3.5f2.md`](6000.3.5f2.md) | `v0.0.29` | `0.0.43` local | measured | `list --compact` 93T; `find_gameobjects --ids` 49T |
| `6000.5.0f1` | [`6000.5.0f1.md`](6000.5.0f1.md) | `v0.0.29` | `0.0.43` local | measured | `list --compact` 93T; `find_gameobjects --ids` 55T |

## Recommended Agent Policy

Until a version-specific benchmark proves otherwise, use this low-token order:

1. `hera-agent-unity list --names`
2. `hera-agent-unity list --compact`
3. `hera-agent-unity list --tool <name>` only for the specific tool about to be called
4. plain `hera-agent-unity list` only when descriptions are needed

For GameObject search:

1. `find_gameobjects --ids` when feeding a follow-up command
2. default `find_gameobjects` when the agent needs IDs and names
3. `find_gameobjects --fields instance_id,name,path` when duplicate names require hierarchy context
4. `find_gameobjects --fields all` only for diagnostics

Avoid expensive broad reads unless the extra data is needed for the current decision:

```bash
hera-agent-unity list
hera-agent-unity find_gameobjects --fields all
hera-agent-unity console --stacktrace full --lines 0
```
