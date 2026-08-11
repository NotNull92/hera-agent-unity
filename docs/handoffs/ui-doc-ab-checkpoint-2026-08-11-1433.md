# Hera v0.2.1 ui_doc A/B Benchmark Checkpoint

Date: 2026-08-11 14:33 KST

Canonical workstream design and frozen decision rules:

[`ui-doc-ab-reduction-2026-08-11.md`](ui-doc-ab-reduction-2026-08-11.md)

## Prior accepted-wave invalidation

[`screening-v021-minimal-reuse-20260811`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-reuse-20260811/INVALID.md)
is invalid as a whole. Its runner process group stopped during
`T02 / uidoc / rep-01 / attempt-01` before required terminal artifacts were
written. The three prior T01 `run.json` records are preserved as raw artifacts
only; none may contribute to M2–M5.

The interruption source could not be established from available PowerShell,
Unity, or Windows event evidence. This is an infrastructure/audit failure, not
a scored timeout or an arm outcome.

## Current direct formal wave

Wave:

[`screening-v021-minimal-direct-20260811-143322`](../benchmarks/ui-doc-ab/results/screening-v021-minimal-direct-20260811-143322/)

The runner was launched directly as an independent hidden PowerShell process,
not through the interrupted benchmark host shell. Its stdout and stderr are
preserved at:

- `docs/benchmarks/ui-doc-ab/results/runner-logs/screening-v021-minimal-direct-20260811-143322.stdout.log`
- `docs/benchmarks/ui-doc-ab/results/runner-logs/screening-v021-minimal-direct-20260811-143322.stderr.log`

Initial live state:

- runner PID: `67600`
- shared Unity PID: `28152`
- fixture profile: `minimal-ugui`
- fixture location: `%TEMP%\hera-ui-ab-wave-7f75550e34f5470cb2b3f5e3f2890580\project`
- status: `running`
- active cell: `T01 / uidoc / rep-01 / attempt-01`
- CLI SHA-256: `0ea29afb54c3b7bb1db8c9e638db2cb052683c23984e11210be6aa154ad3d346`
- baseline Scene SHA-256: `9ff1d0b3cbbbf451987285a4e1604909de2e5caa257dd88b53ffaff54c5090e7`
- user asset-config SHA-256: `dd468637e1bc07c3ec24ac7024e278a0f1be0b9b68b89f6961711ec7258bc888`
- `ui_system`: `ugui`
- Scene Recovery backup count at launch: `0`

Frozen gate evidence immediately before this wave:

- `test-shim.ps1`: `SHIM_POLICY_PASS`
- `test-fixture.ps1`: `FIXTURE_RESET_PASS`
- `git diff --check`: PASS
- production-path change check: PASS

## Continuation rules

1. Read `ACTIVE.md`, this checkpoint, the canonical handoff, and the benchmark README.
2. Check `wave.json`, runner PID/command line, exact Unity project path, `run.json` count, frozen asset-config SHA, and Scene Recovery backup count.
3. If this wave is still running, do not start, resume, or duplicate a runner. Observe it only.
4. If it reaches `screening_complete` with 27 valid accepted cells, run `Compare-Results.ps1`, update the canonical handoff with raw evidence, and select exactly one M5 branch.
5. If it stops without `screening_complete`, mark the entire direct wave invalid with its exact artifact boundary. Do not retain a favorable subset.
6. Do not modify production `ui_doc` code, UITK schemas, `html-to-uidoc`, or capture routing before M5 selects a branch.
