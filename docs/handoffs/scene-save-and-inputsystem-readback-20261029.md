# Scene save and Input System readback — blocked verification

Status: **BLOCKED, not release-ready**. The implementation compiles, but the required normal-install gate reports **0 errors / 30 warnings**, against FINAL v2's required 0 errors / 0 warnings. All 30 warning entries also occur in the unpatched baseline, including matching source locations after normalizing the export directory. No warning was cleared or suppressed to claim a pass. Under the design's stop-and-report rule, subsequent Unity verification was stopped pending a decision on the gate or a separately authorized warning-cleanup scope.

## Candidate and scope

- Base: `main@867bf2dfaa9ded351cd40daac75bbb76a7a04823`.
- Branch: `fix/scene-save-and-inputsystem-readback`, in a separate worktree; the existing Developer Mode checkout remains unchanged.
- Design commit: `d360ced`; typed scene guard and regression tests: `036042c`.
- Exported implementation commit: `3658ef7e2f2db650d08793ab370fd70f4f44d762`, Connector **0.2.14**. The normal-install project uses this commit's exported package, not the working tree.
- Installed CLI: **v0.0.38**, unchanged. Its exact local path and SHA-256 are retained in the external `environment.json` and doctor logs. No push, tag, release, self-update, or global CLI replacement was performed.
- Unity: **6000.6.6f1**, macOS arm64. D0 additionally uses Input System **1.20.0**. Normal-install verification excludes Input System and `testables` to check the optional-package boundary.

The retained external compatibility evidence bundle has a `defects/implementation/` directory containing `repo/`, `d0/`, `candidate/`, `go-checks/`, `environment.json`, and `cleanup.json`. Paths below are relative to that directory. Machine-specific paths remain in local logs, not this repository.

## Verification status

| Step | Status | Evidence / limit |
|---|---|---|
| 1. Go checks | PASS | `go-checks/01.json`–`07.json`: clean test cache, gofmt, golangci-lint run, lint fmt diff, `go test ./...`, generated guide check, Connector package validator; all exit 0. TMPDIR is a real scratch directory. |
| 2. Exact exported SHA | PASS for normal install | `candidate/exported-sha.txt`, manifest and package export. Fresh project with no copied Library. Post-fix Input System/test-enabled passes have not run. |
| 3. D0 baseline | PASS — I6 confirmed | `d0/instrumentation.patch`, `d0/Project/Assets/D0InputProbe.cs`, `d0/logs/07-key-down.json`–`12-delta.json`, `Editor.log`, `player-trace.tsv`, `d0-excerpts.json`. |
| 4. 6000.6.6f1 gate | FAIL; continuation BLOCKED | Ready, compiler errors 0, but 30 warnings. `candidate/logs/04-console.json`, `05-console-errors.json`, `warning-comparison.json`. Testables suite and live catalog comparison were not run after this gate failed. |
| 5. Post-fix input matrix | BLOCKED / not run | Pending gate decision. No PASS claimed for delivery, timeout, pause/exit/reload, focus, Fixed/Manual, sequence or record/replay. |
| 6. Post-fix scene matrix | BLOCKED / not run | Regression cases are committed but have not executed. No post-command no-dialog PASS claimed. |
| 7. Unity 2022.3.62f2 | deferred | Developer Mode branch has not been merged into this branch; no 2022.3 execution. |

Normal installation produced `HeraAgent.Editor.dll`; its compiler response file contains **0** `AgentConnector/Editor/Tests/` source paths, and `HeraAgent.Editor.Tests.dll` is absent. This is a partial installation check, not a substitute for the complete gate.

## D0 findings

The baseline export is main plus logging-only instrumentation in `InputQaInputSystem.cs`; no behavior fix was applied there. The runtime probe logs before/after input updates, MonoBehaviour.Update and FixedUpdate. Its trace cap was not reached during the commands used below. Each command log retains its command, exit code, duration and timestamp.

Settings: `ProcessEventsInDynamicUpdate`, `PointersAndKeyboardsRespectGameViewFocus`, `ResetAndDisableNonBackgroundDevices`, `runInBackground=False`, `activeInputHandler=Both`, probe `Application.isFocused=True`. This is the focused baseline only, not evidence for the unfocused matrix.

| Input | Apply observation | Following player observation |
|---|---|---|
| A down/up | Forced Dynamic update, frames 12516 / 12562 | A persisted as true/false; down response incorrectly reported false. |
| Move (320,200) | Forced Dynamic, frame 12617 | Position persisted into player Update; response incorrectly reported (0,0). |
| Left click | Forced Dynamic down/up, frames 12670 / 12725 | Player observed button down and up. |
| Scroll (0,3) | Forced Dynamic, frame 12778, input update 25562; after callback saw (0,3) | Natural Dynamic update 25563 at frame 12779 already had (0,0), and player Update read (0,0). |
| Delta (12,-4) | Forced Dynamic, frame 12827, input update 25661; after callback saw (12,-4) | Natural Dynamic update 25662 at frame 12828 already had (0,0), and player Update read (0,0). |

This confirms the extra forced input-only update/next natural reset hypothesis for both transient controls. It also confirms delta failure, which was only a hypothesis in FINAL v2. The writes were not erased by the reset in their own apply update. The post-fix natural-update path is not yet runtime-verified.

The baseline `editor play --wait` lost its connection during domain reload (exit 1). It was not resent. `06-status-after-reload.json` and the runtime probe confirmed Play Mode actually started, and subsequent commands succeeded. This transport event is recorded separately from input correctness.

## Implemented changes, pending remaining verification

- Typed single-scene save rejects dirty untitled scenes with existing `SCENE_UNSAVED`, after the existing clean-scene no-op. The scene regression suite adds clean/dirty untitled, saved dirty and mixed save_all/no-partial-write assertions.
- Input application subscribes before writing, waits for the configured player update, captures the existing physical snapshot in that same update, then preserves it through settle ticks. The old forced Update and temporary updateMode changes are removed.
- One **2000 ms wall-clock deadline per change** permits slow editor frames while bounding an absent Manual pump independently of settle_frames. Before/after and editor lifecycle subscriptions are removed at completion; cancellation is marshalled to the Unity context. Existing response/error fields and codes are reused.
- Ownership is recorded after actual down application; successful releases remove ownership after actual release. Unapplied expired releases retain their record for existing cleanup. No queue flush, device creation, focus-policy change or new protocol is introduced.
- Help, canonical/generated agent guides, and both READMEs describe the capture time, release semantics, Manual requirement and typed scene/modal boundary. README additions were explicitly requested during this task. No release table entry was added.

Required follow-up includes executing all scene tests, adding/retaining meaningful input regression coverage with actual player observations, testing lifecycle/ownership failure paths, and checking recorder/replay frame gaps. Compilation alone does not establish those properties.

## Warning blocker

| Warning | Candidate entries | Baseline comparison |
|---|---:|---|
| UAC0005: AppDomain.GetAssemblies | 19 | Same source locations and text in D0 main export |
| UAC0007: Assembly.Location | 8 | Same source locations and text in D0 main export |
| UAC0020: Assembly.Load(byte[]) | 2 | Same source locations and text in D0 main export |
| Input Manager deprecation | 1 | Also present in baseline log |

`candidate/logs/warning-comparison.json` retains all entries and 30/30 exact matches after export-root normalization. The scope does not authorize broad assembly-loading/API changes or hiding diagnostics. Continuation requires deciding whether this gate permits explicitly recorded baseline warnings, or authorizing a separate warning-remediation scope. No implementation of either alternative was attempted.

## Modal audit

| Path | Scope and status |
|---|---|
| `Tools/ManageScene.cs`, single-scene SaveScene(scene) | Dirty empty path now rejected; live verification pending |
| `Tools/ManageScene.Authoring.cs`, SaveScene(scene, path) | Uses explicit validated asset path |
| `Tools/ManageScene.Authoring.cs`, SaveOpenScenes | Existing dirty-untitled preflight; mixed-scene regression added, not executed |
| SaveCurrentModifiedScenesIfUserWantsTo | No direct call found in Connector sources |
| Settings-window DisplayDialog calls | UI save/auto-detect failure handling; outside typed scene handler |
| Generic menu and exec | Explicit caller-requested dialogs remain possible; out of scope, help/guide caution recorded |

Before shutdown, the available native UI inspection showed only the candidate's `Untitled - Project - Windows, Mac, Linux - Unity 6.6 (6000.6.6f1) <Metal>` standard window. No scene-save test had run, so this is not no-dialog regression evidence. The optional Orca executable was unavailable; no alternate Orca binary was used.

## Cleanup and handoff

Both Editors started for this task were stopped: D0 PID 67599 and candidate PID 69046; process exit was verified. `cleanup.json` confirms the machine-global asset-config and installed CLI hashes remain unchanged. No global configuration restoration was necessary because neither was modified. Scratch projects, exports and logs are retained for continuation.

This branch is a reviewable partial implementation, not a completed or approved fix. Continue from the exported candidate after resolving the normal-install warning gate; do not mark unexecuted steps PASS.
