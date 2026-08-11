# Hera v0.2.1 ui_doc A/B Fast-Redesign Checkpoint (Completed)

Date: 2026-08-11 17:23 KST

Canonical workstream design and frozen decision rules before redesign:

[`ui-doc-ab-reduction-2026-08-11.md`](ui-doc-ab-reduction-2026-08-11.md)

## Current state

There is no live benchmark runner or benchmark fixture Unity process.

The approved fast wave completed successfully:

- [`screening-v021-fast-utf8-20260811-163214`](../benchmarks/ui-doc-ab/results/screening-v021-fast-utf8-20260811-163214/wave.json): 12/12 valid cells, no recovery backups, complete raw artifacts, and no invalid/incomplete marker. The measured cell window was 49.888 minutes (16:32:59–17:22:52 KST).
- [`comparison.md`](../benchmarks/ui-doc-ab/results/screening-v021-fast-utf8-20260811-163214/comparison.md): decision **inconclusive**. `uidoc` mean score was 25.406 versus `primitives_batch` 74.017; both arms had zero strict passes.

This valid result does not select a production reduction branch. Production
`ui_doc` code remains unchanged.

The two formal screening waves are invalid as whole units and are raw evidence
only:

- [`screening-v021-minimal-reuse-20260811`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-reuse-20260811/INVALID.md): runner interruption before the T02 terminal artifacts.
- [`screening-v021-minimal-direct-20260811-143322`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-direct-20260811-143322/INVALID.md): T03/uidoc exhausted three zero-call invalid attempts and the runner exited.

The second wave retained six valid `run.json` records and three invalid
records. None is eligible for M2–M5. Both fixtures had zero Scene Recovery
backup files at inspection.

The first approved fast attempt,
[`screening-v021-fast-20260811-160910`](../benchmarks/ui-doc-ab/results/screening-v021-fast-20260811-160910/INVALID.md),
is also invalid as a whole. T03 contained non-ASCII prompt text and
`Run-One.ps1` wrote child stdin with the Windows `ks_c_5601-1987` default;
Codex rejected it before the first Hera call. Redirected stdin is now pinned to
UTF-8 and covered by `test-run-one-utf8.ps1`. The attempt's five raw run records
are not eligible for M2–M5.

## User direction

The user stopped the slow formal matrix and requested a faster redesign.
Do not start another wave from the former 15-minute × 27-cell protocol.

The user approved the fast protocol on 2026-08-11. Its fixed design is in
[`2026-08-11-ui-doc-fast-ab-design.md`](../superpowers/specs/2026-08-11-ui-doc-fast-ab-design.md).
It preserves arm enforcement, a fixed shared environment, raw artifacts,
predeclared score/validity rules, and a branch decision rule while capping the
wave below one hour. The completed fast measurement was inconclusive, so do not
modify production `ui_doc` code on its basis.
