# ui_doc Authoring Accuracy A/B

Status: M0/M1 frozen. Execution begins at M2.

Handoff: [`../../handoffs/ui-doc-ab-reduction-2026-08-11.md`](../../handoffs/ui-doc-ab-reduction-2026-08-11.md)

## Question

Does `ui_doc` authoring materially improve final uGUI implementation accuracy compared with Hera's generic UI authoring primitives?

This benchmark does not test whether `ui_doc` can build UI. It already can. It tests whether the dedicated IR and authoring pipeline produce a better final result often enough to justify their maintenance cost.

## Frozen environment

- Unity: `6000.3.5f2`
- Game View: `1280x720`
- backend: `ugui`
- CLI/Connector: current pinned source for one complete benchmark wave
- agent: same Codex CLI version, model, reasoning effort, sandbox policy, and prompt wrapper for every arm
- every run starts in a fresh disposable fixture state

The exact source commit, CLI build SHA-256, Connector package version, Codex version, model, reasoning setting, and fixture hash must be recorded in every result bundle.

## Arms

| Arm | UI mutation path | `batch` | `ui_doc` authoring |
|---|---|---:|---:|
| `uidoc` | `ui_doc apply` / `ui_doc export` | no | yes |
| `primitives` | `manage_ui`, `manage_components`, `manage_gameobject` | no | no |
| `primitives_batch` | same generic primitives | yes | no |

Common read and verification tools remain available in every arm.

`ui_doc capture` may be available to every arm only as ScreenSpaceOverlay visual verification. It must be logged separately and never used to mutate UI. This keeps visual feedback constant while isolating the authoring path.

## Task set

| ID | Task | Primary stress |
|---|---|---|
| T01 | Precision Mission HUD | exact geometry, text, color, interaction |
| T02 | Inventory Grid | repeated hierarchy and layout consistency |
| T03 | Crystal Forge static recreation | reference-image interpretation and visual repair loop |

Prompts:

- [`tasks/T01-precision-mission-hud.md`](tasks/T01-precision-mission-hud.md)
- [`tasks/T02-inventory-grid.md`](tasks/T02-inventory-grid.md)
- [`tasks/T03-crystal-forge-reference.md`](tasks/T03-crystal-forge-reference.md)

Oracles:

- [`oracles/T01.json`](oracles/T01.json)
- [`oracles/T02.json`](oracles/T02.json)
- [`oracles/T03.json`](oracles/T03.json)

Machine manifest: [`manifest.json`](manifest.json)

## Scoring

Every run returns:

- `strict_pass: true|false`
- `accuracy_score: 0..100`

Shared category budget:

| Category | Points |
|---|---:|
| required structure/components | 30 |
| geometry/layout | 30 |
| visible styling/text | 20 |
| interaction/raycast | 10 |
| cleanliness/final Editor state | 10 |

A task oracle breaks these category totals into deterministic criteria.

Critical failures force `strict_pass=false` regardless of weighted score.

## Result bundle

Each run gets a unique directory:

```text
results/<wave>/<task>/<arm>/<rep>/
```

Required files:

```text
run.json
agent-events.jsonl
hera-calls.jsonl
score.json
final-capture.png
final-annotations.json
console-errors.json
```

`run.json` must include:

```json
{
  "schema": "hera.ui-authoring-ab-run/1",
  "task": "T01",
  "arm": "uidoc",
  "repetition": 1,
  "repo_commit": "...",
  "cli_sha256": "...",
  "connector_version": "...",
  "unity_version": "6000.3.5f2",
  "codex_version": "...",
  "model": "...",
  "reasoning_effort": "...",
  "started_at": "...",
  "finished_at": "...",
  "wall_ms": 0,
  "hera_calls": 0,
  "mutation_calls": 0,
  "verification_calls": 0,
  "stdout_bytes": 0,
  "stderr_bytes": 0,
  "estimated_tool_result_tokens": 0,
  "provider_usage": null
}
```

Provider usage must stay `null` when exact telemetry is not available.

## Call logging

The benchmark shim records one JSONL row per Hera invocation before forwarding it:

```json
{
  "seq": 1,
  "started_at": "...",
  "argv": ["manage_ui", "create", "--element", "button"],
  "classification": "mutation",
  "allowed": true,
  "exit_code": 0,
  "wall_ms": 0,
  "stdout_bytes": 0,
  "stderr_bytes": 0
}
```

Forbidden calls are logged with `allowed:false` and are not forwarded.

For B/C, the shim rejects all `ui_doc` subcommands except `capture`. For A, `ui_doc apply`, `ui_doc export`, and `ui_doc capture` are allowed; generic UI mutation commands are rejected. Common read-only inspection is allowed in all arms.

For C, a `batch` payload must be inspected recursively before forwarding. Any contained `ui_doc` authoring command invalidates the batch.

## Run protocol

### Wave setup

1. build one pinned Hera CLI from the repository commit under measurement;
2. create a marker-verified **`minimal-ugui`** disposable fixture from Unity `6000.3.5f2` ProjectSettings;
3. keep only the fixture-local Hera Connector snapshot, `com.unity.ugui`, and Unity built-in `com.unity.modules.*` packages; exclude the Connector package-test assemblies from the disposable snapshot;
4. create two GameObject-free Scenes: `SampleScene.unity` for the task and `__HeraABReset.unity` as the live-reset parking Scene;
5. record the user's existing `asset-config.json` SHA-256 and require `ui_system=ugui` without changing any user-global Hera setting;
6. launch the exact Unity fixture **once**, record its PID and cold-start wall time, then perform one live baseline reset;
7. keep that same warm Editor process for every measured cell in the wave.

### Each measured cell

1. switch the shared Editor to `__HeraABReset.unity`, restore `SampleScene.unity` from the frozen baseline hash, remove benchmark-owned/generated Assets and prior agent scratch files, refresh, reopen `SampleScene.unity`, verify `rootCount=0`, and clear Console out-of-band;
2. verify the shared Editor PID and pinned user `asset-config.json` SHA are unchanged;
3. install the arm-enforcing Hera shim first on the fresh Codex session's child `PATH`;
4. launch a fresh ephemeral `codex exec` session with the frozen task prompt and the fixed 15-minute budget;
5. Codex runs in automation bypass mode because its platform exec-policy otherwise rejects the benchmark CLI before the shim; the disposable fixture, strict arm shim, JSONL call log, and post-run audit are the experiment safety boundary;
6. shim-blocked commands are recorded as measured usability/following failures and are **not** forwarded; they do not receive an automatic fresh attempt;
7. invalidate the cell only for infrastructure/audit failures such as actual MCP/WorkForge use, alternate Hera binary use, lost/shared Editor PID, fixture corruption, or non-timeout harness failure;
8. save complete Codex JSON event output and exact provider usage telemetry when present;
9. after Codex exits or times out, save the produced Scene out-of-band, run the scoring oracle with the read-only scene probe, and render every arm through the same neutral `ui_doc capture --width 1280 --height 720` measurement path;
10. read final Console errors and button-center EventSystem raycasts, then write `run.json`, `score.json`, state, capture, and call logs;
11. verify the exact shared Editor PID is still running and Scene Recovery backup count remains `0` before starting the next cell.

### Wave teardown

After all cells complete, close the **one shared benchmark Unity PID** through `CloseMainWindow`, verify Scene Recovery backup count `0`, and remove the marked disposable fixture only after no process still owns it.

No valid run may inherit a modified Scene, Console state, generated Asset, or agent scratch file from another arm. The same warm Library/package cache and Editor PID are deliberately reused because the comparison targets **authoring accuracy and agent wall time**, not Package Manager or Editor-startup variance. Cold UPM/launch cost is recorded separately at wave setup.

## Fixed task time budget

Every Codex authoring session gets exactly **15 minutes**. A timeout is a measured performance/accuracy result, not an infrastructure failure: the agent process is stopped at the cutoff and the exact Unity state at that moment is scored. Timeout runs are therefore valid when the arm policy/audit stayed clean. Only harness, fixture, API, policy, or other non-timeout execution failures are retried.

## Screening matrix

Three repetitions per arm/task:

```text
3 tasks x 3 arms x 3 repetitions = 27 runs
```

Rotate arm order per task/repetition. Example Latin-style rotation:

```text
rep1: uidoc -> primitives -> primitives_batch
rep2: primitives -> primitives_batch -> uidoc
rep3: primitives_batch -> uidoc -> primitives
```

This does not eliminate all model variance, but it prevents a fixed warmup/order pattern from always favoring one arm.

## Decision margins

### Removal candidate

After screening:

- identical total strict-pass count between `uidoc` and the best generic arm;
- overall mean score difference <= 3 points;
- no task generic deficit > 5 points;
- no generic-only critical failure pattern. During 3-repetition screening, a pattern means the same critical failure ID occurs in at least 2 runs of the best generic arm for the same task and in 0 `uidoc` runs for that task.

### Retention candidate

After screening, any of:

- `uidoc` overall mean advantage >= 8 points;
- `uidoc` advantage >= 10 points on one task;
- `uidoc` earns at least two extra strict passes across its 9 screening runs.

### Borderline / confirmation

Otherwise extend to 5 repetitions per arm/task, `45` total runs.

After confirmation, authoring is practically equivalent when:

- overall mean difference remains within 3 points;
- every per-task difference remains within 5 points;
- overall strict-pass difference is at most one run;
- no generic-only critical failure pattern appears.

Wall time, call count, and token/output measurements do not override a real accuracy loss. They are used to choose between B and C and to quantify the benefit of removal after accuracy equivalence is established.

## Post-decision verification

If removal is selected:

1. remove the authoring path and pre-extracted UI Toolkit schema bundles;
2. preserve/migrate any neutral capture capability still needed for visual verification;
3. rerun the same tasks without any `ui_doc` command available;
4. run the five Unity compatibility buckets for Connector changes;
5. record before/after production LOC, catalog bytes, CLI binary size, exact-source compile time, wall time, call count, output bytes, and accuracy.

The benchmark is only complete when the simplified implementation reproduces the decision benchmark result.
