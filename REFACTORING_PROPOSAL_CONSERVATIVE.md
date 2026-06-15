# hera-agent-unity 리팩토링 제안 (보수적 버전)

> 기능 훼손 위험이 있는 항목은 제외하고, **기존 동작을 유지하면서 성능/토큰을 개선**할 수 있는 항목만 선별했습니다.  
> 각 항목 뒤에 **Risk**와 **Safety Guard**를 명시했습니다.

---

## ✅ 안전하게 적용 가능한 항목

### 1. `batch` 성공 시 nil pointer panic 수정
**파일**: `cmd/root.go:152-153`  
**Risk**: 없음 (버그 수정)  
**Safety Guard**: 기존 성공/실패 응답 형식 그대로 유지

```go
case "batch":
    err := batchCmd(ctx, subArgs, client.SendBatch, resolve)
    if err != nil { return nil, err }
    return &client.CommandResponse{Success: true}, nil
```

현재 `batchCmd` 성공 시 `nil`을 반환해 `Execute()`에서 `resp.Success` 역참조로 패닉이 발생합니다.

---

### 2. Go 응답 출력에서 불필요한 JSON round-trip 제거 (문자열 특수 처리 유지)
**파일**: `cmd/root.go:415-429`  
**Risk**: 낮음  
**Safety Guard**: plain string 응답에 대한 특수 처리를 그대로 유지

현재는 `resp.Data`를 `interface{}`로 unmarshal한 뒤 다시 marshal합니다. 이 과정에서 숫자 정밀도 손실과 키 순서 변경이 발생할 수 있습니다.

```go
if rp.shouldCompactJSON(category) {
    fmt.Println(string(resp.Data))
} else {
    // string 응답("hello")은 JSON이 아닌 raw string으로 출력하는 기존 동작 유지
    if len(resp.Data) >= 2 && resp.Data[0] == '"' && resp.Data[len(resp.Data)-1] == '"' {
        var s string
        if err := json.Unmarshal(resp.Data, &s); err == nil {
            fmt.Println(s)
            return
        }
    }
    var buf bytes.Buffer
    if err := json.Indent(&buf, resp.Data, "", "  "); err == nil {
        fmt.Println(buf.String())
    } else {
        fmt.Println(string(resp.Data))
    }
}
```

---

### 3. `batch` 명령에 `--compact` / `--quiet` 출력 모드 추가 (기본값 유지)
**파일**: `cmd/batch.go:76-82`  
**Risk**: 낮음  
**Safety Guard**: 기본 출력 형식은 그대로 두고, flag 전달 시에만 compact 동작

AI 소비를 위해 기존 기본 동작을 바꾸지 않고, `--compact-json` 또는 `--quiet`가 명시적으로 전달될 때만 JSON 형식으로 step별 결과를 출력합니다.

```go
compact := flagCompactJSON || !isHumanCommand("batch")
quiet := flagQuiet

for i, result := range resp.Results {
    if compact {
        b, _ := json.Marshal(result)
        fmt.Println(string(b))
        continue
    }
    // 기존 동작 유지
    status := "OK"
    if !result.Success { status = "FAIL" }
    if quiet {
        fmt.Printf("[%d/%d] %s: %s\n", i+1, len(resp.Results), status, result.Message)
    } else {
        fmt.Printf("%s %s %s\n", tui.Progress(i+1, len(resp.Results)), tui.StatusBadge(status), result.Message)
    }
}
```

---

### 4. C# 컴파일 메모리 캐시 크기 증가 (+ 테스트 상수 동기화)
**파일**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:24`, `cmd/exec_cache_integration_test.go:100`  
**Risk**: 낮음  
**Safety Guard**: 테스트의 `inMemoryCap` 상수도 함께 변경

```csharp
private const int MaxInMemoryAssemblies = 128; // 32 -> 128
```

> ⚠️ `TestExecCacheLRUEviction`에 하드코딩된 `const inMemoryCap = 32`도 함께 업데이트해야 합니다.

---

### 5. 컴파일러 서버 사전 예열 (백그라운드, 실패 무시)
**파일**: `AgentConnector/Editor/HttpServer.cs`  
**Risk**: 낮음  
**Safety Guard**: `try/catch` + `delayCall`로 메인 스레드 및 초기화 방해 없음

도메인 리로드 후 첫 `exec`는 VBCSCompiler를 다시 띄워야 해서 5~15초가 걸립니다. 커넥터 로드 시 백그라운드로 trivial compile을 한 번 수행하면 첫 실제 호출이 warm 상태가 됩니다.

```csharp
static HttpServer()
{
    EditorApplication.delayCall += () =>
    {
        try
        {
            ExecuteCsharp.PreWarmCompiler();
        }
        catch { /* ignore */ }
    };
}
```

`PreWarmCompiler`는 `return null;`을 `--no-cache`로 컴파일만 하고 결과를 버리는 정적 메서드입니다.

---

### 6. `asset-config.json`의 컴파일러 경로를 `exec`에서 우선 사용
**파일**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:138-158`  
**Risk**: 낮음  
**Safety Guard**: 파일이 존재하고 유효할 때만 사용, 그렇지 않으면 기존 탐색 로직 fallback

```csharp
public static string ResolveCsc(string overridePath)
{
    if (!string.IsNullOrEmpty(overridePath)) return overridePath;
    lock (Gate)
    {
        if (s_CscPath != null && File.Exists(s_CscPath)) return s_CscPath;
        var configPath = AssetConfigReader.GetDefaultCscPath(); // asset-config.json에서 읽기
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            s_CscPath = configPath;
            return s_CscPath;
        }
        s_CscPath = FindCsc();
        return s_CscPath;
    }
}
```

---

### 7. 컴파일러 탐색에서 known-path 먼저, recursive 탐색은 fallback
**파일**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:229-272`  
**Risk**: 낮음  
**Safety Guard**: known path 실패 시 기존 `SearchFile` 그대로 fallback

```csharp
private static string FindCsc()
{
    var content = EditorApplication.applicationContentsPath;
    var candidates = new[]
    {
        Path.Combine(content, "DotNetSdkRoslyn", "csc.dll"),
        Path.Combine(content, "MonoBleedingEdge", "lib", "mono", "4.5", "csc.exe"),
    };
    foreach (var c in candidates)
        if (File.Exists(c)) return c;
    return SearchFile(content, "csc.dll"); // 최후의 수단
}
```

---

### 8. Assembly refs enumeration 단축 (검증 조건 충족 시)
**파일**: `AgentConnector/Editor/Tools/ExecCompileCache.cs:101-136`  
**Risk**: 낮음 (검증 로직 추가 시)  
**Safety Guard**: refs 파일 존재 + 메타데이터(hash, assembly count) 일치 시에만 skip

```csharp
private static void EnsureRefs()
{
    lock (Gate)
    {
        if (s_RefLocations != null && s_RefRspPath != null && File.Exists(s_RefRspPath))
            return;

        // 검증 가능한 메타데이터 파일 활용
        var metaPath = Path.Combine(CacheDir, "refs-meta.json");
        if (TryLoadRefsMeta(metaPath, out var meta) &&
            File.Exists(meta.RspPath) &&
            meta.AssemblyCount == GetLoadedAssemblyCount())
        {
            s_RefLocations = meta.Locations;
            s_RefHash = meta.Hash;
            s_RefRspPath = meta.RspPath;
            return;
        }

        // 기존 enumeration 수행
        // ...
        // 완료 후 meta 파일 저장
        SaveRefsMeta(metaPath, s_RefHash, s_RefLocations, s_RefRspPath);
    }
}
```

---

### 9. `Serialize`에서 멤버 정보 캐싱
**파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs:780-801`  
**Risk**: 낮음  
**Safety Guard**: 도메인 리로드 시 캐시 무효화 (어셈블리 unload로 인한 Type identity 변경 방지)

```csharp
private static readonly Dictionary<Type, MemberInfo[]> s_MemberCache = new();

private static MemberInfo[] GetSerializableMembers(Type type)
{
    if (s_MemberCache.TryGetValue(type, out var members)) return members;
    var list = new List<MemberInfo>();
    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) list.Add(f);
    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) list.Add(p);
    members = list.ToArray();
    s_MemberCache[type] = members;
    return members;
}
```

> 캐시는 `ExecCompileCache.Invalidate()` 호출 시 함께 초기화되어야 합니다.

---

### 10. `SendBatch`에 domain-reload retry 추가
**파일**: `internal/client/client.go:455-506`  
**Risk**: 낮음  
**Safety Guard**: `doWithReloadRetry`와 동일한 로직 재사용

현재 `Send`는 domain reload 중 retry를 하지만 `SendBatch`는 하지 않습니다. 동일한 retry 루프를 적용합니다.

```go
func (c *Client) SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest) (*BatchCommandResponse, error) {
    // ... timeout 계산 ...
    body, _ := json.Marshal(req)
    resp, err := c.doWithReloadRetry(ctx, body, inst) // 또는 batch용 URL/엔드포인트 조정
    // ...
}
```

> 구현 시 `/commands` 엔드포인트를 사용하도록 `doWithReloadRetry`를 약간 일반화해야 합니다.

---

### 11. HTTP 응답 크기 제한 (명시적 에러)
**파일**: `internal/client/client.go:376-396`  
**Risk**: 낮음  
**Safety Guard**: 초과 시 명확한 에러 반환, silent truncation 없음

```go
const maxResponseSize = 50 * 1024 * 1024 // 50MB
respBody, err := io.ReadAll(io.LimitReader(resp.Body, maxResponseSize+1))
if len(respBody) > maxResponseSize {
    return nil, fmt.Errorf("response exceeded maximum size of %d bytes", maxResponseSize)
}
```

---

### 12. `SendBatch` 타임아웃이 명시적 `--timeout` 존중
**파일**: `internal/client/client.go:457-467`  
**Risk**: 낮음  
**Safety Guard**: `--timeout` 미지정 시 기존 공식 유지

```go
func (c *Client) SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest, timeoutMs int) (*BatchCommandResponse, error) {
    batchTimeout := 30 * time.Second
    if timeoutMs > 0 {
        batchTimeout = time.Duration(timeoutMs) * time.Millisecond
    } else if n := len(req.Commands); n > 0 {
        if calculated := time.Duration(n) * 15 * time.Second; calculated > batchTimeout {
            batchTimeout = calculated
        }
    }
    // ... cap ...
}
```

---

### 13. `humanCategories` 정리
**파일**: `cmd/root.go:60-72`  
**Risk**: 없음  
**Safety Guard**: `Execute()`에서 미리 처리되는 항목 제거

`--help`, `-h`, `--version`, `-v`는 이미 `Execute()` 초반에 처리되므로 `humanCategories`에서 제거합니다.

---

## ⚠️ 제외한 항목과 이유

| 제외 항목 | 제외 이유 |
|-----------|-----------|
| `exec` 기본 depth 3 → 1 | 기존 동작 변경. `[ToolParameter]`에도 "default 3"으로 명시되어 있어 사용자/스크립트가 의존할 수 있음. 문서와 코드의 불일치는 `AGENTS.md`를 3으로 수정하는 방향으로 해결하는 것이 더 보수적임. |
| Unity Object 항상 얕게 직렬화 | 의도적으로 `Component` 속성을 반환해 쓰는 사용자 코드가 있을 수 있음. AGENTS.md Rule 2는 이미 권장사항이므로 코드 강제는 기능 훼손 위험이 있음. |
| `IDictionary` 항목 제한 / 문자열 truncation | 정상적으로 큰 데이터를 반환하는 사용자 코드가 잘리는 기능 훼손. opt-in 파라미터(`--max-string`, `--max-dict`)로 추가하는 것은 가능하지만 기본값 변경은 위험. |
| `console --lines` 기본값 20 | 현재는 미지정 시 전체 로그를 반환하는 동작. 일부 워크플로우가 이에 의존할 수 있음. `--lines` 기본값 변경은 문서-코드 불일치라도 기능 훼손. |
| `ManageComponents` property dump 기본 제한 | 기본적으로 최상위 속성만 반환하도록 바꾸면 기존에 전체 속성을 기대하는 호출이 깨짐. `--depth` 파라미터 추가는 가능하나 기본값 변경은 위험. |
| 디스크 DLL 캐시 LRU 삭제 | 구현 버그로 필요한 DLL을 삭제하면 캐시 히트 실패로 컴파일 재수행. 기능은 복구되지만 성능 저하. 매우 보수적인 age threshold(예: 30일)와 size cap을 같이 적용할 때만 고려. |
| 응답 envelope `null` 필드 제거 | Go 클라이언트는 `omitempty`로 처리하지만, Unity HTTP API를 직접 호출하는 외부 소비자가 `data: null`을 기대할 수 있음. |
| HTTP `IdleConnTimeout` 30s → 5s | localhost 재연결 비용은 낮지만, 일부 환경에서 연결 재사용률 저하로 오히려 느려질 수 있음. 벤치마크 후 결정. |

---

## 권장 적용 순서

1. `batch` nil panic 수정
2. Go 응답 출력 round-trip 제거
3. `batch` compact/quiet 모드 추가
4. `humanCategories` 정리
5. 컴파일 캐시/탐색 최적화 (4~8번)
6. `Serialize` 멤버 캐싱
7. HTTP response size cap
8. `SendBatch` retry + timeout 개선
