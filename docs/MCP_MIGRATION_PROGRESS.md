# MCP Migration Progress

This ledger records milestone evidence for the migration specified by
[`CODEX_MCP_MIGRATION_IMPLEMENTATION.md`](CODEX_MCP_MIGRATION_IMPLEMENTATION.md).
The existing CLI remains the production default until the M17 completion and
benchmark gates pass.

## Status Summary

| Milestone | Status |
|---|---|
| M0 Migration authority, rules, and progress ledger | PASS |
| M1 Structural JSON Schema validity | PASS |
| M2 Action contracts and validation taxonomy | IN PROGRESS (M2.1, M2.2, M2.3 PASS) |
| M3 Safety classification and profiles | PENDING |
| M4 Canonical catalog, hash, and domain epoch | PENDING |
| M5 Go registry, cache, and validation | PENDING |
| M6 Typed CLI | PENDING |
| M7 Connector operation ledger and safe retry | PENDING |
| M8 stdio MCP skeleton | PENDING |
| M9 Native Profile tool bridge | PENDING |
| M10 Compact and Full exposure | PENDING |
| M11 Approval and MRTR | PENDING |
| M12 Tasks bridge | PENDING |
| M13 Catalog invalidation and list-changed | PENDING |
| M14 Large result resources | PENDING |
| M15 Telemetry and benchmark harness | PENDING |
| M16 Documentation, compatibility, and release hardening | PENDING |
| M17 Cross-verification and default decision | PENDING |

## M0 Migration Authority, Rules, and Progress Ledger

- **Status:** PASS
- **Commit baseline:** `f25ccd54f9d521f4cbff71440109842830744e44`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Replaced the obsolete blanket MCP prohibition with the authorized transitional architecture lock.
  - Documented the canonical and generated rule-document hierarchy.
  - Added deterministic agent-guide generation and drift checking.
  - Synchronized the distributable and embedded agent guides.
  - Corrected the `cmd/doctor.go` embed-source explanation.
  - Added this milestone ledger.
  - Post-implementation review rebased repository-relative links in nested Cursor and AntiGravity skill derivatives.
  - Replaced the remaining absolute MCP prohibition statements in the AntiGravity example and documentation index with truthful planned-state wording.
- **Tests:**
  - `go test -count=1 ./tools/sync-agent-guides` before implementation — expected RED, exit 1 because the generator symbols did not exist.
  - `go test -count=1 ./tools/sync-agent-guides -run 'TestGenerateGuides|TestSyncGuides'` — PASS.
  - UTF-8 boundary test before validation — expected RED, exit 1 with the pre-validation missing-heading error.
  - `go test -count=1 ./tools/sync-agent-guides` — final PASS (`ok`, 0.387s).
  - `go run ./tools/sync-agent-guides --check` — PASS, exit 0 with no drift.
  - `go test -count=1 ./cmd/...` — PASS (`ok`, 2.047s).
  - `go test -count=1 ./...` — final PASS; every Go package passed or reported no test files (`cmd` 0.926s, `internal/assetconfig` 0.333s, `internal/client` 0.119s, `internal/poll` 0.941s, `tools/build-unity-docs` 0.038s, `tools/sync-agent-guides` 0.060s).
  - `go run . doctor --agent-rules` source verification — PASS; Bootstrap, Quick Rules, and Pitfalls sections were all present.
  - `AGENT.md` and `cmd/AGENT.md` — byte-identical SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Generated guides — required Cursor and AntiGravity skill frontmatter present; no machine-specific absolute path found.
  - Post-review link regression test before correction — expected RED, exit 1 because both nested generated guides retained root-relative repository links.
  - `go test -count=1 ./tools/sync-agent-guides -run TestGenerateGuides_RebasesRepositoryLinksForNestedTargets` — PASS (`ok`, 0.379s).
  - `go test -count=1 ./tools/sync-agent-guides` — post-review PASS (`ok`, 0.394s).
  - `go test -count=1 ./cmd/...` — post-review PASS (`ok`, 0.924s).
  - `go test -count=1 ./...` — post-review PASS; every Go package passed or reported no test files (`cmd` 1.153s, `internal/assetconfig` 0.391s, `internal/client` 0.302s, `internal/poll` 0.983s, `tools/build-unity-docs` 0.051s, `tools/sync-agent-guides` 0.222s).
  - `go run ./tools/sync-agent-guides --check` — post-review PASS, exit 0 with no drift.
  - Locally built `doctor --agent-rules` — post-review PASS; exactly Bootstrap, Quick Rules, and Pitfalls were extracted, with no repository-development section, machine-specific path, Typed CLI command, or MCP command.
  - Nested generated-guide links — post-review PASS; every repository-relative link resolves from both generated target directories.
  - Obsolete-prohibition search — post-review PASS; the exact old `CLAUDE.md` lock and absolute “never as MCP”/“No JSON-RPC” statements have zero current matches. Remaining README matches describe the currently shipped CLI and absence of required MCP setup; `CLAUDE.md`, `docs/INDEX.md`, and `examples/rules/GEMINI.md` describe the adapter only as planned; implementation-plan/progress matches are normative or historical.
  - `AGENT.md` and `cmd/AGENT.md` remain byte-identical SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - `git diff --check` — post-review PASS, exit 0.
- **Known limitations:**
  - No Typed CLI or MCP runtime command exists in M0.
  - The CLI remains the production default.
  - An installed binary may contain the pre-M0 embedded guide; source verification must use a locally built binary.
- **Rollback procedure:**
  - Revert the M0 rule, generator, generated-guide, doctor-comment, test, progress-ledger, documentation-index, and example-rule changes together.
  - No Connector, package, installed binary, or runtime data migration needs rollback.
- **Next prerequisite:** M1 may start only under a separate instruction after re-reading this ledger and confirming the M0 PASS gate.

### Rule-document impact

- **CLAUDE.md lock change:** Replaced the blanket MCP prohibition with the authorized migration-in-progress lock.
- **AGENTS.md user rule change:** Added the repository-only canonical rule hierarchy under the co-development section.
- **AGENT.md regeneration:** Generated from the user-facing portion of `AGENTS.md`.
- **cmd/AGENT.md regeneration:** Generated byte-identically with `AGENT.md`.
- **Derived guide regeneration:** Cursor, Copilot, AntiGravity entry, workspace, and skill files generated deterministically; nested derivatives rebase repository-relative links to their target directories.
- **README / README.ko change:** None; M0 does not advertise unimplemented commands.
- **Docs change:** Added this progress ledger and clarified the current-versus-planned interface state in `docs/INDEX.md`; the implementation plan remains the authority.
- **Example rule change:** Clarified that AntiGravity uses the CLI today while the optional stdio adapter remains planned.

## M1 Structural JSON Schema Validity

- **Status:** PASS
- **Commit baseline:** `f25ccd54f9d521f4cbff71440109842830744e44`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Replaced primitive-only type fallback with deterministic recursive schema generation for current scalar, nullable, enum, array/list, string-key dictionary, nested DTO, and permissive legacy `object` parameters.
  - Removed property-level boolean `required` and retained object-level required-name arrays.
  - Added array `items`, nullable type unions, structured unsupported-type failures, and recursive ordinal schema canonicalization.
  - Preserved handler behavior, external `list --tool` fields, 31 runtime tool names, and 27 action names.
  - Kept unknown-property handling permissive; M2 was not started.
  - Added recursive runtime-schema, determinism, unsupported-type, nullable, compatibility-field, and name/action regression tests to `HeraAgent/Tests/ToolDiscovery`.
  - Post-review correction: made the baseline name/action regression tolerate supported custom `[HeraTool]` additions while still requiring all 31 built-in tool names and 27 built-in action names.
  - Post-review correction: expanded the in-repository Draft 2020-12 shape validator and added malformed-keyword fixtures so invalid `additionalProperties`, combinator branches, and empty `enum` arrays cannot produce a false-green schema gate.
  - Bumped the unreleased Connector package version from `0.0.64` to `0.0.65` because M1 changes the C# schema contract; no package was installed, published, or released.
- **Tests:**
  - Post-review local Connector/TestRunner merged compile using the active Unity Editor's response files, mechanically redirected to this repository's exact `AgentConnector/Editor` sources and a temporary output DLL — PASS, exit 0 with no compiler output.
  - Post-review isolated `ToolDiscoveryTests` execution from that local DLL — PASS, exit 0 (`OK`).
  - Final Unity evidence — `[ToolDiscoveryTests] ALL PASSED`; property-level boolean `required` count `0`; invalid runtime schema count `0`; malformed Draft 2020-12 keyword fixtures rejected; baseline tool names unchanged `true (31)`; action names unchanged `true (27)`.
  - Unity readiness/error check after the final pass — PASS; Editor state `ready`, `hera-agent-unity console --type error --lines 20 --since 102` returned `matched=0`, `returned=0`.
  - `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.908s, `internal/assetconfig` 0.320s, `internal/client` 0.126s, `internal/poll` 0.945s, `tools/build-unity-docs` 0.043s, `tools/sync-agent-guides` 0.068s).
  - `go vet ./...` — PASS, exit 0.
  - `go build ./...` — PASS, exit 0.
  - `golangci-lint run ./...` — PASS, `0 issues`.
  - `go run ./tools/sync-agent-guides --check` — PASS, exit 0 with no drift.
  - Locally built `hera-agent-unity doctor --agent-rules` — PASS; 21,913 bytes, 6 sections, no machine-specific absolute path, and no unimplemented `mcp` or `call` command advertisement.
  - Canonical/distributable guide hash check — PASS; `AGENT.md` and `cmd/AGENT.md` both SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Obsolete MCP-prohibition search — PASS; no current rule-document match. Remaining matches are historical migration records or normative implementation constraints in this progress ledger and the authoritative implementation plan.
  - `AgentConnector/package.json` parse/name/version check — PASS; version `0.0.65`.
  - `git diff --check` — PASS, exit 0.
- **Known limitations:**
  - The connected Editor project consumes Connector `0.0.64` from a Git URL, not this repository as a local package. Verification therefore compiled the exact local `0.0.65` sources with Unity's active response files and loaded a temporary merged validation assembly in memory; no installed package or project manifest was changed.
  - Free-form legacy `object` parameters remain permissive with `additionalProperties: true` until explicit schema fragments are introduced by a later work unit.
  - Strict unknown-property rejection, action contracts, safety classification, Typed CLI, and MCP runtime remain unimplemented.
- **Rollback procedure:**
  - Revert the M1 changes in `SchemaUtility.cs`, `ToolMetadata.cs`, `ToolDiscovery.cs`, and `ToolDiscoveryTests.cs`.
  - Restore `AgentConnector/package.json` from `0.0.65` to the pre-M1 Connector version `0.0.64`.
  - Remove this M1 ledger entry and the matching `CLAUDE.md` completed-ledger row.
  - No scene, asset, project manifest, installed package, CLI binary, or runtime data migration needs rollback.
- **Next prerequisite:** The M1 prerequisite remains satisfied. This review did not add or advance any M2.1 implementation; the pre-existing M2.1 worktree state was preserved unchanged.

### M1 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded M1 PASS and its explicit M2 boundary.
- **Generated agent guides:** Unchanged; M1 does not change user-facing usage rules.
- **README / README.ko change:** None; M1 adds no command and does not advertise Typed CLI or MCP.
- **Package version:** Connector manifest bumped to unreleased `0.0.65` for the C# schema-contract change; no package was released or installed.

## M2.1 Read and Query Tool Contracts

- **Status:** PASS
- **Commit baseline:** `165afd8265a95c910249090f2cacdcb22e3a6eb1`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Added the typed Connector contract model, registry, schema builder, validator, canonical JSON, profile, and safety foundations specified by the shared M2 contracts.
  - Extended `HeraToolAttribute`, `HeraActionAttribute`, and `ToolParameterAttribute` without removing legacy fields, and added class-level `HeraActionContractAttribute`.
  - Switched the nine M2.1 default read/query tools to strict contracts: `console`, `describe_shader`, `describe_type`, `find_gameobjects`, `find_method`, `game_feel`, `list_assemblies`, `ui_slop`, and `unity_docs`.
  - Added strict action contracts for `scene info`, `scene list`, and `menu list`; scene mutation actions and menu execution remain legacy.
  - Added alias normalization, scalar compatibility, required/type/format/enum validation, strict unknown-property rejection, deprecated-argument diagnostics, action alias normalization, and stable structured validation errors.
  - Changed explicit invalid declared actions to `UNKNOWN_ACTION`, normalized strict inputs before dispatch, and preserved default-handler compatibility for legacy positional commands.
  - Encoded the `find_gameobjects` projection conflict and `describe_shader` alternatives in both generated schemas and pre-dispatch validation while preserving the shipped `INVALID_PROJECTION` compatibility code.
  - Kept the external tool and action name sets unchanged at 31 tools and 27 actions.
  - Post-review correction: explicit JSON `null` is accepted only when `AllowNull` is declared; optional means omittable, not nullable.
  - Post-review correction: validated explicit schema fragments structurally, enforced M2.1 enum and pattern constraints, and added deterministic schema combinators for cross-field alternatives/conflicts.
  - Post-review correction: included class-level action contracts in discovery, kept typed action outputs inside the response envelope, added concrete `scene info/list` result schemas, and exercised every M2.1 runtime response envelope.
  - Bumped the unreleased Connector package version from `0.0.65` to `0.0.66` for the reviewed M2.1 C# contract change; no package was installed, published, or released.
- **Tests:**
  - Initial local Connector compile with the RED `ToolContractTests.cs` and no implementation — expected FAIL, exit 1: `ContractMode`, `ToolContractMode`, `Aliases`, and `Deprecated` were absent.
  - Final local Connector compile using the active Unity Editor's `HeraAgent.Editor.rsp`, mechanically redirected to this repository's sources and a temporary DLL — PASS, exit 0.
  - Final local TestRunner compile using `HeraAgent.TestRunner.rsp` against the temporary Connector reference assembly — PASS, exit 0.
  - `HeraAgent.Tests.ToolContractTests.RunTests` — PASS under `exec --strict`; minimum valid inputs, every M2.1 action, missing arguments, wrong types, unknown properties, unknown actions, aliases, deprecated diagnostics, enum/format constraints, invalid schema-fragment failure, mutually exclusive targets, alternatives, and output envelope shapes all passed.
  - Initial isolated M1 schema regression run with only the temporary Editor assembly — expected harness-only FAIL because `run_tests` is defined in the separate TestRunner assembly, leaving 30 discovered tools.
  - Final `HeraAgent.Tests.ToolDiscoveryTests.RunTests` with both temporary assemblies loaded — PASS; schema determinism and compatibility fields remained valid, with 31 tool names and 27 action names unchanged.
  - Final Unity console delta check: `hera-agent-unity console --type error --lines 20 --since 65 --stacktrace none` — PASS (`matched=0`, `returned=0`); Editor remained `ready` on Unity `6000.3.5f2`.
  - `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.945s, `internal/assetconfig` 0.386s, `internal/client` 0.145s, `internal/poll` 0.951s, `tools/build-unity-docs` 0.044s, `tools/sync-agent-guides` 0.091s).
  - `go run ./tools/sync-agent-guides --check` — PASS, exit 0 with no generated-rule drift.
  - `git diff --check` — PASS, exit 0.
  - Post-review RED probes reproduced all confirmed violations: explicit optional `null` passed strict validation; invalid `describe_type.members`, `console.type`, and `console.stacktrace` values passed; `{"type":"nonsense"}` was accepted as a schema fragment; cross-field constraints were absent from schemas; class-level action contracts were omitted from discovery; and `find_gameobjects` returned the incompatible `ARGUMENT_CONFLICT` code.
  - Post-review exact local Connector/TestRunner merged compile from the active Editor response file, redirected to this repository's current sources and temporary `HeraAgent.Editor.Final2.dll` — PASS, exit 0 with no compiler output.
  - Post-review `HeraAgent.Tests.ToolContractTests.RunTests` — PASS under `exec --strict` (`OK`), including explicit-null semantics, structural schema-fragment rejection, enum/pattern value constraints, cross-field schema/runtime constraints, class-level action discovery, runtime response envelopes for all nine default tools and three strict actions, and typed scene output schemas.
  - Post-review `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS under `exec --strict` (`OK`); deterministic schemas, 31 built-in tool names, and 27 built-in action names remained unchanged.
  - Post-review Unity console delta — PASS: `console --type error --lines 20 --since 342 --stacktrace none` returned `matched=0`, `returned=0`, `last_cursor=388`; Editor remained `ready` on Unity `6000.3.5f2`.
  - Post-review `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.900s, `internal/assetconfig` 0.329s, `internal/client` 0.087s, `internal/poll` 0.933s, `tools/build-unity-docs` 0.032s, `tools/sync-agent-guides` 0.059s).
  - Post-review `go vet ./...`, `go build ./...`, and `golangci-lint run ./...` — PASS; lint reported `0 issues`.
  - Post-review `go run ./tools/sync-agent-guides --check` — PASS with no generated-rule drift.
  - Post-review locally built `doctor --agent-rules` — PASS; 21,913 bytes, required Bootstrap/Quick Rules/Pitfalls sections present, with no machine-specific path or unimplemented `mcp`/`call` command claim.
  - Post-review `AGENT.md` / `cmd/AGENT.md` synchronization — PASS; both SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Post-review package manifest/meta checks — PASS; `com.notnull92.hera-agent-unity` version `0.0.66`, every new C# source has a `.meta`, and no duplicate Connector GUID exists.
  - Post-review obsolete-prohibition search — PASS; all remaining matches are normative history in the authoritative implementation plan or this progress ledger, with no contradictory current rule-document language.
- **Known limitations:**
  - M2 overall remains in progress. M2.2, M2.3, and M2.4 tools remain legacy and were not migrated.
  - The connected Editor project consumes the Connector from a Git package cache, not this repository as a local package. Verification compiled the exact local `0.0.66` sources with the active Unity response files and loaded a temporary assembly in memory; no project manifest or installed package was changed.
  - Safety classification and profile enforcement remain M3 work. Canonical catalog hashing and domain epochs remain M4 work.
  - Typed CLI and MCP runtime commands remain unimplemented; the existing CLI remains the production default.
- **Rollback procedure:**
  - Revert the M2.1 attribute extensions, contract core files and their `.meta` files, strict read/query annotations, action parameter DTOs, router/discovery integration, response diagnostics, validation error normalization, and `ToolContractTests`.
  - Restore `AgentConnector/package.json` from `0.0.66` to the pre-M2.1 Connector version `0.0.65`, then remove this M2.1 ledger entry plus its `CLAUDE.md` status updates.
  - No scene, asset, project manifest, installed package, CLI binary, or runtime data migration needs rollback.
- **Next prerequisite:** M2.2 may start only under a separate instruction after confirming this M2.1 PASS gate. Do not infer authorization to begin it from this entry.

### M2.1 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded M2.1 PASS and the explicit M2.2 boundary.
- **Generated agent guides:** Unchanged; M2.1 does not change user-facing usage rules.
- **README / README.ko change:** None; M2.1 adds no CLI or MCP command and does not advertise unavailable functionality.
- **Package version:** Connector manifest bumped to unreleased `0.0.66`; no package was released or installed.

## M2.2 Scene and Object Mutation Tool Contracts

- **Status:** PASS
- **Commit baseline:** `165afd8265a95c910249090f2cacdcb22e3a6eb1`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Added strict action contracts for `scene load/save/close` while preserving the M2.1 `scene info/list` contracts.
  - Switched `manage_gameobject`, `manage_components`, `manage_editor`, `screenshot`, and `input` to strict typed contracts.
  - Declared action-specific required fields, actual handler aliases, enums, numeric limits, mutually exclusive targets, complex vector/object/array schemas, and typed output schemas where practical.
  - Removed the unsupported `manage_editor refresh` schema claim; the handler and public action set remain the source of truth.
  - Extended strict validation for `JToken`/`JObject`/`JArray` values and recursively validated nested object, array, range, required, additional-property, and combinator constraints from `SchemaJson`.
  - Preserved explicit invalid-action normalization as `UNKNOWN_ACTION` and retained existing handler behavior and response envelopes.
  - Kept the 31 built-in tool names unchanged. Declared action contracts now cover all 43 currently shipped handlers, adding the 16 M2.2 actions without adding commands or handlers.
  - Post-review correction: restored the existing boolean scalar compatibility forms (`true/false`, `yes/no`, `on/off`, and `1/0`) before strict validation.
  - Post-review correction: added declarative at-least-one and conditional-required argument groups so `manage_components` requires `type` for GameObject targeting and `screenshot --isolated` requires a target while preserving accepted redundant component metadata.
  - Post-review correction: made `AllowNull` add a `null` branch to complex `oneOf`/`anyOf` schemas, keeping the published `set_parent.parent` schema aligned with runtime validation.
  - Post-review correction: expanded M2.2 minimum-input, required-condition, alias, scalar-compatibility, nullable-schema, and output-envelope regression coverage.
  - Bumped the unreleased Connector package version from `0.0.66` to `0.0.67`; no package was installed, published, tagged, or released.
- **Tests:**
  - Initial local Connector compile with the seven new M2.2 contract tests but before the M2.2 annotations/validator changes — PASS; the RED test execution then failed all seven expected cases: strict coverage, every action contract, validation failures, aliases, mutually exclusive targets, complex schema values, and output schemas.
  - Final local Connector compile using the Unity `6000.3.5f2` response file, redirected to this repository's exact sources and temporary `HeraAgent.Editor.M22.Green2.dll`/reference outputs — PASS, exit 0 with no compiler output.
  - Final local TestRunner compile against the temporary Connector reference assembly — PASS, exit 0.
  - `HeraAgent.Tests.ToolContractTests.RunTests` — PASS; all M2.1 regressions and all seven M2.2 tests passed, including minimum valid input, every M2.2 action, missing required values, wrong types, unknown properties/actions, aliases, mutually exclusive targets, complex values, and output envelope shapes.
  - `HeraAgent.Tests.ToolDiscoveryTests.RunTests` in a clean Unity domain with both temporary assemblies loaded before discovery — PASS; Draft 2020-12 shape validation, malformed-keyword rejection, determinism, compatibility fields, 31 built-in tool names, and all 43 declared action contracts passed.
  - Initial non-Play-Mode `HeraAgent.Tests.InputQaTests.RunTests` — PASS for all input caps (`hold_ms`, `settle_frames`, drag steps, click count, and max results); interaction checks were skipped because the Editor was not yet in Play Mode.
  - Follow-up Play Mode `HeraAgent.Tests.InputQaTests.RunTests` — PASS twice on Unity `6000.3.5f2`; click success, pointer down/up/click counts, submit, scroll, two-step drag with begin/end callbacks, click/drag target destruction reporting, and blocked-target `INPUT_TARGET_BLOCKED` behavior all passed. `HeraInputQaRoot` cleanup returned zero remaining objects, and the Play Mode console error delta was zero.
  - The combined clean-domain verification request exceeded the CLI's HTTP request deadline while Unity continued executing; the Editor log subsequently recorded `[ToolContractTests] ALL PASSED`, `[ToolDiscoveryTests] ALL PASSED`, and `[InputQaTests] LIMITS PASSED`, with no error entries. A separate strict `ToolContractTests` run completed with exit 0.
  - Final Unity readiness/error check — PASS; Inventoria was `ready` on Unity `6000.3.5f2`, and `console --type error --lines 20 --stacktrace none` returned `matched=0`, `returned=0`.
  - `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.939s, `internal/assetconfig` 0.481s, `internal/client` 0.158s, `internal/poll` 0.970s, `tools/build-unity-docs` 0.039s, `tools/sync-agent-guides` 0.103s).
  - `go vet ./...` and `go build ./...` — PASS, exit 0.
  - `golangci-lint run ./...` — PASS, `0 issues`.
  - `go run ./tools/sync-agent-guides --check` — PASS, exit 0 with no generated-rule drift.
  - Locally built `doctor --agent-rules` — PASS; 21,912 bytes, 6 sections, required Bootstrap/Quick Rules/Pitfalls sections present, with no machine-specific path or unimplemented `mcp`/`call` command claim.
  - `AGENT.md` / `cmd/AGENT.md` synchronization — PASS; both SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Connector manifest/meta check — PASS; `com.notnull92.hera-agent-unity` version `0.0.67`, with all eight contract sources paired with `.meta` files.
  - `git diff --check` — PASS, exit 0.
  - Post-review read-only probes against `HeraAgent.Editor.M22.Green2.dll` reproduced every confirmed contract defect: legacy boolean forms were rejected; GameObject-targeted `manage_components get` without `type` and isolated screenshot without a target passed validation; and `set_parent.parent` omitted `null` from its published schema.
  - Post-review exact local Connector and TestRunner compilation from the active Unity `6000.3.5f2` response files into temporary `HeraAgent.Editor.M22.ReviewFix1.dll` and `HeraAgent.TestRunner.M22.ReviewFix1.dll` — PASS, exit 0.
  - Post-review `HeraAgent.Tests.ToolContractTests.RunTests` — PASS under `exec --strict` (`OK`), including the five confirmed review corrections and all M2.1/M2.2 regressions.
  - Post-review `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS under `exec --strict` (`OK`); Draft 2020-12 shape checks, schema determinism, 31 built-in names, and 43 declared action contracts remained green.
  - Post-review Play Mode `HeraAgent.Tests.InputQaTests.RunTests` — PASS under `exec --strict` (`OK`) on Unity `6000.3.5f2`; EventSystem click, pointer, submit, scroll, drag, destruction, blocking, and input-limit checks completed. After Play Mode exit, `HeraInputQaRoot` count was `0`, the active scene was clean, and the error console returned `matched=0`, `returned=0`.
  - Post-review `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.899s, `internal/assetconfig` 0.309s, `internal/client` 0.129s, `internal/poll` 0.947s, `tools/build-unity-docs` 0.034s, `tools/sync-agent-guides` 0.069s).
  - Post-review `go vet ./...`, `go build ./...`, and `golangci-lint run ./...` — PASS; lint reported `0 issues`.
  - Post-review `go run ./tools/sync-agent-guides --check` — PASS with no generated-rule drift.
  - Post-review locally built `doctor --agent-rules` — PASS; 21,913 bytes, required Bootstrap/Quick Rules/Pitfalls sections present, with no machine-specific path or unimplemented `mcp`/`call` command claim.
  - Post-review manifest/meta validation — PASS; package version remains unreleased `0.0.67`, all seven `ToolContract*.cs` sources have sibling `.meta` files, and duplicate Connector meta GUID count is zero.
  - Post-review obsolete-prohibition search — PASS; the only current rule-document match is the locked requirement not to implement MCP inside the Unity Connector. README matches describe the currently shipped CLI and lack of required MCP setup; implementation-plan/progress matches are normative or historical.
  - Post-review `git diff --check` — PASS, exit 0.
- **Known limitations:**
  - M2 overall remains in progress. M2.3 asset/UI tools and M2.4 package/test/profiler/raw tools remain legacy and were not modified by M2.2.
  - The connected Editor project consumes a cached Connector package rather than this repository as a local package. Verification therefore compiled and loaded the exact local sources into a temporary in-memory assembly; no project manifest, installed package, scene, or asset was changed.
  - Play Mode verification covers Unity EventSystem input synthesis, not physical OS/window clicks. No physical-click claim is made.
  - Safety classification/profile enforcement remains M3, catalog hashing/domain epochs remain M4, and Typed CLI/MCP runtime commands remain unimplemented. The existing CLI remains the production default.
- **Rollback procedure:**
  - Revert the M2.2 typed DTO/action annotations in `ManageScene`, `ManageGameObject`, `ManageComponents`, `ManageEditor`, `EditorScreenshot`, and `Input`.
  - Revert the M2.2 recursive complex-value/range validation additions, argument-group modes, boolean compatibility normalization, complex-schema null handling, and the M2.2 test expansions in `HeraToolAttribute`, `ToolContractSchemaBuilder`, `ToolContractValidator`, `ToolContractTests`, and `ToolDiscoveryTests`.
  - Restore `AgentConnector/package.json` from `0.0.67` to the pre-M2.2 Connector version `0.0.66`, then remove this M2.2 ledger entry and the matching `CLAUDE.md` status/ledger changes.
  - No Go runtime, MCP runtime, scene, asset, project manifest, installed package, or persistent data migration needs rollback.
- **Next prerequisite:** M2.3 may start only under a separate instruction after confirming this M2.2 PASS gate. Do not infer authorization to begin it from this entry.

### M2.2 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded M2.2 PASS and the explicit M2.3 boundary without weakening any architecture lock.
- **Generated agent guides:** Unchanged by M2.2; the deterministic synchronization check remains green.
- **README / README.ko change:** None; M2.2 adds no CLI or MCP command and does not advertise unavailable Typed CLI or MCP functionality.
- **Package version:** Connector manifest bumped to unreleased `0.0.67`; no package was installed, published, tagged, or released.

## M2.3 Asset and UI Tool Contracts

- **Status:** PASS
- **Commit baseline:** `529166e49159209bd028f2e46a9e49292a8948ef`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Switched `manage_assets`, `manage_asset_import`, `manage_material`, `manage_prefab`, `manage_animation`, `manage_ui`, `ui_doc`, `reserialize`, `refresh_unity`, and `detect_assets` to strict contracts.
  - Added 31 strict M2.3 action contracts across the seven action tools, bringing the unchanged 31 built-in tool names to 70 declared canonical action contracts.
  - Declared action-specific required fields, actual `ui_doc gensprite` and `reserialize path` aliases, mutually exclusive prefab/UI/export targets, conditional anchor requirements, and at-least-one payload requirements.
  - Modeled asset property values, animation keyframes and conditions, UI vectors, import manifests, and procedural sprite specifications with recursive JSON Schema fragments.
  - Added typed output schemas where one runtime shape is stable. Kept the dual-backend `manage_ui create` and `ui_doc apply` outputs plus dynamic `ui_doc export` generic because uGUI and UI Toolkit intentionally return different data shapes.
  - Preserved the variadic positional `reserialize` form and normalized its singular `path` alias to the canonical `paths` array before strict validation.
  - Preserved explicit invalid-action normalization as `UNKNOWN_ACTION`; no CLI command, Unity handler, MCP runtime, Typed CLI, package install, or release behavior was added.
  - Bumped the unreleased Connector package version from `0.0.67` to `0.0.68`.
- **Tests:**
  - Initial exact-source Connector compile with the seven new M2.3 contract tests and before M2.3 annotations — PASS; executing the RED assembly failed all seven expected M2.3 cases.
  - Final merged Connector/TestRunner compile using the active Unity `6000.3.5f2` response file redirected to this repository's exact sources — PASS, exit 0 with no compiler output.
  - `HeraAgent.Tests.ToolContractTests.RunTests` — PASS, including all M2.1/M2.2 regressions plus M2.3 strict coverage, all 31 M2.3 actions, missing required values, wrong types, unknown properties/actions, aliases and positional compatibility, mutually exclusive targets, complex values, and output schemas.
  - `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS; Draft 2020-12 schema validation, malformed-keyword rejection, canonical determinism, external response compatibility, 31 built-in tool names, and 70 declared canonical action contracts passed.
  - The in-memory Unity reflection requests exceeded the installed CLI's HTTP deadline while the Editor continued running. The final Editor log recorded `[ToolContractTests] ALL PASSED`, `[ToolDiscoveryTests] ALL PASSED`, and `M23_FINAL3_DONE` for the exact-source assembly; the Editor then reported `ready`.
  - Exact-source Unity smoke on Inventoria (`6000.3.5f2`) — `manage_assets find` returned a successful compact one-result envelope. The connected project is configured for UI Toolkit, so `manage_ui get_rect` returned the expected `UITK_ACTION_UNSUPPORTED` compatibility response rather than mutating the project.
  - `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 1.001s, `internal/assetconfig` 0.680s, `internal/client` 0.249s, `internal/poll` 1.050s, `tools/build-unity-docs` 0.132s, `tools/sync-agent-guides` 0.193s).
  - `go run ./tools/sync-agent-guides --check` — PASS, exit 0 with no generated-rule drift.
  - `git diff --check` — PASS, exit 0.
  - Post-review RED exact-source run — PASS as a regression test: `TestM23ComplexSchemaValues` and `TestM23OutputSchemas` failed against the reviewed implementation before corrections.
  - Post-review corrections — fixed `ManageAssets.CreateResult.Applied` to match the runtime string array; represented `ui_doc import` arrays with typed item DTOs; kept dual-backend `ui_doc apply` generic; widened `manage_ui` vector validation to the handler-compatible signed/scientific form; removed unused default-tool result DTOs; and added direct coverage for every M2.3 unknown action, the actual `ui_doc gensprite` dispatch alias, vector compatibility, and corrected output shapes.
  - Post-review exact-source `HeraAgent.Tests.ToolContractTests.RunTests` — PASS; all M2.1/M2.2 regressions and all M2.3 contract tests passed.
  - Post-review exact-source `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS; 31 tool names, 70 canonical action contracts, Draft 2020-12 validation, determinism, and external response compatibility passed.
  - Post-review exact-source Unity smoke — PASS; `manage_assets find` with `type=Texture2D` and `limit=1` returned `success=true`, one asset, and the expected compact envelope. Unity `6000.3.5f2` returned to `ready`.
  - Post-review narrow Go tests — PASS (`cmd` 0.923s, `internal/assetconfig` 0.321s, `internal/client` 0.127s, `internal/poll` 0.935s, `tools/build-unity-docs` 0.035s, `tools/sync-agent-guides` 0.053s).
  - Post-review `go test -count=1 ./...` — PASS; all packages passed or reported no test files (`cmd` 0.881s, `internal/assetconfig` 0.301s, `internal/client` 0.113s, `internal/poll` 0.931s, `tools/build-unity-docs` 0.028s, `tools/sync-agent-guides` 0.046s).
  - Post-review `go vet ./...`, `go build ./...`, and `golangci-lint run` — PASS; lint reported `0 issues`.
  - Post-review locally built `doctor --agent-rules` — PASS; 21,912 captured bytes (the shell removed the final newline), required Bootstrap/Quick Rules/Pitfalls sections present, and no machine-specific absolute path.
  - Post-review generated-guide check — PASS; `go run ./tools/sync-agent-guides --check` exited 0 and `AGENT.md` / `cmd/AGENT.md` remain byte-identical SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Post-review obsolete-prohibition search — PASS; current rule matches are the locked prohibition against implementing MCP inside the Unity Connector and the truthful prohibition against advertising unimplemented commands. Other matches are historical migration records or normative implementation-plan constraints.
  - Post-review scope/diff check — PASS; all 16 modified files are within the M2.3 implementation, test, package-version, locked-status, or progress-ledger scope, with no unrelated user change identified. `git diff --check` exited 0.
- **Known limitations:**
  - M2 overall remains in progress. M2.4 package/test/profiler/raw tools remain legacy and were not modified by M2.3.
  - The connected Editor consumes a cached Connector package rather than this repository as a local package. Verification therefore compiled and loaded the exact local sources into temporary in-memory assemblies; no package or project manifest was installed or changed.
  - The available Editor project uses UI Toolkit, so the uGUI RectTransform smoke branch was not available without changing project configuration. Contract and schema coverage for `manage_ui` and `ui_doc` passed in the exact-source Unity assembly.
  - Safety/profile enforcement remains M3, catalog hashing/domain epochs remain M4, and Typed CLI/MCP runtime commands remain unimplemented. The existing CLI remains the production default.
- **Rollback procedure:**
  - Revert the M2.3 action DTOs, action annotations, argument groups, and strict modes in the ten M2.3 tool files.
  - Revert the array alias/variadic positional normalization in `ToolContractValidator` and the M2.3 additions in `ToolContractTests` and `ToolDiscoveryTests`.
  - Restore `AgentConnector/package.json` from `0.0.68` to `0.0.67`, then remove this M2.3 ledger entry and the matching `CLAUDE.md` status/ledger changes.
  - No Go runtime, MCP runtime, scene, asset, project manifest, installed package, or persistent data migration needs rollback.
- **Next prerequisite:** M2.4 may start only under a separate instruction after confirming this M2.3 PASS gate. Do not infer authorization to begin it from this entry.

### M2.3 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded M2.3 PASS and the explicit M2.4 boundary without weakening any architecture lock.
- **Generated agent guides:** Unchanged by M2.3; the deterministic synchronization check remains green.
- **README / README.ko change:** None; M2.3 adds no CLI or MCP command and does not advertise unavailable Typed CLI or MCP functionality.
- **Package version:** Connector manifest bumped to unreleased `0.0.68`; no package was installed, published, tagged, or released.
