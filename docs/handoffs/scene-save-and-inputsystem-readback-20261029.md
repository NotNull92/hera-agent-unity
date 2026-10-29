# Scene save and Input System readback — verification complete

Status: **PASS for the requested Unity 6000.6.6f1 scope**. Unity 2022.3 remains **deferred**. The approved warning gate is Hera assemblies with 0 errors, 0 CS warnings and no new warnings, including UAC analyzers, versus main on the same Editor. Existing UAC diagnostics are retained; UAC removal is **별도 작업**.

## Candidate and evidence locations

- Base: `main@867bf2dfaa9ded351cd40daac75bbb76a7a04823`.
- Branch: `fix/scene-save-and-inputsystem-readback`, in a separate worktree. The Developer Mode checkout was preserved.
- Final implementation: **`e85b7f1851fa5a316c83f605b47321e9e227ce3c`**, Connector **0.2.14**. Final verification used committed exports, never a working-tree package.
- Unity **6000.6.6f1**, macOS arm64; optional Input System **1.20.0**. The fresh normal-install pass has neither Input System nor testables.
- Installed CLI **v0.0.38** was not replaced. Verification also used a scratch CLI built from the branch for the current typed-call/catalog surface; its commands, path and hash are retained locally. CLI source behavior was unchanged; help text was updated.

The external compatibility evidence bundle contains `defects/implementation/`. All artifact paths below are relative to that directory. It retains `d0/` (main baseline), `candidate/` (first implementation), `final/` (D5 and reentrant-cleanup checks), `completion/` (fresh final export and remaining checks), `go-checks/`, `go-checks-d5/`, and the bounded probe/command scripts. Machine-specific paths appear only in those local logs.

## Verification summary

| Required step | Result | Evidence and scope |
|---|---|---|
| 1. Go checks | PASS | `go-checks/` and `go-checks-d5/`: format, lint, lint-format diff, `go test ./...`, generated-guide check and Connector package validation; all exit 0. Tests used a real-path scratch TMPDIR. The final reentrant fix changes only C# and was compiled and tested in Unity. |
| 2. Exact exported SHA | PASS | `completion/exported-sha.txt`: e85b7f1 exported into a fresh project without a copied Library. `final/exported-sha-v2.txt` identifies the same source used for input/testables checks. |
| 3. D0 baseline | PASS | Forced-update/reset hypothesis confirmed for scroll and delta. `d0/logs/d0-excerpts.json`, `Editor.log`, `player-trace.tsv`, and logging-only `instrumentation.patch`. |
| 4. 6000.6.6f1 gate | PASS | Fresh normal installation ready; Hera errors 0, CS warnings 0, new warnings 0. Test source references 0 and test assembly absent in normal installation. Final testables: **48 total, 47 passed, 0 failed, 1 skipped**. Catalog unchanged. |
| 5. Post-fix input matrix | PASS | Dynamic/readback/settle, focused and unfocused Game view, Fixed, Manual timeout, pending pause/cancel, grouped transients, pulses and record/replay; artifacts below. Inactive-application timeout is classified separately as an environment condition. |
| 6. Scene matrix | PASS | Dirty untitled rejection with no Save dialog; clean no-op; named dirty save; mixed save_all refusal before partial write. `completion/logs/finish-scene-assertions.json` and per-command/fixture logs. |
| 7. Unity 2022.3.62f2 | deferred | Developer Mode branch is not merged into this branch. No 2022.3 execution or PASS claim. |
| D5 additions | PASS | Same-frame replay `[0,1]`; Shift+A+scroll in one player frame; main two-key baseline measured; pulse boundaries; existing input tests and full testables rerun; no new warnings. |

The one skipped test is `ReleaseGateTests.AnimationTimeline`; Timeline is not installed in this fixture. `ReleaseGateTests.InputQa` and `SceneGameObject` passed. InputQa runs the sequence and recording contract suites, including the new zero-settle/pulse-boundary and same-frame loader regressions. Full results are in `final/logs/33-v2-tests.json`.

The final live catalog has **34 tools / 133 actions** and hash `sha256:5d966c220de01b8642658e74c5e6b7950165332294c633db37508ab2bf6dd18e`, equal to the repository baseline. Only the expected project/domain identity fields differ between raw live documents. Contract fields, descriptions and normalized payload have zero delta; no baseline regeneration was needed. See `completion/logs/catalog-report.json` and `catalog-comparison.json`.

## Warning gate and installation stages

The initial literal zero-warning gate was blocked by main's existing diagnostics. The user approved the baseline-relative gate in commit `9898297`. No diagnostics were suppressed or cleared to obtain a pass. The counts below are console entries for each installation/compilation phase, not a sum of unique source diagnostics; repeated analyzer emissions can produce different entry counts.

| Installation phase | Observed warnings | New warnings / CS warnings / Hera errors |
|---|---:|---|
| Initial normal candidate | 30 entries | 0 / 0 / 0; all exact locations/messages matched D0 after export-root normalization |
| D5 normal candidate | 30 entries | 0 / 0 / 0 |
| D5 testables incremental compile | 8 entries | 0 / 0 / 0; identical to main testables |
| Final e85b7f1 input + testables compile | 34 entries, 21 distinct | 0 / 0 / 0 |
| Fresh final e85b7f1 normal installation | 44 entries, 16 distinct | 0 / 0 / 0 |
| Final completion Input System installation | 1 existing Input Manager deprecation entry | 0 / 0 / 0 |

Main comparison uses the same 6000.6.6f1 Editor and baseline source logs, including its test-enabled compilation. `completion/logs/final-warning-diffs.json` retains the final per-stage comparisons. `normal-install-assertions.json` also confirms no test assembly or test source inclusion in ordinary installation. Earlier comparisons remain in `candidate/logs/` and `final/logs/`.

UAC remediation is **별도 작업**. No assembly-loading or reflection migration was attempted in this branch. The independent-gates document was not changed: it makes no warning-count claim requiring correction.

## D0: measured cause

Baseline settings: Dynamic updates; `PointersAndKeyboardsRespectGameViewFocus`; `ResetAndDisableNonBackgroundDevices`; `runInBackground=False`; `activeInputHandler=Both`; application focused. The export was main plus logging-only instrumentation. Its probe recorded input before/after callbacks and player Update/FixedUpdate.

| Input | Baseline observation |
|---|---|
| A down/up | Forced Dynamic applies at frames 12516 / 12562; state survives into player Update, but the down response reports false. |
| Move (320,200) | Forced Dynamic apply at frame 12617; player position persists, but response reports (0,0). |
| Click | Player observes down and up after forced applies at frames 12670 / 12725. |
| Scroll (0,3) | After forced update 25562 at frame 12778: (0,3). Natural update 25563 at frame 12779 and player Update: (0,0). |
| Delta (12,-4) | After forced update 25661 at frame 12827: (12,-4). Natural update 25662 at frame 12828 and player Update: (0,0). |

Thus the transient value survives its apply update and is reset by the next natural update before player Update. Delta's previously unmeasured failure was confirmed. This supports removing the extra forced input-only update rather than changing global input policy.

## Input results and D5

The final implementation applies in a natural configured update and captures the existing physical snapshot in that same update. Captured values survive the subsequent settle wait. The single **2000 ms wall-clock deadline per change/group** permits slow Editor frames while bounding an unpumped Manual update independently of settle_frames. It does not promise completion while Unity's main thread itself cannot run.

| Case | Final result / artifact |
|---|---|
| Dynamic key, move, press, click | Correct player observations and response values; release response false. `final/logs/v2-dynamic-assertions.json` and corresponding command/probe logs. |
| Scroll with settle 0, 1, 5 | Response (0,3); exactly one player Update observes it; next frame returns to 0. Same dynamic assertions. |
| Delta (12,-4) | Correct response; one player Update, next frame 0. Same dynamic assertions. |
| Unfocused Game view, application active | Key delivery/readback preserved with the default policy. `v2-focus-fixed-assertions.json`. |
| Fixed | Key and scroll observed in FixedUpdate; scroll also remained visible in Update until the next fixed reset. Same focus/fixed artifact records both observations. |
| Manual without a pump | Bounded failure with no remaining apply; later explicit fixture pump sees no late input. Group timeout about 2.18 s including CLI overhead. |
| Pending group cancel / pause | About 0.21 / 0.19 s in the measured run; callbacks restored to baseline, no owned holds or late input. `v2-group-lifecycle-assertions.json`. |
| Same-frame replay | Recorded A-down + scroll together, then A-up: player frames 958 / 958 / 959; gaps **[0,1]** preserved. `v2-group-assertions.json`. |
| Shift+A+scroll group | Both keys and scroll observed together in player frame 935. Same group artifact. |
| Press/click boundaries | Existing down/hold/release retained, including 50 ms + 50 ms pulse holds with Shift held around them. Same group artifact. |
| Record/replay round trip | Five core keyboard/scroll events preserve relative frames **[0,0,0,1,1]**, no duplicate core events. `v2-roundtrip-assertions.json`; recording and complete traces retained. |
| Combined scroll + delta group | A held, scroll (0,3) and delta (12,-4) occur together once; completed count 5, cleanup succeeds, final held sets empty. `completion/logs/finish-group-transient-assertions.json`. |

Unless prefixed with `completion/`, the artifacts in that table are under `final/logs/`. Probe settings are recorded in the associated `qa-*.json` files. The scratch probe caps rendering at targetFrameRate 60; it does not change input focus/background policy.

D5 was explicitly approved after the first per-step natural-update implementation changed same-frame replay gaps `[0,1]` to `[1,1]`. The common sequence/replay executor now groups instantaneous steps connected by settle 0, applies them in order in one before-update callback, and captures once after that update. The user separately confirmed that press/click are group boundaries. Standalone keyboard/mouse commands retain their behavior. Nonzero settle counts remain **Editor updates**, including replay gaps; they are not a promise of player-frame timing.

The main baseline was measured, not assumed: Shift and A both applied at frame 1719 with `forced=1`; player Update at frame 1720 saw both held. `d0/logs/baseline-chord-summary.json` and `Editor-group-baseline.log` establish the regression comparison.

A further reentrant case found that an InputAction callback could pause the Editor during the first write, causing cleanup to resume before ownership was registered. Commit `e85b7f1` defers task continuations until the current write finishes and stops later group changes after completion. The final regression reports exactly one applied step, releases only A, leaves actual held sets empty, and never applies the later scroll: `final/logs/v2-reentrant-assertions.json`. The failing predecessor evidence remains in `group-reentrant-pause.json` / `group-reentrant-state.json`.

## Final script failure classification

The interrupted `v2-group-transients` run was an **environment condition**, not a product failure: `applicationFocus=False`, `focusedWindow=null`, `runInBackground=False`; its trace contains only Editor input updates and a stationary player frame. The call returned `INPUTSYSTEM_INVOCATION_FAILED` after the deadline with completed_count 0, empty held sets and baseline callback counts. See `final/logs/v2-group-transients.json`, `qa-v2-no-update-observe.json`, and its probe trace.

A later harness assertion correctly refused to treat an unfocused app as a focused test. After a fresh exact-export project restored the focused condition, the remaining combined-input case passed (`completion/logs/qa-finish-focused.json` and `finish-group-transient-assertions.json`). Input policy was never relaxed to make this pass.

Scratch preparation failures are retained separately: a probe compiled before package resolution, an invalid bitfield-writing fixture, and a harness parsing a human-readable response as JSON. They were corrected and excluded from product PASS/FAIL assertions. No failed fixture with zero observed events was accepted as a passing replay case.

## Scene verification and modal audit

Final scene commands and fixture state are retained under `completion/logs/finish-*`:

- Dirty untitled save returns `SCENE_UNSAVED` immediately, remains dirty, and the Editor remains ready. Native window inspection after rejection showed only the standard `Untitled - Project - Windows, Mac, Linux - Unity 6.6 (6000.6.6f1) <Metal>` window, with no Save dialog.
- Clean untitled save returns saved=false.
- Named dirty scene save returns saved=true, clears dirty state and changes the saved file hash.
- Mixed save_all returns SCENE_UNSAVED, keeps both scenes dirty and preserves the previously saved file's SHA-256. No partial write occurs.

| Path | Scope / result |
|---|---|
| `Tools/ManageScene.cs`, SaveScene(scene) | Dirty empty path rejected before Unity can open a dialog; verified |
| `Tools/ManageScene.Authoring.cs`, SaveScene(scene, path) | Explicit validated path; preserved |
| `Tools/ManageScene.Authoring.cs`, SaveOpenScenes | Existing dirty-untitled preflight; mixed-scene no-partial-write regression passed |
| SaveCurrentModifiedScenesIfUserWantsTo | No direct Connector call found |
| Settings-window DisplayDialog calls | UI save/auto-detect failure handling; outside typed scene scope |
| Generic menu and exec | Explicitly requested dialogs remain possible; out of scope, with help/guide caution |

## Limits and cleanup

- Unity 2022.3 is deferred. No support claim or version-floor change is made by this branch.
- The optional Timeline suite was skipped because the package is absent. Other optional package combinations are not implied by this result.
- Nonzero settle counts use Editor ticks. Manual input needs the project's own pump; an inactive app with a stopped player loop can fail honestly at the deadline. Physical OS input delivery is not claimed.
- The public catalog, error-code set and response fields are unchanged. No global input policy, approval flow, protocol or operation-ledger change was introduced.
- All Editors started for this task are now closed. The requester closed the previously retained PID 80362 during handoff; the final completion PIDs 83764 and 84679 were stopped only after verifying their exact scratch project paths. Other-session Unity projects, including the protected assignment project, were not targeted.
- `completion/logs/final-cleanup.json` confirms asset-config SHA-256 **99214cadfcfb5a2cec44164074cbb980de8ede9bd27f97425975dcaeac86e3ab** and installed CLI SHA-256 **06590d528b79d10051a13202e8558fc21f453e7c32ae2fd3bb6d62e2bd4c2a33**, unchanged from the original snapshot. Neither required restoration because neither was modified.
- Local commits only; no push, tag, release or self-update. Scratch exports, scripts and logs remain available for independent review. UAC removal is **별도 작업**.
