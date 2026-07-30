# CODEX MCP Migration — Integrated Implement → Review → Gate Prompts

Each block below is a single Codex prompt. It enforces this sequence without requiring a second pasted review prompt:

```text
preflight → implementation → implementation gate → read-only review → issue list → confirmed fixes only → full gate rerun → progress evidence → stop
```

Do not use a later block until the previous block reports `Completion gate: PASS`.

---

## M2.4 — Package, test, profiler, and raw tools

```text
Implement and independently review ONLY work unit M2.4 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

This is one sequential task with two strictly separated passes:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing:

1. Read AGENTS.md and CLAUDE.md
2. Read the complete M2 section, the complete M2.4 section, and every contract it references
3. Read docs/MCP_MIGRATION_PROGRESS.md
4. Verify M0 and M1 are recorded as PASS and inspect evidence that M2.1 through M2.3 are complete
5. Inspect current code, handlers, schemas, declarations, tests, git branch, HEAD, status, and diff rather than relying only on document line numbers
6. Preserve unrelated changes

M2.4 scope is only:

- manage_packages
- run_tests
- profiler
- log
- exec
- menu execute

In PASS A, follow the exact M2.4 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.

For every scoped tool, implement and test:

- action contracts or a strict default contract
- only aliases actually accepted by the handler
- nested schema or SchemaJson for complex values
- required fields, alternatives, and conflicts per action
- practical output schema
- strict ContractMode only after tests pass
- no hidden handler-only aliases
- UNKNOWN_ACTION for explicit invalid actions
- minimum valid input
- every action
- missing required input
- wrong type
- unknown property
- unknown action
- alias normalization
- mutually exclusive targets
- output shape

Do not begin M3.
Do not commit, push, tag, publish, release, or install packages.

Before PASS B, run the full M2.4 implementation gate:

1. Narrow M2.4 tests
2. Every required schema validation check
3. go test -count=1 ./...
4. Required Unity compile, HeraAgent tests, console error check, and fixture cleanup when an appropriate Editor project is available
5. git diff --check

If the implementation gate cannot pass, do not begin review. Record only truthful BLOCKED evidence when appropriate and stop.

PASS B — FIRST REVIEW PASS IS READ-ONLY

Do not modify code, documentation, generated files, configuration, dependencies, or progress ledger during the first review pass.

Review the completed M2.4 implementation for:

- scope compliance
- locked architecture decisions
- action contract and strict-mode correctness
- declared alias versus handler behavior
- required, conflict, alternative, unknown-property, and unknown-action behavior
- output schema and output-shape coverage
- rule-document source-of-truth hierarchy
- generated-file drift
- backward compatibility
- tests and missing tests
- hidden manual duplication
- stale MCP prohibition language
- doctor --agent-rules output
- unrelated changes
- rollback viability

Write the complete issue list before changing anything.

For every issue provide:

- severity
- exact file and symbol
- violated requirement
- concrete consequence
- required correction

After the complete list is written, fix only confirmed M2.4 violations.

Then rerun the entire final M2.4 gate:

1. Narrow M2.4 tests
2. Every required schema validation check
3. go test -count=1 ./...
4. Required Unity compile, HeraAgent tests, console error check, and fixture cleanup when available
5. git diff --check and complete final diff review
6. go run ./tools/sync-agent-guides --check when rule-derived files changed or drift is suspected

Only after the final gate passes, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update the CLAUDE.md completed ledger only if the full M2 gate is actually complete.

Stop after reporting:

Milestone/work unit:
Baseline commit:
Files changed:
Contract changes:
Compatibility behavior:
Implementation-gate evidence:
Read-only review findings:
Confirmed corrections:
Final-gate evidence:
Unity evidence:
Rule documents updated:
Remaining risks:
Completion gate: PASS | FAIL | BLOCKED
Suggested next unit:
```

---

## M3 — Safety classification and profiles

```text
Implement and independently review ONLY milestone M3 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M3 section, every referenced safety and profile contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M2 are PASS, inspect current declarations, handlers, contract Core files, ToolDiscoveryTests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M3 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Classify each built-in from actual handler behavior, normalize legacy booleans compatibly, define parameter-dependent rules, classify every built-in tool and action, define profiles, exclude arbitrary-code operations from normal profiles, fail unspecified built-in safety, and test conservative MCP annotation mapping.
Do not copy the audit table blindly, do not begin M4, and do not commit, push, tag, publish, release, or install packages.

Run the full M3 implementation gate before review, including narrow safety, profile, catalog, and annotation tests, required schema validation, go test -count=1 ./..., required Unity validation when available, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review scope compliance, locked architecture, handler-derived safety classification, parameter-dependent rules, profile correctness, arbitrary-code exclusion, unspecified-safety failure behavior, annotation mapping, source-of-truth hierarchy, generated drift, compatibility, test gaps, hidden duplication, stale MCP prohibition language, doctor --agent-rules output, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M3 violations and rerun the entire M3 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update the CLAUDE.md completed ledger.
Stop and report the required section 32.4 completion format plus review findings and corrections, including whether unclassified built-ins, arbitrary-code normal-profile exposure, and profile-validation failures are all zero.
```

---

## M4 — Canonical catalog, hash, and domain epoch

```text
Implement and independently review ONLY milestone M4 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M4 section, every referenced catalog, canonical JSON, hash, Heartbeat, legacy-list, and client contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M3 are PASS, inspect current ToolContract models, registry, canonical JSON, ToolDiscovery, CommandRouter, Heartbeat, client types, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M4 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Add catalog list mode without a new Unity tool, emit the normalized envelope, use deterministic canonical hashing, add a non-reversible project fingerprint, add domain epoch and feature capabilities to Heartbeat, and preserve all old list mode byte shapes.
Never include timestamps, project paths, ports, PIDs, or domain epoch in catalog_hash.
Do not begin M5 and do not commit, push, tag, publish, release, or install packages.

Run the full M4 implementation gate before review, including catalog, hash, Heartbeat, and compatibility tests, schema and catalog validation, go test -count=1 ./..., required Unity validation, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review scope compliance, envelope shape, ordering, hash canonicalization, volatile-field exclusion, project fingerprint privacy, domain epoch, capability behavior, old list compatibility, test gaps, hidden duplicate catalog truth, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, doctor output, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M4 violations and rerun the entire M4 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update the CLAUDE.md completed ledger.
Stop and report the required completion format, review findings, corrections, and whether one HTTP request returns a validated 31-tool catalog with stable hash.
```

---

## M5 — Go registry, cache, and validation

```text
Implement and independently review ONLY milestone M5 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M5 section, every referenced provider, cache, schema, legacy fallback, dependency-review, and package-boundary contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M4 are PASS, inspect internal/toolregistry, internal/schema, internal/client, catalog validator, go.mod, go.sum, existing dependency records, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M5 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, dependency rules, and completion gate.
Add catalog-v1 and conservative legacy providers, concurrency-safe memory and disk cache, schema compilation by catalog hash, deterministic profiles, fixture and live tests, and complete dependency-review evidence.
Do not allow internal/toolregistry to import cmd, do not use @latest, do not begin M6, and do not commit, push, tag, publish, release, or install packages.

Run the full M5 implementation gate before review, including registry, cache, validation, fallback, profile, and integration tests, schema and catalog validation, go test -count=1 ./..., formatter, vet, build, lint, guide sync, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review package boundaries, provider correctness, Compact-only degradation, cache atomicity, bounds, privacy, corruption rejection, schema compilation, dependency pins and review evidence, test gaps, hidden manual duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M5 violations and rerun the entire M5 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update the CLAUDE.md completed ledger.
Stop and report the required completion format, review findings, corrections, and whether all strict schemas validate, cross-process cache works, invalid cache is rejected, old Connector degrades safely, and no cmd import exists.
```

---

## M6 — Typed CLI

```text
Implement and independently review ONLY milestone M6 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M6 section, every referenced Typed CLI, input precedence, GlobalConfig, legacy-normalization, help, and policy-skeleton contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M5 are PASS, inspect cmd/call, cmd/root, cmd/dispatch, global flags, legacy command paths, schema and registry APIs, help docs, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M6 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement call <tool>, exact input-source precedence and conflict handling, isolated immutable configuration, shared normalization where safe, validate-only and explain modes, compact-output preservation, and quoting-free stdin documentation.
Do not generate independent per-tool command trees, do not begin M7, and do not commit, push, tag, publish, release, or install packages.

Run the full M6 implementation gate before review, including every named M6 call and legacy compatibility test, schema and catalog validation, go test -count=1 ./..., formatter, vet, build, lint, help smoke tests, guide sync, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review source precedence, pre-HTTP validation, config isolation, legacy compatibility, output compatibility, help correctness, hidden per-tool duplication, test gaps, locked architecture, rule hierarchy, generated drift, stale prohibition language, doctor output, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M6 violations and rerun the entire M6 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update relevant rule or help documents.
Stop and report the required completion format, review findings, corrections, and whether Typed CLI works for all strict built-ins while existing CLI tests remain green.
```

---

## M7 — Connector operation ledger and safe retry

```text
Implement and independently review ONLY milestone M7 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M7 section, and every operation ID, metadata, context, ledger, AtomicFile, retry, capability, retention, and ultra-verification contract.
Read docs/MCP_MIGRATION_PROGRESS.md, verify M0 through M6 are PASS, inspect HttpServer, CommandRouter, OperationLedger, AtomicFile, Heartbeat, client transport and retry paths, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M7 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, ultra verification, and completion gate.
Add metadata, operation IDs, structured context, pre-execution ledger persistence, pre-response persistence, replay, conflict behavior, unknown outcomes, safe retries, capability gating, and tested retention cleanup.
Never generate a new operation ID during retry and never auto-reexecute a non-idempotent operation after unknown outcome.
Do not begin M8 and do not commit, push, tag, publish, release, or install packages.

Run the full M7 implementation gate before review, including all declared ledger and retry tests, schema, catalog, policy, and ledger validation, go test -count=1 ./..., disposable Unity response-loss or reload fixture verification proving one mutation, Unity validation, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review state transitions, persistence order, replay, argument conflicts, prior-domain handling, exactly-once safety, legacy capability behavior, retention, fixture validity, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M7 violations and rerun the entire M7 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence and update the CLAUDE.md completed ledger.
Stop and report the required completion format, review findings, corrections, and whether no tested non-idempotent operation executes twice under response loss or reload.
```

---

## M8 — stdio MCP skeleton

```text
Implement and independently review ONLY milestone M8 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M8 section, every referenced stdio, stdout-purity, official SDK, protocol, feature flag, shutdown, and dependency-review contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M7 are PASS, inspect cmd/mcp, internal/mcpserver, output behavior, flags, dependencies, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M8 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, dependency rules, and completion gate.
Pin the reviewed official SDK, implement the stdio-only experimental server, preserve protocol-only stdout, move diagnostics to stderr, support discovery and graceful shutdown, and expose no Unity tools.
Do not hand-roll JSON-RPC, do not use @latest, do not begin M9, and do not commit, push, tag, publish, release, or install packages.

Run the full M8 implementation gate before review, including all declared stdout, stderr, EOF, cancellation, transport, and feature-flag tests, protocol smoke testing, go test -count=1 ./..., formatter, vet, build, lint, dependency review, guide sync, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review SDK selection, protocol mapping, stdio enforcement, stdout purity, stderr behavior, update-notice suppression, no-Unity-tools boundary, feature flag behavior, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M8 violations and rerun the entire M8 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact SDK, protocol, limitation, and test evidence.
Stop and report the required completion format, review findings, corrections, and whether stdio discovery succeeds with zero non-protocol stdout bytes.
```

---

## M9 — Native Profile tool bridge

```text
Implement and independently review ONLY milestone M9 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M9 section, every referenced native profile, validation, policy, operation ID, result mapping, annotation, and startup contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M8 are PASS and confirm M7 ledger and policy prerequisites are actually active, then inspect mcpserver, policy, registry, client, tests, git branch, HEAD, status, and diff while preserving unrelated changes.

In PASS A, follow the exact M9 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Fetch catalog at startup, select fixed profile, register strict tools deterministically, validate and enforce policy before Unity, assign operation IDs, use shared internal client, map results and annotations, and keep profiles fixed.
Begin with core read-only behavior, add writes only after ledger and policy proof, do not expose exec or raw menu in normal profiles, and do not begin M10 or M11.
Do not commit, push, tag, publish, release, or install packages.

Run the full M9 implementation gate before review, including all declared profile, ordering, validation, error, mutation-ID, and exec-exclusion tests, schema, catalog, policy, and ledger validation, go test -count=1 ./..., disposable Unity integration where needed, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review startup catalog behavior, fixed profiles, registration order, validation order, policy enforcement, ID assignment, shared client use, result mapping, annotations, normal-profile code exclusion, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M9 violations and rerun the entire M9 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether every seed profile registers exactly its expected strict tool set.
```

---

## M10 — Compact and Full exposure

```text
Implement and independently review ONLY milestone M10 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M10 section, every Compact, Full-safe, Advanced, lexical ranking, dynamic custom tool, policy, operation, and documentation contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M9 are PASS, inspect compact tools, profile logic, registry visibility, policy paths, docs/MCP.md, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M10 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement deterministic tool_search, tool_describe, and tool_call, support legacy custom tools in Compact, Full-safe strict policy-allowed tools, and Advanced only with explicit arbitrary-code permission.
Do not use embeddings, do not begin M11 or M13, and do not commit, push, tag, publish, release, or install packages.

Run the full M10 implementation gate before review, including Compact, Full-safe, Advanced, custom-tool, policy, and operation-ID tests, schema and catalog validation, go test -count=1 ./..., MCP and documentation smoke tests, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review discovery completeness, ranking determinism, dynamic legacy handling, describe metadata, call validation, Full-safe visibility, Advanced gating, arbitrary-code exclusion, docs truthfulness, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, doctor output, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M10 violations and rerun the entire M10 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether Compact calls, Full-safe visibility, and Advanced startup protection pass.
```

---

## M11 — Approval and MRTR

```text
Implement and independently review ONLY milestone M11 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M11 section, every policy, approval binding, token, TTY, MCP fallback, and Connector revalidation contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M10 are PASS, inspect policy, middleware, call command, Connector policy paths, secret handling, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M11 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement deterministic preflight, MAC or signature-protected process-local or protected local approvals, required token binding and expiry, TTY and non-interactive CLI behavior, MCP negotiation and fallback, and Connector revalidation.
Do not store long-lived secrets in the repository, never silently downgrade approval, do not begin M12, and do not commit, push, tag, publish, release, or install packages.

Run the full M11 implementation gate before review, including all approval and MRTR tests, policy, schema, catalog, ledger, and Connector validation, go test -count=1 ./..., disposable Unity zero-mutation verification, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review preflight, token protection, secret handling, field binding, expiry, single use, TTY, non-interactive, MCP fallback, Connector revalidation, zero-mutation guarantees, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M11 violations and rerun the entire M11 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether no destructive benchmark operation mutates state before approval.
```

---

## M12 — Tasks bridge

```text
Implement and independently review ONLY milestone M12 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M12 section, every task, package job, test run, file-bus, cancellation, fallback, and durability contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M11 are PASS, inspect taskbridge, mcpserver, test and package commands, polling, durable file state, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M12 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement generic task state, package and test adapters, negotiated Tasks extension, blocking fallback, truthful cancellation, and adapter-restart recovery while retaining run_id, job_id, result files, pending records, and post-reload verification.
Do not generalize all commands into tasks, do not claim unsupported cancellation, do not begin M13 or M14, and do not commit, push, tag, publish, release, or install packages.

Run the full M12 implementation gate before review, including task, package, test, fallback, cancellation, restart-recovery, policy, schema, catalog, and task validation tests, go test -count=1 ./..., disposable Unity lifecycle verification, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review task-state behavior, adapter correctness, extension negotiation, fallback, durable state preservation, restart recovery, cancellation truthfulness, scope restraint, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M12 violations and rerun the entire M12 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether package or test task state survives whenever existing file-bus state survives.
```

---

## M13 — Catalog invalidation and list-changed

```text
Implement and independently review ONLY milestone M13 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M13 section, every domain epoch, invalidation, refetch, atomic swap, notification, stale call, and in-flight snapshot contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify prerequisite PASS evidence, inspect Heartbeat, registry and schema caches, MCP lifecycle, notification paths, custom-tool behavior, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M13 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Observe domain epoch, refetch and validate catalog, atomically replace registry and schemas, notify clients only when appropriate, reject stale or removed tools, and preserve in-flight snapshots.
Do not begin M14 and do not commit, push, tag, publish, release, or install packages.

Run the full M13 implementation gate before review, including every named invalidation and notification test, schema, catalog, cache, and protocol checks, go test -count=1 ./..., disposable Unity custom-tool reload verification, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review epoch handling, refetch sequencing, validation-before-swap, atomicity, notification correctness, unchanged-hash behavior, custom-tool reload behavior, stale and in-flight behavior, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M13 violations and rerun the entire M13 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether custom-tool add or remove appears without MCP process restart.
```

---

## M14 — Large result resources

```text
Implement and independently review ONLY milestone M14 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M14 section, every inline limit, spooling, resource, retrieval, retention, projection, and sensitive-result contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify prerequisite PASS evidence, inspect result mapping, spool paths, operation IDs, resources, retention, guards, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M14 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement inline cap, atomic spooling, handles, retrieval, retention, summary metadata, sensitive-result guard, and projection-first behavior.
Do not confuse HTTP transport size with model-context policy, do not spool credentials or arbitrary sensitive files, do not begin M15, and do not commit, push, tag, publish, release, or install packages.

Run the full M14 implementation gate before review, including result, resource, retention, projection, and guard tests, schema and catalog checks, go test -count=1 ./..., MCP resource smoke tests, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review cap behavior, atomicity, handle integrity, retrieval, retention, sensitive data handling, below-cap behavior, projection controls, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M14 violations and rerun the entire M14 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether oversized output stays out of inline content while remaining retrievable by handle.
```

---

## M15 — Telemetry and benchmark harness

```text
Implement and independently review ONLY milestone M15 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M15 section, every telemetry event, metric, benchmark, fixture, reproducibility, and safety contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify required implementation gates are PASS, inspect telemetry, recorder, JSONL, benchmark tools, fixtures, reports, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M15 target files, symbols, contracts, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Implement required event IDs, successful-task economics metrics, reproducible disposable fixtures, complete metric capture, and factual reports.
Never benchmark destructively on a production project, never claim unmeasured performance, do not promote MCP to default, do not begin M16, and do not commit, push, tag, publish, release, or install packages.

Run the full M15 implementation gate before review, including telemetry, JSONL, benchmark, fixture, accounting, reproducibility, and safety tests, go test -count=1 ./..., A-to-E disposable Unity benchmark, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review required event IDs, metric correctness, token accounting, duplicate-side-effect accounting, fixture isolation, reproducibility, report integrity, scope, test gaps, hidden duplication, locked architecture, rule hierarchy, generated drift, compatibility, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M15 violations and rerun the entire M15 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact benchmark evidence and limitations.
Stop and report the required completion format, review findings, corrections, and whether the A-to-E benchmark is reproducible on a disposable Unity fixture.
```

---

## M16 — Documentation, compatibility, and release hardening

```text
Implement and independently review ONLY milestone M16 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — IMPLEMENTATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED FIXES AND FINAL GATE

Before editing, read AGENTS.md, CLAUDE.md, the complete M16 section, every rule hierarchy, generated guide, documentation state, compatibility, versioning, and release-boundary contract, and docs/MCP_MIGRATION_PROGRESS.md.
Verify M0 through M15 are PASS, inspect actual behavior, all changed documentation, AGENTS.md, guides, CLAUDE.md, examples, flags, tests, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M16 required file list, subjects, compatibility rules, tests, stop conditions, rule-document impact, and completion gate.
Document only verified experimental behavior and include required install, configuration, profiles, Compact fallback, approval, operation IDs, task fallback, degraded mode, flags, stdout troubleshooting, versioning, and security boundaries.
Do not claim MCP is default, do not advertise untested capabilities, do not alter package versions, do not begin M17, and do not commit, push, tag, publish, release, or install packages.

Run the full M16 implementation gate before review, including executable documentation smoke tests, guide sync, schema and catalog checks, go test -count=1 ./..., formatter, vet, build, lint, and git diff --check.
If it cannot pass, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review every required document, canonical and generated rule hierarchy, examples, terminology, experimental wording, degraded-mode truthfulness, path leakage, default claims, unadvertised release behavior, test gaps, stale prohibition language, unrelated changes, and rollback viability.
For every issue list severity, exact file and symbol or heading, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M16 violations and rerun the entire M16 gate.
Only after final PASS, update docs/MCP_MIGRATION_PROGRESS.md with exact evidence.
Stop and report the required completion format, review findings, corrections, and whether no documentation claims MCP is default while all examples pass.
```

---

## M17 — Cross-verification and default decision

```text
Implement and independently review ONLY milestone M17 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Use this exact sequence:

PASS A — EVIDENCE COLLECTION AND CROSS-VERIFICATION
PASS B — READ-ONLY REVIEW, THEN CONFIRMED EVIDENCE OR RULE FIXES AND FINAL GATE

Before any changes, read AGENTS.md, CLAUDE.md, the complete M17 section, definition of done, test matrix, decision thresholds, feature flags, rollback rules, and every referenced contract.
Read docs/MCP_MIGRATION_PROGRESS.md, verify M0 through M16 are PASS, inspect code, tests, Connector and Unity evidence, benchmark fixtures and reports, conformance evidence, guides, docs, git branch, HEAD, status, and diff, and preserve unrelated changes.

In PASS A, follow the exact M17 required evidence, decision gates, compatibility rules, stop conditions, rule-document impact, and final constraints.
Run full Go verification, Connector compilation, Unity EditMode tests, disposable-fixture integration, MCP conformance, A-to-E benchmarks, approval tests, response-loss exactly-once verification, catalog reload verification, and agent-guide synchronization.
Measure thresholds rather than copying them as achieved facts.
Do not change the production default, do not make MCP primary, do not redesign the Connector, and do not commit, push, tag, publish, release, or install packages.

If evidence cannot be completed, record only truthful BLOCKED evidence when appropriate and stop.

PASS B is read-only until the complete issue list exists.
Review every definition-of-done item, evidence validity, threshold calculations, separation of data from recommendation, safety, exactly-once behavior, catalog reload, fixture cleanup, secret leakage, rule hierarchy, guide drift, stale prohibition language, unrelated changes, rollback viability, and absence of automatic default promotion.
For every issue list severity, exact file, symbol, report section, or missing evidence, violated requirement, concrete consequence, and required correction.

After the complete list is written, fix only confirmed M17 evidence, test, documentation, or rule-drift violations and rerun the full final M17 gate.
Only after final evidence is complete, update docs/MCP_MIGRATION_PROGRESS.md with exact measured outcomes, threshold calculations, limitations, and recommendation.
Stop with PASS, FAIL, or BLOCKED and do not change the production default.
```
