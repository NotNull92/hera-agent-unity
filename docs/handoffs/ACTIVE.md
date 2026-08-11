# Active Development Handoff

Current workstream: **v0.2.1 ui_doc authoring accuracy A/B and conditional reduction.**

Active checkpoint:

[`ui-doc-ab-checkpoint-2026-08-11-1548.md`](ui-doc-ab-checkpoint-2026-08-11-1548.md)

Canonical workstream design and frozen decision rules:

[`ui-doc-ab-reduction-2026-08-11.md`](ui-doc-ab-reduction-2026-08-11.md)

Approved one-hour fast protocol:

[`2026-08-11-ui-doc-fast-ab-design.md`](../superpowers/specs/2026-08-11-ui-doc-fast-ab-design.md)

## Codex continuation

From the repository root, start an interactive session with:

```powershell
codex "Read docs/handoffs/ACTIVE.md and the active checkpoint it points to. Then read the canonical ui-doc A/B handoff, the approved one-hour fast design, and docs/benchmarks/ui-doc-ab/README.md. Follow AGENTS.md and CLAUDE.md. Verify git status, current Unity processes, accepted wave state, run.json count, frozen asset-config SHA, and whether an existing screening process is still running before doing anything. Do not restart or duplicate a live wave. Do not run the former 15-minute × 27-cell protocol; implement and validate only the approved one-hour fast protocol. Do not modify production ui_doc code before valid measurement evidence selects a branch. Preserve unrelated changes. Do not commit, push, tag, or release without explicit instruction."
```

The checkpoint is the exact continuation state. The canonical handoff owns the experiment design, scoring thresholds, and conditional production branch. Do not alter frozen benchmark rules because of intermediate scores.
