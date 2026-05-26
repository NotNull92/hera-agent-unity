# Issue: PowerShell에서 `hera-agent-unity exec` 인자 파싱 실패

- **상태**: Open
- **심각도**: Medium (워크어라운드 존재하나, DX 저해)
- **발견일**: 2026-05-11
- **환경**: Windows 11 Pro, PowerShell 7+, hera-agent-unity (Go 바이너리)

---

## 증상

PowerShell에서 `hera-agent-unity exec "C# code"` 형태로 실행하면 아래 에러 발생:

```
hera-agent-unity.exe: The command parameter was already specified.
```

CLI가 C# 코드 문자열을 **단일 인자**로 받지 못하고, PowerShell이 문자열을 분리/해석하여 **복수 인자**로 전달한다.

---

## 재현 조건

### 실패하는 명령어 (PowerShell)

```powershell
# 케이스 1: 세미콜론이 PowerShell 명령어 구분자로 해석됨
hera-agent-unity exec "var x = 1; return x.ToString();"

# 케이스 2: $를 PowerShell 변수 참조로 해석함
hera-agent-unity exec "return $\"Hello {name}\";"

# 케이스 3: 중괄호가 PowerShell ScriptBlock으로 해석됨
hera-agent-unity exec "var dict = new Dictionary<string, int>(){};  return dict.Count;"
```

### 성공하는 명령어 (Bash stdin pipe)

```bash
echo 'var x = 1; return x.ToString();' | hera-agent-unity exec
```

---

## 근본 원인 분석

### PowerShell의 특수 문자 처리

PowerShell은 더블 쿼트(`"`) 낭부에서도 다음 문자들을 **자체 문법으로 해석**한다:

| 문자 | PowerShell 해석 | C#에서의 용도 |
|------|-----------------|---------------|
| `;` | 명령어 구분자 (Statement separator) | 문장 종결자 |
| `$` | 변수 참조 (`$variable`) | 문자열 보간 (`$"..."`) |
| `{}` | ScriptBlock 리터럴 | 딕셔너리/람다/초기화 |
| `()` | 서브표현식 | 메서드 호출, 캐스팅 |
| `` ` `` | 이스케이프 문자 | 사용 빈도 낮음 |
| `@` | Splatting / Here-string | 어트리뷰트 |

### Go CLI 측 인자 처리

`hera-agent-unity exec`는 `os.Args` 또는 cobra/flag 라이브러리로 인자를 받는다. PowerShell이 문자열을 분리하여 넘기면 CLI는 이미 분리된 복수 인자를 받게 되어 `"The command parameter was already specified"` 에러를 출력한다.

**핵심**: 문제는 CLI가 아니라 **PowerShell → CLI 인자 전달 과정**에서 발생한다. 하지만 CLI가 이 상황을 감지하고 대응할 수 있다.

---

## 현재 워크어라운드

### 1. Bash stdin pipe (권장)

```bash
echo 'var enemy = UnityEngine.Object.FindFirstObjectByType<Enemy>(); if (enemy == null) return "not found"; return enemy.name;' | hera-agent-unity exec
```

- 싱글 쿼트로 감싸면 Bash가 내용을 해석하지 않음
- 단점: PowerShell 환경에서 Bash를 별도로 호출해야 함

### 2. PowerShell 싱글 쿼트 + 수동 이스케이프

```powershell
hera-agent-unity exec 'var x = 1; return x.ToString();'
```

- 싱글 쿼트는 PowerShell에서 리터럴 문자열이므로 세미콜론, 달러사인이 해석되지 않음
- **제한**: C# 코드 내에 싱글 쿼트(`'`)가 포함되면 이스케이프 필요 (`''`로 두 번)

### 3. PowerShell Here-String

```powershell
hera-agent-unity exec @'
var enemy = UnityEngine.Object.FindFirstObjectByType<Enemy>();
if (enemy == null) return "not found";
return $"Name: {enemy.name}";
|'@
```

- `@'...'@`는 PowerShell에서 완전한 리터럴 — 어떤 특수 문자도 해석하지 않음
- **제한**: here-string이 CLI의 positional argument로 올바르게 전달되는지 CLI 측 테스트 필요

---

## CLI 측 개선 제안

### 제안 1: `--stdin` 플래그 공식 지원 (우선순위: 높음)

현재 stdin pipe는 암묵적으로 동작하지만, 명시적 `--stdin` 플래그를 추가하면 사용자가 의도를 명확히 할 수 있다.

```go
// 의사코드
if stdinFlag || !terminal.IsTerminal(os.Stdin) {
    code, _ := io.ReadAll(os.Stdin)
    executeCode(string(code))
} else {
    code := args[0]  // positional argument
    executeCode(code)
}
```

사용 예:

```powershell
# PowerShell에서 파이프
'var x = 1; return x;' | hera-agent-unity exec --stdin

# 또는 파일에서 읽기
Get-Content script.csx | hera-agent-unity exec --stdin
```

### 제안 2: 복수 positional args 자동 병합 (우선순위: 중간)

PowerShell이 세미콜론 기준으로 인자를 분리해서 넘기더라도, CLI에서 `exec` 뒤의 모든 args를 하나로 합치면 문제가 완화된다.

```go
// 의사코드 — exec 명령어 핸들러
func execHandler(cmd *cobra.Command, args []string) {
    // 복수 인자가 들어오면 세미콜론으로 재결합
    code := strings.Join(args, "; ")
    executeCode(code)
}
```

- 장점: 기존 사용 패턴에서 에러 없이 동작
- 단점: 사용자가 의도적으로 복수 인자를 넘긴 경우와 구분 불가 (현재 exec는 단일 인자만 받으므로 충돌 없음)

### 제안 3: `--file` 플래그 (우선순위: 낮음)

복잡한 C# 코드를 파일로 저장한 뒤 실행:

```powershell
hera-agent-unity exec --file check_enemy.csx
```

```go
// 의사코드
if fileFlag != "" {
    code, _ := os.ReadFile(fileFlag)
    executeCode(string(code))
}
```

- 재사용 가능한 스크립트에 유용
- 단, 일회성 확인 작업에는 오버헤드

### 제안 4: PowerShell 전용 에러 메시지 개선 (우선순위: 낮음)

복수 인자가 감지되면 에러 메시지에 워크어라운드를 안내:

```
Error: Multiple arguments received for 'exec' command.
This often happens in PowerShell due to semicolons being parsed as statement separators.

Try one of:
  1. Use single quotes:  hera-agent-unity exec 'your code here;'
  2. Use stdin pipe:     echo 'your code' | hera-agent-unity exec
  3. Use here-string:    hera-agent-unity exec @'
                         your code here;
                         '@
```

---

## 영향 범위

- `hera-agent-unity exec` 명령어
- PowerShell 7+ 환경 (Windows 기본 셸)
- Claude Code CLI가 PowerShell을 통해 hera-agent-unity를 호출하는 모든 시나리오

---

## 참고

- [PowerShell 특수 문자 문서](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_special_characters)
- [PowerShell 인용 규칙](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules)
- Go `os.Args`는 셸이 분리한 토큰을 그대로 받으므로 셸 의존적
