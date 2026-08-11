# ui_doc Fast Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the approved one-hour `ui_doc` fast A/B protocol executable and reject any incomplete or invalid wave as decision evidence.

**Architecture:** Add the fast profile to the benchmark manifest as the single machine-readable source of truth. `Run-Screening.ps1` selects it only through `-Protocol fast`, locks the 12-cell schedule and time limits, and records a terminal `fast_complete`, `incomplete`, or `invalid` status. `Compare-Results.ps1` derives the expected cell matrix and decision rule from that recorded protocol instead of assuming the formal 27-cell matrix.

**Tech Stack:** PowerShell 7, JSON manifests, existing disposable Unity fixture and Hera CLI harness.

## Global Constraints

- Keep Unity `6000.3.5f2`, uGUI, `1280x720`, the frozen three task prompts/oracles, the model, and reasoning effort unchanged.
- Fast mode must use only `uidoc` and `primitives_batch`, two repetitions, 4-minute Codex sessions, one attempt per cell, 53-minute cell-admission cutoff, and a 60-minute wave deadline.
- Preserve the shared warm fixture PID, live reset, arm shim, asset-config SHA, raw events/calls, scorer, capture, Console check, and Scene Recovery check.
- A 4-minute authoring timeout and a shim-blocked call remain valid measured outcomes; a deadline overrun is incomplete; every infrastructure/audit fault invalidates the whole wave.
- Only a `fast_complete` wave with all 12 valid cells can receive a fast decision. Never reuse data from an invalid formal wave.
- Do not touch production `ui_doc`, Connector C#, Go CLI, or public Hera command help.
- Do not commit or push this follow-up change unless the user explicitly asks.

---

### Task 1: Encode and preflight the fast schedule

**Files:**
- Modify: `docs/benchmarks/ui-doc-ab/manifest.json`
- Modify: `tools/benchmark-ui-authoring/Run-Screening.ps1`
- Create: `tools/benchmark-ui-authoring/test-fast-protocol.ps1`

**Interfaces:**
- Consumes: `manifest.fast` with arms, repetitions, session timeout, attempts, order, admission cutoff, deadline, and decision margins.
- Produces: `Run-Screening.ps1 -Protocol fast -PlanOnly`, which emits one compact `hera.ui-authoring-ab-wave-plan/1` JSON object without creating a fixture, result directory, or Unity process.

- [ ] **Step 1: Write the failing fast-plan smoke**

```powershell
$plan = & pwsh -NoProfile -File $runner -Protocol fast -PlanOnly | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'fast plan did not run' }
if ($plan.expected_cells -ne 12) { throw 'expected 12 fast cells' }
if (@($plan.arms) -join ',' -ne 'uidoc,primitives_batch') { throw 'wrong fast arms' }
if ($plan.codex_timeout_minutes -ne 4 -or $plan.admission_cutoff_minutes -ne 53 -or $plan.wave_deadline_minutes -ne 60) { throw 'wrong fast time limits' }
if ((@($plan.cells | Where-Object { $_.repetition -eq 1 } | Select-Object -ExpandProperty arm) -join ',') -ne 'uidoc,primitives_batch') { throw 'wrong first order' }
if ((@($plan.cells | Where-Object { $_.repetition -eq 2 } | Select-Object -ExpandProperty arm) -join ',') -ne 'primitives_batch,uidoc') { throw 'wrong second order' }
```

- [ ] **Step 2: Run the smoke and observe the expected failure**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-protocol.ps1`

Expected: FAIL because `-Protocol` and `-PlanOnly` do not yet exist.

- [ ] **Step 3: Add a frozen `fast` profile and runner selection**

```json
"fast": {
  "arms": ["uidoc", "primitives_batch"],
  "repetitions": 2,
  "codex_timeout_minutes": 4,
  "max_attempts_per_cell": 1,
  "admission_cutoff_minutes": 53,
  "wave_deadline_minutes": 60,
  "order": [["uidoc", "primitives_batch"], ["primitives_batch", "uidoc"]]
}
```

```powershell
param(..., [ValidateSet('formal','fast')][string]$Protocol = 'formal', [switch]$PlanOnly)
if ($Protocol -eq 'fast') {
    # Reject bound overrides that drift from manifest.fast, then use its values.
}
if ($PlanOnly) { $plan | ConvertTo-Json -Compress -Depth 8; exit 0 }
```

Keep formal defaults and behavior unchanged. In fast mode, start the stopwatch before setup, reject a new cell at or after minute 53, cap each child `Run-One.ps1` process by the remaining hard deadline, and emit the applied profile/timestamps in `wave.json`.

- [ ] **Step 4: Add terminal-wave bookkeeping**

```powershell
catch {
    $waveMeta.status = if ($fastDeadlineExceeded) { 'incomplete' } else { 'invalid' }
    $waveMeta.terminal_reason = $_.Exception.Message
    [IO.File]::WriteAllText($waveJson, ($waveMeta | ConvertTo-Json -Depth 12) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    $terminalName = if ($waveMeta.status -eq 'incomplete') { 'INCOMPLETE.md' } else { 'INVALID.md' }
    [IO.File]::WriteAllText((Join-Path $waveDirectory $terminalName), "# $($waveMeta.status)`n`n$($waveMeta.terminal_reason)`n", [Text.UTF8Encoding]::new($false))
}
```

Use `fast_complete` only after exactly 12 valid cells; any non-timeout child failure, missing/malformed `run.json`, PID/config/recovery failure, or audit failure follows the invalid path.

- [ ] **Step 5: Re-run the smoke and override rejection checks**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-protocol.ps1`

Expected: PASS; the script must also verify that `-Protocol fast -CodexTimeoutMinutes 15` and `-Protocol fast -Repetitions 3` exit nonzero without starting Unity.

### Task 2: Make comparison protocol-aware

**Files:**
- Modify: `tools/benchmark-ui-authoring/Compare-Results.ps1`
- Create: `tools/benchmark-ui-authoring/test-fast-comparison.ps1`

**Interfaces:**
- Consumes: a terminal `wave.json` and one `run.json` plus `score.json` for every expected cell.
- Produces: `comparison.json` with `protocol`, `expected_runs`, all arm/task summaries, and exactly one fast decision: `retain_pending_simplification`, `reduction_candidate`, or `inconclusive`.

- [ ] **Step 1: Write a fake 12-cell fast-wave test**

```powershell
$comparison = & pwsh -NoProfile -File $comparer -WaveDirectory $wave | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'fast comparison failed' }
if ($comparison.protocol -ne 'fast' -or $comparison.expected_runs -ne 12) { throw 'fast matrix was not selected' }
if ($comparison.decision -ne 'reduction_candidate') { throw 'equal fast cells should be a reduction candidate' }
```

The test creates a marked `fast_complete` `wave.json` and six valid synthetic cells per arm. A second case adds a generic-only critical failure and expects `inconclusive`.

- [ ] **Step 2: Run the comparison test and observe the expected failure**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-comparison.ps1`

Expected: FAIL because the comparator still requires three arms × three repetitions and formal decision margins.

- [ ] **Step 3: Select arms, repetitions, and rules from wave protocol**

```powershell
$waveMeta = Get-Content -LiteralPath (Join-Path $wave 'wave.json') -Raw | ConvertFrom-Json
$config = if ($waveMeta.protocol -eq 'fast') { $manifest.fast } else { $manifest }
$armIds = @($config.arms | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.id } })
$expectedCount = [int]$manifest.tasks.Count * $armIds.Count * [int]$config.repetitions
```

Reject every `invalid`, `incomplete`, or wrong terminal status before aggregation. For fast mode, require equal strict counts, absolute overall delta `<= 2`, every absolute task delta `<= 4`, and no generic-only critical failure for `reduction_candidate`; select `retain_pending_simplification` at `>= 12` overall, `>= 15` on one task, or `>= 2` extra strict passes; otherwise select `inconclusive`.

- [ ] **Step 4: Re-run both comparison cases**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-comparison.ps1`

Expected: PASS; the equality case reduces and the generic-only critical case is inconclusive.

### Task 3: Document and run non-Unity gates

**Files:**
- Modify: `docs/benchmarks/ui-doc-ab/README.md`
- Modify: `tools/benchmark-ui-authoring/README.md`

**Interfaces:**
- Documents the exact command: `pwsh -NoProfile -File tools/benchmark-ui-authoring/Run-Screening.ps1 -Protocol fast -Wave <unique-wave-name>`.
- Documents that the former default remains formal and must not be used for this approved run.

- [ ] **Step 1: Document the exact fast invocation and terminal statuses**

Add a short Fast protocol section that links to `docs/superpowers/specs/2026-08-11-ui-doc-fast-ab-design.md`, names the 12-cell/4-minute/53-minute/60-minute limits, and says only `fast_complete` is eligible for comparison.

- [ ] **Step 2: Run all non-Unity validations**

Run:

```powershell
pwsh -NoProfile -File tools/benchmark-ui-authoring/test-shim.ps1
pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fixture.ps1
pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-protocol.ps1
pwsh -NoProfile -File tools/benchmark-ui-authoring/test-fast-comparison.ps1
git diff --check
```

Expected: all scripts print their PASS marker and `git diff --check` is clean.

- [ ] **Step 3: Perform a no-Unity manual preflight**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/Run-Screening.ps1 -Protocol fast -PlanOnly`

Expected: compact JSON shows exactly 12 cells, only the two retained arms, 4-minute sessions, a 53-minute admission cutoff, and a 60-minute hard deadline. No fixture directory, result directory, or Unity process is created.

### Task 4: Launch only the validated fast wave

**Files:**
- Create at runtime: `docs/benchmarks/ui-doc-ab/results/<fast-wave>/...`

**Interfaces:**
- Consumes: all passing non-Unity gates and `Run-Screening.ps1 -Protocol fast`.
- Produces: raw per-cell artifacts plus terminal `wave.json`; subsequent comparison consumes only a `fast_complete` wave.

- [ ] **Step 1: Re-check live process ownership and configuration immediately before launch**

Run the Hera bootstrap sequence, inspect `wave.json` directories, confirm no benchmark Unity process is alive, and verify `asset-config.json` SHA with `ui_system=ugui` without changing it.

- [ ] **Step 2: Start one named fast wave**

```powershell
pwsh -NoProfile -File tools/benchmark-ui-authoring/Run-Screening.ps1 `
  -Protocol fast `
  -Wave screening-v021-fast-<timestamp> `
  -Model gpt-5.6-sol `
  -ReasoningEffort medium
```

Expected: no more than 12 fresh authoring sessions; a non-terminal interruption writes raw artifacts and an `INVALID.md` or `INCOMPLETE.md`; no selected subset is compared.

- [ ] **Step 3: Aggregate only a completed wave**

Run: `pwsh -NoProfile -File tools/benchmark-ui-authoring/Compare-Results.ps1 -WaveDirectory docs/benchmarks/ui-doc-ab/results/<fast-wave>`

Expected: either one of the three fast decisions or an explicit nonzero incomplete/invalid refusal. Do not modify production `ui_doc` in this task.

## Plan Review

- Spec coverage: Tasks 1 and 2 implement every frozen fast matrix, timing, validity, and decision rule; Task 3 preserves the harness gates; Task 4 records the operational sequence without changing production UI code.
- Placeholder scan: no TBD/TODO markers; every code task has a target file, concrete command, expected result, and test-first step.
- Consistency: `Protocol=fast`, `manifest.fast`, `fast_complete`, `expected_runs=12`, and the three fast decision labels are shared between the runner, comparator, tests, and documentation.
