# Connector 0.3.0 release verification — merged main

Status: **PASS**. Candidate `main@27bda7f`: Unity 2022.3 Developer Mode merged with the Connector 0.2.14 scene save and Input System readback fixes. The version conflict resolved to Connector **0.3.0**. Verifier: Claude Opus 5.5, independent of the implementing agent.

The candidate was exported with `git archive` into fresh blank projects with no copied `Library`. Every check used the exact Connector source as a `file:` dependency and a CLI built from the same export.

| Check | Result |
|---|---|
| Go: `gofmt -l`, golangci-lint run/fmt, `go test ./...`, `sync-agent-guides --check`, `validate-connector-package`, `generate-runtime-contracts --check` | PASS |
| 2022.3.62f2 headless compile (Bee reference set) | uGUI 1.0.0 and HeraAgent.Editor: 0 errors, 0 warnings |
| 6000.6.6f1 normal install | 0 errors, 0 CS warnings; analyzer warning set identical to Connector 0.2.14; no test sources in `HeraAgent.Editor` and no test assembly |
| 6000.6.6f1 testables + Input System 1.20.0 | 50 total: 49 passed, 0 failed, 1 skipped (`AnimationTimeline`, Timeline not installed) |
| Catalog | 34 tools / 133 actions; `catalog-payload-report --fail-on-change` reports no change against `docs/metrics/catalog-payload-baseline.json` |
| 6000.6.6f1 live | Clean untitled save no-op; dirty untitled save `SCENE_UNSAVED` with no dialog. Scroll (0,3) and delta (12,-4) are read back and observed by player Update for exactly one frame. Key down/up readback is correct. Shift+A+scroll `settle_frames=0` sequence is observed in one player frame. |
| 2022.3.62f2, Developer Mode off | Dispatch refused with `DEVELOPER_MODE_REQUIRED` |
| 2022.3.62f2, Developer Mode on; TMP 3.0.7, Timeline 1.7.7, Input System 1.14.0, AI Navigation 1.1.6, test-framework 1.1.33 | 0 compile errors; testables 52 / 52 passed / 0 failed / 0 skipped |
| 2022.3.62f2 live | Same scene results as 6000.6.6f1. Scroll/delta are observed for one frame. Move, key, click and grouped sequence delivery matches 6000.6.6f1; held sets end empty. |

This run completes the 2022.3 verification of the scene save and Input System fixes. That verification had been deferred in [scene-save-and-inputsystem-readback-20261029.md](scene-save-and-inputsystem-readback-20261029.md). Earlier Developer Mode evidence stays in [unity-2022-3-independent-gates-20261029.md](unity-2022-3-independent-gates-20261029.md).

## Environment and cleanup

- macOS arm64; `activeInputHandler=Both`; `runInBackground` off; Unity focused during input checks.
- The global asset-config was toggled to Developer Mode on and back. It was then restored byte-for-byte; SHA-256 matched the pre-run snapshot.
- All verification editors were closed. Raw logs are kept outside the repository with the verifier's local evidence bundle.
- Unity 6.6+ remains the supported line; 2022.3 stays opt-in and unsupported.
