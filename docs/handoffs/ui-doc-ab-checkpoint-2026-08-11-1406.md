# Hera v0.2.1 ui_doc A/B Benchmark Checkpoint

Date: 2026-08-11 14:06 KST

Repository: `C:\Users\PC\Desktop\Cowork\hera-agent-unity`

Canonical workstream design and frozen decision rules:

[`ui-doc-ab-reduction-2026-08-11.md`](ui-doc-ab-reduction-2026-08-11.md)

This checkpoint exists so a new session can continue from the exact live benchmark state without reconstructing the debugging history.

## Mission

Measure whether `ui_doc` authoring materially improves final Unity UI implementation accuracy over generic Hera primitives.

Arms are frozen:

- A `uidoc`: `ui_doc apply/export`
- B `primitives`: `manage_ui` + `manage_components` + `manage_gameobject`, no batch
- C `primitives_batch`: same generic primitives plus `batch`

All arms share the same read/verification surface, including neutral `ui_doc capture` during M2-M4.

Do **not** remove or refactor production `ui_doc` code before M5 selects a branch.

## Repository baseline

The production baseline for this workstream remains:

- `main` / `origin/main`: `4ed01eb64bc1851bb18c77bedb1cc614ccbb2a20`
- CLI release: `v0.2.0`
- Connector: `0.0.86`

Expected dirty workstream paths at this checkpoint are limited to benchmark/handoff material:

- `docs/handoffs/ACTIVE.md`
- `docs/handoffs/ui-doc-ab-reduction-2026-08-11.md`
- this checkpoint file
- `docs/benchmarks/ui-doc-ab/`
- `tools/benchmark-ui-authoring/`

No Hera production source should have been changed yet. Verify `git status --porcelain` fresh before continuing.

## Frozen benchmark rules

Do not change these because of an intermediate score:

- Unity: `6000.3.5f2`
- backend: uGUI
- model: `gpt-5.6-sol`
- reasoning: `medium`
- task time budget: 15 minutes per Codex authoring session
- tasks: T01/T02/T03 from `docs/benchmarks/ui-doc-ab/tasks/`
- oracles: `docs/benchmarks/ui-doc-ab/oracles/`
- 3 screening repetitions, 27 accepted cells total
- arm order rotates by repetition per manifest
- timeout is a **valid measured outcome**; score the exact Unity state at cutoff
- commands blocked by the arm shim are also a **valid usability/following cost** and do not earn a retry
- retry only infrastructure/audit failures that prevent a valid `run.json`
- do not cherry-pick cells from an infrastructure-invalid wave
- exact provider usage is recorded when Codex emits it; do not fabricate telemetry
- `ui_doc capture` is neutral measurement infrastructure only during M2-M4

Removal/retention thresholds remain exactly those in the canonical handoff and benchmark README.

## Why the benchmark architecture changed

The first formal wave used a copied Unity Hub 2D template. That wave is invalid because unrelated packages entered compile-error states during repeated Editor relaunches.

Invalid wave:

`docs/benchmarks/ui-doc-ab/results/screening-v021-20260811/`

Its `INVALID.md` explains the contamination. Never include any result from that wave in M2-M5, including the early cells that happened to finish.

The accepted architecture now uses:

- fixture profile: `minimal-ugui`
- ProjectSettings from Unity `6000.3.5f2`
- fixture-local Connector snapshot
- `com.unity.ugui`
- Unity built-in `com.unity.modules.*`
- no 2D/URP/IDE/Test Framework root packages
- GameObject-free `SampleScene.unity`
- GameObject-free `__HeraABReset.unity`
- Connector `Editor/Tests` and `Editor/TestRunner` removed only from the disposable snapshot

The minimal fixture currently reports `36` root dependencies.

## Single-Editor protocol

Do not relaunch Unity for every cell.

The accepted protocol is:

1. create one minimal disposable fixture;
2. build one pinned source CLI;
3. launch Unity once for the wave;
4. keep that exact Unity PID alive across all cells;
5. before every cell call `Reset-LiveFixture.ps1`;
6. live reset parks Unity on `__HeraABReset.unity`;
7. restore `SampleScene.unity` from the frozen baseline SHA;
8. remove generated benchmark Assets and previous agent scratch outputs;
9. refresh and reopen `SampleScene.unity`;
10. verify root count `0` and Console reset;
11. launch a fresh ephemeral Codex session for the cell;
12. score out-of-band;
13. repeat without restarting the Editor;
14. close the shared exact Unity PID only after the wave finishes or aborts.

Two-cell reuse smoke already PASSed on one Editor PID: `uidoc -> live reset -> primitives_batch`, with valid score/run artifacts, Scene Recovery backup `0`, and graceful final close.

## Harness files

Primary scripts:

- `tools/benchmark-ui-authoring/New-Fixture.ps1`
- `tools/benchmark-ui-authoring/Reset-Fixture.ps1`
- `tools/benchmark-ui-authoring/Reset-LiveFixture.ps1`
- `tools/benchmark-ui-authoring/Run-One.ps1`
- `tools/benchmark-ui-authoring/Run-Screening.ps1`
- `tools/benchmark-ui-authoring/Score-Run.ps1`
- `tools/benchmark-ui-authoring/Compare-Results.ps1`
- `tools/benchmark-ui-authoring/shim/hera-arm.ps1`
- `tools/benchmark-ui-authoring/shim/hera-agent-unity.cmd`

Latest frozen gate before the current formal wave:

`FINAL_SINGLE_EDITOR_GATE_PASS`

It covered PowerShell parsing, oracle totals, shim policy, minimal fixture reset, production-source untouched check, and `git diff --check`.

## Current accepted formal wave

Wave:

`docs/benchmarks/ui-doc-ab/results/screening-v021-minimal-reuse-20260811/`

Wave metadata at start:

- fixture profile: `minimal-ugui`
- fixture path: `C:\Users\PC\AppData\Local\Temp\hera-ui-ab-wave-4bb40e2e20be4974a286a62aa2680960\project`
- pinned CLI: `C:\Users\PC\AppData\Local\Temp\hera-ui-ab-wave-4bb40e2e20be4974a286a62aa2680960\bin\hera-agent-unity.exe`
- CLI SHA-256: `0ea29afb54c3b7bb1db8c9e638db2cb052683c23984e11210be6aa154ad3d346`
- Connector: `0.0.86`
- shared Unity PID at checkpoint: `63320`
- initial launch wall time: `14328.582 ms`
- baseline Scene SHA-256: `9ff1d0b3cbbbf451987285a4e1604909de2e5caa257dd88b53ffaff54c5090e7`
- user asset-config SHA-256: `dd468637e1bc07c3ec24ac7024e278a0f1be0b9b68b89f6961711ec7258bc888`
- `editor_reused_across_cells=true`

Important: PID/path values are checkpoint evidence, not durable identifiers. On continuation, inspect whether the wave is still running before acting.

## Current live execution state at this checkpoint

WorkForge shell that launched the accepted formal wave:

`shell_a072976c7a02d0499b4c0b8391a80ee2`

At 14:06 KST it was still running.

Do not cancel or replay another session's WorkForge shell merely to take ownership. First inspect whether the benchmark process/wave is still active.

Completed accepted cell so far:

### T01 / uidoc / repetition 1

Artifact:

`docs/benchmarks/ui-doc-ab/results/screening-v021-minimal-reuse-20260811/T01/uidoc/rep-01/attempt-01/run.json`

Result:

- `benchmark_valid=true`
- score: `84.067`
- `strict_pass=false`
- agent wall: `793336.659 ms`
- Hera calls: `72`
- mutation calls: `23`
- verification calls: `35`
- forbidden/blocked attempts: `3`
- stdout bytes: `76157`
- stderr bytes: `46068`
- estimated tool-result tokens: `30557`
- Codex input tokens: `4236386`
- cached input tokens: `4109824`
- output tokens: `24145`
- reasoning output tokens: `12224`
- recovery backup files: `0`
- `editor_reused=true`

Do not infer the M5 decision from this single result.

Cell in progress when this checkpoint was written:

`T01 / primitives / repetition 1 / attempt 1`

Its partial `hera-calls.jsonl` already existed and showed the agent moving from failed shorthand `manage_ui --params` attempts to the canonical `manage_ui create` path. Partial data is not a result and must not be scored manually. Wait for `run.json`.

## Continuation procedure

Start by reading:

1. repository `AGENTS.md` and `CLAUDE.md`;
2. `docs/handoffs/ACTIVE.md`;
3. this checkpoint;
4. `docs/handoffs/ui-doc-ab-reduction-2026-08-11.md`;
5. `docs/benchmarks/ui-doc-ab/README.md` and `manifest.json`.

Then inspect fresh state before issuing any mutation:

- `git status --porcelain`
- current Unity processes and their project command lines
- current accepted wave directory
- whether `wave.json` says `running`, `screening_complete`, or failed
- number/location of accepted `run.json` files
- whether the benchmark shell/process is still active
- Scene Recovery backup count for the disposable fixture if it still exists
- user asset-config SHA, which must still equal the frozen SHA above

### If the current wave is still running

Do **not** start another screening wave.

Observe it and let it continue. Do not change harness/protocol mid-wave. Do not edit production code.

### If the current wave completed successfully

Require 27 valid accepted cells, then run:

`tools/benchmark-ui-authoring/Compare-Results.ps1`

Use the frozen decision rules to classify exactly one of:

- retention candidate
- removal candidate
- borderline / confirmation required

Update the canonical handoff with raw evidence before beginning a production branch.

### If the current wave aborted because of infrastructure failure

Do not preserve a favorable subset as M2-M4 evidence.

1. mark that entire wave invalid with the exact failure reason;
2. verify the shared Unity process is gone or close only that exact disposable fixture PID gracefully;
3. require Scene Recovery backup count `0` before cleanup;
4. fix the infrastructure problem only;
5. rerun all frozen gates;
6. start a new uniquely named formal wave from cell 1.

Do not alter task prompts, scoring oracles, time budget, model, arm definitions, or decision thresholds after seeing results.

## M2-M5 status

- M0 benchmark definition: **PASS**
- M1 frozen task/oracle set: **PASS**
- M2 uidoc baseline: **IN PROGRESS**
- M3 primitives baseline: **IN PROGRESS** as part of rotated formal wave
- M4 primitives_batch baseline: **IN PROGRESS** as part of rotated formal wave
- M5 comparison/branch selection: **PENDING**

Production reduction branch: **NOT STARTED**.

## Safety / preservation rules

- Do not touch unrelated Unity projects or their processes.
- The benchmark Unity window may show the project name simply as `project`; that is the disposable temp fixture, not Hera/Inventoria.
- Do not change the user's global Hera Settings during the benchmark.
- Do not force-kill the benchmark Unity merely because a shell connection changes ownership.
- Do not modify or delete invalid-wave evidence until the workstream is complete.
- Do not commit/push/tag/release unless the user explicitly asks.
- Do not remove `ui_doc`, UITK schemas, `html-to-uidoc`, or move capture until M5 evidence selects that branch.
- Preserve all raw JSONL, captures, score files, provider usage, and invalid-wave notes.

## Suggested continuation prompt

From the Hera repository root:

```text
Read docs/handoffs/ACTIVE.md and the active checkpoint it points to. Then read docs/handoffs/ui-doc-ab-reduction-2026-08-11.md and docs/benchmarks/ui-doc-ab/README.md. Verify git status, current Unity processes, the accepted wave directory, wave.json, run.json count, the frozen asset-config SHA, and whether the existing formal screening process is still running. Do not restart or duplicate a live wave. Continue only M2-M5 with the frozen benchmark protocol. Do not modify production ui_doc code before M5 evidence selects a branch. If the wave completes, compare all 27 valid cells with Compare-Results.ps1 and update the canonical handoff with raw evidence. If infrastructure aborts the wave, mark the whole wave invalid and restart from cell 1 only after fixing infrastructure and rerunning gates. Preserve unrelated changes and do not commit/push/release without explicit instruction.
```
