# hera-agent-unity

> See [`AGENTS.md`](AGENTS.md) for the canonical cross-tool agent rules file (read natively by Codex, Claude Code, Cursor, Copilot, and 30+ tools).

CLI tool to control Unity Editor from the command line.
Unified successor to `hera-agent` + `hera-agent-pro`. All features ship free under MIT.

## 협업 개발 (Claude ↔ Codex)

hera-agent-unity는 Claude(Claude Code)와 Codex가 **협업해서 개발**하는 프로젝트다. 목표는 완벽한 hera를 함께 만드는 것 — 한 에이전트가 놓친 것을 다른 에이전트가 잡는다. (이건 hera를 *사용*하는 이야기가 아니라 hera라는 도구 *자체를 개발*하는 협업 원칙이다.)

- **git 히스토리 = 공유 인계 채널.** Codex는 대화 맥락이 아니라 git 커밋 히스토리로 프로젝트 상태를 파악한다. 그래서 작업은 명확한 conventional-commit 메시지로 커밋하고, 큰 기능은 `docs/CODEX_HANDOFF_*.md` 같은 인계 문서를 남겨 다음 에이전트가 이어받게 한다.
- **교차검증.** 한쪽이 구현하면 다른 쪽이 리뷰·검증하는 것을 기본으로 한다. 자기 작업을 자기가 승인하지 않는다.
- **공통 규약 준수.** 둘 다 이 `CLAUDE.md`(설계 의도·체크리스트·"이미 처리된 항목" 표)와 `AGENTS.md`(hera 사용 규약)를 따르고, 🔒 잠긴 설계 결정을 존중한다.
- **정확성 우선.** 추측 대신 실측으로 근거를 만든다 — `hera-agent-unity`로 라이브 Unity를 확인하고, 버전별 사실은 바이너리 리플렉션에서 뽑는다.

## 설계 의도

**CLI(단일 Go 바이너리 + localhost HTTP) 구성은 의도된 선택이다.** MCP / WebSocket relay / 영속 서버 / Python 런타임 같은 대안으로 전환하자는 제안은 하지 말 것.

이유:
- 런타임 의존성 0개 — 사용자는 바이너리 하나 + UPM 패키지 하나만 설치
- Stateless — 모든 요청이 독립적이라 세션·재연결 로직 불필요
- 도메인 리로드를 파일시스템 버스(`~/.hera-agent-unity/instances/`, `status/`)로 우회
- 어떤 셸·AI 에이전트·스크립트에서도 호출 가능 (MCP 클라이언트에 묶이지 않음)

**파생 원칙** — decoupled/비대칭이 *의도된* 곳에 결합·통일 제안 금지:

- **CLI ↔ Connector 버전 핸드셰이크 불필요**: 두 버전이 일치한다는 전제 자체가 없음. HTTP+JSON forward-compat과 동적 dispatch가 자연 처리. "버전 매칭 검사 추가하자"는 제안은 모델 밖.
- **버전 용어 분리 필수**: CLI release tag(`hera-agent-unity vX.Y.Z`, git tag)는 Go 바이너리 버전이다. Unity UPM connector 버전은 `AgentConnector/package.json` 의 `version` 값(`0.0.N`)이다. 둘은 독립 버전이므로 같은 숫자라고 말하거나, UPM 패키지 버전을 CLI tag처럼 `vX.Y.Z`로 부르지 않는다. Git lock hash/commit 은 "어느 커밋의 connector를 받았는지"만 증명하며 UPM package version 자체가 아니다.
- **양방향/스트리밍 채널 없음**: 단발성 호출이 디폴트. "lock 점유자 보여달라", "진행률 스트림", "실시간 알림" 같은 제안은 모델 밖.
- **단일 에디터 모델 (멀티 에디터 미지원)** 🔒: 한 머신에 Unity 에디터 하나를 전제. 포트 바인딩 구조상 같은 머신에서 멀티 에디터는 실사용 불가 — instance discovery 는 "한" 인스턴스를 해석하고, 재시도·재해석(`doWithReloadRetry` 의 `DiscoverInstance` 포트 추종 등)이 *다른* 에디터를 집을 위험은 **모델 밖**. "여러 에디터 구분/디스앰비규에이션 추가하자", "재해석이 substring 매치로 잘못된 에디터를 고를 수 있다", "PID 로 정확 매칭하자" 같은 지적·제안은 모델 밖 — 멀티 인스턴스 충돌은 발생하지 않는 전제이므로 새 문제로 제기 금지.
- **출력 비대칭은 명령별로 분리** — 세 부류:
  - **표준 envelope tool 명령** (`exec`, `editor`, `console`, `scene`, `menu`, `screenshot`, `reserialize`, `test`, `profiler`, `list`, `describe_type`, `find_method`, `list_assemblies`, `batch`, `log`, `manage_gameobject`, `find_gameobjects`, `manage_components`, `manage_packages`, `unity_docs`, `describe_shader`, `manage_material`, `manage_prefab`, `manage_asset_import`, `manage_assets`, `manage_ui`, `ui_doc`, custom tools): 성공/실패 응답은 **compact JSON** 으로 통일 — AI agent 가 소비. 박스 drawing / ANSI escape / 한국어 banner 금지. `humanCategories` 화이트리스트(`cmd/root.go`)에 없으면 자동으로 compact + stderr 장식 억제.
  - **human 명령** (`install`, `uninstall`, `status`, `update`, `doctor`, `help`, `version` + 별칭): `humanCategories` 화이트리스트 등재. `tui.ErrorPanel` / `BoxAccent` / banner / `printUpdateNotice` 유지.
  - **자체 출력 경로 명령** (`asset-config`, `ping`): `printResponse` 를 거치지 않고 직접 출력. `asset-config` 는 기본 styled + `--json` 로 AI 모드. `ping` 은 단일 라인 `port=N alive=N state=... age_ms=N`. `doctor` 도 human 카테고리지만 `--json` / `--agent-rules` 분기 별도.
  - "tool 에러도 인간이 읽는다"는 가정은 audience reality와 어긋남 (실제로 tool 호출 = AI). 새 명령 추가 시 `humanCategories` 등재 여부가 출력 모드를 결정한다.

- **외부에서 참고한 것은 완전한 헤라화 후에만 올린다** 🔒: 외부 도구·논문·아티클을 연구해 헤라 기능을 설계하는 건 권장. 단 **레포에 남는 결과물은 이식본이 아니라 처음부터 Unity 전용으로 만들어진 장비처럼 읽혀야** 한다.
  - **기원 서사 0**: 도구 `Description`, agent-rules 문자열, 코드 주석, `CLAUDE.md`, `CHANGELOG.md`, **커밋 메시지** 어디에도 출처를 남기지 않는다. *무엇을 하는 기능인지*만 쓰고 어디서 왔는지는 쓰지 않는다.
  - **외부 기준점 금지**: "ported/이식/편입" 같은 동사, 그리고 원 도메인과의 대조("웹과 달리", "CSS로 치면")를 쓰지 않는다. Unity 사실로 직접 서술한다 — **원 도메인을 언급해야만 설명되는 규칙은 아직 헤라화가 안 된 것**이다.
  - **번역이 아니라 재도출**: 술어·임계값·어휘는 실제 Unity API(`manage_components` 프로퍼티, 버전 버킷별 USS 어휘)에 대고 새로 저술하고 라이브 검증한다. 변환물이 아니라 Unity 에서 옳은 것이 되게.
  - **외부 코드·원문 무반입**: 아이디어·방법론은 배워도 되지만 소스 파일·데이터 파일·문장 복사는 반입 금지. 헤라 배포물에 제3자 라이선스가 붙지 않게 유지한다.
  - **작명은 헤라 어휘로**(`ui_slop`, `game_feel`, `unity_docs`) — 외부 도구 이름을 따지 않는다.

새 기능을 추가할 때도 이 모델 안에서 풀 것: HTTP 한 번 / 필요하면 파일 폴링.

## Structure

```
cmd/                  # Go CLI — thin passthrough layer
  root.go             # Entry point, flag/arg parsing, humanCategories, response printing
  dispatch.go         # Standalone vs Unity-backed command routing
  editor.go           # editor command (waitForReady polling)
  test.go             # test command (EditMode/PlayMode result polling via pollResultFile)
  internal/poll/      # (extracted from cmd/) shared pollResultFile file-bus poller
                      # w/ exponential backoff (100ms→1.5s) + state/PID liveness (test + packages)
  status.go           # status + ping + waitForAlive/waitForState/waitForReady
                      # (heartbeat reads, same backoff)
  update.go           # self-update from GitHub releases (download + rename dance)
  version_check.go    # periodic update notice (12h interval, human-only)
  asset_config.go     # asset plugin config (TUI default + --json for AI,
                      # includes Ultra Hera loopEngineeringMode for agent rules)
  batch.go            # batch (multi-command) dispatch + --dry-run preview
  manage_packages.go  # async job_id dispatch + pollResultFile (file-bus, like test)
  unity_docs.go       # thin passthrough — connector ships its own data set
  ui_doc.go           # ui_doc tool: CLI-side sample (color measure) + catalog
                      # (folder scan → manifest, image decode, no Unity);
                      # apply/import inject --file→doc param
  html_to_uidoc.go    # HTML mockup → ui_doc/2 JSON (CLI-side, no Unity)
  install.go          # self-install onto PATH + legacy scrub
  uninstall.go        # self-uninstall (+ uninstall_{unix,windows}.go variants)
  doctor.go           # self-diagnostic; embeds AGENT.md for --agent-rules
  paths.go            # install path resolution (+ paths_windows.go)
  path_check.go       # per-command PATH-mismatch warning (HERA_AGENT_NO_PATH_CHECK)
  deferred_delete_*.go # Windows-safe .bak cleanup after self-update
  AGENT.md            # embedded copy for `doctor --agent-rules` (go:embed)
internal/client/      # Unity HTTP client, instance discovery, SendBatch
                      # + process_{unix,windows}.go (PID liveness check)
internal/assetconfig/ # Asset plugin configuration persistence
                      # (assets + ui_system + game_feel_mode + game_feel_ui_mode + loopEngineeringMode)
internal/tui/         # Terminal UI helpers: style.go, assetconfig.go (bubbletea), detect.go
tools/build-unity-docs/ # One-shot maintainer Go script: Documentation/en/ScriptReference
                        # → unity_docs_<ver>.jsonl(.gz)(.bytes). Run per Unity version.
tools/build-uitk-schema/ # One-shot maintainer Go reflection extractor: Editor UI Toolkit
                         # binaries → uitk_schema_<bucket>.jsonl.gz.bytes. Run per Unity version.
tools/build-game-feel-docs/ # game_feel.jsonl (checked-in source of truth, curated from
                            # Game Feel & Juice Bible + Ethical Engagement Framework)
                            # → validate + gzip → Data/game_feel_1.0.jsonl.gz.bytes.
tools/build-ui-slop-docs/ # ui_slop.jsonl (checked-in source of truth: Unity UI-slop
                          # taxonomy, 49 tells across areas A–E)
                          # → validate + gzip → Data/ui_slop_1.0.jsonl.gz.bytes.
AgentConnector/       # C# Unity Editor package (UPM) — package.json holds version
  Editor/
    HttpServer.cs     # [InitializeOnLoad] HttpListener + queue + main-thread pump
    CommandRouter.cs  # SemaphoreSlim lock (120s) + Dispatch / DispatchBatch
    Heartbeat.cs      # 1.0s atomic write to ~/.hera-agent-unity/instances/<md5>.json
    ToolDiscovery.cs  # [HeraTool] reflection cache + Levenshtein "did you mean"
    HeraAgent.asmdef
    HeraAgentAssetConfigWindow.cs   # Editor GUI for asset-config
    Core/             # Response, ParamCoercion, ToolParams,
                      # AtomicFile (temp-write + replace for JSON file bus),
                      # AssetPathGuard (normalized Assets/ containment),
                      # StringCaseUtility, ToolMetadata, UnityPitfalls,
                      # HierarchyPath (Build: Transform→path; Find:
                      # path→GameObject, inactive-aware — shared by
                      # manage_gameobject/components/ui),
                      # ComponentTypeResolver (TypeCache + suggest),
                      # SerializedPropertyValue (JSON ↔ SerializedProperty +
                      # ObjectReference resolver — manage_components base),
                      # PackageJobState ([InitializeOnLoad] job watcher),
                      # UnityDocsStore (gzipped JSONL → dict + 3-layer
                      # prefix/length/bounded Levenshtein suggest),
                      # GameFeelStore (game_feel bundle → dict + full-scan
                      # Levenshtein suggest — corpus ~40, 3-layer 미적용),
                      # UiSlopStore (ui_slop bundle → dict + full-scan Levenshtein
                      # + area-grouped BuildIndex + CheckFor(ui_system slice);
                      # GameFeelStore 패턴 복제, corpus ~50),
                      # Levenshtein (shared edit-distance helper),
                      # HeraSettings (reads shared asset-config.json at dispatch
                      # time — UiSystem + GameFeelMode + GameFeelUiMode + UiSlopMode
                      # + DotweenPreferred, mtime-cached),
                      # UIJuiceGuide (per-UI-element juice recipes from Juice
                      # Bible + UI Feedback Guide + UIUX Theory + Ethical
                      # Framework; uGUI DOTween-aware / UITK USS-first,
                      # pointer — manage_ui/ui_doc agent_hint source),
                      # TargetResolver (GameObject/component lookup from
                      # ToolParams — instance_id > path; shared target helper),
                      # EntityIdCompat (instanceID→EntityId rename shim,
                      # Unity 6000.5 gate — int instance_id contract preserved),
                      # SchemaUtility (C#→JSON-Schema type map for
                      # ToolDiscovery/ToolMetadata),
                      # safety metadata flags on [HeraTool] and per-action
                      # [HeraActionSafety] (shown only by list --tool, not
                      # list --compact),
                      # ProceduralSprite (Tier-1 sprite bake+import:
                      # solid/rounded_rect/gradient/nine_slice — ui_doc
                      # gen_sprite/apply; Assets/HeraGenerated default),
                      # UiDocSchema (ui_doc/2 IR: uGUI subtree ↔ compact JSON,
                      # ExportNode/ApplyNode + anchor presets/layout),
                      # UiDocFixer (official uGUI docs bucket selection +
                      # deterministic fixes/diagnostics for ui_doc apply),
                      # UiToolkitStore (bundled reflection schema loader),
                      # UiToolkitFixer (runtime element/UXML/USS validation +
                      # runtime world-space gate),
                      # UiToolkitDocument (UXML/USS/PanelSettings/UIDocument
                      # emitter, reflected UI Toolkit types only)
    Tools/            # Tool implementations (auto-registered via [HeraTool]).
                      # 30 [HeraTool] classes. Name= explicit unless noted
                      # (no Name= → filename snake_case). ExecCompileCache.cs is
                      # NOT a tool — internal helper for exec compile caching.
                      #   exec        ExecuteCsharp
                      #   console     ReadConsole  /  log          LogToConsole
                      #   scene       ManageScene  /  menu          ExecuteMenuItem
                      #   screenshot  EditorScreenshot / profiler   ManageProfiler
                      #   reserialize ReserializeAssets / detect_assets DetectAssets(no Name=)
                      #   describe_type DescribeType / find_method  FindMethod
                      #   list_assemblies ListAssemblies
                      #   manage_editor ManageEditor(no Name=) / refresh_unity RefreshUnity(no Name=)
                      # Post-v0.0.6 queue (all shipped): manage_gameobject ManageGameObject /
                      #   manage_packages ManagePackages / find_gameobjects FindGameObjects /
                      #   manage_components ManageComponents / unity_docs UnityDocs.
                      # Game Feel Mode: game_feel GameFeel (bundled knowledge
                      #   base lookup — always on; toggle gates hints only).
                      # Unity De-slop Mode: ui_slop UiSlop (bundled UI-slop taxonomy
                      #   lookup — always on; toggle gates hints only).
                      # Asset-editing queue v0.0.14 (all shipped): describe_shader
                      #   DescribeShader / manage_material ManageMaterial /
                      #   manage_prefab ManagePrefab / manage_asset_import ManageAssetImport.
                      # AssetDatabase utility v0.0.46: manage_assets ManageAssets
                      #   (find/mkdir/create/copy/move/delete, Assets/ containment;
                      #   create = ScriptableObject .asset authoring via TypeCache
                      #   + optional SerializedPropertyValue field set).
                      # uGUI queue v0.0.15 (shipped): manage_ui ManageUI
                      #   (RectTransform anchor/pivot/preset + UI-aware create;
                      #   UI/TMP types via TypeCache → no com.unity.ugui compile dep).
                      # HTML→UI pipeline: ui_doc UiDoc — export/apply (ui_doc/2
                      #   uGUI IR via UiDocSchema or UITK UXML/USS via
                      #   UiToolkitDocument) / gen_sprite (ProceduralSprite) /
                      #   capture (overlay-canvas render) / import (external
                      #   sprites → Sprite assets) / sample + catalog (CLI-side).
                      # Animation authoring v0.0.59: manage_animation ManageAnimation
                      #   (create_clip/set_curve → AnimationClip float curves;
                      #   create_controller/add_parameter/add_state/add_transition
                      #   → AnimatorController state machine on base layer;
                      #   animation types = built-in engine module → no asmdef ref).
    Data/             # Bundled data (UPM-shipped, immutable). Versioned
                      # unity_docs_<bucket>.jsonl.gz.bytes bundles for
                      # 2022.3 / 2023.2 / 6000.0 / 6000.3 / 6000.5 plus
                      # legacy unity_docs_6.0 fallback, regenerated by
                      # tools/build-unity-docs; reflected runtime-only
                      # uitk_schema_<bucket>.jsonl.gz.bytes bundles for the same
                      # five buckets, regenerated by tools/build-uitk-schema;
                      # game_feel_1.0.jsonl.gz.bytes
                      # (Game Feel Mode knowledge base, ~30 KiB) regenerated by
                      # tools/build-game-feel-docs; ui_slop_1.0.jsonl.gz.bytes
                      # (Unity De-slop Mode taxonomy, 49 tells, ~10 KiB) regenerated
                      # by tools/build-ui-slop-docs. Folder needs its own .meta
                      # (folderAsset: yes) or UPM ignores the contents.
    TestRunner/       # RunTests + TestRunnerState (domain-reload safe via files)
    Attributes/       # [HeraTool], [ToolParameter]
```

## Development

### Adding a Command

#### C# side

1. Add a C# tool in `AgentConnector/Editor/Tools/` with `[HeraTool(Name = "command_name")]`.
2. CLI command name matches the tool name — default passthrough handles dispatch.
3. Positional args arrive as the `args` array, flags as named params.

#### Go side

4. Add handler in `cmd/<command>.go` if the command needs polling/waiting logic (editor, test, manage_packages, etc.). Passthrough commands need no Go handler.
5. Add routing in `cmd/root.go` `Execute()` switch. Default passthrough (fallthrough to `buildParams` + `send`) is enough for simple commands.
6. Add to `cmd/root.go` `humanCategories` if the command is **human-target** (install, status, doctor, etc.). Omit for AI-target tool commands (exec, manage_components, batch, etc.).
7. Add help text in `cmd/root.go` `printHelp()` overview and `printTopicHelp()` detailed section.

### Adding C# files to the Connector (.meta is mandatory)

새 `.cs` 파일을 `AgentConnector/` 아래에 추가할 때는 **같은 폴더에 `<file>.cs.meta`를 함께 커밋한다.** UPM 패키지 안의 `.cs`는 immutable로 취급되어 Unity가 .meta 없는 파일을 컴파일 대상에서 제외함 — Unity 안에서 직접 만든 게 아니므로 자동 생성도 안 됨. 누락 시 사용자는 cascading "name does not exist" 컴파일 에러를 봄.

**폴더에도 .meta 필요.** `AgentConnector/Editor/Data/` 같은 새 디렉토리를 commit 할 때 `<folder>.meta` (sibling, not inside) 도 같이 — `folderAsset: yes` 키 + DefaultImporter. 없으면 UPM 이 `Asset .../X has no meta file, but it's in an immutable folder. The asset will be ignored.` 에러 + 폴더 안 *모든 자식* 무시.

절차:
1. 기존 .meta 한 개 복사 (파일이면 `ExecuteMenuItem.cs.meta`, 폴더면 다른 폴더의 .meta).
2. GUID를 새로 발급해서 덮어쓰기:
   ```bash
   od -An -N16 -tx1 /dev/urandom | tr -d ' \n'
   ```
3. `find AgentConnector -name "*.meta" -exec grep -h "^guid:" {} \; | sort | uniq -d` 로 충돌 없음 확인.
4. `.cs`/폴더와 그 .meta 한 커밋에 같이 넣기.

### Namespace 충돌 함정 (CS0104) — `[HeraTool]` 작성 시 grep 한 번

`using System;` + `using UnityEditor;` 가 거의 항상 같이 쓰여서 다음 type 들은 충돌:

- `Object` → `System.Object` vs `UnityEngine.Object`. `UnityEngine.Object.Destroy(...)` 명시, 또는 `using Object = UnityEngine.Object;`.
- `PackageInfo` → `UnityEditor.PackageInfo` (legacy AssetStore) vs `UnityEditor.PackageManager.PackageInfo`. `using PackageInfo = UnityEditor.PackageManager.PackageInfo;`.
- 예방적 후보: `Random` (System vs UnityEngine), `Debug` (System.Diagnostics vs UnityEngine).

새 `.cs` 파일 *Unity 컴파일 트리거 직전* `Object|PackageInfo|Random|Debug` grep 으로 미리 정규화하면 hotfix 사이클 절감 (post-v0.0.6 큐 5건 중 3건이 이 패턴으로 hotfix 발생).

### Code Quality Guidelines

리팩토링이나 코드 리뷰 시 다음 패턴은 의도된 설계가 아닌 경우 **즉시 제거/통일**한다:

1. **Simple delegation wrapper 제거** — 아무 로직 없이 그대로 전달만 하는 함수는 호출처에서 직접 호출.
2. **Dead code 제거** — 삼항 연산자/조건 분기의 true/false 결과가 동일하면 하드코딩.
3. **중복 로직은 기존 유틸리티 재사용** — `StringCaseUtility.ToSnakeCase`, `ToolParams` 등 이미 존재하는 유틸리티를 중복 구현하지 않음.
4. **C# 에러 메시지 스타일** — 모든 에러/경고 메시지는 `[Hera] I ...` 1인칭 스타일로 통일. 기계적 문장(`"Command dispatch timed out..."`) 대신 자연스러운 1인칭(`"[Hera] I couldn't acquire the command lock..."`) 사용.
5. **Fire-and-forget 예외 처리** — `_ = ProcessItemAsync(item)`처럼 discard된 async 호출은 unobserved exception 위험이 있음. `.ContinueWith(..., TaskContinuationOptions.OnlyOnFaulted)`로 명시적 예외 처리.
6. **CommandRouter 타임아웃 (120초)는 건드리지 않음** — 이 값은 `SemaphoreSlim` lock 획득 대기 시간이며, **개별 명령어의 실행 시간과 무관**. 컴파일처럼 오래 걸리는 작업은 이미 heartbeat 폴링(`waitForReady`)으로 처리됨. 전체 타임아웃을 늘리면 profiler 추출 등 빠른 명령어도 불필요하게 기다리게 되므로 수정 금지.

### 이미 처리된 항목 (다른 세션에서 중복 제안 금지)

| 항목 | 상태 | 비고 |
|------|------|------|
| `readStatus()` delegation wrapper | ✅ 제거됨 | `client.FindByPort()` 직접 호출 |
| `splitArgs()` switch-case | 🔒 의도된 설계 | slice 상수 + helper로 리팩토링했다가 더 장황해져서 원복. 8줄 switch가 18줄 슬라이스 스캔보다 명확하고 O(1). 다시 손대지 말 것 |
| `envInt/Str/Bool` 위치 | 🔒 의도된 설계 | `cmd/root.go` 안의 private helper로 유지. 한 번 `internal/envutil`로 추출했다가 소비자 없어서 원복 — premature abstraction. 두 번째 소비자 생기면 그때 분리 |
| `check` → `compile_only` 매핑 | ✅ 이동됨 | `buildParams()` 안에서 일괄 처리 |
| asset-config 한/영 혼용 | ✅ 영어 통일 | 카테고리명, help text, 출력 메시지 모두 영어 |
| `GetSnakeCaseName` 중복 | ✅ 제거됨 | `StringCaseUtility.ToSnakeCase()` 사용 |
| ExecuteCsharp 삼항 연산자 | ✅ 단순화 | `const string name = "csc.exe"` |
| ProcessItem fire-and-forget | ✅ 수정됨 | v0.0.13: `ListenLoop`/`ProcessItem`/`HandleRequest` 모두 `ContinueWith(..., OnlyOnFaulted)` 적용 |
| CommandRouter 에러 메시지 | ✅ 스타일링 완료 | `"[Hera] I waited 120s for the command lock..."` 형태 |
| `gocritic` 린터 | 🚫 제외됨 | `unlambda`/`ifElseChain` 등 false-positive 多. `.golangci.yml`에서 뺌. `goimports`/`govet`/`staticcheck`/`ineffassign`/`misspell`/`bodyclose`만 활성화 |
| HTTP body drain (non-200) | ⏭️ 넘어감 | stateless 단발성 + `io.ReadAll`로 충분히 drain |
| 테스트 커버리지 확대 | 🗑️ 폐기 | 실제 Unity Editor 없이는 비현실적. 통합 테스트만 의미 있음 |
| 120초 타임아웃 | 🔒 의도된 설계 | `SemaphoreSlim` lock 대기 시간. 변경 금지 |
| Go 에러 wrapping `%v` → `%w` | ✅ v0.0.5 적용 | `internal/client` + `cmd` 8곳 전환. `errors.Is`/`errors.As` 가능 |
| `ExecCompileCache` SHA256 dispose | ✅ v0.0.5 수정 | `ComputeKey`/`HashStrings` 모두 `using var sha = SHA256.Create()` |
| `batch --help` 의 `manage_editor refresh` 예시 | ✅ v0.0.6 교체 | `refresh_unity` / `compile:"request"` 로 정정 — 원래 action 미존재 |
| `cmd/test.go` 의 메시지 문자열 분기 | ✅ v0.0.6 수정 | `resp.Code == "UNKNOWN_COMMAND"` 로 변경 (AGENT.md Rule 3 준수) |
| `humanCategories` 문서의 `upgrade` 단어 | ✅ v0.0.6 제거 | 실제 코드엔 없음 — `upgrade` 입력 시 `UNKNOWN_COMMAND` + did_you_mean 반환 |
| asset-config TUI 의 dead `FullHelp`/`ShortHelp` | ✅ v0.0.6 제거 | `bubbles/help` import 안 함, 호출처 0개 |
| `internal/assetconfig` 의 dead `SetAssetInstalled` 주석 | ✅ v0.0.6 제거 | 함수 자체는 이전에 삭제됨, 고아 주석만 남아 있던 것 |
| post-v0.0.6 capability queue (5건) | ✅ 완료 (2026-05-28) | `manage_gameobject` v0.0.5 / `manage_packages` v0.0.6 / `find_gameobjects` v0.0.7 / `manage_components` v0.0.8 / `unity_docs` v0.0.10–0.0.12. vault `capability-gaps-priorities-final.md` §5 잠금된 큐. 다음 큐는 새 vault 결정 시 lock-in |
| asset-editing capability queue (4건) | ✅ 완료 (2026-06-02, v0.0.14) | `describe_shader` / `manage_material` / `manage_prefab` / `manage_asset_import` 4종 전부 구현+로컬 dev 패키지로 end-to-end 검증 완료. ②자산편집 공백(prefab/material/shader) 메움. `manage_material`/`manage_asset_import`는 `Core/SerializedPropertyValue` 재사용(후자는 manage_components 패턴을 AssetImporter에 그대로 적용; `TryParseColor`/`TryParseFloats` public화). `manage_prefab`은 `PrefabUtility.LoadPrefabContents`→edit→`SaveAsPrefabAsset`→`UnloadPrefabContents` 단발 headless(=PrefabStage 불필요). 4종 모두 live `exec`로 end-to-end 검증 후 출시 |
| `manage_ui` (uGUI) | ✅ 완료 (2026-06-02, v0.0.15) | RectTransform anchor/pivot/preset + UI-aware `create`(canvas/panel/image/button/text/empty, Canvas+EventSystem 자동 스캐폴딩). Scope 경계 🔒: 요소 *프로퍼티* 편집은 `manage_components` 위임(중복 금지). 구현 🔒: UI/TMP 타입은 `ComponentTypeResolver`(TypeCache)+`AddComponent(type)`로 해석 → asmdef `references:[]` 유지, com.unity.ugui/TMP 컴파일타임 의존 0. anchor 🔒: named grid 프리셋(`top-center`…`stretch`)+raw, 기본 시각 rect 보존(offset 재계산), `--snap`=Alt+Shift. Text 🔒: auto(TMP 있으면 TMP, 없으면 legacy `Text`+`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`), `--text tmp\|legacy` 오버라이드. v0.0.14 cohort 선례 따라 root.go help 미등재(README/docs/COMMANDS/CHANGELOG만). live end-to-end 검증 완료 |
| reliability/efficiency pass (v0.0.13) | ✅ 완료 (2026-06-01) | `cmd/poll.go` 공유 `pollResultFile` 추출 (test + manage_packages 통합) + 고정 500ms sleep → exponential backoff (100ms→1.5s). status 폴링도 동일 backoff. `Heartbeat` 1.0s 로 완화 (3s stale margin 유지). HTTP idle pool MaxIdleConns 4→8 / PerHost 2→4. C# `ManageComponents`/`ExecuteCsharp` `GetType()` 캐시. 다시 제안 금지 |
| `unity_docs` 데이터 소스 | 🔒 의도된 설계 (v0.0.10+, 2026-07-03 exact bucket 검증) | 사용자 PC 의 `Documentation/en` 직접 읽기는 *폐기됨* (v0.0.9 만). 공식 Unity ScriptReference 루트에서 maintainer 가 빌드 도구 (`tools/build-unity-docs`) 로 한 번 추출 → `AgentConnector/Editor/Data/unity_docs_<bucket>.jsonl.gz.bytes` 로 UPM 패키지에 동봉. 현재 동봉 bucket: `2022.3`/`2023.2`/`6000.0`/`6000.3`/`6000.5`; 없으면 `6000.0`/legacy `6.0` bundle 로 fallback. 사용자 docs 폴더 의존성 0. 테스트: `HeraAgent/Tests/UnityDocsStore` + `unity_docs GameObject` on `6000.0.35f1`. 다시 *런타임 HTML fetch* 또는 *docs_root path resolution* 제안 금지 |
| `unity_docs` SuggestSimilar 알고리즘 | 🔒 의도된 설계 (v0.0.12) | prefix bucket + length filter + bounded Levenshtein 의 3-layer pre-filter. miss latency 2–5ms (vs 이전 ~290ms). 첫 글자 typo 는 full-scan fallback. ToolDiscovery / ComponentTypeResolver 는 corpus 작아서 동일 최적화 *미적용* (premature) — 같이 묶지 말 것 |
| `unity_docs` 응답 shape | 🔒 의도된 설계 (v0.0.12) | `{title, signature, summary}` 만. `query_normalized` / `manual_url` / `scriptreference_url` / `unity_version` 은 caller 가 알거나 유도 가능해서 제거. 토큰 ~33. 다시 *verbose shape* 또는 *manual_url 자동 fetch* 제안 금지 |
| `Levenshtein` / `HierarchyPath` / `ComponentTypeResolver` Core 추출 | ✅ 완료 (v0.0.7–0.0.10) | 3 consumer 발생 시 추출 (premature 회피). HierarchyPath = ManageGameObject + FindGameObjects. ComponentTypeResolver = FindGameObjects + ManageComponents. Levenshtein = ToolDiscovery + ComponentTypeResolver + UnityDocsStore. 새로 다시 묶지 말 것 |
| `HierarchyPath.Find` (inactive-aware path→GO) Core 추출 | ✅ 완료 (v0.0.16) | manage_ui 가 3번째 소비자가 되며 verbatim `FindByPath`/`ResolveByPath`+`WalkPath` 복사본이 ManageGameObject/ManageComponents/ManageUI 3곳 → `Core/HierarchyPath.Find` 로 통합 (Build 의 역방향). 3 consumer 임계 충족. 다시 인라인 복사 금지. (ManagePrefab 의 `ResolveByPathOrId` 는 inactive walk 없는 별개 헬퍼 — 묶지 말 것) |
| `manage_ui` EventSystem 입력 모듈 | ✅ 수정 (v0.0.16) | `create` 가 항상 StandaloneInputModule 우선 시도 → new-Input-System 전용 프로젝트에서 매 프레임 throw + 입력 무반응 버그. `#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER` (Unity 내장 디파인 = 활성 입력 핸들링 반영, asmdef 참조 불필요) 로 게이트 — Unity 자체 EventSystem 메뉴와 동일. 다시 runtime 타입 존재 체크로 되돌리지 말 것 |
| `doWithReloadRetry` 60s fallback vs ctx 타임아웃 | 📝 알려진 이슈, 미수정 (v0.0.13) | `reloadRetryFallbackDeadline`(60s)가 호출자 ctx(=`flagTimeout`)와 무관하게 적용. **기본 설정에선 무해** — `flagTimeout` 기본 60s 라 두 deadline 이 일치. `--timeout`/`HERA_AGENT_TIMEOUT_MS` 를 60s 초과로 올린 + 도메인 리로드가 60s 초과인 대형 프로젝트에서만, 리로드 창 명령이 ctx 만료 전 60s 에 `"after 60s (still reloading?)"` 로 실패(올린 타임아웃이 리로드 인내를 60s 너머로 못 늘림). 실 사용 설정에서 발생 안 해 **의도적으로 미수정** — 다시 새 버그로 제기 말 것. 정 고치려면 deadline 을 ctx deadline 파생(없을 때만 60s)으로 |
| `manage_components` SerializedProperty 패턴 | 🔒 의도된 설계 (v0.0.8) | `Core/SerializedPropertyValue` 가 *모든 후속 manage_\* (material/animation/vfx/SO/prefab)* 의 property-set 토대. raw `m_X` 경로 그대로, friendly-name mapping *미적용* — Unity 가 실제 직렬화하는 이름과 1:1 유지. 다시 friendly mapping 제안 금지 |
| `manage_packages` 비동기 패턴 | 🔒 의도된 설계 (v0.0.6) | `add/remove/embed` 는 `job_id` 발급 후 파일버스. `list` 만 동기 (CommandRouter 락 안에서 60s budget). 도메인 리로드 후 `[InitializeOnLoad]` 가 `Client.List` 로 결과 검증. 다시 *전부 동기* 또는 *전부 비동기* 통일 제안 금지 |
| Game Feel UI Mode (Beta) (구 UI Juicy Mode) | ✅ 완료 (Connector v0.0.19, 명칭 변경 2026-07-03) | Hera Settings 체크박스(`asset-config.json` 의 `game_feel_ui_mode`) ON → `manage_ui create` 응답에 `agent_hint` 로 Game Feel & Juice Bible + UI Feedback Design Guide juice 레시피(element별) 주입. **표시명 🔒**: UI 표시명·JSON 키·CLI 서브커맨드는 `Game Feel UI Mode (Beta)` / `game_feel_ui_mode` / `gamefeel-ui` (구 `UI Juicy Mode` / `ui_juicy_mode` / `juicy` 에서 개명; `gamefeel` 은 전역 Game Feel Mode 가 차지). 구 `ui_juicy_mode` 키는 로드 시 자동 마이그레이션(Go `Load`, C# `LoadConfig`, `HeraSettings` 폴백 read) 후 다음 저장에서 제거; CLI `juicy` 서브커맨드는 하위호환 별칭으로만 잔존. 내부 클래스명 `Core/UIJuiceGuide`·`WithJuice`·"juice" game-feel 용어·"Game Feel & Juice Bible" 인용은 개명 대상 아님(유지). **가이드 주입 방식 🔒**(런타임 컴포넌트 자동부착 아님 — connector Editor 전용 유지, manage_ui scope 경계 유지). DOTween 우선은 기존 `dotween`/`dotween_pro` `enabled` 플래그 참조(`Core/HeraSettings.DotweenPreferred`) → DOScale vs lerp 분기. juice 지식은 **2층 구조** (2026-07-04 강화): 즉시 주입층 = `Core/UIJuiceGuide` 순수 문자열(Data 에셋 아님 🔒 — 4개 문서[Juice Bible + UI Feedback Guide + UIUX Theory + Ethical Framework]에서 강화: panel=선택 대칭 윤리+toast, image=rarity 5등급 사다리, text=크리티컬 스펙, bar=차지/쿨다운+저체력 펄스 가속, canvas=ECN-DMN 밀도+100ms/7±2/≤30%+접근성 기본), 심층 조회층 = 힌트 끝의 `game_feel` `ui` 토픽 포인터(요소별 `DeepTopics` 맵). connector 가 dispatch 시 `asset-config.json` 을 mtime-cache 로 읽음(`Core/HeraSettings`). CLI 는 `asset-config gamefeel-ui [on\|off]` + `--json` 의 `game_feel_ui_mode`/`dotween_preferred` 로 표면화. Go struct 에 `DefaultCscPath`/`DefaultDotnetPath` round-trip 필드 추가(CLI Save 가 Editor 컴파일러 경로 안 지우도록). 다시 *자동부착(runtime asmdef)* 또는 *친절-매핑* 제안 금지 |
| Game Feel Mode (Beta) | ✅ 완료 (2026-07-03, Connector 0.0.56) | 게임플레이 전역 game-feel 가이드 — Game Feel UI Mode 의 확장판이자 **독립 토글**(`asset-config.json` 의 `game_feel_mode`, CLI `asset-config gamefeel [on\|off]`, Hera Settings 별도 섹션). **지식 번들 🔒**: `Tools/GameFeel`(`game_feel`) + `Core/GameFeelStore` 가 `Data/game_feel_1.0.jsonl.gz.bytes`(54 토픽, ~38 KiB) 를 unity_docs 패턴으로 로드. 원천은 vault 의 Game Feel & Juice Bible + Ethical Engagement Game Feel Framework + UI Feedback Design Guide + UIUX Visual Theory & Trends (4개 문서) — repo 의 source of truth 는 `tools/build-game-feel-docs/game_feel.jsonl`(편집 후 `go run ./tools/build-game-feel-docs` 로 재생성). `ui` 카테고리(15 토픽: 요소별 스펙 + ECN-DMN + cognitive load + choice symmetry + 2026 트렌드)는 Game Feel UI Mode 힌트의 심층 레이어를 겸함. **Ethical 내장 🔒**: 윤리는 사후 검증 레이어가 아니라 각 레시피 본문에 접근성·정직성 제약이 포함(예: screen_shake→강도 옵션+모바일 70–80%, dynamic_lighting→광과민성, anticipation_reward→확률/천장 투명성) — 레시피대로 만들면 `ethics_checklist` 통과. 인덱스도 ethics 우선 정렬. **조합 주입 🔒**: (a) `doctor --agent-rules` 가 모드 ON 일 때 독립 섹션(Ultra Hera 와 무관) 주입, (b) `game_feel` 조회 도구는 토글 무관 상시 동작, (c) `manage_components add` 가 Camera/ParticleSystem/AudioSource/Rigidbody(2D)/CharacterController/Light(2D)/Animator/Animation 에 1줄 topic-pointer `agent_hint` 부착(전문 주입 금지 — 토큰 봉인). Suggest 는 full-scan Levenshtein(corpus ~40 — 3-layer 최적화 미적용, unity_docs 잠금 원칙과 일관). UI 레시피 주입은 계속 `UIJuiceGuide`/UI 모드 소관(중복 주입 금지). 다시 *런타임 자동부착*, *전문 힌트 주입*, *Ethical 을 검증 전용으로 분리* 제안 금지 |
| Ultra Hera | ✅ 완료 | Hera Settings 에서 `Ultra Hera`를 `Off` / `Light` / `Ultra` 중 하나만 선택. 저장 키는 `asset-config.json` 의 `loopEngineeringMode`, 기본값은 `light`. **가이드 주입 방식 🔒**: Hera가 AI 작업 루프를 직접 실행하지 않음. `doctor --agent-rules`가 현재 모드를 읽어 Codex/Claude/기타 agent 규칙에 검증 강도 지침을 넣는다. `Ultra` 선택 시 모든 작업은 Light 확인(컴파일 상태·최근 콘솔 에러·변경 대상 확인)을 적용하고, `ultra`/`strict`/`꼼꼼히`/`정확히`/`검증까지`/`테스트까지`/`플레이해서 확인`/`UI 맞춰줘`/`스크린샷` 같은 키워드나 PlayMode·시각 UI·scene/prefab·release/PR 검증 작업에서 Ultra 확인(PlayMode, 테스트, 스크린샷, 씬 점검)으로 승격. 사용자 문구는 초등학생도 이해할 수 있는 쉬운 문장 유지. 내부 키 이름은 기능 의미 보존을 위해 `loopEngineeringMode` 유지, UI 표시명만 `Ultra Hera` |
| UI Toolkit UXML/USS scaffold | ✅ 완료 (2026-07-13, Connector 0.0.61) | 최상위 `ui_system`(`ugui` 기본 / `uitk`)은 Go `asset-config`, Hera Settings, `HeraSettings` dispatch cache에 함께 저장. `uitk`에서는 기존 `ui_doc apply`/`manage_ui create`가 runtime-only reflection bundle(`UiToolkitStore`)로 정확한 element/UXML attribute/USS property를 검증한 뒤 `Assets/HeraGenerated/UI`에 `.uxml` + `.hera-*` `.uss` + `PanelSettings` + `UIDocument` scaffold를 만든다. `UiToolkitFixer`는 `uitk_version`/`uxml_traits`/`uxml_api`, fixes/diagnostics를 분리한다. `PanelSettings.renderMode`는 버전별로 absent/non-public/public이므로 default screen-space는 absent일 때 write 없이 유지하고, 존재하면 public·non-public 모두 반영한다. world-space는 **docs bucket이 아니라 live runtime `>=6000.2`**에서만 허용; MVVM/data binding은 v1 범위 밖; 새 도구 0개. 다시 uGUI와 섞거나 docs bucket으로 world-space를 gate하자고 제안 금지 |
| `ui_doc` HTML→UI pipeline | ✅ 완료 (Connector 0.0.21 최초, IR v2 0.0.26, capture/sample 0.0.27; CanvasScaler config + root-canvas-at-scene-root 0.0.39, CLI `html-to-uidoc` 0.0.27; official uGUI docs fixer 0.0.45; CLAUDE.md 등재는 2026-06-18) | `UiDoc` 도구 + `Core/UiDocSchema`(ui_doc/2 IR) + `Core/UiDocFixer`(Unity 버전→공식 uGUI docs bucket 자동 선택, deterministic fixes/diagnostics) + `Core/ProceduralSprite`(절차 스프라이트). 액션: `export`/`apply`(IR↔uGUI, `--mode create\|upsert`) / `gen_sprite` / `capture`(overlay 캔버스 throwaway-camera 렌더) / `sample`(레퍼런스 색 측정, CLI측). `apply` 가 IR 최상위 `canvas` 블록으로 `CanvasScaler` 를 설정하고 root `element:"canvas"` 는 씬 루트에 생성하며, 공식문서 기반 `docs_version`/`ugui_package`/`fixes`/`diagnostics` 를 응답한다. CLI `html-to-uidoc` 는 인라인 스타일 HTML → `ui_doc/2` JSON. v0.0.14 cohort 선례 따라 root.go help 미등재(README/docs/COMMANDS/UI_DOC_IR/CHANGELOG만). 컴파일 의존성 0 🔒: uGUI/TMP 타입 전부 TypeCache+리플렉션 해석 |
| CLI-native asset ops + isolated screenshots | ✅ 완료 (2026-06-29, Connector 0.0.47) | `manage_assets` 추가: `find`/`mkdir`/`copy`/`move`/`delete`를 `AssetPathGuard`로 `Assets/` containment 보장하며 compact payload로 반환. `screenshot --isolated --target\|--path\|--instance_id` 추가: 대상 GameObject clone + throwaway camera로 단일/다중 angle contact sheet 캡처. `[HeraTool]` tool-level safety와 `[HeraActionSafety]` action-level safety metadata는 `list --tool` deep schema에만 노출해 `list --compact` 토큰 비용을 유지 |
| `ui_doc catalog` / `import` (UI 에셋 목업) | ✅ 완료 (2026-06-18, Connector v0.0.38 + CLI) | 사용자 UI 에셋 폴더 → 목업. `catalog`(CLI측, Unity 무관): 폴더 재귀 스캔 → 이미지별 매니페스트(size/has_alpha/opaque_bounds/palette/`nine_slice_hint [l,b,r,t]`/name_hint, GIF=reference_only/frames). `import`(connector): 절대경로 원본 → `Assets/`(기본 `HeraImported`) 복사 + Sprite 임포트(border/ppu/filter/pivot). **분류 주체 🔒**: "어떤 UI인가" 판단은 비전 가진 에이전트가 PNG 직접 읽어서 — 도구(Go/C#)는 픽셀 못 봄, 메타+힌트만 제공. **GIF 🔒**: catalog엔 reference_only로 등장, import는 skip(Unity GIF→Sprite 없음). import `TextureImporter` 세팅은 `ProceduralSprite` 블록 복제(2번째 소비자=replicate, 3번째에 Core 추출). 다시 *도구측 자동분류(휴리스틱 단정)* 또는 *GIF import* 제안 금지 |
| 파일버스/경로 안전 리팩토링 | ✅ 완료 (2026-06-22, Connector v0.0.42 / CLI v0.0.29) | `Core/AtomicFile` 로 heartbeat/package/test 결과 JSON 을 temp-write+replace 방식으로 통일하고 Go poller 는 parse 성공 후 삭제. `Core/AssetPathGuard` 로 `ui_doc import`/`gen_sprite` 의 `Assets/` traversal escape 차단. `cmd/dispatch.go` 로 routing 분리, `--params` 명시 플래그 override 회귀 테스트와 embedded help topic 테스트 추가. 단방향 HTTP/파일버스 모델은 유지 |
| duplicate / menu list / batch atomic cohort | ✅ 완료 (2026-06-29, Connector v0.0.48) | 헤라 철학(새 도구 추가 0개 — 기존 도구 액션/플래그로 흡수, `list --compact` 토큰 불변)에 맞춰 3건 구현. **C** `manage_gameobject duplicate`: `Unsupported.DuplicateGameObjectsUsingPasteboard`(에디터 Ctrl+D = 프리팹 링크·오버라이드·자식 보존, `Object.Instantiate` 아님 🔒) + `--count`(max 100)·`--name`(인덱스 suffix)·`--parent`. Selection 복원, 단 copy/paste 버퍼는 덮어씀. **A** `menu list`: `TypeCache.GetMethodsWithAttribute<MenuItem>` 수집(validator/`CONTEXT/` 제외). 무필터 → **prefix 버킷팅**(상위 그룹+개수만, 평면 리스트 금지 — 토큰 봉인·오염 방지 🔒), `--filter` → 캡(`--limit` 300)+명시 `truncated`. **D** `batch --atomic`: `BatchOptions.Atomic` → `DispatchBatch` 를 Undo 그룹으로 감싸 실패 시 `RevertAllDownToGroup`. caveat 🔒: Undo 등록 도구(scene/GO/component)만 롤백 — `exec`/AssetDB/파일은 비트랜잭션. reflection-기반 component-set 대신 `SerializedPropertyValue` 유지(우월). 다시 *gameobject select 도구*·*test 목록 전용 리스팅*·*평면 menu 덤프* 제안 금지 |
| 공유 문서 로컬 절대경로 금지 | ✅ 완료 (2026-07-03) | 다른 사용자 환경에서도 재사용 가능한 산출물을 위해 docs/examples/generated Markdown/checked-in scripts 에 현재 PC 절대경로를 남기지 않음. Unity Hub 에디터 경로는 `%UNITY_HUB_EDITOR%` 토큰 + 기본 resolver `%ProgramFiles%\Unity\Hub\Editor` 로 문서화하고 실제 root 는 `-HubRoot` 같은 입력으로 받음 |
| Unity 컴파일러 경로 선택 | ✅ 완료 (2026-07-03) | `ExecCompileCache` 와 Hera Settings 자동 감지가 실행 중인 Editor 번들 도구를 우선 선택한다. 검증된 레이아웃: `DotNetSdkRoslyn/csc.dll` + `NetCoreRuntime/dotnet.exe`(2022.3/2023.2/6000.0/6000.3), `DotNetSdk/sdk/<version>/Roslyn/bincore/csc.dll` + `DotNetSdk/dotnet.exe`(6000.5). `asset-config.json` 의 저장 경로가 다른 Unity Editor 설치본 내부를 가리키면 stale 로 보고 무시하되, 외부 SDK override 는 유지. 테스트: `HeraAgent/Tests/ExecCompileCache` |
| `status`/`doctor` 버전 bucket reporting | ✅ 완료 (2026-07-03) | `Heartbeat` 가 `docsVersion` 과 `compiler` 요약을 instance JSON 에 기록하고, CLI `status`/`doctor --json` 이 이를 노출한다. `compiler` 는 full local path 와 함께 `unity_dotnet_sdk_roslyn`/`unity_dotnet_sdk`/`unity_netcore_runtime`/`unity_mono`/`external`/`missing` kind 를 제공한다. 공유 문서에는 로컬 절대경로 대신 kind 와 `%UNITY_HUB_EDITOR%` 토큰만 기록 |
| `ui_doc` uGUI 버전별 diagnostics profile | ✅ 완료 (2026-07-03) | `UiDocFixer.ProfileForDocsVersion` 이 docs bucket 별 uGUI manual package 를 고정한다: `2022.3 -> com.unity.ugui@1.0`, `2023.2`/`6000.0`/`6000.3 -> com.unity.ugui@2.0`, `6000.5 -> com.unity.ugui@2.5`. 테스트: `HeraAgent/Tests/UiDocFixer`; runtime `ui_doc apply` on `6000.0.35f1` 에서 `docs_version=6000.0`, `ugui_package=com.unity.ugui@2.0` 확인 |
| `EntityIdCompat` 6000.5 rename gate | ✅ 완료 (2026-07-03) | `Object.GetInstanceID()` / `EditorUtility.InstanceIDToObject(int)` 직접 사용은 `EntityIdCompat` 내부로 격리한다. 외부 도구는 int `instance_id` 계약을 유지하면서 `EntityIdCompat.IdOf/ToObject` 만 사용. `manage_gameobject duplicate` 의 source/clone 비교도 shim 으로 교체. 테스트: `HeraAgent/Tests/EntityIdCompat`; runtime duplicate probe on `6000.0.35f1` 통과 |
| reliability/efficiency pass + `manage_assets create` (Connector 0.0.58 / CLI 0.0.39) | ✅ 완료 (2026-07-07) | 3-갈래 분석 후 최적화 배치, 전부 live 에디터(6000.3.5f2) 스모크 검증. **C#**: Heartbeat 불변 필드(pid/projectPath/unityVersion/docsVersion/compiler) 도메인당 1회 계산 — 매 1.0s 틱마다 `Process` 할당 + ~4 File.Exists stat 제거(v0.0.13 은 *interval* 만 완화, 필드 캐싱은 별개) · `manage_packages list` 폴링을 `Task.Delay`→`NextEditorUpdate`(continuation 을 메인스레드에 유지 — UPM `Request`/`PackageCollection` 은 메인스레드 read 필요; 헬퍼는 InputQaEventSystem 이어 2번째 소비자라 **local 유지**, 3번째에 Core 추출) · `HttpServer` 응답 직렬화+쓰기를 try/catch/finally 로 감싸 `response.Close()` 보장(비직렬화 그래프/클라 조기 종료 시 CLI 무한대기 방지, best-effort 500) · `ForceEditorUpdate` 는 `!InternalEditorUtility.isApplicationActive` 일 때만 RepaintAllViews(포커스 시 per-command churn 제거, 백그라운드=CLI 상용 경로는 그대로) · `Response.data` 에 `NullValueHandling.Ignore` · InputQaResolver deprecated `FindObjectsOfType`/`FindObjectOfType` → `FindObjectsByType(FindObjectsSortMode.None)`/`FindFirstObjectByType`(2022.3+ 지원, CS0618 경고 + 동일-내용 dead `#elif` 제거) · `ToolMetadataRegistry` 캐시 히트 추가(`GetToolSchema` 가 도구당 리플렉션 2회→1회) · UnityDocsStore/GameFeelStore `EnsureLoaded` early-out(조회당 `PackageInfo.FindForAssembly`+FileInfo stat 제거 — 번들은 도메인 수명 불변). **Go(CLI)**: `update.go` GitHub 호출(release-check + download)에 타임아웃 클라이언트(release-check 가 human 명령 후 동기 실행돼 stall 시 무한대기 위험 차단) · 죽은 `UNITY_AGENT_ENABLED_ASSETS` env write 제거 · `reload_retry` 가 package-level delegator 대신 리시버 호출 · `internal/poll` min/max 빌트인 · `build-unity-docs` `filepath.WalkDir` + dead `io.Discard` 제거 · `benchmark.yml` 을 `go-version-file: go.mod` + `upload-artifact@v7` 로 정렬. **신규 기능**: `manage_assets create`(ScriptableObject `.asset` 저작, TypeCache 타입 해석 + `SerializedPropertyValue.Apply` 재사용한 optional 필드 set). 전부 다시 제기 금지 |
| `manage_animation` (Connector 0.0.59) | ✅ 완료 (2026-07-07) | 신규 [HeraTool] — 애니메이션 에셋 저작(구 capability-gap C2). 6 액션: `create_clip`/`set_curve`(AnimationClip float 커브, keyframe+optional tangent, `--params keys`) · `create_controller`/`add_parameter`/`add_state`/`add_transition`(AnimatorController base-layer state machine — typed param, motion+default state, condition transition). `set_curve` 컴포넌트 타입은 `ComponentTypeResolver` 재사용, 경로는 `AssetPathGuard` 로 `Assets/` containment. **asmdef 🔒**: 애니메이션 타입은 빌트인 엔진 모듈(`UnityEngine.AnimationModule`)+`UnityEditor` 라 `references:[]`/`overrideReferences:false`/`noEngineReferences:false` asmdef 에서 자동 참조 — ugui 처럼 TypeCache 우회 **불필요**(패키지가 아니라 상시 존재하는 모듈). **네임스페이스 함정**: `AnimatorControllerParameterType` 는 `UnityEngine`, `AnimatorConditionMode`/`AnimatorController`/`AnimatorState*` 는 `UnityEditor.Animations`, `AnimationUtility` 는 `UnityEditor`. live 에디터(6000.3.5f2) 6 액션 happy-path + 크로스콜 영속성 + 4 에러 경로 스모크 검증. 다시 제기 금지 |
| Unity De-slop Mode (Beta) (`ui_slop`, Connector 0.0.62) | ✅ 완료 (2026-07-20) | **정적 시각 슬롭 정리** — Game Feel Mode(움직임·감각)의 상보. 헤라 라이브 실측 + 버전별 에디터 바이너리 리플렉션 위에 설계한 Unity 전용 택소노미 🔒. **지식 번들 🔒**: `Tools/UiSlop`(`ui_slop`) + `Core/UiSlopStore` 가 `Data/ui_slop_1.0.jsonl.gz.bytes`(49 tells, A~E) 를 GameFeelStore 패턴으로 로드(EnsureLoaded early-out·full-scan Levenshtein·area-grouped BuildIndex). source of truth 는 `tools/build-ui-slop-docs/ui_slop.jsonl`(편집 후 `go run ./tools/build-ui-slop-docs`). **check = 평가 함수 🔒**: 각 tell 은 상태 문자열이 아니라 라이브 씬에서 매번 재측정하는 술어(`check_ugui`/`check_uitk` 2벌 + `CheckFor(id, ui_system)` 슬라이스). 값은 도출(고정 px 금지), 교체형 tell 만 `borrow`(Tailwind spacing/type·Radix palette·WCAG 4.5:1), 삭제형은 null. **게임-UI 예외 게이트 🔒**: `box-in-box` 에 "무조건 flatten" 금지 — 반복 시리즈 셀(≥3)·인터랙션 보유·독립 밀도 패널은 **기능 표면**이라 제외(게이트 없이 판정하면 인벤토리 그리드의 셀이 전수 오탐으로 잡힌다 — 라이브 검증). 게임 UI 에서 중첩 표면은 대개 기능이므로, 전수 적용형 규칙에는 기능-표면 예외를 반드시 설계한다. **UITK 버전 게이트 🔒**: `check_uitk` 는 실제 USS 어휘만 참조 — `box-shadow`/`backdrop-filter`/`picking-mode` 는 USS 에 **없음**(요소 그림자는 uGUI 소관, picking 은 C#/UXML). `filter`(블러)는 **6000.2+ 버킷에만** 존재(uitk_schema 바이너리 추출로 확정: 2022.3/2023.2/6000.0 ❌). **조합 주입 🔒**: (a) `doctor --agent-rules` 가 모드 ON 일 때 독립 섹션 주입(Ultra Hera·Game Feel 과 무관), (b) `ui_slop` 조회 도구는 토글 무관 상시, (c) `manage_components add` 가 Shadow/Outline·Image/RawImage·TMP/Text 에 1줄 tell-pointer `agent_hint` 부착(game_feel hint 와 append 로 compose — clobber 금지). 토글은 `asset-config.json` 의 `ui_slop_mode`(CLI `asset-config uislop [on\|off]`, Hera Settings 섹션); **Go json.go 의 커스텀 Marshal/Unmarshal 필드 리스트 4곳 + C# `ConfigFieldNames` 등록 필수**(누락 시 조용히 미저장 — 테스트가 잡음). 모션·카피는 각각 Game Feel·humanize 소관(중복 주입 금지). 오케스트레이션 파이프라인은 `.claude/skills/unity-deslop/`(로컬 skill, gitignore). live 검증(6000.3.5f2): 도구·list 등재·토글 round-trip·doctor 주입·agent_hint 전부 통과. 다시 *무조건 flatten*·*기능 표면 일괄 정리*·*USS 미지원 속성 기반 판정* 제안 금지 |

> **핵심 원칙**: 위 표에 있는 내용을 "새로 발견한 문제"라고 제기하지 말 것.

### Ultra Hera 상세 프로토콜

`loopEngineeringMode` 기본값은 `light`다. Hera Settings 에서는 `Ultra Hera` 항목으로 보이며 `Off` / `Light` / `Ultra` 중 하나만 선택한다. Hera가 AI 작업을 직접 실행하는 기능이 아니라, `doctor --agent-rules`가 Codex/Claude/기타 agent에게 검증 강도를 알려주는 기능이다.

**Light Mode** — 모든 Unity 코딩/에디터/인스펙터 작업에 부담 없이 적용한다.

1. 목표를 한 문장으로 확정
2. 필요한 현재 상태만 compact하게 관측
3. 코드/씬/Inspector 변경
4. compile 또는 상태 검증
5. console error 확인
6. 변경 대상만 재조회
7. 실패하면 최대 1~2회 수정 반복
8. 최종 증거를 짧게 보고

대표 명령: `hera-agent-unity status`, `hera-agent-unity console --type error --lines 20`, `hera-agent-unity editor refresh --compile`, `hera-agent-unity find_gameobjects --ids`, `hera-agent-unity manage_components get ...`, `hera-agent-unity exec --depth 1 ...`.

Light Mode의 목표는 "틀린 상태로 끝내지 않기"다. PlayMode, screenshot, 전체 테스트는 기본 강제하지 않는다.

**Ultra Mode** — 사용자가 "정확히 검증해줘", "플레이해서 확인해줘", "UI 맞춰줘", "인스펙터까지 확실히 봐줘" 같은 요청을 했을 때 쓰는 엄격 모드다.

1. 목표를 성공 기준으로 분해
2. 변경 전 상태 snapshot
3. 변경 적용
4. compile
5. console error 0건 확인
6. Inspector/GameObject/asset 상태 재조회
7. PlayMode 또는 Unity Test 실행
8. 필요하면 screenshot/ui_doc capture
9. 실패 원인 분류 후 반복
10. 최종 증거와 남은 리스크 보고

대표 명령: `hera-agent-unity editor refresh --compile`, `hera-agent-unity console --type error --lines 50`, `hera-agent-unity test --mode EditMode`, `hera-agent-unity test --mode PlayMode`, `hera-agent-unity editor play --wait`, `hera-agent-unity screenshot --view game`, `hera-agent-unity ui_doc capture --out ...`.

### Why Additional Unit Tests Are Not Added

**Go-side code is a thin passthrough layer.** All business logic lives in the C# connector. The Go CLI's job is limited to:

- Parsing CLI arguments (`root.go`) — covered by `root_test.go`
- HTTP dispatch to localhost — mocking Unity's response format is meaningless; the real value is in how C# handles the request
- File polling (`status.go`, `test.go`) — covered by `status_test.go`
- Version check caching (`version_check.go`) — covered by `version_check_test.go`
- Self-update (`update.go`) — `findAsset()` is covered; the actual download+replace logic requires a real GitHub Release

**Unity Editor is required for all meaningful tests.** Commands like `editor play`, `exec`, `console`, `profiler`, `screenshot` only work when Unity is running. Without it, tests can only verify "we sent the right HTTP payload" — which tells us nothing about whether the command actually works.

**Result:** No additional unit tests are pursued. Real validation happens via manual integration testing with Unity Editor open.

## Verification

Run all of the following before pushing:

```bash
go clean -testcache
gofmt -w .
~/go/bin/golangci-lint run ./...
~/go/bin/golangci-lint fmt --diff
go test ./...
```

### Integration Tests (requires Unity)

Integration tests are tagged with `//go:build integration` and excluded from the default test run. Run them manually when Unity Editor is open:

```bash
go test -tags integration ./...
```

CI skips these since Unity is not available.

## Checklist

### 변경 시

CLI option, command, parameter를 수정하면 관련된 모든 곳을 함께 반영한다:

- C# tool (Parameters class, HandleCommand)
- Go help text (`root.go`의 `printHelp()` overview + `printTopicHelp()` 명령별 detail)
- `README.md`, `README.ko.md`
- `docs/` (해당하는 문서)
- `CLAUDE.md` (구조·체크리스트에 영향이 있을 때)

### 버전 관리

CLI(Go)와 Connector(C#)는 독립 버전. 변경된 쪽만 올린다.

- **Connector** (`AgentConnector/package.json`): C# 코드 변경 시 버전 갱신.
- **Connector 태그** (`git tag connector-X.Y.Z`): connector 버전을 올린 커밋에 매칭 태그를 찍어 push한다 (`v*` 아님 → `release.yml` 미트리거). 사용자가 UPM git URL 뒤에 `#connector-X.Y.Z` 를 붙여 커넥터 버전을 고정할 수 있게 하는 용도. 핀 안 하면 main HEAD 추종(기존 동작).
- **CLI** (`git tag vX.Y.Z`): Go 코드 변경 시 태그 생성 + push → `release.yml` workflow가 cross-build + GitHub Release 자동 생성.
- **명명 규칙**: `hera-agent-unity version` 출력은 CLI 버전이다. Unity Package Manager의 `com.notnull92.hera-agent-unity` 버전은 UPM connector 버전이며 `AgentConnector/package.json` 과 `manage_packages list` 의 `version` 으로 확인한다.
- **검증 표현**: `packages-lock.json` 의 git `hash` 는 설치된 connector 소스 커밋을 가리킨다. 이 hash가 CLI release tag 커밋과 같아도 "UPM이 vX.Y.Z"라고 쓰지 말고, "UPM connector package version 0.0.N이 commit <sha>에서 설치됨"처럼 분리해서 기록한다.

둘 다 바뀌면 둘 다 올린다. 한쪽만 바뀌면 한쪽만.

### 작업 마무리 시

- Verification 항목 전부 실행.
- 변경한 기능은 Unity가 열려 있으면 `hera-agent-unity`로 직접 실행해서 동작 확인.
- 로컬 임시 파일(테스트용 스크립트, 디버깅 출력 등) 정리.
- 관련 없는 변경은 별도 커밋으로 분리.
- 공유 문서/예제/생성 Markdown/체크인 스크립트에는 현재 PC의 절대경로를 넣지 않는다. repo-relative path, 환경변수, 명시적 CLI flag를 우선 사용하고, Unity Hub 에디터 경로는 `%UNITY_HUB_EDITOR%` 토큰 + 기본 해석 규칙(`%ProgramFiles%\Unity\Hub\Editor`)으로 표기한다. 실제 설치 루트는 `-HubRoot` 같은 입력값으로 받는다.
- **README 반영 확인** — 매 작업이 끝날 때마다, 이번 변경을 `README.md` / `README.ko.md`(특히 "What's New" 버전 테이블·명령어 테이블)에 반영할지 사용자에게 항상 물어본다. 사용자 지시가 없어도 빠뜨리지 말 것. 커밋 누락분이 쌓이면 히스토리를 거슬러 채워야 하므로, 작업 단위마다 제때 동기화한다.

## Git

Commit all unstaged changes before finishing. Unrelated changes should be committed separately.

## 실행 규칙

`go run .`은 테스트 목적일 때만 사용. CLI 기능 실행은 반드시 설치된 바이너리 `hera-agent-unity`로.

## 릴리스 플로우

"커밋하고 올려" 지시 시 아래를 한 번에 수행:

1. Verification 전부 실행.
2. 변경된 쪽 버전 갱신 (Connector `package.json` / CLI tag).
3. 커밋 + push.
4. main CI 통과 확인 (`gh run watch --exit-status`).
5. CLI 변경 있으면 새 tag push — `release.yml`이 cross-build 5종(linux/darwin × amd64+arm64, windows amd64) + GitHub Release를 자동 생성.
6. release workflow 통과 확인 (`gh run watch --exit-status`).
7. `go clean -cache -testcache`로 빌드/테스트 캐시 전부 정리.
8. 둘 다 성공하면 `hera-agent-unity update`로 설치된 CLI 업데이트.

> Release notes는 release.yml이 compare 링크만 자동 생성한다. 의미 있는 변경 요약이 필요하면 push 후 `gh release edit <tag> --notes "..."`로 보강.

### 수동 release (fallback)

`release.yml`이 깨지거나 일회성으로 우회해야 할 때:

```bash
VERSION=vX.Y.Z
GOOS=linux   GOARCH=amd64 go build -ldflags="-s -w -X main.Version=${VERSION}" -o hera-agent-unity-linux-amd64 .
GOOS=linux   GOARCH=arm64 go build -ldflags="-s -w -X main.Version=${VERSION}" -o hera-agent-unity-linux-arm64 .
GOOS=darwin  GOARCH=amd64 go build -ldflags="-s -w -X main.Version=${VERSION}" -o hera-agent-unity-darwin-amd64 .
GOOS=darwin  GOARCH=arm64 go build -ldflags="-s -w -X main.Version=${VERSION}" -o hera-agent-unity-darwin-arm64 .
GOOS=windows GOARCH=amd64 go build -ldflags="-s -w -X main.Version=${VERSION}" -o hera-agent-unity-windows-amd64.exe .
gh release create ${VERSION} --title "${VERSION}" --notes "..." hera-agent-unity-*
```

## CI

- `push/PR → main` (`ci.yml`): build, vet, test, lint, format.
- `tag push (v*)` (`release.yml`): cross-build matrix (linux × amd64/arm64, darwin × amd64/arm64, windows × amd64 — 5 binaries) + GitHub Release with auto-generated notes.
- `benchmark.yml`: exec-50 scenario timing benchmark, manually triggered.
