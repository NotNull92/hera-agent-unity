# Post-Migration Stability Remediation

## Purpose

This document turns the post-M17 stability review into bounded implementation tasks. The goal is not another architecture migration. The existing Go CLI, optional stdio MCP adapter, localhost HTTP Connector, dynamic tool catalog, Unity main-thread queue, approval flow, file-bus recovery, and single-selected-Editor model remain the architecture.

The remediation has four goals:

1. prevent stale contracts, duplicate mutations, and ambiguous outcomes;
2. keep UPM Hera Settings reliable under concurrent or interrupted writes;
3. remove defenses whose cost is larger than the risk they mitigate;
4. restore executable Connector evidence before release.

## Baseline

```text
Repository: repository root (`%HERA_AGENT_UNITY_REPO%`)
Branch:     main
HEAD:       e7612612f685e3f2e7c3f55bbbef158b14cd5456
Worktree:   clean at remediation start
CLI:        v0.1.0 installed; current source reports dev
Connector:  0.0.78 at remediation start
```

No commit, tag, push, package publication, dependency installation, or persistent external-project mutation is authorized by this document. Marked disposable verification fixtures may be changed only temporarily when the original files are retained and restored byte-for-byte in a `finally` path.

## Locked boundaries

- Do not move MCP into the Unity Connector.
- Do not split CLI and MCP tool contracts.
- Do not remove the existing CLI syntax.
- Do not weaken approval, operation-ID binding, project targeting, schema validation, or arbitrary-code isolation.
- Keep Go validation and Connector validation. They protect different trust boundaries.
- Do not split methods merely to reduce line counts. Extract only when a separate state transition, side effect, or independently testable contract exists.
- Full MCP remains opt-in. Profile and Compact remain the token-control paths.
- A Connector source change requires an unreleased Connector version bump and exact-source compilation evidence.

## Status summary

| Task | Priority | Status | Depends on |
|---|---:|---|---|
| S0 Baseline, document, and guardrails | P0 | PASS | none |
| S1 Connector catalog freshness gate | P0 | PASS | S0 |
| S2 Operation ledger scope and lifecycle | P0 | PASS | S1 |
| S3 Hera Settings last-known-good cache | P0 | PASS | S0 |
| S4 Cross-process asset-config lock recovery | P1 | PASS | S3 |
| S5 HTTP continuation and timeout outcome contract | P1 | PASS | S1, S2 |
| S6 MCP runtime feature-transition fail-closed gate | P1 | PASS | S1 |
| S7 Oversized-result sensitivity precision | P2 | PASS | S0 |
| S8 Connector test harness restoration | P1 | PASS | S1-S7 |
| S9 Long-lived MCP transport and polling efficiency | P2 | DEFERRED | S8 |
| S10 Rule/docs synchronization and release matrix | P1 | PARTIAL (compile PASS, runtime partial) | S8 |

`PASS` means the task-specific tests, relevant Go gates, exact-source Connector compile, documentation impact, and diff review completed. `BLOCKED` is used when a required Unity version or executable test environment is unavailable. Static review is never promoted to PASS evidence.

---

## S0. Baseline, document, and guardrails

### Objective

Record the exact starting state, preserve unrelated work, and establish one remediation ledger.

### Verification

```text
git status --short --branch
git rev-parse HEAD
git diff --check
```

### Completion gate

- Baseline is reproducible.
- Worktree contained no unrelated changes before implementation.
- Every later task records files changed, tests, limitations, and rollback.

### Evidence

- **Status:** PASS
- **Branch / HEAD:** `main` / `e7612612f685e3f2e7c3f55bbbef158b14cd5456`
- **Initial worktree:** clean and aligned with `origin/main`
- **Behavior changed:** none
- **Rollback:** delete this document

---

## S1. Connector catalog freshness gate

### Problem

Go Typed CLI and MCP calls send `meta.catalog_hash`, but the Connector currently stores the value without comparing it to the live catalog. A request validated against an earlier contract can therefore reach a reloaded Connector.

### Required behavior

```text
catalog_hash omitted
    -> allow legacy-compatible request

catalog_hash equals current domain catalog hash
    -> continue

catalog_hash differs
    -> return CATALOG_STALE before approval consumption, ledger creation,
       handler construction, or mutation
```

Error data:

```json
{
  "request_catalog_hash": "sha256:...",
  "current_catalog_hash": "sha256:...",
  "domain_epoch": "..."
}
```

### Target files

- `AgentConnector/Editor/Core/ToolContractCanonicalJson.cs`
- `AgentConnector/Editor/Core/CommandRequestContext.cs`
- `AgentConnector/Editor/CommandRouter.cs`
- focused Connector tests

### Constraints

- Cache the catalog hash for the current Unity domain. Do not rebuild all contracts on every request.
- Do not reject legacy clients that omit the field.
- Do not write a ledger record for a stale request.

### Tests

- matching hash passes;
- omitted hash passes;
- mismatched hash returns `CATALOG_STALE` with exact structured fields;
- mismatch is detected before handler or ledger work;
- a new Unity domain naturally receives a new domain-local cache.

### Completion gate

- Exact-source Connector compile has zero warnings or errors.
- A live stale-hash probe is rejected.
- A normal current-hash probe succeeds.

### Rollback

Revert the domain hash cache, request check, and tests together.

---

## S2. Operation ledger scope and lifecycle

### Problems

1. Read-only idempotent requests currently receive durable `received`, `running`, `committed`, and `responded` writes.
2. Old `running` records are skipped forever by cleanup and are not removed by the byte-cap compactor.

### Required behavior

- Durable ledger is required for operations with side effects.
- `ReadOnly && Idempotent` requests bypass the durable ledger.
- A `running` record from another domain becomes `outcome_unknown` during cleanup.
- A same-domain `running` record older than the conservative execution ceiling also becomes `outcome_unknown`.
- `outcome_unknown` keeps the existing seven-day retention.
- The 64 MiB cap includes all ledger files.
- Current-domain, non-expired `running` records are never deleted by the cap.

### Target files

- `AgentConnector/Editor/CommandRouter.cs`
- `AgentConnector/Editor/Core/OperationLedger.cs`
- `AgentConnector/Editor/Tests/OperationLedgerTests.cs`

### Tests

- read-only safety bypasses durable ledger;
- write and destructive safety still use ledger;
- prior-domain running transitions to unknown;
- expired same-domain running transitions to unknown;
- current active running remains;
- hard cap cannot be bypassed by response-less stale records.

### Completion gate

- Existing replay, conflict, response-loss, approval, and no-reinvoke tests remain green.
- A live read-only call creates no operation file.
- A mutation still produces replayable committed state.

### Rollback

Revert routing scope and lifecycle changes together. Existing ledger files remain forward-readable.

---

## S3. Hera Settings last-known-good cache

### Problem

`HeraSettings` stores the file timestamp before JSON parsing. A transient partial or locked read can replace all settings with defaults and suppress another read for the same timestamp.

### Required behavior

- Parse into local values first.
- Publish values and the successful timestamp only after complete parsing.
- Preserve the last-known-good snapshot on read or parse failure.
- Retry a failed timestamp after a short bounded backoff.
- Log at most one English warning per failing timestamp.
- A genuinely missing file still resets product defaults.

### Target files

- `AgentConnector/Editor/Core/HeraSettings.cs`
- focused Connector settings-cache tests

### Tests

- successful initial read;
- malformed replacement preserves prior values;
- same timestamp recovers after retry;
- missing file resets defaults;
- legacy `ui_juicy_mode` remains supported;
- UI system and compiler paths publish atomically with booleans.

### Completion gate

Existing consumers continue using the same public properties with no consumer rewrite.

### Rollback

Revert cache state and focused tests. No config format changes.

---

## S4. Cross-process asset-config lock recovery

### Problem

Both Go and C# create an empty `.lock` file. If the owner process exits without cleanup, every future writer waits five seconds and fails forever.

### Lock record

```json
{
  "version": 1,
  "pid": 1234,
  "acquired_at_ms": 0,
  "nonce": "..."
}
```

### Recovery policy

- Recover only when the lock is older than the stale threshold and its recorded process is not alive.
- Recover a legacy empty or malformed lock only after the same threshold.
- Re-read and compare nonce immediately before deletion.
- Access denied or indeterminate process state means “assume alive”.
- Never steal a live owner lock.

### Target files

- `AgentConnector/Editor/Core/AssetConfigFile.cs`
- `AgentConnector/Editor/Tests/AssetConfigPersistenceTests.cs`
- `internal/assetconfig/persistence.go`
- platform-specific Go process-liveness helpers
- `internal/assetconfig/config_test.go`

### Tests

- live owner remains busy;
- dead stale owner is recovered;
- recent dead owner is not recovered;
- malformed legacy stale lock is recovered;
- nonce change prevents deletion;
- unknown config fields still round-trip.

### Completion gate

C# and Go use the same threshold and fail-safe interpretation.

### Rollback

Revert both implementations and tests together. Old clients treat a version-1 lock as an existing busy lock.

---

## S5. HTTP continuation and timeout outcome contract

### Problems

- `HttpServer` creates three default `TaskCompletionSource<object>` instances; continuations may execute inline on the Unity main thread.
- A dispatched request cancelled by its caller may still complete in Unity, while some timeout paths return only a generic target error.

### Required behavior

- The three HTTP queue completion sources use `RunContinuationsAsynchronously`.
- Once an attempt is dispatched, cancellation or timeout returns an operation-outcome-unknown error containing operation and target identity.
- MCP converts that error into a structured tool result instead of an opaque protocol failure.
- CLI tool commands emit one compact JSON error envelope, not duplicated output.

### Target files

- `AgentConnector/Editor/HttpServer.cs`
- `internal/client/operation.go`
- `internal/client/reload_retry.go`
- `internal/client/transport.go`
- `internal/mcpserver/native_tools.go`
- CLI error mapping and focused tests

### Tests

- target diagnostics remain available through `errors.As`;
- ledger-capable timeout wraps `OperationOutcomeUnknownError`;
- MCP result includes operation ID, tool, project, and port;
- CLI emits one stable envelope;
- successful calls remain unchanged.

### Completion gate

Existing target restart, lost, and unresponsive tests pass; MCP stdout remains protocol-only.

### Rollback

Revert continuation and error mapping changes together. Wire request formats remain unchanged.

---

## S6. MCP runtime feature-transition fail-closed gate

### Problem

`taskMode` is fixed at MCP startup while catalog refresh replaces the live instance and snapshot. A Connector upgrade or downgrade can change `task_bridge_v1` without changing capabilities already advertised to the client.

### Required behavior

- Detect a task-bridge feature transition during domain refresh.
- Mark runtime stale and require MCP process restart.
- Do not silently add or remove Tasks capabilities mid-session.
- Existing task reads remain available through the durable CLI task store.

### Target files

- `internal/mcpserver/catalog_refresh.go`
- `internal/mcpserver/m13_invalidation_test.go`
- test helpers as needed

### Tests

- unchanged capability refreshes normally;
- false-to-true and true-to-false transitions fail closed;
- state remains stale until restart;
- normal catalog add/remove remains unchanged.

### Completion gate

All `internal/mcpserver` and process-level MCP tests pass.

### Rollback

Revert transition check and tests.

---

## S7. Oversized-result sensitivity precision

### Problem

Substring matching marks benign keys such as `cancellationToken`, `tokenCount`, or `secretDoor` as credentials, preventing result spooling and causing repair calls.

### Required behavior

- Continue blocking arbitrary-code results and recognized credential payloads.
- Match exact credential keys and strong credential suffixes, not arbitrary substrings.
- Generic `token` is sensitive only as an exact normalized key.
- Preserve bearer-header and private-key string signatures.

### Target files

- `internal/mcpserver/result_resources.go`
- `internal/mcpserver/result_resources_test.go`

### Tests

- credential keys remain blocked;
- bearer and private-key strings remain blocked;
- `cancellationToken`, `tokenCount`, and `secretDoor` are not blocked;
- malformed JSON remains conservatively blocked.

### Completion gate

All result-store and MCP resource tests pass.

### Rollback

Revert predicate and fixtures.

---

## S8. Connector test harness restoration

### Problem

Editor tests were correctly isolated in a `TestAssemblies` asmdef to avoid production compilation stalls, but suites remain static `MenuItem` runners. They are not currently discovered by Unity Test Runner in a normal package fixture, and their menu entries are not registered.

### Required behavior

- Convert release-gating suites to NUnit `[Test]` or `[TestCase]` entry points.
- Keep expensive tests outside `HeraAgent.Editor` production assembly.
- Configure disposable fixtures with the package in `testables`.
- `hera-agent-unity test --mode EditMode --filter HeraAgent.Tests...` must discover a non-zero count.
- Menu wrappers, if retained, are maintainer conveniences, not release evidence.

### Initial release-gating suites

- catalog and contract;
- safety and profiles;
- approval and operation ledger;
- settings persistence and lock recovery;
- output-file policy and project identity;
- UI backend selection and UI authoring regressions.

### Completion gate

- Test Runner discovers non-zero tests in a marked disposable fixture.
- Exact counts and failures are retained.
- Production first-compile performance remains within the five-bucket gate.

### Rollback

Revert NUnit entry points and fixture `testables`; do not move tests back into production assembly.

---

## S9. Long-lived MCP transport and polling efficiency

### Status

Deferred until safety tasks and executable Connector tests pass.

### Candidate changes

- Permit keep-alive only for actions proven not to reload the domain; retain `Connection: close` for reload-capable or unknown operations.
- Back off catalog polling while stable and poll rapidly only while reloading or stale.
- Add action-specific Compact describe so multi-action tools do not return every action schema.

### Gate before implementation

Retain the historical Mono idle-channel warning reproduction. No keep-alive change is accepted without latency evidence and zero warning regression.

---

## S10. Rule/docs synchronization and release matrix

### Changes

- Record completed remediation behavior in `CLAUDE.md` without copying this full document into active rules.
- Update user-facing docs only where runtime behavior changed.
- Regenerate derived guides only when `AGENTS.md` changes.
- Bump the unreleased Connector version once for the completed Connector batch.
- Run the five supported Unity buckets:

```text
2022.3
2023.2
6000.0-6000.2
6000.3-6000.4
6000.5+
```

### Required gates

```text
gofmt -l .
go vet ./...
go build ./...
go test -count=1 ./...
golangci-lint run ./...
go run ./tools/validate-connector-package
go run ./tools/sync-agent-guides --check
git diff --check
```

For each Unity bucket:

- first UPM compile;
- exact-source Editor and TestRunner compile;
- non-zero EditMode test discovery;
- focused stability suites;
- UI backend selection and authoring tests;
- zero unexpected Console errors;
- fixture cleanup.

A missing bucket is `BLOCKED`, never PASS. Publication, tagging, and pushing remain separate user-authorized actions.

---

## Implementation order

```text
S0
 ├─ S1 ─ S2 ─ S5
 ├─ S3 ─ S4
 ├─ S6
 └─ S7
       ↓
      S8
       ↓
      S10

S9 starts only after S8 proves the safety baseline executable.
```

## Per-task evidence format

```text
Status:
Files changed:
Behavior implemented:
Tests executed:
Exact results:
Known limitations:
Rollback:
Next prerequisite:
```

## Final definition of done

The remediation is complete only when:

1. stale catalog requests fail before execution;
2. read-only calls do not create durable ledger files;
3. abandoned running records cannot live forever or bypass the byte cap;
4. Hera Settings recover from partial reads without losing last-good state;
5. stale config locks recover without stealing a live owner lock;
6. dispatched timeout and cancellation retain operation identity;
7. MCP capability transitions fail closed;
8. sensitive-result filtering blocks credentials without common false positives;
9. Connector tests execute through Unity Test Runner with a non-zero count;
10. all supported Unity buckets have truthful PASS or BLOCKED evidence;
11. the diff contains only remediation scope;
12. no commit, push, tag, release, or publication occurred implicitly.

---

## Implementation evidence — 2026-08-04

### S1 Connector catalog freshness gate

- **Status:** PASS
- **Implemented:** one domain-local lazy catalog snapshot shared by catalog listing and request validation; omitted-hash compatibility; `CATALOG_STALE` rejection before list, handler, approval, or ledger execution. This avoids a second full reflection/schema build on the first Typed CLI or MCP call.
- **Live evidence:** an all-zero stale hash returned `CATALOG_STALE` with the
  request hash, current hash, and domain epoch. Repeating the same read with the
  returned current hash succeeded.
- **Ledger evidence:** neither stale nor successful read-only probe created an
  operation file.

### S2 Operation ledger scope and lifecycle

- **Status:** PASS
- **Implemented:** read-only idempotent requests bypass durable journaling; prior-domain or
  over-one-hour `running` records become `outcome_unknown`; active current-domain
  work is retained; response-less stale/corrupt records can no longer evade the
  hard byte cap.
- **Tests:** replay, conflict, committed-response replay, non-idempotent unknown,
  retention, active-running retention, stale-running conversion, read-only
  bypass, and byte-cap fixtures passed in the 20-test Unity release gate.

### S3 Hera Settings last-known-good cache

- **Status:** PASS
- **Implemented:** parse-before-publish snapshot, successful-stamp cache,
  250 ms retry backoff, one warning per failed timestamp, last-known-good
  preservation, and missing-file default reset.
- **Tests:** valid load, malformed read preservation, same-timestamp retry,
  recovery, compiler-path/UI-system atomic publication, and missing-file reset
  passed in `ReleaseGateTests.AssetConfigPersistence`.

### S4 Cross-process asset-config lock recovery

- **Status:** PASS
- **Implemented in Go and C#:** versioned PID/timestamp/nonce lock record,
  two-minute stale threshold, confirmed-dead-owner recovery, conservative
  access-denied handling, byte/nonce recheck before deletion, and owner-only
  release.
- **Tests:** live owner not stolen, recent exited owner not stolen, stale exited
  owner recovered, malformed legacy lock recovered, and nonce mismatch protected.

### S5 HTTP continuation and timeout outcome contract

- **Status:** PASS
- **Implemented:** the three HTTP queue completion sources now run continuations
  asynchronously; non-idempotent dispatched timeouts preserve
  `OPERATION_OUTCOME_UNKNOWN`, operation ID, project, port, and underlying target
  diagnostic; CLI emits one compact envelope; MCP returns a structured tool
  error rather than an opaque protocol failure.
- **Tests:** focused client, CLI, and MCP tests plus all Go packages passed.

### S6 MCP runtime feature transition

- **Status:** PASS
- **Implemented:** adding or removing `task_bridge_v1` during a long-lived MCP
  session marks the runtime stale and requires process restart instead of
  changing advertised Tasks capability in place.
- **Tests:** both transition directions and normal catalog refresh passed.

### S7 Oversized-result sensitivity precision

- **Status:** PASS
- **Implemented:** exact generic credential keys and strong credential suffixes
  replace arbitrary substring matching; bearer/private-key content signatures
  and arbitrary-code withholding remain conservative.
- **Tests:** credentials remain blocked while `cancellationToken`, `tokenCount`,
  `secretDoor`, and `credentialStatus` remain spoolable.

### S8 Connector test harness restoration

- **Status:** PASS on Unity `6000.5.6f1`
- **Implemented:** `ReleaseGateTests` exposes 13 existing deterministic suites as
  NUnit tests without moving them back into the production assembly. The
  reusable `run-package-tests.ps1` temporarily enables package `testables`, runs
  EditMode tests, restores `manifest.json` byte-for-byte, and recompiles the
  restored fixture.
- **Discovered result:** `20 total / 20 passed / 0 failed / 0 skipped`, including
  13 release-gate wrappers and seven UI backend parameterized tests.
- **Lifecycle correction:** a live same-project Test Runner helper PID no longer
  converts the Editor-owned pending run to `TEST_RUN_INTERRUPTED`; only a
  confirmed-dead owner is classified as a previous Editor process.
- **Fixture restoration:** manifest SHA-256 before and after the run was
  `d7027d5eb027b50a40aef8c935e70f2ee985b54dc24efbcaef2b6004eef3fe96`.

### S9 long-lived MCP efficiency

- **Status:** DEFERRED intentionally
- No keep-alive, polling interval, or action-specific describe behavior changed.
  Safety and executable evidence were repaired first. Any transport optimization
  still requires latency evidence and the historical Mono idle-channel warning
  regression test.

### S10 verification and release boundary

- **Status:** PARTIAL / BLOCKED
- **PASS:** Unity `6000.5.6f1` exact-source Editor and TestRunner compile;
  current-source package compile; 20/20 package EditMode tests; zero unexpected
  Console errors after restoration.
- **PASS:** `gofmt -l .`, `go vet ./...`, `go build ./...`,
  `go test -count=1 ./...`, `golangci-lint run ./...`, Connector package
  validator, guide drift check, and `git diff --check`.
- **PASS exact-source compatibility matrix:** current `0.0.79` Editor and
  TestRunner sources compiled against existing Bee response files from
  `2022.3.62f2`, `2023.2.22f1`, `6000.0.35f1`, `6000.3.5f2`, and
  `6000.5.6f1` with zero compiler output.
- **RUNTIME PARTIAL / BLOCKED:** live package NUnit execution and manifest
  byte-restoration were completed on `6000.5.6f1` only. Equivalent live
  20-test package runs on the other four buckets have not been executed and are
  not claimed as PASS.
- **PASS race detector matrix:** Go's default temporary race executable path was
  blocked by Windows, so each critical package was compiled with `go test -race
  -c` to one repository-local temporary executable and run from its package
  working directory. `cmd`, `internal/assetconfig`, `internal/client`,
  `internal/mcpserver`, `internal/resultstore`, `internal/taskbridge`, and
  `internal/toolregistry` all passed with no race report; the executable was
  removed after every package.
- **Version:** Connector source is bumped once from `0.0.78` to unreleased
  `0.0.79`. No tag, publication, install, commit, or push occurred.

### Rollback

Revert the `0.0.79` stability batch, NUnit wrapper and verification script,
changelog/rule entry, and this evidence block together. The asset-config JSON
format is unchanged; version-1 lock files are safely interpreted as busy by old
clients; existing operation records remain readable.

