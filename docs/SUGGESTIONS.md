# 개선 제안 — hera-agent-unity-unity-unity (lite, agent-first)

> hera-agent-unity-unity-unity 의 주 사용자는 **AI 에이전트(Claude Code CLI, Codex 등)** 입니다.
> 인간이 직접 입력하는 명령은 사실상 다음 5개 뿐입니다:
>
> 1. 설치 — `install.sh` / `install.ps1`
> 2. 상태 점검 — `hera-agent-unity-unity status`
> 3. 진단 — `hera-agent-unity-unity doctor`
> 4. 업데이트 — `hera-agent-unity-unity update`
> 5. 제거 — `hera-agent-unity-unity uninstall`
>
> 나머지 명령(`exec`, `scene`, `console`, `menu`, `screenshot`, `profiler`, `test`,
> `reserialize`, `editor`, `refresh`, `list` 등)은 모두 에이전트가 호출합니다.
>
> 따라서 본 문서는 다음 4가지를 의사결정 기준으로 삼습니다:
>
> - **토큰 효율** — 매 호출이 에이전트 컨텍스트 윈도우를 갉아먹음
> - **응답 안정성** — 같은 입력은 같은 JSON 모양을 반환해야 prompt cache 유효
> - **에러 가이드성** — 에이전트가 막혔을 때 다음 액션을 추론할 수 있어야 함
> - **자기서술성(self-describing)** — `list` 만 보면 도구 사용법이 결정 가능해야 함
>
> hera-agent-unity-pro 의 차별 기능은 의도적으로 제외합니다.

---

## 0. Pro 영역 — 제안에서 제외한 항목

| 영역 | pro 에만 있는 것 |
|:-----|:-----------------|
| Reflection 헬퍼 | `describe_type`, `find_method`, `list_assemblies` 도구 |
| 다중 명령 | `batch` 명령 (JSON · stdin 으로 여러 명령 직렬 실행) |
| Exec 가속 | Roslyn `CSharpCompilation` 인프로세스 컴파일 + 메타데이터 캐시 |
| Unity 지식 베이스 | `UnityPitfalls.cs` — 에이전트용 함정 카탈로그 |
| Editor UI | `HeraAgentAssetConfigWindow.cs` — Unity 메뉴 설정 창 |
| Harness 설계 | `HARNESS_ENGINEERING_DESIGN.md` 의 SemaphoreSlim 분리, Block-at-Submit 등 |

lite 는 "Unity 를 CLI 로 조작하는 얇은 통로" 포지션 유지.

---

## 1. 개선사항 — 에이전트 UX 우선

### 1-1. `list` 응답 토큰 폭탄 [매우 높음]
- 위치: `AgentConnector/Editor/ToolDiscovery.cs:66-123`
- 현상: 도구 1개당 `parameters`(legacy 배열) + `schema`(JSON Schema) + `output_schema` + `metadata`(custom_types 등) 4중 표현. 도구 10여 개에 도구별 평균 5KB → `list` 1회로 수십 KB 응답. 에이전트가 세션 시작 때마다 호출 → 매번 컨텍스트 손해.
- 제안:
  - 기본 출력을 슬림화: `name`, `description`, `schema` 만.
  - `list --names` → 한 줄당 `name\tdescription`. 약 90% 감축.
  - `list --tool <name>` → 단일 도구만 풀스키마. 에이전트가 "내가 쓸 도구만" 정밀 조회.
  - `parameters`(legacy 배열) 제거 — `schema` 와 중복.

### 1-2. 스키마 4중 표현 [높음]
- 위치: 위와 동일.
- 현상: 같은 정보가 `parameters`, `schema`, `metadata.custom_types`, `metadata.enum_support` 등 여러 키로 흩어짐. 에이전트가 어느 필드를 읽어야 정답인지 모호.
- 제안: JSON Schema (`schema` 필드) 단일 출처로 통일. metadata 는 schema 의 `x-*` 확장 키로 흡수.

### 1-3. 에러가 plain string [매우 높음]
- 위치: `Core/Response.cs:19-31`, 거의 모든 도구.
- 현상: `ErrorResponse(message)` 만 존재. 에이전트가 분기하려면 문자열 매칭에 의존 — 메시지 한 글자 바뀌면 깨짐.
- 제안: 에러 응답에 안정 키 추가:
  ```json
  {
    "success": false,
    "message": "Compile error:\nL3: ; expected",
    "code": "EXEC_COMPILE_ERROR",
    "suggestions": ["check syntax", "run hera-agent-unity-unity list to see available tools"],
    "data": { "compile_errors": [{ "line": 3, "message": "; expected" }] }
  }
  ```
  - `code`: `EXEC_COMPILE_ERROR`, `EXEC_RUNTIME_ERROR`, `UNITY_NOT_RUNNING`, `PORT_BUSY`, `UNKNOWN_COMMAND`, `MISSING_PARAM`, `SCENE_NOT_FOUND` 등 enum.
  - `suggestions`: 짧은 다음-액션 힌트 0~3개. 에이전트가 retry 시 참고.
  - 기존 `message` 는 유지(인간이 doctor 등에서 읽을 때 자연어).

### 1-4. 잘림(truncation) continuation 없음 [높음]
- 위치: `Tools/ExecuteCsharp.cs:394` (`"... (truncated at 100)"`), `console` 의 `--lines` 제한, profiler `max` 등.
- 현상: 에이전트가 100개 잘림 응답을 받으면 "다음 100개 가져오는 법" 모름. 그냥 다시 호출하면 같은 결과만 반복.
- 제안: 잘림 발생 시 응답에 `truncation` 필드 명시:
  ```json
  "truncation": { "total": 247, "returned": 100, "next_cursor": "100" }
  ```
  - `exec` Serialize: cursor 자체는 휘발성이라 어려우면 `total` 만이라도 노출.
  - `console`: `--offset` 추가하여 페이지네이션. 또는 entry index 기준 cursor.
  - `profiler hierarchy`: `--max` 잘림 시 `dropped_count` 표시.

### 1-5. `exec` Serialize 가 property 무시 [매우 높음]
- 위치: `Tools/ExecuteCsharp.cs:401` — `BindingFlags.Public | BindingFlags.Instance` 로 `GetFields` 만 호출.
- 현상: Unity 의 거의 모든 데이터(`Transform.position`, `Scene.name`, `GameObject.activeInHierarchy` …) 가 property. exec 결과로 객체 반환 시 fields 가 없는 객체는 `ToString()` fallback → 에이전트가 받는 JSON 이 사실상 빈 객체 또는 타입명 문자열.
- 제안: `GetProperties()` 도 함께 직렬화. read-only properties 포함. exception throw 하는 property 는 catch 후 `"<error: {name}>"`.
- 부수 효과: 응답 크기 증가 가능 → depth limit 을 3 으로 조이거나 `--depth` 파라미터 추가.

### 1-6. 도구 description 에 "쓰지 말아야 할 때" 없음 [높음]
- 위치: 모든 `[HeraTool(Description = "...")]`
- 현상: 현재 description 은 "무엇을 하는가" 만 적힘. 에이전트는 "언제 쓰지 말아야 하는가" 정보가 없으면 잘못된 도구를 고름.
  - 예: `refresh` 가 play mode 에서 차단되는데 description 만으로는 안 보임.
  - 예: `reserialize` 가 매우 무거운데 description 만 보면 "그냥 호출하면 됨" 같음.
- 제안: `HeraToolAttribute` 에 `WhenNotToUse` 필드 추가 또는 description 본문에 "NOT for: ..." 한 줄 컨벤션. `list` 에서 그대로 노출.

### 1-7. 응답 모양이 호출마다 다름 (prompt cache 적대적) [높음]
- 위치: `Tools/ExecuteCsharp.cs:350-353` (Invoke 결과를 그대로 Serialize), `ManageScene.Info()` 등.
- 현상: 같은 `exec "return Camera.main;"` 도 메인 카메라 상태에 따라 결과 키 셋이 달라짐 → 에이전트 입장에서 캐시 무효.
- 제안:
  - exec 결과의 최상위 envelope 키 셋 고정: `{success, message, data, timings}`. `data` 내부는 사용자 코드 결과라 본질적으로 변동성 가짐 — 여기는 어쩔 수 없음.
  - 도구 결과(`scene info`, `profiler status`, `console` 등) 는 키 셋 명시. null/undefined 도 키는 항상 존재하도록.

### 1-8. `status` 응답 자체는 compact 옵션 추가 [중간]
- 위치: `cmd/status.go:28-45`
- 현상: `status` 는 인간용이지만 에이전트가 health probe 로도 호출. plain mode 도 4줄 출력.
- 제안: `status --compact` → `port=8090 state=ready pid=12345 project=/path` 한 줄. parse 쉬움.
  - 또는 `status --json` (envelope 동일 `{success, data: {...}}`).

### 1-9. `ToolDiscovery` 매 호출 풀스캔 [중간]
- 위치: `ToolDiscovery.cs:15-64`
- 현상: `FindHandler`/`GetToolSchemas` 가 매 호출마다 모든 어셈블리 reflection.
- 제안: `AssemblyReloadEvents.afterAssemblyReload` 단위 `Dictionary<string, MethodInfo>` 캐시. invalidation 은 reload 이벤트 1군데만.
- **lite vs pro 영역 구분**: pro 의 다층 캐싱(JSON 직렬화 캐시, 스키마별 캐시 등) 은 도입 안 함. lite 는 단순 dict 1개만.

### 1-10. dead code 정리 [중간]
- 위치: `Core/ToolMetadata.cs:236-392`, `Core/HeraToolInterfaces.cs` 전체.
- 현상: `ToolMetadataRegistry.EnableTool`/`DisableTool`/`GetToolsByGroup` 등 호출처 0건. `IHeraTool<T>`, `BaseHeraTool<T>` 도 구현체 0건. 에이전트가 schema 를 분석할 때 "이 API 의도가 뭐지?" 잘못된 추론 가능.
- 제안: 미사용 코드 제거.

### 1-11. snake_case 변환 중복 [중간]
- 위치: `Core/StringCaseUtility.cs:7` vs `Core/ToolMetadata.cs:209` (인라인 정규식)
- 현상: 후자가 약식이라 `HTTPServer` 같은 케이스에서 결과 불일치 → 에이전트가 도구 이름을 잘못 추측할 위험.
- 제안: `StringCaseUtility.ToSnakeCase` 단일 사용.

### 1-12. async / sync 도구 dispatch 분기 통일 [중간]
- 위치: `CommandRouter.cs:46-82`
- 현상: 도구 결과 `Task<object>`, `Task`, 일반 객체 3 갈래. 향후 새 도구 추가 시 패턴 혼선.
- 제안: 모든 핸들러 시그니처를 `Task<object>` 로 통일. sync 도구는 `Task.FromResult(...)` 래핑.

### 1-13. `HttpServer.ProcessItem` async void [중간]
- 위치: `HttpServer.cs:166-177`
- 제안: `async Task` 로 변경 후 `ProcessQueue()` 에서 `_ = ProcessItem(item);`. 예외 도망 경로 차단.

### 1-14. `client.Send` 매 호출 새 `http.Client` [낮음]
- 위치: `internal/client/client.go:201`
- 제안: 패키지 레벨 싱글톤. timeout 은 `http.Request.Context()` 로 per-call.
- **lite vs pro 영역 구분**: pro 설계 doc 의 HTTP 클라이언트 풀링과 의도 동일하나, lite 는 **1:1 사용 가정**이라 `MaxIdleConns 2~4` 정도 최소 설정만. 멀티에이전트용 풀 튜닝(`MaxIdleConns 10+` 등)은 pro 영역.

### 1-15. `Heartbeat.Write()` 무조건 매 0.5s 디스크 쓰기 [중간]
- 위치: `Heartbeat.cs:92-122`
- 현상: state 가 안 변해도 매 tick I/O. 에이전트 폴링과 무관한 상시 비용.
- 제안: `s_LastJson` 캐시 비교. 동일 JSON 이면 skip. 단 timestamp staleness 방지를 위해 N tick(예: 5초)마다 1회 강제 write.
- **lite vs pro 영역 구분**: pro 설계 doc(`HARNESS_ENGINEERING_DESIGN.md` Layer 2-4) 와 카테고리 동일하나, 본 항목은 trivial code quality 변경이라 lite 도 채택. pro 와 lite 둘 다 적용 가능한 보편 최적화.

### 1-16. `assetconfig` ID-not-found 가 `nil, nil` [중간]
- 위치: `internal/assetconfig/config.go:165-222`
- 제안: `return nil, fmt.Errorf("asset '%s' not found in config", id)`. 호출처 코드 단순화.

### 1-17. `DetectAssets`/`exec` silent catch [중간]
- 위치: `Tools/DetectAssets.cs:83-90`, `Tools/ExecuteCsharp.cs:270-285` 외
- 제안: `catch (Exception ex) { Debug.LogWarning(...); }` 로 한 줄 로그 추가. 에이전트는 못 보지만 사용자 디버깅 시 결정적.

### 1-18. `update` GitHub asset 무결성 검증 없음 [낮음]
- 위치: `cmd/update.go:67-74`
- 제안: Release 에 `.sha256` sidecar 업로드 + 검증. CI 측 1회 변경.

---

## 2. 잠재적 문제 — 버그 / 엣지케이스

### 2-1. `HttpServer` 포트 8091 누락 [높음]
- 위치: `HttpServer.cs:64-66, 96-98`
- 현상: 시도 순서 `8090, 8092, 8093, ..., 8100`. 8091 영구 skip.
- 제안: 의도면 주석. 아니면 `FALLBACK_PORT=8091` 로 수정.

### 2-2. `exec` `Process.Start()` null 미체크 [높음]
- 위치: `Tools/ExecuteCsharp.cs:270-285`
- 현상: AV·sandbox 가 csc 차단 시 null 반환 → 직후 `proc.StandardOutput` 접근에서 NRE → 도구 호출 단위가 아닌 에디터 에러로 번질 수 있음 → 에이전트가 unrecoverable 상태 인식 못 함.
- 제안: null 체크 후 `EXEC_LAUNCH_FAILED` 에러 반환.

### 2-3. `exec` stdout/stderr 동기 read 데드락 [높음]
- 위치: `Tools/ExecuteCsharp.cs:272-274`
- 현상: stderr 가 큰 출력(컴파일 에러 수백 개)일 때 파이프 버퍼 가득 → stdout 측 `ReadToEnd()` 영구 대기 → 30초 timeout 발동. 에이전트는 "왜 항상 30초 걸리지?" 미궁.
- 제안: `proc.OutputDataReceived` / `proc.ErrorDataReceived` + `BeginOutputReadLine`/`BeginErrorReadLine` 비동기 read.

### 2-4. `Heartbeat` compile race (에이전트 루프에 치명) [높음]
- 위치: `Heartbeat.cs:67-75`
- 현상: `MarkCompileRequested` 후 3초 grace forced "compiling". grace 만료 직후 Unity 가 그제서야 컴파일 시작하면 한 tick 동안 "ready" 노출 → CLI poller(`waitForReady`) 가 premature ready 감지 → 에이전트가 "컴파일 끝났다 가정" → exec 실패 → 디버깅 어려움.
- 제안: grace 종료 시 `EditorApplication.isCompiling` 재확인. true 면 forced "compiling" 연장.

### 2-5. `pollTestResults` Unity 크래시 감지 늦음 [중간]
- 위치: `cmd/test.go:102-106`
- 현상: `inst.State == "stopped"` 만 체크. domain reload 중 크래시면 state="reloading" 그대로 멈춤 → 10분 timeout 까지 대기 → 에이전트가 진행 못 함.
- 제안: PID liveness (`isProcessDead`) 도 N초마다 보강 체크. 죽었으면 즉시 `UNITY_CRASHED` 에러.

### 2-6. `Send` empty response 모호 [중간]
- 위치: `internal/client/client.go:218-227`
- 현상: 0 byte 응답을 일반 에러 처리. play mode entry 등 일부 명령은 의도적으로 connection 일찍 끊음 → 에이전트는 "실패" 로 받음.
- 제안: 명령별 "empty 도 정상" 화이트리스트. 또는 Unity 측에서 응답 완전 송신 후 close.

### 2-7. `exec --no-cache` ALC unload race [낮음]
- 위치: `Tools/ExecuteCsharp.cs:188-193`
- 현상: Serialize 가 primitive 로 깊은 복사하나 `Type`/`Delegate` 참조는 남을 수 있음. unload 후 stale 접근 위험.
- 제안: Serialize 가 `Type` 또는 `Delegate` 검출 시 `ToString()` 으로 강제 변환.

### 2-8. `Serialize` 깊이로만 cycle 차단 [낮음]
- 위치: `Tools/ExecuteCsharp.cs:373-414`
- 현상: depth > 4 차단이 유일한 cycle 방지. 자기참조 깊은 객체는 깊이 4까지 가서 ToString fallback. 무한루프 안 나지만 응답 폭증 가능.
- 제안: `HashSet<object>` 로 ReferenceEquals 검출. 단 ValueType 은 박싱 비용.

### 2-9. `exec` 30초 compile timeout 하드 [낮음]
- 위치: `Tools/ExecuteCsharp.cs:274`
- 제안: `Parameters.TimeoutSec` 추가. 기본 30, 최대 300 cap.

### 2-10. `waitForReady` 5분 하드 cap [낮음]
- 위치: `cmd/status.go:97`
- 현상: 큰 프로젝트 초기 컴파일은 5분 넘을 수 있음. 에이전트가 false negative 받음.
- 제안: `--compile-timeout <min>` 또는 환경변수 `HERA_AGENT_COMPILE_TIMEOUT_MIN`.

### 2-11. `console` reflection 초기화 실패 silent [중간]
- 위치: `Tools/ReadConsole.cs:47-53`
- 현상: 초기화 실패 시 모든 필드 null → 매 호출마다 8개 필드 체크 → 에러는 `"reflection error"` 단일 문자열. 어느 필드인지 모름.
- 제안: 실패한 멤버 이름을 응답 `data` 에 포함 (`code: "READCONSOLE_INIT_FAILED", data: { missing_member: "LogEntries.GetEntryInternal" }`). Unity 버전 호환성 디버깅 결정적.

### 2-12. `MD5` 사용 [낮음]
- 위치: `Heartbeat.cs:85`
- 현상: 인스턴스 파일명 hashing. 보안 무관하나 lint warning 가능.
- 제안: SHA256 + 16자 truncate.

### 2-13. `screenshot` 알파 손실 [낮음]
- 위치: `Tools/EditorScreenshot.cs:113, 118`
- 제안: `--alpha` flag 추가. true 면 RGBA32 + ARGB32 RenderTexture.

### 2-14. `asset-config --json` 위치 무관 처리 [낮음]
- 위치: `cmd/asset_config.go:18-28`
- 현상: 모든 args 에서 `--json` 검색 → 서브명령과 조합 시 의미 모호.
- 제안: 서브명령별 명시 옵션 또는 `--json` 은 무조건 list 의미로 고정.

### 2-15. `HttpServer` Origin 일괄 차단 [낮음]
- 위치: `HttpServer.cs:214-223`
- 현상: 모든 `Origin` 헤더 403. localhost CLI 는 Origin 안 보내므로 OK. 다만 에이전트가 디버깅 목적으로 헤더 부가 시 막힘.
- 제안: `Origin == null` 또는 `http://localhost*`/`http://127.*` 는 허용. 외부 origin 만 차단.

### 2-16. `update` `.bak` Unix 잔재 [낮음]
- 위치: `cmd/update.go:78-97`
- 제안: `update` 진입 시 `installDir/*.bak` sweep (`uninstall_windows.go:88-104` 패턴 차용).

---

## 3. 신규 기능 — agent-first

### 3-1. `list --names` / `list --tool <name>` [매우 높음]
- 동기: 1-1 의 token bomb 해결 동반 신규.
- 형태:
  - `list --names` → `name\tdescription` 한 줄/도구. 컨텍스트 90% 절감.
  - `list --tool exec` → 단일 도구 풀스키마. 에이전트가 "내가 호출할 도구만" 정밀 조회.
- 구현 비용: ToolDiscovery 출력 필터링 + root.go 분기. ~40 LoC.

### 3-2. 구조화 에러 envelope [매우 높음]
- 위치: 거의 모든 도구 + `Response.cs`.
- 동기: 1-3.
- 형태:
  ```json
  {
    "success": false,
    "code": "EXEC_COMPILE_ERROR",
    "message": "Compile error: ; expected at L3",
    "suggestions": ["Check syntax around line 3"],
    "data": { "errors": [{ "line": 3, "col": 14, "message": "; expected" }] }
  }
  ```
- 구현 비용: `ErrorResponse(code, message, data, suggestions)` 오버로드 추가, 각 도구에서 코드 채우기. 기존 호출처와 backward compat (code 누락 시 `"UNKNOWN"`). ~150 LoC.

### 3-3. 모든 응답에 `agent_hint` 옵션 필드 [중간]
- 동기: 에이전트가 후속 액션을 추론할 수 있게 명시적 힌트 제공.
- 형태:
  ```json
  "agent_hint": "Modified scene needs save. Call 'scene save' before exiting."
  ```
- 구현 비용: `SuccessResponse`/`ErrorResponse` 에 `agent_hint` 필드 추가. 도구별로 채우기. 부가 정보라 빈 도구 많아도 OK. ~80 LoC.
- **lite vs pro 영역 구분**:
  - `agent_hint` = 응답에 박힌 **짧은 operational 다음-액션** ("scene save 호출", "compile 후 재호출" 등).
  - pro 의 `UnityPitfalls` = 타입/메서드 기준 함정 **카탈로그**(`describe_type` 결과에 부착).
  - 둘은 보완 관계 — lite 는 의도적으로 짧고 동작 지향 힌트만. Unity API 사용법 / 함정 데이터베이스는 lite 에 도입하지 않음.

### 3-4. truncation continuation cursor [높음]
- 동기: 1-4.
- 형태:
  ```json
  "truncation": { "total": 247, "returned": 100, "next_cursor": "100" }
  ```
- 적용 대상: `exec` Serialize, `console`, `profiler hierarchy` `--max`.
- 구현 비용: 각 도구마다 cursor 의미 정의. ~100 LoC 총합.

### 3-5. `exec --file <path.cs>` [높음]
- 동기: 에이전트가 긴 코드를 `exec` 인자로 전달할 때 shell escape 지옥. stdin pipe 도 OK 지만 디버깅용으로 파일 보존 워크플로우 필요.
- 형태: 파일 본문을 code 로. 기존 stdin/문자열 인자와 양립. 우선순위: 인자 > stdin > `--file`.
- 구현 비용: root.go 의 exec 분기. ~20 LoC.

### 3-6. `log "<message>" [--level error|warning|log]` 명령 [높음]
- 동기: 에이전트가 Unity 콘솔에 진행 마커 / 디버깅 메시지 남기기. 현재는 `exec "Debug.Log(...)"` 로 가능하나 컴파일 비용 발생.
- 형태: 새 도구 `LogToConsole.cs`. `[HeraTool(Name="log")]`. compile 없이 즉시 `Debug.Log/LogWarning/LogError`.
- 구현 비용: 새 C# 도구 ~40 LoC. exec 의 컴파일 캐시 hit 보다도 빠름.

### 3-7. `ping` 명령 [중간]
- 동기: `status` 는 instance discovery + JSON 비용. 에이전트가 매 명령 전 health 체크할 때 더 가벼운 옵션.
- 형태: heartbeat 파일만 읽고 `port=8090 alive=1` 또는 `alive=0` 한 줄. exit code 0/1.
- 구현 비용: status.go 옆 새 함수. ~20 LoC.

### 3-8. `menu --list [--filter <q>]` [중간]
- 동기: Unity menu path 발견 어려움. 에이전트가 정확한 path 를 추측만으로 맞추기 힘듦. 현재는 `exec` 로 reflection 해야 함.
- 형태: 등록된 모든 menu path 덤프. 필터링.
- 구현 비용: `ExecuteMenuItem.cs` 에 `--list` 분기. Unity internal `Menu.GetMenuItems` 또는 reflection. ~60 LoC.

### 3-9. `console --since <unix_ms>` incremental fetch [중간]
- 동기: 에이전트가 폴링할 때 매번 전체 로그 받지 않게.
- 형태: timestamp 이후 entry 만 반환. 응답에 `last_timestamp` 포함.
- 구현 비용: ReadConsole.cs 에 필터링 추가. Unity LogEntry 자체에 timestamp 없으면 ReadConsole 측에서 인덱스 기반 cursor 로 대체. ~50 LoC.

### 3-10. `doctor --json` [중간]
- 동기: 에이전트가 진단을 파싱해 자가복구 시도 (`PATH 어긋남 → 사용자에게 가이드 노출`).
- 형태: 기존 plain 출력 외 JSON. `{binary: {...}, shell: {...}, unity: [...]}`
- 구현 비용: doctor.go 출력 분기. ~50 LoC.

### 3-11. `test --junit <path>` [중간]
- 동기: CI 통합. CI 안에서 동작하는 에이전트(GitHub Actions 등)도 JUnit XML 을 native parser 로 처리.
- 형태: JSON 응답과 양립. 파일 저장 후 path 반환.
- 구현 비용: Go 측 변환 함수. ~80 LoC.

### 3-12. `exec --no-return` (fire-and-forget) [중간]
- 동기: `Debug.Log` 만 출력하는 코드(상태 마킹용)는 결과 직렬화 비용 불필요.
- 형태: 컴파일 + 실행 후 `{success:true, message:"executed"}` 만. Serialize 스킵.
- 구현 비용: ExecuteCsharp 에 분기. ~15 LoC.

### 3-13. `scene new <path>` / `scene reload` [중간]
- 동기: 에이전트가 테스트 픽스처용 빈 scene 자주 생성. 또는 변경 사항 버리고 reload.
- 형태:
  - `new <path>` → `EditorSceneManager.NewScene` + save.
  - `reload [--force]` → 활성 scene 의 path 를 single 모드 재오픈.
- 구현 비용: ManageScene.cs 에 case 2개. ~50 LoC.

### 3-14. `profiler export <path>` [낮음]
- 동기: 에이전트가 캡처한 프로파일을 인간에게 전달.
- 형태: `ProfilerDriver.SaveProfile` 호출.
- 구현 비용: ManageProfiler.cs 에 action 추가. ~25 LoC.

### 3-15. 도구 schema 에 "common usage" 예시 [중간]
- 동기: 에이전트가 도구를 처음 만났을 때 가장 흔한 패턴을 즉시 학습.
- 형태: `HeraToolAttribute.Examples` 필드 추가. `list` 응답에 그대로 노출.
- 구현 비용: Attribute 필드 추가 + 도구별 예시 1~2개 작성. ~ 도구 수 × 5분.
- **lite vs pro 영역 구분**:
  - lite — **도구당 1~2개 단순 패턴**만 (기본 호출 + 1개 옵션 변형 정도).
  - pro — `ExampleDescriptions` 동반 + 4개+ 큐레이션된 예시 + 트레이드오프 설명 (`DescribeType.cs` 가 좋은 reference).
  - 동일 필드 이름을 쓰되 컨텐츠 깊이로 차별. lite 가 "기본 사용법", pro 가 "심화 가이드".

### 3-16. `version --json` [낮음]
- 동기: 에이전트가 버전 분기 (예: 0.0.6 이상에서만 동작하는 도구).
- 형태: `{cli_version: "0.0.7", connector_version: "0.0.5"}` (connector 버전은 list 응답에 이미 포함시키거나 별도 noop 도구로).
- 구현 비용: root.go ~10 LoC.

### 3-17. 환경변수 `HERA_AGENT_QUIET=1` [낮음]
- 동기: 에이전트는 `[hera-agent-unity-unity] compiling...` 같은 진행 메시지가 컨텍스트 오염. 인간이 직접 호출할 때는 유지.
- 형태: env 가 1 이면 stderr 의 진행 메시지 전부 silence. result 만 stdout.
- 구현 비용: root.go 및 출력 지점 7~8 곳. ~30 LoC.

---

## 4. 의도적으로 비포커스 (인간 영역 / 드롭)

다음은 lite 의 agent-first 정책상 의도적으로 제안하지 않는 항목입니다.

- **`status --watch`** — 에이전트는 폴링을 직접 구현. 인간이 보던 명령이라 raw tail 도 효용 낮음.
- **`console --follow` raw tail** — 위와 같음. 단 `--since` 기반 incremental fetch(3-9) 는 채택.
- **`screenshot --copy` (clipboard)** — 에이전트가 clipboard 못 씀.
- **bash/zsh/powershell `completion`** — 인간 tab 자동완성. 명령 입력 자체가 인간 영역인 `status`/`uninstall` 두 개 뿐이라 가치 낮음.
- **글로벌 `config` 명령** — 에이전트는 매 명령에 옵션 명시. 글로벌 default 가 오히려 결정성 해침.
- **`uninstall --keep-config`** — 인간 관심사. `uninstall` 자체 빈도 낮음.
- **`asset-config dump`** — 인간이 CLAUDE.md 에 붙여넣기용. 가치는 있으나 우선순위 매우 낮음.

`status`/`doctor`/`update`/`uninstall` 의 인간용 출력(`tui.StatusPanel`, `tui.BoxAccent`, doctor plain text 진단, update 진행 메시지 등)은 **유지**합니다. 네 명령이 인간 영역으로 명확히 분리됐기 때문에 정체성 충돌 없음.

`doctor` 는 인간이 1차 사용자이지만, 3-10 (`doctor --json`) 은 에이전트가 자가 진단 / 자동 복구 가이드용으로 사용 가능. 두 출력 형태 양립.

`update` 관련 개선사항(1-18 무결성 검증, 2-16 `.bak` 잔재)도 인간 영역이라는 전제로 유지 — 에이전트가 자기 자신을 update 하는 시나리오 없음.

---

## 우선순위 요약 (에이전트 영향도)

| 우선 | 영역 | 항목 |
|:----:|:----:|:-----|
| **매우 높음** | UX | 1-1 list bomb, 1-3 error structure, 1-5 Serialize property 누락, 3-1 list --names, 3-2 error envelope |
| **높음** | 버그 | 2-1 포트 8091, 2-2 Process.Start null, 2-3 파이프 데드락, 2-4 compile race |
| **높음** | UX | 1-2 schema 4중, 1-4 truncation cursor, 1-6 when-not-to-use, 1-7 응답 안정성, 3-4 cursor, 3-5 exec --file, 3-6 log |
| **중간** | UX | 1-8, 1-9, 1-10, 1-11, 1-12, 1-13, 1-15, 1-17, 3-3 agent_hint |
| **중간** | 버그 | 2-5 Unity 크래시 감지, 2-6 empty response, 2-11 console init |
| **중간** | 기능 | 3-7 ping, 3-8 menu --list, 3-9 console --since, 3-10 doctor --json, 3-11 test --junit, 3-12 exec --no-return, 3-13 scene new/reload, 3-15 examples |
| **낮음** | 버그/UX | 2-7~2-16, 1-14, 1-16, 1-18 |
| **낮음** | 기능 | 3-14 profiler export, 3-16 version --json, 3-17 QUIET env |

---

*마지막 갱신: 2026-05-18 — agent-first 관점 재작성. 인간 직접 사용 명령은 `install.{sh,ps1}`, `status`, `doctor`, `update`, `uninstall` 5개로 정의.*
