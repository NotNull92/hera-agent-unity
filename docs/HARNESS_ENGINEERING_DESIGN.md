# Harness Engineering & Orchestration 설계 문서

> hera-agent-unity-pro의 성능, 코드 품질, 확장성을 개선하기 위한 설계 가이드.
> 속도와 정확성을 최우선으로 하며, 지연을 유발하는 패턴은 배제.

---

## 설계 원칙

1. **속도 저하 없음** — 기존 응답 시간을 해치지 않는다. 모든 변경은 동일하거나 더 빨라야 한다.
2. **Fail-Fast** — 실패할 거면 빨리 실패한다. 잘못된 요청을 Unity까지 보내지 않는다.
3. **Overthinking 금지** — 하네스가 에이전트보다 오래 생각하면 안 된다. 단순한 명령에 복잡한 검증을 씌우지 않는다.
4. **점진적 적용** — 한 번에 전부 바꾸지 않는다. 독립적으로 적용 가능한 단위로 나눈다.

---

## 구조 개요

```
Layer 1: 성능 개선 (체감 가능)
  ├── Roslyn API 전환 (exec: -150ms)
  └── 이벤트 기반 테스트 결과 (test: -250ms)

Layer 2: 코드 품질 & 확장성 (체감 불가, 기반 강화)
  ├── HTTP 클라이언트 풀링
  ├── SemaphoreSlim 읽기/쓰기 분리
  ├── ToolDiscovery 캐시 검증 강화
  ├── Heartbeat 상태변경 기반 쓰기
  ├── 매개변수 사전 검증
  ├── Tool 결과 캐싱 (제한적)
  └── 폴링 백오프 전략

Layer 3: Overthinking 제어
  ├── 명령 위험도 분류 (safe / dangerous)
  ├── 검증 시점 최적화 (Block-at-Submit)
  └── 도구 지연 활성화
```

---

## Layer 1: 성능 개선

### 1-1. Roslyn API 전환 — exec 명령

**현재 문제**

`ExecuteCsharp.cs`에서 매 `exec` 호출마다 외부 프로세스를 생성한다:

```csharp
// ExecuteCsharp.cs:188
using (var proc = Process.Start(psi))
{
    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    if (!proc.WaitForExit(30000))
    {
        try { proc.Kill(); } catch { }
        ...
    }
}
```

Windows에서 `Process.Start()` 오버헤드: **50-200ms**
프로세스 생성 → stdin/stdout 파이프 → csc 실행 → 결과 읽기 → 프로세스 종료

**변경 설계**

Roslyn `CSharpCompilation` API로 인프로세스 컴파일:

```csharp
// 설계 — RoslynCompiler.cs (신규)
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

public static class RoslynCompiler
{
    // 메타데이터 참조는 한 번만 수집하여 캐싱
    private static MetadataReference[] s_CachedReferences;

    public static CompileResult Compile(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetCachedReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: $"HeraExec_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage());
            return CompileResult.Failure(string.Join("\n", errors));
        }

        ms.Seek(0, SeekOrigin.Begin);
        return CompileResult.Success(ms.ToArray());
    }

    private static MetadataReference[] GetCachedReferences()
    {
        if (s_CachedReferences != null) return s_CachedReferences;

        s_CachedReferences = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a =>
            {
                try { return MetadataReference.CreateFromFile(a.Location); }
                catch { return null; }
            })
            .Where(r => r != null)
            .ToArray();

        return s_CachedReferences;
    }

    // Domain reload 시 캐시 무효화
    [InitializeOnLoadMethod]
    static void InvalidateOnReload() => s_CachedReferences = null;
}
```

**성능 예상**

| 항목 | 현재 (Process.Start) | 변경 후 (Roslyn) |
|:-----|:---:|:---:|
| 컴파일 시간 | 200-400ms | 50-150ms |
| 메모리 | 프로세스당 ~20MB (일시적) | +~50MB (상주) |
| 임시 파일 | src + rsp + out (3개) | 없음 (메모리 내) |
| 프로세스 생성 | 매번 | 없음 |

**트레이드오프**: 메모리 50MB 상주 vs 호출당 150ms 절감. AI 에이전트가 exec를 반복 호출하는 워크플로우에서는 메모리 트레이드가 충분히 가치 있음.

**주의사항**
- Roslyn NuGet 패키지를 UPM 패키지에 포함해야 함 (DLL 번들링)
- Unity 버전별 Roslyn 호환성 확인 필요 (Unity 6000.0+ 기준)
- `AssemblyLoadContext` 관리는 기존 `ExecuteCsharp.cs`의 Collectible ALC 패턴 유지
- 폴백: Roslyn 로드 실패 시 기존 Process.Start 방식으로 자동 전환

**적용 순서**
1. `RoslynCompiler.cs` 신규 생성
2. `ExecuteCsharp.cs`의 `CompileAndExecute()`에서 Roslyn 우선 시도, 실패 시 기존 방식 폴백
3. 기존 임시 파일 관리 로직은 폴백 경로에만 유지
4. `s_CachedReferences` 무효화를 `[InitializeOnLoadMethod]`에 등록

---

### 1-2. 이벤트 기반 테스트 결과 — test 명령

**현재 문제**

Go CLI가 500ms 간격으로 결과 파일을 폴링한다:

```go
// test.go:81-115
for time.Now().Before(deadline) {
    time.Sleep(500 * time.Millisecond)
    data, err := os.ReadFile(resultsPath)
    ...
}
```

테스트 완료 → 다음 폴링까지 최대 500ms, 평균 250ms 불필요 대기.

**변경 설계**

C# 측에서 테스트 완료 시 결과 파일을 쓴 후, Go CLI가 `fsnotify`로 감지:

```go
// 설계 — test.go 변경
import "github.com/fsnotify/fsnotify"

func watchTestResults(resultsPath string, timeout time.Duration) ([]byte, error) {
    watcher, err := fsnotify.NewWatcher()
    if err != nil {
        // 폴백: 기존 폴링 방식
        return pollTestResults(resultsPath, timeout)
    }
    defer watcher.Close()

    dir := filepath.Dir(resultsPath)
    if err := watcher.Add(dir); err != nil {
        return pollTestResults(resultsPath, timeout)
    }

    deadline := time.After(timeout)
    for {
        select {
        case event := <-watcher.Events:
            if event.Op&(fsnotify.Create|fsnotify.Write) != 0 {
                if filepath.Base(event.Name) == filepath.Base(resultsPath) {
                    // 파일 쓰기 완료 대기 (짧은 딜레이)
                    time.Sleep(50 * time.Millisecond)
                    data, err := os.ReadFile(resultsPath)
                    if err == nil && len(data) > 0 {
                        return data, nil
                    }
                }
            }
        case err := <-watcher.Errors:
            // 워처 에러 시 폴링 폴백
            _ = err
            return pollTestResults(resultsPath, timeout)
        case <-deadline:
            return nil, fmt.Errorf("test timed out after %v", timeout)
        }
    }
}
```

**성능 예상**

| 항목 | 현재 (폴링) | 변경 후 (fsnotify) |
|:-----|:---:|:---:|
| 결과 감지 지연 | 0-500ms (평균 250ms) | ~50ms (쓰기 완료 대기) |
| CPU 사용 | 500ms마다 웨이크업 | 이벤트 대기 (idle) |
| 파일 읽기 횟수 | 최대 1200회 (10분) | 1회 |

**트레이드오프**: `fsnotify` 의존성 추가 vs 250ms 지연 제거 + CPU 효율. fsnotify는 Go 생태계에서 가장 안정적인 파일 감시 라이브러리로, 추가 리스크 낮음.

**주의사항**
- Windows: ReadDirectoryChangesW 기반, 안정적
- `fsnotify` 실패 시 기존 폴링으로 자동 폴백 (graceful degradation)
- 결과 파일 쓰기 중 읽기 방지를 위해 50ms 딜레이 유지
- `go.mod`에 `github.com/fsnotify/fsnotify` 추가 필요

**적용 순서**
1. `go get github.com/fsnotify/fsnotify`
2. `test.go`에 `watchTestResults()` 추가
3. 기존 `pollTestResults()`는 폴백으로 유지
4. `watchTestResults()`를 기본 경로로 전환

---

## Layer 2: 코드 품질 & 확장성

> 이 레이어의 변경은 체감 속도 차이가 없다 (합산 2-3ms).
> 목적은 코드 견고성, 디버깅 편의성, 향후 확장 대비.

### 2-1. HTTP 클라이언트 풀링

**현재**: 매 요청마다 `http.Client{}` 새로 생성 (`client.go:216`)

**변경**:
```go
// client.go — 패키지 레벨 싱글톤
var defaultClient = &http.Client{
    Timeout: 120 * time.Second,
    Transport: &http.Transport{
        MaxIdleConns:        10,
        MaxIdleConnsPerHost: 5,
        IdleConnTimeout:     90 * time.Second,
    },
}

func (inst *Instance) Send(cmd string, params map[string]interface{}) (*CommandResponse, error) {
    // defaultClient 사용
    resp, err := defaultClient.Post(url, "application/json", body)
    ...
}
```

**이점**: GC 압력 감소, 연결 재사용, localhost에서 ~1ms 절감 (체감 불가하나 코드가 올바름)

---

### 2-2. SemaphoreSlim 읽기/쓰기 분리

**현재**: 전역 `SemaphoreSlim(1,1)` — 모든 명령 직렬화

**변경**:
```csharp
// CommandRouter.cs
// 읽기 전용 명령 목록
private static readonly HashSet<string> s_ReadOnlyCommands = new()
{
    "status", "list", "console", "screenshot"
};

// 읽기 전용은 락 없이, 쓰기는 기존 락 유지
public static async Task<object> Dispatch(string command, JObject parameters)
{
    if (s_ReadOnlyCommands.Contains(command))
        return await ExecuteHandler(command, parameters);

    if (!await s_Lock.WaitAsync(TimeSpan.FromSeconds(120)))
        return new ErrorResponse("Command dispatch timed out...");
    try
    {
        return await ExecuteHandler(command, parameters);
    }
    finally { s_Lock.Release(); }
}
```

**이점**: 현재는 효과 없음 (동시 요청 없음). 향후 멀티에이전트 또는 배치 API 추가 시 병렬 읽기 가능.

---

### 2-3. ToolDiscovery 캐시 검증 강화

**현재**: 이미 캐싱됨. 다만 `s_SchemaCache` 재구축 시 스키마 JSON 직렬화를 매번 수행.

**변경**:
```csharp
// ToolDiscovery.cs
// 스키마 JSON 문자열을 캐싱하여 list 명령에서 재직렬화 방지
private static string s_SchemaCacheJson;

public static string GetToolSchemasJson()
{
    EnsureCache();
    if (s_SchemaCacheJson == null)
        s_SchemaCacheJson = JsonConvert.SerializeObject(s_SchemaCache);
    return s_SchemaCacheJson;
}
```

**이점**: `list` 명령 호출 시 직렬화 1회만 수행. 미미하지만 올바른 캐싱 패턴.

---

### 2-4. Heartbeat 상태변경 기반 쓰기

**현재**: 0.5초마다 무조건 파일 쓰기

**변경**:
```csharp
// Heartbeat.cs
private static string s_LastStatusJson;

static void WriteIfChanged(HeartbeatStatus status)
{
    var json = JsonConvert.SerializeObject(status);
    if (json == s_LastStatusJson) return;  // 변경 없으면 스킵

    s_LastStatusJson = json;
    File.WriteAllText(GetFilePath(), json);
}
```

**이점**: 상태가 변하지 않으면 I/O 0회. `ready` 상태에서 대부분의 쓰기를 제거.

---

### 2-5. 매개변수 사전 검증

**현재**: 검증이 C# 측에서만 발생. 잘못된 파라미터도 HTTP 왕복 후 에러 반환.

**변경**:
```go
// root.go — 디스패치 전 기본 검증
func validateBasicParams(command string, params map[string]interface{}) error {
    switch command {
    case "exec":
        if _, ok := params["code"]; !ok {
            return fmt.Errorf("[Hera] I need a 'code' parameter to execute C# code")
        }
    case "test":
        if mode, ok := params["mode"]; ok {
            if mode != "EditMode" && mode != "PlayMode" {
                return fmt.Errorf("[Hera] Test mode must be 'EditMode' or 'PlayMode', got '%s'", mode)
            }
        }
    }
    return nil
}
```

**이점**: 명백한 오류를 HTTP 왕복 없이 즉시 차단 (~5-15ms 절감/오류). 오류 빈도가 낮아 평균 영향 미미하나, 디버깅 경험 개선.

---

### 2-6. Tool 결과 캐싱 (제한적)

**현재**: 모든 명령이 매번 Unity에 요청

**변경**: `list` 명령만 캐싱 (도구 목록은 domain reload 전까지 불변):
```go
// root.go
var (
    listCache     *CommandResponse
    listCacheTime time.Time
)

func getToolList(inst *client.Instance) (*CommandResponse, error) {
    if listCache != nil && time.Since(listCacheTime) < 30*time.Second {
        return listCache, nil
    }
    resp, err := inst.Send("list", nil)
    if err == nil && resp.Success {
        listCache = resp
        listCacheTime = time.Now()
    }
    return resp, err
}
```

**이점**: AI 에이전트가 `list`를 반복 호출하는 패턴에서 HTTP 왕복 제거. 다른 명령(exec, play 등)은 side effect가 있으므로 캐싱하지 않음.

---

### 2-7. 폴링 백오프 전략

**현재**: `waitForAlive()`, `waitForReady()` 모두 고정 500ms 간격

**변경**:
```go
// status.go — 지수 백오프 (최소 100ms → 최대 2s)
func backoffSleep(attempt int) time.Duration {
    d := 100 * time.Millisecond
    for i := 0; i < attempt && d < 2*time.Second; i++ {
        d = d * 3 / 2  // 1.5배씩 증가
    }
    if d > 2*time.Second {
        d = 2 * time.Second
    }
    return d
}

// 사용: 초기 응답은 더 빠르게 감지, 장기 대기는 CPU 절약
for attempt := 0; time.Now().Before(deadline); attempt++ {
    time.Sleep(backoffSleep(attempt))
    ...
}
```

**진행**: 100ms → 150ms → 225ms → 337ms → 506ms → 759ms → 1.1s → 1.7s → 2s (cap)

**이점**: 초기 응답 감지가 500ms → 100ms로 빨라짐. 장기 대기 시 CPU 웨이크업 감소. 다만 평균적으로는 기존과 비슷하거나 초기 몇 회만 빠름.

---

## Layer 3: Overthinking 제어

> 하네스를 추가하면 검증 레이어가 늘어난다.
> 검증이 실제 작업보다 오래 걸리면 본말전도.
> 아래 3가지 제어장치로 하네스가 속도를 잡아먹지 않도록 한다.

### 3-1. 명령 위험도 분류

모든 명령을 `safe`와 `dangerous`로 분류하고, 검증 레벨을 다르게 적용:

```
safe (검증 최소화):
  ├── status     — 읽기 전용, 부작용 없음
  ├── list       — 읽기 전용, 부작용 없음
  ├── console    — 읽기 전용, 부작용 없음
  └── screenshot — 읽기 전용, 부작용 없음

dangerous (풀 검증):
  ├── exec       — 임의 코드 실행
  ├── editor     — 플레이 모드 제어
  ├── test       — 테스트 실행 (시간 소요)
  ├── menu       — 메뉴 아이템 실행 (부작용 불명)
  ├── refresh    — 에셋 리임포트 + 컴파일
  ├── reserialize — 에셋 파일 수정
  └── profiler   — 프로파일러 상태 변경
```

**적용 규칙**:
- `safe` 명령: SemaphoreSlim 우회, 매개변수 검증 스킵, 캐싱 허용
- `dangerous` 명령: 전체 검증 파이프라인 통과

---

### 3-2. 검증 시점 최적화 — Block-at-Submit

Anthropic의 Claude Code 설계에서 검증된 패턴:

```
❌ Block-at-Write (매 단계 검증):
   요청 → [검증] → 파싱 → [검증] → 라우팅 → [검증] → 실행 → [검증] → 응답
   
✅ Block-at-Submit (커밋 시점 검증):
   요청 → 파싱 → 라우팅 → 실행 → [검증] → 응답
```

**구현**: 검증은 응답 직전에 한 번만 수행:

```csharp
// CommandRouter.cs — 실행 후 결과 검증
private static async Task<object> ExecuteAndValidate(string command, JObject parameters)
{
    var result = await ExecuteHandler(command, parameters);

    // 실행 후 결과 검증 (실행 전이 아님)
    if (result is SuccessResponse sr && sr.Data != null)
    {
        // 데이터 크기 제한 (과도한 응답 방지)
        var json = JsonConvert.SerializeObject(sr.Data);
        if (json.Length > 1_000_000)  // 1MB 초과
        {
            return new SuccessResponse(
                sr.Message + " (response truncated)",
                TruncateData(sr.Data)
            );
        }
    }

    return result;
}
```

**이유**: 입력 검증을 여러 단계에 분산하면 각 단계에서 "이게 맞나?" 체크가 쌓여서 총 지연이 커짐. 한 곳에서 한 번만 검증하면 하네스 오버헤드를 최소화할 수 있음.

---

### 3-3. 도구 지연 활성화

**현재**: `ToolDiscovery`가 모든 도구를 한 번에 스캔

**원칙**: 도구 수가 적을수록 에이전트의 선택 시간이 줄어듦 (Claude Code: 기본 20개 미만 유지)

**적용**: 이 프로젝트는 이미 9개 도구로 적은 편. 추가 조치 불필요.
단, 향후 도구가 20개를 넘으면:
- 기본 도구 세트 (status, exec, console, editor, test)만 기본 노출
- 나머지는 `list` 호출 또는 명시적 요청 시에만 활성화
- Go CLI의 `--help` 출력에서 자주 쓰는 명령을 상단에 배치

---

## 적용 로드맵

### Phase 1: 즉시 (독립 적용, 상호 의존 없음)

| 항목 | 파일 | 예상 작업량 |
|:-----|:-----|:---:|
| HTTP 클라이언트 풀링 | `client.go` | 10분 |
| Heartbeat 상태변경 기반 쓰기 | `Heartbeat.cs` | 15분 |
| 명령 위험도 분류 | `CommandRouter.cs` | 20분 |
| ToolDiscovery JSON 캐싱 | `ToolDiscovery.cs` | 10분 |

### Phase 2: 단기 (1-2일)

| 항목 | 파일 | 예상 작업량 |
|:-----|:-----|:---:|
| 이벤트 기반 테스트 결과 | `test.go` + `go.mod` | 2시간 |
| 매개변수 사전 검증 | `root.go` | 1시간 |
| SemaphoreSlim 분리 | `CommandRouter.cs` | 30분 |
| 폴링 백오프 | `status.go`, `editor.go` | 30분 |
| Tool 결과 캐싱 (list) | `root.go` | 30분 |

### Phase 3: 중기 (검증 필요)

| 항목 | 파일 | 예상 작업량 |
|:-----|:-----|:---:|
| Roslyn API 전환 | 신규 `RoslynCompiler.cs` + `ExecuteCsharp.cs` | 1-2일 |
| Block-at-Submit 패턴 | `CommandRouter.cs` | 1시간 |

---

## 참고 자료

### Harness Engineering
- [Martin Fowler — Harness Engineering for Coding Agents](https://martinfowler.com/articles/harness-engineering.html)
- [Addy Osmani — Agent Harness Engineering](https://addyosmani.com/blog/agent-harness-engineering/)
- [MindStudio — Stripe, Shopify, Airbnb 실무 사례](https://www.mindstudio.ai/blog/ai-coding-agent-harness-stripe-shopify-airbnb)
- [12 Agentic Harness Patterns from Claude Code](https://generativeprogrammer.com/p/12-agentic-harness-patterns-from)

### 성능 최적화
- [Tool Cache Agent — Intelligent Tool Call Caching](https://openreview.net/forum?id=tX3YcbNa5w)
- [HTTP Connection Pooling](https://devblogs.microsoft.com/premier-developer/the-art-of-http-connection-pooling-how-to-optimize-your-connections-for-peak-performance/)
- [Azure — AI Agent Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)

### 학술 논문
- [Building AI Coding Agents for the Terminal (ArXiv 2603.05344)](https://arxiv.org/html/2603.05344v2)
- [Dive into Claude Code (ArXiv 2604.14228)](https://arxiv.org/html/2604.14228v1)
- [Inside the Scaffold: Taxonomy (ArXiv 2604.03515)](https://arxiv.org/html/2604.03515)

### Overthinking 제어
- [Claude Code Auto Mode — Block-at-Submit](https://www.anthropic.com/engineering/claude-code-auto-mode)
- [Avoiding Overthinking: Curriculum-Aware Budget Scheduling (ArXiv 2604.19780)](https://arxiv.org/abs/2604.19780)
- [Stop Overthinking: Efficient Reasoning Survey (ArXiv 2503.16419)](https://arxiv.org/pdf/2503.16419)
- [Token-Budget-Aware LLM Reasoning (ACL 2025)](https://aclanthology.org/2025.findings-acl.1274.pdf)
- [Early Reasoning Exit via Pattern Mining (ArXiv 2508.17627)](https://arxiv.org/html/2508.17627v1)

---

*이 문서는 버전 관리에 포함되어야 하며, 구현 완료 시 각 항목에 완료 날짜를 기록합니다.*
