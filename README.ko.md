<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![GitHub stars](https://img.shields.io/github/stars/NotNull92/hera-agent-unity?style=flat-square&logo=github&label=stars&color=181717)](https://github.com/NotNull92/hera-agent-unity/stargazers)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.25-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-2022.3%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**AI 코딩 에이전트를 위한 토큰 절약형 Unity Editor 조작 CLI입니다.**

<sub>Codex, Claude, Cursor, Copilot, AntiGravity가 열린 Unity 프로젝트를 직접 확인하고 수정하게 합니다 — 기본 CLI 경로는 MCP 설정 없음, Python 서버 없음.</sub>

<br>

[1분 시작](#바로-시작) · [설치](#설치) · [UI 시스템](#ui-시스템) · [명령어](#명령어) · [전체 문서](docs/COMMANDS.md)

<sub>[새로운 점](#새로운-점) · [검증](#ultra-hera) · [AI 규칙](#ai용-규칙-넣기) · [FAQ](#faq)</sub>

[English](README.md) · **한국어**

</div>

---

## 무엇인가요?

`hera-agent-unity`는 AI 코딩 에이전트가 실행 중인 Unity Editor를 낮은 토큰 비용으로 조작하게 해 주는 CLI입니다.

쉽게 말하면, AI에게 살아 있는 Unity Editor 리모컨을 쥐여 주는 도구입니다.

| AI가 하고 싶은 일 | Hera가 해 주는 일 |
|:---|:---|
| Unity가 켜져 있는지 보기 | 실제 Editor 상태를 확인합니다. |
| C# 코드를 실행하기 | 지금 열린 Unity 프로젝트 안에서 실행합니다. |
| 콘솔 에러 보기 | Unity Console의 실제 에러를 읽습니다. |
| Play 버튼 누르기 | Play Mode에 들어가고 기다립니다. |
| 오브젝트 만들기/고치기 | Unity API로 직접 처리합니다. |
| UI 만들기 | 진짜 Unity UI 오브젝트를 만들고 캡처합니다. |
| UI 입력 검증하기 | 화면 좌표에 의존하지 않고 Unity EventSystem 이벤트를 보냅니다. |

AI가 오래된 학습 데이터로 추측하지 않아도 됩니다. 실제 Editor를 보고, 실행하고, 결과를 다시 확인할 수 있습니다.

```text
AI 에이전트  ->  hera-agent-unity  ->  Unity Editor
```

---

## 왜 필요한가요?

AI는 Unity 화면을 볼 수 없어서 자주 넘겨짚습니다.

예를 들면 이런 것을 틀릴 수 있습니다:

- 지금 어떤 씬이 열려 있는지;
- 어떤 오브젝트가 있는지;
- 내 Unity 버전에 어떤 API가 있는지;
- Play Mode가 제대로 되는지;
- Console에 어떤 에러가 있는지.

Hera를 쓰면 AI가 Unity에게 직접 물어볼 수 있습니다.

```bash
hera-agent-unity status
hera-agent-unity console --type error
hera-agent-unity exec "return Application.unityVersion;"
hera-agent-unity editor play --wait
```

Python 서버는 필요 없습니다. production 기본값인 CLI 경로에는 MCP 설정
파일이나 특별한 에이전트 플러그인이 필요 없습니다. CLI `v0.1.0+`에는 명시적으로
설정한 MCP 클라이언트용 실험적·default-off stdio adapter도 포함됩니다. 설정과
호환성 경계는 [docs/MCP.md](docs/MCP.md)에 있습니다.

---

## 새로운 점

### v0.1.1 - 계약, 복구, 릴리스 검증 강화

이번 릴리스는 검증된 Unity 실행 코어를 교체하지 않고, 완성된 CLI + 선택형 MCP
구조의 계약과 장애 복구 경계를 더 명확하게 다듬었습니다.

| 릴리스 변경 | 쉬운 뜻 |
|:---|:---|
| 실행 프로토콜 버전 명시 | 현재 단일 명령은 `hera.execution/1`을 보내며, 지원하지 않는 버전은 승인·기록·Unity 실행 전에 중단됩니다. |
| 복구 경계 강화 | 오래된 catalog, 고아 ledger, 부분 Settings 읽기, stale config lock, 결과 불명 timeout을 명시적으로 차단하거나 복구합니다. |
| Compact 조회 축소 | `tool_describe`가 전체 Tool 대신 필요한 Action 하나만 반환할 수 있습니다. 가장 큰 실측 사례는 약 92% 작아졌습니다. |
| 반복 가능한 릴리스 게이트 | Go/C# 생성물 drift, Unity 5개 compile bucket, 격리된 NUnit package test, race test, catalog payload 측정을 자동 검증합니다. |
| Connector 0.0.80 | UPM 패키지에도 같은 runtime 안정화와 release-gate 변경이 포함됩니다. CLI와 Connector 버전은 계속 독립적입니다. |

일반 CLI는 계속 production 기본값입니다. MCP는 선택형·default-off·stdio-only로 유지됩니다.

### v0.1.0 — 안전한 다중 Editor 선택과 선택형 MCP adapter

이번 릴리스는 일반 CLI를 대체하지 않으면서 M0-M17 adapter migration을
완료합니다. MCP는 실험적·stdio-only·환경 변수 opt-in 기능으로 배포되며, typed
CLI와 localhost Unity Connector가 계속 production 기본값입니다.

#### 왜 이 마이그레이션을 했나요?

이제 여러 AI 프로그램이 도구를 찾고 실행하는 공통 방식으로 MCP를 사용합니다.
Hera에는 이미 작고 효율적인 CLI와 검증된 Unity 실행 경로가 있으므로, 제품 전체를
MCP 중심으로 다시 만들면 같은 기능을 중복 구현하고 기존 사용법까지 바꾸게 됩니다.
그래서 v0.1.0은 가장자리에 얇은 번역기만 추가했습니다. MCP를 아는 AI는 익숙한
방식으로 요청하고, 실제 작업은 기존과 같은 Hera CLI와 Connector 경로에서
검증·실행됩니다.

쉽게 비유하면 CLI는 Hera 전용의 작고 빠른 리모컨입니다. MCP adapter는 다른
기기도 그 리모컨을 사용할 수 있게 해 주는 작은 변환 젠더이지, 리모컨을 크고
복잡한 조종판으로 교체하는 장치가 아닙니다.

#### 어떻게 작동하나요?

요청은 `AI client → 선택형 MCP adapter → 기존 Hera 실행 core → localhost
Connector → 선택한 Unity Editor` 순서로 이동합니다. Adapter는 필요한 도구를
찾고 설명하며, 요청 형식과 안전 규칙을 확인한 뒤 기존 Hera 실행 경로로 넘깁니다.
Unity를 외부 네트워크에 공개하거나 Connector를 교체하지 않으며, 승인을 몰래
생략하지도 않습니다. 승인이나 작업 기록 기능을 지원하지 않는 환경에서는 안전하다고
추측해 실행하는 대신 요청을 차단합니다.

#### 정확도는 어떻게 달라졌나요?

MCP 자체가 AI를 더 똑똑하게 만들지는 않으며, adapter가 다른 Unity 실행 엔진을
사용하는 것도 아닙니다. 정확도는 요청을 전달하는 과정에서 개선됐습니다. 정규화된
전체 프로젝트 경로로 다른 열린 Editor에 요청이 잘못 전달되는 것을 막고, 현재
Editor가 공개한 엄격한 도구 형식으로 잘못되거나 오래된 인자를 거부합니다. 응답이
끊기면 heartbeat를 새로 읽어 domain reload, Editor 재시작, 사라진 대상, 다른
프로젝트가 가져간 포트를 구분합니다. Operation ID와 Connector 작업 기록은 결과를
확인하지 못한 요청이 같은 변경을 두 번 만드는 것도 방지합니다.

일상적인 말로 표현하면, Hera가 작업 전에 전체 배송 주소와 영수증을 함께 확인하게
된 것입니다. 따라서 잘못된 프로젝트 호출, 유효하지 않은 요청, 중복 변경 가능성이
줄어듭니다. 하지만 AI의 디자인 판단이 반드시 옳다고 보장하거나 Unity test를
대체하지는 않으며, 정확도가 몇 퍼센트 높아졌다고 말할 근거도 아직 없습니다. 현재
저장소에는 그런 수치 주장을 뒷받침하는 benchmark가 없습니다. 이번 릴리스가 보장하는
범위는 더 좁고 분명합니다. 모호하거나 오래된 연결 상태를 더 많이 감지하고, 추측해
실행하는 대신 안전하게 멈춥니다.

처음으로 보존한 전체 게임 제작 실험은
[Crystal Forge 실사용 benchmark](docs/benchmarks/user-scenario/crystal-forge-6000.3.5f2.md)입니다.
최종 플레이 결과는 정확했지만 여러 차례 수리한 뒤에야 통과했으며, 최초 시도 성공은
달성하지 못했습니다. 이 결과는 회귀 검증 기준이지 MCP와 CLI의 A/B 결과나 모델
정확도 향상의 증거가 아닙니다.

#### 토큰을 더 사용하나요?

일반 CLI 경로는 바뀌지 않았으므로 새로 늘어나는 토큰 비용이 없습니다. MCP를
사용하면 프로토콜의 부가 정보가 생기므로 토큰 사용량이 **항상 CLI와 똑같다고
보장할 수는 없으며**, 사용하는 AI client와 작업에 따라 달라집니다. Hera는 이
증가분을 두 가지 방식으로 줄입니다. Profile은 작고 고정된 기본 도구만 보여 주고,
Compact MCP는 찾기·설명·실행이라는 세 개의 관문 도구만 등록한 뒤 실제로 필요한
도구의 설명을 그때 가져옵니다. 모든 도구를 한꺼번에 펼치는 Full 방식은 기본값이
아니라 진단·개발용 선택지로 남겨 두었습니다.

현재 저장소에는 CLI와 MCP의 토큰 사용량이 정확히 같다고 증명하는 benchmark가
없습니다. 따라서 목표를 과장하지 않습니다. 기존 CLI 비용은 그대로 보존하고, MCP
호환성 때문에 처음부터 대화에 들어오는 정보량을 가능한 한 작게 만드는 것이 이번
설계의 정확한 목표입니다.

#### CLI 중심인 Hera가 왜 Compact MCP를 빌려왔나요?

Hera의 핵심 철학은 특정 프로토콜만 고집하는 것이 아니라, 적은 토큰으로 Unity를
검증 가능하게 제어하는 것입니다. MCP를 완전히 거부하면 이를 사용하는 AI와 연결할
수 없고, 일반적인 “모든 도구 등록” MCP 구조를 그대로 쓰면 사용하지도 않을 도구의
이름·설명·형식이 대화에 먼저 들어옵니다. v0.1.0은 MCP를 바깥쪽 대화 언어로만
사용하고 production 중심에는 계속 CLI를 둡니다. Compact 방식은 호환성을 필요할
때만 불러오므로, 호환성이 매번 지불하는 고정 토큰 비용이 되지 않게 하면서 기존
철학을 지킵니다.

| 릴리스 변경 | 쉬운 뜻 |
|:---|:---|
| 프로젝트 기반 Editor 선택 | 정규화된 전체 프로젝트 경로로 Editor를 식별합니다. 포트는 임시 연결점으로 취급하고 모호한 선택은 실패합니다. |
| 안전한 응답 손실 복구 | 재시도 전에 domain reload, Editor 재시작, 사라진 대상, 포트 재사용을 구분합니다. 비멱등 mutation은 무작정 반복하지 않습니다. |
| 실험적 MCP adapter | `HERA_MCP_ENABLED=1 hera-agent-unity mcp`로 Profile, Compact, Full-safe, 승인, operation ledger, Tasks, 제한된 result resource를 사용할 수 있습니다. |
| Connector 0.0.76 패키징 | UPM 테스트를 production assembly에서 분리해 Unity 6000.5 컴파일 정지와 중복 TestRunner 참조를 제거했습니다. |
| Apache-2.0 | 명시적인 특허 조건, 수정 표시, 배포용 `NOTICE` 파일을 적용했습니다. |

### Unity De-slop Mode (Beta) — 정적 시각 규율

Game Feel Mode가 화면이 *어떻게 움직이는가*를 다룬다면, De-slop Mode는 화면이
*가만히 있을 때*를 다룹니다. 생성된 UI를 생성된 티 나게 만드는 흔적들입니다.
택소노미는 Connector 안에 함께 들어 있으므로(**0.0.63** 이상) 따로 받아올 것이
없습니다.

| 하는 일 | 그렇게 만든 이유 |
|:---|:---|
| 5개 영역 49개 tell, A → B → C → D → E 순서로 수정 | 상류 수정이 하류 수정의 충돌을 미리 녹여 없앱니다 |
| 모든 tell이 uGUI *와* UI Toolkit 판정을 함께 가집니다 | UI Toolkit 쪽은 각 Unity 버전이 실제로 제공하는 USS 어휘에 대고 작성했습니다 |
| 판정은 상태가 아니라 라이브 씬에서 매번 재측정하는 술어입니다 | "완료" 를 저장하는 체크리스트는 낡지만, 측정하는 체크리스트는 낡지 않습니다 |
| 간격·타입 스케일은 1280x720 기준 해상도에 대고 해석합니다 | 기준 해상도를 말하지 않은 절대 px 는 의미가 없습니다 |
| 반복되는 인터랙션 셀은 절대 평탄화하지 않습니다 | 게임 UI 의 중첩 표면은 대개 기능입니다 — 인벤토리 슬롯, 핫바, HUD 패널 |

[모드 살펴보기 →](#unity-de-slop-mode-beta) · [Game Feel Mode →](#game-feel-mode-beta)

### 라이브 Editor에 근거한 UI Toolkit 스캐폴딩

Connector **0.0.61**은 에이전트가 버전별 API를 추측하지 않아도 되는
UI Toolkit 경로를 추가했습니다.

| 선택 | Hera 동작 | 기본 경계 |
|:---|:---|:---|
| `ugui` (기본값) | Canvas / GameObject / RectTransform 워크플로를 유지합니다 | 기존 uGUI 파이프라인 |
| `uitk` | 검증된 `.uxml`, 공유 `.hera-*` `.uss`, `PanelSettings`, `UIDocument`를 생성합니다 | runtime-only 리플렉션 element, UXML attribute, USS property |
| World-space | 라이브 Unity 6000.2+에서만 활성화합니다 | 문서 bucket에서 추론하지 않음 |
| v1 범위 | layout scaffolding에 집중합니다 | MVVM과 data binding은 의도적으로 제외 |

[UI 시스템 선택하기 →](#ui-시스템) · [UI 문서 계약 보기 →](docs/UI_DOC_IR.md)

### 최신 CLI 릴리스 - v0.1.1

공개된 최신 CLI 릴리스는 **v0.1.1**입니다(2026년 8월 4일). 이 릴리스의
Unity 패키지는 **Connector 0.0.80**입니다. CLI와 Connector 버전은 의도적으로
분리되어 있습니다.

| 현재 하이라이트 | 쉬운 뜻 |
|:---|:---|
| **버전과 catalog가 맞는 요청만 실행** | 실행 프로토콜과 live catalog가 맞지 않으면 Unity를 변경하기 전에 중단합니다. |
| **더 작은 Compact 조회** | 필요한 Action 하나만 설명할 수 있어 관계없는 Action schema를 반복해서 보내지 않습니다. |
| **복구 경계 강화** | ledger, Settings, config lock, timeout, MCP lifecycle이 결과 불명 상태에서 추측하지 않습니다. |
| **반복 가능한 릴리스 증거** | Unity 5개 compile bucket과 격리된 Connector NUnit gate를 자동 검증하고 fixture manifest를 원상복구합니다. |

릴리스 호환성 매트릭스:

| Unity Editor | Connector 0.0.80 exact-source compile |
|:---|:---:|
| 2022.3.62f2 | PASS |
| 2023.2.22f1 | PASS |
| 6000.0.35f1 | PASS |
| 6000.3.5f2 | PASS |
| 6000.5.6f1 | PASS |

직전 Connector 0.0.75는 같은 매트릭스에서 clean UPM import와 runtime 검사도
통과했습니다. 근거: [Unity 호환성 인벤토리](docs/UNITY_EDITOR_VERSION_INVENTORY.md)

낮은 토큰 벤치마크 기준:

| Unity Editor | `list --compact` | `find_gameobjects --ids` | 상세 |
|:---|---:|---:|:---|
| 2022.3.62f2 | **93 T** | **54 T** | [벤치마크](docs/benchmarks/token-reduction/2022.3.62f2.md) |
| 2023.2.22f1 | **93 T** | **54 T** | [벤치마크](docs/benchmarks/token-reduction/2023.2.22f1.md) |
| 6000.3.5f2 | **93 T** | **49 T** | [벤치마크](docs/benchmarks/token-reduction/6000.3.5f2.md) |
| 6000.5.0f1 | **93 T** | **55 T** | [벤치마크](docs/benchmarks/token-reduction/6000.5.0f1.md) |

전체 벤치마크: [docs/benchmarks/token-reduction/README.md](docs/benchmarks/token-reduction/README.md)

---

## 바로 시작

### 1. Unity를 엽니다

Hera Unity 패키지가 설치된 프로젝트를 엽니다.

### 2. 연결을 확인합니다

```bash
hera-agent-unity status
```

프로젝트 이름, Unity 버전, 포트, 상태가 나오면 연결된 것입니다.

### 3. AI에게 시킵니다

예시:

```text
hera-agent-unity를 사용해줘. Unity 콘솔을 확인하고, Play Mode에 들어가서 문제를 재현한 뒤 고쳐줘.
```

그러면 AI는 이런 명령을 직접 실행할 수 있습니다:

```bash
hera-agent-unity console --type error
hera-agent-unity editor play --wait
hera-agent-unity exec "return EditorSceneManager.GetActiveScene().name;"
hera-agent-unity test --mode PlayMode
```

---

## 설치

설치는 두 부분입니다.

1. 컴퓨터에 CLI 프로그램 설치;
2. Unity 프로젝트에 Unity 패키지 설치.

### CLI

**npm (Windows, macOS, Linux)**

```bash
npm install --global hera-agent-unity
```

**Windows PowerShell**

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex"
```

설치 후 새 터미널을 열고 확인합니다:

```powershell
hera-agent-unity version
```

**macOS / Linux**

```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | bash
```

**Go로 설치**

```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**수동 설치**

[Releases](https://github.com/NotNull92/hera-agent-unity/releases)에서 파일을 받은 뒤 실행합니다:

```bash
hera-agent-unity install
```

### Unity 패키지

Unity에서:

```text
Window -> Package Manager -> Add package from git URL
```

아래 주소를 넣습니다:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

또는 `Packages/manifest.json`에 직접 추가합니다:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

최신을 따라가지 않고 특정 커넥터(UPM) 버전을 고정하려면 존재하는
`connector-<버전>` git 태그를 뒤에 붙입니다:

```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-<버전>"
```

Connector 버전은 CLI `v*` 릴리스와 분리되어 있습니다.

Unity가 열리면 커넥터가 자동으로 시작합니다.

---

## 명령어

AI가 가장 자주 쓰는 명령어입니다.

| 명령어 | 하는 일 |
|:---|:---|
| `status` | 어떤 Unity Editor에 연결됐는지 보여 줍니다. |
| `doctor --json` | 설치, PATH, Unity 연결을 검사합니다. |
| `list --compact` | 작은 응답으로 도구 목록을 봅니다. |
| `call <tool> --json '{...}'` | live strict tool contract로 검증한 뒤 호출합니다. |
| `console --type error` | Unity의 실제 에러를 읽습니다. |
| `exec "..."` | Unity 안에서 C#을 실행합니다. |
| `editor play --wait` | Play Mode에 들어가고 기다립니다. |
| `editor stop --wait` | Play Mode를 멈추고 기다립니다. |
| `scene info` | 현재 씬 정보를 봅니다. |
| `find_gameobjects` | 열린 씬에서 오브젝트를 찾습니다. |
| `manage_assets` | `Assets/` 아래 프로젝트 에셋을 찾고, 폴더를 만들고, ScriptableObject `.asset`을 생성하고, 복사·이동·삭제합니다. |
| `manage_gameobject` | GameObject를 만들고, 복제하고, 옮기고, 이름을 바꿉니다. |
| `manage_components` | 컴포넌트를 추가, 삭제, 조회, 수정합니다. |
| `manage_animation` | AnimationClip과 AnimatorController 상태머신을 저작합니다. |
| `ui_doc` | uGUI 또는 UI Toolkit 스캐폴드를 만들고, uGUI overlay를 캡처합니다. |
| `input` | Unity EventSystem raycast와 pointer handler로 uGUI 상호작용을 검증합니다. |
| `game_feel` | 게임 필 레시피를 조회합니다 (screen shake, hit stop, honest juice 등). |
| `ui_slop` | UI 슬롭 항목과 수정법을 조회합니다 (장식, 레이아웃, 간격, 타이포, 색). |
| `test` | Unity 테스트를 실행합니다. |
| `screenshot` | Scene/Game 뷰나 단일 GameObject를 캡처합니다. |
| `batch` | 여러 명령을 한 번에 실행합니다 (atomic 롤백 옵션). |

전체 명령어: [docs/COMMANDS.md](docs/COMMANDS.md)

---

## 토큰 절약

Hera는 AI 에이전트를 위해 만들었습니다. 그래서 응답이 작아야 합니다.

응답이 크면 AI의 입력 토큰도 커집니다. 토큰이 커지면 돈도 더 들고, 대화창도 빨리 찹니다. 그래서 Hera는 자주 쓰는 명령의 응답을 작게 만듭니다.

추천 경로:

```bash
hera-agent-unity list --compact
hera-agent-unity find_gameobjects --name Player --ids
hera-agent-unity list --tool manage_gameobject
```

정말 필요할 때만 큰 응답을 받습니다:

```bash
hera-agent-unity list
hera-agent-unity find_gameobjects --fields all
hera-agent-unity console --lines 0 --stacktrace full
```

---

## 스크린샷으로 Unity UI 만들기

Unity UI는 AI가 틀리기 쉽습니다. 앵커, 피벗, 레이아웃이 복잡하기 때문입니다.

Hera는 AI에게 이런 반복 작업을 시킵니다:

1. 지금 UI를 읽습니다;
2. 진짜 Unity UI 오브젝트를 만듭니다;
3. Unity가 그린 화면을 캡처합니다;
4. 비교하고 고칩니다.

```bash
hera-agent-unity ui_doc export --path /Canvas/HUD
hera-agent-unity ui_doc sample --image hud_ref.png --region "0,0,1,0.2"
hera-agent-unity ui_doc apply --file hud.json --parent /Canvas --mode upsert
hera-agent-unity ui_doc capture --out hud_built.png
```

핵심은 간단합니다. UI를 찍어서 확인하고 고칩니다. 눈대중으로 맞추지 않습니다.
`ui_doc apply`는 현재 Unity Editor 버전에 맞는 공식 uGUI 문서 bucket도 보고합니다.
예를 들어 Unity 6000.3은 `com.unity.ugui@2.0`, Unity 6000.5+는
`com.unity.ugui@2.5` 규칙을 사용합니다. 자동으로 고친 항목은 `fixes`,
판단이 필요한 구조 문제는 `diagnostics`에 나옵니다.

---

## UI 시스템

`ui_system`은 출력 backend를 명시합니다. `asset-config.json`에서 설정하며,
각 UI 요청은 선택한 backend 안에서 처리됩니다. Hera는 씬을 보고 UI 방식을
추측하거나 자동 전환하지 않으며, `ui_doc.backend`가 설정과 다르면 씬이나
에셋을 변경하기 전에 거부합니다.

| Backend | 적합한 용도 | Hera 생성물 |
|:---|:---|:---|
| `ugui` (기본값) | Canvas 기반 UI | GameObject와 RectTransform |
| `uitk` | 런타임 UI Toolkit layout | 검증된 UXML, 공유 USS, `PanelSettings`, `UIDocument` |

런타임 UI Toolkit 프로젝트라면 `uitk`를 설정합니다:

```bash
hera-agent-unity asset-config ui-system uitk
hera-agent-unity ui_doc apply --file settings-uitk.json
```

UITK 문서는 `backend: "uitk"`를 사용하고, 정확한 runtime element 이름,
리플렉션으로 검증된 UXML attribute, 리플렉션으로 검증된 USS property를
사용합니다. 생성 파일은 `Assets/HeraGenerated/UI` 아래에 둡니다.

| 요구 사항 | UI Toolkit v1 동작 |
|:---|:---|
| Screen-space | 지원하는 모든 Editor에서 기본값 |
| World-space | 라이브 Unity runtime 6000.2+에서만 지원하며 문서 bundle bucket과 별개 |
| 검증 | 리플렉션으로 확인한 runtime element, attribute, USS property schema |
| Data binding | v1 범위에서 의도적으로 제외 |

두 backend 계약은 [UI_DOC_IR.md](docs/UI_DOC_IR.md)를 참고하세요.

---

## Input QA

일부 에이전트 환경은 Unity screenshot state를 안정적으로 얻지 못해서 물리 좌표 클릭을 거부합니다. Hera의 `input` 명령은 이때 사용할 수 있는 별도의 Unity 레벨 QA 경로입니다.

```bash
hera-agent-unity input state
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
hera-agent-unity input submit --path /Canvas/StartButton
hera-agent-unity input scroll --path /Canvas/ScrollRect --scroll_delta 0,-3
hera-agent-unity input drag --path /Canvas/Slider/Handle --to_normalized 0.8,0.5
```

`input`은 Unity uGUI의 `EventSystem.RaycastAll`과 `ExecuteEvents` pointer handler를 사용합니다. blocker, handler, interactability, submit, scroll, drag 동작까지 Unity UI 이벤트 경로가 실제로 동작하는지 확인할 수 있습니다.

입력 작업과 진단 출력은 dispatch 전에 제한됩니다. `hold_ms`는 5000 이하, `settle_frames`는 120 이하, `steps`는 120 이하, `click_count`는 3 이하, `max_results`는 100 이하(기본 50)여야 하며 잘못된 값은 `INPUT_INVALID_PARAM`을 반환합니다.

단, 물리 OS/window 클릭은 아닙니다. 증거는 분리해서 보고해야 합니다:

| QA 기준 | 보고 방식 |
|:---|:---|
| Unity EventSystem input QA | `input inspect`, `input click`, callback, console log, Play Mode test 결과로 PASS/FAIL을 기록합니다. |
| Physical OS click QA | Computer Use가 Unity screenshot state를 얻지 못하거나 native window input backend를 쓸 수 없으면 BLOCKED로 기록합니다. |

자세한 명령 문서: [docs/COMMANDS.md](docs/COMMANDS.md#input)

---

## Game Feel Mode (Beta)

AI는 돌아가는 게임은 만들 수 있습니다. Game Feel Mode (Beta)는 그 게임이 *제대로 느껴지게* 도와줍니다.

이 모드를 켜면 Hera로 작업하는 에이전트가 게임플레이 자체의 game feel 가이드를 받습니다 — screen shake, hit stop, knockback, 조작감(coyote time, input buffering), 카메라, 사운드, 보상 연출 — *Game Feel & Juice Bible*과 *Ethical Engagement Game Feel Framework*에서 가져온 구체적 수치(px, 초, %, Hz)와 함께.

윤리 원칙은 나중에 검사하는 게 아니라 레시피에 내장되어 있습니다. 모든 레시피가 제약을 함께 담습니다 — screen shake 강도 옵션, 광과민성 flash 감소, 정직한 보상 연출, 확률 투명성 — 그래서 에이전트가 만든 결과물은 애초에 윤리 체크리스트를 통과하는 구조입니다 (**Honest Juice**: 연출 강도는 실제 성취 가치와 일치해야 한다).

세 가지 표면이 함께 동작합니다:

- `hera-agent-unity game_feel <토픽>` — 동봉 지식 베이스 (54개 토픽, ethics 우선 정렬), 항상 사용 가능
- `doctor --agent-rules` — 모드가 켜져 있으면 핵심 원칙 + 워크플로 주입
- 도구 힌트 — `manage_components`로 Camera / ParticleSystem / AudioSource / Rigidbody / Light / Animator를 붙이면 관련 토픽을 안내

가이드만 제공합니다 — Hera가 런타임 컴포넌트를 자동으로 붙이지 않습니다.

Unity에서 켭니다:

```text
HeraAgent -> Hera Settings -> Game Feel Mode (Beta)
```

CLI에서는: `hera-agent-unity asset-config gamefeel on`

---

## Game Feel UI Mode (Beta)

AI는 작동하는 버튼은 만들 수 있습니다. Game Feel UI Mode (Beta)는 그 버튼이 게임처럼 느껴지게 도와줍니다.

이 모드를 켜면 Hera가 UI 생성 결과에 `agent_hint`를 붙입니다. 이 힌트에는 hover 확대, press 눌림, release bounce, 대칭 선택 버튼을 갖춘 팝업 overshoot, 등급별 보상 연출 사다리, 크리티컬 스펙을 포함한 숫자 카운트업, dual-response 체력바, 차지/쿨다운 패턴, ECN-DMN 밀도 가이드, 햅틱, 접근성 기본 요건 같은 구체적인 game-feel 레시피가 들어갑니다. 힌트 끝에는 `game_feel` 지식 베이스의 `ui` 카테고리 포인터가 붙어 — 요소별 스펙 표, 인지 부하 이론, 선택 대칭 윤리, 2026 트렌드 — 필요할 때 깊이 조회할 수 있습니다.

이 기능은 가이드이지 무거운 런타임 기능이 아닙니다. Hera가 씬에 큰 컴포넌트를 자동으로 붙이지 않습니다. 에이전트가 레시피를 받고, 평소처럼 Unity 수정 명령으로 애니메이션과 피드백을 적용합니다.

uGUI fixer는 game-feel 레시피와 별개입니다. `ui_doc apply`는 항상 공식문서 기반
`fixes` / `diagnostics`를 보고하고, Game Feel UI Mode (Beta)는 선택적으로 game-feel
가이드를 `agent_hint`에 붙입니다.

Unity에서 켭니다:

```text
HeraAgent -> Hera Settings -> Game Feel UI Mode (Beta)
```

CLI에서는: `hera-agent-unity asset-config gamefeel-ui on`

같은 Hera Settings 패널에서 DOTween이 켜져 있으면 DOTween 방식의 트윈을 추천합니다. 없으면 coroutine이나 lerp 방식으로 안내합니다.

대표 레시피:

| UI 요소 | Game-feel 힌트 |
|:---|:---|
| Button | hover 확대, press 눌림, release bounce, 클릭음, 햅틱. |
| Popup / panel | pop-in 등장, 화면 dim, 빠르고 조용한 퇴장. |
| Text | 줄별 등장, 숫자 카운트업, 떠오르는 데미지 텍스트. |
| Image / reward | pop-in, 희귀도 pulse, glow, hover lift. |
| Bar | 즉시 줄어드는 fill, 늦게 따라오는 chip bar, 낮은 수치 pulse, segment tick. |

자세한 명령 문서: [docs/COMMANDS.md](docs/COMMANDS.md#ui_doc)

---

## Unity De-slop Mode (Beta)

Game Feel Mode가 화면이 **어떻게 움직이는지**를 다룬다면, De-slop Mode는 화면이 **가만히 있을 때의 모습**을 다룹니다. 생성된 UI를 생성물처럼 보이게 만드는 통계적 신호 — 반사적인 장식, 규율 없는 컨테이너, 눈대중으로 정한 간격, 장식용 이탤릭, 무지개 팔레트 — 를 걷어냅니다.

동봉된 `ui_slop` 택소노미는 이를 5개 영역으로 나누고, 수정은 이 순서로 진행합니다. 상류 수정이 하류에서 생길 충돌을 미리 없애기 때문입니다.

| 영역 | 다루는 것 |
|:---|:---|
| A | 장식 스윕 — 오브, 글로우, 글래스, 스파클, 이모지 아이콘 |
| B | 레이아웃, RectTransform, 컨테이너, 앵커, Raycast Target |
| C | 간격 — 사다리, 밀도, 그루핑, 죽은 여백 |
| D | 타이포 — 이탤릭, 폰트 역할, 타입 스케일, 한글 조판 |
| E | 색 — 시맨틱 역할, 팔레트 규율, WCAG 대비 |

각 항목은 uGUI 검사와 UI Toolkit 검사를 함께 갖습니다. UI Toolkit 쪽은 각 Unity 버전이 실제로 지원하는 USS 어휘에 맞춰 작성됐습니다. 여기에 기계적인 수정법과 **슬롭으로 취급하면 안 되는 기능적 예외**가 함께 붙습니다 — 게임 UI에서 중첩 표면은 대개 정당하므로, 인벤토리 슬롯처럼 반복되는 인터랙티브 셀은 절대 평탄화하지 않습니다.

```bash
hera-agent-unity ui_slop                 # 영역별 택소노미 인덱스
hera-agent-unity ui_slop box-in-box      # 항목 하나: 검사, 예외, 수정법
```

도구 자체는 항상 사용할 수 있습니다. 모드를 켜면 추가로 `doctor --agent-rules`가 de-slop 규율을 주입하고, `manage_components add`가 관련 항목을 가리킵니다.

```text
HeraAgent -> Hera Settings -> Unity De-slop Mode (Beta)
```

CLI에서는: `hera-agent-unity asset-config uislop on`

---

## Ultra Hera

Ultra Hera는 AI 에이전트 규칙 설정입니다. 이 기능이 AI 작업을 대신 하지는 않습니다. AI가 Hera로 Unity 작업을 한 뒤 얼마나 꼼꼼히 확인해야 하는지 알려줍니다.

위치:

```text
HeraAgent -> Hera Settings -> Ultra Hera
```

모드:

| 모드 | 쉬운 뜻 |
|:---|:---|
| `Off` | AI가 Hera 사용 후 다시 확인하지 않아도 됩니다. |
| `Light` | 기본값입니다. AI가 목표를 확인하고, 필요한 상태만 읽고, 코드/씬/Inspector를 바꾸고, 컴파일 또는 상태를 확인하고, 콘솔 에러를 읽고, 바꾼 대상만 다시 본 뒤, 필요하면 한두 번 고칩니다. |
| `Ultra` | 모든 작업에는 Light 확인을 쓰고, 중요한 요청은 테스트, Play Mode, Inspector 재확인, screenshot, `ui_doc` capture 같은 더 강한 확인으로 올립니다. |

Light는 "틀린 상태로 끝내지 않기"가 목표입니다. Ultra는 "정확히 검증해줘", "플레이해서 확인해줘", "UI 맞춰줘", "인스펙터까지 봐줘" 같은 요청에 씁니다.

대표 Light 명령:

```bash
hera-agent-unity status
hera-agent-unity console --type error --lines 20
hera-agent-unity editor refresh --compile
hera-agent-unity find_gameobjects --ids
hera-agent-unity exec --depth 1 ...
```

대표 Ultra 명령:

```bash
hera-agent-unity test --mode EditMode
hera-agent-unity test --mode PlayMode
hera-agent-unity editor play --wait
hera-agent-unity screenshot --view game
hera-agent-unity ui_doc capture --out ...
```

---

## Unity 버전

| Unity 버전 | 상태 | 설명 |
|:---|:---|:---|
| 2022.3 LTS | 지원 | `2022.3.62f2`에서 확인했습니다. |
| 2023.2 | 지원 | `2023.2.22f1`에서 확인했습니다. |
| 6000.0 - 6000.4 | 지원 | Unity 6입니다. |
| 6000.5+ | 지원 | 필요한 경우 Unity의 새 오브젝트 ID 방식을 사용합니다. |
| 2022.3 미만 | 미지원 | 최소 지원 버전은 Unity 2022.3입니다. |

---

## AI용 규칙 넣기

프로젝트에 Hera 규칙을 넣으면 AI가 추측하기 전에 Hera부터 사용합니다.

Codex에서는 저장소에 포함된 플러그인을 바로 설치할 수도 있습니다:

```bash
npx codex-marketplace add NotNull92/hera-agent-unity/plugins/hera-unity --plugin
```

이 저장소에는 주요 코딩 에이전트용 규칙 파일이 준비되어 있습니다:

| 에이전트 | 넣을 파일 | 뜻 |
|:---|:---|:---|
| Codex / Claude / Gemini CLI / 대부분의 에이전트 | `AGENTS.md` | 셸 명령을 실행하는 에이전트가 함께 읽는 기본 가이드입니다. |
| Cursor | `.cursor/rules/hera-agent-unity.mdc` | Cursor는 `.mdc` frontmatter가 있어야 프로젝트 규칙이 켜집니다. |
| GitHub Copilot | `.github/copilot-instructions.md` | 저장소 전체에 적용되는 Copilot 지침입니다. |
| GitHub Copilot, 파일별 | `.github/instructions/hera-agent-unity.instructions.md` | `.cs`, `.prefab`, `.unity`, `Assets/**` 같은 Unity 파일에 적용됩니다. |
| Google AntiGravity | `GEMINI.md`, `.agents/agents.md`, `.agents/skills/hera-agent-unity/SKILL.md` | 프로젝트 진입 규칙, 워크스페이스 연결, 온디맨드 스킬입니다. |
| Continue.dev | `.continuerules` | 일반 Markdown 규칙입니다. |

가장 흔한 공통 파일은 이렇게 만듭니다:

```bash
hera-agent-unity doctor --agent-rules >> AGENTS.md
```

Cursor용:

```bash
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
```

Copilot, AntiGravity, Continue 템플릿은 [examples/rules](examples/rules)에 있습니다. 이 저장소에는 실제 예시도 들어 있습니다: [.github/copilot-instructions.md](.github/copilot-instructions.md), [.github/instructions/hera-agent-unity.instructions.md](.github/instructions/hera-agent-unity.instructions.md), [GEMINI.md](GEMINI.md), [.agents/skills/hera-agent-unity/SKILL.md](.agents/skills/hera-agent-unity/SKILL.md).

가장 중요한 규칙:

- 도구 목록은 `list --compact`로 작게 읽기;
- 다음 명령에 오브젝트 ID만 필요하면 `find_gameobjects --ids` 쓰기;
- 씬을 바꾸는 `exec`는 보통 `return null;`로 끝내기;
- 큰 Unity 오브젝트를 그대로 반환하지 않기;
- 에러는 추측하지 말고 `console --type error`로 읽기.

---

## 어떻게 동작하나요?

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
        | Unity 메인 스레드
        v
씬, 콘솔, Play Mode, 에셋, UI
```

Unity 패키지가 작은 로컬 HTTP 서버를 엽니다. CLI가 그 서버에 명령을 보냅니다. 명령은 Unity Editor 안에서 실행됩니다.

구조 자세히 보기: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

---

## FAQ

### MCP인가요?

production 기본값은 일반 CLI이므로 셸 명령을 실행할 수 있는 모든 에이전트가
사용할 수 있습니다. CLI `v0.1.0+`에는 실험적·default-off·stdio-only MCP
adapter도 포함되지만 기본값은 아닙니다. CLI와 localhost Connector 실행 코어를
그대로 사용합니다. [MCP adapter 가이드](docs/MCP.md)를 참고하세요.

### Python이 필요한가요?

아니요.

### 어떤 Unity Editor에 연결되나요?

각 CLI 호출이나 MCP 프로세스는 Unity Editor 하나를 대상으로 합니다. 여러
Editor heartbeat가 있으면 정규화된 전체 프로젝트 경로를 `--project`에 지정하는
방식을 우선하세요. 포트는 `8090`–`8099`에서 선택되는 임시 연결점이라 Editor
재시작이나 domain reload 뒤에 바뀔 수 있습니다. 정확한 프로젝트 경로가 우선하고,
부분 경로는 하나의 프로젝트만 식별할 때만 허용되며, `--project`와 `--port`를
함께 쓰면 둘 다 같은 Editor를 가리켜야 합니다. 선택자가 없으면 현재 작업
디렉터리와 일치하는 프로젝트를 먼저, 그다음 가장 최근의 살아 있는 heartbeat를
선택합니다. 응답 손실이나 timeout 뒤에는 heartbeat 소유권을 다시 확인해 다른
프로젝트가 재사용한 포트로 mutation을 재전송하지 않습니다.

### 연결이 안 되면 어떻게 하나요?

이 명령을 실행하세요:

```bash
hera-agent-unity doctor --json
```

그리고 Unity 패키지가 설치되어 있는지, Unity 컴파일이 끝났는지 확인하세요.

### 자세한 문서는 어디에 있나요?

- [docs/MCP.md](docs/MCP.md)

- [docs/COMMANDS.md](docs/COMMANDS.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/CSHARP_CONNECTOR.md](docs/CSHARP_CONNECTOR.md)
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

---

## Hera를 쓰는 프로젝트

| 프로젝트 | 설명 |
|:---|:---|
| **NoMoreRolls** | AI가 Hera로 Unity Editor를 조작하며 만든 Unity 게임입니다. |

<div align="center">

https://github.com/user-attachments/assets/15d353e4-b7bb-4534-bbca-c27de0792147

<sub><b>NoMoreRolls</b> — Hera로 Unity Editor 작업을 보조하며 만든 Unity 게임의 전체 Play Mode 영상입니다.</sub>

</div>

---

## 제작자

**Victor** — 라이브 서비스 MMORPG 프로덕션 경험 6년 이상의 Unity/C# 개발자.

GitHub: [@NotNull92](https://github.com/NotNull92)

Discord: [Hera 커뮤니티 참여하기](https://discord.gg/QBzEVuYwK)

---

## 후원

Hera는 무료이며 Apache-2.0 라이선스로 제공됩니다. Hera가 시간을 아껴줬다면 개발을 후원할 수 있습니다:

[![Support on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/notnull92)

---

## 라이선스

Apache License 2.0. [LICENSE](LICENSE)와 [NOTICE](NOTICE)를 확인하세요.
