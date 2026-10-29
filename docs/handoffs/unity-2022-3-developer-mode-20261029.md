# Unity 2022.3 Developer Mode — implementation evidence

Follow-up: [PlayMode reload cancellation correction and live regression evidence](unity-2022-3-developer-mode-crossreview-20261029.md).

Implementation complete. **Required 6000.6.6f1 gate: PASS**, independently verified on candidate `925e020` after the original implementation checks. The selected 2022.3 package paths also passed. See the [independent gate and package evidence](unity-2022-3-independent-gates-20261029.md); original execution logs and counts below remain historical evidence, not a release authorization.

## Candidate and environment

- Branch: `feat/unity-2022-3-developer-mode`, based on `main@867bf2dfaa9ded351cd40daac75bbb76a7a04823`.
- Final implementation source: `281e7ef181bb2e0b9e424e6732285bd940bfd933`. The evidence-only commit follows it.
- Connector: **0.3.0**. Scratch CLI: **v0.3.24-dev**; proposed next CLI tag: **v0.3.24**. No tag, push, release, update or installed-CLI replacement was performed.
- macOS arm64; Unity **2022.3.62f2** and **6000.6.7f1**. Go **1.25.7**, golangci-lint **2.11.4**.
- `$EVIDENCE_ROOT` means the external compatibility bundle supplied with the implementation request. `$R` means its `implementation-v2` directory. `$UNITY_HUB_EDITOR` means the installed Hub Editor root. These are descriptions of the local evidence layout, not machine-specific repository paths.
- CLI binary: `$R/hera-agent-unity`. Full raw request/exit/log journal: `$R/commands.jsonl`. Unity import/test logs and CLI stdout/stderr: `$R/logs/`. Helper scripts and disposable projects remain under `$R`, outside the repository.
- Unity received only immutable `git archive` exports. No Editor referenced the repository working tree.

## Results

| Plan step | Status | Evidence and limits |
|---|---|---|
| 1. Go checks | PASS | Full `go test ./...` with canonical scratch TMPDIR; `lint-clean-cache.log`: 0 issues; `fmt-final.log`: empty; package validator and generated-guide drift checks pass. Initial host-temp-path failures and transient lint findings are retained below. |
| 2. Exact-source installation | PASS | Normal installs used `bf884891` (2022.3) and `60b9c25a` (6000.6.7f1); tests ultimately used `ed117e6fa327b373a786946cbe730d9e7cb6870f` (full SHA in exports/journal). Final `281e7ef` archive also retained. Later implementation commits changed Go and help only; `git diff ed117e6..281e7ef -- AgentConnector` is empty. |
| 3. Shared setting hygiene | PASS | Original file existed without developer_mode. `config-backup.json` and `asset-config.original` captured it before any Unity step. Cleanup restored identical bytes and metadata. |
| 4. 2022.3 normal install | PASS | UPM resolves with uGUI 1.0.0; 0 CS errors/warnings; production rsp has 0 Tests sources; no HeraAgent.Editor.Tests DLL in normal install. Locked list/call/legacy/batch return DEVELOPER_MODE_REQUIRED; ON reaches ready and console errors 0. |
| 4. 2022.3 TestRunner/testables | PASS with optional skip | TestRunner compiles with 1.1.33. Final `tests-2022.3.62f2.xml`: **51 total, 50 passed, 0 failed, 1 skipped**. Only AnimationTimeline skips because Timeline is absent. New ID and gate tests pass. |
| 5. 6000.6.7f1 regression | PASS with optional skip | 0 CS errors/warnings, TestRunner built. Final `tests-6000.6.7f1.xml`: **49 total, 48 passed, 0 failed, 1 skipped** (same missing Timeline). ON and OFF both allow scene queries. Settings save leaves developer_mode untouched. |
| 5. Required 6000.6.6f1 | PASS | Independent verification on `925e020`: clean normal/testables imports, unchanged catalog, 48 passed / 0 failed / 1 Timeline skip. See [follow-up evidence](unity-2022-3-independent-gates-20261029.md). The representative was not substituted. |
| 6. Catalog | PASS | `catalog-compare.json`: no contract/payload growth or change, review_required=false. Hash `sha256:5d966c220de01b8642658e74c5e6b7950165332294c633db37508ab2bf6dd18e`. Baseline not regenerated. Normal 6.6 catalog exposes 34 tools. |
| 7. Cold/warm gate | PASS | Cold and warm list, strict call, legacy and batch refuse dispatched requests. Cached list --profiles works locally. Strict exec warm-cache approval rejection now also retains its JSON code; no token is issued while locked. |
| 7. Settings | PASS | CLI→2022.3 UI toggle→OFF save→ON save; unknown key preserved. 6.6 UI has no toggle and preserves the shared value on save. `settings-ui22.out`, `settings-ui66.out`, `settings-readback22.out`. UI Toolkit state/callback verification, not physical OS clicks. |
| 7. Scene and prefab | PASS | Created/moved object to (4,5,6), saved scene, created/instantiated prefab, checked overrides, loaded a different scene and reopened saved scene. Position and prefab link persisted. |
| 7. Reload/play/restart | PASS | Compile and Play Mode enter/exit reconnect. Restart PID changed from 41212 to 43236; batchmode starts an empty scene, so the saved scene was explicitly reopened and coordinates re-read. Saved data survived. |
| 7. ID boundaries | PASS | New 2022.3 test checks positive/negative IDs, zero, destroyed object and int min−1/max+1. Live negative ID −1390 and reloaded positive IDs 17510/18740 observed. 6.6 live ID 3298534897109 remains wider than 32 bits. |
| 7. Duplicate/reload/stale QA | PASS | Same operation ID returned the same creation result twice; independent lookup counted one object. This simulates discarding the first result, not physical network loss. A known-dead owned heartbeat was removed while the correct target remained selected. `compile-observed.json` confirms compiling state before a concurrent request, which completed successfully. |
| 7. Repeated exec | PASS | 20 identical `return 1;` executions, all exit 0 and result 1, each through its exact approval replay. |
| 7. Active cancel on 1.1.33 | PASS | Bounded 15-second test: cancel returns nunit_cancel_requested=false; second run returns TEST_RUN_ALREADY_RUNNING; original run completes with 1 passed and a real durable result. No cancelled result overwrites it. `cancel-2022.3.62f2*` logs. |
| 7. UI/docs | PASS within stated limits | Text uses legacy Text without TMP; explicit TMP reports TMP_NOT_INSTALLED. unity_docs returns docs_version=6000.6 while status identifies Editor 2022.3.62f2. |
| 8. 6000.6 live smoke | PASS | Mutation/save, compile reconnect, zero final console errors; active cancel requests framework cancellation and returns TEST_RUN_CANCELLED to the original waiter. `cancel-6000.6.7f1*` logs. |
| 9. Cleanup | PASS | Every owned Editor and import worker stopped. Shared config restored to SHA-256 `99214cadfcfb5a2cec44164074cbb980de8ede9bd27f97425975dcaeac86e3ab`. `cleanup.json` reports no owned Unity processes remaining. Installed CLI untouched. |

## Implemented behavior

Unity 6.6 keeps its EntityId and FindObjectsByType paths. Unity 2022.3 uses the signed-int bridge inside the long ID contract and explicit SortMode overloads. Other lines fail at the compile boundary. Package metadata now declares the 2022.3 compilation floor and uGUI 1.0.0.

Developer Mode is machine-global, false by default and ignored by 6.6. Protocol validation remains first; the gate precedes catalog/handler/approval/ledger work. Batch and HTTP approval preflight are also gated. Missing file/key resets false; malformed/locked reads preserve the last good snapshot. Go custom JSON paths and the C# known-field merge both include the key. The TUI currently lists assets only, so no new mode-control row was added; its save test verifies the typed value survives.

On test-framework <1.4.0 an in-memory marker tracks durable runs with live completion callbacks. Active runs keep their pending record and are not cancelled; stale records without that marker retain the existing recovery path. The 1.4.0+ branch and cancel output schema remain unchanged.

The approval/catalog error envelope fixes preserve codes instead of reclassifying Connector rejection as a generic CLI error. No runtime contract constants, tool/action schemas or baseline were changed.

## Corrections and retained failures

- D6 was corrected with requester approval: existing expectations already used EntityIdCompat. No ID expectation was disabled or weakened. The first preflight stop is preserved in commit `9478d60`.
- Test-framework 1.1.33 rejected async Task NUnit methods with “Method has non-void return value, but no result is expected”. Added 2022.3 coroutine wrappers around the same async bodies; the newer path remains unchanged. One existing test now restores its prior config override instead of forcing null, keeping the temporary fixture active.
- The legacy TestAdaptor required a parent fixture and platform metadata in synthetic result data. Added both only for 2022.3, retaining all result assertions. Attempts 2–4 and their XML/logs remain under `$R`.
- An initial active-run marker edit accidentally reached the legacy synchronous cleanup block and caused CS0103. Fixed in `a8a1d31`; the first compiler-failure log is retained.
- Initial `go test ./...` failed host-temp symlink expectations (`/var` versus `/private/var`). It passed with TMPDIR set to `$R/go-tmp`; no unrelated path-resolution code was changed.
- A later lint run reported SA5011 after existing `t.Fatal` nil guards. Cleaning the lint cache and rerunning yielded 0 issues without changing those tests. Both logs remain available.
- Scratch QA assembly setup initially duplicated references via optionalUnityReferences, then briefly lacked resolved test-framework on the live 2022.3 project. Corrected the scratch asmdef and restarted to resolve its manifest; no repository workaround was added. Subsequent compile and both final console error reads are clean.
- Early QA invocation mistakes (`scene save` on a not-yet-created scene, unsupported `get` action and a harness JSON key lookup) were corrected using documented create/get_transform operations. They are not reported as Connector defects. Restart initially opened an empty batchmode scene; persistence was checked by explicit load.
- One non-batch Editor launch did not expose a heartbeat promptly and was terminated. Live checks use a running batchmode Editor with normal graphics, no `-quit` and no `-nographics`. This does not prove interactive window-focus behavior.

## Approval and limits

**사용자 승인(범위 한정):** the requester relayed the user's explicit authorization to replay APPROVAL_REQUIRED only for scratch-project settings UI state/save, instance-ID reads, 20 `return 1;` calls, and operation-ID retry QA. Each approved call repeated the original request with the returned single-use `--approve` token. No `--yes` flag was used. The first probe was left unapproved until that authorization arrived. Tokens are redacted from the repository appendix; raw local evidence retains the preflight and execution records.

The required 6000.6.6f1 gate and the named 2022.3 package paths now have independent PASS evidence. The original blank-template Timeline skips above are retained; the later package-present 2022.3 suite passed 51/51 without skips. See [verified versions and paths](unity-2022-3-independent-gates-20261029.md). No claim is made for other Unity lines, physical OS clicks, real packet loss or every feature of those packages. Developer Mode remains unsupported opt-in, not a new supported Unity series. Recording this evidence does not authorize release.

## Local commit sequence

- `9478d60` — docs: record developer mode implementation preflight blocker
- `bf88489` — feat: add opt-in developer mode for Unity 2022.3
- `60b9c25` — fix: gate approval preflight while developer mode is locked
- `a8a1d31` — fix: scope active test tracking to durable runs
- `a75634c` — test: preserve async assertions on Unity 2022.3 test runner
- `d0fd9dc` — test: provide parent fixture for Unity 2022.3 result adaptors
- `ed117e6` — test: identify platform in legacy test result fixture
- `281e7ef` — fix: preserve developer mode errors through approval preflight

## Check commands outside the Unity/CLI journal

| Command | Exit | Evidence |
|---|---:|---|
| `go clean -testcache` | 0 | Before first complete suite |
| `gofmt -w .` | 0 | Formatting applied |
| `go test ./...` | 1 | `$R/logs/go-test.log`, initial temp symlink failures |
| `TMPDIR=$R/go-tmp go test ./...` | 0 | `$R/logs/go-test-canonical-tmp.log`, then `go-test-final.log` after the approval error fix |
| `golangci-lint run ./...` | 0 initially, 1 later | `$R/logs/lint.log`, `lint-final.log` |
| `golangci-lint cache clean` | 0 | Cleared stale analysis state |
| `TMPDIR=$R/go-tmp golangci-lint run ./...` | 0 | `$R/logs/lint-clean-cache.log`: 0 issues |
| `golangci-lint fmt --diff` | 0 | `$R/logs/fmt-final.log`: empty |
| `go run ./tools/sync-agent-guides` and `--check` | 0 | Generated derivatives checked |
| `go run ./tools/validate-connector-package` | 0 | Connector package integrity PASS |
| `go build -ldflags='-X main.Version=v0.3.24-dev' -o $R/hera-agent-unity .` | 0 | Scratch binary; installed CLI unchanged |
| `go run ./tools/catalog-payload-report --catalog $R/logs/catalog66.out --compare docs/metrics/catalog-payload-baseline.json --fail-on-change` | 0 | `$R/logs/catalog-compare.json` |
| `git diff --check` | 0 | No whitespace errors |

## Unity and CLI command journal

Paths are normalized to the tokens defined above. The actual expanded arguments, timestamps/durations and stdout/stderr filenames are in `$R/commands.jsonl`. Expected negative tests intentionally exit 1. Approval tokens below are replaced with `<issued-token>`; code and all other request parameters are preserved.

1. Exit **0**; log `$R/logs/create-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -createProject $R/Normal-2022.3.62f2 -logFile $R/logs/create-2022.3.62f2.log
```

2. Exit **0**; log `$R/logs/import-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath $R/Normal-2022.3.62f2 -logFile $R/logs/import-2022.3.62f2.log
```

3. Exit **0**; log `$R/logs/create-6000.6.7f1.log`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -createProject $R/Normal-6000.6.7f1 -logFile $R/logs/create-6000.6.7f1.log
```

4. Exit **0**; log `$R/logs/import-6000.6.7f1.log`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath $R/Normal-6000.6.7f1 -logFile $R/logs/import-6000.6.7f1.log
```

5. Exit **1**; log `$R/logs/tests-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-2022.3.62f2 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-2022.3.62f2.xml -logFile $R/logs/tests-2022.3.62f2.log
```

6. Exit **0**; log `$R/logs/tests-6000.6.7f1.log`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-6000.6.7f1 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-6000.6.7f1.xml -logFile $R/logs/tests-6000.6.7f1.log
```

7. Exit **2**; log `$R/logs/tests-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-2022.3.62f2 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-2022.3.62f2.xml -logFile $R/logs/tests-2022.3.62f2.log
```

8. Exit **0**; log `doctor66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 doctor --json
```

9. Exit **2**; log `$R/logs/tests-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-2022.3.62f2 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-2022.3.62f2.xml -logFile $R/logs/tests-2022.3.62f2.log
```

10. Exit **0**; log `status66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 status
```

11. Exit **0**; log `list66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 list --compact
```

12. Exit **0**; log `catalog66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 list --catalog --schema_version hera.tool-catalog/1
```

13. Exit **0**; log `console66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 console --type error --lines 10
```

14. Exit **0**; log `doctor22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 doctor --json
```

15. Exit **0**; log `status22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

16. Exit **1**; log `locked-list22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 list --compact
```

17. Exit **1**; log `locked-call22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call scene --json '{"action":"info"}'
```

18. Exit **1**; log `locked-legacy22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene info
```

19. Exit **2**; log `$R/logs/tests-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-2022.3.62f2 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-2022.3.62f2.xml -logFile $R/logs/tests-2022.3.62f2.log
```

20. Exit **0**; log `locked-ping22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 ping
```

21. Exit **1**; log `locked-batch22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 batch --file $R/gate-batch.json
```

22. Exit **0**; log `enable22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode on
```

23. Exit **0**; log `enabled-list22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 list --compact
```

24. Exit **0**; log `enabled-call22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call scene --json '{"action":"info"}'
```

25. Exit **0**; log `console22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```

26. Exit **1**; log `save-scene22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene save Assets/DeveloperModeQA.unity
```

27. Exit **0**; log `create-scene22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene create Assets/DeveloperModeQA.unity --template empty
```

28. Exit **0**; log `create22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject create --name DeveloperModeQA --position 1,2,3
```

29. Exit **0**; log `move22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject move --target /DeveloperModeQA --position 4,5,6
```

30. Exit **0**; log `save-object22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene save
```

31. Exit **1**; log `read-object22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject get --target /DeveloperModeQA
```

32. Exit **0**; log `prefab-create22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_prefab create --source /DeveloperModeQA --path Assets/DeveloperModeQA.prefab
```

33. Exit **0**; log `prefab-instantiate22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_prefab instantiate --path Assets/DeveloperModeQA.prefab
```

34. Exit **0**; log `save-prefab-scene22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene save
```

35. Exit **0**; log `disable22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode off
```

36. Exit **1**; log `warm-list22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 list --compact
```

37. Exit **1**; log `warm-call22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call scene --json '{"action":"info"}'
```

38. Exit **1**; log `warm-legacy22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene info
```

39. Exit **1**; log `warm-batch22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 batch --file $R/gate-batch.json
```

40. Exit **0**; log `warm-local22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 list --profiles
```

41. Exit **0**; log `enable-again22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode on
```

42. Exit **1**; log `exec-probe22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

43. Exit **0**; log `$R/logs/tests-2022.3.62f2.log`.

```text
$UNITY_HUB_EDITOR/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-2022.3.62f2 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-2022.3.62f2.xml -logFile $R/logs/tests-2022.3.62f2.log
```

44. Exit **0**; log `rename-prefab22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject set_name --instance_id -1444 --name PrefabQA
```

45. Exit **0**; log `transform22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject get_transform --target /DeveloperModeQA
```

46. Exit **0**; log `prefab-overrides22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_prefab list_overrides --target /PrefabQA --include_default true
```

47. Exit **0**; log `save-before-reopen22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene save
```

48. Exit **0**; log `empty-scene22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene create Assets/EmptyQA.unity --template empty
```

49. Exit **0**; log `reopen22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene load Assets/DeveloperModeQA.unity
```

50. Exit **0**; log `persisted-transform22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject get_transform --target /DeveloperModeQA
```

51. Exit **0**; log `prefab-reloaded22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_prefab list_overrides --target /PrefabQA
```

52. Exit **0**; log `lost-response22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call manage_gameobject --json '{"action":"create","name":"DuplicateSafetyQA"}' --operation-id op_developer_duplicate_20261029
```

53. Exit **0**; log `replayed-operation22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call manage_gameobject --json '{"action":"create","name":"DuplicateSafetyQA"}' --operation-id op_developer_duplicate_20261029
```

54. Exit **0**; log `duplicate-count22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 find_gameobjects --name DuplicateSafetyQA --ids
```

55. Exit **0**; log `legacy-ui22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_ui create --element text --name LegacyQA --content Compatibility
```

56. Exit **1**; log `missing-tmp22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_ui create --element text --name MissingTMP --text tmp
```

57. Exit **0**; log `docs22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 unity_docs Transform
```

58. Exit **0**; log `save-before-play22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene save
```

59. Exit **0**; log `compile22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor refresh --compile
```

60. Exit **0**; log `post-compile22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene info
```

61. Exit **0**; log `play22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor play --wait
```

62. Exit **0**; log `stop22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor stop --wait
```

63. Exit **0**; log `post-play22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene info
```

64. Exit **0**; log `final-console22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```

65. Exit **0**; log `on-mode66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 scene info
```

66. Exit **0**; log `create-scene66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 scene create Assets/DeveloperModeQA.unity --template empty
```

67. Exit **0**; log `create-object66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 manage_gameobject create --name SupportedQA --position 7,8,9
```

68. Exit **0**; log `save66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 scene save
```

69. Exit **0**; log `compile66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 editor refresh --compile
```

70. Exit **0**; log `$R/logs/tests-6000.6.7f1.log`.

```text
$UNITY_HUB_EDITOR/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $R/Tests-6000.6.7f1 -runTests -testPlatform EditMode -assemblyNames HeraAgent.Editor.Tests -testResults $R/tests-6000.6.7f1.xml -logFile $R/logs/tests-6000.6.7f1.log
```

71. Exit **1**; log `repeat-exec22-1-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

72. Exit **0**; log `repeat-exec22-1.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

73. Exit **1**; log `repeat-exec22-2-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

74. Exit **0**; log `repeat-exec22-2.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

75. Exit **1**; log `repeat-exec22-3-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

76. Exit **0**; log `repeat-exec22-3.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

77. Exit **1**; log `repeat-exec22-4-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

78. Exit **0**; log `repeat-exec22-4.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

79. Exit **1**; log `repeat-exec22-5-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

80. Exit **0**; log `repeat-exec22-5.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

81. Exit **1**; log `repeat-exec22-6-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

82. Exit **0**; log `repeat-exec22-6.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

83. Exit **1**; log `repeat-exec22-7-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

84. Exit **0**; log `repeat-exec22-7.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

85. Exit **1**; log `repeat-exec22-8-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

86. Exit **0**; log `repeat-exec22-8.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

87. Exit **1**; log `repeat-exec22-9-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

88. Exit **0**; log `repeat-exec22-9.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

89. Exit **1**; log `repeat-exec22-10-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

90. Exit **0**; log `repeat-exec22-10.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

91. Exit **1**; log `repeat-exec22-11-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

92. Exit **0**; log `repeat-exec22-11.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

93. Exit **1**; log `repeat-exec22-12-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

94. Exit **0**; log `repeat-exec22-12.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

95. Exit **1**; log `repeat-exec22-13-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

96. Exit **0**; log `repeat-exec22-13.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

97. Exit **1**; log `repeat-exec22-14-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

98. Exit **0**; log `repeat-exec22-14.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

99. Exit **1**; log `repeat-exec22-15-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

100. Exit **0**; log `repeat-exec22-15.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

101. Exit **1**; log `repeat-exec22-16-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

102. Exit **0**; log `repeat-exec22-16.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

103. Exit **1**; log `repeat-exec22-17-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

104. Exit **0**; log `repeat-exec22-17.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

105. Exit **1**; log `repeat-exec22-18-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

106. Exit **0**; log `repeat-exec22-18.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

107. Exit **1**; log `repeat-exec22-19-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

108. Exit **0**; log `repeat-exec22-19.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

109. Exit **1**; log `repeat-exec22-20-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

110. Exit **0**; log `repeat-exec22-20.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1 --approve '<issued-token>'
```

111. Exit **1**; log `settings-ui22-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'var w = UnityEditor.EditorWindow.GetWindow<HeraAgent.Editor.HeraAgentAssetConfigWindow>();\nif (w.rootVisualElement.childCount == 0) w.CreateGUI();\nvar toggle = w.rootVisualElement.Q<UnityEngine.UIElements.Toggle>("developer-mode");\nif (toggle == null || !toggle.value) throw new System.Exception("Developer Mode toggle must show the CLI value");\ntoggle.value = false;\nvar path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".hera-agent-unity", "asset-config.json");\nvar off = !Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path)).Value<bool>("developer_mode");\ntoggle.value = true;\nw.GetType().GetMethod("SaveConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, null);\nvar root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));\nvar result = new { toggle_present = true, off_saved = off, on_saved = root.Value<bool>("developer_mode"), unknown_preserved = root["developer_mode_qa_unknown"].Value<bool>("preserve") };\nw.Close(); return result;' --usings UnityEngine.UIElements --depth 1
```

112. Exit **0**; log `settings-ui22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'var w = UnityEditor.EditorWindow.GetWindow<HeraAgent.Editor.HeraAgentAssetConfigWindow>();\nif (w.rootVisualElement.childCount == 0) w.CreateGUI();\nvar toggle = w.rootVisualElement.Q<UnityEngine.UIElements.Toggle>("developer-mode");\nif (toggle == null || !toggle.value) throw new System.Exception("Developer Mode toggle must show the CLI value");\ntoggle.value = false;\nvar path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".hera-agent-unity", "asset-config.json");\nvar off = !Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path)).Value<bool>("developer_mode");\ntoggle.value = true;\nw.GetType().GetMethod("SaveConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, null);\nvar root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));\nvar result = new { toggle_present = true, off_saved = off, on_saved = root.Value<bool>("developer_mode"), unknown_preserved = root["developer_mode_qa_unknown"].Value<bool>("preserve") };\nw.Close(); return result;' --usings UnityEngine.UIElements --depth 1 --approve '<issued-token>'
```

113. Exit **1**; log `settings-ui66-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 exec 'var w = UnityEditor.EditorWindow.GetWindow<HeraAgent.Editor.HeraAgentAssetConfigWindow>();\nif (w.rootVisualElement.childCount == 0) w.CreateGUI();\nvar present = w.rootVisualElement.Q<UnityEngine.UIElements.Toggle>("developer-mode") != null;\nw.GetType().GetMethod("SaveConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, null);\nw.Close(); return new { toggle_present = present };' --usings UnityEngine.UIElements --depth 1
```

114. Exit **0**; log `settings-ui66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 exec 'var w = UnityEditor.EditorWindow.GetWindow<HeraAgent.Editor.HeraAgentAssetConfigWindow>();\nif (w.rootVisualElement.childCount == 0) w.CreateGUI();\nvar present = w.rootVisualElement.Q<UnityEngine.UIElements.Toggle>("developer-mode") != null;\nw.GetType().GetMethod("SaveConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, null);\nw.Close(); return new { toggle_present = present };' --usings UnityEngine.UIElements --depth 1 --approve '<issued-token>'
```

115. Exit **0**; log `settings-readback22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config --json
```

116. Exit **1**; log `ids22-preflight.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'var go = UnityEngine.GameObject.Find("DeveloperModeQA");\nreturn new { id = go.GetInstanceID(), resolves = UnityEditor.EditorUtility.InstanceIDToObject(go.GetInstanceID()) == go,\nzero_missing = UnityEditor.EditorUtility.InstanceIDToObject(0) == null };' --depth 1
```

117. Exit **0**; log `ids22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'var go = UnityEngine.GameObject.Find("DeveloperModeQA");\nreturn new { id = go.GetInstanceID(), resolves = UnityEditor.EditorUtility.InstanceIDToObject(go.GetInstanceID()) == go,\nzero_missing = UnityEditor.EditorUtility.InstanceIDToObject(0) == null };' --depth 1 --approve '<issued-token>'
```

118. Exit **1**; log `import-testframework22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor refresh --compile
```

119. Exit **0**; log `pending-status22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

120. Exit **0**; log `qa-compile-console22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```

121. Exit **1**; log `import-qa66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 editor refresh --compile
```

122. Exit **1**; log `list-tests22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test list --filter CompatibilityQaTests --limit 5
```

123. Exit **1**; log `repair-qa22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor refresh --compile
```

124. Exit **0**; log `repair-qa66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 editor refresh --compile
```

125. Lifecycle: stop tracked Editor for manifest resolution and restart QA (PID 41212).

126. Exit **0**; log `cancel-6000.6.7f1-list.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test list --filter CompatibilityQaTests --limit 5
```

127. Exit **0**; log `cancel-6000.6.7f1.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test cancel
```

128. Exit **1**; log `cancel-6000.6.7f1-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 test --mode EditMode --filter CompatibilityQaTests.BoundedRun
```

129. Exit **0**; log `restart-status22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

130. Exit **1**; log `restart-read22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject get_transform --target /DeveloperModeQA
```

131. Exit **0**; log `cancel-2022.3.62f2-list.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test list --filter CompatibilityQaTests --limit 5
```

132. Exit **0**; log `cancel-2022.3.62f2.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test cancel
```

133. Exit **1**; log `cancel-2022.3.62f2-second-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --mode EditMode --filter CompatibilityQaTests.Fast
```

134. Exit **0**; log `preflight-off22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode off
```

135. Exit **1**; log `locked-warm-exec22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

136. Exit **0**; log `cancel-2022.3.62f2-run.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 test --mode EditMode --filter CompatibilityQaTests.BoundedRun
```

137. Exit **0**; log `preflight-on22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode on
```

138. Exit **0**; log `preflight-warm22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call scene --json '{"action":"info"}'
```

139. Exit **0**; log `preflight-off-warm22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode off
```

140. Exit **1**; log `preflight-exec-warm22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 exec 'return 1;' --depth 1
```

141. Exit **0**; log `off-mode66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 scene info
```

142. Exit **1**; log `strict-exec-off22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call exec --json '{"code":"return 1;"}'
```

143. Exit **1**; log `strict-exec-off-fixed22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 call exec --json '{"code":"return 1;"}'
```

144. Exit **0**; log `enable-final22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 asset-config developer-mode on
```

145. Exit **0**; log `load-after-restart22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene load Assets/DeveloperModeQA.unity
```

146. Exit **0**; log `persisted-after-restart22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 manage_gameobject get_transform --target /DeveloperModeQA
```

147. Exit **0**; log `stale-heartbeat22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

148. Exit **0**; log `request-during-compile22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 scene info
```

149. Exit **0**; log `overlap-refresh22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 editor refresh --compile
```

150. Exit **0**; log `reloaded-status22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 status
```

151. Exit **0**; log `reloaded-console22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```

152. Exit **0**; log `final-console66.out`.

```text
$R/hera-agent-unity --project $R/Normal-6000.6.7f1 console --type error --lines 10
```

153. Exit **0**; log `final-console-afterqa22.out`.

```text
$R/hera-agent-unity --project $R/Normal-2022.3.62f2 console --type error --lines 10
```
