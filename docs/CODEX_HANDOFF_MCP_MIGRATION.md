# Codex Handoff — CLI + MCP Adapter Migration

## Goal

Preserve the completed M0-M16 migration and the truthful M17 BLOCKED record.
Future work must keep the locked architecture and must not promote MCP without
complete measured evidence plus an explicit user decision.

## Current state

- M0 through M16 are recorded as `PASS`; M17 is `BLOCKED` in
  `docs/MCP_MIGRATION_PROGRESS.md`.
- `origin/main` baseline before M17 documentation is `9080f94`.
- The Go CLI and localhost HTTP Unity Connector remain the execution core.
- Current-source MCP is an unreleased, stdio-only, environment-gated adapter.
  It supports native Profile, Compact, Full-safe, explicitly permitted
  Advanced, approval/MRTR, operation-ledger replay, negotiated Tasks with
  blocking fallback, catalog invalidation, and bounded large-result resources.
- M17 now has bounded Inventoria evidence for nine of its ten required
  categories. The Profile-versus-Full definition-size subgate measured a 72.5%
  estimated token reduction, but the Typed benefit and MCP-primary gates were
  not met by the zero-model-call ceiling-effect smoke run. The complete section
  28.3 matrix still has not run in one marked disposable fixture.
- Typed CLI and the existing CLI therefore remain the production default. MCP
  remains experimental and default-off. No runtime default or Connector
  architecture changed.

## M17 partial verification summary

- Full Go format, vet, build, test, lint, and guide-sync gates pass.
- Repository Connector `0.0.74` compiled in Inventoria on Unity `6000.3.5f2`.
  The 31-tool/75-action strict catalog and eight Editor suites recorded 137
  PASS lines, eight `ALL PASSED` summaries, and zero failures.
- Disposable-fixture observations covered scene, object, component, asset,
  uGUI, Play Mode, reload, catalog invalidation, and custom-tool lifecycle with
  zero console errors. Fixture tests, package job, invalid-argument repair,
  missing target, destructive approve/deny, and batch were not completed.
- Live official-SDK MCP conformance exposed eight core Profile tools and
  returned clean `GameScene` state. Response-loss replay produced exactly one
  mutation. An unapproved destructive batch completed zero items; a separately
  approved cleanup removed only its temporary target.
- A live temporary strict tool changed catalog hash, domain epoch, and count
  31→32, returned `Value=17`, and was removed. The original hash and count
  returned under a new epoch.
- A-to-E run `m17_20260802_ae_1` recorded 5/5 first and final success, zero
  wrong-tool, invalid-argument, duplicate, unsafe, reload-recovery, and human
  intervention events. It makes no model calls and is not statistical accuracy
  or billing evidence. The raw benchmark records, definition-size output, and
  bounded Inventoria report are retained. M17 cannot be marked PASS until the
  complete integration matrix runs in one disposable fixture.

## Operational follow-up

Inventoria's `Packages/manifest.json` and `Packages/packages-lock.json` are
restored byte-for-byte and its worktree is clean. A later live check corrected
the earlier cold-start diagnosis: Unity Package Manager is healthy, Git package
`com.notnull92.hera-agent-unity` `0.0.64` is registered from the restored
package cache, the Editor returns `ready` after a requested compile, and the
console has zero matching errors. Do not regenerate or otherwise mutate
Inventoria's `Library` for M17 recovery.

The marked M17 fixture and temporary Connector copies were moved to the Recycle
Bin. They are recoverable and are not repository or Inventoria project state.

## Locked decisions

- Do not implement MCP inside the Unity Connector.
- Do not split CLI and MCP tool contracts.
- Keep localhost HTTP, main-thread serialization, heartbeat discovery,
  file-bus recovery, and the single-selected-Editor model.
- Keep CLI and Connector versions separate.
- Keep MCP unreleased and default-off unless a future benchmark satisfies the
  decision gates and the user explicitly selects a new default.
- `AGENTS.md` is the canonical generated-guide source. Do not edit generated
  guides independently.

## References

- `docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md`
- `docs/CODEX_MCP_MIGRATION_IMPLEMENT_REVIEW_PROMPTS.md`
- `docs/MCP_MIGRATION_PROGRESS.md`
- `docs/benchmarks/mcp/6000.3.5f2.md`
- `docs/benchmarks/mcp/m17_inventoria_20260802.md`
- `CLAUDE.md`
- `AGENTS.md`

## Environment notes

- Use a source-built binary when validating unreleased MCP behavior.
- Exact-source Connector QA must compile repository sources, not infer behavior
  from an installed older package.
- Use unique temporary assembly names to avoid stale in-memory Unity assemblies.
- Never use a production project as a destructive benchmark fixture.
- Classify historical MCP prohibition text before changing it; many hits are
  intentional architecture locks or milestone records.
