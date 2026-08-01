# Experimental MCP Adapter

Hera Agent Unity includes an optional stdio MCP adapter in the existing Go CLI.
The CLI and localhost Unity Connector remain the execution core and the normal
CLI remains the production default.

## Start the server

The adapter is disabled unless `HERA_MCP_ENABLED=1` is set. Stdio is the only
supported transport.

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --transport stdio --profile core
```

Protocol frames are written only to stdout. Diagnostics are written to stderr.
The server requires a running Unity Editor and loads its current tool catalog
before accepting MCP sessions.

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
Unclassified legacy tools remain confirmation-required and therefore return
`APPROVAL_REQUIRED` until the approval milestone is implemented.

`--exposure full` registers every strict tool in the normal-policy `full`
profile. Arbitrary-code tools are excluded.

## Advanced profile

The `advanced` profile contains arbitrary or raw execution surfaces and cannot
start without explicit process permission:

```text
HERA_MCP_ENABLED=1 hera-agent-unity mcp --profile advanced --allow-arbitrary-code
```

Startup permission does not approve individual operations. An operation that
requires confirmation still returns `APPROVAL_REQUIRED` until approval support
is available.

## Safety and reliability

Every call uses the live catalog hash and `client_kind=mcp`. Mutating calls
require Connector operation-ledger support and use a generated operation ID
when the client does not provide one. Hera error codes and structured response
envelopes are preserved as MCP tool results.
