# Codex Handoff — CLI + MCP Adapter Migration

## Goal

Continue the authorized CLI + MCP adapter migration without changing the locked
architecture or advertising unimplemented commands. The next work unit is not
authorized implicitly; execute it only after the user names the applicable
milestone or review prompt.

## Current state

### Done and verified

- M0 through M7 are recorded as `PASS` in
  `docs/MCP_MIGRATION_PROGRESS.md`.
- M6 is committed and pushed to `origin/main` as
  `fda9921 feat(cli): add schema-validated typed calls`.
- M7 adds stable operation IDs, request metadata, canonical argument hashes,
  typed Connector context, atomic pre-execution/pre-response persistence,
  stored-response replay, conflict and unknown-outcome handling, retention, and
  capability-gated retry.
- `operation_ledger_v1` is emitted in heartbeat features. Connector manifest is
  unreleased `0.0.72`.
- Required Go retry tests and the full Go suite pass. Exact repository Connector
  sources compile in Unity `6000.3.5f2` with zero console errors, and all seven
  operation-ledger menu tests pass.
- A real response-loss fixture closed the first request after 50 ms, retried the
  same body and operation ID, replayed `{count:1}`, and independently observed
  the mutation counter at exactly one.
- The Inventoria manifest and package lock used for exact-source QA were restored
  byte-for-byte and are clean.

### Final QA state

- M7 implementation, review corrections, final QA, and PASS recording are
  complete.
- M8 is not authorized implicitly by this handoff.

### Not implemented

- MCP runtime commands or transport.
- Approval enforcement and MRTR.
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

- M8 may begin only after a separate user instruction and confirmation that M7
  remains PASS.
- Batch commands intentionally stop with unknown outcome on a transient
  connection because M7 does not add per-item batch operation records.

## Next steps

1. Start the next session by confirming a clean worktree and reading the commit
   containing this handoff, whose subject is
   `feat(reliability): add operation ledger and replay-safe retries`.
2. Re-read `AGENTS.md`, `CLAUDE.md`, the M8 section of the implementation plan,
   the exact M8 implement/review prompt, and the progress ledger before changing
   source.
3. On separate authorization, begin only the M8 stdio MCP skeleton and keep the
   CLI as the production default. Do not start M9 approval enforcement as part
   of M8.

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
