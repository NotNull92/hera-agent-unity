# Codex Handoff — CLI + MCP Adapter Migration

## Goal

Continue the authorized CLI + MCP adapter migration without changing the locked
architecture or advertising unimplemented commands. The next work unit is not
authorized implicitly; execute it only after the user names the applicable
milestone or review prompt.

## Current state

### Done and verified

- M0 through M3 are recorded as `PASS` in
  `docs/MCP_MIGRATION_PROGRESS.md`.
- M4 implementation and confirmed review fixes are committed together with this
  handoff on `main` as `feat(connector): expose normalized tool catalog`.
- The M3 final gate passed with 31 built-in tools, 75 actions, zero
  unclassified operations, zero invalid schemas, and zero arbitrary-execution
  exposure in normal profiles.
- M4 adds the one-request `list` catalog envelope, deterministic catalog hash,
  project fingerprint, domain epoch/capabilities in heartbeat, Go heartbeat
  decoding, legacy custom-action normalization, and catalog regression tests.
- Exact-source M4 compilation passed. A unique-output exact-source Unity run
  passed the full `ToolDiscovery` suite with zero console errors and returned
  31 built-in tools, 75 actions, and all strict contracts.
- After the final heartbeat payload refactor, exact-source compilation passed.
  `go test -count=1 ./...` passed with a canonical macOS temporary root;
  `go vet ./...`, `go build ./...`, `golangci-lint run ./...`,
  generated-guide drift checking, and `git diff --check` also passed.
- The M4 goal, code-quality, context, and security review lanes have no current
  blockers.
- The unreleased Connector manifest is `0.0.71`.

### In progress

- M4 is **not yet PASS**. The final unique-output exact-source Unity QA must be
  rerun after the final heartbeat refactor.
- The previous Editor entered a native open-scene external-change decision
  dialog during the real domain-reload check, so the connector heartbeat stopped
  before the final QA rerun. Do not infer a source failure from that UI blocker.
- After live QA passes, update the M4 entry in
  `docs/MCP_MIGRATION_PROGRESS.md` and the matching `CLAUDE.md` ledger row from
  `IN PROGRESS` to `PASS`.

### Not implemented

- Typed CLI commands.
- MCP runtime commands or transport.
- Any package installation, publishing, tagging, or release.

## Decisions made

- Keep the Go CLI and localhost HTTP Unity Connector; MCP remains an optional Go
  adapter in front of the shared execution core.
- Do not implement MCP inside the Unity Connector.
- CLI remains the production default until the migration and benchmark gates
  pass.
- Tool and action safety classification is canonical registry metadata. Built-in
  operations must be explicitly classified.
- Unknown custom tools are conservative Compact-only; strict custom tools with
  no explicit profile normalize to `custom` and policy-allowed `full`.
- Preserve legacy discovery metadata, including nested `action_safety`, while
  exposing normalized contracts separately.
- `AGENTS.md` is the canonical cross-tool rules source. Generated rule files are
  never independent editing targets.

## Open questions / pending decisions

- M4 continuation is authorized only to finish its final QA, PASS ledger, and
  stop gate. This does not authorize M5.
- Do not change the project fingerprint design, catalog schema, or exposure
  rules unless new evidence reveals a concrete M4 defect.

## Next steps

1. Pull `main` and confirm the latest commit subject is
   `feat(connector): expose normalized tool catalog`.
2. Read `AGENTS.md`, `CLAUDE.md`, both migration documents, and
   `docs/MCP_MIGRATION_PROGRESS.md` completely.
3. Confirm M3 is `PASS`, M4 is `IN PROGRESS`, the Connector manifest is
   `0.0.71`, and the worktree is clean.
4. Connect to a suitable Unity Editor and compile the repository's exact
   Connector and TestRunner sources into a **new unique assembly identity**.
   Reusing an already-loaded assembly name can select stale bytes and create a
   false test failure.
5. Run the exact-source `ToolDiscoveryTests` / `ToolCatalogTests` with strict
   log handling. Verify zero console errors and one-request catalog output with
   schema `hera.tool-catalog/1`, exactly 31 built-ins, 75 actions, all strict,
   lowercase SHA-256 catalog/project identifiers, and a non-empty domain epoch.
6. Verify unsupported or missing catalog schema versions return
   `SCHEMA_INVALID`, and verify legacy default/names/compact/per-tool list data
   remains byte-shape compatible.
7. Perform a real script-domain reload only when the Editor has no unresolved
   scene-change dialog. Record that the domain epoch changes while the catalog
   hash stays stable, and confirm heartbeat features contain
   `domain_epoch_v1` and `tool_catalog_v1`.
8. Rerun the Go/build/lint/guide/diff gates. On macOS, use a canonical
   `/private/...` temporary root so the pre-existing `/var` symlink assertion
   does not produce an environmental false failure.
9. Rerun the QA review lane. Only after it passes, mark M4 `PASS` in the
   progress ledger and `CLAUDE.md`, commit that final ledger update, and stop.
10. Do not begin M5, Typed CLI, or MCP runtime work.

## References

- M4 WIP implementation commit: the commit containing this handoff,
  `feat(connector): expose normalized tool catalog`
- `docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md`
- `docs/CODEX_MCP_MIGRATION_IMPLEMENT_REVIEW_PROMPTS.md`
- `docs/MCP_MIGRATION_PROGRESS.md`
- `CLAUDE.md`
- `AGENTS.md`
- `AgentConnector/Editor/Core/ToolContractRegistry.cs`
- `AgentConnector/Editor/Core/ToolCatalogBuilder.cs`
- `AgentConnector/Editor/Core/ToolContractCanonicalJson.cs`
- `AgentConnector/Editor/Tests/ToolCatalogTests.cs`
- `AgentConnector/Editor/Core/ToolContractSafety.cs`
- `AgentConnector/Editor/Core/ToolContractSafetyRules.cs`
- `AgentConnector/Editor/Core/ToolContractProfiles.cs`
- `AgentConnector/Editor/Tests/ToolSafetyTests.cs`
- `AgentConnector/Editor/Tests/ToolProfileTests.cs`

## Environment notes / gotchas

- Do not rely on migration-document line numbers; inspect current symbols.
- For Windows shell work, use the Git Bash MCP rather than a bare `bash`.
- New Connector `.cs` files require matching `.meta` files.
- The installed CLI may not represent modified source; use `go run .` when
  validating local CLI behavior.
- Exact-source Connector validation must compile the repository source or use an
  appropriate Unity project without installing or overwriting a package.
- Give each rebuilt exact-source assembly a unique output/assembly name.
  Reusing an identity already loaded into the Editor can execute stale code.
- A native scene external-change dialog blocks Editor updates and therefore the
  connector heartbeat. Resolve it before interpreting `status` timeouts.
- On macOS, `/var` resolves to `/private/var`; run the full Go gate with a
  canonical `/private/...` temporary root when validating symlink behavior.
- Remaining MCP-prohibition search hits may be valid architecture locks,
  historical records, or truthful statements that runtime commands are not yet
  implemented. Classify them instead of deleting them blindly.

## Suggested skills

- `hyper-mode`: use for the next repository implementation/review milestone.
- `omo:programming`: use for Go or C# source changes and diagnostics.
- `hera-agent-unity`: use when live Unity compilation, tests, or state
  verification is required.
- `review-hera-agent-unity`: use for the milestone's read-only review pass when
  its instructions permit that workflow.
- `omo:git-master`: use only when the user asks to commit, inspect history, or
  push the completed next unit.
