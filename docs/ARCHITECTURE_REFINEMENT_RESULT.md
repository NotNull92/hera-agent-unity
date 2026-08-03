# Architecture Refinement Result

## Verdict

The completed CLI + optional MCP architecture remains the correct execution architecture for local Unity Editor control. This refinement wave did not replace the Go CLI, MCP adapter, localhost Connector, Unity main-thread queue, heartbeat, operation ledger, or file-bus recovery.

The work improved cleanliness where the cost was concrete:

- one authoring source for cross-language wire constants;
- an explicit version for single-command execution metadata;
- an explicit MCP catalog lifecycle;
- one isolated legacy CLI compatibility boundary;
- action-specific Compact discovery;
- smaller active rule context;
- repeatable release, compatibility, race, and payload evidence.

Connector source is `0.0.80`, unreleased. No commit, push, tag, release, publication, dependency installation, or production-project mutation occurred.

## Completed tasks

| Task | Result |
|---|---|
| A0 Baseline and roadmap | PASS |
| A1 Active rule-context diet | PASS |
| A2 Shared protocol manifest | PASS |
| A3 Versioned single-command metadata | PASS |
| A4 Action-specific Compact describe | PASS |
| A5 Explicit MCP catalog lifecycle | PASS |
| A6 Legacy CLI compatibility boundary | PASS |
| A7 Unity compatibility matrix runner | PASS |
| A8 Release-gate ownership | PASS |
| A9 Catalog payload budget | PASS |
| A10 Keep-alive and observer optimization | DEFERRED BY MEASUREMENT GATE |
| A11 Final evidence and decision | PASS |

## Architectural changes

### Contract ownership

`contracts/runtime-contracts.json` is now the authoring source for stable cross-language wire constants. `tools/generate-runtime-contracts` emits checked-in Go and C# constants and supports deterministic drift checking. Tool definitions, schemas, DTOs, and business logic remain hand-readable rather than being hidden behind broad code generation.

The shared manifest owns:

- tool-catalog schema version;
- single-command execution protocol version;
- heartbeat feature names;
- asset-config lock record version and stale threshold.

### Versioned execution metadata

Current single-command requests send:

```json
{
  "meta": {
    "protocol_version": "hera.execution/1"
  }
}
```

A missing version remains compatible with older clients. An unknown non-empty version fails with `EXECUTION_PROTOCOL_UNSUPPORTED` before catalog validation, approval, ledger, or handler execution. Batch remains on its existing contract and is not described as versioned.

### MCP lifecycle

Catalog lifecycle is represented by three states:

```text
ready
refreshing
restart_required
```

A transient catalog refresh produces `CATALOG_STALE`. A Tasks capability transition produces `MCP_RESTART_REQUIRED`; `tools/list` and tool calls fail immediately rather than waiting on a signal that cannot complete. Ordinary replacement cannot clear restart-required state.

### Compact discovery

Compatibility is preserved:

```text
tool_describe(name)          -> full tool contract
tool_describe(name, action)  -> selected canonical action only
```

Aliases resolve to canonical actions. Unknown actions return `ACTION_NOT_FOUND` with compact available-action names.

Measured example:

```text
input/state full tool describe: 27,926 bytes
input/state selected action:     2,264 bytes
saved:                          25,662 bytes, 91.89%
```

### Legacy CLI boundary

`cmd/legacy_tool.go` owns only the original dynamic custom-tool passthrough and legacy `exec` input adaptation. Strict `call`, specialized commands, approval, and transport remain outside that adapter. No one-method-per-command abstraction layer was introduced.

### Rule context

The active `CLAUDE.md` was reduced from `92,278` bytes to `39,519` bytes before the concise refinement lock was added. The `54,008`-byte completed-decision history is preserved in `docs/DECISION_LEDGER.md` and is read only when a proposed change overlaps an old decision.

### Verification ownership

`ReleaseGateTests.CanonicalSuiteNames` is the explicit automated release-gate manifest. Its NUnit coverage test detects wrapper drift. Legacy menu runners remain optional maintainer conveniences and tests remain isolated from the production assembly.

## Payload baseline

Current normalized catalog baseline:

```text
31 tools
75 actions
185,339 normalized bytes
8,123 tool-description characters
```

The report in `docs/metrics/catalog-payload-baseline.json` separates raw bytes from labelled token estimates and lists profile, tool, action, and action-describe sizes. Budgets are warnings, not unproven hard release failures.

## Verification evidence

### Standard Go and repository gates

```text
gofmt -l .                               PASS
go vet ./...                             PASS
go build ./...                           PASS
go test -count=1 ./...                   PASS
golangci-lint run ./...                  PASS, zero issues
runtime-contract generator --check       PASS
Connector package integrity              PASS
agent-guide drift check                  PASS
git diff --check                         PASS
```

### Race matrix

Race-instrumented binaries passed these `18` packages:

```text
cmd
internal/assetconfig
internal/client
internal/mcpserver
internal/policy
internal/poll
internal/resultstore
internal/schema
internal/taskbridge
internal/telemetry
internal/toolregistry
tools/benchmark-mcp
tools/build-unity-docs
tools/catalog-payload-report
tools/generate-runtime-contracts
tools/sync-agent-guides
tools/validate-connector-package
tools/validate-tool-catalog
```

### Unity exact-source compatibility

Current `0.0.80` Editor and TestRunner source compiled against all five supported buckets:

```text
2022.3.62f2   PASS
2023.2.22f1   PASS
6000.0.35f1  PASS
6000.3.5f2   PASS
6000.5.6f1   PASS
```

### Unity package runtime

On a disposable `6000.5.6f1` local-package fixture:

```text
21 total
21 passed
0 failed
0 skipped
```

The fixture manifest was restored byte-for-byte to SHA-256:

```text
d7027d5eb027b50a40aef8c935e70f2ee985b54dc24efbcaef2b6004eef3fe96
```

No `testables` field remained afterward.

## Deferred A10

The following were deliberately not changed:

- HTTP keep-alive policy;
- stable-domain observer interval;
- event-driven catalog invalidation.

A10 may start only when the benchmark records p50/p95 latency and call/result bytes, reproduces the historical Mono idle-channel warning, verifies domain-reload recovery, and proves zero safety regression. Clean-looking transport code is not enough evidence.

## Rollback map

- A1: restore the historical table to `CLAUDE.md` and remove the ledger pointer.
- A2/A3: revert the manifest, generator, generated constants, feature, and protocol metadata together.
- A4: remove only the optional `action` describe path; name-only compatibility remains the baseline.
- A5: revert lifecycle state and its structured restart errors together.
- A6: move the cohesive legacy adapter body back into `cmd/dispatch.go`.
- A7/A8: remove the matrix runner and NUnit ownership manifest without moving tests into production assembly.
- A9: remove the report tool and metrics file; runtime behavior is unaffected.

## Final boundary

This wave is complete. Future work should not reopen the execution architecture merely for cleanliness. A new architecture is justified only by a new product boundary such as multi-tenant remote control, cross-engine orchestration, untrusted plugin isolation, or high-frequency streaming that the current adapter-plus-executor model cannot provide.
