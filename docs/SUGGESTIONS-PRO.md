# SUGGESTIONS-PRO (v2 — 2026-05-19)

`hera-agent-pro` 전반 분석. 2026-05-19 lite (`C:\Users\PC\Desktop\Cowork\hera-agent`) 의 phase 1-4 작업과 diff 비교 결과 반영.

> **설계 원칙 (CLAUDE.md)** — 단발 HTTP / 파일 폴링 모델 안에서만 제안한다. MCP / WebSocket / persistent server / 양방향 스트리밍 / 버전 핸드셰이크 / 출력 통일 제안은 배제했다.

---

## Audience Reality Check (필독)

**실제 소비자 분리:**

| 명령 | 사용자 | 비고 |
|---|---|---|
| `install` | 인간 | 1회성. Branding/banner OK. |
| `status` | 인간 | 디버깅 시 직접 실행. Pretty panel OK. |
| `uninstall` | 인간 | 확인 prompt 필요. |
| `doctor` | 인간 | (lite 머지 후 신규) 진단용. styled OK. `--json` 은 AI 경로 |
| **그 외 전부** | **AI agent (Claude Code CLI / Codex)** | tool 호출 = AI. 인간은 거의 안 침. |

전부 = `editor`, `exec`, `console`, `scene`, `menu`, `screenshot`, `reserialize`, `test`, `profiler`, `list`, `describe_type`, `find_method`, `list_assemblies`, `batch`, `asset-config`, `update`, `log` (신규), `ping` (신규), custom tools.

**파생 결론:**

1. **모든 AI-target 명령의 stderr 데코는 토큰 비용**.
2. **`tui.ErrorPanel` 가 tool error 에 사용되는 건 §3.1 에서 결정됨** — tool = plain JSON.
3. **`printUpdateNotice` 매 명령 호출은 AI 컨텍스트 오염**. status/uninstall 만 활성화.
4. **신규 기능은 AI 토큰 절감 / schema 신뢰성 / 라운드트립 회피 관점 우선**. Pretty TUI 없음.

**본 문서의 모든 항목에 `Audience: AI | Human | Both` 태그 부여.**

---

## 0. Lite Parity 머지 (P0) — 2026-05-19 lite 8 커밋 반영

lite repo 가 phase 1-4 + 3 hotfix 로 대규모 갱신. Pro = lite 상위집합 원칙에 따라 머지 분류:

### 0.1 SKIP — Pro 가 이미 lite 동등/상위

작업 불필요. lite 의 변경분이 Pro 에 이미 적용되어 있거나 더 정교.

| lite 커밋 / 변경 | Pro 상태 | 검증 |
|---|---|---|
| `ProcessItem` async void → async Task (39fcb77, efe8cba) | ✅ 상위 — `async Task ProcessItemAsync` + `ContinueWith(OnlyOnFaulted)` fault catch | `AgentConnector/Editor/HttpServer.cs:181-192`. CLAUDE.md 표 참조 |
| `HeraToolInterfaces.cs` 제거 (39fcb77) | ✅ 이미 없음 | 파일 부재 확인 |
| `ToolMetadataRegistry` slim (39fcb77) | ✅ 이미 정리됨 — `StringCaseUtility.ToSnakeCase` 단일화 | CLAUDE.md 표 참조 |
| `ToolDiscovery` assembly scan 캐시 (efe8cba) | ✅ 상위 — `s_HandlerCache` + `s_CacheValid` + `s_Rebuilding` 재진입 가드 + `AssemblyReloadEvents.beforeAssemblyReload` + `ToolMetadataRegistry.ToolsRefreshed` 양쪽 hook | `ToolDiscovery.cs:17-39` |
| singleton `http.Client` + keep-alive (efe8cba) | ✅ 상위 — `MaxIdleConns: 10`, `MaxIdleConnsPerHost: 1` (lite 는 4) | `internal/client/client.go:21-23` |

### 0.2 PARTIAL — Pro base 보유, 일부만 보강

함수/메커니즘 자체는 Pro 에 있음. lite 의 보강분만 반영.

| 항목 | Pro 현재 | lite 변경 | 작업 |
|---|---|---|---|
| `client.Send` retry 길이 | `maxRetries = 3` exp backoff | 10회 × 500ms (~5s) — domain reload 윈도우 커버. `Do()` 응답 받으면 retry 안 함. heartbeat `state="stopped"` 면 즉시 중단 | reload 윈도우 커버용 패턴 채택. `internal/client/client.go:349-360` |
| `update` `.bak` sweep 호출 | `sweepArtifacts` 함수 존재 (`cmd/uninstall_windows.go:38`), uninstall 에서만 호출 | update 시작에도 sweep 호출 (96a5f72) | `cmd/update.go` 진입부에 `sweepArtifacts(installDir)` 호출 추가. **= 기존 SUGGESTIONS §2.9 와 동일** |
| Heartbeat `MarkCompileRequested` grace window | 함수 존재 (`Heartbeat.cs:88-92`), grace = 3.0초 | 30초로 확장 + `isCompiling=true` 윈도우 전체 (39fcb77) | grace 30s 로 확장. premature "ready" tick 제거 |
| Heartbeat diff-cache **revert** | Pro `Heartbeat.cs:145` 가 정확히 lite 가 revert 한 그 코드 — `s_LastStatusJson` 동일성 비교 후 write skip | timestamp drift 로 `waitForAlive` baseline 못 넘김 → unconditional 0.5s tick 복원 (f8eec3d) | **Pro 도 동일 revert. lite 가 같은 결론에 독립적으로 도달 = 검증됨.** 기존 SUGGESTIONS §2.4 해결책 = 머지 |
| `ReadConsole` init 실패 메시지 | "ReadConsole failed to initialize (reflection error)." 단순 문자열 (`ReadConsole.cs:94`) | `READCONSOLE_INIT_FAILED` 코드 + 누락 reflection 멤버 list + Unity 버전 (088d135) | 구조화 ErrorResponse 로 교체 |

### 0.3 FULL MERGE — Pro 미보유 (전체 포팅)

| 항목 | 커밋 | Audience | 비고 |
|---|---|---|---|
| `ErrorResponse` { code, suggestions } + `SuccessResponse` { agent_hint } | 6466c7d | AI | `NullValueHandling.Ignore` 하위호환. `Core/Response.cs` 전면 갱신 |
| exec 구조화 에러 코드 (`EXEC_COMPILE_ERROR`, `EXEC_RUNTIME_ERROR`, `EXEC_LAUNCH_FAILED`) + parsed compile errors + stack trace in `data` | 6466c7d | AI | `Tools/ExecuteCsharp.cs` |
| Serialize: `[Obsolete]` skip, reference-cycle guard (`HashSet<object>`), UnityObject 분기 (IEnumerable 우선순위 역전), self-typed 멤버 skip (`Vector3.normalized` 등), value-type visited 제외 | 1b9275e, a3ab3c1 | AI | depth 4 → 3. 응답 노이즈 ~90% 감축 |
| Serialize truncation envelope `{__truncated, returned, hint}` | 088d135 | AI | 문자열 sentinel 폐기 |
| **`log` 툴** (`LogToConsole.cs`) — Debug.Log/Warning/Error 직접 dispatch. exec 우회 (~1ms vs ~500ms) | 39fcb77 | AI | 신규 `[HeraTool(Name="log")]`. `.cs.meta` 동반 필수 |
| `exec --file <path>` — 디스크 코드 읽기. precedence: positional/stdin > --file | 39fcb77 | AI | CLI + Connector |
| `exec --depth N` (default 3, cap 8) — caller-scoped Serialize depth | 088d135 | AI | |
| `exec` stdout/stderr async pipe read (`BeginOutputReadLine`/`BeginErrorReadLine`) — 큰 stderr 데드락 fix | 39fcb77 | AI | `Tools/ExecuteCsharp.cs` |
| `exec` Process.Start 예외/null catch → `EXEC_LAUNCH_FAILED` + AV/sandbox hint | 6466c7d | AI | |
| HttpServer 포트 retry 순서 fix (8092 → 8091) | 6466c7d | AI | `HttpServer.cs` |
| **`ping`** 명령 — heartbeat-only liveness. HTTP/Unity dispatch 없음. `port=N alive=1 state=X age_ms=N`. exit 0/1 | 088d135 | AI | CLI-only. status 보다 가벼움 |
| **`doctor`** 명령 + `--json` — binary path / shell / Unity scan 진단 | 088d135 (+ lite pre-existing) | Human + AI (`--json`) | CLI 전체 신규 |
| `console --since <cursor>` + envelope `{entries, total_in_console, matched, returned, since, last_cursor, truncated}` | 088d135 | AI | `Tools/ReadConsole.cs` 응답 구조 변경. 기존 flat list 폐기 |
| `pollTestResults` PID liveness probe (5초마다 `client.IsProcessDead`) | efe8cba | AI | Unity crash during domain reload 즉시 감지. **기존 §2.3 waitForReady 패턴과 동일 — `cmd/editor.go` 에도 같이 적용** |
| `list --tool <name>` (단일 tool schema) + `list --names` (이름만) | 6466c7d | AI | slim list default (name/desc/schema) + 두 flag |
| CLI `CommandResponse` 가 `Code` / `Suggestions` / `AgentHint` 노출. `printResponse` 가 code prefix + hints 표시 | 6466c7d | AI | `internal/client/client.go` + `cmd/root.go` |
| `assetconfig` 함수들 explicit "asset not found" 에러 (`(nil, nil)` → 명시 에러) | efe8cba | 개발자 | `internal/assetconfig/config.go`. 호출처 동작 동일하지만 contract 정직화 |

### 0.4 Pro-전용 lite parity (lite 와 무관)

기존 §0 항목 중 lite 작업과 무관한 것 — 별도 머지.

| Lite 자산 | Pro 상태 | Lite 위치 | Audience |
|---|---|---|---|
| `doctor` 명령 (기본 형태) | ❌ 없음 | `hera-agent/cmd/doctor.go` | Human (`--json` 은 AI). §0.3 `doctor --json` 과 묶어서 동시 포팅 |
| `checkBinaryPath()` PATH 불일치 경고 + `HERA_AGENT_NO_PATH_CHECK=1` opt-out | ❌ 없음 | `hera-agent/cmd/path_check.go` | Both. AI 경로에는 silent 가드 필수 (§3.2 `isHumanCommand` 와 연결) |
| `paths_windows.go` (legacy paths, PowerShell helper) | ❌ 없음 | `hera-agent/cmd/paths_windows.go` | Human |
| `docs/TROUBLESHOOTING.md` | ❌ 없음 | `hera-agent/docs/TROUBLESHOOTING.md` | Human |

---

## 1. Pro 고유 개선 (lite 머지 후에도 남는 항목)

### 1.1 License TODO 블록 — `AgentConnector/Editor/Heartbeat.cs:11-28`

**Audience: 개발자 (코드 위생)**

영문화 완료 (2026-05-19). 다음 중 하나:
- 실제 구현 (별도 PR, §4.3 참조)
- GitHub issue 로 이동 후 코드에서 제거

### 1.2 거대 단일 파일 — `AgentConnector/Editor/HeraAgentAssetConfigWindow.cs` (2028줄)

**Audience: 개발자**

Palette / Data Model / State / UI build / Persistence 혼재. `partial class` 분할:

- `HeraAgentAssetConfigWindow.Palette.cs`
- `HeraAgentAssetConfigWindow.Model.cs`
- `HeraAgentAssetConfigWindow.UI.cs`
- `HeraAgentAssetConfigWindow.Persistence.cs`

### 1.3 `check → compile_only` 매핑 위치 — `cmd/root.go:482-486`

**Audience: AI (schema 신뢰성)**

```go
if v, ok := params["check"]; ok {
    params["compile_only"] = v
    delete(params, "check")
}
```

`buildParams()` 안에 있어 **모든 명령** 에 적용. AI가 자체 정의한 커스텀 툴이 `--check` 파라미터를 가지면 silent rename → schema 위반. `exec` 분기 (`cmd/root.go:282-288`) 안으로 이동.

### 1.4 `uninstall --yes` 부재 — `cmd/uninstall.go:18-25`

**Audience: Human (CI/스크립트도)**

`promptConfirm` 무조건 stdin 읽음. CI / 스크립트 호출 차단. `--yes` / `--force` 플래그 추가.

### 1.5 `findAsset` 매칭 brittle — `cmd/update.go:178-184`

**Audience: Both**

`strings.Contains(a.Name, "linux-amd64")` 향후 `hera-agent-pro-linux-amd64-debug` 등 추가시 매칭 충돌. 정확 suffix 매칭:

```go
suffix := fmt.Sprintf("-%s-%s", runtime.GOOS, runtime.GOARCH)
if runtime.GOOS == "windows" { suffix += ".exe" }
return strings.HasSuffix(a.Name, suffix)
```

### 1.6 Unix PATH rc 파일 커버리지 — `cmd/install.go:201-220`

**Audience: Human (install)**

`.bashrc` / `.zshrc` 만 처리. `.profile` / `config.fish` 미지원. Lite shell 스크립트 (`hera-agent/install.sh:50-58`) 는 `$SHELL` basename 분기. 같은 로직 포팅.

### 1.7 `cache.Outdated` 디스크 저장 무의미 — `cmd/version_check.go:96-102`

**Audience: Both**

`Outdated` 를 캐시에 저장하면 update 직후 stale 캐시로 잘못된 알림. `isNewerVersion(Version, cache.Latest)` 런타임 비교로 충분. 필드 제거 가능.

### 1.8 `PROGRESS.json` stale — repo root

**Audience: 개발자**

`last_updated: 2026-05-14`. 자동 갱신 워크플로 없음.
- CI에서 자동 갱신 (release.yml 에 step)
- 또는 삭제 + `.gitignore`

### 1.9 `flagPort > 0` 분기 중복 — `cmd/root.go`

**Audience: 개발자**

`status` 분기 (line 217), 메인 resolve (line 228), resolve 클로저 (line 243-248) 3곳 동일 분기. helper:

```go
func resolveInstance(project string, port int) (*client.Instance, error) {
    if port > 0 { return client.DiscoverInstance("", port) }
    return client.DiscoverInstance(project, 0)
}
```

### 1.10 `CommandRouter` timeout 분기 timing 누락 — `AgentConnector/Editor/CommandRouter.cs:40-43`

**Audience: AI (debug data)**

```csharp
if (!await s_Lock.WaitAsync(TimeSpan.FromSeconds(120)))
    return new ErrorResponse("[Hera] I waited 120s ...");
```

`ResponseTimings.Set(result, "total_ms", ...)` 우회. timeout 케이스에도 `total_ms` 기록하면 AI 가 lock contention 진단 가능.

### 1.11 `ExecCompileCache` eviction 주석 정정 — `AgentConnector/Editor/Tools/ExecCompileCache.cs:38-39`

**Audience: 개발자**

`LinkedList<string> s_AssemblyOrder` + `TryGetAssembly` 가 `AddLast` 로 touch. 실제로 LRU. 주석/네이밍 정리.

---

## 2. Pro 고유 잠재 문제 (Bugs / Edge cases)

lite 머지로 해결되지 않는 항목만.

### 2.1 Origin 헤더 광범위 차단 — `AgentConnector/Editor/HttpServer.cs:248-257`

**Audience: 개발자 (디버깅)**

```csharp
var origin = request.Headers["Origin"];
if (origin != null) { response.StatusCode = 403; ... }
```

브라우저 차단 의도이나 디버깅용 `curl -H "Origin: ..."` 도 차단. AI 는 보통 Origin 안 보내므로 영향 없으나, 의도 확인.

### 2.2 비-JSON 응답이 success 로 처리 — `internal/client/client.go:338-344`

**Audience: AI (schema 위반)**

```go
if err := json.Unmarshal(respBody, &result); err != nil {
    return &CommandResponse{
        Success: true,
        Message: string(respBody),
    }, nil
}
```

Unity 가 깨진 응답 보내도 `Success: true`. AI 는 success 로 믿고 다음 단계 진행 → cascading 실패. `Success: false` 로 변경. **lite 작업에 동일 변경 없음 — Pro 고유 잠재 버그**.

### 2.3 `waitForReady` Unity 종료 감지 못함 — `cmd/editor.go:48-55`

**Audience: AI (hang 회피)**

`refresh --compile` 분기에서 5분 timeout 까지 폴링. Unity 가 컴파일 중 죽으면 timeout 까지 hang. **lite 의 `pollTestResults` PID liveness 패턴 (§0.3) 을 `editor.go` 에도 동일 적용** — 한 PR 에 묶기.

### 2.4 batch `fail_fast` 기본값 `true` 직관 위배 — `AgentConnector/Editor/HttpServer.cs:328`

**Audience: AI**

```csharp
FailFast = optionsObj?["fail_fast"]?.Value<bool>() ?? true
```

AI batch 사용처는 대부분 best-effort. 기본값 `false` 또는 명시 요구.

### 2.5 `RequiredCscDeps` 최소 집합 — `AgentConnector/Editor/Tools/ExecuteCsharp.cs:67-70`

**Audience: AI (exec 신뢰성)**

`System.Text.Encoding.CodePages.dll` 1개만 검증. .NET SDK 버전 따라 더 필요할 수 있음 → detection 통과 후 런타임 실패. `csc --version` dry-run 으로 동적 검증.

### 2.6 `install` 같은 경로 가드 부재 — `cmd/install.go:39-43`

**Audience: Human**

사용자가 이미 install path 에서 실행 중이면 `copyFile` 가 `os.Create(dst)` 로 src 를 truncate → 자기 자신 손상. `EvalSymlinks(src) == EvalSymlinks(dst)` 체크 후 skip.

### 2.7 `Application.dataPath.Replace("/Assets", "")` 부정확 — `AgentConnector/Editor/Heartbeat.cs:120, 134`

**Audience: AI (instance discovery)**

프로젝트 경로에 `/Assets` 가 중간 디렉토리로 포함되면 (`~/MyAssets/proj`) 잘못 치환 → projectPath 왜곡 → AI 의 `--project` 매칭 실패. `Path.GetDirectoryName(Application.dataPath)` 사용. **lite Heartbeat 도 동일 패턴 — lite 측에도 별도 제안 가치**.

### 2.8 `splitArgs` 인자 over-consume — `cmd/root.go:514-528`

**Audience: AI (입력 파싱)**

```go
case "--port", "--project", "--timeout":
    flags = append(flags, args[i])
    if i+1 < len(args) {
        i++
        flags = append(flags, args[i])
    }
```

`hera-agent-pro --port --debug exec ...` 에서 `--debug` 가 port 값으로 먹힘. 다음 토큰이 `--` 시작이면 skip:

```go
if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
    i++
    flags = append(flags, args[i])
}
```

### 2.9 Asset 카탈로그 이중 관리

**Audience: 개발자**

`internal/assetconfig` (Go) 와 `AgentConnector/Editor/Tools/DetectAssets.cs` (C#) 양쪽에 asset 정의. 한쪽 추가 시 다른쪽 누락 위험. 단일 source of truth (JSON 카탈로그 → 양쪽 read) 또는 Unity-side 단방향 sync.

### 2.10 `ProcessItem` `OnlyOnFaulted` 만 catch — `AgentConnector/Editor/HttpServer.cs:181-189`

**Audience: 개발자**

`Canceled` transition 로그 안됨. domain reload 중 await 취소 등의 경로면 silent. `NotOnRanToCompletion` 으로 확장 고려.

### 2.11 `list_assemblies` 기본값이 system 제외인데 AI 는 모름 — `AgentConnector/Editor/Tools/ListAssemblies.cs:39-43`

**Audience: AI**

기본 제외 prefix (`System.`, `mscorlib`, 등) 가 응답에 포함 안됨. AI 가 "왜 `System.Linq` 가 없지?" 의문 → 다시 `--include_system true` 로 호출 → 토큰 2배. 응답 메시지에 `"filter_applied": "system_excluded"` 명시.

### 2.12 ToolMetadataRegistry 동기화 race — `AgentConnector/Editor/Core/ToolMetadata.cs:302-456`

**Audience: 개발자 (잠재 race)**

`_tools` 가 `Dictionary` 인데 `Register`/`Unregister`/`GetAllTools` 동시 호출 시 lock 없음. 일반적으로 단일 thread (메인) 에서만 호출되지만 `ToolDiscovery.RebuildCache` 가 어셈블리 리로드 중 호출 → 이론적 race. `ConcurrentDictionary` 또는 명시적 lock.

---

## 3. 토큰 경제 / AI ergonomics

> 모든 항목 **Audience: AI**.

### 3.1 ✅ DECIDED — Styled error 분리 (Option B 채택, 2026-05-18)

**결정:** tool 명령 (AI consumer) 의 성공/실패 응답은 **plain JSON 통일**. human 명령 (`install` / `status` / `uninstall` / `doctor`) 만 styled.

**근거:** audience reality — tool 호출자는 AI. styled stderr box 는 토큰만 누적. CLAUDE.md 갱신 완료.

**영향 받는 코드:**

| 위치 | 현재 | 변경 |
|---|---|---|
| `cmd/root.go:393-401` `printResponse` 실패 분기 | `tui.ErrorPanel("Failed", ...)` | tool 명령일 때 plain JSON `{"success": false, "code": "...", "message": "...", "data": ..., "suggestions": [...], "agent_hint": "..."}` (lite 의 §0.3 envelope 머지 후 형태) |
| `cmd/root.go:218-222` status 분기 | styled OK | 유지 |
| `cmd/install.go` / `cmd/uninstall.go` / 신규 `cmd/doctor.go` | styled OK | 유지 (doctor 의 `--json` 은 plain) |
| `cmd/batch.go:56-78` 진행 출력 | styled | plain JSON line-per-result |
| `cmd/asset_config.go` styled list / kv / banner | styled | plain JSON, `--json` 기본화 |
| `cmd/editor.go` / `cmd/test.go` `waitForAlive`/`waitForReady` styled stderr | styled by default | tool 명령 경로면 default off (§3.4 와 동일 변경) |

**구현 방향:**
1. `cmd/root.go` 에 `isHumanCommand(category string) bool` helper. `install` / `status` / `uninstall` / `update` / `doctor` / `--help` / `version` → true.
2. `printResponse` 가 helper 보고 분기.
3. `tui` 패키지는 install/status/uninstall/doctor 만 사용.
4. `printUpdateNotice` 도 같은 helper 활용 (§3.2 와 통합).

### 3.2 `printUpdateNotice` 매 명령 호출 — `cmd/root.go:222, 303, 326`

**제안:** `status` 와 `uninstall` 에서만 호출. `if !isHumanCommand() { return }` 가드.

### 3.3 `[hera-agent-pro] compiling...` stderr 무조건 출력 — `cmd/root.go:261`

AI 가 `exec` 매번 호출 → 매번 토큰 소모. `flagQuiet` 가드 또는 default off (verbose 시만).

### 3.4 `waitForAlive` / `waitForReady` styled stderr default — `cmd/status.go:79-91, 105-129`

AI 가 `editor refresh --compile` 호출하면 4줄 토큰. **default quiet + `--narrate` flag opt-in**.

### 3.5 `list --since <hash>` (신규)

**Audience: AI (세션 재사용)**

매 AI 세션 시작에 `list` 호출 → 전체 schema 다운로드. 대부분 세션에서 tool 목록 안 변함.

```bash
hera-agent-pro list --since abc123
# 304-like: {"unchanged": true, "hash": "abc123"}
# or: full list + new hash
```

**lite 의 `list --names` 와 별개 — 그쪽은 이름만 반환, 이쪽은 schema 변경 감지**.

### 3.6 `--fields <a,b,c>` projection (신규)

```bash
hera-agent-pro describe_type GameObject --fields name,methods.signature
```

응답 크기 50~90% 절감. CLI side dot-path 파싱 후 JSON 잘라냄.

### 3.7 ~~`--explain` flag~~ → lite 머지로 base 확보

**lite 의 `EXEC_*` 코드 + `suggestions` + `agent_hint` envelope (§0.3) 가 핵심 가치 흡수.** 추가 가치 = exec 외 다른 tool 에 동일 패턴 확장. **§3.13 와 통합 — 모든 tool 에 `suggestions` / `agent_hint` 적용**.

### 3.8 Unknown command `did-you-mean` — `AgentConnector/Editor/CommandRouter.cs:108-110`

**Audience: AI (typo 회복)**

```csharp
var handler = ToolDiscovery.FindHandler(command);
if (handler == null)
    return new ErrorResponse($"Unknown command: {command}");
```

가까운 tool name (Levenshtein ≤ 2) 응답에 포함. **lite §0.3 `suggestions` envelope 머지 후 자연스럽게 fit**:

```json
{
  "success": false,
  "code": "UNKNOWN_COMMAND",
  "message": "Unknown command: describ_type",
  "suggestions": ["describe_type"]
}
```

### 3.9 `<tool> --sample-call` (신규)

```bash
hera-agent-pro describe_type --sample-call
# 출력:
#   hera-agent-pro describe_type UnityEditor.EditorApplication
#   hera-agent-pro describe_type GameObject --members all
```

`[HeraTool]` attribute 의 `Examples` 노출.

### 3.10 `console --pattern <regex>` (신규)

**lite 의 `console --since` (§0.3) 와 별개 — 그쪽은 incremental, 이쪽은 server-side 필터**.

```bash
hera-agent-pro console --pattern "NullReference"
```

Unity-side filter 가 토큰 90%+ 절감. `--since` 와 조합 가능.

### 3.11 `exec --cache-key <hint>` (신규)

같은 코드를 다른 컨텍스트 (다른 scene state) 로 재실행 시 강제 미스. `--no-cache` 는 무효화. `--cache-key` 로 keying 영역 한정:

```bash
hera-agent-pro exec "return GameObject.FindObjectsOfType<Camera>().Length;" --cache-key scene_main
```

### 3.12 Compact JSON default for tool commands

`--compact-json` 명시할 때만 indent 제거. AI 는 90% indent 불필요. **status/uninstall/doctor pretty default, 그 외 default compact**.

### 3.13 Tool response truncation 마커 표준화 + AgentHint 일반화

**lite 의 `__truncated` envelope (§0.3) + `agent_hint` 필드 (§0.3) 머지 후 — exec 외 다른 tool 에 동일 적용:**

- `describe_type` `truncated` (이미 있음 → `__truncated` envelope 로 통일)
- `console` lines 제한 도달 (lite 의 `truncated: true` flag 와 통일)
- `list_assemblies` filter 적용 후 결과 잘림
- `find_method` 응답 `truncated: true` + `agent_hint: "use --limit"`

AI 가 응답 끝에서 "이게 전부인가?" 의문 없게.

### 3.14 `screenshot --selected` (신규)

`Selection.activeGameObject` 가 있으면 그 영역 capture. 없으면 SceneView 전체. 현재는 SceneView 전체만.

---

## 4. 신규 기능 (Features)

### 4.1 Lite parity → §0 로 흡수 완료

§0.1~§0.4 참조.

### 4.2 CLI 보조

| 항목 | Audience | 우선 |
|---|---|---|
| `uninstall --yes` | Human/CI | P2 |
| `update --to <tag>` 롤백 / 핀 | Human | P3 |
| `batch --dry-run` flag 노출 (내부 변수 존재 `cmd/batch.go:30`) | AI | P2 |
| `list --since <hash>` (§3.5) | AI | P1 |
| `console --pattern <regex>` (§3.10) | AI | P2 |
| `screenshot --selected` (§3.14) | AI | P2 |

### 4.3 License system (Pro identity)

**Audience: 시스템 (수익화)**

`Heartbeat.cs:11-28` TODO 실현 (영문화 완료):

- `hera-agent-pro license activate <key>` (Human)
- `hera-agent-pro license status` (Human)
- `hera-agent-pro license deactivate` (Human)
- 검증 서버: Cloudflare Workers 무료 티어
- 로컬 캐시: `~/.hera-agent-pro/license.json` (오프라인 7~30일)
- Pro-only tool gating: license invalid 시 `ToolMetadataRegistry.DisableTool` 로 `describe_type` / `find_method` / `list_assemblies` / batch endpoint / asset-config 비활성

설계 주의: AI tool 응답에 license 만료 메시지 끼우면 토큰 누적. 만료는 status / doctor / uninstall 같은 human 경로에서만 노출.

### 4.4 신규 Unity 도메인 툴 (AI 라운드트립 절감)

| 툴 | Audience | 가치 | 우선 |
|---|---|---|---|
| `gameobject` (find by path/name/tag, get/set component field, dump tree) | AI | exec 라운드트립 회피 ★★★ | P1 |
| `prefab` (instantiate variant, apply overrides, dump nested) | AI | exec 회피 ★★★ | P1 |
| `inspector_snapshot <go|asset>` | AI | SerializedObject 덤프 — exec 회피 ★★★ | P1 |
| `asset` (`AssetDatabase.FindAssets` 래퍼 label/type/path) | AI | exec 회피 ★★ | P2 |
| `package` (UPM list/add/remove) | AI | API discovery ★★ | P2 |
| `addressables` (group/entry/build info) | AI | 패키지 있을 때만 ★ | P3 |
| `editor reload` (`EditorUtility.RequestScriptReload`) | AI | refresh 명시 trigger ★ | P3 |
| `editor warm` (exec cache 사전 빌드) | AI | cold path 미리 실행 ★ | P3 |

> lite 의 `log` 툴은 §0.3 에서 머지. `ping` 도 §0.3. 별도 항목 아님.

### 4.5 AI ergonomics (§3 항목 신규로 추가)

§3.5 / §3.6 / §3.8 / §3.9 / §3.10 / §3.11 / §3.13 / §3.14 — 모두 신규 기능.

§3.7 (`--explain`) 은 lite §0.3 머지로 흡수, 별도 항목 폐기.

---

## 5. 우선순위 매트릭스

Audience-weighted impact. AI 영향 가중치 ×2.

| 우선 | 항목 | Audience | 사유 |
|---|---|---|---|
| **P0** | §0.2 Heartbeat diff-cache **revert** (= 구 §2.4) | AI | idle Unity 명령 못 보냄. lite 가 같은 결론 — 검증됨 |
| **P0** | §0.2 Heartbeat `MarkCompileRequested` 30s 확장 | AI | premature "ready" tick 으로 exec 가 재컴파일 전 실행 |
| **P0** | §0.4 lite parity: `doctor`, `path_check`, `paths_windows`, `TROUBLESHOOTING.md` | Human | Pro = lite 상위집합 원칙 |
| **P0** | §0.3 Heartbeat 외 핵심 envelope (`ErrorResponse` code/suggestions, `SuccessResponse` agent_hint, exec EXEC_* 코드) | AI | 모든 후속 envelope 변경의 base |
| **P1** | §0.3 Serialize Obsolete/cycle/UnityObject/self-typed/depth3 | AI | 응답 노이즈 대폭 감축 |
| **P1** | §0.3 `log` 툴 | AI | exec 라운드트립 ~500× 절감 |
| **P1** | §0.3 `exec --file` / `--depth` / async pipe / launch failed | AI | exec 안정성 + 토큰 |
| **P1** | §0.3 `ping` | AI | status 보다 가벼움 |
| **P1** | §0.3 `doctor --json` (P0 의 doctor 와 같이) | Human + AI | self-진단 |
| **P1** | §0.3 `console --since` + envelope | AI | incremental fetch |
| **P1** | §0.3 `pollTestResults` PID liveness + §2.3 `waitForReady` 같이 적용 | AI | 5/10분 hang 회피 |
| **P1** | §0.3 `list --tool` / `--names` slim | AI | schema 토큰 절감 |
| **P1** | §0.2 client retry 길이 확장 (5s reload 윈도우 커버) | AI | reliability |
| **P1** | §0.2 update `.bak` sweep 호출 (= 구 §2.9) | Human | 디스크 위생 |
| **P1** | §0.2 ReadConsole init 실패 구조화 | AI | 진단성 |
| **P1** | §1.3 `check → compile_only` 위치 | AI | 커스텀 툴 schema 위반 |
| **P1** | §2.2 비-JSON 응답 success | AI | cascading 실패 |
| **P1** | §3.1 styled error 분리 (Option B) | AI | tool = plain JSON. `isHumanCommand` 공통 의존 |
| **P1** | §3.2 `printUpdateNotice` AI 차단 | AI | 토큰 노이즈 |
| **P1** | §3.5 `list --since <hash>` | AI | schema 재다운로드 절감 |
| **P1** | §3.8 did-you-mean | AI | typo retry 회피 |
| **P1** | §3.13 truncation 표준화 + agent_hint 일반화 | AI | (구 §3.7 흡수) |
| **P1** | §4.4 `gameobject` / `prefab` / `inspector_snapshot` | AI | exec 라운드트립 핵심 |
| **P2** | §2.4 batch fail_fast 기본값 | AI | 토큰 손실 |
| **P2** | §2.5 csc deps 동적 검증 | AI | exec 신뢰성 |
| **P2** | §2.8 splitArgs over-consume | AI | 입력 파싱 |
| **P2** | §3.3 / §3.4 stderr noise 감축 | AI | 토큰 |
| **P2** | §3.6 `--fields` projection | AI | 응답 크기 |
| **P2** | §3.10 `console --pattern` | AI | grep 절감 |
| **P2** | §3.14 `screenshot --selected` | AI | visual context |
| **P2** | §4.4 `asset` / `package` | AI | 신규 툴 |
| **P2** | §1.4 `uninstall --yes` | Human/CI | 자동화 |
| **P2** | §1.6 `.profile` / fish 지원 | Human | install |
| **P3** | §1.2 partial class 분할 | 개발자 | 가독성 |
| **P3** | §1.5 / §1.7 / §1.8 / §1.9 / §1.10 / §1.11 | 개발자 | 코드 위생 |
| **P3** | §2.1 / §2.6 / §2.7 / §2.9 / §2.10 / §2.11 / §2.12 | Mixed | edge case |
| **P3** | §3.9 `--sample-call` | AI | 보조 |
| **P3** | §3.11 `exec --cache-key` | AI | 보조 |
| **P3** | §3.12 compact JSON default | AI | 정책 |
| **P3** | §4.3 license system | 시스템 | 별도 트랙 |
| **P3** | §4.4 `addressables` / `editor reload` / `editor warm` | AI | 보조 툴 |

---

## 6. Implementation 순서

1. **P0 lite parity (한 큰 PR — 또는 2-3 분할)**:
   - §0.2 partial 전체 (Heartbeat revert + MarkCompileRequested + client retry + .bak sweep + ReadConsole init)
   - §0.3 envelope core (`ErrorResponse` code/suggestions, `SuccessResponse` agent_hint, `CommandResponse` 노출)
   - §0.3 Serialize 안전화 (Obsolete/cycle/UnityObject/self-typed/depth3)
   - §0.4 doctor + path_check + paths_windows + TROUBLESHOOTING

2. **P1 lite parity 후속**:
   - §0.3 exec EXEC_* / async pipe / launch failed / `--file` / `--depth`
   - §0.3 `log` 툴 (+ `.cs.meta`)
   - §0.3 `ping`
   - §0.3 `console --since` + envelope (`ReadConsole.cs` 응답 구조 변경)
   - §0.3 `pollTestResults` PID liveness + §2.3 `waitForReady` 동시 적용
   - §0.3 `list --tool` / `--names`

3. **P1 Pro 고유 schema 신뢰성**:
   - §1.3 + §2.2
   - §3.1 + §3.2 + §3.4 (`isHumanCommand` 공통)
   - §3.8 + §3.5

4. **P1 신규 도메인 툴**:
   - §4.4 `gameobject` → `prefab` → `inspector_snapshot` (별 PR)

5. **P2 token economy + Pro 고유**:
   - §3.3 / §3.6 / §3.10 / §3.13 / §3.14
   - §2.4 / §2.5 / §2.8
   - §1.4 / §1.6

6. **P3 코드 위생 + 보조 features**: 일괄

7. **별도 트랙**: §4.3 license system

---

*분석 일시: 2026-05-19*
*기준 lite 경로: `C:\Users\PC\Desktop\Cowork\hera-agent` (lite repo, today commits: 6466c7d → 96a5f72)*
*Audience reality: tool 명령 = AI agents (Claude Code CLI / Codex). install / status / uninstall / doctor 만 human.*
