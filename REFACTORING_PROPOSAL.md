# hera-agent-unity 리팩토링 제안

> 성능 최적화 + 토큰 사용량 감소 관점에서 코드베이스를 분석한 결과입니다.  
> 우선순위: **P0(즉시)** / **P1(고려)** / **P2(장기)**

---

## 1. 토큰 사용량 감소 (Response Payload 최적화)

### 1.1 `exec` 기본 직렬화 깊이를 1로 변경 + Unity Object 얕게 처리
**위치**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs:106-113`, `:724-805`  
**우선순위**: P0

- 현재 `DefaultSerializeDepth = 3`이지만 `AGENTS.md`에는 기본값이 1로 문서화되어 있습니다. 실제 동작과 문서가 불일치합니다.
- Unity Object(`GameObject`, `Component`, `Transform` 등)를 실수로 `return`하면 수 KB~수십 KB가 한 번에 나갑니다.

**제안**:
```csharp
private const int DefaultSerializeDepth = 1;  // 3 -> 1

private static object Serialize(object obj, int depth, int maxDepth, HashSet<object> visited)
{
    // ... existing checks ...

    if (obj is UnityEngine.Object uo)
    {
        // Unity Object는 depth와 무관하게 얕은 식별자만 반환
        return new
        {
            name = uo.name,
            type = uo.GetType().Name,
            instance_id = EntityIdCompat.IdOf(uo)
        };
    }
    // ...
}
```

**기대 효과**: 실수로 `return transform;` 했을 때 응답 크기 9KB+ → 수십 B. LLM context 절약.

---

### 1.2 `exec` 직렬화에서 대용량 데이터 제한
**위치**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs:724-805`  
**우선순위**: P0

현재 `IDictionary`는 항목 수 제한이 없고, 긴 문자염(`TextAsset.text`, 큰 로그)은 잘라내지 않습니다.

**제안**:
```csharp
const int MaxStringLength = 2048;
const int MaxDictionaryEntries = 1000;
const int MaxEnumerableItems = 100; // 이미 존재

if (obj is string s)
    return s.Length > MaxStringLength ? s.Substring(0, MaxStringLength) + "... [truncated]" : s;

if (obj is IDictionary dict)
{
    var r = new Dictionary<string, object>();
    int count = 0;
    foreach (DictionaryEntry e in dict)
    {
        if (++count > MaxDictionaryEntries) { r["__truncated"] = true; break; }
        r[e.Key.ToString()] = Serialize(e.Value, depth + 1, maxDepth, visited);
    }
    return r;
}
```

---

### 1.3 `console --lines` 기본값 20 적용
**위치**: `AgentConnector/Editor/Tools/ReadConsole.cs:109`  
**우선순위**: P0

```csharp
// 현재: lines 미지정 시 전체 콘솔 로그 반환
int? count = p.GetInt("lines") ?? p.GetInt("count");

// 변경
int? count = p.GetInt("lines") ?? p.GetInt("count") ?? 20;
```

`AGENTS.md`에도 "default 20"으로 문서화되어 있으나 코드에는 반영되지 않았습니다.

---

### 1.4 응답 envelope에서 null 필드 제거
**위치**: `AgentConnector/Editor/Core/Response.cs`, `AgentConnector/Editor/HttpServer.cs:285`  
**우선순위**: P1

현재 `data: null`, `timings: null`이 항상 실려 나갑니다. AI-target 명령에서는 불필요한 바이트입니다.

**제안**:
```csharp
// Response.cs
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public object data;

[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public Dictionary<string, long> timings;

// HttpServer.cs
var settings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore,
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
};
var responseJson = JsonConvert.SerializeObject(result, settings);
```

또한 Unity Object가 누설되지 않도록 커스텀 `JsonConverter<UnityEngine.Object>`를 글로벌 설정에 추가해 `{name,type,instance_id}`로 평탄화합니다.

---

### 1.5 `batch` 명령이 compact/quiet 모드를 존중하도록 수정
**위치**: `cmd/batch.go:76-82`  
**우선순위**: P1

현재 `batch`는 항상 `tui.Progress`, `tui.StatusBadge` 등 스타일 출력을 하며 각 step의 `Data`는 버립니다.

**제안**:
```go
func batchCmd(ctx context.Context, args []string, sendBatch SendBatchFunc, resolve instanceResolver) error {
    // ... existing setup ...

    compact := flagCompactJSON || !isHumanCommand("batch")
    quiet := flagQuiet

    for i, result := range resp.Results {
        if compact {
            b, _ := json.Marshal(result)
            fmt.Println(string(b))
            continue
        }
        status := "OK"
        if !result.Success { status = "FAIL" }
        if quiet {
            fmt.Printf("[%d/%d] %s: %s\n", i+1, len(resp.Results), status, result.Message)
        } else {
            fmt.Printf("%s %s %s\n", tui.Progress(i+1, len(resp.Results)), tui.StatusBadge(status), result.Message)
        }
    }
    // ...
}
```

---

### 1.6 `manage_components get`의 property dump 제한
**위치**: `AgentConnector/Editor/Tools/ManageComponents.cs:314-337`  
**우선순위**: P1

`Terrain`, `MeshRenderer`, `ParticleSystem` 등의 컴포넌트는 `SerializedProperty`가 매우 큽니다.

**제안**:
- `--property` 미지정 시 최상위 property 이름만 반환 (기본 동작 변경) 또는
- `--depth` / `--limit` 파라미터 추가해 deep dump를 opt-in으로 변경.

---

## 2. 성능 최적화

### 2.1 C# 컴파일 캐시 메모리 크기 증가
**위치**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:24`  
**우선순위**: P1

```csharp
private const int MaxInMemoryAssemblies = 128; // 32 -> 128
```

장시간 에이전트 세션에서 자주 쓰는 snippet(예: `GameObject.Find`, `AssetDatabase` 조회)의 메모리 캐시 히트율이 올라갑니다.

---

### 2.2 컴파일러 서버 사전 예열 (pre-warm)
**위치**: `AgentConnector/Editor/HttpServer.cs` 또는 `ExecuteCsharp.cs`  
**우선순위**: P1

도메인 리로드 후 첫 `exec`는 VBCSCompiler를 다시 띄워야 해서 5~15초가 걸립니다.

**제안**: `Heartbeat`나 `HttpServer` 시작 시 백그라운드로 `return null;` snippet을 컴파일만 합니다.

```csharp
// HttpServer.cs Start()
EditorApplication.delayCall += () => {
    try {
        ExecuteCsharp.CompileOnly("return null;");
    } catch { /* ignore */ }
};
```

---

### 2.3 `asset-config.json`의 컴파일러 경로를 `exec`에서 사용
**위치**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:138-158`, `internal/assetconfig/config.go`  
**우선순위**: P1

`HeraAgentAssetConfigWindow`에는 SDK-aware한 `csc`/`dotnet` 리졸버가 있지만 `ExecuteCsharp`는 이를 무시하고 느린 recursive `Directory.GetFiles`를 사용합니다.

**제안**:
- `asset-config.json`에 저장된 `defaultCscPath` / `defaultDotnetPath`를 먼저 확인.
- 미설정 시에만 fallback으로 검색.

---

### 2.4 컴파일러 탐색에서 recursive `GetFiles` 제거
**위치**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:229-272`  
**우선순위**: P1

```csharp
private static string FindCsc()
{
    var content = EditorApplication.applicationContentsPath;
    var candidates = new[] {
        Path.Combine(content, "DotNetSdkRoslyn", "csc.dll"),
        Path.Combine(content, "MonoBleedingEdge", "lib", "mono", "4.5", "csc.exe"),
        // ... known paths
    };
    foreach (var c in candidates) if (File.Exists(c)) return c;
    return SearchFile(content, "csc.dll"); // 최후의 수단
}
```

---

### 2.5 Assembly reference enumeration 캐싱 개선
**위치**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:101-136`  
**우선순위**: P1

현재는 매 도메인 리로드 후 `EnsureRefs`가 모든 어셈블리를 다시 탐색합니다.

**제안**:
- `refs-<hash>.rsp`가 이미 존재하면 어셈블리 개수/해시가 일치하는지 빠르게 확인하고 탐색을 건 너뜁니다.
- 또는 `EditorApplication.quitting`이 아닌 한, 이전 세션의 refs 파일을 재사용.

---

### 2.6 디스크 DLL 캐시 정리
**위치**: `AgentConnector/Editor/Tools/ExecCompileCache.cs`  
**우선순위**: P2

`Library/HeraAgentCache/bin/`은 무제한 증가합니다.

**제안**:
- 접근 시간 기준 7일 이상된 DLL 삭제
- 또는 총 용량 100MB 초과 시 LRU 삭제
- `CleanupOldTempFiles`와 유사한 방식으로 `BinCacheDir` 정리

---

### 2.7 `Serialize`에서 멤버 정보 캐싱
**위치**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs:780-801`  
**우선순위**: P2

매 호출마다 `type.GetFields()` / `type.GetProperties()`를 반복합니다.

**제안**:
```csharp
private static readonly Dictionary<Type, MemberInfo[]> s_MemberCache = new();

private static MemberInfo[] GetSerializableMembers(Type type)
{
    if (s_MemberCache.TryGetValue(type, out var members)) return members;
    // ... compute fields + properties ...
    s_MemberCache[type] = members;
    return members;
}
```

---

### 2.8 Go 응답 출력에서 불필요한 JSON round-trip 제거
**위치**: `cmd/root.go:415-429`  
**우선순위**: P1

현재 `resp.Data`를 `interface{}`로 unmarshal한 뒤 다시 marshal합니다. 숫자 정밀도 손실, 키 순서 변경, CPU/메모리 낭비가 발생합니다.

**제안**:
```go
if rp.shouldCompactJSON(category) {
    fmt.Println(string(resp.Data))
} else {
    var buf bytes.Buffer
    if err := json.Indent(&buf, resp.Data, "", "  "); err == nil {
        fmt.Println(buf.String())
    } else {
        fmt.Println(string(resp.Data))
    }
}
```

---

### 2.9 HTTP 응답 크기 제한
**위치**: `internal/client/client.go:376-396`  
**우선순위**: P1

현재 `io.ReadAll(resp.Body)`에 크기 제한이 없어 Unity가 거대한 응답을 본낼 때 CLI OOM 위험이 있습니다.

**제안**:
```go
const maxResponseSize = 50 * 1024 * 1024 // 50MB
respBody, err := io.ReadAll(io.LimitReader(resp.Body, maxResponseSize))
```

---

### 2.10 `SendBatch`에도 domain-reload retry 추가
**위치**: `internal/client/client.go:455-506`  
**우선순위**: P1

`Send`는 `doWithReloadRetry`로 도메인 리로드를 버틸 수 있지만 `SendBatch`는 없습니다.

**제안**: `SendBatch`도 `doWithReloadRetry` 패턴을 재사용하거나, 유사한 retry 루프를 추가합니다.

---

### 2.11 HTTP keep-alive / timeout 튜닝
**위치**: `internal/client/client.go:50-61`  
**우선순위**: P2

- `MaxIdleConnsPerHost: 4`는 충분하지만 `ResponseHeaderTimeout`이 없습니다.
- Unity가 idle connection을 끊을 때 `"Unsolicited response received on idle HTTP channel"` 로그가 반복됩니다.

**제안**:
```go
Transport: &http.Transport{
    // ... existing fields ...
    ResponseHeaderTimeout: 30 * time.Second,
    MaxConnsPerHost:       8,
    // ForceAttemptHTTP2: false, // Unity HttpListener는 HTTP/1.1
}
```

또한 idle connection을 더 짧게 회수해 domain reload 후 stale connection 문제를 줄입니다:
```go
IdleConnTimeout: 5 * time.Second, // 30s -> 5s (localhost이므로 재연결 비용이 작음)
```

---

### 2.12 `SendBatch` 타임아웃이 `--timeout`을 무시하는 문제
**위치**: `internal/client/client.go:457-467`  
**우선순위**: P2

`SendBatch`는 독자적으로 `30s + 15s * N`을 계산합니다. 사용자가 `--timeout 120000`을 줘도 5분 cap이 우선합니다.

**제안**: 사용자 `--timeout`이 명시적으로 주어지면 그것을 존중하고, 미지정 시에만 기본 공식을 사용.

---

## 3. 정확성 / 유지보수 개선

### 3.1 `batch` 성공 시 nil pointer panic
**위치**: `cmd/root.go:152-153`, `:362`  
**우선순위**: P0

```go
case "batch":
    return nil, batchCmd(...)  // batchCmd 성공 시 nil 반환
```

`Execute()`에서 `printer.Print(resp, category)`가 `resp`가 nil일 때 `resp.Success`를 역참조합니다. 이는 `batch` 성공 시 패닉을 유발합니다.

**제안**:
```go
case "batch":
    err := batchCmd(ctx, subArgs, client.SendBatch, resolve)
    if err != nil { return nil, err }
    return &client.CommandResponse{Success: true}, nil
```

또는 `ResponsePrinter.Print`에서 nil guard 추가.

---

### 3.2 `humanCategories`에 도달 불가능한 항목 제거
**위치**: `cmd/root.go:60-72`  
**우선순위**: P2

`--help`, `-h`, `--version`, `-v`는 `Execute()`에서 미리攔截되므로 `humanCategories`에 포함될 이유가 없습니다.

---

## 4. 권장 적용 순서

| 순서 | 항목 | 이유 |
|------|------|------|
| 1 | `exec` default depth 1 + Unity Object shallow | 토큰 절약 효과 즉각적, 문서-코드 불일치 해소 |
| 2 | `console --lines` default 20 | 문서-코드 불일치, 콘솔 폭증 방지 |
| 3 | `batch` nil panic 수정 | 정확성 버그 |
| 4 | Go 응답 출력 round-trip 제거 | CPU/메모리 낭비 제거 |
| 5 | `batch` compact/quiet 모드 | 토큰 낭비 제거 |
| 6 | 컴파일러 캐시/탐색 최적화 | cold/warm compile 시간 단축 |
| 7 | HTTP response size cap + batch retry | 안정성/보안 |
| 8 | `ManageComponents` property dump 제한 | 대형 컴포넌트 토큰 폭증 방지 |

---

## 5. 측정 제안

각 최적화 적용 전후를 비교하기 위해 다음 벤치마크를 추가하면 좋습니다:

1. **cold exec compile time**: 도메인 리로드 후 첫 `exec "return null;"` 시간
2. **warm exec compile time**: 동일 snippet 두 번째 호출 시간
3. **serialization byte size**: `return GameObject.Find("X").transform;` 같은 반환 크기
4. **console response size**: `--lines` 기본값 적용 전후
5. **batch AI output size**: compact 모드 적용 전후

`scripts/benchmark.sh`에 hyperfine 시나리오를 추가해 지속적으로 측정할 수 있습니다.
