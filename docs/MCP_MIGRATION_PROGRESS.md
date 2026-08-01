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
| M2 Action contracts and validation taxonomy | PASS (M2.1-M2.4 PASS) |
| M3 Safety classification and profiles | PASS |
| M4 Canonical catalog, hash, and domain epoch | PASS |
| M5 Go registry, cache, and validation | PASS |
| M6 Typed CLI | PASS |
| M7 Connector operation ledger and safe retry | PASS |
| M8 stdio MCP skeleton | PASS |
| M9 Native Profile tool bridge | PASS |
| M10 Compact and Full exposure | PASS |
| M11 Approval and MRTR | PASS |
| M12 Tasks bridge | PASS |
| M13 Catalog invalidation and list-changed | PASS |
| M14 Large result resources | PASS |
| M15 Telemetry and benchmark harness | PASS |
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

## M2.4 Package, Test, Profiler, and Raw Tool Contracts

- **Status:** PASS
- **Commit baseline:** `d83849b5b14a0e3e5e25dc2cece387e390773d6e`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Switched `manage_packages`, `run_tests`, `profiler`, `execute_menu_item`, `execute_csharp`, and `log_to_console` to strict contracts without changing their existing handlers or CLI command surface.
  - Declared four package actions and five profiler actions, bringing the unchanged 31 built-in tool names to 75 canonical action contracts with every built-in tool strict.
  - Added action-specific required fields, actual aliases, package identifiers, typed test mode/default parameters, profiler argument conflicts, menu paths, log levels, stacktrace choices, and the existing `nocache` / `no-cache` compatibility forms.
  - Modeled `execute_csharp.usings` as the existing array-or-comma-string complex value while preserving scalar normalization and pre-dispatch strict validation.
  - Added typed outputs for package list/jobs, test runs, profiler hierarchy/status/control, menu execution, C# execution, and logging wherever the runtime shape is stable.
  - Updated default-contract result inference and explicit Newtonsoft property-name handling so the advertised default profiler schema matches the runtime camelCase response exactly.
  - Updated legacy metadata parsing to honor explicit `SchemaJson`, preventing complex strict parameters from failing discovery before the canonical contract is selected.
  - Preserved explicit invalid-action normalization as `UNKNOWN_ACTION`; no Go CLI command, MCP runtime, Typed CLI, package installation, release behavior, Unity scene, or project asset was added or changed.
  - Bumped the unreleased Connector package version from `0.0.68` to `0.0.69`.
- **Tests:**
  - PASS A RED exact-source run — PASS as a test of the new coverage: all expected M2.4 strict coverage, action, validation, alias, conflict, and output-schema cases failed before implementation.
  - PASS A final exact-source Connector/TestRunner compile using the active Unity `6000.3.5f2` response files — PASS with no compiler output.
  - PASS A exact-source `HeraAgent.Tests.ToolContractTests.RunTests` and `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS; 31 tool names, 75 canonical actions, all built-ins strict, property-level boolean `required` count zero, invalid runtime schema count zero, and all M2.1-M2.4 regressions passed.
  - PASS A Unity console/state verification — PASS; zero console errors and the Editor returned to `ready`.
  - PASS A `go test -count=1 ./...` — PASS (`cmd` 0.897s, `internal/assetconfig` 0.338s, `internal/client` 0.129s, `internal/poll` 0.937s, `tools/build-unity-docs` 0.035s, `tools/sync-agent-guides` 0.067s; remaining packages reported no test files).
  - PASS B read-only review found three confirmed gaps before any correction: profiler typed schemas used snake_case while runtime returned camelCase; the valid default profiler route exposed a generic output despite its stable hierarchy result; and tests did not directly compare runtime keys/default profiler schema or the default no-action conflict route.
  - Confirmed corrections only — honored explicit `JsonProperty` names in schema generation, inferred nested `Result` for default contracts, made the default profiler contract typed, and expanded only the missing runtime-key/default-route regression coverage.
  - Final exact-source Connector/TestRunner compile — PASS with no compiler output.
  - Final exact-source `HeraAgent.Tests.ToolContractTests.RunTests` — PASS, including `[PASS] TestM24StrictToolCoverage`, `TestEveryM24ActionContract`, `TestM24ValidationFailures`, `TestM24AliasesNormalize`, `TestM24MutuallyExclusiveTargets`, and `TestM24OutputSchemas`.
  - Final exact-source `HeraAgent.Tests.ToolDiscoveryTests.RunTests` — PASS; 31 built-in names, 75 canonical action contracts, all strict, Draft 2020-12 schema validity, deterministic output, and external response compatibility passed.
  - Final profiler runtime/schema smoke — PASS; runtime status keys `enabled`, `firstFrame`, `lastFrame`, `frameCount`, and `isPlaying` exactly match the typed status schema, and the typed default hierarchy schema exposes `children`, `depth`, `frame`, `frameCount`, `items`, `parent`, `parentName`, `root`, and `threadIndex`.
  - Final Unity console/state verification — PASS; zero matched errors and the Editor was `ready`.
  - Final `go test -count=1 ./...` — PASS (`cmd` 0.897s, `internal/assetconfig` 0.338s, `internal/client` 0.129s, `internal/poll` 0.937s, `tools/build-unity-docs` 0.035s, `tools/sync-agent-guides` 0.067s; remaining packages reported no test files).
  - Final `go vet ./...`, `go build ./...`, and `golangci-lint run ./...` — PASS; lint reported `0 issues`.
  - Final generated-guide check — PASS; `go run ./tools/sync-agent-guides --check` exited 0 and `AGENT.md` / `cmd/AGENT.md` remain byte-identical SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Final locally built `doctor --agent-rules` — PASS; 21,913 bytes, six required sections, no machine-specific absolute path, and no claim that unimplemented `mcp` or `call` commands are available.
  - Final obsolete-prohibition search — PASS; current rule language authorizes the planned Go adapter and prohibits only implementing MCP inside the Unity Connector or advertising unimplemented commands. README matches truthfully describe the shipped CLI/no-required-MCP setup; remaining implementation-plan/progress matches are normative or historical.
  - Final `git diff --check` — PASS, exit 0.
- **Known limitations:**
  - Safety classification and profile enforcement remain M3, catalog hashing and domain epochs remain M4, and Typed CLI/MCP runtime commands remain unimplemented. The existing CLI remains the production default.
  - The connected Editor consumes a cached Connector package rather than this repository as a local package. Verification therefore compiled and loaded the exact local sources into temporary in-memory assemblies; no package or project manifest was installed or changed.
  - Package mutation actions were contract-tested but not executed against the connected project because M2.4 does not authorize changing its package manifest. The profiler runtime smoke was read-only.
  - C# language-server diagnostics were unavailable; exact Unity compilation, contract/discovery tests, runtime schema probes, and the zero-error console check provided the C# verification surface.
- **Rollback procedure:**
  - Revert the M2.4 strict annotations, DTOs, aliases, action contracts, and typed results in `ManagePackages`, `RunTests`, `ManageProfiler`, `ExecuteMenuItem`, `ExecuteCsharp`, and `LogToConsole`.
  - Revert nested default-result inference in `ToolContractRegistry`, explicit `SchemaJson` parsing in `ToolMetadata`, and explicit Newtonsoft property-name handling in `SchemaUtility`.
  - Revert the M2.4 additions in `ToolContractTests` and `ToolDiscoveryTests`.
  - Restore `AgentConnector/package.json` from `0.0.69` to `0.0.68`, then remove this M2.4 ledger entry and the matching `CLAUDE.md` status/ledger changes.
  - No Go runtime, MCP runtime, scene, asset, project manifest, installed package, or persistent data migration needs rollback.
- **Next prerequisite:** M3 may start only under a separate instruction after confirming this M2.4 PASS gate. Do not infer authorization to begin it from this entry.

### M2.4 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded the M2.4 and full M2 PASS gates plus the explicit M3 boundary without weakening any architecture lock.
- **Generated agent guides:** Unchanged by M2.4; usage truth did not change and the deterministic synchronization check remains green.
- **README / README.ko change:** None; M2.4 adds no CLI or MCP command and does not advertise unavailable Typed CLI or MCP functionality.
- **Package version:** Connector manifest bumped to unreleased `0.0.69`; no package was installed, published, tagged, or released.

## M3 Safety Classification and Profiles

- **Status:** PASS
- **Commit baseline:** `d83849b5b14a0e3e5e25dc2cece387e390773d6e`
- **Date:** 2026-07-30
- **Implemented scope:**
  - Added the normalized safety contract, parameter-dependent safety rules, conservative MCP annotation mapping, and deterministic profile normalization/validation to the shared Connector contract registry.
  - Classified all 31 built-in tools and all 75 declared actions from their handlers. Read-only, write, destructive, package-change, and arbitrary-code operations now have explicit canonical metadata; destructive, package-change, and arbitrary-code operations require confirmation conservatively.
  - Added parameter rules for `console clear=true` and `exec compile_only=true`, most-specific rule selection, and ambiguous-rule rejection.
  - Defined the exact `core`, `scene`, `assets`, `ui`, `diagnostics`, `testing`, `full`, and `advanced` memberships. Normal profiles exclude `exec` and `menu`; strict custom tools without profile metadata default to `custom` plus policy-allowed `full`, while unspecified custom tools remain Compact-only.
  - Preserved legacy boolean normalization and the existing `list --tool` nested metadata shape. Canonical action risk is authored on `HeraActionContract` / `HeraAction`; pre-existing `HeraActionSafety` remains only as the legacy compatibility source.
  - Made unspecified built-in safety a contract-build failure and unspecified custom safety conservative, non-idempotent, potentially destructive, confirmation-required, and profile-hidden.
  - Bumped the unreleased Connector package version from `0.0.69` to `0.0.70`.
- **Tests:**
  - Initial exact-source RED compile with the new M3 safety/profile tests — expected failure; the compiler reported missing `HeraSafetyRuleAttribute` / `HeraSafetyRule` before the M3 implementation existed.
  - PASS A exact-source Connector/TestRunner compile using the active Unity `6000.3.5f2` response files redirected to this repository's sources — PASS with no compiler output.
  - PASS A exact-source `ToolSafetyTests` and `ToolProfileTests` — PASS; 31 built-in tools, 75 actions, unclassified built-in tools/actions `0`, normal-profile arbitrary-code operations `0`, and profile validation failures `0`.
  - PASS A exact-source `ToolContractTests` and `ToolDiscoveryTests` — PASS; all M2.1-M2.4 regressions, Draft 2020-12 validation, 31 names, 75 actions, all strict contracts, invalid runtime schema count `0`, and external response compatibility passed.
  - PASS A `go test -count=1 ./...` — PASS (`cmd` 0.899s, `internal/assetconfig` 0.268s, `internal/client` 0.099s, `internal/poll` 0.929s, `tools/build-unity-docs` 0.029s, `tools/sync-agent-guides` 0.044s; remaining packages reported no test files).
  - PASS B read-only review found six confirmed M3 gaps before any correction: conservative unknown flags contaminated classified actions; risk-only class safety changed legacy nested metadata; Input/TestRunner/package state flags did not match handlers; strict custom default profiles were missing; safety rules were absent from profile validation; and the audit table did not cover every action or directly test built-in unspecified failure.
  - Confirmed corrections only — moved risk to canonical action declarations, fixed legacy override normalization, restored legacy nested metadata and Input play-mode behavior, removed unsupported cancellation claims, marked package mutation as domain-reload-capable, completed custom/profile-rule validation, and expanded the audit to all 106 tool/action operations plus direct unspecified failure coverage.
  - Final exact-source Connector/TestRunner compile — PASS with no compiler output.
  - Final exact-source `ToolSafetyTests` and `ToolProfileTests` — PASS, including all 106 expected risk entries, legacy normalization, built-in/custom unspecified behavior, parameter rules, ambiguous-rule rejection, conservative annotations, strict custom defaults, rule-aware profile validation, and stateless resolution.
  - Final exact-source `ToolContractTests` and `ToolDiscoveryTests` — PASS; 31 built-in names, 75 canonical action contracts, all strict, Draft 2020-12 validity, deterministic schemas, legacy nested metadata preservation, property-level boolean `required` count `0`, and invalid runtime schema count `0`.
  - Final normalized-safety probe — PASS; `manage_components` read/write actions are non-destructive and confirmation-free, while only `remove` is destructive and confirmation-required.
  - Final Unity console/state verification — PASS; zero new console errors and the Editor returned `ready`.
  - Final `go test -count=1 ./...` — PASS (`cmd` 0.876s, `internal/assetconfig` 0.265s, `internal/client` 0.080s, `internal/poll` 0.930s, `tools/build-unity-docs` 0.030s, `tools/sync-agent-guides` 0.042s; remaining packages reported no test files).
  - Final generated-guide check — PASS; `go run ./tools/sync-agent-guides --check` exited `0` and `AGENT.md` / `cmd/AGENT.md` remain byte-identical SHA-256 `b3092befd6fb6ed45deb896369dd020074b14e082863a25dc2d2314f9c5e947e`.
  - Final locally built `doctor --agent-rules` — PASS; 21,913 bytes, six required sections, no machine-specific absolute path, and no claim that unimplemented `mcp` or `call` commands are available.
  - Final obsolete-prohibition search — PASS; remaining matches are the locked prohibition against implementing MCP inside the Unity Connector, the truthful prohibition against advertising unimplemented commands, or historical/normative migration records.
  - Final `git diff --check` — PASS, exit `0`.
- **Known limitations:**
  - M3 adds internal normalized safety/profile metadata only. The canonical one-request catalog, catalog hash, project fingerprint, and domain epoch remain M4; Typed CLI and MCP runtime commands remain unimplemented. The existing CLI remains the production default.
  - The connected Editor consumes a cached Connector package rather than this repository as a local package. Verification therefore compiled and loaded the exact local sources into temporary in-memory assemblies; no package or project manifest was installed or changed.
  - C# language-server diagnostics were unavailable; exact Unity compilation, exhaustive contract/safety/profile tests, runtime reflection probes, and the zero-error console check provided the C# verification surface.
- **Rollback procedure:**
  - Remove the M3 risk/profile/rule annotations from built-in declarations and restore the pre-M3 attribute surfaces.
  - Revert `ToolContractSafety`, `ToolContractSafetyRules`, `ToolContractProfiles`, the M3 registry/model fields, and only the M3 additions in `ToolDiscoveryTests`.
  - Remove `ToolSafetyTests`, `ToolSafetyExpectations`, and `ToolProfileTests` with their sibling `.meta` files.
  - Restore `AgentConnector/package.json` from `0.0.70` to `0.0.69`, then remove this M3 ledger entry and the matching `CLAUDE.md` status/ledger changes.
  - No Go runtime, MCP runtime, scene, asset, project manifest, installed package, or persistent data migration needs rollback.
- **Next prerequisite:** M4 may start only under a separate instruction after confirming this M3 PASS gate. Do not infer authorization to begin it from this entry.

### M3 Rule-document impact

- **CLAUDE.md migration state and completed ledger:** Recorded the M3 PASS gate and explicit M4 boundary without weakening any architecture lock.
- **Generated agent guides:** Unchanged by M3; user-facing CLI truth did not change and deterministic synchronization remains green.
- **README / README.ko change:** None; M3 adds no CLI or MCP command and does not advertise unavailable Typed CLI or MCP functionality.
- **Package version:** Connector manifest bumped to unreleased `0.0.70`; no package was installed, published, tagged, or released.

## M4 Canonical Catalog, Hash, and Domain Epoch

- **Status:** PASS
- **Commit baseline:** `0d36c1e788f2df1c15cd942a3677f683239b5d03`
- **Date:** 2026-07-31
- **Implemented scope:**
  - Added the existing `list` command's internal catalog mode with required
    schema-version negotiation and a normalized one-response envelope containing
    `schema_version`, `catalog_hash`, `domain_epoch`, `project_id`, and `tools`.
  - Added ordinal tool/action ordering, canonical schema ordering, deterministic
    SHA-256 hashing over only schema version and normalized tools, and a hashed
    normalized project fingerprint without emitting the absolute project path.
  - Added domain-lifetime epoch and `domain_epoch_v1` / `tool_catalog_v1`
    heartbeat capabilities plus Go client decoding.
  - Preserved legacy default, names, compact, and per-tool list data shapes.
  - Normalized implicit public-static legacy custom actions into the shared
    registry with conservative unknown-custom safety so the full catalog does
    not omit callable legacy actions.
  - Added the six required M4 tests plus exhaustive 31-tool/75-action snapshot,
    legacy dispatch byte-shape, heartbeat payload, and legacy custom-action
    coverage.
  - Bumped the unreleased Connector package version from `0.0.70` to `0.0.71`.
- **Evidence completed:**
  - Initial RED Go client test failed because heartbeat domain/features fields
    did not exist; initial exact-source C# compile failed only on the missing M4
    catalog/runtime symbols.
  - Exact-source Editor/TestRunner and merged assemblies compiled successfully.
  - A fresh unique-identity exact-source Unity run passed the full
    `ToolDiscoveryTests` suite with zero console errors; the catalog reported 31
    built-in tools, 75 actions, all strict contracts, and a stable lowercase
    SHA-256 hash.
  - The initial read-only review found an omitted legacy-custom-action path and
    incomplete snapshot/byte-shape/volatile/heartbeat evidence. Confirmed fixes
    were applied and the goal, code-quality, context, and security lanes have no
    current blockers.
  - After the final heartbeat `BuildStatus` refactor, exact-source compilation
    passed.
  - `go test -count=1 ./...` passed with a canonical macOS temporary root;
    `go vet ./...`, `go build ./...`, `golangci-lint run ./...`,
    `go run ./tools/sync-agent-guides --check`, meta GUID uniqueness, and
    `git diff --check` passed.
- **Final completion evidence:**
  - A new unique-identity exact-source Editor/TestRunner build compiled with
    Unity's active Bee response files and zero compiler output. The strict
    `ToolDiscoveryTests` run, including `ToolCatalogTests`, reported all tests
    passed with zero Unity console errors.
  - A single catalog request returned schema `hera.tool-catalog/1`, exactly 31
    built-in tools and 75 actions, all strict contracts, lowercase 64-hex
    `sha256:` catalog/project identifiers, and a non-empty domain epoch.
  - Missing and unsupported catalog schema versions both returned
    `SCHEMA_INVALID`; the legacy default, names, compact, and per-tool list
    shapes remained compatible.
  - A real script-domain reload changed the domain epoch while the same
    exact-source assembly identity retained catalog hash
    `sha256:879b6ed90ce05cf0492f607a1d6ba745eee103528426aa104fb6ba437ee6d99c`.
    The post-reload heartbeat retained `domain_epoch_v1` and
    `tool_catalog_v1`.
  - A separate post-reload unique-identity exact-source build and strict suite
    also passed with zero compiler output and zero Unity console errors.
  - `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    `golangci-lint run ./...`, `gofmt -l .`,
    `go run ./tools/sync-agent-guides --check`, meta GUID uniqueness,
    `git diff --check`, and the final read-only QA review all passed.
- **Known limitations:**
  - M5 Go registry/cache/validation, Typed CLI, and MCP runtime commands remain
    unimplemented. The existing CLI remains the production default.
  - Exact-source QA loaded temporary validation assemblies without changing the
    installed Unity package. Because source assembly identity is normalized
    catalog data, reload hash comparisons must reuse the same compiled assembly
    identity and bytes.
- **Rollback procedure:**
  - Revert the M4 implementation commit and this PASS-ledger commit together,
    restoring the Connector manifest to unreleased `0.0.70`.
  - No installed package, published artifact, tag, or runtime data migration
    requires rollback.
- **Boundary:**
  - M5 Go registry/cache/validation, Typed CLI, and MCP runtime commands remain
    unimplemented and unauthorized by this work unit. The existing CLI remains
    the production default.
- **Package version:** Connector manifest is unreleased `0.0.71`; no package was
  installed, published, tagged, or released.
- **Next prerequisite:** M5 may start only under a separate instruction after
  re-reading this ledger and confirming the M4 PASS gate.

## M5 Go Registry, Cache, and Validation

- **Status:** PASS
- **Commit baseline:** `b1a74833181b881d181c5d54bf67d0ea73a5281d`
- **Date:** 2026-07-31
- **Implemented scope:**
  - Added an internal canonical Go tool registry with native catalog-v1 and
    conservative legacy providers, deterministic profile selection, project and
    feature identity, and strict decoding of the M4 catalog envelope.
  - Added bounded concurrent memory and cross-process disk caches keyed by
    project, connector features, domain epoch, and catalog hash. Disk writes are
    atomic, use private permissions, and reject stale, corrupt, misnamed, or
    schema-invalid entries.
  - Added a bounded compiled JSON Schema cache using pinned
    `github.com/santhosh-tekuri/jsonschema/v6 v6.0.2`, Draft 2020-12 validation,
    and JSON-pointer diagnostics.
  - Added `tools/validate-tool-catalog`, accepting stdin or `--file`, for compact
    catalog and strict-schema validation summaries.
  - Added unit, fixture, concurrency, cross-process, corruption, schema, profile,
    provider, and live native/legacy integration coverage without importing the
    registry into `cmd`.
  - Recorded the dependency license, maintenance, transitive graph, and
    point-in-time OSV review in `docs/dependency-reviews/jsonschema-v6.md`.
- **Review corrections:**
  - Bounded the compiled-schema cache with LRU eviction.
  - Required schema compilation before cache store and disk-cache acceptance.
  - Required content-addressed disk filenames to match their decoded hash.
  - Restored 31 as the native live-test default; the body-only exact-source QA
    harness must opt in explicitly to its 30-tool expectation.
  - Rejected empty connector feature names at the registry boundary.
- **Evidence completed:**
  - `go test -count=1 ./...`, shuffled targeted tests, `go vet ./...`,
    `go build ./...`, `golangci-lint run ./...`, `golangci-lint fmt --diff`,
    `gofmt -l .`, `go run ./tools/sync-agent-guides --check`, and
    `git diff --check` passed.
  - Windows race-enabled schema, registry, and validator test binaries passed.
    The binaries were compiled and run from a repository-local temporary
    directory because the host denied execution from the default Windows
    temporary directory.
  - Fixture validation passed, including all strict schemas. Cross-process cache
    reuse passed, and corrupt, stale, schema-invalid, and filename/hash-mismatched
    cache entries were rejected.
  - A live legacy Connector integration passed through the compact-only
    conservative provider.
  - A live exact-source native catalog-v1 integration passed against Unity
    `6000.3.5f2`, and the validator reported schema `hera.tool-catalog/1`, hash
    `sha256:859b347534f43268ca860b1c8d647939b87e97877d688c8efbf61b693cef4f7a`,
    30 tools, 75 actions, and 30 strict schemas.
  - The native default remains the production M4 expectation of 31 tools. The
    exact-source body-only validation assembly intentionally omitted the separate
    TestRunner assembly, so only that harness used the explicit 30-tool override.
  - Exact `jsonschema/v6 v6.0.2` and its new `regexp2 v1.11.0` transitive
    dependency returned no vulnerabilities from the OSV query at review time;
    the local dependency license is Apache-2.0.
- **Known limitations:**
  - M5 supplies the internal registry, cache, and validation foundation only.
    Typed CLI and MCP runtime commands remain unimplemented, and the existing CLI
    remains the production default.
  - No installed CLI, Unity package, project manifest, scene, asset, published
    artifact, tag, or release was changed. Connector package version remains the
    unreleased `0.0.71`.
- **Rollback procedure:**
  - Remove `internal/schema`, `internal/toolregistry`, and
    `tools/validate-tool-catalog`; remove the pinned schema dependency and its
    dependency review; then revert this M5 ledger entry and the matching
    `CLAUDE.md` status/structure changes.
  - Cached catalog files under the Hera user cache may be deleted safely, but no
    persistent-data migration is required.
- **Rule-document impact:**
  - `CLAUDE.md` records the M5 PASS state and the new internal package/tool
    structure. Generated agent guides and README files are unchanged because M5
    adds no user-facing production command.
- **Next prerequisite:** M6 Typed CLI may start only under a separate instruction
  after re-reading this ledger and confirming the M5 PASS gate. Do not infer
  authorization to begin it from this entry.

## M6 Typed CLI

- **Status:** PASS
- **Commit baseline:** `3302e75`
- **Date:** 2026-07-31
- **Implemented scope:**
  - Added `hera-agent-unity call <tool>` with mutually exclusive `--json`,
    `--file`, and stdin request-object sources; no source defaults to `{}`.
  - Loads the M5 canonical live registry, requires a strict contract, resolves
    aliases and optional profile membership, validates before tool execution,
    and sends the canonical tool name with normalized request shape.
  - Added `--validate-only` and `--explain`. Both skip target tool execution;
    explain reports canonical action, contract mode, resolved M3 safety
    metadata, and the non-enforcing M6 policy projection.
  - Replaced package-level global CLI flag state with an immutable
    `GlobalConfig` passed through standalone, Unity, batch, editor, package,
    status, and update-notice paths while preserving legacy command behavior.
  - Added the `internal/policy` type skeleton without enabling approval
    enforcement, operation IDs, retries, or MCP runtime behavior.
- **Compatibility coverage:**
  - JSON, stdin, file, conflicting-source, unknown-property-before-execution,
    validate-only, explain, profile, and parameter-dependent safety behavior.
  - Legacy `scene` routing and explicit-flag-over-`--params` precedence remain
    unchanged.
  - Typed and legacy console inputs produce the same canonical wire request.
- **Review corrections:**
  - Applied catalog `when.const` safety rules by most-specific match so
    `console clear=true` and `exec compile_only=true` explanations cannot
    under-report risk.
  - Strengthened typed/legacy equivalence coverage to use the real JSON decoder.
  - Isolated config tests from ambient `HERA_AGENT_*` variables and corrected
    the general stdin help example.
- **Evidence completed:**
  - Required named M6 tests, `go test -count=1 ./...`, shuffled focused tests,
    `go vet ./...`, `go build ./...`, `golangci-lint run ./...`,
    `golangci-lint fmt --diff`, `gofmt -l .`, catalog fixture validation,
    guide drift, and `git diff --check` passed.
  - Race-enabled `cmd`, `internal/policy`, and `internal/toolregistry` binaries
    passed when compiled and run from repository-local paths; Windows denied
    direct execution from Go's temporary build directory.
  - A repository-local CLI executable connected to live Unity `6000.3.5f2` on
    port 8093. `call scene` passed through `--validate-only`, `--explain`, and
    stdin execution; the real invocation returned the active `GameScene`.
- **Known limitations:**
  - M6 policy output is descriptive only (`enforced=false`). Approval,
    operation-ledger, and retry semantics belong to M7/M11.
  - No stdio MCP server or MCP exposure mode exists yet; the CLI remains the
    production default through the M17 decision gate.
  - No installed CLI, Unity package, project manifest, tag, release, or
    published artifact was changed. Connector package version remains the
    unreleased `0.0.71`.
- **Rollback procedure:**
  - Remove `cmd/call*.go`, the call help/tests, `cmd/config.go`, and
    `internal/policy`; restore the legacy package-level flag plumbing and
    dispatch signatures; then revert this ledger, README, command reference,
    canonical agent guide, generated guides, and `CLAUDE.md` updates together.
- **Rule-document impact:**
  - `AGENTS.md` documents typed strict-tool invocation and is regenerated into
    the distributable and tool-specific guides.
  - README files and `docs/COMMANDS.md` now advertise only the implemented M6
    CLI surface. MCP commands remain undocumented because they do not exist.
- **Next prerequisite:** M7 may start only under a separate instruction after
  re-reading this ledger and confirming the M6 PASS gate. Do not infer
  authorization to begin it from this entry.

## M7 Connector Operation Ledger and Safe Retry

- **Status:** PASS
- **Commit baseline:** `fda9921`
- **Date:** 2026-07-31
- **Implemented scope:**
  - Added Go-generated operation IDs and canonical argument hashes to typed
    request metadata, while preserving one immutable request body and operation
    ID across retries.
  - Added typed Connector request context and a per-project atomic operation
    ledger under the Hera status directory. Records persist `received` and
    `running` before handler invocation, then persist the complete response as
    `committed` or `failed` before the HTTP response is written.
  - Added stored-response replay, changed-argument conflict rejection,
    current-domain in-progress responses, prior-domain unknown outcomes, and a
    no-reinvoke rule for non-idempotent unknown operations.
  - Added successful-write acknowledgement, 24-hour terminal retention,
    seven-day unknown retention, response compaction under a configurable
    per-project byte ceiling, and Windows-safe project ledger paths.
  - Added `operation_ledger_v1` heartbeat capability. New clients retry
    idempotent requests or ledger-capable single commands with the same
    operation ID; mutation retries against legacy Connectors stop with the
    typed `OPERATION_OUTCOME_UNKNOWN` error.
  - Wired `call` safety and catalog identity into transport options and exposed
    optional `--operation-id` reuse for explicit replay/query workflows.
- **Review corrections:**
  - Disabled HTML escaping in the Go canonical hash material so Go and
    Newtonsoft hash the same JSON string values.
  - Replaced the `sha256:` project fingerprint separator in the Windows
    directory name while retaining the canonical fingerprint in records and
    heartbeat data.
  - Compacted stored terminal responses into safe tombstones instead of
    deleting recent operation identity when the byte ceiling is exceeded.
- **Evidence completed:**
  - Required Connector tests
    `TestOperationReplayReturnsStoredResponse`,
    `TestOperationConflictRejectsDifferentArguments`,
    `TestCommittedResponseSurvivesResponseLoss`,
    `TestPriorDomainRunningBecomesUnknown`,
    `TestNonIdempotentUnknownDoesNotInvokeHandler`, and
    `TestLedgerAtomicWriteFallback` passed in Unity `6000.3.5f2`; the additional
    `TestLedgerRetentionCleanup` expiry test also passed.
  - Required Go tests `TestIdempotentRetryUsesSameOperationID` and
    `TestLegacyConnectorDisablesMutationRetry` passed, together with the
    cross-runtime canonical JSON hash regression.
  - `go test -count=1 ./...` passed for every package. Two isolated
    `go test -race ./internal/client` attempts were blocked before execution by
    the host denying access to the generated Windows `client.test.exe`; this was
    an environment execution restriction, not a test failure.
  - Exact repository Connector sources compiled in the live Unity project with
    zero console errors after temporary local-package resolution.
  - A real HTTP response-loss fixture closed the first client after 50 ms,
    retried the identical body and operation ID, replayed the committed response
    `{count:1}`, and independently read the mutation counter as exactly `1`.
  - The temporary Unity package manifest and lockfile were restored byte-for-byte
    and the external project package files were clean afterward.
- **Known limitations:**
  - Batch requests do not yet carry per-item operation metadata; transient batch
    outcomes therefore stop as unknown instead of being retried.
  - Approval enforcement remains M11 scope. No stdio MCP server exists yet, and
    the CLI remains the production default through the M17 decision gate.
  - No installed CLI, project manifest, tag, release, or published artifact was
    changed. Connector manifest is unreleased `0.0.72`.
- **Rollback procedure:**
  - Remove request metadata and retry policy changes from `internal/client`,
    remove Connector request context and operation ledger files, restore the
    prior router/server signatures and heartbeat feature list, remove the M7
    tests, and restore Connector manifest `0.0.71`.
  - Existing operation files are runtime cache/state and may be retained or
    removed after rollback; they are not project assets.
- **Rule-document impact:**
  - `CLAUDE.md` records the M7 PASS state and new Connector/client structure.
    User-facing guides remain unchanged because M7 adds reliability semantics,
    not an MCP command surface.
- **Next prerequisite:** M8 may start only under a separate instruction after
  re-reading this ledger and confirming the M7 PASS gate. Do not infer
  authorization to begin it from this entry.

## M8 stdio MCP skeleton

- **Status:** PASS
- **Commit baseline:** `d74bf5c`
- **Date:** 2026-07-31
- **Implemented scope:**
  - Pinned the official Go MCP SDK at stable `v1.7.0` and added an isolated
    `internal/mcpserver` lifecycle boundary.
  - Added `hera-agent-unity mcp --transport stdio --profile core` as a
    default-off experimental entry point gated by `HERA_MCP_ENABLED=1`.
  - Implemented protocol `2026-07-28` discovery and stable server identity.
    No Unity tools, resources, prompts, catalog bridge, or Connector calls are
    registered in M8.
  - Restricted transport to stdio. Protocol frames use stdout exclusively;
    SDK and CLI diagnostics use stderr.
  - Kept standalone MCP routing ahead of Unity discovery and update notices,
    so the existing CLI remains the production default and MCP discovery does
    not require a running Editor.
- **Review corrections:**
  - Upgraded cancellation coverage from a pre-cancelled context to an active,
    initialized MCP session cancelled while running.
  - Added real subprocess EOF coverage after manual QA found that SDK `v1.7.0`
    reports terminal stdio EOF as JSON-RPC server-closing code `-32004` without
    wrapping `io.EOF`; normalized only that coded terminal-EOF case.
  - Updated `CLAUDE.md` so current-state and structure documentation no longer
    claim that no MCP runtime entry point exists.
- **Evidence completed:**
  - Required lifecycle, feature-flag, unsupported-transport, discovery,
    protocol-only stdout, stderr diagnostic, and subprocess EOF tests pass.
  - `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    `golangci-lint run ./...`, `golangci-lint fmt --diff`, `go mod verify`,
    guide drift, formatting, and `git diff --check` pass.
  - The release matrix builds for Windows amd64, Linux amd64/arm64, and macOS
    amd64/arm64 with `CGO_ENABLED=0`. A first Linux cross-build without the
    release setting correctly failed because the Windows C compiler lacks
    Linux system headers; no source correction was required.
  - A freshly built temporary real CLI binary was driven by the official SDK
    client.
    Discovery negotiated protocol `2026-07-28`, returned the expected server
    identity with no tools capability, emitted only JSON-RPC frames on stdout,
    and exited cleanly.
  - Direct race-mode execution was attempted for `cmd` and
    `internal/mcpserver`, but the Windows host denied access to Go's temporary
    test executables before either suite could run. This is the same host
    execution restriction recorded for M7, not a test assertion failure.
  - Dependency review confirmed the official stable SDK tag, Go 1.25
    compatibility with this module, Apache/MIT licensing, and verified module
    checksums. No package installer or system-wide dependency mutation was
    used.
- **Known limitations:**
  - M8 is discovery-only. Native Profile tool registration belongs to M9 and
    was not implemented or implied.
  - Only stdio and the fixed `core` placeholder profile are accepted. The MCP
    entry point remains disabled unless explicitly enabled by environment.
  - No C# or Connector behavior changed, so Unity compile/runtime validation
    was not applicable. Connector manifest remains unreleased `0.0.72`.
  - No installed CLI, project manifest, tag, release, published artifact,
    commit, or push was changed.
- **Rollback procedure:**
  - Remove `cmd/mcp.go`, MCP help/routing/tests, and `internal/mcpserver`; remove
    the MCP SDK module requirement and sums; then restore this ledger and
    `CLAUDE.md` M7-only state together.
- **Rule-document impact:**
  - `CLAUDE.md` records the experimental discovery-only command and isolated
    server package. Public README and generated agent guides remain unchanged;
    public release documentation is M16 scope.
- **Next prerequisite:** M9 may start only under a separate instruction after
  re-reading this ledger and confirming the M8 PASS gate. Do not infer
  authorization to begin it from this entry.

## M9 Native Profile Tool Bridge

- **Status:** PASS
- **Commit baseline:** `d74bf5c` plus the uncommitted M8 working tree
- **Date:** 2026-07-31
- **Implemented scope:**
  - MCP startup now discovers Unity, loads the native catalog and compiled
    schema snapshot, selects one fixed seed profile, and registers its strict
    tools in ordinal order through the official Go MCP SDK.
  - Supported seed profiles are `core`, `scene`, `assets`, `ui`,
    `diagnostics`, and `testing`. Tool membership remains owned by Connector
    catalog metadata; Go contains no duplicate tool-membership lists.
  - Each native call parses a JSON object and validates the live strict schema
    before policy evaluation or Unity dispatch. Parameter-dependent safety is
    shared with Typed CLI policy resolution.
  - Approval-required operations return `APPROVAL_REQUIRED` before Unity until
    M11. Profiles containing any mutation require the Connector's
    `operation_ledger_v1` capability.
  - Every dispatched call gets a fresh operation ID plus `client_kind=mcp`,
    the live catalog hash, and resolved idempotence through the shared client.
  - Hera success and failure envelopes remain structured MCP content. Stable
    error codes, messages, data, suggestions, agent hints, and timings are
    preserved; expected Hera failures are MCP tool errors rather than protocol
    failures.
  - Conservative MCP annotations aggregate tool, action, and nested safety
    rules. `readOnlyHint` and `idempotentHint` are true only when every exposed
    operation qualifies; destructive/open-world hints turn true if any
    operation requires them.
- **Review corrections:**
  - Moved safety-rule resolution from `cmd` into `internal/policy` so Typed CLI
    and MCP cannot drift.
  - Added Go catalog rejection for legacy or arbitrary-code tools placed in a
    normal profile, including nested action safety rules.
  - Required the ledger feature before registering any mutating profile.
  - Expanded annotation aggregation to nested action safety rules after the
    read-only PASS B review found that omission.
  - Reworked the profile test fixture so all six seed profiles assert distinct,
    exact catalog-derived tool sets rather than exercising only `core`.
- **Evidence completed:**
  - Required tests `TestProfileRegistersExpectedTools`,
    `TestProfileOrderingStable`, `TestNativeToolValidatesBeforeUnity`,
    `TestNativeToolPreservesHeraErrorCode`,
    `TestNativeMutationUsesOperationID`, and
    `TestExecAbsentFromNormalProfiles` pass.
  - Approval-before-Unity, mutation-ledger gating, nested annotation,
    arbitrary-code catalog rejection, stdout purity, graceful EOF, and real
    MCP subprocess native-call regressions pass.
  - `go test -count=1 ./...`, `go vet ./...`, `go build ./...`, guide drift,
    formatting, and `git diff --check` pass. Direct race execution retains the
    previously recorded Windows temporary-executable access restriction.
  - For live QA, Inventoria temporarily resolved this repository's local
    Connector `0.0.72`. Its heartbeat advertised `domain_epoch_v1`,
    `operation_ledger_v1`, and `tool_catalog_v1`, compilation completed, and
    the Unity console reported zero errors.
  - A freshly built MCP server was driven by the official SDK client against
    Unity `6000.3.5f2`. All six seed profiles exactly matched their expected
    strict tool sets, with no `exec` or `menu`, and native `scene info`
    returned the live `GameScene` structured envelope successfully.
  - Inventoria's manifest and lock were restored byte-for-byte to their
    pre-QA SHA-256 hashes and the Editor returned to ready with zero console
    errors.
- **Known limitations:**
  - MCP remains default-off and stdio-only; the existing CLI remains the
    production default.
  - Compact/Full/custom/advanced exposure belongs to M10. `exec` and raw menu
    execution are not exposed by M9.
  - Approval/MRTR belongs to M11. M9 rejects approval-required calls instead
    of bypassing policy or inventing a temporary approval mechanism.
  - Catalog invalidation notifications, Tasks, resources, telemetry, and
    release documentation remain M13/M12/M14/M15/M16 work respectively.
- **Rollback procedure:**
  - Remove `internal/mcpserver/native_tools.go`, `results.go`, and
    `middleware.go`; restore the M8 discovery-only server/config/routing/help;
    restore CLI-local safety resolution and the pre-M9 profile validation;
    then restore this ledger and `CLAUDE.md` M8-only state together.
- **Rule-document impact:**
  - `CLAUDE.md` records native fixed-profile behavior and the M10/M11 locks.
    Public README and generated agent guides remain unchanged; release-facing
    documentation is still M16 scope.
- **Next prerequisite:** M10 may start only under a separate instruction after
  re-reading this ledger and confirming the M9 PASS gate. Do not infer
  authorization to begin it from this entry.

## M10 Compact and Full Exposure

- **Status:** PASS
- **Commit baseline:** `3fb1dd9`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added Compact `tool_search`, `tool_describe`, and `tool_call` as the only
    registered tools for compact exposure.
  - Added deterministic lexical ranking with ordinal name tie-breaking,
    optional profile filtering, bounded results, and optional compact schema.
  - Compact accepts native strict and conservative legacy catalogs. Strict
    calls use the compiled live schema; all calls use shared policy, ledger,
    catalog hash, `client_kind=mcp`, and generated or client operation IDs.
  - Added profile, compact, and full exposure selection. Full selects the
    catalog-owned strict `full` profile and excludes arbitrary code.
  - Added `custom`, `full`, and `advanced` profiles. Advanced cannot start
    without `--allow-arbitrary-code`; Compact also hides and rejects arbitrary
    tools unless that process permission was explicit.
- **Review corrections:**
  - Closed an arbitrary-code permission bypass in Compact search, describe,
    and call discovered during the read-only PASS B review.
  - Made Compact call output data schema unconstrained so valid array or scalar
    Hera results are not falsely rejected.
  - Strengthened the subprocess E2E to prove the dynamic custom result is
    actually discovered, not merely that search returned success.
  - Propagated result-data JSON encoding failures instead of returning a
    misleading empty-data success.
- **Evidence completed:**
  - Unit/integration tests cover Compact-only registration, deterministic
    ranking, describe identity, dynamic strict custom dispatch, legacy
    conservative policy, client operation IDs, Full-safe visibility, and
    Advanced permission gating.
  - A real subprocess E2E driven by the official Go MCP SDK discovered and
    called a dynamic custom tool through Compact.
  - `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    `golangci-lint run ./...`, formatter diff, catalog validation, MCP help,
    guide drift, and `git diff --check` pass.
  - Direct race-mode tests were attempted, but Windows denied access to the
    generated test executables before the suites could start, matching the
    previously recorded host restriction.
  - A current-source MCP binary was driven against Inventoria on Unity
    `6000.3.5f2`. Compact exposed exactly three tools and passed live scene
    search, describe, and call. Full exposed 29 strict tools without `exec` or
    `menu`. Advanced failed without permission and exposed `exec`/`menu` only
    with explicit permission. Unity remained ready with zero console errors.
- **Known limitations:**
  - Approval/MRTR remains M11 scope. Confirmation-required native or legacy
    calls return `APPROVAL_REQUIRED` even when startup exposure permits them.
  - Catalog invalidation, tasks, resources, telemetry, release docs, and
    benchmarks remain later milestones. CLI remains the production default.
- **Rollback procedure:**
  - Remove Compact tool/search files and tests; restore M9 seed-only config,
    profile validation, help, and native registration; then restore this ledger,
    `docs/MCP.md`, the handoff, and `CLAUDE.md` M9-only state together.
- **Rule-document impact:**
  - `CLAUDE.md`, `docs/MCP.md`, MCP help, and the handoff now describe the
    implemented experimental M10 surface. Public README remains M16 scope.
- **Next prerequisite:** M11 may start only under a separate instruction after
  re-reading this ledger and confirming the M10 PASS gate.

## M11 Approval and MRTR

- **Status:** PASS
- **Commit baseline:** `dab1e16`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added Connector-issued, process-local HMAC-SHA256 approval tokens bound to
    operation ID, tool, normalized action, canonical arguments hash, risk
    class, project ID, expiry, and single-use semantics.
  - Added Connector-authoritative preflight summaries and revalidation before
    any mutation or new operation-ledger `received`/`running` record.
  - Added Typed CLI human-TTY confirmation, non-interactive
    `APPROVAL_REQUIRED` preflight output, and `--approve` second-call support.
  - Added opt-in MCP Form elicitation through `--mrtr` /
    `HERA_MCP_MRTR=1`, plus an approval-token metadata fallback for clients
    without elicitation support. Unsupported Connectors fail closed with
    `APPROVAL_UNSUPPORTED`.
  - Batch requests reject confirmation-required items before the command lock
    or Undo group, and debug HTTP logging redacts approval-token fields.
- **Review corrections:**
  - Moved target and side-effect summary derivation fully into the Connector so
    misleading client fields cannot alter what the user approves.
  - Reordered exact operation-ledger replay ahead of new token consumption so
    response-loss retries return the committed result without reusing a token.
  - Reused the catalog's canonical risk spelling for `arbitrary_code` and
    `package_change`, preventing valid approved calls from mismatching.
  - Redacted both request and response token keys from debug HTTP output.
- **Evidence completed:**
  - Required CLI, client, policy, and MCP tests cover no-approval rejection,
    denied zero-mutation behavior, approved dispatch, arguments binding,
    expiry, single use, unsupported fallback, MRTR approval, and denied MRTR.
  - `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    `golangci-lint run ./...`, and `go run ./tools/sync-agent-guides --check`
    pass; lint reports `0 issues`.
  - The repository's exact Connector sources compile against Inventoria's
    Unity `6000.3.5f2` response file with the Unity-bundled Roslyn compiler.
    The only compiler diagnostic is the pre-existing
    `EntityIdCompat.cs` CS0618 warning.
  - The exact-source assembly was loaded in memory into active Inventoria.
    All seven `ApprovalPolicyTests` passed and the console error delta was zero.
    The destructive no-token fixture created no ledger directory, and the
    denied batch mutation counter remained zero.
  - Inventoria's package manifest and lock remained on the restored Git package
    reference; no package was installed, published, tagged, or released.
- **Known limitations:**
  - The active installed Git Connector did not contain the unreleased M11
    source, so combined local-CLI/live-HTTP approval E2E was not claimed.
    Connector behavior was verified through exact-source compilation and
    in-memory execution in the active Editor.
  - Tasks, catalog invalidation, resources, telemetry, release documentation,
    and benchmark rollout remain later milestones. CLI remains the production
    default.
- **Rollback procedure:**
  - Remove the approval authority/policy, preflight endpoint, ledger and router
    approval gates, CLI/MCP approval adapters and tests; restore M10 policy/help
    text and Connector version `0.0.72`; then revert this ledger, `docs/MCP.md`,
    the handoff, and `CLAUDE.md` M11 state together.
- **Rule-document impact:**
  - `CLAUDE.md`, `docs/MCP.md`, CLI/MCP help, and the handoff describe the
    implemented experimental M11 surface. Generated agent guides and public
    README files remain unchanged.
- **Next prerequisite:** M12 may start only under a separate instruction after
  re-reading this ledger and confirming the M11 PASS gate.

## M12 Tasks bridge

- **Status:** PASS
- **Commit baseline:** `f45bf8e`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added a generic Go task model and stateless file-bus adapters for Unity test
    `run_id` and Package Manager add/remove/embed `job_id` operations.
  - Added negotiated `io.modelcontextprotocol/tasks` capability advertisement,
    durable task creation responses, and `tasks/get`, `tasks/update`, and
    `tasks/cancel` extension methods. Task handles reconstruct state after an
    adapter restart from the existing pending and result files.
  - Preserved blocking result polling when Tasks is not negotiated or the
    Connector lacks `task_bridge_v1`; Typed CLI behavior and result-file cleanup
    remain unchanged.
  - Kept cancellation truthful: neither current Test Framework runs nor Package
    Manager jobs are marked cancelled, and the cancellation response explicitly
    reports unsupported execution cancellation.
  - Removed process-global standard logger mutation from concurrent async waits,
    raised package job IDs to full GUID entropy, advertised `task_bridge_v1`, and
    advanced the unreleased Connector manifest to `0.0.74`.
- **Review corrections:**
  - Restricted asynchronous metadata decoding to test and package adapters so
    unrelated tools returning a `running` message remain ordinary tool results.
  - Added bounded task-ID decoding and stable invalid-params/not-found JSON-RPC
    errors for task methods.
  - Preserved explicit `supported:false` and `cancelled:false` fields on the wire
    and locked full package-job GUID entropy with a Connector regression helper.
  - Added older-Connector negotiation fallback, package fallback, test terminal
    recovery, tool-error-as-completed, cancellation, and oversized-ID coverage;
    updated the public package job example.
- **Evidence completed:**
  - Taskbridge and MCP tests cover package and test restart recovery, negotiated
    extension results, custom task methods, blocking fallback, cancellation,
    malformed and oversized IDs, policy/schema/catalog regressions, and
    process-global logger invariance.
  - `gofmt -l .`, `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    `golangci-lint run ./...`, `go run ./tools/sync-agent-guides --check`, and
    `git diff --check` pass; lint reports `0 issues`.
  - Windows race-mode tests were attempted for `internal/taskbridge` and
    `internal/mcpserver`, but the host denied access to both generated test
    executables before either suite started. Ordinary targeted and full suites
    passed.
  - Repository-exact feature and package source probes compiled against
    Inventoria's Unity `6000.3.5f2` reference set. In-memory Editor checks saw
    `task_bridge_v1`; compiled package IL called `Guid.NewGuid().ToString("N")`
    without `Substring`, and the final helper produced distinct 36-character
    `pkg-` IDs with hexadecimal suffixes.
  - A disposable live EditMode run exercised the existing run-scoped file-bus
    lifecycle and returned a zero-test terminal result with no pending/result
    residue. An exact-source package pending record preserved the full job ID,
    port, action, identifier, and start timestamp, then was removed. Unity
    remained `ready` with zero console errors.
  - Inventoria's package manifest and lock were not changed. No package was
    installed, published, tagged, released, committed, or pushed for M12.
- **Known limitations:**
  - The active installed Git Connector predates unreleased M12, so negotiated
    task E2E used the official Go SDK's in-memory transport while Connector
    changes were verified through exact-source compilation and in-memory Editor
    execution. Installed-package task E2E is not claimed.
  - A whole-Editor external Roslyn probe was abandoned after sustained
    single-core execution produced no assembly; focused repository-exact probes
    for every changed Connector behavior passed instead.
  - Catalog invalidation, large-result resources, telemetry, release
    documentation, and benchmark rollout remain later milestones. CLI remains
    the production default.
- **Rollback procedure:**
  - Remove `internal/taskbridge`, MCP task registration/adapters/tests, restore
    blocking-only native invocation and the async wait wrapper, restore package
    job IDs and Connector version `0.0.73`, remove `task_bridge_v1`, then restore
    this ledger, the handoff, `docs/COMMANDS.md`, and `CLAUDE.md` together.
- **Rule-document impact:**
  - `CLAUDE.md`, this ledger, the handoff, and the package command example now
    describe M12. Generated agent guides remain unchanged and pass drift checks.
- **Next prerequisite:** M13 may start only under a separate instruction after
  re-reading this ledger and confirming the M12 PASS gate.

## M13 Catalog invalidation and list-changed

- **Status:** PASS
- **Commit baseline:** `7b93e49`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added a context-owned fresh-heartbeat observer that detects Unity domain
    epoch changes, refetches through `toolregistry.Registry.Load`, and publishes
    the matched Instance, catalog, and compiled schemas as one immutable runtime
    snapshot.
  - New calls return `CATALOG_STALE` while discovery or validation is pending;
    removed native tools return `TOOL_NOT_FOUND`. Calls already in flight retain
    their originally acquired instance, tool contract, schema cache, and catalog
    hash through completion.
  - Profile and Full exposure reconcile only added, removed, or semantically
    changed public MCP definitions. Same-hash, out-of-profile, and Compact
    catalog changes avoid spurious `tools/list_changed`; Compact keeps its fixed
    three-tool surface while search, describe, and call acquire the current
    snapshot.
  - Serialized `tools/list` across SDK registry reconciliation with a read/write
    publication gate, so clients observe a complete old or complete new tool
    registry rather than an intermediate delta.
  - Split task adaptation, runtime preparation, and MCP contract projection out
    of `native_tools.go`; every changed Go file remains below 250 pure LOC.
- **Review corrections:**
  - PASS B found that SDK `AddTool`/`RemoveTools` mutations could notify before
    runtime/schema publication or expose a partial list. Added a catalog-ready
    gate, then closed its initial TOCTOU gap by holding a registry read lock
    through the actual list handler while refresh holds the write lock across
    SDK reconciliation and runtime replacement.
  - Marked the catalog stale when fresh discovery temporarily loses the active
    Editor during reload, and clear that state only after same-epoch recovery or
    a validated new snapshot.
  - Added failed-load retry, list/refresh interleaving, removed-tool zero-send,
    Compact refresh, and old-then-new catalog-hash assertions to the required
    invalidation regressions.
- **Evidence completed:**
  - Required tests `TestDomainEpochInvalidatesCatalog`,
    `TestSameCatalogHashAvoidsSpuriousChange`,
    `TestListChangedOnCustomToolAdd`,
    `TestRemovedToolReturnsCatalogStaleOrUnknownTool`, and
    `TestInFlightCallUsesOriginalSnapshot` pass. The complete focused group,
    including Compact refresh, passed 50 consecutive runs.
  - `go test -count=1 ./...`, `go vet ./...`, targeted MCP tests, formatting,
    and `git diff --check` pass.
  - Race-enabled MCP, tool-registry, and schema test binaries were built into
    the repository evidence directory and executed from their package working
    directories with shuffle enabled; all three passed. This avoids the host's
    temporary-directory executable access restriction without weakening race
    instrumentation.
  - A freshly built MCP process remained connected to Inventoria while a
    disposable strict `m13_live_probe` custom Editor tool was added and removed.
    The same client received both list-changed notifications, listed the probe
    only after addition, invoked it successfully with structured data, and
    omitted it after removal.
  - Inventoria temporarily resolved the repository-local Connector for that
    live scenario, then restored its Git package manifest and lock byte-clean.
    The disposable asset and metadata were removed, the Editor returned to
    `ready`, the Unity console contained zero errors, and the Inventoria
    worktree was clean.
  - Independent PASS B re-review approved the final registry read/write gate,
    lock ordering, stale recheck, interleaving test, and complete Go gate.
- **Known limitations:**
  - Catalog observation uses a one-second heartbeat interval; during reload the
    server may emit transient discovery diagnostics while calls remain stale.
  - MCP remains experimental, default-off, stdio-only, and single-Editor. The
    existing CLI remains the production default.
  - Large-result resources, telemetry, release documentation, and benchmark
    rollout remain M14/M15/M16 work.
- **Rollback procedure:**
  - Remove `catalog_refresh.go`, `runtime.go`, the M13 tests, and the extracted
    contract helper; restore startup-captured native and Compact handlers,
    startup-only runtime preparation, the pre-M13 server lifecycle, and the
    original task helper location; then restore this ledger to M12 and run
    `go test -count=1 ./...`.
- **Rule-document impact:**
  - This ledger records the completed M13 behavior. Connector contracts,
    versions, generated agent guides, public README, and release docs are
    unchanged.
- **Next prerequisite:** M14 may start only under a separate instruction after
  re-reading this ledger and confirming the M13 PASS gate. Do not infer
  authorization to begin it from this entry.

## M14 Large result resources

- **Status:** PASS
- **Commit baseline:** `5c5723c`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added the positive `HERA_MCP_MAX_INLINE_BYTES` model-facing limit with the
    specified 131072-byte default. The limit measures the complete inline MCP
    tool result, including structured content and its compact text fallback,
    rather than reusing the unrelated 50 MiB Unity HTTP transport ceiling.
  - Results over the limit are atomically written under the per-project and
    per-operation result cache with a content SHA-256, byte size, truncation
    marker, `RESULT_SPOOLED` outcome, opaque `hera-result` URI, and MCP resource
    link. The complete JSON envelope remains retrievable through the registered
    resource template and is absent from inline tool content.
  - Added integrity-checked handle parsing, traversal rejection, restrictive
    cache permissions, pre-publication timestamping, 24-hour access retention,
    a 64 MiB cache cap, deterministic oldest-first pruning, and protection for
    the just-published result during cleanup.
  - Applied the same bounded mapping to blocking calls and negotiated task
    completions. Existing projection parameters continue unchanged to Unity,
    and oversized summaries direct clients toward supported limit, cursor,
    field, ID/name, stacktrace, and depth controls before reading full results.
  - Credential-shaped results and operation-resolved arbitrary-code output are
    withheld from both disk and inline content with the stable
    `RESULT_RESOURCE_UNAVAILABLE` outcome.
- **Review corrections:**
  - PASS B required retention enforcement on idle reads, broader session-token
    detection, timestamping before atomic publication, documented result codes,
    explicit-zero environment rejection, and observable projection/task tests.
    All were corrected and re-reviewed.
  - Changed the threshold calculation from only the structured envelope to the
    complete `CallToolResult`, preserving the original Unity error code as
    `unity_code` when that error result is resource-backed.
  - Replaced tool-wide arbitrary-code blocking with operation-resolved safety,
    then added a rule-specific regression proving a safe menu-list result can be
    stored while the same tool's arbitrary-code operation is withheld.
- **Evidence completed:**
  - Result-store tests cover atomic publication, timestamp failure, path and
    handle validation, hash tampering, idle expiry, retention pruning, byte-cap
    eviction, and temporary-file cleanup.
  - MCP tests cover below-cap projection passthrough, complete-result byte
    accounting, oversized inline exclusion and retrieval, stable result codes,
    credential and arbitrary-code guards, operation-specific safety, and
    oversized negotiated-task completion.
  - A subprocess MCP smoke test negotiated the official SDK, listed the result
    resource template, called a Unity fixture through stdio, confirmed the
    oversized payload was absent inline, and read the complete payload by URI.
  - `gofmt -l .`, `go test -count=1 ./...`, `go vet ./...`, `go build ./...`,
    schema/catalog validator tests, guide drift, and `git diff --check` pass.
  - Race-enabled result-store, MCP-server, and command test binaries were built
    into the repository evidence directory and executed from their package
    working directories with shuffle enabled; all three passed.
  - Independent PASS B first reported six concrete retention, guard, atomicity,
    taxonomy, configuration, and test-coverage violations. The complete fixes
    passed the full gate, and final re-review returned `APPROVE` with no open
    M14 finding.
- **Known limitations:**
  - Expired results are denied and removed on their next read; otherwise idle
    expired files are cleaned on the next spool. Explicit MCP resource reads may
    return the full stored result because retrieval is client-requested.
  - MCP remains experimental, default-off, stdio-only, and single-Editor. The
    existing CLI remains the production default. Telemetry, benchmark rollout,
    and release documentation remain M15/M16 work.
- **Rollback procedure:**
  - Remove `internal/resultstore`, the MCP result mapper/resource registration,
    result runtime/config fields, task-result adaptation, result tests and MCP
    help addition; restore direct `commandResult` mapping and the prior path
    helper; then restore this ledger to M13 and run `go test -count=1 ./...`.
- **Rule-document impact:**
  - MCP command help now documents the inline limit and resource behavior. This
    ledger records the completed M14 contract. Connector code/version, generated
    agent guides, public README files, release metadata, and install behavior are
    unchanged.
- **Next prerequisite:** M15 may start only under a separate instruction after
  re-reading this ledger and confirming the M14 PASS gate. Do not infer
  authorization to begin it from this entry.

## M15 Telemetry and benchmark harness

- **Status:** PASS
- **Commit baseline:** `9922d68`
- **Date:** 2026-08-01
- **Implemented scope:**
  - Added versioned task-economics telemetry with the required run,
    conversation, model, host, process, MCP, operation, Unity, and task
    correlation fields; successful-task, repair, token, timing, safety,
    reload, and intervention metrics; strict JSONL recording/reading; and
    aggregate p50/p95 summaries.
  - Added a reproducible A-to-E harness for legacy CLI, Typed CLI, MCP Profile,
    MCP Compact, and MCP Full. Every surface executes the same read-only
    `scene info` task, warms before recording, uses a fresh measured process,
    and refuses an existing output file.
  - Added marked disposable fixture preparation that invokes Unity with
    `-noUpm`, refuses non-empty destinations, leaves package manifests
    unchanged, copies repository-local Connector Editor sources without their
    tests, and supplies only the Newtonsoft and uGUI dependencies bundled with
    the selected Unity Editor.
  - Captured host IDs at the host boundary, real OS process IDs, MCP
    `tools/call` JSON-RPC IDs, and Connector operation IDs. The Connector does
    not expose a separate Unity HTTP request ID, so schema v2 records
    `not_available` with explicit ID provenance instead of inventing one.
  - Published the version-specific factual report at
    `docs/benchmarks/mcp/6000.3.5f2.md`, including reproduction commands,
    accounting methods, results, and limitations.
- **Review corrections:**
  - PASS B found that the first reproduction recipe launched Unity in the
    foreground, reused a non-empty fixture path, and lacked exact process
    cleanup. The final recipe uses a unique GUID path, asynchronous hidden
    launch, a readiness wait, same-run console evidence, and exact-PID cleanup.
  - Versioned the new accounting declaration as `hera.telemetry/2` while
    retaining read compatibility for pre-M15 `hera.telemetry/1` JSONL records;
    added positive v2 requirement and legacy decoding regressions.
  - Replaced synthetic process, MCP request, and operation labels with values
    observed at their execution boundaries, documented genuinely unavailable
    IDs, and added protocol/Connector observer tests.
  - Replaced the untyped single-use JSON helper with typed fixture-marker
    serialization and split observation code so every changed Go file remains
    below 250 pure LOC.
- **Evidence completed:**
  - Corrected disposable Unity runs `m15_20260801_ae_6` and
    `m15_20260801_ae_7` each completed all five variants with 5/5 first-attempt
    and final success, five host calls, five process launches, five logical
    Unity HTTP requests, 409 estimated tool-result tokens, and zero wrong-tool,
    invalid-argument, repair, duplicate-side-effect, unsafe-mutation,
    reload-recovery, and human-intervention counts.
  - The two corrected runs reproduced every non-time metric exactly. ae_6
    recorded p50 314 ms and p95 781 ms; ae_7 recorded p50 296 ms and p95
    759 ms. Immediately after ae_6 the same fixture was `ready` and returned
    zero matching console errors.
  - Fixture safety checks refused an unmarked project and an existing output.
    The live fixture used Unity `6000.3.5f2`, compiled the copied Connector, and
    did not open or mutate the production project.
  - A follow-up verification temporarily resolved the repository-local
    Connector `0.0.74` in the active Inventoria Editor. Legacy CLI, Typed CLI,
    MCP Profile, MCP Compact, and MCP Full all returned the same read-only
    `GameScene` state successfully. The Editor was `ready`, the scene remained
    clean, and zero console errors matched after the run.
  - Inventoria then restored its original Git Connector `0.0.64`, manifest,
    lock file, and lock hash byte-for-byte; Unity recompiled against the Git
    package cache and remained `ready`. The Inventoria worktree was clean and
    no project asset or recovery file was created.
  - `go test -count=1 ./...`, targeted `go vet`, guide drift,
    `git diff --check`, and race-instrumented telemetry and benchmark binaries
    executed from the repository evidence directory all pass.
  - Independent PASS B initially reported three HIGH, two MEDIUM, and one LOW
    issue. All were corrected; final read-only re-review returned
    `APPROVE / CLEAR`.
- **Known limitations:**
  - This scripted smoke benchmark makes no model call. Raw, cached, and billed
    model-token counts are therefore zero, and tool-result tokens are the
    declared deterministic estimate `ceil(serialized UTF-8 bytes / 4)`, not a
    provider tokenizer or billing measurement.
  - MCP result accounting excludes initial tool definitions, outer host
    framing, and model context policy. One task per variant is not statistical
    accuracy evidence and does not satisfy the later M17 default-decision gate.
  - The read-only task cannot empirically exercise mutation approval,
    duplicate-side-effect, unsafe-mutation, or reload-recovery failure paths.
    MCP remains experimental and default-off; CLI remains the production
    default.
- **Rollback procedure:**
  - Remove `internal/telemetry`, `tools/benchmark-mcp`, and the version-specific
    benchmark report; restore this ledger to M14; then run
    `go test -count=1 ./...` and the guide drift check. Disposable fixtures and
    repository-local evidence are not production project state.
- **Rule-document impact:**
  - This ledger and the version-specific benchmark report describe M15.
    Connector code/version, generated agent guides, public README files,
    release metadata, package manifests, and install behavior are unchanged.
- **Next prerequisite:** M16 may start only under a separate instruction after
  re-reading this ledger and confirming the M15 PASS gate. Do not infer
  authorization to begin it from this entry.
