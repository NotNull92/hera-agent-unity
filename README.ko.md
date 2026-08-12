<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![GitHub stars](https://img.shields.io/github/stars/NotNull92/hera-agent-unity?style=flat-square&logo=github&label=stars&color=181717)](https://github.com/NotNull92/hera-agent-unity/stargazers)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.25-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-6000.0%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

# hera-agent-unity

**AI 코딩 에이전트에게 Unity 안에서 움직이는 손, 결과를 보는 눈, 작업을 확인하는 체크리스트를 줍니다.**

<sub>Codex, Claude, Cursor, Copilot, AntiGravity 같은 AI가 실제 Unity Editor를 확인하고, 수정하고, 실행하고, 테스트하고, 결과를 다시 본 뒤 근거가 맞을 때까지 고치게 합니다.</sub>

<br>

[3단계로 시작](#어떻게-사용하나요) · [무엇을 할 수 있나요?](#실제로-무엇을-할-수-있나요) · [왜 Hera인가요?](#왜-hera를-써야-하나요) · [얼마나 좋은가요?](#얼마나-좋은가요) · [활용 예시](#어떻게-응용할-수-있나요) · [명령 한눈에 보기](#명령-한눈에-보기)

[English](README.md) · **한국어**

</div>

---

## Hera를 1분 만에 이해하기

Hera 없이도 AI는 Unity C# 코드를 작성할 수 있습니다. 하지만 코드를 쓴 다음 **실제 Unity Editor가 어떤 상태인지**는 스스로 확실히 알기 어렵습니다.

보통 작업은 이렇게 됩니다:

```text
사람이 AI에게 요청
   ↓
AI가 코드 작성
   ↓
사람이 Unity로 이동해서 컴파일 대기
   ↓
에러를 복사해서 AI에게 전달
   ↓
AI가 수정
   ↓
사람이 Play 버튼을 누르고 결과 설명
   ↓
반복...
```

Hera를 붙이면 이렇게 바뀝니다:

```text
사람이 AI에게 요청
   ↓
AI가 Hera 사용
   ↓
Unity에서 컴파일, 실행, 테스트, UI 입력, 화면 확인
   ↓
Hera가 실제 결과를 AI에게 전달
   ↓
AI가 실패 원인을 고치고 다시 확인
   ↓
검증된 결과
```

가장 쉽게 표현하면 이렇습니다.

> **AI가 두뇌라면 Hera는 Unity 안에서 움직이는 손이고, 결과를 보는 눈이며, 아직 망가진 상태에서 "완료"라고 말하지 않게 해 주는 체크리스트입니다.**

Hera가 Unity를 대신하는 것도 아니고 AI를 대신하는 것도 아닙니다. 둘 사이를 연결해서 AI가 소스 코드만 보고 추측하지 않고 **지금 내 프로젝트의 실제 상태**를 보고 일하게 해 줍니다.

---

## 이게 왜 필요한가요?

Unity 개발의 중요한 피드백은 코드 파일 밖에 있습니다.

코드만 보면 맞는 것 같아도 실제로는 이런 문제가 생길 수 있습니다.

- Unity가 컴파일하지 못했습니다.
- 엉뚱한 Scene이 열려 있습니다.
- 필요한 GameObject나 Component가 없습니다.
- Inspector의 직렬화 값이 예상과 다릅니다.
- 내가 쓰는 Unity 버전에서 API가 달라졌습니다.
- Console에 예외가 떠 있습니다.
- Edit Mode에서는 괜찮지만 Play Mode에서 깨집니다.
- 버튼은 보이지만 클릭을 받지 못합니다.
- UI는 구조상 정상인데 실제 화면은 이상합니다.
- AI가 아무것도 확인하지 않고 "완료했습니다"라고 말합니다.

Hera를 쓰면 AI가 실제 Unity에게 직접 물어봅니다.

```bash
hera-agent-unity status
hera-agent-unity console --type error
hera-agent-unity scene info
hera-agent-unity editor play --wait
hera-agent-unity test --mode PlayMode
```

선택한 Editor가 실행 중이 아니면 Connector 탐색 전에 정확한 프로젝트를 시작할
수 있습니다.

```bash
hera-agent-unity --project C:/Projects/Game editor launch
hera-agent-unity --project C:/Projects/Game editor restart
```

프로젝트 버전은 `ProjectSettings/ProjectVersion.txt`에서 읽고, 해당 Unity Hub
설치는 `UNITY_HUB_EDITOR` 또는 플랫폼 기본 경로(Windows는
`%ProgramFiles%\Unity\Hub\Editor`)에서 찾습니다. Hub 위치가 다르면
`--hub-root`를 사용합니다. 시작 시 Package Manager를 정상 사용하며 `-noUpm`을
전달하지 않습니다. 새 프로세스가 정확한 프로젝트 heartbeat를 게시하면
명령이 반환됩니다. Windows에서는 에이전트 셸이 표준 공용 프로필 환경 변수를
누락한 경우 Unity 자식 프로세스에 복원하여, 일반 데스크톱 실행과 같은 기준으로
UPM 경로 해석을 시작합니다.

중요한 것은 명령어 이름이 아닙니다. 핵심은 AI가 사람 대신 **확인 → 수정 → 실행 → 검증 → 재수정**을 반복할 수 있다는 점입니다.

---

## 실제로 무엇을 할 수 있나요?

한 줄짜리 상태 확인부터 꽤 긴 Unity 제작 작업까지 사용할 수 있습니다.

| AI에게 시키고 싶은 일 | Hera가 제공하는 것 |
|:---|:---|
| Unity가 정상인지 확인 | 실제 Editor 상태, 버전, 프로젝트, 컴파일 상태, Console 에러 확인 |
| 올바른 Editor 시작/재시작 | 프로젝트에 기록된 Unity 버전으로 정확한 프로젝트를 실행·재시작하고 해당 heartbeat까지 대기 |
| 현재 Scene 이해 | Scene 정보, GameObject 검색, Component와 Inspector 값 조회 |
| Scene 수정 | GameObject 생성, 복제, 이름 변경, 부모 변경, 이동, 삭제 |
| Component 편집 | Component 추가, 제거, 조회, 직렬화 값 수정 |
| 프로젝트 에셋 관리 | `Assets/` 아래에서 찾기, 생성, 복사, 이동, 삭제 |
| 프로젝트용 C# 실행 | 현재 열린 프로젝트의 Unity API와 Assembly를 사용해 Editor 안에서 C# 실행 |
| 애니메이션 제작 | AnimationClip과 AnimatorController 상태머신 저작 |
| 기능 테스트 | EditMode/PlayMode 테스트 실행, Domain Reload를 넘어 결과 추적 |
| 게임 실행 | 실제 Play Mode 진입을 기다리고 상태를 확인한 뒤 Stop |
| Unity가 그린 화면 확인 | Scene/Game View나 단일 오브젝트 캡처, 제한된 uGUI 식별자/좌표와 Camera.main 기준 3D physics 근거 수집 |
| Unity 입력 검증 | uGUI raycast 검증 또는 Play Mode Input System 키보드/마우스 sequence 합성, 녹화와 replay |
| UI 제작 | uGUI 레이아웃 제작과 결과 검증 |
| 참고 이미지로 UI 재현 | 색과 레이아웃 측정 → Unity UI 생성 → 캡처 → 비교 → 반복 수정 |
| 게임 감각 개선 | shake, hit stop, 카메라, 사운드, 보상, 접근성 레시피 제공 |
| 생성형 티가 나는 UI 정리 | 장식, 계층, 간격, 타이포, 색상 문제와 수정법 제공 |
| 우리 프로젝트 전용 명령 만들기 | `[HeraTool]`로 프로젝트 전용 작업을 자동 발견 |
| Editor 여러 개 다루기 | 프로젝트를 명확히 고르고 포트가 바뀌어도 같은 프로젝트를 추적 |
| 위험한 작업 승인받기 | 파괴적인 작업을 먼저 검토하고 해당 요청에 묶인 승인 토큰으로만 진행 |

즉 Hera는 단순한 "Play 버튼 리모컨"이 아니라, **Unity를 수정하고 다시 확인하는 전체 반복 작업**을 AI에게 열어 주는 도구입니다.

---

## 왜 Hera를 써야 하나요?

### 1. AI가 자기 작업을 직접 확인할 수 있습니다

Editor를 볼 수 없는 AI는 자주 이렇게 끝냅니다.

> "아마 잘 작동할 겁니다."

Hera를 쓰면 이런 근거로 끝낼 수 있습니다.

```text
컴파일: 통과
Console 에러: 0
EditMode 테스트: 18/18
PlayMode 테스트: 6/6
버튼 입력: EventSystem 경로 확인
최종 Game View: 캡처 완료
```

이 차이가 Hera를 만든 가장 중요한 이유입니다.

### 2. 사람이 복사-붙여넣기 중계기가 되지 않아도 됩니다

매번 다음 일을 반복하지 않아도 됩니다.

1. AI가 만든 코드를 복사합니다.
2. Unity로 이동합니다.
3. 컴파일을 기다립니다.
4. 에러를 다시 복사합니다.
5. Scene 계층을 설명합니다.
6. Play를 누릅니다.
7. 결과를 말로 설명합니다.

이 반복의 상당 부분을 AI가 직접 수행할 수 있습니다.

### 3. 지금 쓰는 AI 도구를 그대로 사용할 수 있습니다

제품 기본 경로는 CLI입니다. 셸 명령을 실행할 수 있는 AI라면 사용할 수 있습니다.

- Codex
- Claude Code
- Cursor
- GitHub Copilot
- AntiGravity
- 스크립트와 CI
- 직접 만든 자동화

Python 서버가 필요하지 않습니다. MCP도 필수가 아니라 선택 기능입니다.

### 4. 사람보다 AI가 읽기 좋은 출력에 신경 씁니다

도구 응답이 커지면 AI가 다음 판단을 위해 읽어야 할 입력도 커집니다. 그래서 Hera는 ID만 필요한 검색, 필요한 Tool 하나의 schema만 조회하는 방식처럼 작은 출력 경로를 제공합니다.

목표는 단순합니다. **다음 결정을 내리는 데 필요한 Unity 정보만 작게 전달합니다.**

### 5. "요청을 보냈다"와 "작업이 끝났다"를 구분합니다

Unity는 스크립트를 다시 컴파일하고, Domain Reload를 하고, 포트를 바꾸고, Play Mode에 들어가고, 테스트를 실행하면서 연결이 잠깐 끊길 수 있습니다.

Hera는 HTTP 요청이 성공했다는 이유만으로 Unity 작업이 완료됐다고 간주하지 않습니다.

### 6. 프로젝트가 커지면 Hera도 우리 도구가 될 수 있습니다

처음에는 기본 명령만 쓰면 됩니다. 나중에는 매일 반복하는 프로젝트 작업을 `[HeraTool]`로 만들 수 있습니다.

예를 들어 던전 테스트 룸 생성, 퀘스트 그래프 검사, 테이블 빌드, 전투 fixture 생성, 에셋 규칙 검사 같은 일을 프로젝트 전용 명령으로 만들 수 있습니다.

---

## 얼마나 좋은가요?

Hera는 "AI니까 알아서 잘합니다" 같은 모호한 표현 대신 저장소에 남아 있는 실측과 호환성 근거를 사용합니다.

### 자주 읽는 정보는 작게 보냅니다

`list --compact`의 보존된 저토큰 측정은 테스트한 Unity 버전들에서 **약 93 토큰 추정치**입니다. `find_gameobjects --ids`는 **약 49~55 토큰 추정치**를 기록했습니다.

| Unity Editor | `list --compact` | `find_gameobjects --ids` |
|:---|---:|---:|
| 2022.3.62f2 | **93 T** | **54 T** |
| 2023.2.22f1 | **93 T** | **54 T** |
| 6000.3.5f2 | **93 T** | **49 T** |
| 6000.5.0f1 | **93 T** | **55 T** |

`T`는 Hera CLI 출력의 UTF-8 바이트를 `ceil(bytes / 4)`로 계산한 단순 추정치입니다. AI 서비스의 실제 과금 토큰을 뜻하지 않습니다. 측정 방법: [token-reduction benchmark](docs/benchmarks/token-reduction/README.md).

### 여러 Unity 세대에서 패키지 컴파일을 확인합니다

지원 Unity 버전은 Unity 6+ (최소 `6000.0`)이며, 컴파일러/API 경계 기준 세 개의 호환 bucket으로 나눕니다. 릴리스 컴파일 게이트는 bucket별 대표 Editor에서 Connector의 동일 소스를 실행합니다.

| Bucket | 대표 Editor | 최근 전체 게이트 결과 |
|:---|:---|:---:|
| 6000.0 - 6000.2 | 6000.0.35f1 | PASS |
| 6000.3 - 6000.4 | 6000.3.5f2 | PASS |
| 6000.5+ | 6000.5.6f1 | PASS |

CLI 버전과 Connector 버전은 의도적으로 따로 관리합니다.

### v0.2.0은 컴파일만 본 것이 아니라 실제 Editor에서도 돌렸습니다

최종 릴리스 후보인 **Connector 0.0.86**을 Unity **6000.5.6f1**, Input System **1.20.0** 환경에 실제로 로드하고 Play Mode에서 직접 회귀 검증했습니다. 남긴 결과는 다음과 같습니다.

- live catalog **31 tools / 80 actions**;
- Play Mode 키보드 down/up과 마우스 위치 합성 성공;
- bounded input sequence 정상 완료;
- 입력 **5 events / 588 bytes** 녹화 후 같은 파일을 연속 두 번 replay 성공;
- replay 종료 뒤 Hera가 잡고 있는 control **0개**;
- Connector `ReleaseGateTests` **18/18 PASS**;
- 최종 Unity Console error **0건**;
- Editor 정상 종료 후 새 Scene Recovery backup **0개**, disposable fixture의 manifest/lock도 원복 확인.

즉 5개 Unity 버전의 컴파일 호환성뿐 아니라, 이번 릴리스에서 추가한 실제 기능 묶음도 Editor 안에서 끝까지 확인했습니다.

### 실제 작은 게임 제작 실험도 끝까지 통과했습니다

보존된 Crystal Forge 실험에서는 AI에게 코드와 테스트 작성, UI 생성, 컴파일, Unity EventSystem 입력, 테스트, 화면 캡처, 마지막 깨끗한 Editor 상태까지 요구했습니다.

**최종 결과: 수정 후 PASS. 첫 시도: FAIL.**

측정된 실행 구간은 **15분 52초**였습니다. 이 실험에서 중요한 점은 실패를 숨기지 않았다는 것입니다. 내부 상태는 맞았지만 실제 UI 텍스트가 보이지 않는 상태가 있었고, AI가 다시 관찰하고 고친 뒤 화면까지 확인해야 최종 PASS가 됐습니다.

이 결과는 Hera를 쓰면 모든 작업이 첫 시도에 성공한다는 뜻이 아닙니다. AI가 몇 퍼센트 더 똑똑해진다는 주장도 아닙니다. 더 현실적인 의미는 이것입니다.

> **Hera는 AI에게 실제 Editor의 피드백을 주기 때문에, 첫 번째 그럴듯한 답에서 멈추지 않고 통합 문제를 찾아 고치고 다시 확인하는 루프를 만들 수 있습니다.**

전체 기록: [Crystal Forge 실사용 benchmark](docs/benchmarks/user-scenario/crystal-forge-6000.3.5f2.md).

---

## 어떻게 사용하나요?

필요한 것은 두 개뿐입니다.

```text
내 컴퓨터                     내 Unity 프로젝트
─────────                     ───────────────
Hera CLI        <---------->  Hera Unity Connector
```

### 1단계. CLI 설치

가장 간단한 크로스플랫폼 방법은 npm입니다.

```bash
npm install --global hera-agent-unity
```

또는 운영체제용 설치 스크립트를 사용할 수 있습니다.

**Windows PowerShell**

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex"
```

**macOS / Linux**

```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | bash
```

확인:

```bash
hera-agent-unity version
```

<details>
<summary>다른 CLI 설치 방법</summary>

**Go install**

```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**수동 설치**

[GitHub Releases](https://github.com/NotNull92/hera-agent-unity/releases)에서 바이너리를 받은 뒤:

```bash
hera-agent-unity install
```

</details>

### 2단계. Unity 패키지 추가

Unity에서:

```text
Window -> Package Manager -> Add package from git URL
```

아래 주소를 넣습니다.

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

또는 `Packages/manifest.json`에 추가합니다.

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

Editor가 열리면 Connector가 자동으로 시작합니다.

특정 Connector 태그를 고정하려면:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-<version>"
```

현재 릴리스 Connector를 고정하려면:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-0.0.86
```

### 3단계. Unity를 열고 연결 확인

```bash
hera-agent-unity doctor --json
hera-agent-unity status
```

실제 프로젝트 경로, Unity 버전, Editor 상태, 연결 정보가 보이면 준비된 것입니다.

이제 AI에게 이렇게 말하면 됩니다.

```text
이 Unity 프로젝트에서는 hera-agent-unity를 사용해줘.
먼저 현재 Editor 상태를 확인해.
요청한 내용을 구현한 뒤 컴파일하고 실제 Console 에러를 읽어.
바꾼 오브젝트나 UI를 다시 확인하고,
Unity가 깨끗한 상태라는 근거가 있기 전에는 완료했다고 하지 마.
```

이게 Hera의 기본 사용 흐름입니다.

---

## 어떻게 응용할 수 있나요?

### 컴파일 또는 런타임 에러 자동 수정

```text
Hera를 사용해 Unity Console의 실제 에러를 읽고 원인을 고쳐줘.
다시 컴파일하고 에러가 없어질 때까지 반복해줘.
```

AI는 보통 이런 명령을 사용합니다.

```bash
hera-agent-unity console --type error --lines 20
hera-agent-unity editor refresh --compile
hera-agent-unity console --type error --lines 20
```

### 기능을 만들고 실제 동작까지 증명

```text
인벤토리 필터 기능을 구현해줘.
Hera로 컴파일하고 관련 EditMode/PlayMode 테스트를 돌려.
필요하면 Play Mode에도 들어가서 확인한 뒤 최종 근거를 보고해줘.
```

### 게임플레이 버그 재현

```text
올바른 Scene을 열고 Play Mode에서 관련 오브젝트를 확인해.
버그를 재현하고 수정한 뒤 같은 경로를 다시 실행해서 고쳐졌는지 증명해줘.
```

### 참고 이미지로 UI 제작

Hera는 AI에게 눈대중 루프가 아니라 측정 루프를 줄 수 있습니다.

```text
참고 이미지
   ↓ 색/레이아웃 측정
Unity UI 생성
   ↓ 캡처
비교
   ↓ 수정
다시 캡처
```

예시:

```bash
hera-agent-unity manage_ui create --element panel --name HUD
hera-agent-unity manage_ui set_anchor --path /Canvas/HUD --preset stretch --snap true
hera-agent-unity manage_components set --path /Canvas/HUD --property m_Color --value '#1A1A2E'
hera-agent-unity screenshot --overlay --output_path hud_built.png
```

`manage_ui`, `manage_components`, `screenshot --overlay`를 작은 검증 단위로 사용합니다.

### 화면 좌표를 찍지 않고 버튼 검증

```bash
hera-agent-unity input state
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
```

이 방식은 Unity EventSystem 경로를 검증합니다. 실제 Windows/macOS 마우스 클릭과는 다르므로 두 증거는 구분해서 보고합니다.

이미 선택적 Input System 패키지를 사용하는 프로젝트라면 Hera 의존성을 추가하지 않고 게임플레이 입력도 검증할 수 있습니다.

```bash
hera-agent-unity input state --backend inputsystem
hera-agent-unity input keyboard --key space --mode press
hera-agent-unity input mouse --mode click --button left --position 640,360
hera-agent-unity call input --json '{"action":"sequence","steps":[{"action":"keyboard","key":"space","mode":"down"},{"action":"keyboard","key":"space","mode":"up"}]}'
hera-agent-unity call input --json '{"action":"record","mode":"start"}'
hera-agent-unity call input --json '{"action":"record","mode":"stop"}'
hera-agent-unity call input --json '{"action":"replay","path":"Library/HeraAgent/Recordings/input.json"}'
```

키보드·마우스·bounded sequence·입력 녹화 capture·replay는 Play Mode에서 동작합니다. 녹화는 프로젝트 또는 시스템 임시 폴더의 bounded `hera.input-recording/1` JSON을 사용하고, replay는 mutation 전에 파일 전체를 검증한 뒤 sequence 소유권·정리 로직을 재사용합니다. Hera는 런타임에 패키지를 확인하고, 장치를 생성하지 않으며, Play Mode가 끝나면 잡고 있던 control을 해제합니다.

### 반복 Scene 작업 자동화

AI에게 다음을 묶어서 시킬 수 있습니다.

- 테스트 전장 생성
- Prefab 배치
- Component 추가와 값 설정
- ScriptableObject 생성
- Animator 상태 구성
- Scene 저장
- 마지막 검증

### 우리 스튜디오 전용 명령 만들기

프로젝트에서 자주 반복하는 작업이 있다면 `[HeraTool]`로 노출할 수 있습니다. 그러면 Hera가 자동으로 발견합니다.

예시:

- `build_test_battle`
- `validate_item_database`
- `spawn_quest_fixture`
- `bake_localization_table`
- `check_prefab_rules`

즉 Hera는 처음에는 범용 Unity 연결 도구로 시작하지만, 나중에는 **우리 게임 제작 파이프라인 전용 CLI**로 자랄 수 있습니다.

---

## Ultra Hera: "완료"를 "검증 완료"로 바꾸기

<div align="center">

<img src="docs/assets/ultra_hera_logo.png" width="42%" alt="Ultra Hera">

<br>

**작업한다. 확인한다. 그다음 결과를 보고한다.**

</div>

Ultra Hera는 AI Unity 작업을 위한 검증 규칙입니다. 기능을 대신 만드는 모드가 아니라, AI가 Hera로 한 작업을 얼마나 꼼꼼히 확인할지 정합니다.

위치:

```text
HeraAgent -> Hera Settings -> Ultra Hera
```

| 모드 | 쉬운 뜻 |
|:---|:---|
| `Off` | 추가 확인 규칙 없음 |
| `Light` | 기본값. 컴파일/상태, Console 에러, 바꾼 대상을 다시 확인하고 종료 |
| `Ultra` | 중요한 작업. 테스트, Play Mode, Inspector, screenshot 같은 강한 증거까지 추가 |

`Light`가 안전벨트 확인이라면 `Ultra`는 비행 전 점검에 가깝습니다.

이런 요청에서 Ultra가 잘 맞습니다.

- "정확히 검증해줘"
- "플레이해서 확인해줘"
- "이 UI랑 맞춰줘"
- "Inspector까지 확인해줘"
- "테스트가 전부 통과하기 전에는 끝내지 마"

목표는 하나입니다. **Unity가 아직 깨져 있는데 AI가 작업을 닫지 않게 하는 것.**

---

## 단순한 Editor 리모컨보다 더 많은 것

Hera에는 오브젝트를 수정하는 기능 외에도 AI가 더 좋은 결과를 만들게 돕는 선택형 제작 시스템이 있습니다.

### Game Feel Mode (Beta)

게임이 단순히 동작하는 것을 넘어 **어떻게 느껴지는지** 생각하게 돕습니다. Screen shake, hit stop, knockback, 카메라, 조작감, 사운드, 보상 연출, 햅틱, 접근성 가이드를 제공합니다.

```bash
hera-agent-unity asset-config gamefeel on
hera-agent-unity game_feel hit-stop
```

가이드 기능이며 Hera가 몰래 무거운 런타임 시스템을 붙이지 않습니다.

### Game Feel UI Mode (Beta)

Hover 확대, press squash, popup 등장, 숫자 카운트업, 체력바 반응, cooldown 피드백, 접근성 같은 UI 감각 레시피를 AI에게 제공합니다.

```bash
hera-agent-unity asset-config gamefeel-ui on
```

### Unity De-slop Mode (Beta)

생성형 UI에서 자주 보이는 불필요한 장식, 약한 간격 규칙, box-in-box, 장식용 이탤릭, 들쭉날쭉한 색상 같은 문제를 찾고 수정 방향을 줍니다.

```bash
hera-agent-unity asset-config uislop on
hera-agent-unity ui_slop box-in-box
```

인벤토리 슬롯처럼 기능적으로 반복돼야 하는 게임 UI를 무조건 평탄화하지 않도록 예외 규칙도 포함합니다.

---

## 명령 한눈에 보기

외울 필요는 없습니다. AI에게 어떤 손과 눈을 제공하는지 이해하기 위한 표입니다.

| 명령 | 쉬운 뜻 |
|:---|:---|
| `doctor --json` | "Hera가 설치됐고 Unity에 연결되나요?" |
| `status` / `ping` | Editor 상태와 생존 여부 확인 |
| `list --compact` | 기본/프로젝트 전용 Tool을 작은 응답으로 발견 |
| `call <tool>` | 현재 Tool 규격을 검증하고 안전하게 호출 |
| `console` | 실제 Unity Console 읽기/초기화 |
| `scene` | Scene 조회, 열기, 저장, 목록, 닫기, GameObject 트리 덤프 |
| `find_gameobjects` | 열린 Scene의 GameObject 검색 |
| `manage_gameobject` | GameObject 생성과 편집 |
| `manage_components` | Component 조회, 추가, 제거, 수정 |
| `manage_assets` | `Assets/` 아래 에셋 작업 |
| `manage_animation` | AnimationClip/AnimatorController 저작·읽기 |
| `manage_settings` | 프로젝트 설정(physics·time·quality·player·audio) 조회·변경 — dry_run 프리뷰 + 승인 게이트 |
| `bake` | lighting/NavMesh/occlusion 베이크 트리거·상태 폴링·취소·삭제 |
| `exec` | Editor 안에서 프로젝트를 아는 C# 실행 |
| `editor` | 정확한 프로젝트 launch/restart 또는 Play, Stop, Pause, Refresh, Compile |
| `test` | Unity 테스트 실행/재개 |
| `task` | Unity에 다시 명령하지 않고 장기 작업 상태 확인 |
| `screenshot` | Scene/Game View, ScreenSpaceOverlay Canvas 또는 단일 오브젝트 캡처; 제한된 uGUI 또는 Camera.main 기준 3D Collider 식별자/좌표 메타데이터와 메타데이터 전용 모드 지원 |
| `input` | EventSystem uGUI 검증 또는 Play Mode Input System 키보드/마우스/sequence 합성 및 record/replay |
| `profiler` | Profiler hierarchy snapshot·성능 스탯 1호출 읽기 |
| `game_feel` | Game Feel 가이드 조회 |
| `ui_slop` | UI 정리 가이드 조회 |
| `batch` | 여러 작업을 한 요청으로 실행 |
| 프로젝트 `[HeraTool]` | 우리 프로젝트가 정의한 전용 명령 실행 |

전체 명령 문서: [docs/COMMANDS.md](docs/COMMANDS.md).

---

## AI가 Hera를 자동으로 쓰게 만들기

프로젝트 규칙에 Hera 사용법을 넣으면 AI가 Unity 상태를 추측하기 전에 Hera부터 확인할 수 있습니다.

### Codex plugin

```bash
codex plugin marketplace add NotNull92/hera-agent-unity --ref main
```

그다음 `/plugins`에서 **Hera Agent Unity**의 **Hera Unity**를 활성화합니다.

### Standalone Agent Skill

```bash
npx skills add NotNull92/hera-agent-unity --skill hera-agent-unity --agent codex
```

### 공용 `AGENTS.md`

```bash
hera-agent-unity doctor --agent-rules --compact >> AGENTS.md
```

기본 compact 규칙은 의도적으로 작습니다. 검토된 기준은 **UTF-8 2,277바이트**이며 bootstrap, 대상 선택, 승인, 안전, 검증에 필요한 핵심 규칙만 항상 읽게 합니다. 전체 가이드는 필요할 때 가져옵니다.

Cursor, Copilot, AntiGravity, Continue 등 다른 환경용 템플릿도 [examples/rules](examples/rules)에 있습니다.

---

## 안전성과 신뢰성을 쉬운 말로

Hera는 실제 Unity 프로젝트를 바꿀 수 있으므로 빠르기만 하면 안 됩니다. **언제 멈춰야 하는지도 알아야 합니다.**

### 포트 번호보다 프로젝트를 신분증으로 봅니다

Unity는 Domain Reload나 재시작 뒤 로컬 포트가 바뀔 수 있습니다. Hera는 정규화된 전체 프로젝트 경로를 Editor 신분으로 우선 사용하고 포트는 임시 연결점으로 취급합니다.

Editor가 여러 개면:

```bash
hera-agent-unity --project /full/path/to/project status
```

모호하면 추측하지 않고 실패합니다.

### 위험한 작업은 승인받을 수 있습니다

승인이 필요한 작업은 먼저 무엇을 하려는지 검사합니다. 반환된 토큰은 그 요청의 대상과 인자에 묶이며 한 번만 사용할 수 있습니다.

### 결과가 애매한 변경을 무작정 다시 하지 않습니다

Reload나 timeout 중 응답이 사라졌다면 새로운 Editor 상태와 소유권을 확인한 뒤 재시도 가능한 작업만 재시도합니다. 응답을 못 받았다는 이유만으로 같은 변경을 두 번 실행하지 않습니다.

### 오래 걸리는 테스트는 새로 시작하지 않고 이어서 기다릴 수 있습니다

Test Runner가 오래 걸리면 실행 상태를 파일에 남기므로 AI가 같은 테스트를 다시 시작하는 대신 기존 run을 이어서 확인할 수 있습니다.

---

## Unity 버전

| Unity 버전 | 상태 | 대표 검증 |
|:---|:---|:---|
| 6000.0 - 6000.2 | 지원 | `6000.0.35f1` |
| 6000.3 - 6000.4 | 지원 | `6000.3.5f2` |
| 6000.5+ | 지원 | `6000.5.6f1` 릴리스 게이트 |
| 6000.0 미만 | 미지원 | 최소 Unity 6 (6000.0) |

버전별 동작은 Unity 한 버전을 보고 추측하지 않고 실제 대표 Editor에서 확인합니다.

---

## CLI가 기본, MCP는 선택

제품 기본값은 일반 CLI입니다.

```text
AI / 터미널 -> Hera CLI -> localhost Connector -> Unity Editor
```

따라서 셸을 실행할 수 있는 AI라면 MCP 설정 없이 Hera를 사용할 수 있습니다.

CLI `v0.1.0+`에는 MCP를 원하는 호스트를 위해 **실험적·default-off·stdio-only MCP adapter**도 들어 있습니다. 별도의 Unity backend를 만드는 것이 아니라 같은 Hera 실행 코어를 사용합니다.

```text
MCP AI -> 선택형 Hera MCP adapter -> 같은 Hera 실행 코어 -> Unity
```

MCP 자체가 AI를 더 똑똑하게 만드는 것은 아닙니다. 같은 Unity 기능을 노출하는 다른 인터페이스입니다. Hera는 단순하고 명시적이며 대부분의 AI에서 사용할 수 있는 CLI를 계속 기본값으로 둡니다.

MCP 설정과 호환성: [docs/MCP.md](docs/MCP.md).

---

## 현재 릴리스

- CLI / GitHub Release: **v0.2.0**, native binary 5종
- npm: **0.2.0** (`latest`)
- Unity Connector / OpenUPM: **0.0.86** (`latest`)
- Official MCP Registry: **0.2.0** (`active`, latest)
- License: **Apache-2.0**

두 버전 번호가 다른 것은 의도된 설계입니다. CLI와 Unity 패키지는 독립적으로 발전하며 호환 계약을 따로 관리합니다.

v0.2.0의 핵심은 정확한 프로젝트 Editor launch/restart, 제한된 Input System sequence/record/replay, UI와 3D physics 스크린샷 근거, opt-in restricted exec입니다. CLI-first 기본값과 최상위 Tool 31개는 그대로 유지합니다. 릴리스 Connector는 **80 actions**를 제공하고, Unity 5개 compile bucket과 위의 6000.5.6f1 실에디터 회귀에서 `ReleaseGateTests` **18/18 PASS**, Console error **0건**을 확인했습니다.

릴리스별 기술 변경을 모두 보고 싶다면 메인 README보다 [CHANGELOG.md](CHANGELOG.md)를 참고하세요.

---

<details>
<summary><strong>Hera는 내부에서 어떻게 동작하나요?</strong></summary>

```text
터미널 / AI 에이전트
        |
        | hera-agent-unity 명령
        v
Go CLI
        |
        | localhost HTTP
        v
Unity Editor 패키지
        |
        | 직렬화된 Unity main-thread 작업
        v
Scene, Console, Play Mode, Assets, Tests, UI
```

Unity 패키지가 로컬 HTTP listener를 열고 CLI가 local heartbeat 상태에서 대상 Editor를 고른 뒤 명령을 보냅니다. Unity 작업은 Editor 메인 스레드에서 실행됩니다.

Domain Reload와 장기 작업은 파일시스템 상태를 사용해 HTTP listener가 재생성돼도 컴파일, 테스트, 복구 흐름을 이어갑니다.

자세한 구조: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

</details>

<details>
<summary><strong>고급 exec, 토큰, async 사용 참고</strong></summary>

- 전용 명령이 있으면 임의 `exec`보다 전용 명령을 우선합니다.
- ID만 필요하면 `find_gameobjects --ids`처럼 작은 projection을 사용합니다.
- 상태를 바꾸는 `exec`는 보통 큰 결과를 반환하지 말고 `null` 또는 무반환을 사용합니다.
- 정말 필요하지 않다면 `UnityEngine.Object` 전체를 반환하지 않습니다.
- 로그 에러가 CLI 실패로 보여야 하면 `--strict` 또는 예외를 사용합니다.
- 플랫폼 API만 필요한 코드에서 파일·네트워크·프로세스·리플렉션·네이티브·스레드·`UnityEditor`·프로젝트 어셈블리 접근을 막으려면 `--security-mode restricted`를 사용합니다. 기본값은 기존 Full Access입니다.
- 실행하지 않고 컴파일만 확인하려면 `exec --check`를 사용합니다.
- 오래 걸리는 비동기 작업은 한 번 실행하고 끝나는 `exec` 안에서 떼어내기보다 추적 가능한 `[HeraTool]`, task, test 흐름으로 표현하는 편이 안전합니다.

전체 에이전트 운영 가이드: [AGENTS.md](AGENTS.md).

</details>

---

## FAQ

### Hera를 쓰면 AI가 더 똑똑해지나요?

아닙니다. Hera는 AI에게 **내 Unity의 실제 상태**와 결과를 검증할 방법을 줍니다. 설계와 구현 판단은 여전히 사용하는 AI 모델이 합니다.

### Python이 필요한가요?

아니요. 기본 설치는 native CLI 하나와 Unity 패키지 하나입니다.

### MCP가 꼭 필요한가요?

아니요. CLI가 production 기본값입니다. MCP는 선택형이며 기본으로 꺼져 있습니다.

### Unity Editor를 여러 개 열어도 되나요?

각 명령은 Editor 하나를 대상으로 합니다. 여러 개가 열려 있으면 전체 `--project` 경로를 지정하는 것이 가장 명확합니다. 로컬 포트가 바뀌어도 선택한 프로젝트 신분을 추적합니다.

### Unity 창을 실제 마우스로 클릭할 수 있나요?

`input`은 uGUI QA용 Unity EventSystem 이벤트를 보내며, Play Mode에서 선택적 Input System 키보드/마우스 상태도 합성할 수 있습니다. 둘 다 Unity 수준 동작의 증거이지 운영체제의 물리 클릭 증거는 아닙니다. 물리 클릭 결과는 따로 보고해야 합니다.

### 연결이 안 되면 무엇을 하나요?

```bash
hera-agent-unity doctor --json
```

Unity 패키지가 설치되어 있고 Editor 컴파일이 끝났는지도 확인하세요.

### 자세한 문서는 어디에 있나요?

- [명령어](docs/COMMANDS.md)
- [문제 해결](docs/TROUBLESHOOTING.md)
- [아키텍처](docs/ARCHITECTURE.md)
- [C# Connector](docs/CSHARP_CONNECTOR.md)
- [MCP adapter](docs/MCP.md)
- [Agent 운영 가이드](AGENTS.md)

---

## Hera를 쓰는 프로젝트

| 프로젝트 | 설명 |
|:---|:---|
| **NoMoreRolls** | AI가 Hera를 통해 Unity Editor를 조작하며 만든 1인 개발 Unity 게임 |

<div align="center">

https://github.com/user-attachments/assets/15d353e4-b7bb-4534-bbca-c27de0792147

<sub><b>NoMoreRolls</b> - Hera가 Unity Editor 작업을 보조해 만든 게임의 전체 Play Mode 영상입니다.</sub>

</div>

---

## 제작자

**Victor** - 라이브 서비스 MMORPG 프로덕션 경험 6년 이상의 Unity/C# 개발자.

GitHub: [@NotNull92](https://github.com/NotNull92)

Discord: [Hera 커뮤니티 참여하기](https://discord.gg/QBzEVuYwK)

---

## 후원

Hera는 무료이며 Apache-2.0 라이선스로 제공됩니다. Hera가 시간을 아껴줬다면 개발을 후원할 수 있습니다.

[![Support on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/notnull92)

---

## 라이선스

Apache License 2.0. [LICENSE](LICENSE)와 [NOTICE](NOTICE)를 확인하세요.
