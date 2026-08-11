# Hera v0.2.1 ui_doc A/B Fast-Redesign Checkpoint

Date: 2026-08-11 15:48 KST

Canonical workstream design and frozen decision rules before redesign:

[`ui-doc-ab-reduction-2026-08-11.md`](ui-doc-ab-reduction-2026-08-11.md)

## Current state

There is no live benchmark runner or benchmark fixture Unity process.

Two screening waves are invalid as whole units and are raw evidence only:

- [`screening-v021-minimal-reuse-20260811`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-reuse-20260811/INVALID.md): runner interruption before the T02 terminal artifacts.
- [`screening-v021-minimal-direct-20260811-143322`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-direct-20260811-143322/INVALID.md): T03/uidoc exhausted three zero-call invalid attempts and the runner exited.

The second wave retained six valid `run.json` records and three invalid
records. None is eligible for M2–M5. Both fixtures had zero Scene Recovery
backup files at inspection.

## User direction

The user stopped the slow formal matrix and requested a faster redesign.
Do not start another wave from the former 15-minute × 27-cell protocol.

The user approved the fast protocol on 2026-08-11. Its fixed design is in
[`2026-08-11-ui-doc-fast-ab-design.md`](../superpowers/specs/2026-08-11-ui-doc-fast-ab-design.md).
It preserves arm enforcement, a fixed shared environment, raw artifacts,
predeclared score/validity rules, and a branch decision rule while capping the
wave below one hour. Do not modify production `ui_doc` code until a valid fast
measurement selects a branch.
