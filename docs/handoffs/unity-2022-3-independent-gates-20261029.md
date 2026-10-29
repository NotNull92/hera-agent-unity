# Independent representative gate and optional-package verification

**PASS:** the required Unity 6000.6.6f1 representative gate and the scoped Unity 2022.3.62f2 optional-package paths are independently verified. This supersedes the earlier missing-Editor BLOCKED status and package-path unverified status; it does not change Developer Mode's unsupported, opt-in policy or authorize a release.

## Provenance

- Verifier: Claude Opus 5.5, independent of the implementer; 2026-10-29, macOS 26.6.2 arm64.
- Candidate: `feat/unity-2022-3-developer-mode` at `925e020e0f2dcd76e3a6adf926f8456958dbd3f3`, Connector `0.3.0`, installed from a `git archive` export as a `file:` dependency.
- Representative-gate CLI: scratch `v0.3.24-dev+925e020`; installed global CLI untouched.
- `$EVIDENCE_ROOT` denotes the external compatibility bundle supplied by the requester. Sources: `gate-6000.6.6f1/GATE_RESULT.md`, `pkg-2022.3/PACKAGE_RESULT.md` and their `logs/` directories. The parity probe is in `pkg-6000.6.6f1/logs/` under that same bundle.
- This documentation update read the reports and supporting logs. It did not rerun code, builds, tests or Unity, and changed documentation only.

## Required representative: Unity 6000.6.6f1

| Check | Result | Evidence relative to `gate-6000.6.6f1/logs/` |
|---|---|---|
| Fresh blank project, ordinary UPM installation | PASS; uGUI resolves to built-in 2.6.0 | `01-normal-create.log`, `02-normal-import.log`; report's package-lock observation |
| Bounded initial compilation and TestRunner | PASS; 6.9 seconds, 0 Hera errors/warnings, TestRunner built with test-framework 1.8.0 | `02-normal-import.log` |
| Normal-install test isolation | PASS; 121 production sources, 0 Editor/Tests sources, no HeraAgent.Editor.Tests assembly | `normal-HeraAgent.Editor.rsp`, `GATE_RESULT.md` |
| GUI Editor ready and console | PASS; error count 0 | `04-normal-ready.txt` |
| Developer Mode absent/on on 6.6 | PASS; object discovery and batch succeed with 64-bit IDs | `05-devmode-noeffect.txt` |
| Catalog baseline | PASS; unchanged, review_required=false | `06-catalog.json`, `06-catalog-report.txt` |
| Separate testables installation | PASS; 46 test sources, production rsp still excludes tests, 0 Hera errors/warnings | `08-testable-import.log` |
| Testables ready and console | PASS; error count 0 | `09-testable-launch.out`, `10-testable-console.txt` |
| Hera EditMode suite | PASS; **48 passed, 0 failed, 1 skipped** | `11-release-gate-suite.json`; AnimationTimeline skipped because the blank template has no Timeline package |

The first GUI launch waited for the Editor Software Terms window. The user accepted it; the original process then produced its heartbeat. The earlier launch timeout during that wait was not classified as a Hera failure. The required representative remains 6000.6.6f1. Earlier 6000.6.7f1 results are supplementary historical evidence; that Editor is no longer installed on the verifier's machine.

## Unity 2022.3.62f2 package paths

Configuration: TMP **3.0.7**, Timeline **1.7.7**, Input System **1.14.0** with `activeInputHandler=Both`, AI Navigation **1.1.6**, test-framework **1.1.33**. The verifier reports that the reflected package types/members are present, with 0 CS errors and 0 Hera warnings on import.

| Observed path | Result | Evidence relative to `pkg-2022.3/logs/` |
|---|---|---|
| Hera suite with Timeline installed | PASS; **51 passed, 0 failed, 0 skipped** | `05-suite.json` |
| TMP text/button creation and value readback | PASS; TextMeshProUGUI | `06-tmp-timeline.txt` |
| Timeline create, Activation/Animation tracks, clip addition and get | PASS | `07-timeline.txt` |
| NavMeshSurface bake and saved data reference | PASS; start→idle, NavMeshData asset written and referenced | `11-navmesh.txt` |
| Input System availability, Play Mode requirement and held-key tracking | PASS | `12-inputsystem.txt` |
| Delivery to player-code probe | PASS; A down/up, mouse position (320,200), left-button down/up observed | `14-probe-2022.3.txt` |
| Same input probe on Unity 6000.6.6f1 + Input System 1.20.0 | PASS; equivalent observed delivery | `pkg-6000.6.6f1/logs/03-probe-6.6.txt` relative to the bundle root |

These results cover the named versions and observed paths, not every feature of those packages. The package-present suite removes the earlier 2022.3 Timeline skip; the representative's separate blank-template skip remains an accurate record of that run.

## Scope and cleanup

`PACKAGE_RESULT.md`의 pre-existing defects 5건: **별도 작업으로 이관**.

The verifier reports all started Editors closed and the shared asset configuration restored with identical before/after SHA-256. No code changes, Unity execution, push, tag or release were performed while recording this evidence. Other-line compatibility, physical OS input and real network-loss claims remain outside these results.
