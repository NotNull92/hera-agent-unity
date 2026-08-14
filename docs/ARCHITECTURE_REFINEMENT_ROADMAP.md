# Architecture Refinement Roadmap

## Purpose

This roadmap improves the cleanliness and long-term maintainability of the completed CLI + optional MCP architecture without replacing its proven execution core.

The goal is not fewer files, more interfaces, or a visually fashionable rewrite. A change is accepted only when it creates one of these concrete improvements:

1. one contract has one authoritative source;
2. a compatibility path is visibly isolated from the strict path;
3. runtime states are explicit rather than encoded by unrelated booleans;
4. fixed model context or tool-result bytes are reduced without hiding safety data;
5. release evidence becomes repeatable across supported Unity buckets;
6. an optimization is backed by measurement and a regression test.

## Baseline

```text
Branch: main
HEAD: e7612612f685e3f2e7c3f55bbbef158b14cd5456
Connector source at roadmap start: 0.0.79, unreleased
Working tree: contains the uncommitted post-M17 stability remediation batch
```

This roadmap is layered on top of `docs/POST_MIGRATION_STABILITY_REMEDIATION.md`. It must preserve those changes and their evidence.

No commit, push, tag, release, package publication, software installation, credential change, or production-project mutation is authorized by this roadmap.

## Locked architecture boundaries

- Keep the existing Go CLI and established command syntax.
- Keep the optional Go stdio MCP adapter.
- Keep the localhost HTTP Unity Connector, `HttpServer`, `CommandRouter`, `ToolDiscovery`, heartbeat discovery, main-thread serialization, operation ledger, and file-bus recovery.
- Do not implement MCP directly in the Unity Connector.
- CLI and MCP must continue consuming the same normalized tool catalog.
- Keep Go-side and Connector-side validation because they protect different trust boundaries.
- Keep Profile MCP as the normal token-control path and Full exposure opt-in.
- Do not split methods merely to reduce line count. Extract only a compatibility boundary, protocol/state transition, side effect, or independently testable contract.
- Do not turn generated files into independent authoring sources.
- Do not place workstation-specific absolute paths in checked-in files.

## Status summary

| Task | Priority | Status | Depends on |
|---|---:|---|---|
| A0 Baseline and roadmap ledger | P0 | PASS | none |
| A1 Active rule-context diet | P1 | PASS | A0 |
| A2 Shared protocol manifest and generated constants | P1 | PASS | A0 |
| A3 Versioned single-command execution metadata | P1 | PASS | A2 |
| A4 Action-specific Compact describe | P1 | PASS | A0 |
| A5 Explicit MCP catalog lifecycle state machine | P1 | PASS | A0 |
| A6 Legacy CLI compatibility boundary isolation | P2 | PASS | A0 |
| A7 Repeatable Unity compatibility-matrix runner | P1 | PASS | A0 |
| A8 Release-gate and maintainer-menu ownership cleanup | P2 | PASS | A7 |
| A9 Catalog payload budget and description discipline | P2 | PASS | A4 |
| A10 Measured keep-alive and catalog-observer optimization | P2 | DEFERRED — measurement gate not opened | A5, A7, A9 |
| A11 Final architecture evidence and decision gate | P1 | PASS | A1-A10 |

---

## A0. Baseline and roadmap ledger

### Objective

Create one bounded ledger for this refinement wave and prove that pre-existing remediation changes are preserved.

### Gate

- Record branch, HEAD, initial status, and diff stat.
- Run the existing Go and Connector gates before structural edits.
- Every later task records files, behavior, tests, limitations, and rollback.

---

## A1. Active rule-context diet

### Problem

`CLAUDE.md` contains current design locks and a very large historical completion table. Loading all historical rows for ordinary work spends context without improving most decisions.

### Required behavior

- Keep current locks, development rules, verification rules, and release rules in `CLAUDE.md`.
- Move the completed historical table to `docs/DECISION_LEDGER.md` without losing text.
- Replace the table with a short pointer and a rule that the ledger is read only when a proposed change intersects an old decision.
- Update the rule-document hierarchy.
- Preserve `AGENTS.md` and generated downstream guides unless their canonical content actually changes.

### Gate

- Historical rows are byte-preserved apart from the new document heading and link context.
- `CLAUDE.md` becomes materially smaller.
- Repository searches for old locked decisions still find the ledger.
- Guide drift and Markdown checks pass.

---

## A2. Shared protocol manifest and generated constants

### Problem

Tool-catalog schema version, execution metadata version, feature names, and asset-config lock timing are cross-language contracts. Hand-written matching constants can drift.

### Required behavior

Create one checked-in authoring manifest:

```text
contracts/runtime-contracts.json
```

It owns only stable wire constants, not tool definitions or business logic:

- tool catalog schema/version feature;
- single-command execution protocol version/feature;
- asset-config lock record version and stale threshold.

A deterministic Go generator emits:

```text
internal/protocol/contracts_gen.go
AgentConnector/Editor/Core/ProtocolContracts.Generated.cs
```

Generated files are checked in and verified with `--check`. Existing runtime structs remain readable source code; this task does not generate the whole domain model.

### Gate

- Generator output is deterministic.
- `--check` fails on drift.
- Go and C# use generated constants.
- New C# source has a `.meta` file.
- Exact-source five-bucket compile passes.

---

## A3. Versioned single-command execution metadata

### Problem

The HTTP request has evolved through operation IDs, hashes, approvals, client kind, and catalog hash but has no explicit execution-contract version.

### Required behavior

- Add `meta.protocol_version` using the generated current value.
- A missing version remains accepted for old clients.
- The current version is accepted.
- An unknown non-empty version returns `EXECUTION_PROTOCOL_UNSUPPORTED` before catalog validation, approval, ledger, or handler work.
- Advertise the corresponding feature in the heartbeat.
- Batch remains on its existing contract until a separate batch-version task is justified. Do not pretend batch is versioned here.

### Gate

Go serialization tests, Connector validation tests, live current-version probe, and unsupported-version probe pass.

---

## A4. Action-specific Compact describe

### Problem

Compact discovery still loads every action schema when callers use name-only
describe before choosing an action.

### Required behavior

- `tool_search` returns action names and compact safety without input schemas.
- `name` only returns tool identity and compact action summaries.
- `name + action` returns tool identity plus the selected action's full contract, catalog hash, and domain epoch.
- Tools without actions retain their input/output schemas in the name-only result.
- Action aliases resolve to the canonical action name.
- Missing action returns `ACTION_NOT_FOUND` with compact available-action names.
- Arbitrary-code visibility rules remain unchanged.

### Gate

Tests prove compact name-only discovery, full action-specific contracts, and
schema preservation for tools without actions.

---

## A5. Explicit MCP catalog lifecycle state machine

### Problem

One `stale` boolean currently represents both a transient catalog refresh and a permanent restart-required capability transition.

### Required states

```text
ready
refreshing
restart_required
```

### Required behavior

- `ready`: calls acquire the current runtime.
- `refreshing`: tool calls return `CATALOG_STALE`; `tools/list` waits for the current refresh or its context.
- `restart_required`: calls fail immediately with `MCP_RESTART_REQUIRED`; they do not wait on a channel that can never close.
- Task capability transitions enter `restart_required` and retain the reason.
- Successful same-epoch or new-epoch refresh returns to `ready` only from `refreshing`, never from `restart_required`.

### Gate

Concurrency and transition tests cover all legal transitions, no lost wake-up, and no permanent `tools/list` hang.

---

## A6. Legacy CLI compatibility boundary isolation

### Problem

Strict typed calls, specialized commands, and dynamic legacy passthrough share one large routing method, so compatibility behavior is harder to identify during changes.

### Required behavior

- Keep the user-visible command syntax and exact parameter coercion.
- Move only the generic legacy tool adapter into one cohesive file/function.
- Keep specialized commands in the existing runner.
- Do not create one wrapper per command.
- Preserve approval preflight, operation metadata, `exec --file`/stdin behavior, and custom-tool passthrough.

### Gate

Existing command tests remain unchanged or become simpler; no help, output, or wire regression.

---

## A7. Repeatable Unity compatibility-matrix runner

### Problem

Five-bucket exact-source and package runtime verification currently requires hand-written workstation commands.

### Required behavior

Add a path-parameterized script that:

- accepts one project per supported bucket;
- runs exact-source Editor/TestRunner compile for every supplied bucket;
- optionally runs the package NUnit gate with byte-for-byte manifest restoration;
- reports `PASS`, `FAIL`, or `BLOCKED`, never silently converts a missing project into PASS;
- contains no machine-specific path;
- emits a concise machine-readable summary.

### Gate

Compile-only mode passes all five local buckets. Runtime mode is proven on at least one disposable fixture and restores it exactly.

---

## A8. Release-gate and maintainer-menu ownership cleanup

### Objective

Make NUnit the release evidence while retaining menu runners only as optional maintainer conveniences.

### Required behavior

- Document `ReleaseGateTests` as canonical automated evidence.
- Add a coverage assertion or manifest showing which legacy suites are included.
- Do not move tests back into the production assembly.
- Do not mechanically convert every utility menu into a separate NUnit method when one release-gate wrapper is clearer.

### Gate

A missing release suite fails the coverage check; package first-compile behavior remains intact.

---

## A9. Catalog payload budget and description discipline

### Objective

Prevent quiet schema growth and move deep detail behind action-specific discovery.

### Required behavior

- Add a reproducible report for catalog bytes by profile and the largest tool/action contracts.
- Record raw bytes separately from token estimates.
- Establish warning budgets, not arbitrary hard failures, until benchmark evidence exists.
- Shorten descriptions only when action/schema text already carries the same information.
- Keep safety and approval semantics visible.

### Gate

The report is deterministic and can compare a baseline file in CI or release review.

---

## A10. Measured keep-alive and catalog-observer optimization

### Status

Blocked until A5, A7, and A9 provide executable safety and measurement harnesses.

### Candidate experiments

- keep-alive only for proven non-reloading read-only actions;
- adaptive observer interval while the domain is stable;
- file notification with polling fallback;
- Compact action-specific describe usage rate and payload savings.

### Acceptance rule

No transport or polling behavior changes without p50/p95 evidence, the historical Mono idle-channel regression, domain-reload recovery tests, and zero safety regression.

---

## A11. Final architecture evidence and decision gate

### Required outputs

- completed task table;
- exact files and contracts changed;
- five-bucket compile evidence;
- available runtime matrix evidence;
- Go race and full test evidence;
- before/after active-rule bytes;
- before/after Compact action-describe bytes;
- deferred work and its entry conditions;
- rollback map;
- explicit statement that the execution core was preserved.

## Per-task evidence format

```text
Status:
Files changed:
Behavior implemented:
Tests executed:
Exact result:
Known limitations:
Rollback:
Next prerequisite:
```


---

## Implementation evidence — 2026-08-04

### A0 Baseline and preservation

- **Status:** PASS.
- **Baseline:** branch `main`, HEAD `e7612612f685e3f2e7c3f55bbbef158b14cd5456`.
- The pre-existing uncommitted `0.0.79` post-M17 stability batch remained in place. No commit, push, tag, release, publication, package installation, or production-project mutation occurred.

### A1 Active rule-context diet

- **Status:** PASS.
- `CLAUDE.md` changed from `92,278` bytes to `39,519` bytes before the concise refinement lock was added, reducing ordinary active context by `52,759` bytes.
- The `54,008`-byte completed-decision history moved to `docs/DECISION_LEDGER.md`; old decisions remain searchable but are loaded only when relevant.
- Current architecture, collaboration, verification, and release rules remain in `CLAUDE.md`.

### A2 Shared protocol manifest

- **Status:** PASS.
- `contracts/runtime-contracts.json` is the authoring source for stable cross-language wire constants.
- `tools/generate-runtime-contracts` deterministically generates Go and C# constants and supports `--check`.
- CI, release workflow, Connector package validation, and focused generator tests reject drift.
- Tool definitions, schemas, DTOs, and business logic remain hand-readable; only high-drift wire constants are generated.

### A3 Versioned single-command execution metadata

- **Status:** PASS.
- Current Go requests send `meta.protocol_version = hera.execution/1`.
- Missing version remains compatible. A live `hera.execution/999` request returned `EXECUTION_PROTOCOL_UNSUPPORTED` with request/current versions before execution.
- A live current-version `scene info` request succeeded, and heartbeat features include `execution_protocol_v1`.
- Batch remains explicitly outside this version contract.

### A4 Action-specific Compact describe

- **Status:** PASS.
- `tool_search` returns action names and compact safety without schemas.
- `tool_describe(name)` returns compact action summaries, while `tool_describe(name, action)` returns one canonical full action contract; aliases resolve and missing actions return `ACTION_NOT_FOUND`.
- A tool without actions keeps its schemas available from name-only describe.
- Measured catalog baseline shows `input/state` reduced from `27,926` bytes to `2,264` bytes, saving `25,662` bytes (`91.89%`). The next seven `input` actions save approximately `84–86%` each.

### A5 Explicit MCP catalog lifecycle

- **Status:** PASS.
- Catalog state is `ready`, `refreshing`, or `restart_required` rather than one overloaded boolean.
- Transient reloads return `CATALOG_STALE`; Tasks capability transitions return `MCP_RESTART_REQUIRED` immediately.
- Tests cover no infinite `tools/list` wait, no lost wake-up, structured tool-call failure, and inability of ordinary replacement to clear restart-required state.

### A6 Legacy CLI compatibility boundary

- **Status:** PASS.
- `cmd/legacy_tool.go` owns only dynamic custom-tool passthrough and legacy `exec` file/stdin/`--check` adaptation.
- Strict `call`, specialized commands, approval, and transport remain outside the compatibility adapter.
- Existing coercion and user-visible syntax are unchanged; focused compatibility tests pass.

### A7 Repeatable Unity compatibility matrix

- **Status:** PASS.
- `run-compatibility-matrix.ps1` accepts project paths as parameters, emits `hera.compatibility-matrix/1`, and reports missing inputs as `BLOCKED` rather than `PASS`.
- Current `0.0.80` Editor/TestRunner exact-source compilation passed `2022.3.62f2`, `2023.2.22f1`, `6000.0.35f1`, `6000.3.5f2`, and `6000.5.6f1` (`5/5`, no failures or blocked buckets).

### A8 Release-gate ownership

- **Status:** PASS.
- `ReleaseGateTests.CanonicalSuiteNames` is the explicit automated release-gate ownership manifest; an NUnit test detects wrapper drift.
- Menu runners remain maintainer conveniences and tests remain outside the production assembly.
- Disposable Unity `6000.5.6f1` package runtime discovered `21` tests and passed `21/21` with zero failures/skips.
- The fixture manifest was restored byte-for-byte to SHA-256 `d7027d5eb027b50a40aef8c935e70f2ee985b54dc24efbcaef2b6004eef3fe96`, with no `testables` field left behind.

### A9 Catalog payload budget

- **Status:** PASS.
- `tools/catalog-payload-report` separates raw input bytes, internal normalized catalog/profile bytes, actual MCP tool-definition bytes, profile/tool/action sizes, description characters, and clearly labelled rough token estimates.
- Current baseline: `185,339` normalized bytes, `31` tools, `75` actions, `8,123` tool-description characters.
- The report records the largest tools/profiles and action-specific describe savings. Budgets are warnings, not arbitrary release failures.
- The report now compares a live catalog with the reviewed baseline. `--fail-on-change` marks any canonical contract difference for review, while `--fail-on-growth` gates only positive tool/action/description/profile deltas. A built reporter exits `3`; `go run` surfaces that child status as its own non-zero result. The disposable package test path runs the contract comparison before EditMode tests.

### A10 Transport and observer optimization

- **Status:** DEFERRED BY DESIGN.
- HTTP keep-alive, polling cadence, and event-driven invalidation were not changed.
- Entry requires latency/token evidence, the historical Mono idle-channel reproduction, domain-reload recovery tests, and zero safety regression. The new lifecycle, matrix, and payload tools provide the prerequisites for that later experiment.


### A11 Final evidence and decision

- **Status:** PASS.
- Final standard gate passed: format, vet, build, all Go tests, `golangci-lint` with zero issues, generated contract drift, Connector package integrity, agent-guide drift, and `git diff --check`.
- Race-instrumented binaries passed all `18` packages with tests in the selected matrix; temporary binaries were deleted.
- Current Connector `0.0.80` exact-source compilation passed all five supported Unity buckets.
- Unity `6000.5.6f1` disposable package runtime passed `21/21` EditMode tests and restored its manifest exactly.
- The execution core was preserved: Go CLI, optional MCP adapter, localhost Connector, main-thread queue, heartbeat, operation ledger, and file-bus recovery were not replaced.
- A10 remains a deliberate experiment gate. No keep-alive or observer-cadence change is accepted without measurement and the historical Mono regression evidence.
- **Final decision:** architecture refinement is complete for this wave. The result is a cleaner contract/lifecycle/test boundary, not a new runtime architecture.
