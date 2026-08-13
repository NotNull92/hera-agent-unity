# hera-agent-unity

> See [`AGENTS.md`](AGENTS.md) for the canonical cross-tool agent rules file (read natively by Codex, Claude Code, Cursor, Copilot, and 30+ tools).

CLI tool to control Unity Editor from the command line.
Unified successor to `hera-agent` + `hera-agent-pro`. All features ship free under Apache-2.0.

## 협업 개발 (Claude ↔ Codex)

hera-agent-unity는 Claude(Claude Code)와 Codex가 **협업해서 개발**하는 프로젝트다. 목표는 완벽한 hera를 함께 만드는 것 — 한 에이전트가 놓친 것을 다른 에이전트가 잡는다. (이건 hera를 *사용*하는 이야기가 아니라 hera라는 도구 *자체를 개발*하는 협업 원칙이다.)

- **git 히스토리 = 공유 인계 채널.** Codex는 대화 맥락이 아니라 git 커밋 히스토리로 프로젝트 상태를 파악한다. 그래서 작업은 명확한 conventional-commit 메시지로 커밋하고, 큰 기능은 `docs/CODEX_HANDOFF_*.md` 같은 인계 문서를 남겨 다음 에이전트가 이어받게 한다.
- **교차검증.** 한쪽이 구현하면 다른 쪽이 리뷰·검증하는 것을 기본으로 한다. 자기 작업을 자기가 승인하지 않는다.
- **공통 규약 준수.** 둘 다 이 `CLAUDE.md`(설계 의도·체크리스트·"이미 처리된 항목" 표)와 `AGENTS.md`(hera 사용 규약)를 따르고, 🔒 잠긴 설계 결정을 존중한다.
- **정확성 우선.** 추측 대신 실측으로 근거를 만든다 — `hera-agent-unity`로 라이브 Unity를 확인하고, 버전별 사실은 바이너리 리플렉션에서 뽑는다.

## 설계 의도

**기존 Go CLI와 localhost HTTP Unity Connector가 실행 코어라는 결정은 유지한다** 🔒. `HttpServer`, `CommandRouter`, `ToolDiscovery`, `Heartbeat`, Unity main-thread queue, 파일버스 복구 경로를 교체하거나 Unity Connector 안에 MCP를 직접 구현하지 않는다.

**CLI + MCP adapter migration의 M0부터 M17까지 PASS다** 🔒. 기존 바이너리 안의 선택적 Go stdio MCP adapter는 같은 실행 코어 앞에 있으며 Connector를 대체하거나, 도구 정의를 분기하거나, CLI 호환성을 제거하면 안 된다. CLI와 MCP는 하나의 정규화된 tool contract registry를 공유한다. M17에서 열네 개 section 28.3 시나리오와 복구 증거를 완결하고 독립 PASS B 승인을 받았다. Profile의 도구 정의 절감 기준은 통과했지만 Typed contract 및 MCP-primary 이득 기준은 충족하지 못했으므로 Typed CLI와 기존 CLI가 production default이고, MCP는 `v0.1.0`부터 배포되지만 experimental·default-off 상태를 유지한다. 이후 기본값 변경은 완전한 새 근거와 명시적 사용자 결정 없이는 금지한다.

이유:
- 런타임 의존성 0개 — 사용자는 바이너리 하나 + UPM 패키지 하나만 설치
- Stateless — 모든 요청이 독립적이라 세션·재연결 로직 불필요
- 도메인 리로드를 파일시스템 버스(`~/.hera-agent-unity/instances/`, `status/`)로 우회
- 어떤 셸·AI 에이전트·스크립트에서도 호출 가능 (MCP 클라이언트에 묶이지 않음)

### MCP migration lock and ledger

- **Authoritative implementation specification:** `docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md`
- **Milestone evidence and rollback ledger:** `docs/MCP_MIGRATION_PROGRESS.md`
- **현재 상태:** M0부터 M17까지 PASS다. CLI `v0.1.0+`의 `mcp`는 `HERA_MCP_ENABLED=1`일 때만 stdio로 시작하며 Profile, Compact 3-tool fallback, Full-safe, 명시적 arbitrary-code permission이 필요한 Advanced, 승인/MRTR, operation ledger, Tasks와 blocking fallback, large-result resource를 지원한다. 오래된 Connector는 Compact-only로 보수적으로 저하되고 안전 feature 누락은 fail-closed다. M17은 Inventoria 증거, 하나의 marked disposable fixture에서 완결한 열네 개 integration 시나리오, 복구 증거, 독립 PASS B 승인을 보유한다. 측정 이득 기준이 MCP 승격을 정당화하지 못했으므로 Typed CLI와 기존 CLI가 production default이고 MCP는 experimental·default-off다.
- **보존 경계:** 기존 Go CLI, localhost HTTP Connector, single-selected-target model, main-thread serialization, heartbeat discovery, package/test file bus, CLI/Connector 독립 버전은 계속 잠금 상태다.

### Rule-document hierarchy

- `CLAUDE.md`: 레포 개발 헌법, 현재 설계 잠금과 협업 규칙의 hand-authored canonical source.
- `docs/DECISION_LEDGER.md`: 완료 항목, 폐기된 제안, 과거 검증 근거의 historical canonical source. 관련 변경을 제안할 때만 필요한 행을 조회한다.
- `contracts/runtime-contracts.json`: Go/C#이 공유하는 안정적인 wire 상수의 hand-authored canonical source. `internal/protocol/contracts_gen.go`와 `AgentConnector/Editor/Core/ProtocolContracts.Generated.cs`는 생성물이므로 직접 수정하지 않는다.
- `AGENTS.md`: cross-tool project rule과 배포용 Hera agent guide의 hand-authored canonical source.
- `AGENT.md`: `AGENTS.md`의 user-facing 부분에서 생성되는 distributable guide.
- `cmd/AGENT.md`: `go:embed` 제약 때문에 `cmd/` 안에 두는 `AGENT.md`의 byte-identical generated copy.
- `.cursor/rules/hera-agent-unity.mdc`, `.github/copilot-instructions.md`, `GEMINI.md`, `.agents/agents.md`, `.agents/skills/hera-agent-unity/SKILL.md`: `AGENTS.md`에서 결정론적으로 생성되는 tool-specific derivative 또는 stub.
- 생성 파일은 독립적으로 수정하지 않는다. `go run ./tools/sync-agent-guides`로 재생성하고 `--check`로 drift를 검사한다.

### Architecture refinement lock

- **Roadmap and evidence:** `docs/ARCHITECTURE_REFINEMENT_ROADMAP.md`; catalog payload baseline: `docs/metrics/catalog-payload-baseline.json`.
- **Execution protocol:** current single-command metadata is `hera.execution/1`. Missing version remains a legacy-compatible request; an unknown non-empty version fails before catalog validation, approval, ledger, or handler execution. Batch remains on its existing contract until a separate versioned-batch requirement is proven.
- **MCP catalog lifecycle:** `ready`, `refreshing`, and `restart_required` are distinct states. A Tasks capability transition requires MCP process restart and must not be cleared by an ordinary catalog refresh.
- **Compact discovery:** `tool_describe(name)` preserves the full legacy result; `tool_describe(name, action)` returns only the selected canonical action contract. Full MCP remains opt-in.
- **Legacy CLI boundary:** dynamic custom-tool passthrough and legacy `exec` input adaptation live in `cmd/legacy_tool.go`; do not spread legacy coercion into strict `call` or MCP paths.
- **Measurement gate:** keep-alive, observer cadence, and event-driven catalog invalidation remain unchanged until latency, domain-reload, and Mono idle-channel regression evidence exists.

**파생 원칙** — decoupled/비대칭이 *의도된* 곳에 결합·통일 제안 금지:

- **CLI ↔ Connector 버전 핸드셰이크 불필요**: 두 버전이 일치한다는 전제 자체가 없음. HTTP+JSON forward-compat과 동적 dispatch가 자연 처리. "버전 매칭 검사 추가하자"는 제안은 모델 밖.
- **버전 용어 분리 필수**: CLI release tag(`hera-agent-unity vX.Y.Z`, git tag)는 Go 바이너리 버전이다. Unity UPM connector 버전은 `AgentConnector/package.json` 의 `version` 값(`0.0.N`)이다. 둘은 독립 버전이므로 같은 숫자라고 말하거나, UPM 패키지 버전을 CLI tag처럼 `vX.Y.Z`로 부르지 않는다. Git lock hash/commit 은 "어느 커밋의 connector를 받았는지"만 증명하며 UPM package version 자체가 아니다.
- **양방향/스트리밍 채널 없음**: 단발성 호출이 디폴트. "lock 점유자 보여달라", "진행률 스트림", "실시간 알림" 같은 제안은 모델 밖.
- **다중 에디터 발견 + 단일 선택 모델** 🔒: 한 머신의 여러 Unity heartbeat를 발견할 수 있지만 각 CLI 명령과 MCP 프로세스는 한 에디터만 선택한다. 프로젝트의 정규화된 절대경로가 안정적인 정체성이고 `port`는 `8090`–`8099`에서 재시작/domain reload 때 바뀔 수 있는 임시 연결 정보다. `--project`는 exact match 우선, legacy substring은 유일할 때만 허용하며 모호하면 실패한다. `--project`와 `--port`를 함께 주면 같은 heartbeat를 가리켜야 한다. 전송 실패/timeout 때는 fresh heartbeat로 port 재사용·PID 변경·target 소실을 다시 확인하고, idempotent 또는 operation-ledger-backed 작업만 재시도한다. 여러 에디터 broadcast/fan-out은 모델 밖이다.
- **출력 비대칭은 명령별로 분리** — 세 부류:
  - **표준 envelope tool 명령** (`call`, `exec`, `editor`, `console`, `scene`, `menu`, `screenshot`, `reserialize`, `test`, `profiler`, `list`, `describe_type`, `find_method`, `list_assemblies`, `batch`, `log`, `manage_gameobject`, `find_gameobjects`, `manage_components`, `manage_packages`, `unity_docs`, `describe_shader`, `manage_material`, `manage_prefab`, `manage_asset_import`, `manage_assets`, `manage_ui`, `manage_animation`, `manage_settings`, `bake`, `build`, `input`, `task`, `game_feel`, `ui_slop`, custom tools): 성공/실패 응답은 **compact JSON** 으로 통일 — AI agent 가 소비. 박스 drawing / ANSI escape / 한국어 banner 금지. `humanCategories` 화이트리스트(`cmd/root.go`)에 없으면 자동으로 compact + stderr 장식 억제.
  - **human 명령** (`install`, `uninstall`, `status`, `update`, `doctor`, `help`, `version` + 별칭): `humanCategories` 화이트리스트 등재. `tui.ErrorPanel` / `BoxAccent` / banner / `printUpdateNotice` 유지.
  - **자체 출력 경로 명령** (`asset-config`, `ping`): `printResponse` 를 거치지 않고 직접 출력. `asset-config` 는 기본 styled + `--json` 로 AI 모드. `ping` 은 단일 라인 `port=N alive=N state=... age_ms=N`. `doctor` 도 human 카테고리지만 `--json` / `--agent-rules` 분기 별도.
  - "tool 에러도 인간이 읽는다"는 가정은 audience reality와 어긋남 (실제로 tool 호출 = AI). 새 명령 추가 시 `humanCategories` 등재 여부가 출력 모드를 결정한다.

- **외부에서 참고한 것은 완전한 헤라화 후에만 올린다** 🔒: 외부 도구·논문·아티클을 연구해 헤라 기능을 설계하는 건 권장. 단 **레포에 남는 결과물은 이식본이 아니라 처음부터 Unity 전용으로 만들어진 장비처럼 읽혀야** 한다.
  - **기원 서사 0**: 도구 `Description`, agent-rules 문자열, 코드 주석, `CLAUDE.md`, `CHANGELOG.md`, **커밋 메시지** 어디에도 출처를 남기지 않는다. *무엇을 하는 기능인지*만 쓰고 어디서 왔는지는 쓰지 않는다.
  - **외부 기준점 금지**: "ported/이식/편입" 같은 동사, 그리고 원 도메인과의 대조("웹과 달리", "CSS로 치면")를 쓰지 않는다. Unity 사실로 직접 서술한다 — **원 도메인을 언급해야만 설명되는 규칙은 아직 헤라화가 안 된 것**이다.
  - **번역이 아니라 재도출**: 술어·임계값·어휘는 실제 Unity API(`manage_components` 프로퍼티, 버전 버킷별 `unity_docs` 항목)에 대고 새로 저술하고 라이브 검증한다. 변환물이 아니라 Unity 에서 옳은 것이 되게.
  - **외부 코드·원문 무반입**: 아이디어·방법론은 배워도 되지만 소스 파일·데이터 파일·문장 복사는 반입 금지. 헤라 배포물에 제3자 라이선스가 붙지 않게 유지한다.
  - **작명은 헤라 어휘로**(`ui_slop`, `game_feel`, `unity_docs`) — 외부 도구 이름을 따지 않는다.
- **번들 지식과 도구 표면은 영어** 🔒: `Data/*.jsonl.gz.bytes` 의 소스(`tools/build-*-docs/*.jsonl`)와 에이전트가 보는 모든 문자열(`[HeraTool]` Description·`agent_hint`·`doctor --agent-rules` 섹션·응답 필드)은 **영어로 쓴다**. 소비자가 다국어 AI 에이전트이고 `game_feel`·`unity_docs` 번들이 이미 영어라 혼용은 표면만 갈라놓는다. **주제가 한국어인 tell 도 예외가 아니다** — `hangul-font-fallback-jump` 처럼 한글 조판을 다루더라도 설명문은 영어로 쓰고, 한국어는 이 `CLAUDE.md` 같은 레포 자체 문서에만 둔다. (asset-config 한/영 혼용을 영어로 통일한 선례와 같은 결정.)

Unity 실행 기능을 추가할 때도 기존 코어 모델 안에서 풀 것: HTTP 한 번 / 필요하면 파일 폴링. 선택적 Go adapter는 이 코어 앞에서만 동작한다.

## Structure

```
cmd/                  # Go CLI — thin passthrough layer
  root.go             # Entry point, flag/arg parsing, humanCategories, response printing
  dispatch.go         # Standalone vs Unity-backed command routing
  mcp.go              # Env-gated stdio MCP entry point and Profile selection
  call.go             # Strict typed-tool validation/explain/dispatch
  call_input.go       # JSON/file/stdin source parsing and conflict checks
  config.go           # Immutable per-execution global CLI configuration
  editor.go           # Connector-backed play/stop/pause/refresh commands
  editor_bootstrap.go # pre-discovery exact-project launch/restart + heartbeat wait
  editor_install.go   # project Unity version + Hub Editor resolution
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
  build.go            # build start --wait: file-bus poll with a 15-minute floor,
                      # because BuildPipeline.BuildPlayer blocks the main thread
  task.go             # durable test/package job inspection without contacting Unity
  send.go             # command send path shared by dispatch and call
  discovery.go        # instance selection from heartbeats (--project / --port)
  help.go             # go:embed help/*.txt topic printer
  call_approval.go    # approval-token preflight and --approve replay
  call_safety.go      # risk-class projection for call
  call_tty.go         # interactive confirmation when stdin is a TTY
  legacy_approval.go  # approval handling on the legacy passthrough path
  doctor_agent_rules.go # --agent-rules sections (Ultra Hera loop text lives here)
  editor_process.go   # PID liveness + stop helpers (+ _unix/_windows variants)
  unity_docs.go       # thin passthrough — connector ships its own data set
  install.go          # self-install onto PATH + legacy scrub
  uninstall.go        # self-uninstall (+ uninstall_{unix,windows}.go variants)
  help/*.txt          # per-command help topics, embedded via go:embed
  doctor.go           # self-diagnostic; embeds AGENT.md for --agent-rules
  paths.go            # install path resolution (+ paths_windows.go)
  path_check.go       # per-command PATH-mismatch warning (HERA_AGENT_NO_PATH_CHECK)
  deferred_delete_*.go # Windows-safe .bak cleanup after self-update
  AGENT.md            # embedded copy for `doctor --agent-rules` (go:embed)
internal/client/      # Unity HTTP client, instance discovery, SendBatch
                      # + process_{unix,windows}.go (PID liveness check)
internal/schema/      # Bounded Draft 2020-12 compiled-schema cache + validation
internal/toolregistry/ # Native/legacy catalog providers, profiles, memory/disk cache
internal/policy/      # Typed policy projection skeleton (M6: descriptive only)
internal/mcpserver/   # Official Go SDK server identity/discovery + stdio lifecycle
                      # Profile/Full native registration + Compact search/describe/call
internal/assetconfig/ # Asset plugin configuration persistence
                      # (assets + game_feel_mode + game_feel_ui_mode + loopEngineeringMode)
internal/tui/         # Terminal UI helpers: style.go, assetconfig.go (bubbletea), detect.go
internal/paths/       # Single source of truth for ~/.hera-agent-unity/** file-bus paths
internal/resultstore/ # Durable async job results behind `task`
internal/taskbridge/  # MCP Tasks bridge over the same durable results
internal/unitystate/  # Heartbeat parsing and editor-state predicates
internal/telemetry/   # Opt-in local counters
internal/logutil/     # Shared log helpers
tools/build-unity-docs/ # One-shot maintainer Go script: Documentation/en/ScriptReference
                        # → unity_docs_<ver>.jsonl(.gz)(.bytes). Run per Unity version.
tools/build-game-feel-docs/ # game_feel.jsonl (checked-in source of truth, curated from
                            # Game Feel & Juice Bible + Ethical Engagement Framework)
                            # → validate + gzip → Data/game_feel_1.0.jsonl.gz.bytes.
tools/build-ui-slop-docs/ # ui_slop.jsonl (checked-in source of truth: Unity UI-slop
                          # taxonomy, 48 uGUI tells across areas A–E)
                          # → validate + gzip → Data/ui_slop_1.0.jsonl.gz.bytes.
tools/validate-tool-catalog/ # Maintainer validator for catalog files/stdin and strict schemas
AgentConnector/       # C# Unity Editor package (UPM) — package.json holds version
  Editor/
    HttpServer.cs     # [InitializeOnLoad] HttpListener + queue + main-thread pump
    CommandRouter.cs  # SemaphoreSlim lock (120s) + Dispatch / DispatchBatch
    Heartbeat.cs      # 1.0s atomic write to ~/.hera-agent-unity/instances/<md5>.json
    ToolDiscovery.cs  # [HeraTool] reflection cache + Levenshtein "did you mean"
    HeraAgent.asmdef
    HeraAgentAssetConfigWindow.cs   # Editor GUI for asset-config
    Core/             # BundleStore<TEntry> (one gzipped-JSONL bundle → keyed
                      #   dict, once per domain; Lookup/Count/LoadError/Values +
                      #   full-scan Levenshtein suggest. Owned by GameFeelStore
                      #   and UiSlopStore; UnityDocsStore excluded on purpose),
                      # Response, ParamCoercion, ToolParams,
                      # EditorUpdate (shared EditorApplication.update wait used
                      # by input QA and package polling),
                      # InputQaEventSystem (uGUI raycast/ExecuteEvents backend),
                      # InputQaInputSystem (reflection-only optional keyboard/
                      # mouse state backend; held-control cleanup),
                      # InputQaSequence + InputQaSequencePlan (strict bounded
                      # PlayMode keyboard/mouse plans; aggregate budgets and cleanup),
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
                      # GameFeelStore (BundleStore<Entry> over game_feel bundle
                      # + category-grouped BuildIndex, ethics first; corpus 54),
                      # UiSlopStore (BundleStore<Entry> over ui_slop bundle
                      # + area-grouped BuildIndex + CheckFor;
                      # corpus 48),
                      # Levenshtein (shared edit-distance helper),
                      # HeraSettings (reads shared asset-config.json at dispatch
                      # time — GameFeelMode + GameFeelUiMode + UiSlopMode
                      # + DotweenPreferred, mtime-cached),
                      # UIJuiceGuide (per-UI-element juice recipes from Juice
                      # Bible + UI Feedback Guide + UIUX Theory + Ethical
                      # Framework; uGUI DOTween-aware,
                      # pointer — manage_ui agent_hint source),
                      # TargetResolver (GameObject/component lookup from
                      # ToolParams — instance_id > path; shared target helper),
                      # EntityIdCompat (instanceID→EntityId rename shim,
                      # Unity 6000.5 gate — int instance_id contract preserved),
                      # SchemaUtility (C#→JSON-Schema type map for
                      # ToolDiscovery/ToolMetadata),
                      # safety metadata flags on [HeraTool] and per-action
                      # [HeraActionSafety] (shown only by list --tool, not
                      # list --compact),
    Tools/            # Tool implementations (auto-registered via [HeraTool]).
                      # 33 [HeraTool] classes (32 here + RunTests in TestRunner/).
                      # Name= explicit unless noted
                      # (no Name= → filename snake_case). ExecCompileCache.cs is
                      # NOT a tool — internal helper for exec compile caching.
                      #   exec        ExecuteCsharp (Full Access default + opt-in
                      #               Restricted source/metadata/IL validation)
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
                      # Animation authoring v0.0.59: manage_animation ManageAnimation
                      #   (create_clip/set_curve → AnimationClip float curves;
                      #   create_controller/add_parameter/add_state/add_transition
                      #   → AnimatorController state machine on base layer;
                      #   animation types = built-in engine module → no asmdef ref).
                      # Editor-workflow queue (all shipped):
                      #   manage_settings ManageSettings v0.0.96 (physics/time/
                      #     quality/player/audio get+set; dry_run previews are
                      #     downgraded to ReadOnly by a HeraSafetyRule, writes are
                      #     approval-gated; omitted fields stay unchanged).
                      #   bake Bake v0.0.97 (lighting / built-in scene NavMesh /
                      #     occlusion × start/status/cancel/clear; status is derived
                      #     live, so no job ledger — deprecated editor NavMesh and
                      #     giWorkflowMode APIs are suppressed in one pragma block
                      #     because no non-obsolete replacement exists).
                      #   build Build v0.0.98 (Player build + Build Settings;
                      #     BuildPipeline.BuildPlayer blocks the main thread, so the
                      #     report goes over the file bus like test — see cmd/build.go).
                      # Later action additions on existing tools: run_tests list/cancel
                      #   and --category/--assembly (v0.0.99), manage_prefab
                      #   list_overrides/apply/revert/unpack + --child (v0.0.100),
                      #   manage_assets deps forward|reverse (v0.0.101).
    Data/             # Bundled data (UPM-shipped, immutable). Versioned
                      # unity_docs_<bucket>.jsonl.gz.bytes bundles for
                      # 6000.0 / 6000.3 / 6000.5, regenerated by
                      # tools/build-unity-docs; game_feel_1.0.jsonl.gz.bytes
                      # (Game Feel Mode knowledge base, ~30 KiB) regenerated by
                      # tools/build-game-feel-docs; ui_slop_1.0.jsonl.gz.bytes
                      # (Unity De-slop Mode taxonomy, 48 uGUI tells, ~10 KiB) regenerated
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

### Feature admission gate

외부 실행 표면을 늘리는 변경은 "코드가 동작한다"만으로 입장시키지 않는다. 새 top-level tool, 새 action, profile 노출 변경, agent-rules 상시 주입 확대는 다음 증거를 같은 변경에 남긴다.

1. **Failure prevented** — 실제로 발생했거나 재현 가능한 사용자 실패, 누락된 workflow, 또는 안전성 구멍을 한 문장으로 명시한다.
2. **Existing surface reuse** — 기존 tool의 action/flag, 기존 projection, `exec`, 또는 on-demand skill로 해결할 수 없는 이유를 기록한다. 기존 action으로 자연스럽게 흡수할 수 있으면 새 top-level tool을 만들지 않는다.
3. **Contract and safety** — strict input/output schema, action safety, approval·ledger 영향, Unity 버전 경계를 함께 수정한다.
4. **Regression evidence** — 실패 재현 테스트와 수정 후 회귀 테스트를 추가하고, Unity 동작이면 disposable Editor에서 live evidence를 남긴다.
5. **Surface cost** — tool/action 수, profile payload, compact agent-rules baseline, 신규 의존성과 배포 크기 변화를 기록한다.
6. **Reviewed baseline** — live built-in catalog가 바뀌면 `docs/metrics/catalog-payload-baseline.json`을 의도적으로 재생성하고 같은 리뷰에서 승인한다. baseline 변경 없이 계약을 조용히 키우지 않는다.

카탈로그 비교 명령:

```powershell
go run . --project $env:HERA_UNITY_PROJECT list --catalog `
  --schema_version hera.tool-catalog/1 > catalog.json

go run ./tools/catalog-payload-report `
  --catalog catalog.json `
  --compare docs/metrics/catalog-payload-baseline.json `
  --fail-on-change
```

`--fail-on-change`의 `review_required` 결과는 기능이 금지됐다는 뜻이 아니라, surface 변경과 baseline 갱신을 명시적으로 리뷰해야 한다는 뜻이다. 직접 빌드한 비교기는 이때 exit code `3`을 사용하고, `go run`은 이를 자체 non-zero 상태와 `exit status 3` 메시지로 감싼다. 내부 리팩터링은 기존 tool/action 목록, response envelope, compact 규칙 baseline이 그대로임을 테스트로 증명하면 충분하다.

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

### Historical decision ledger

The completed investigation and refactor history moved to [`docs/DECISION_LEDGER.md`](docs/DECISION_LEDGER.md). Do not load that full ledger for ordinary work. Search or read the relevant row only when a proposed change intersects an old decision, rejected refactor, compatibility constraint, or release precedent.

The rule remains unchanged: a ledger item must not be presented as a newly discovered issue unless current code or new evidence invalidates the recorded decision.
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
8. 필요하면 screenshot --overlay
9. 실패 원인 분류 후 반복
10. 최종 증거와 남은 리스크 보고

대표 명령: `hera-agent-unity editor refresh --compile`, `hera-agent-unity console --type error --lines 50`, `hera-agent-unity test --mode EditMode`, `hera-agent-unity test --mode PlayMode`, `hera-agent-unity editor play --wait`, `hera-agent-unity screenshot --view game`, `hera-agent-unity screenshot --overlay --output_path ...`.

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

### Connector UPM 전체 호환 버전 릴리스 게이트

Connector 코드, asmdef, 패키지 의존성, 테스트 소스 또는 `package.json`이
바뀌면 한 Unity 버전에서의 성공만으로 검증 완료라고 기록하지 않는다. 현재
지원 범위(`AgentConnector/package.json`의 최소 `6000.0`, README의
Unity 6+)를 컴파일러/API 경계별로 나눈 다음 **세 버킷 전부** 확인한다:

1. `6000.0`–`6000.2`
2. `6000.3`–`6000.4`
3. `6000.5+`

각 버킷에서 사용할 실제 대표 에디터 버전의 source of truth는
`docs/UNITY_EDITOR_VERSION_INVENTORY.md`다. 인벤토리의 대표 버전을 최신화해야
하면 먼저 갱신하고, 설치되지 않았거나 실행할 수 없는 버킷은 **PASS가 아니라
BLOCKED**로 기록한다. 특정 버킷 하나(예: `6000.5`)만 확인하고 전체 호환 검증을
통과했다고 표현하지 않는다.

각 버킷은 새 disposable blank project 또는 `Library`를 초기화한 disposable
project에서 동일 Connector 후보를 일반 UPM dependency로 설치하여 다음을 모두
확인한다:

- bounded timeout 안에 최초 script compilation이 끝나고 Editor가 `ready`가 된다.
- `console --type error`의 compiler/package 오류가 0건이다.
- 일반 설치의 `HeraAgent.Editor` compiler response file에
  `AgentConnector/Editor/Tests/` 소스가 0개이고 `HeraAgent.Editor.Tests` assembly가
  만들어지지 않는다.
- 별도의 test-enabled pass에서는 `testables`를 켰을 때
  `HeraAgent.Editor.Tests`가 독립적으로 컴파일된다. 일반 설치 manifest에는
  `testables`를 남기지 않는다.
- timeout, `compiling` 고착, Roslyn/csc 비정상 장기 실행은 재시도로 덮지 않고
  release-blocking failure로 기록한다.

이 게이트는 Hera 저장소 개발자용이다. 아래 내용을 `doctor --agent-rules`,
`AGENT.md`, `cmd/AGENT.md`, `examples/rules/`, UPM 사용 가이드 또는 기타
downstream 사용자 규칙에 복사하지 않는다.

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
7. `npm-publish.yml`과 그 뒤를 잇는 `mcp-publish.yml` 통과 확인. 두 워크플로는 릴리스 태그에서 버전을 유도해 `npm/package.json`·`npm/package-lock.json`·`server.json`에 써 넣으므로 손으로 bump할 필요가 없다. 실패하면 npm과 MCP Registry만 조용히 뒤처지고 GitHub Release는 정상으로 보이므로 반드시 확인한다.
8. `go clean -cache -testcache`로 빌드/테스트 캐시 전부 정리.
9. 전부 성공하면 `hera-agent-unity update`로 설치된 CLI 업데이트.

> Release notes는 release.yml이 compare 링크만 자동 생성한다. 의미 있는 변경 요약이 필요하면 push 후 `gh release edit <tag> --notes "..."`로 보강.

> 배포 채널은 셋이다: GitHub Release(태그 push), npm(`npm-publish.yml`, Release 성공에 반응), MCP Registry(`mcp-publish.yml`, npm 성공에 반응). 사슬이라 앞이 실패하면 뒤는 skipped 된다 — `v0.2.1`~`v0.2.11` 구간에서 실제로 그렇게 11회 연속 누락됐다.
>
> **사슬에서 태그를 보는 건 첫 hop 뿐이다.** `workflow_run`으로 시작된 실행은 자신의 `head_branch`를 기본 브랜치(`main`)로 보고하므로, 두 번째 hop이 이전 실행의 `head_branch`를 태그로 믿으면 `main`을 받는다. commit SHA는 정확하니 `git tag --points-at`으로 태그를 역인출한다. 또한 각 publish job은 `npm/package.json`·`npm/package-lock.json`·`server.json`을 **모두** 태그 버전으로 핀해야 한다 — 패키지 테스트가 이 값들의 일치를 검사한다.

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
