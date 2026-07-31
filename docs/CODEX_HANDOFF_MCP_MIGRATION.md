# Codex Handoff — CLI + MCP Adapter Migration

## Goal

Continue the authorized CLI + MCP adapter migration without changing the locked
architecture or advertising unimplemented commands. The next work unit is not
authorized implicitly; execute it only after the user names the applicable
milestone or review prompt.

## Current state

### Done and verified

- M0 through M4 are recorded as `PASS` in
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
- Final unique-identity exact-source and post-reload Unity suites passed with
  zero compiler output and zero console errors. A real domain reload changed
  the epoch while preserving the catalog hash for the same assembly identity,
  and retained both heartbeat capabilities.
- The one-request catalog probe returned 31 built-ins, 75 actions, all strict;
  missing and unsupported schema versions returned `SCHEMA_INVALID`.
- The final Go/build/lint/guide/meta/diff gates and read-only QA review passed.
- The unreleased Connector manifest is `0.0.71`.

### Final QA state

- M4 final QA and PASS recording are complete.
- No M5 implementation is authorized by this handoff.

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

1. Stop after the M4 PASS ledger commit.
2. Do not begin M5, Typed CLI, or MCP runtime work without a separate user
   instruction.

## References

- M4 implementation commit: the commit containing this handoff,
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
