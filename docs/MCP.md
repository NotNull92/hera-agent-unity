# Experimental MCP Adapter

Hera Agent Unity's current `main` source includes an optional stdio MCP adapter
in the existing Go CLI. This experimental adapter is not included in the latest
published CLI release, `v0.0.42`. The CLI and localhost Unity Connector remain
the execution core and the normal CLI remains the production default.

## Start the server

The adapter is disabled unless `HERA_MCP_ENABLED=1` is set. Stdio is the only
supported transport.

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --transport stdio --profile core
```

Protocol frames are written only to stdout. Diagnostics are written to stderr.
The server requires a running Unity Editor and loads its current tool catalog
before accepting MCP sessions.

Until a later CLI release includes the adapter, build the current source with a
supported Go toolchain. This does not install or alter the Unity package:

```bash
git clone https://github.com/NotNull92/hera-agent-unity.git
cd hera-agent-unity
go build -o hera-agent-unity .
./hera-agent-unity mcp --help
```

Keep the normal Unity package installation from the [README](../README.md#install).
Then configure an MCP client to launch the source-built binary as a child
process. A client configuration with `command`, `args`, and `env` fields has
this portable shape (adapt the outer key and command path to the client):

```json
{
  "command": "hera-agent-unity",
  "args": ["mcp", "--transport", "stdio", "--profile", "core"],
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

`--exposure profile` is the default. It registers the strict tools in one of
these catalog-owned profiles:

```text
core scene assets ui diagnostics testing custom full advanced
```

`--exposure compact` registers only:

```text
tool_search tool_describe tool_call
```

- `tool_search` performs deterministic lexical search over the live catalog.
- `tool_describe` returns a normalized tool definition with the current catalog
  hash and domain epoch.
- `tool_call` validates strict contracts, resolves safety policy, preserves an
  optional client operation ID, and invokes the shared Unity execution path.

Compact exposure can discover legacy custom tools from older Connectors.
Unclassified legacy tools remain confirmation-required. Connectors without
`approval_v1` return `APPROVAL_UNSUPPORTED` without dispatching the operation.

`--exposure full` registers every strict tool in the normal-policy `full`
profile. Arbitrary-code tools are excluded.

Use Profile for a stable, small native surface. Use Compact when the client
needs runtime search, when custom tools can change after startup, or when the
connected Connector is too old to publish the strict catalog. Full is an
explicit diagnostic/development opt-in, not the default.

## Advanced profile

The `advanced` profile contains arbitrary or raw execution surfaces and cannot
start without explicit process permission:

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --profile advanced --allow-arbitrary-code
```

Startup permission does not approve individual operations. Each destructive,
package, external-process, or arbitrary-code call still requires a
Connector-signed approval token bound to the exact operation.

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
(default `131072`) are stored in the local per-project result cache and returned
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
| `HERA_MCP_PROFILE` | `core` | Profile selected by Profile exposure |
| `HERA_MCP_EXPOSURE` | `profile` | `profile`, `compact`, or `full` |
| `HERA_MCP_MRTR` / `--mrtr` | `0` | Negotiate Form elicitation approval |
| `HERA_MCP_MAX_INLINE_BYTES` | `131072` | Positive maximum complete inline tool-result bytes |
| `--allow-arbitrary-code` | off | Required startup permission for `advanced` |

The normal global `HERA_AGENT_PROJECT`, `HERA_AGENT_PORT`, and
`HERA_AGENT_TIMEOUT_MS` settings also apply.

## Troubleshooting

- **`MCP server is disabled`:** set `HERA_MCP_ENABLED=1` in the child-process
  environment, not only in an unrelated terminal.
- **Profile startup says a strict catalog is required:** update the Connector
  or use `--exposure compact` for conservative legacy access.
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

The current-source MCP adapter is unreleased, experimental, default-off,
stdio-only, and limited to one selected local Editor per process. It does not
replace the localhost Connector, expose Unity over the network, bypass
Connector validation, or make arbitrary code safe.
The CLI remains the production default until the separate M17 evidence and
default-decision gate is completed. No benchmark in this repository currently
justifies promoting MCP to the default.
