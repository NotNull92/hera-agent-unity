# hera-agent-unity

CLI tool to control Unity Editor from the command line.
Unified successor to `hera-agent` + `hera-agent-pro`. All features ship free under MIT.

## 설계 의도

**CLI(단일 Go 바이너리 + localhost HTTP) 구성은 의도된 선택이다.** MCP / WebSocket relay / 영속 서버 / Python 런타임 같은 대안으로 전환하자는 제안은 하지 말 것.

이유:
- 런타임 의존성 0개 — 사용자는 바이너리 하나 + UPM 패키지 하나만 설치
- Stateless — 모든 요청이 독립적이라 세션·재연결 로직 불필요
- 도메인 리로드를 파일시스템 버스(`~/.hera-agent-unity/instances/`, `status/`)로 우회
- 어떤 셸·AI 에이전트·스크립트에서도 호출 가능 (MCP 클라이언트에 묶이지 않음)

**파생 원칙** — decoupled/비대칭이 *의도된* 곳에 결합·통일 제안 금지:

- **CLI ↔ Connector 버전 핸드셰이크 불필요**: 두 버전이 일치한다는 전제 자체가 없음. HTTP+JSON forward-compat과 동적 dispatch가 자연 처리. "버전 매칭 검사 추가하자"는 제안은 모델 밖.
- **양방향/스트리밍 채널 없음**: 단발성 호출이 디폴트. "lock 점유자 보여달라", "진행률 스트림", "실시간 알림" 같은 제안은 모델 밖.
- **출력 비대칭은 명령별로 분리** — 세 부류:
  - **표준 envelope tool 명령** (`exec`, `editor`, `console`, `scene`, `menu`, `screenshot`, `reserialize`, `test`, `profiler`, `list`, `describe_type`, `find_method`, `list_assemblies`, `batch`, `log`, custom tools): 성공/실패 응답은 **compact JSON** 으로 통일 — AI agent 가 소비. 박스 drawing / ANSI escape / 한국어 banner 금지. `humanCategories` 화이트리스트(`cmd/root.go`)에 없으면 자동으로 compact + stderr 장식 억제.
  - **human 명령** (`install`, `uninstall`, `status`, `update`, `doctor`, `help`, `version` + 별칭): `humanCategories` 화이트리스트 등재. `tui.ErrorPanel` / `BoxAccent` / banner / `printUpdateNotice` 유지.
  - **자체 출력 경로 명령** (`asset-config`, `ping`): `printResponse` 를 거치지 않고 직접 출력. `asset-config` 는 기본 styled + `--json` 로 AI 모드. `ping` 은 단일 라인 `port=N alive=N state=... age_ms=N`. `doctor` 도 human 카테고리지만 `--json` / `--agent-rules` 분기 별도.
  - "tool 에러도 인간이 읽는다"는 가정은 audience reality와 어긋남 (실제로 tool 호출 = AI). 새 명령 추가 시 `humanCategories` 등재 여부가 출력 모드를 결정한다.

새 기능을 추가할 때도 이 모델 안에서 풀 것: HTTP 한 번 / 필요하면 파일 폴링.

## Structure

```
cmd/                  # Go CLI — thin passthrough layer
  root.go             # Entry point, flag/arg parsing, humanCategories, default passthrough
  editor.go           # editor command (waitForReady polling)
  test.go             # test command (PlayMode result polling)
  status.go           # status + ping + waitForAlive/waitForReady (heartbeat reads)
  update.go           # self-update from GitHub releases (download + rename dance)
  version_check.go    # periodic update notice (12h interval, human-only)
  asset_config.go     # asset plugin config (TUI default + --json for AI)
  batch.go            # batch (multi-command) dispatch + --dry-run preview
  install.go          # self-install onto PATH + legacy scrub
  uninstall.go        # self-uninstall (+ uninstall_{unix,windows}.go variants)
  doctor.go           # self-diagnostic; embeds AGENT.md for --agent-rules
  paths.go            # install path resolution (+ paths_windows.go)
  path_check.go       # per-command PATH-mismatch warning (HERA_AGENT_NO_PATH_CHECK)
  deferred_delete_*.go # Windows-safe .bak cleanup after self-update
  AGENT.md            # embedded copy for `doctor --agent-rules` (go:embed)
internal/client/      # Unity HTTP client, instance discovery, SendBatch
                      # + process_{unix,windows}.go (PID liveness check)
internal/assetconfig/ # Asset plugin configuration persistence (config.go only)
internal/tui/         # Terminal UI helpers: style.go, assetconfig.go (bubbletea), detect.go
AgentConnector/       # C# Unity Editor package (UPM) — package.json holds version
  Editor/
    HttpServer.cs     # [InitializeOnLoad] HttpListener + queue + main-thread pump
    CommandRouter.cs  # SemaphoreSlim lock (120s) + Dispatch / DispatchBatch
    Heartbeat.cs      # 0.5s atomic write to ~/.hera-agent-unity/instances/<md5>.json
    ToolDiscovery.cs  # [HeraTool] reflection cache + Levenshtein "did you mean"
    HeraAgent.asmdef
    HeraAgentAssetConfigWindow.cs   # Editor GUI for asset-config
    Core/             # Response, ParamCoercion, ToolParams,
                      # StringCaseUtility, ToolMetadata, UnityPitfalls
    Tools/            # Tool implementations (auto-registered via [HeraTool])
    TestRunner/       # RunTests + TestRunnerState (domain-reload safe via files)
    Attributes/       # [HeraTool], [ToolParameter]
```

## Development

### Adding a Command

1. Add a C# tool in `AgentConnector/Editor/Tools/` with `[HeraTool(Name = "command_name")]`.
2. CLI command name matches the tool name — default passthrough handles dispatch.
3. Positional args arrive as the `args` array, flags as named params.
4. Go-side code is only needed for polling/waiting logic (editor, test).
5. Add help text in `cmd/root.go` `printHelp()` overview and `printTopicHelp()` detailed section.

### Adding C# files to the Connector (.meta is mandatory)

새 `.cs` 파일을 `AgentConnector/` 아래에 추가할 때는 **같은 폴더에 `<file>.cs.meta`를 함께 커밋한다.** UPM 패키지 안의 `.cs`는 immutable로 취급되어 Unity가 .meta 없는 파일을 컴파일 대상에서 제외함 — Unity 안에서 직접 만든 게 아니므로 자동 생성도 안 됨. 누락 시 사용자는 cascading "name does not exist" 컴파일 에러를 봄.

절차:
1. 기존 .meta 한 개 복사 (예: `cp ExecuteMenuItem.cs.meta NewTool.cs.meta`).
2. GUID를 새로 발급해서 덮어쓰기:
   ```bash
   od -An -N16 -tx1 /dev/urandom | tr -d ' \n'
   ```
3. `find AgentConnector -name "*.cs.meta" -exec grep -h "^guid:" {} \; | sort | uniq -d` 로 충돌 없음 확인.
4. `.cs`와 `.cs.meta` 한 커밋에 같이 넣기.

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
| ProcessItem fire-and-forget | ✅ 수정됨 | `ContinueWith(..., OnlyOnFaulted)` |
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

> **핵심 원칙**: 위 표에 있는 내용을 "새로 발견한 문제"라고 제기하지 말 것.

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
- **CLI** (`git tag vX.Y.Z`): Go 코드 변경 시 태그 생성 + push → `release.yml` workflow가 cross-build + GitHub Release 자동 생성.

둘 다 바뀌면 둘 다 올린다. 한쪽만 바뀌면 한쪽만.

### 작업 마무리 시

- Verification 항목 전부 실행.
- 변경한 기능은 Unity가 열려 있으면 `hera-agent-unity`로 직접 실행해서 동작 확인.
- 로컬 임시 파일(테스트용 스크립트, 디버깅 출력 등) 정리.
- 관련 없는 변경은 별도 커밋으로 분리.

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
