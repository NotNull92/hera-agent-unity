# Developer Mode cross-review follow-up — PlayMode reload cancellation

Both requested findings are fixed and the targeted regression checks pass. The required Unity 6000.6.6f1 release gate remains BLOCKED because that Editor is not installed; no release approval is claimed.

## Candidate

- Branch: `feat/unity-2022-3-developer-mode`.
- Fix commit/export: `b0cc9d6e2783b08c318e91a05285330f0a0c987d` (`b0cc9d6`). Based on reviewed `03fa493`.
- Connector remains `0.3.0`; scratch CLI reports `v0.3.24-dev`; proposed CLI release remains `v0.3.24`.
- `$R` denotes the external compatibility evidence bundle's new `crossreview-fix` directory. `$UNITY_HUB_EDITOR` denotes the Hub Editor root. CLI: `$R/hera-agent-unity`; source: `$R/export-b0cc9d6e`. Neither Editor used the repository working tree.

## Changes

1. For test-framework older than 1.4.0, `RunTests.MarkActive` / `UnmarkActive` are shared by initial execution and `TestRunnerState.ReattachCallbacks`. Successful callback reattachment marks the run active; completion or failed registration cleanup unmarks it. Therefore an active PlayMode run remains protected after its domain reload. All additions are under `!HERA_TEST_FRAMEWORK_CANCEL_API`.
2. The `developer-mode [on|off]` help entry now appears under **Subcommands**, not Interactive TUI.

## Targeted live evidence

The scratch runtime test assembly contains a 25-second bounded PlayMode coroutine. It writes an `Application.isPlaying=True` marker after entering PlayMode. The harness waits for both that marker and a changed heartbeat domain epoch before requesting cancellation. It then inspects pending/result files before any CLI result consumer removes them.

| Check | Unity 2022.3.62f2 / test-framework 1.1.33 | Unity 6000.6.7f1 / test-framework 1.8.0 |
|---|---|---|
| Fresh project import | PASS; 0 CS errors/warnings | PASS; 0 CS errors/warnings |
| Actual PlayMode/domain reload | PASS; marker true, epoch changed | PASS; marker true, epoch changed |
| Cancel during active reattached run | `was_running=true`, `nunit_cancel_requested=false` | `was_running=true`, `nunit_cancel_requested=true` |
| Lock/result immediately after cancel | Pending retained; no result file yet; second run refused with `TEST_RUN_ALREADY_RUNNING` | Existing framework cancel behavior retained |
| Durable completion | `success=true`, total=1, passed=1, failed=0 | `success=false`, `code=TEST_RUN_CANCELLED` |
| Terminal state stability | Result bytes unchanged after PlayMode exit; pending removed; Editor ready | Result bytes unchanged after PlayMode exit; pending removed; Editor ready |
| Resume via CLI | Exit 0, same passing result | Expected exit 1, same cancellation result |
| Final console | 0 error entries | 0 error entries |

The first CLI-driven run on each Editor also produced the expected cancellation behavior and original waiter result. Its extra attempt to read the durable file after the waiter finished failed because `internal/poll/poll.go` deletes consumed result files. This was a verification-harness mistake, not a Connector failure. The second run uses strict `call run_tests` with `async_results=true`, verifies the durable file first, then calls `test --resume`. Both runs and their outputs are retained; no failed artifact was overwritten.

No additional synthetic EditMode test was added: the regression is exercised directly through an actual PlayMode reload, cancellation, concurrent start rejection, completion callback and durable-file check. The existing full Editor suites from the prior implementation were not repeated for this narrow follow-up.

## Other checks

- `TMPDIR=$R/go-tmp go test ./...`: exit 0 (`logs/go-test.log`).
- `TMPDIR=$R/go-tmp golangci-lint run ./...`: exit 0, 0 issues (`logs/lint.log`).
- `golangci-lint fmt --diff`: exit 0, empty (`logs/fmt.log`).
- Scratch `go build -ldflags='-X main.Version=v0.3.24-dev' -o $R/hera-agent-unity .`: exit 0.
- `go run ./tools/validate-connector-package`: exit 0; package integrity PASS.
- `go run ./tools/sync-agent-guides --check`: exit 0.
- `git diff --check`: exit 0.
- `asset-config --help`: exit 0; section placement verified (`logs/asset-config-help.txt`). The expected warning about using a scratch binary instead of the installed CLI did not change either binary.
- 1.4.0+ active-source comparison against `03fa493`: both TestRunner files identical after conditional branch selection and whitespace normalization (`logs/framework14-source-equality.json`). The newer-framework branch was not edited.
- 6.6 live catalog comparison: exit 0, no contract/payload change, no baseline regeneration (`logs/catalog-compare.json`). Hash remains `sha256:5d966c220de01b8642658e74c5e6b7950165332294c633db37508ab2bf6dd18e`.

## Durable run identities

- `2022.3.62f2`: run `872475b31f084324a04553fea51a9161`; epoch `2706242f6a12426ebd58a72704f7b44c` → `cab452ccbd7144e4b0a5b9c80e58b3b5`. Evidence: `logs/durable-2022.3.62f2-reload.json`, `-cancel.out`, `-result.json`, `-stability.json`, `-resume.out` / `-resume.err`.
- `6000.6.7f1`: run `be096a94fa4b4acfa784d535f2d30a18`; epoch `27202a0b29e341fd93d356940b7abb92` → `8035ff66161c40b6973e7d33fd76fd25`. Evidence: `logs/durable-6000.6.7f1-reload.json`, `-cancel.out`, `-result.json`, `-stability.json`, `-resume.out` / `-resume.err`.

## Cleanup and limits

All owned Editor processes and import workers were closed. The shared configuration was backed up before enabling Developer Mode and restored byte-for-byte with original metadata: SHA-256 `99214cadfcfb5a2cec44164074cbb980de8ede9bd27f97425975dcaeac86e3ab`. `cleanup.json` records zero remaining owned Unity processes. The installed global CLI was untouched. No exec approvals were needed for this follow-up; no push, tag or release was performed.

The unavailable 6000.6.6f1 representative remains BLOCKED. Prior optional-package and physical-input limitations remain unchanged. The requester should cross-review this correction before release.

## Unity/CLI command journal

Raw commands and exit codes are in `$R/commands.jsonl`; stdout/stderr and Unity logs are under `$R/logs`. Paths below are normalized to the tokens above. Expected rejection/cancellation returns exit 1.

1. Exit **0**. Log `Unity -logFile below`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -createProject $R/Normal-2022.3.62f2 -logFile $R/logs/create-2022.3.62f2.log
```

2. Exit **0**. Log `Unity -logFile below`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -createProject $R/Normal-6000.6.7f1 -logFile $R/logs/create-6000.6.7f1.log
```

3. Exit **0**. Log `Unity -logFile below`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath $R/Normal-2022.3.62f2 -logFile $R/logs/import-2022.3.62f2.log
```

4. Exit **0**. Log `enable22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode on
```

5. Exit **0**. Log `doctor22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 doctor --json
```

6. Exit **0**. Log `status22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

7. Exit **0**. Log `list22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 list --compact
```

8. Exit **0**. Log `Unity -logFile below`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath $R/Normal-6000.6.7f1 -logFile $R/logs/import-6000.6.7f1.log
```

9. Exit **0**. Log `playmode-2022.3.62f2-list.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test list --mode PlayMode --filter PlayModeReloadQa --limit 5
```

10. Exit **0**. Log `playmode-2022.3.62f2-cancel.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test cancel
```

11. Exit **1**. Log `playmode-2022.3.62f2-second-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --mode PlayMode --filter PlayModeReloadQa.Fast
```

12. Exit **0**. Log `doctor66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 doctor --json
```

13. Exit **0**. Log `status66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 status
```

14. Exit **0**. Log `list66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 list --compact
```

15. Exit **0**. Log `playmode-6000.6.7f1-list.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test list --mode PlayMode --filter PlayModeReloadQa --limit 5
```

16. Exit **0**. Log `playmode-6000.6.7f1-cancel.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test cancel
```

17. Exit **1**. Log `playmode-6000.6.7f1-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test --mode PlayMode --filter PlayModeReloadQa.BoundedRun
```

18. Exit **0**. Log `playmode-2022.3.62f2-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --mode PlayMode --filter PlayModeReloadQa.BoundedRun
```

19. Exit **0**. Log `durable-2022.3.62f2-start.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call run_tests --json '{"mode": "PlayMode", "filter": "PlayModeReloadQa.BoundedRun", "async_results": true}'
```

20. Exit **0**. Log `durable-6000.6.7f1-start.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 call run_tests --json '{"mode": "PlayMode", "filter": "PlayModeReloadQa.BoundedRun", "async_results": true}'
```

21. Exit **0**. Log `durable-2022.3.62f2-cancel.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test cancel
```

22. Exit **1**. Log `durable-2022.3.62f2-second.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --mode PlayMode --filter PlayModeReloadQa.Fast
```

23. Exit **0**. Log `durable-6000.6.7f1-cancel.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test cancel
```

24. Exit **1**. Log `durable-6000.6.7f1-resume.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test --resume be096a94fa4b4acfa784d535f2d30a18
```

25. Exit **0**. Log `durable-6000.6.7f1-console.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 console --type error --lines 10
```

26. Exit **0**. Log `durable-2022.3.62f2-resume.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --resume 872475b31f084324a04553fea51a9161
```

27. Exit **0**. Log `durable-2022.3.62f2-console.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```

28. Exit **0**. Log `catalog66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 list --catalog --schema_version hera.tool-catalog/1
```
