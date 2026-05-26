# hera-agent-unity

CLI tool to control Unity Editor from the command line.

## Structure

```
cmd/                  # Go CLI — thin passthrough layer
  root.go             # Entry point, flag/arg parsing, default passthrough
  editor.go           # editor command (waitForReady polling)
  test.go             # test command (PlayMode result polling)
  status.go           # status, waitForAlive, heartbeat reading
  update.go           # self-update from GitHub releases
  version_check.go    # periodic update notice (12h interval)
internal/client/      # Unity HTTP client, instance discovery
AgentConnector/      # C# Unity Editor package (UPM)
  Editor/
    Core/             # Shared utilities (Response, ParamCoercion, ToolParams, StringCaseUtility)
    Tools/            # Tool implementations (auto-registered via [HeraTool] attribute)
    TestRunner/       # Test runner (RunTests, TestRunnerState)
```

## Development

### Adding a Command

1. Add a C# tool in `AgentConnector/Editor/Tools/` with `[HeraTool(Name = "command_name")]`
2. CLI command name matches the tool name — default passthrough handles dispatch
3. Positional args arrive as `args` array, flags as named params
4. Go-side code is only needed for polling/waiting logic (editor, test)

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

Integration tests are tagged with `//go:build integration` and excluded from the default test run.
Run them manually when Unity Editor is open:

```bash
go test -tags integration ./...
```

CI skips these since Unity is not available.

### Why Additional Unit Tests Are Not Added

**Go-side code is a thin passthrough layer.** All business logic lives in the C# Unity Editor connector. The Go CLI's job is limited to:
- Parsing CLI arguments (`root.go`) — already covered by `root_test.go`
- HTTP dispatch to localhost — mocking Unity's response format is meaningless; the real value is in how C# handles the request
- File polling (`status.go`, `test.go`) — already covered by `status_test.go`
- Version check caching (`version_check.go`) — already covered by `version_check_test.go`
- Self-update (`update.go`) — `findAsset()` is covered; the actual download+replace logic requires a real GitHub Release, mocking provides no value

**Unity Editor is required for all meaningful tests.** Commands like `editor play`, `exec`, `console`, `profiler`, `screenshot` only work when Unity is running. Without it, tests can only verify "we sent the right HTTP payload" — which tells us nothing about whether the command actually works.

**Result:** No additional unit tests are pursued. Coverage of passthrough/parsing logic is sufficient. Real validation happens via manual integration testing with Unity Editor open.

### Why "~800 lines of Go" Was Changed to "~800 lines of core Go"

The original claim "~800 lines of Go" appeared in README.md, README.ko.md, docs/ARCHITECTURE.md, and docs/INDEX.md. While the **core engine that talks to Unity** is indeed ~800 lines, the total Go codebase is ~3,000 lines (including tests ~4,100 lines), and C# is ~3,300 lines.

**Why the change:**
- The ~800 figure was factually accurate for the **core HTTP client + passthrough logic** but misleading for the total codebase
- External readers (e.g., GeekNews) would call it out as dishonest if they counted the actual files
- The fix: "~800 lines of **core** Go" + a footnote: "Tests, TUI, and platform adapters add ~2,200 more lines — but the engine that talks to Unity stays lean"

**Files updated:** README.md, README.ko.md, docs/ARCHITECTURE.md, docs/INDEX.md

## Checklist

### 변경 시

CLI option, command, parameter를 수정하면 관련된 모든 곳을 함께 반영한다:
- C# tool (Parameters class, HandleCommand)
- Go help text (root.go의 overview + command별 detailed help)
- README.md, README.ko.md
- docs/ (해당하는 문서)
- CLAUDE.md (구조·체크리스트에 영향이 있을 때)

### 버전 관리

CLI(Go)와 Connector(C#)는 독립 버전. 변경된 쪽만 올린다.

- **Connector** (AgentConnector/package.json): C# 코드 변경 시 버전 갱신
- **CLI** (git tag vX.Y.Z): Go 코드 변경 시 태그 생성 + push → CI가 Release 빌드

둘 다 바뀌면 둘 다 올린다. 한쪽만 바뀌면 한쪽만.

### 작업 마무리 시

- Verification 항목 전부 실행
- 변경한 기능은 Unity가 열려 있으면 `hera-agent-unity`로 직접 실행해서 동작 확인
- 로컬 임시 파일(테스트용 스크립트, 디버깅 출력 등) 정리
- 관련 없는 변경은 별도 커밋으로 분리

## Git

Commit all unstaged changes before finishing. Unrelated changes should be committed separately.

## 실행 규칙

`go run .`은 테스트 목적일 때만 사용. CLI 기능 실행은 반드시 설치된 바이너리 `hera-agent-unity`로.

## 릴리스 플로우

"커밋하고 올려" 지시 시 아래를 한 번에 수행:

1. Verification 전부 실행
2. 변경된 쪽 버전 갱신 (Connector package.json / CLI tag)
3. 커밋 + push
4. CLI 변경 있으면 새 tag push
5. CI(CI + Release) 완료 대기 (`gh run watch --exit-status`, background)
6. `go clean -cache -testcache`로 빌드/테스트 캐시 전부 정리
7. 둘 다 성공하면 `hera-agent-unity update`로 설치된 CLI 업데이트

## CI

- `push/PR → main`: build, vet, test, lint, format
- `tag push (v*)`: cross-compile (linux/darwin/windows × amd64/arm64) + GitHub Release
