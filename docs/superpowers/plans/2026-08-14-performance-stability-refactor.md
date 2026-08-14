# Performance and Stability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` to implement this plan task-by-task. Subagents
> are intentionally not used in this workspace. Steps use checkbox syntax for
> tracking.

**Goal:** Remove confirmed stale-state and recovery defects, eliminate duplicated
hot-path work, and apply only performance changes justified by measurements.

**Architecture:** Preserve the Go CLI -> loopback HTTP -> serialized Unity main
thread -> reflection-discovered C# tool architecture, the 120-second command
lock, the file-bus task model, and default-compact MCP exposure. Prefer deleting
unsafe cache shortcuts and reusing Unity/Go platform facilities over adding new
layers.

**Tech Stack:** Go 1.25+, C# Unity Editor package, Newtonsoft JSON, Unity
`TypeCache`, NUnit Unity Test Framework, Go `testing` benchmarks.

## Global constraints

- Work only in `codex/unity-pipeline-parity`; do not commit unless requested.
- Preserve all pre-existing MCP token-diet changes in the dirty worktree.
- Do not modify UIJuiceGuide or any Hera Settings behavior, storage, window, or
  documentation.
- Do not change HTTP keep-alive, heartbeat/catalog observer cadence, the
  120-second command-lock timeout, or the HTTP/file-bus architecture.
- No new package dependency, public command, action, flag, schema, or response
  field unless a durable internal ledger record requires it.
- Tests precede production changes and must fail for the intended reason.
- Every C# lifecycle change receives an EditMode regression plus a live
  compile/console check against the exact-source connector.
- Measurement-gated work is skipped when the measured improvement does not
  justify the compatibility or complexity cost.

---

### Task 1: Remove stale exec reference metadata

**Files:**

- Modify: `AgentConnector/Editor/Tests/ExecCompileCacheTests.cs`
- Modify: `AgentConnector/Editor/Tools/ExecCompileCache.cs`

**Produces:** references recomputed once per Unity domain while hash-addressed
response files and compiled DLL caches remain reusable.

- [ ] Add `TestReferenceLocationsComeFromCurrentDomain` using an internal
  reference collector seam and a stale `refs-meta.json` fixture.
- [ ] Run `HeraAgent.Tests.ReleaseGateTests.ExecCompileCache` and confirm it
  fails because the disk metadata wins over the supplied current references.
- [ ] Delete `TryLoadRefsMeta`, `SaveRefsMeta`, and the `refs-meta.json` fast
  path; retain `CollectReferenceLocations`, reference hashing, `.rsp` reuse,
  compiler prewarm, disk DLL cache, and configured compiler resolution.
- [ ] Re-run the focused test and confirm compiler-setting tests remain green.

### Task 2: Resume an approval-verified received operation safely

**Files:**

- Modify: `AgentConnector/Editor/Core/OperationLedger.cs`
- Modify: `AgentConnector/Editor/Tests/ApprovalPolicyTests.cs`
- Modify: `AgentConnector/Editor/Tests/OperationLedgerTests.cs`

**Produces:** exact-binding `received` records resume without consuming the same
single-use approval token twice; `running` and unknown outcomes never re-run.

- [ ] Add `TestApprovedReceivedRetryDoesNotConsumeTokenAgain` by creating an
  approval-bound record in `received`, reconstructing the ledger, and retrying
  the exact operation.
- [ ] Add a mismatch test that changes risk class or idempotence and expects
  `OPERATION_CONFLICT` rather than execution.
- [ ] Run the focused approval/ledger release-gate tests and confirm the exact
  retry fails with `APPROVAL_ALREADY_USED`.
- [ ] Persist only `approval_verified`, never the token or token hash; set it
  only after successful verification and before the atomic `received` write.
- [ ] Resume without token verification only when state, project-scoped root,
  tool, action, arguments hash, risk class, and idempotence all match.
- [ ] Re-run focused tests including single-use, committed replay, prior-domain
  running, and non-idempotent unknown cases.

### Task 3: Keep HTTP listener health and advertised port consistent

**Files:**

- Modify: `AgentConnector/Editor/HttpServer.cs`
- Create: `AgentConnector/Editor/Tests/HttpServerLifecycleTests.cs`
- Create: `AgentConnector/Editor/Tests/HttpServerLifecycleTests.cs.meta`
- Modify: `AgentConnector/Editor/Tests/ReleaseGateTests.cs`

**Produces:** an unexpectedly stopped listener cannot leave a non-zero healthy
port in the heartbeat and is restarted only from the Unity main thread.

- [ ] Add lifecycle tests around an internal state-transition seam proving
  stop clears listener/CTS/port and expected shutdown does not request restart.
- [ ] Add an unexpected-loop-exit test proving it clears advertised state and
  schedules one bounded restart request.
- [ ] Run the focused test and confirm the current listener exit leaves the port
  non-zero with no restart request.
- [ ] Centralize listener teardown, distinguish expected cancellation from an
  unexpected completed loop, and schedule restart with `EditorUpdate.Once`.
- [ ] Re-run the focused tests and later verify live domain reload plus port
  collision recovery without changing port selection or heartbeat cadence.

### Task 4: Make the catalog cache an optional optimization

**Files:**

- Modify: `internal/toolregistry/registry.go`
- Modify: `internal/toolregistry/registry_test.go`

**Produces:** a valid live catalog is returned even when cache read, write, or
prune fails; invalid cached catalogs are never accepted.

- [ ] Add `TestRegistry_Load_uses_live_catalog_when_cache_is_corrupt` with a
  real corrupt cache entry and a valid fake live sender.
- [ ] Add `TestRegistry_Load_returns_live_catalog_when_cache_store_fails` using
  an unwritable cache root or the narrowest existing cache failure seam.
- [ ] Run the two tests and confirm current `Registry.Load` returns cache errors.
- [ ] Treat every cache load failure as a miss after rejecting its contents;
  compile and validate the live catalog exactly as today.
- [ ] Make cache store/prune best-effort after live validation; do not introduce
  automatic deletion, a new logger, or a fallback catalog format.
- [ ] Run `go test ./internal/toolregistry -count=1` and the race variant.

### Task 5: Prepare each batch request once

**Files:**

- Modify: `AgentConnector/Editor/CommandRouter.cs`
- Modify: `AgentConnector/Editor/Tests/ToolContractTests.cs`
- Modify: `AgentConnector/Editor/Tests/ApprovalPolicyTests.cs`

**Produces:** single and batch dispatch share one canonical handler/action/
normalized-parameters/safety preparation result; batches validate once per item.

- [ ] Add a validator-count seam used only by tests and a batch test expecting
  exactly one validation per item while preserving approval rejection.
- [ ] Run the focused tests and confirm current batch validation count is two.
- [ ] Introduce one private prepared-request value, have single dispatch prepare
  once, and have batch preflight retain prepared items for execution under the
  existing single lock.
- [ ] Lazily materialize sorted action names only for unknown-action errors and
  reuse the resolved default handler within preparation.
- [ ] Re-run contract, approval, fail-fast, atomic rollback, alias, unknown
  action, and release-gate tests.

### Task 6: Bound Package Manager watchers and isolate recovery records

**Files:**

- Modify: `AgentConnector/Editor/Core/PackageJobState.cs`
- Create: `AgentConnector/Editor/Tests/PackageJobStateTests.cs`
- Create: `AgentConnector/Editor/Tests/PackageJobStateTests.cs.meta`
- Modify: `AgentConnector/Editor/Tests/ReleaseGateTests.cs`

**Produces:** same-domain UPM hangs emit `PACKAGE_TIMEOUT`, detach their update
callback, and cannot prevent sibling pending jobs from recovering.

- [ ] Add a deterministic watcher-deadline test with an injected timestamp and
  a fake completion probe; no sleeps or real package mutation.
- [ ] Add a recovery-loop test with one malformed/throwing record followed by a
  valid record and assert the valid record is still attempted.
- [ ] Run the focused tests and confirm the watcher has no same-domain deadline
  and the outer catch aborts the recovery pass.
- [ ] Reuse `StaleJobMs` in the watcher, write the existing timeout envelope,
  detach on completion or deadline, and keep pending state when result writing
  fails.
- [ ] Move exception isolation to each pending record and log one stable Hera
  warning at the lifecycle boundary; do not add a retry subsystem.
- [ ] Re-run the focused tests and a disposable live package list/task status
  scenario without installing or removing packages.

### Task 7: Measure and reduce first catalog discovery work

**Files:**

- Modify only when justified: `AgentConnector/Editor/ToolDiscovery.cs`
- Modify: `AgentConnector/Editor/Tests/ToolDiscoveryTests.cs`

**Produces:** identical deterministic tool/action catalog with lower first-build
reflection work, or a recorded no-change decision when the gain is negligible.

- [ ] Measure first `ToolDiscovery.Invalidate` + catalog rebuild over repeated
  runs in the live Editor and record median/p95 plus tool/action counts.
- [ ] Add a regression that compares the existing discovered tool/action set to
  candidates returned by Unity `TypeCache.GetTypesWithAttribute<HeraToolAttribute>()`.
- [ ] If all supported fixtures match, replace all-assembly type scanning with
  `TypeCache`, retain deterministic sorting, and cache sorted action names in
  each tool entry.
- [ ] Re-run discovery, catalog hash, profile, safety, and release-gate tests;
  require identical 34-tool/132-action public surface and catalog hash.
- [ ] Re-measure median/p95; revert the production refactor if the improvement is
  not repeatable or any supported Unity bucket disagrees.

### Task 8: Measure and trim oversized MCP result allocations

**Files:**

- Modify: `internal/mcpserver/result_resources_test.go`
- Modify only when justified: `internal/mcpserver/result_resources.go`

**Produces:** unchanged sensitive-result and resource-spooling behavior with a
measured reduction in allocations for oversized safe JSON.

- [ ] Add `BenchmarkBoundedCommandResult` cases for 32 KiB, 1 MiB, and 10 MiB
  safe responses and record `-benchmem` baseline results.
- [ ] Add behavior tests proving inline, spooled, sensitive, arbitrary-code, and
  storage-failure results remain byte-shape compatible.
- [ ] Remove the repeated `commandResult(response)` construction/marshal by
  retaining one encoded inline candidate; make no transport change.
- [ ] Replace full `any` tree materialization for sensitivity checks only if a
  streaming `json.Decoder` key/value scan preserves every existing marker test
  and measurably lowers 10 MiB peak allocations.
- [ ] Run focused tests, `go test -bench BenchmarkBoundedCommandResult -benchmem
  ./internal/mcpserver`, and retain only changes with a repeatable improvement.

### Task 9: Full verification and documentation reconciliation

**Files:**

- Modify only if behavior/architecture text changed:
  `docs/ARCHITECTURE_REFINEMENT_ROADMAP.md`, `docs/DECISION_LEDGER.md`
- Do not modify generated agent guides unless their canonical source changes.

**Produces:** clean Go gates, exact-source Unity compile/runtime evidence, no
protected-scope drift, and an auditable final diff.

- [ ] Run CS0104 scan over changed C# files for `Object`, `PackageInfo`,
  `Random`, and `Debug`; resolve every ambiguity explicitly.
- [ ] Run `go clean -testcache`, formatter checks, `golangci-lint run ./...`,
  `golangci-lint fmt --diff`, `go test ./...`, and repository race gauntlet.
- [ ] Run Hera bootstrap, exact-source `editor refresh --compile`, console error
  read, focused EditMode suites, and the full release gate.
- [ ] Compare live catalog tool/action/hash and MCP payload metrics; public
  surface deltas must be zero.
- [ ] Measure pure LOC for every changed production/test file, inspect the full
  diff, run `git diff --check`, and prove UIJuiceGuide/Hera Settings paths are
  untouched.
- [ ] Update only architecture/decision documentation made stale by actual kept
  changes; do not bump versions or commit without a separate user request.
