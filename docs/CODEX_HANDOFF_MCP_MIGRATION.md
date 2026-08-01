# Codex Handoff — CLI + MCP Adapter Migration

## Goal

Continue the authorized CLI + MCP adapter migration without changing the locked
architecture or advertising unimplemented commands. The next work unit is not
authorized implicitly; execute it only after the user names the applicable
milestone or review prompt.

## Current state

### Done and verified

- M0 through M11 are recorded as `PASS` in
  `docs/MCP_MIGRATION_PROGRESS.md`.
- M9 is committed and pushed to `origin/main` as
  `3fb1dd9 feat(mcp): add native profile tool bridge`.
- M10 is committed and pushed to `origin/main` as
  `dab1e16 feat(mcp): add compact and full exposure modes`.
- M11 adds process-local HMAC approval, CLI TTY/non-interactive flows, MCP
  elicitation/fallback, Connector revalidation, and batch fail-closed behavior.
  Its working tree is verified but intentionally uncommitted by the M11 prompt.
- M7 adds stable operation IDs, request metadata, canonical argument hashes,
  typed Connector context, atomic pre-execution/pre-response persistence,
  stored-response replay, conflict and unknown-outcome handling, retention, and
  capability-gated retry.
- `approval_v1` and `operation_ledger_v1` are emitted in heartbeat features.
  Connector manifest is unreleased `0.0.73`.
- Required Go retry tests and the full Go suite pass. Exact repository Connector
  sources compile in Unity `6000.3.5f2` with zero console errors, and all seven
  operation-ledger menu tests pass.
- A real response-loss fixture closed the first request after 50 ms, retried the
  same body and operation ID, replayed `{count:1}`, and independently observed
  the mutation counter at exactly one.
- The Inventoria manifest and package lock used for exact-source QA were restored
  byte-for-byte and are clean.

### Final QA state

- M11 implementation, review corrections, final QA, and PASS recording are
  complete.
- M12 is not authorized implicitly by this handoff.

### Not implemented

- Tasks, resources, telemetry, release documentation, and benchmark rollout.
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

- M12 may begin only after a separate user instruction and confirmation that M11
  remains PASS.
- Batch commands intentionally stop with unknown outcome on a transient
  connection because M7 does not add per-item batch operation records.

## Next steps

1. Confirm the M11 working tree and its full gate before any Git operation.
2. Re-read `AGENTS.md`, `CLAUDE.md`, the M12 implementation section and exact
   implement/review prompt, and the progress ledger before changing source.
3. On separate authorization, begin only M12 Tasks bridge. Keep the CLI as the
   production default and do not begin catalog invalidation.

## References

- M6 implementation commit: `fda9921`
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
- `AgentConnector/Editor/Core/CommandRequestContext.cs`
- `AgentConnector/Editor/Core/OperationLedger.cs`
- `internal/client/operation.go`
- `internal/client/reload_retry.go`
- `AgentConnector/Editor/Tests/OperationLedgerTests.cs`
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
- The active Inventoria manifest points to the restored Git package, not the
  unreleased M11 working tree. M11 Connector QA used a unique exact-source
  assembly loaded in memory; do not claim installed-package E2E from it.
- Give each rebuilt exact-source assembly a unique output/assembly name.
  Reusing an identity already loaded into the Editor can execute stale code.
- A native scene external-change dialog blocks Editor updates and therefore the
  connector heartbeat. Resolve it before interpreting `status` timeouts.
- On macOS, `/var` resolves to `/private/var`; run the full Go gate with a
  canonical `/private/...` temporary root when validating symlink behavior.
- Two Windows `go test -race ./internal/client` attempts were blocked before
  test execution because the generated `client.test.exe` returned
  `Access is denied`, including with an isolated `GOTMPDIR`. Ordinary targeted
  and full Go tests passed; treat race coverage as an environment limitation,
  not as a passing result.
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
