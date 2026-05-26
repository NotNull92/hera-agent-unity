# Issue: install 후 IDE 통합 터미널에서 `hera-agent` 인식 실패 (Windows)

- **상태**: Mitigated (v0.0.6에서 install 위치를 WindowsApps로 이전)
- **심각도**: High (DX 차단 — 사용자가 "다 끄고 다시 켜기"를 강요받음)
- **발견일**: 2026-05-13
- **환경**: Windows 11, PowerShell 7+, JetBrains Rider (Unity External Editor로 연결)

---

## 증상

외부 PowerShell에서 `irm .../install.ps1 | iex`로 install 후, IDE(예: Rider)의 통합 PowerShell 터미널에서:

```
PS C:\path> hera-agent status
hera-agent: The term 'hera-agent' is not recognized as a name of a cmdlet,
function, script file, or executable program.
```

IDE를 닫았다 다시 열어도 동일. `[Environment]::GetEnvironmentVariable("Path","User")`는 hera-agent 경로를 정상 포함하지만, `$env:Path`(현재 세션 값)에는 빠져 있거나 stale 변형(예: 더블 백슬래시)으로 들어가 있음.

---

## 근본 원인

**Windows의 자식 프로세스 환경 블록 상속 구조 + IDE 실행 체인**.

자식 프로세스는 부모의 `STARTUPINFO`에 담긴 env block 스냅샷을 받는다. 부모가 시작된 이후 OS가 PATH를 갱신해도, 이미 살아 있는 부모 프로세스의 env block은 **OS API가 절대 갱신해주지 않는다**. `[Environment]::SetEnvironmentVariable(..., "User")`는 레지스트리만 갱신하고 `WM_SETTINGCHANGE` broadcast를 띄울 뿐, 이미 떠 있는 프로세스에는 효과 없음.

Unity + Rider 사용자의 실제 프로세스 체인:

```
explorer.exe
   └─ Unity Hub.exe                     ← 며칠 전 시작 (옛 PATH)
        └─ Unity.exe                    ← Unity Hub 자식 (옛 PATH 상속)
             └─ rider64.exe             ← Unity가 외부 에디터로 띄움 (옛 PATH 상속)
                  └─ pwsh.exe           ← Rider 통합 터미널 (옛 PATH 상속)
```

이 상태에서 Rider만 재시작해도 **Unity가 Rider를 다시 띄우므로 같은 옛 env block을 상속받는다**. PATH가 정상화되려면 Unity Hub부터 종료 후 재시작해야 하는데, 일반 워크플로상 매우 불편함.

---

## 이전에 시도한 해결책 (v0.0.4, v0.0.5)

1. **`\\` → `\` 더블 백슬래시 버그 수정** (`install.ps1`)
2. **PATH 마이그레이션**: 기존 stale 항목 제거 후 정규화된 경로 재등록
3. **안내 메시지 개선**: "Restart PowerShell" → "IDE 자체 재시작"

→ User PATH(레지스트리)는 깨끗해졌지만, **이미 떠 있는 IDE의 env block은 여전히 stale**. UX 차원에서 사용자가 Unity 작업 도중 모든 IDE를 종료하는 건 비현실적.

---

## 현재 해결책 (v0.0.6)

### 핵심 발상

PATH 등록 자체를 하지 않는다. Windows 10+이 **이미 사용자 PATH에 포함시키고 있는** 시스템 디렉토리에 바이너리를 놓는다.

```
%LOCALAPPDATA%\Microsoft\WindowsApps\
```

이 디렉토리의 특성:

- Windows 10+ 사용자 PATH에 **OS가 자동 포함** (Store 앱 alias 메커니즘용)
- 일반 사용자 쓰기 권한
- `winget`, `scoop`, `python.exe` shim 등 다수 도구가 같은 패턴 사용

`install.ps1`이 PATH 레지스트리를 건드리지 않으므로, **PATH broadcast 누락이라는 문제 자체가 사라진다**. 새로 시작하는 모든 프로세스(터미널·IDE)는 OS의 표준 PATH 해석 경로에서 자동으로 hera-agent를 찾는다.

### 남은 한계

이미 떠 있는 프로세스의 env block은 여전히 OS가 갱신해주지 않는다. 그러나 새 위치는 `\\hera-agent` 같은 stale 변형이 박힐 일이 없고, **사용자가 install 시점에 Unity Editor를 켜 두지 않은 새 IDE를 띄우면 그 즉시 동작**한다.

### 워크어라운드 (이미 막힌 세션용)

현재 세션의 PATH만 즉시 refresh:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable("Path","User") + ";" + [Environment]::GetEnvironmentVariable("Path","Machine")
```

`install.ps1` 성공 메시지에 이 한 줄을 안내한다.

---

## 변경 요약 (v0.0.6)

| 위치 | 변경 |
|---|---|
| `install.ps1` | installDir → `%LOCALAPPDATA%\Microsoft\WindowsApps`. PATH 등록 제거. legacy 디렉토리/PATH 자동 마이그레이션. 막힌 세션용 한 줄 패치 안내 추가. |
| `cmd/install.go` `getInstallPaths` | Windows 분기를 WindowsApps로 |
| `cmd/install.go` `addToPATHWindows` | PATH 추가 없음. legacy `%LOCALAPPDATA%\hera-agent` PATH 항목만 정리. |
| `cmd/install.go` `cleanupLegacyInstall` | 옛 위치의 바이너리·디렉토리 정리 (단, 자기 자신이 옛 위치에서 실행 중이면 skip) |
| `cmd/uninstall_windows.go` `removeBinaryAndDir` | WindowsApps 디렉토리는 보존, 그 안의 hera-agent.exe만 삭제. legacy 위치도 같이 정리. |
| `cmd/uninstall_windows.go` `removeFromPATH` | 실제 레지스트리 갱신 (이전엔 no-op). WindowsApps은 건드리지 않고 legacy PATH 항목만 제거. |
| Install 성공 메시지 | "IDE 재시작 필요" → "새 터미널 즉시 동작 / 막힌 세션은 한 줄 패치" |

---

## 영향 범위

- Windows 사용자의 모든 install/uninstall 경로
- Rider/VSCode/Cursor 같은 IDE를 Unity의 External Editor로 등록한 사용자 (가장 흔한 케이스)
- 기존 사용자: 다음 `install.ps1` 실행 시 자동 마이그레이션 (옛 디렉토리·PATH 항목 정리됨)

---

## 참고

- [Windows: PATH environment variable propagation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw)
- [`%LOCALAPPDATA%\Microsoft\WindowsApps` and App Execution Aliases](https://learn.microsoft.com/en-us/windows/uwp/launch-resume/web-to-app-linking)
- 관련 이슈: [`powershell-exec-escaping.md`](powershell-exec-escaping.md) — 같은 "PowerShell ↔ Go CLI" 카테고리의 별도 layer 문제 (인자 escaping)
