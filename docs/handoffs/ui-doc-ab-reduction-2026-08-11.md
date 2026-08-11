# Hera v0.2.1 ui_doc Accuracy A/B and Reduction

Date: 2026-08-11

Target release: CLI `v0.2.1` planning workstream. Connector version is independent and will only change if Connector source changes.

## Goal

Answer one product question with measured evidence:

> Does `ui_doc` authoring materially improve final Unity UI implementation accuracy compared with Hera's generic UI primitives?

If the answer is no, remove the unnecessary `ui_doc` authoring surface and version-specific UI Toolkit schema maintenance, then remeasure Hera's size and speed. If the answer is yes, keep only the part that earns its maintenance cost and simplify the version-targeted schema path.

This is a new workstream. It does not reopen the completed vNext capability migration.

## Current baseline

Repository baseline before this workstream:

- `main` / `origin/main`: `4ed01eb64bc1851bb18c77bedb1cc614ccbb2a20`
- CLI release: `v0.2.0`
- Connector source: `0.0.86`
- working tree: clean

Measured `ui_doc` footprint before the benchmark:

- direct production code: about `4,038` nonblank LOC
  - CLI-side `ui_doc` + `html-to-uidoc`: `946`
  - uGUI authoring/capture/fixer path: `1,951`
  - UI Toolkit authoring/schema path: `1,141`
- direct implementation + focused tests inspected: `4,768` nonblank LOC across 14 files
- live tool catalog contract: `14,817` normalized bytes, 5 actions
- current full tool catalog: `195,037` normalized bytes
- synthetic Windows CLI build without the two UI-doc CLI implementations: about `646,656` bytes smaller (`3.85%`)
- synthetic Unity 6000.3 Roslyn compile with the eight direct `ui_doc` C# production files removed and minimal stubs inserted:
  - full average: `1,133.02 ms`
  - no-ui-doc average: `1,111.60 ms`
  - observed compile-only difference: about `21.4 ms` (`1.9%`)

Important interpretation:

- `ui_doc` is a large maintenance surface.
- It is not proven to be a large always-on runtime or compiler bottleneck.
- There is currently no retained A/B benchmark proving that `ui_doc` improves final UI accuracy.
- The `ui_doc` catalog surface is unchanged between released `v0.1.4` and `v0.2.0`; the v0.2.0 code growth did not come from `ui_doc`.

## Primary hypothesis

H0, removal hypothesis:

`ui_doc` authoring does not produce a practically meaningful final-accuracy advantage over generic Hera UI primitives when all arms receive the same observation and verification capability.

H1, retention hypothesis:

`ui_doc` authoring produces a repeatable accuracy advantage large enough to justify its dedicated IR, implementation code, tests, documentation, and version-specific UI Toolkit compatibility work.

## Benchmark scope

The first accuracy A/B intentionally isolates authoring-path effects:

- Unity: `6000.3.5f2`
- UI backend: uGUI only
- Game View: `1280x720`
- same Hera source/Connector for every arm
- same Codex model and reasoning setting for every run
- fresh Codex session for every run
- fresh disposable Unity fixture state for every run
- same frozen user prompt and reference asset per task
- no direct scene-YAML editing
- no arbitrary `exec` UI authoring
- no project scripts that generate the UI

Unity-version comparison is deliberately excluded from M0-M5. Mixing version differences into the first benchmark would prevent attribution of an accuracy delta to `ui_doc` itself.

## Arms

### A. `uidoc`

UI mutation path:

- `ui_doc apply`
- `ui_doc export` when a structured read is needed

Common read/verification tools are allowed.

### B. `primitives`

`ui_doc` authoring is forbidden.

UI mutation path:

- `manage_ui`
- `manage_components`
- `manage_gameobject` where needed

`batch` is forbidden so this arm measures the granular generic path.

### C. `primitives_batch`

Same generic mutation capabilities as B, but `batch` is allowed and encouraged for related operations.

This arm answers a second question: whether the main productivity advantage of `ui_doc` can be recovered with generic batched operations instead of a dedicated UI IR.

## Common verification capability

All three arms must have the same ability to inspect and verify the result:

- `status`
- `scene`
- `find_gameobjects`
- read-only `manage_components`
- read-only `manage_ui`
- `console`
- `screenshot --annotations_only` / `--annotate_ui`
- `input inspect` and other non-authoring UI QA

ScreenSpaceOverlay visual capture is held constant as measurement infrastructure. During M2-M4, `ui_doc capture` may be exposed to every arm **only as a visual verifier**, not as an authoring path. Its calls are logged separately and do not count as `ui_doc` authoring use.

If the removal branch is selected, the useful overlay-capture capability must be migrated to `screenshot` or a neutral capture helper before the `ui_doc` tool is deleted. A final full-removal smoke then runs without any `ui_doc` command.

## Agent-run isolation

The benchmark runner must enforce the arm, not merely ask the model to comply.

Planned enforcement:

- put a benchmark shim named `hera-agent-unity` first on the child Codex `PATH`;
- forward allowed commands to the pinned real Hera binary;
- log timestamp, argv, exit code, stdout bytes, stderr bytes, and wall time for every call;
- reject forbidden authoring commands before they reach Unity;
- for `batch`, inspect every contained command and reject `ui_doc` authoring operations in B/C;
- do not expose the scoring oracle to the model prompt.

Any run that bypasses or violates the arm policy is invalid and must be rerun from a clean fixture.

## Accuracy score

Every task produces two primary outcomes:

1. `strict_pass`: all critical acceptance criteria passed.
2. `accuracy_score`: integer `0..100`.

Shared score budget:

- structure and required objects/components: 30
- geometry and layout: 30
- visible styling/text: 20
- interaction/raycast behavior: 10
- cleanliness: one Canvas/root policy, no unwanted duplicates, zero final Console errors: 10

Task-specific oracles may redistribute sub-points inside these five categories, but the category totals stay fixed so tasks remain comparable.

A critical failure forces `strict_pass=false` even when the weighted score is high. Critical examples include missing required visible text, missing required control, duplicate root Canvas, a required button that cannot receive EventSystem input, or final Console errors.

## Performance and cost metrics

Accuracy decides retention. Performance explains the tradeoff.

Record per run:

- end-to-end wall time
- Hera command count
- mutation-command count
- verification-command count
- `batch` item count
- stdout bytes
- stderr bytes
- estimated tool-result tokens: `ceil((stdout_bytes + stderr_bytes) / 4)`
- provider/model usage telemetry when Codex exposes it
- repair-loop count
- final Console error count
- final object count
- duplicate/unwanted object count

Do not fabricate model-token totals when telemetry is unavailable.

## Repetition and decision rule

### Screening

Run every task 3 times per arm with fresh sessions and fixtures.

Current task count: 3.

Initial screening matrix: `3 tasks x 3 arms x 3 repetitions = 27 runs`.

Arm order must be rotated or randomized so cache/warmup/order does not systematically favor one arm.

### Practical significance thresholds

Removal candidate after screening when all are true:

- best generic arm and `uidoc` have identical strict-pass counts overall;
- absolute overall mean accuracy difference is at most 3 points;
- no task has a generic mean accuracy deficit greater than 5 points;
- generic runs introduce no unique critical failure mode.

Retention candidate after screening when any is true:

- `uidoc` overall mean accuracy advantage is at least 8 points;
- `uidoc` mean advantage is at least 10 points on any one task;
- `uidoc` produces at least two additional strict passes across the 9 per-arm screening runs.

Everything else is borderline.

### Confirmation

For a removal candidate or borderline result, extend to 5 repetitions per task/arm (`45` total runs).

After confirmation, treat the authoring paths as practically equivalent only when:

- overall mean accuracy difference remains within 3 points;
- no task differs by more than 5 points;
- strict-pass rate difference is at most one run overall;
- no generic-only critical failure pattern appears;
- the best generic arm does not regress median wall time by more than 15% without a compensating maintenance/cost benefit documented in M5.

If `uidoc` retains a repeatable accuracy advantage above those margins, keep the minimal authoring core and investigate how to remove pre-extracted version bundles without losing that advantage.

## Tasks

Frozen task manifest:

- `T01` precision HUD from a textual specification
- `T02` repetitive inventory grid from a textual specification
- `T03` static Crystal Forge recreation from a reference image

Task prompts live under `docs/benchmarks/ui-doc-ab/tasks/`.
Scoring oracles live under `docs/benchmarks/ui-doc-ab/oracles/`.

The existing Crystal Forge image is reused only as a visual reference:

`docs/benchmarks/user-scenario/assets/crystal-forge-win-6000.3.5f2.png`

The earlier gameplay benchmark result is not reused as an A/B accuracy score.

## Milestones

### M0: benchmark definition

Status: **PASS**

Exit criteria:

- hypothesis frozen;
- arms frozen;
- common verification boundary frozen;
- accuracy metrics frozen;
- repetition/decision rule frozen;
- benchmark version and backend isolated.

### M1: identical UI task set

Status: **PASS**

Exit criteria:

- three frozen task prompts written;
- independent scoring oracles written;
- reference asset pinned by path and SHA-256 where applicable;
- no task requires functionality available to only one authoring arm.

### M2: `uidoc` baseline

Status: **PENDING**

Run A across the frozen task set and store raw command logs, run summaries, captures, and scores.

### M3: generic primitives baseline

Status: **PENDING**

Run B against fresh copies of the same fixtures/prompts.

### M4: generic batch replacement

Status: **PENDING**

Run C against fresh copies of the same fixtures/prompts.

### M5: accuracy/time/call/token comparison

Status: **PENDING**

Produce the decision table and select exactly one branch.

## Branch A: meaningful `ui_doc` accuracy advantage

If M5 shows a material advantage:

1. keep only the authoring pieces that contribute to the measured advantage;
2. remove unrelated convenience surface where possible (`catalog`, `sample`, `html-to-uidoc`, etc. are not protected by an authoring-accuracy win unless separately measured);
3. replace or minimize pre-extracted UI Toolkit version bundles where runtime reflection can supply equivalent validation;
4. retain exact-source five-bucket compatibility gates for Connector code changes;
5. remeasure code size, catalog bytes, compile time, binary size, and benchmark accuracy.

## Branch B: no meaningful accuracy advantage

If M5 shows practical equivalence:

1. remove `ui_doc` authoring surface aggressively;
2. remove `html-to-uidoc` if no independent benchmark justifies it;
3. remove version-specific `uitk_schema_*` bundles and their extraction/maintenance path;
4. move any still-useful neutral capabilities, especially ScreenSpaceOverlay capture, to a generic verification surface;
5. route UI construction through `manage_ui` / `manage_components` / `manage_gameobject` plus `batch`;
6. update agent guidance so generic primitives plus visual/input verification are the canonical UI loop;
7. run the five Unity compile buckets if Connector source changes;
8. rerun the three benchmark tasks without any `ui_doc` command;
9. remeasure production LOC, catalog bytes, CLI binary size, exact-source compile time, command counts, wall time, and accuracy.

## Non-goals

- Do not remove `ui_doc` before M5 evidence.
- Do not redesign all Hera UI tools during the measurement phase.
- Do not mix UI Toolkit version behavior into the first uGUI authoring A/B.
- Do not use this workstream to rewrite the HTTP bridge, tool registry, or `batch` semantics.
- Do not claim statistical significance from one run.
- Do not optimize for fewer lines if accuracy or failure recovery measurably degrades.

## Progress log

### 2026-08-11: M0 and M1 opened and frozen

- User approved the full M0-M5 A/B flow and the conditional reduction branch.
- Existing `ui_doc` footprint and synthetic compile/binary measurements were reviewed before defining the benchmark.
- The benchmark is explicitly an **authoring accuracy** comparison, not a claim that `ui_doc` is an always-on runtime bottleneck.
- uGUI on Unity `6000.3.5f2` is the first controlled environment.
- `ui_doc capture` is held constant as neutral measurement infrastructure during M2-M4 so visual verification capability does not bias the authoring comparison.
- Three task prompts and independent oracles are the M1 frozen task set.
- Oracle validation PASS: all three tasks sum to exactly 100 points and each category matches the frozen 30/30/20/10/10 budget.
- Crystal Forge reference SHA-256 re-read PASS: `1383b9d1175c4777ee866be24617287e5728fc5fec92503cf4c19fa78f5742f7`.
- Added `tools/benchmark-ui-authoring/shim/hera-arm.ps1` plus the `hera-agent-unity.cmd` front shim. The policy is enforced before forwarding to the real CLI and logs every allowed/forbidden invocation as JSONL.
- Shim policy smoke PASS against installed CLI `v0.2.0`: allowed `version` forwarded; `uidoc -> manage_ui create`, `primitives -> ui_doc apply`, `primitives -> batch`, and `primitives_batch -> batch containing ui_doc` all rejected with benchmark exit code `78` before Unity execution.
- Next M2 preparation: implement reproducible fixture reset + out-of-band oracle scorer + one-run Codex orchestrator. Do not start the 27-run matrix until those three infrastructure smokes pass.

### 2026-08-11: benchmark harness and first formal wave correction

- The out-of-band scorer passed a real Unity `6000.3.5f2` smoke: read-only state probe, approval continuation, neutral `1280x720` overlay capture, Console read, EventSystem evidence, oracle scoring, and Scene Recovery backup `0` all worked. An intentionally incomplete Canvas scored `16.333/100` and strict-failed as expected.
- The arm shim now identifies only the actual top-level Hera command, so `list --tool ui_doc` no longer looks like a `ui_doc` invocation. It also owns the frozen experiment boundary: `exec`, `html-to-uidoc`, Editor lifecycle, Scene lifecycle mutation, asset-config mutation, Console clear, menu mutation, off-surface `manage_*`, and unsafe/off-surface batch contents are rejected before Unity execution. Typed `call ui_doc` is allowed only for neutral `action=capture`.
- Codex CLI `0.147.0` rejects the benchmark Hera command through its platform command policy before a PATH shim can run. The benchmark therefore uses Codex automation bypass mode inside the external safety boundary formed by the disposable fixture, strict Hera shim, JSONL call log, and post-run MCP/alternate-binary audit.
- The user's shared `asset-config.json` is no longer changed by the benchmark. A previous interrupted smoke was recovered exactly to SHA-256 `dd468637e1bc07c3ec24ac7024e278a0f1be0b9b68b89f6961711ec7258bc888`; new runs only pin and re-check the existing SHA and require its current `ui_system=ugui`.
- The first formal wave, `results/screening-v021-20260811/`, is explicitly **INVALID** and excluded from M2-M5. It copied the full Unity Hub 2D template manifest; unrelated Performance Test, 2D, Rider/Visual Studio, and other registry packages later entered compile-error states during repeated Editor relaunch, preventing the Hera heartbeat for `primitives_batch`. `INVALID.md` records why even the early successful cells are excluded rather than selectively retained.
- Replaced the fixture with `minimal-ugui`: ProjectSettings from the same Unity version, an empty GameObject-free task Scene, a second empty live-reset parking Scene, fixture-local Connector, uGUI, and built-in `com.unity.modules.*` packages only. Package-test directories are removed only from the disposable Connector snapshot, so the benchmark does not pull Unity Test Framework into the compile graph.
- Minimal fixture static gate PASS: no unrelated root package dependencies, no GameObject/script references in either Scene, no Connector package-test assembly surface, deterministic baseline reset, and unmarked reset refusal.
- Minimal fixture live launch reached Hera ready state in about 15 seconds with Console error match `0`.
- Added `Reset-LiveFixture.ps1`: while one exact benchmark Editor remains open, park on `__HeraABReset.unity`, restore `SampleScene.unity` by SHA, remove generated Assets and previous agent scratch files, refresh, reopen the task Scene, verify `rootCount=0`, and clear Console out-of-band. Live smoke PASS after creating a Canvas/button: root count returned `2 -> 0`, scratch outputs were removed, Console matched `0`, and Scene Recovery remained `0`.
- Reworked the measurement architecture to launch Unity **once per wave** and reuse that exact PID across all cells. Every cell still gets a fresh Codex session. This removes Package Manager/import/restart variance while the live-reset protocol preserves independent task state.
- Two-cell reuse smoke PASS on one Editor PID `12500`: one-minute `uidoc` cell -> live reset -> one-minute `primitives_batch` cell, both emitted valid run/score artifacts with `editor_reused=true`, then the shared Editor closed gracefully and left Scene Recovery backup count `0`.
- M2/M3/M4 remain PENDING. The next accepted screening wave must start from the minimal single-Editor protocol and complete all 27 valid cells before M5 can choose a branch.
