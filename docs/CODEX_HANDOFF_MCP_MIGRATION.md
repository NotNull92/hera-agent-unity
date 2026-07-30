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
- M2.4 and M3 implementation and review fixes are committed on `main` as
  `c736b8a2f9c4f2d1642a471d149944346698430f`
  (`feat(contracts): complete schemas and safety profiles`).
- The M3 final gate passed with 31 built-in tools, 75 actions, zero
  unclassified operations, zero invalid schemas, and zero arbitrary-execution
  exposure in normal profiles.
- Exact-source Unity compilation passed for the Editor and TestRunner
  assemblies. The Unity `ToolSafety`, `ToolProfiles`, `ToolContract`, and
  `ToolDiscovery` suites passed in Unity 6000.3.5f2.
- `go test -count=1 ./...`, agent-guide synchronization, local-source
  `doctor --agent-rules`, generated-guide hash equality, machine-path scanning,
  and `git diff --check` passed.
- The unreleased Connector manifest is `0.0.70`.

### Not implemented

- M4 canonical catalog/hash/project fingerprint/domain epoch.
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

- The user must select and authorize the next milestone or review prompt.
- If M4 is selected, its exact scope and stop gate come from
  `docs/CODEX_MCP_MIGRATION_IMPLEMENT_REVIEW_PROMPTS.md` and
  `docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md`; do not infer later runtime work.

## Next steps

1. Pull `main` and confirm HEAD includes
   `c736b8a2f9c4f2d1642a471d149944346698430f`.
2. Read `AGENTS.md`, `CLAUDE.md`, both migration documents, and
   `docs/MCP_MIGRATION_PROGRESS.md` completely.
3. Confirm the M3 completion gate is recorded as `PASS` and the worktree is
   clean before editing.
4. Execute only the work unit explicitly requested by the user, preserving the
   prompt's pass ordering, review-only phase, confirmed-fixes-only rule, final
   rerun, progress update, and stop condition.
5. Do not begin Typed CLI or MCP runtime work unless the selected milestone
   explicitly authorizes it.

## References

- Implementation commit:
  `c736b8a2f9c4f2d1642a471d149944346698430f`
- `docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md`
- `docs/CODEX_MCP_MIGRATION_IMPLEMENT_REVIEW_PROMPTS.md`
- `docs/MCP_MIGRATION_PROGRESS.md`
- `CLAUDE.md`
- `AGENTS.md`
- `AgentConnector/Editor/Core/ToolContractRegistry.cs`
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
