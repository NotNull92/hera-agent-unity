# Error Logging Improvements

> 이 문서는 hera-agent-pro 프로젝트 전반의 **누락된 에러 로그 메시지**를 정리한 개선 가이드입니다.
> 총 24개 지점(22개 항목)이며, 우선순위(높음/중간/낮음)별로 분류되어 있습니다.
>
> **작업 규칙**: 같은 파일 내에서는 **아래→위** 순서로 수정하여 라인 번호가 밀리지 않도록 합니다.
>
> **상태**: 전체 24개 지점 모두 코드 수정 완료 ✅

---

## 🔴 높은 우선순위 (High Priority)

이 섹션의 항목은 **반드시 수정해야 합니다.** 예외를 침묵으로 처리하거나, 잠재적 NRE(null reference exception) 위험이 있어 정상 동작에 영향을 줄 수 있습니다.

---

### 1. `TestRunnerState.cs:33` — pending 파일 쓰기 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/TestRunner/TestRunnerState.cs`
- **라인**: 33
- **문제**: PlayMode 테스트 실행 시 domain reload를 대비한 pending 파일 쓰기가 실패필 경우 아무 로그 없이 실패함. 테스트가 영원히 멈출 수 있음.
- **현재 코드**:
  ```csharp
  Directory.CreateDirectory(RunTests.StatusDir);
  File.WriteAllText(PendingFilePath(port), JsonConvert.SerializeObject(pending));
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 테스트 대기 파일을 저장하지 못했어요. Domain reload 후 테스트 상태가 유실될 수 있습니다 (port: {port})
      Debug.LogError($"[Hera] I couldn't save the test pending file. The test state might be lost after domain reload (port: {port}): {ex.Message}");
  }
  ```

---

### 2. `TestRunnerState.cs:43` — pending 파일 삭제 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/TestRunner/TestRunnerState.cs`
- **라인**: 43
- **문제**: PlayMode 테스트 완료 후 pending 파일 삭제 실패 시 로그 없음. 파일이 남아있으면 domain reload 후 잘못된 복원이 발생할 수 있음.
- **현재 코드**:
  ```csharp
  var path = PendingFilePath(port);
  if (File.Exists(path)) File.Delete(path);
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 테스트 대기 파일을 지우려 했지만 아직 남아있어요. 다음 reload 때 오작동할 수 있습니다 (port: {port})
      Debug.LogWarning($"[Hera] I tried to clean up the test pending file, but it's still lingering (port: {port}): {ex.Message}");
  }
  ```

---

### 3. `TestRunnerState.cs:63` — domain reload 후 콜백 복원 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/TestRunner/TestRunnerState.cs`
- **라인**: 63
- **문제**: Domain reload 후 TestRunner 콜백 재등록 실패 시 아무 로그 없이 넘어감. PlayMode 테스트 결과를 영원히 기다리게 될 수 있음.
- **현재 코드**:
  ```csharp
  ReattachCallbacks(port, filter);
  }
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] Domain reload에서 깨어났지만 테스트 러너에 다시 연결하지 못했어요. 결과가 영영 돌아오지 않을 수 있습니다
      Debug.LogError($"[Hera] I woke up from domain reload but couldn't reconnect to the test runner. Results may never arrive: {ex.Message}");
  }
  ```

---

### 4. `ExecuteCsharp.cs:140` — 어셈블리 참조 추가 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 140
- **문제**: 동적 컴파일을 위한 어셈블리 참조 추가 중 예외 발생 시 무시됨. 특정 어셈블리가 누락되면 컴파일 에러가 발생하지만, 어떤 어셈블리가 실패했는지 알 수 없음.
- **현재 코드**:
  ```csharp
  if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
  if (!added.Add(asm.GetName().Name)) continue;
  rsp.AppendLine($"-r:\"{asm.Location}\"");
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 어셈블리 '{asm.GetName().Name}'을 참조하지 못했어요. 이 어셈블리에 의존하는 코드는 컴파일에 실패할 수 있습니다
      Debug.LogWarning($"[Hera] I couldn't reference assembly '{asm.GetName().Name}'. Your code may fail to compile if it depends on this: {ex.Message}");
  }
  ```

---

### 5. `ExecuteCsharp.cs:185` — Process.Start() null 반환 (NRE 방지) [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 184~186
- **문제**: `Process.Start()`가 null을 반환할 수 있으나, null 체크가 없어 `proc.StandardOutput.ReadToEnd()`에서 NRE가 발생할 수 있음.
- **현재 코드**:
  ```csharp
  using (var proc = Process.Start(psi))
  {
      var stdout = proc.StandardOutput.ReadToEnd();
      var stderr = proc.StandardError.ReadToEnd();
  ```
- **변경 제안**:
  ```csharp
  using (var proc = Process.Start(psi))
  {
      if (proc == null)
          // ko: [Hera] 컴파일러 프로세스를 시작하지 못했어요. 무언가가 실행을 막고 있습니다
          return new ErrorResponse($"[Hera] I failed to launch the compiler process. Something is blocking execution: {exe} {args}");
  ```

---

### 6. `ExecuteCsharp.cs:191` — 타임아웃 프로세스 킬 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 191
- **문제**: 컴파일 타임아웃 시 프로세스 종료가 실패필 경우 로그 없음. 좀비 프로세스가 남아있을 수 있음.
- **현재 코드**:
  ```csharp
  if (!proc.WaitForExit(30000))
  {
      try { proc.Kill(); } catch { }
      return new ErrorResponse("Compilation timed out (30s). The compiler process was killed.");
  ```
- **변경 제안**:
  ```csharp
  try { proc.Kill(); }
  catch (Exception ex)
  {
      // ko: [Hera] 컴파일러가 시간 초과됐는데 종료시키지도 못했어요. 좀비 프로세스가 남아있을 수 있습니다
      Debug.LogWarning($"[Hera] The compiler timed out and I couldn't terminate it. A zombie process may remain: {ex.Message}");
  }
  ```

---

## 🟡 중간 우선순위 (Medium Priority)

이 섹션의 항목은 **추가 권장**입니다. 기능적으로는 문제가 없지만, 디버깅 시 원인 파악에 도움이 됩니다.

---

### 7. `ExecuteCsharp.cs:101` — 개별 임시 파일 삭제 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 96~101
- **문제**: 24시간 이전 임시 파일 삭제 실패 시 ���별 파일마다 로그 없음.
- **현재 코드**:
  ```csharp
  try
  {
      if (File.GetLastWriteTime(file) < cutoff)
          File.Delete(file);
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 오래된 임시 파일을 발견했지만 삭제하지 못했어요 ({file})
      Debug.LogWarning($"[Hera] I noticed an old temp file but couldn't remove it ({file}): {ex.Message}");
  }
  ```

---

### 8. `ExecuteCsharp.cs:104` — CleanupOldTempFiles 전체 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 102~104
- **문제**: 임시 디렉토리 접근 자체가 실패할 경우 로그 없음.
- **현재 코드** (항목 7의 내부 catch 바로 아래, 외부 catch):
  ```csharp
          catch { }   // ← 항목 7 (개별 파일)
      }
  }
  catch { }           // ← 이 항목 (전체 실패)
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 임시 디렉토리를 정리하다가 문제가 생겼어요. 찌꺼기 파일이 남아있을 수 있습니다
      Debug.LogWarning($"[Hera] I ran into trouble while cleaning up the temp directory. Some stale files may remain: {ex.Message}");
  }
  ```

---

### 9. `ExecuteCsharp.cs:368` — reflection 필드 직렬화 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 367~368
- **문제**: `exec` 결과의 객체 필드를 JSON으로 직렬화할 때 예외 발생 시 `"<error>"`로 대체되지만, 어떤 필드가 실패했는지 알 수 없음.
- **현재 코드**:
  ```csharp
  foreach (var f in fields)
  {
      try { r[f.Name] = Serialize(f.GetValue(obj), depth + 1); }
      catch { r[f.Name] = "<error>"; }
  }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 결과 객체의 필드 '{f.Name}'을 직렬화하지 못했어요. <error>로 표시됩니다
      Debug.LogWarning($"[Hera] I couldn't serialize field '{f.Name}' from the result object. It will show as <error>: {ex.Message}");
      r[f.Name] = "<error>";
  }
  ```

---

### 10. `ReadConsole.cs:130` — EndGettingEntries 호출 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ReadConsole.cs`
- **라인**: 128~131
- **문제**: Unity 콘솔 엔트리 접근 종료 시 예외가 발생할 경우 로그 없음. 메모리 누수 또는 Unity 에디터 상태 불일치 가능성.
- **현재 코드**:
  ```csharp
  finally
  {
      try { _endGettingEntriesMethod.Invoke(null, null); } catch { }
  }
  ```
- **변경 제안**:
  ```csharp
  try { _endGettingEntriesMethod.Invoke(null, null); }
  catch (Exception ex)
  {
      // ko: [Hera] 콘솔 로그를 다 읽었는데 내부 핸들을 제대로 해제하지 못했어요
      Debug.LogWarning($"[Hera] I finished reading console entries but couldn't release the internal handle properly: {ex.Message}");
  }
  ```

---

### 11. `ToolMetadata.cs:114` — enum 값 로딩 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Core/ToolMetadata.cs`
- **라인**: 108~115
- **문제**: `[ToolParameter]`의 `EnumType`으로 지정된 enum을 로딩할 때 실패하면 스키마에 `enum` 항목이 없지만, 왜 없는지 알 수 없음.
- **현재 코드**:
  ```csharp
  try
  {
      var enumType = assembly.GetType(enumName);
      if (enumType != null && enumType.IsEnum)
          return System.Enum.GetNames(enumType).ToList();
  }
  catch { }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] '{enumName}'의 enum 값을 불러오지 못했어요. 스키마에서 허용 값 목록이 빠지게 됩니다
      Debug.LogWarning($"[Hera] I couldn't load enum values for '{enumName}'. The schema will be missing allowed values: {ex.Message}");
  }
  ```

---

### 12. `ParamCoercion.cs:26` — bool 강제 변환 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Core/ParamCoercion.cs`
- **라인**: 21~27
- **문제**: 파라미터를 bool로 강제 변환할 수 없는 경우 `null`을 반환하지만, 어떤 값이 실패했는지 알 수 없음.
- **현재 코드**:
  ```csharp
  if (s.Length == 0) return null;
  if (bool.TryParse(s, out var b)) return b;
  if (s == "1" || s == "yes" || s == "on") return true;
  if (s == "0" || s == "no" || s == "off") return false;
  }
  catch { }
  return null;
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] '{token}'을 받았는데 bool로 해석할 수가 없었어요
      Debug.LogWarning($"[Hera] I received '{token}' but couldn't interpret it as a boolean value: {ex.Message}");
  }
  ```

---

### 13. `DetectAssets.cs:86` — asset-config.json 파싱 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/DetectAssets.cs`
- **라인**: 82~89
- **문제**: 기존 asset-config.json 파일이 손상되었을 때 새로 생성하지만, 기존 파일이 손상된 이유를 알 수 없음.
- **현재 코드**:
  ```csharp
  try
  {
      config = JObject.Parse(File.ReadAllText(configPath));
  }
  catch
  {
      config = null;
  }
  ```
- **변경 제안**:
  ```csharp
  catch (Exception ex)
  {
      // ko: [Hera] 기존 asset-config.json이 손상된 것 같아요. 새로 만들어 드릴게요
      Debug.LogWarning($"[Hera] The existing asset-config.json appears corrupted. I'll create a fresh one: {ex.Message}");
      config = null;
  }
  ```

---

## 🟢 낮은 우선순위 (Low Priority)

이 섹션의 항목은 **cleanup 관련**으로, 기능적으로는 문제가 없으나 완전성을 위해 추가할 수 있습니다.

---

### 14~16. `ExecuteCsharp.cs:254~256` — 임시 파일 삭제 실패 (src, out, rsp) [✅ 완료]

- **파일**: `AgentConnector/Editor/Tools/ExecuteCsharp.cs`
- **라인**: 252~257
- **문제**: 컴파일 후 임시 소스/출력/응답 파일 삭제 실패 시 로그 없음.
- **현재 코드**:
  ```csharp
  finally
  {
      try { File.Delete(srcFile); } catch { }
      try { File.Delete(outFile); } catch { }
      try { File.Delete(rspFile); } catch { }
  }
  ```
- **변경 제안**:
  ```csharp
  finally
  {
      try { File.Delete(srcFile); }
      catch (Exception ex)
      {
          // ko: [Hera] 코드는 컴파일했는데 소스 파일을 치우지 못했어요 ({srcFile})
          Debug.LogWarning($"[Hera] I compiled your code but couldn't clean up the source file ({srcFile}): {ex.Message}");
      }
      try { File.Delete(outFile); }
      catch (Exception ex)
      {
          // ko: [Hera] 컴파일은 끝났는데 출력 파일을 지우지 못했어요 ({outFile})
          Debug.LogWarning($"[Hera] Compilation is done but I couldn't remove the output file ({outFile}): {ex.Message}");
      }
      try { File.Delete(rspFile); }
      catch (Exception ex)
      {
          // ko: [Hera] 컴파일은 끝났는데 응답 파일을 지우지 못했어요 ({rspFile})
          Debug.LogWarning($"[Hera] Compilation is done but I couldn't remove the response file ({rspFile}): {ex.Message}");
      }
  }
  ```

---

### 17. `HttpServer.cs:155~156` — RepaintAllViews 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/HttpServer.cs`
- **라인**: 153~156
- **문제**: CLI 요청 처리 중 에디터 뷰 강제 갱신 실패 시 로그 없음.
- **현재 코드**:
  ```csharp
  static void ForceEditorUpdate()
  {
      try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
      catch { }
  }
  ```
- **변경 제안**:
  ```csharp
  try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
  catch (Exception ex)
  {
      // ko: [Hera] 에디터 화면을 갱신하려 했는데 Unity가 협조를 안 해주네요
      Debug.LogWarning($"[Hera] I tried to refresh the editor views but Unity didn't cooperate: {ex.Message}");
  }
  ```

---

### 18. `RunTests.cs:83` — 이전 결과 파일 삭제 실패 [✅ 완료]

- **파일**: `AgentConnector/Editor/TestRunner/RunTests.cs`
- **라인**: 81~84
- **문제**: 새 테스트 실행 전 이전 결과 파일 삭제 실패 시 로그 없음.
- **현재 코드**:
  ```csharp
  var port = HttpServer.Port;

  try { var f = ResultsFilePath(port); if (File.Exists(f)) File.Delete(f); } catch { }
  TestRunnerState.MarkPending(port, filter);
  ```
- **변경 제안**:
  ```csharp
  try { var f = ResultsFilePath(port); if (File.Exists(f)) File.Delete(f); }
  catch (Exception ex)
  {
      // ko: [Hera] 깨끗하게 시작하고 싶었는데 이전 테스트 결과 파일을 지우지 못했어요
      Debug.LogWarning($"[Hera] I wanted a clean slate but couldn't delete the previous test results file: {ex.Message}");
  }
  ```

---

## Go CLI — 누락된 에러 메시지

Go CLI 측에서는 에러를 반환하지만, **stderr에 로그로 출력하지 않고 조용히 넘어가는** 경우가 있습니다.

---

### 19. `client.go:68` — 인스턴스 파일 읽기 실패 [✅ 완료]

- **파일**: `internal/client/client.go`
- **라인**: 66~70
- **문제**: 인스턴스 JSON 파일 읽기 실패 시 그냥 `continue`로 넘어감. 파일이 손상되었거나 권한 문제가 있어도 사용자는 알 수 없음.
- **현재 코드**:
  ```go
  fp := filepath.Join(dir, e.Name())
  data, err := os.ReadFile(fp)
  if err != nil {
      continue
  }
  ```
- **변경 제안**:
  ```go
  if err != nil {
      // ko: [Hera] 인스턴스 파일을 찾았는데 읽을 수가 없었어요 (%s)
      log.Printf("[Hera] I found an instance file but couldn't read it (%s): %v", fp, err)
      continue
  }
  ```

---

### 20. `client.go:72` — JSON 파싱 실패 [✅ 완료]

- **파일**: `internal/client/client.go`
- **라인**: 71~74
- **문제**: 인스턴스 파일의 JSON 파싱 실패 시 `continue`로 넘어감. 파일이 손상되었음을 알 수 없음.
- **현재 코드**:
  ```go
  var inst Instance
  if err := json.Unmarshal(data, &inst); err != nil {
      continue
  }
  ```
- **변경 제안**:
  ```go
  if err := json.Unmarshal(data, &inst); err != nil {
      // ko: [Hera] 인스턴스 파일을 찾았는데 내용을 알아볼 수가 없어요 (%s)
      log.Printf("[Hera] I found an instance file but its contents don't make sense (%s): %v", fp, err)
      continue
  }
  ```

---

### 21. `assetconfig/config.go:124` — 첫 실행 저장 실패 [✅ 완료]

- **파일**: `internal/assetconfig/config.go`
- **라인**: 119~125
- **문제**: 첫 실행 시 기본 설정 파일 저장 실패를 무시함(`_ = Save(cfg)`). 설정이 저장되지 않아도 사용자는 알 수 없음.
- **현재 코드**:
  ```go
  // First run — create defaults and save
  cfg := &AssetConfig{
      Version: "1.0.0",
      Assets:  DefaultAssets(),
  }
  _ = Save(cfg)
  return cfg, nil
  ```
- **변경 제안**:
  ```go
  if err := Save(cfg); err != nil {
      // ko: [Hera] 기본 에셋 설정을 만들었는데 디스크에 저장하지 못했어요
      log.Printf("[Hera] I generated the default asset config but couldn't save it to disk: %v", err)
  }
  ```

---

### 22. `assetconfig/config.go:181` — ToggleAsset ID 미발견 [✅ 완료]

- **파일**: `internal/assetconfig/config.go`
- **라인**: 171~181 (`ToggleAsset` 함수 내부)
- **문제**: 에셋 ID를 찾지 못하면 `nil, nil`을 반환하여 호출자가 에러인지 모름.
- **현재 코드**:
  ```go
  for i := range cfg.Assets {
      if cfg.Assets[i].ID == id {
          cfg.Assets[i].Enabled = !cfg.Assets[i].Enabled
          if err := Save(cfg); err != nil {
              return nil, err
          }
          return cfg, nil
      }
  }

  return nil, nil
  ```
- **변경 제안**:
  ```go
  // ko: [Hera] 에셋 '%s'을 토글하려고 찾아봤는데 설정에 존재하지 않아요
  return nil, fmt.Errorf("[Hera] I looked for asset '%s' to toggle, but it doesn't exist in the config", id)
  ```

---

### 23. `assetconfig/config.go:201` — SetAssetEnabled ID 미발견 [✅ 완료]

- **파일**: `internal/assetconfig/config.go`
- **라인**: 191~201 (`SetAssetEnabled` 함수 내부)
- **문제**: 에셋 ID를 찾지 못하면 `nil, nil`을 반환하여 호출자가 에러인지 모름.
- **현재 코드**:
  ```go
  for i := range cfg.Assets {
      if cfg.Assets[i].ID == id {
          cfg.Assets[i].Enabled = enabled
          if err := Save(cfg); err != nil {
              return nil, err
          }
          return cfg, nil
      }
  }

  return nil, nil
  ```
- **변경 제안**:
  ```go
  // ko: [Hera] 에셋 '%s'의 활성화 상태를 변경하려고 찾아봤는데 설정에 존재하지 않아요
  return nil, fmt.Errorf("[Hera] I looked for asset '%s' to update its enabled state, but it doesn't exist in the config", id)
  ```

---

### 24. `assetconfig/config.go:221` — SetAssetInstalled ID 미발견 [✅ 완료]

- **파일**: `internal/assetconfig/config.go`
- **라인**: 211~221 (`SetAssetInstalled` 함수 내부)
- **문제**: 에셋 ID를 찾지 못하면 `nil, nil`을 반환하여 호출자가 에러인지 모름.
- **현재 코드**:
  ```go
  for i := range cfg.Assets {
      if cfg.Assets[i].ID == id {
          cfg.Assets[i].Installed = installed
          if err := Save(cfg); err != nil {
              return nil, err
          }
          return cfg, nil
      }
  }

  return nil, nil
  ```
- **변경 제안**:
  ```go
  // ko: [Hera] 에셋 '%s'을 설치 완료로 표시하려고 찾아봤는데 설정에 존재하지 않아요
  return nil, fmt.Errorf("[Hera] I looked for asset '%s' to mark as installed, but it doesn't exist in the config", id)
  ```

---

## 요약 (Summary)

| 우선순위 | C# | Go | 총계 |
|:---|:---:|:---:|:---:|
| 🔴 높음 (High) | 6 | 0 | 6 |
| 🟡 중간 (Medium) | 7 | 0 | 7 |
| 🟢 낮음 (Low) | 5 | 6 | 11 |
| **합계** | **18** | **6** | **24** |

### 핵심 권장사항

1. **`TestRunnerState.cs`** — 3개 지점 모두 `Debug.LogError` 추가. PlayMode 테스트가 domain reload 후 영원히 멈추는 버그 방지.
2. **`ExecuteCsharp.cs:185`** — `Process.Start()` null 체크 추가. NRE로 인한 에디터 크래시 방지.
3. **`ExecuteCsharp.cs:140`** — 어셈블리 참조 추가 실패 로그. 컴파일 에러 원인 파악에 필수.
4. **`assetconfig/config.go`** — `return nil, nil`을 `fmt.Errorf`로 변경. 호출자가 에러를 인식할 수 있게 함.
5. **`client.go`** — 인스턴스 파일 읽기/파싱 실패 시 `log.Printf` 추가. 손상된 인스턴스 파일 디버깅에 도움.

---

*이 문서는 버전 관리에 포함되어야 하며, 관련 수정이 완료되면 이 문서도 함께 업데이트해야 합니다.*
