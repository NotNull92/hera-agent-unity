# Experimental MCP Adapter

Hera Agent Unity CLI `v0.1.0+` includes an optional stdio MCP adapter in the
existing Go binary. The adapter remains experimental and default-off. The CLI
and localhost Unity Connector remain the execution core and the normal CLI
remains the production default.

## Start the server

The adapter is disabled unless `HERA_MCP_ENABLED=1` is set. Stdio is the only
supported transport.

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --transport stdio
```

Protocol frames are written only to stdout. Diagnostics are written to stderr.
The server requires a running Unity Editor and loads its current tool catalog
before accepting MCP sessions.

Install CLI `v0.1.0+` using one of the normal methods in the README, then verify
that the binary exposes the adapter command:

```bash
hera-agent-unity version
HERA_MCP_ENABLED=1 hera-agent-unity mcp --help
```

Keep the normal Unity package installation from the [README](../README.md#install).
Then configure an MCP client to launch the installed binary as a child process.
A client configuration with `command`, `args`, and `env` fields has this
portable shape (adapt the outer key and command path to the client):

```json
{
  "command": "hera-agent-unity",
  "args": ["mcp", "--transport", "stdio"],
  "env": { "HERA_MCP_ENABLED": "1" }
}
```

The client must preserve stdin/stdout for MCP and inherit access to Hera's
heartbeat directory. If several Editor heartbeats are present, add the normal
global `--project <path>` or `--port <N>` before `mcp` to select one; otherwise
Hera applies its current-working-directory and most-recent-heartbeat resolution.
Do not put access tokens in the configuration: the adapter uses the local
Connector session discovered from the heartbeat and does not expose a remote
network listener.

## Exposure modes

`--exposure compact` is the default. It registers only:

```text
tool_search tool_describe tool_call
```

- `tool_search` returns matching tool identity, action names, and compact safety
  metadata. It never embeds input schemas.
- `tool_describe(name)` returns tool identity plus action summaries. For a
  schema-only legacy tool, it returns the tool input/output schemas directly.
- `tool_describe(name, action)` returns the selected canonical action's full
  input/output and safety contract.
- `tool_call` validates strict contracts, resolves safety policy, preserves an
  optional client operation ID, and invokes the shared Unity execution path.

`--exposure profile` is an explicit opt-in. It registers the strict tools in
one of these catalog-owned profiles:

```text
core scene assets ui diagnostics testing custom full advanced
```

Compact exposure can discover legacy custom tools from older Connectors.
Unclassified legacy tools remain confirmation-required. Connectors without
`approval_v1` return `APPROVAL_UNSUPPORTED` without dispatching the operation.

`--exposure full` registers every strict tool in the normal-policy `full`
profile. Arbitrary-code tools are excluded.

Use Compact for the normal MCP product path. Use Profile only when a client
benefits from direct native tool registration and accepts its larger static
schema payload. Full is an explicit diagnostic/development opt-in. Compact also
remains the compatibility path when custom tools can change after startup or a
Connector is too old to publish the strict catalog.

The reviewed baseline records the actual serialized MCP `tools/list` definition
payload separately from the larger internal normalized catalog. For the current
34-tool catalog, Compact exposes 2,505 bytes (rough central estimate: 783 tokens),
compared with 18,635 bytes for `core` and 61,825 bytes for `full`. See
[`metrics/catalog-payload-baseline.json`](metrics/catalog-payload-baseline.json)
and [`../tools/catalog-payload-report/`](../tools/catalog-payload-report/).

## Advanced profile

The `advanced` profile contains arbitrary or raw execution surfaces and cannot
start without explicit process permission:

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --profile advanced --allow-arbitrary-code
```

Startup permission does not approve individual operations. Each destructive,
package, external-process, or arbitrary-code call still requires a
Connector-signed approval token bound to the exact operation.

Without that permission the arbitrary-code surface is not merely refused, it is
absent. Measured on 2026-08-18 against the compact default: `tool_search` for an
arbitrary-code tool returns an empty list, `tool_describe` answers
`TOOL_NOT_FOUND`, and `tool_call` answers `ARBITRARY_CODE_PERMISSION_REQUIRED` —
the approval path is never reached. Plan accordingly: a workflow that assumes
"fall back to `exec`" does not have that fallback on the compact default, and a
non-interactive CLI caller pays an approval round trip for it.

## Approval and MRTR

`HERA_MCP_MRTR=1` or `--mrtr` enables negotiated multi-round-trip approval.
When the client advertises Form elicitation, Hera shows the authoritative tool,
action, target, side-effect scope, reversibility, reload possibility,
external/package impact, and operation ID, then resumes only after acceptance.

Without Form elicitation support, the tool returns `APPROVAL_REQUIRED` with a
short-lived, single-use preflight token. Repeat the identical tool call with
that token in MCP request metadata key `hera/approval_token`. Changing the
tool, action, arguments, risk class, project, or operation ID invalidates it.
Approval is revalidated by the Connector immediately before any mutation or
`running` ledger state. `HERA_MCP_MRTR` defaults to `0`.

## Safety and reliability

Every call uses the live catalog hash and `client_kind=mcp`. Mutating calls
require Connector operation-ledger support and use a generated operation ID
when the client does not provide one. Hera error codes and structured response
envelopes are preserved as MCP tool results.

Compact `tool_call` accepts an optional `operation_id`; Profile and Full calls
generate one, and approval tokens preserve the ID chosen during preflight.
Mutations require the Connector's `operation_ledger_v1` feature. Retries with
the same operation ID and canonical arguments replay the stored response, while
conflicting reuse is rejected. A lost response is never permission to repeat an
unknown mutation with a new ID.

Long-running test and package operations use the negotiated MCP Tasks extension
when both the client and Connector advertise it. `tasks/get` reads durable
state across adapter restarts. `tasks/cancel` truthfully reports unsupported
when Unity cannot cancel the underlying operation, and `tasks/update` is not an
input channel. Without negotiated Tasks, Hera blocks and polls the same durable
result until completion (package operations may wait up to ten minutes).

Results whose complete MCP tool result exceeds `HERA_MCP_MAX_INLINE_BYTES`
(default `32768`) are stored in the local per-project result cache and returned
as integrity-checked `hera-result` resource links. Credential-shaped and
arbitrary-code results are withheld rather than written to that cache.
Stored payloads live under `~/.hera-agent-unity/results/` with restricted
permissions, a 24-hour retention window, and a 64 MiB total cache cap. An
explicit MCP resource read returns the complete stored payload, so clients must
treat resource contents as potentially sensitive project data.

## Connector compatibility

The adapter and Connector versions are independent:

- `hera-agent-unity version` is the Go CLI release (`vX.Y.Z`).
- `AgentConnector/package.json` is the Unity package version (`0.0.N`).
- A package-manager Git lock hash identifies resolved source; it is not either
  version.

Current Connectors advertise feature strings such as `tool_catalog_v1`,
`domain_epoch_v1`, `approval_v1`, `operation_ledger_v1`, and
`task_bridge_v1`. Missing features degrade conservatively:

| Missing Connector capability | Adapter behavior |
|---|---|
| strict catalog or domain epoch | Compact-only legacy discovery; Profile and Full startup fail |
| `approval_v1` | approval-required operations return `APPROVAL_UNSUPPORTED` without dispatch |
| `operation_ledger_v1` | mutations are rejected with `OPERATION_LEDGER_REQUIRED` |
| `task_bridge_v1` | no Tasks extension; asynchronous work uses blocking polling |

Read-only legacy tools remain discoverable through Compact. Legacy safety is
treated as unknown and confirmation-required; the adapter never guesses that
an old Connector is safe.

## Feature flags and startup options

| Setting | Default | Meaning |
|---|---:|---|
| `HERA_MCP_ENABLED` | `0` | Required opt-in for the experimental server |
| `HERA_MCP_PROFILE` | `core` | Profile selected by explicit Profile exposure |
| `HERA_MCP_EXPOSURE` | `compact` | `compact`, `profile`, or `full` |
| `HERA_MCP_MRTR` / `--mrtr` | `0` | Negotiate Form elicitation approval |
| `HERA_MCP_MAX_INLINE_BYTES` | `32768` | Positive maximum complete inline tool-result bytes |
| `--allow-arbitrary-code` | off | Required startup permission for `advanced` |

The normal global `HERA_AGENT_PROJECT`, `HERA_AGENT_PORT`, and
`HERA_AGENT_TIMEOUT_MS` settings also apply.

## Troubleshooting

- **`MCP server is disabled`:** set `HERA_MCP_ENABLED=1` in the child-process
  environment, not only in an unrelated terminal.
- **Profile startup says a strict catalog is required:** update the Connector
  or remove the explicit `--exposure profile` override to use Compact.
- **Client reports invalid JSON or protocol noise:** stdout must contain MCP
  frames only. Remove shell banners, wrappers that print status, and debug
  redirection into stdout. Hera diagnostics belong on stderr; do not merge
  streams with `2>&1` in an MCP client configuration.
- **No Unity instance:** open the Editor with the Connector and verify
  `hera-agent-unity doctor --json`, `hera-agent-unity status`, then
  `hera-agent-unity list --compact` outside the MCP session.
- **Approval cannot continue:** enable `--mrtr` only if the client supports Form
  elicitation; otherwise repeat the identical request with the returned token.

## Release boundary

The MCP adapter ships in CLI `v0.1.0+` but remains experimental, default-off,
stdio-only, and limited to one selected local Editor per process. It does not
replace the localhost Connector, expose Unity over the network, bypass
Connector validation, or make arbitrary code safe.
When the adapter is explicitly enabled, Compact is its default exposure;
Profile and Full remain opt-in surfaces.
M17 completed with the decision to retain the CLI as the production default.
No benchmark in this repository currently justifies promoting MCP to the
default.
