<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.25-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-2022.3%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**AI 코딩 에이전트를 위한 토큰 절약형 Unity Editor 조작 CLI입니다.**

<sub>Codex, Claude, Cursor, Copilot, AntiGravity가 열린 Unity 프로젝트를 직접 확인하고 수정하게 합니다 — MCP 설정 없음, Python 서버 없음.</sub>

<br>

[무엇인가요?](#무엇인가요) · [왜 필요한가요?](#왜-필요한가요) · [바로 시작](#바로-시작) · [설치](#설치) · [명령어](#명령어) · [토큰 절약](#토큰-절약) · [Game Feel Mode (Beta)](#game-feel-mode-beta) · [Game Feel UI Mode (Beta)](#game-feel-ui-mode-beta) · [Ultra Hera](#ultra-hera) · [Unity 버전](#unity-버전) · [AI 규칙](#ai용-규칙-넣기) · [사용 프로젝트](#hera를-쓰는-프로젝트) · [FAQ](#faq)

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

Python 서버도 필요 없습니다. MCP 설정 파일도 필요 없습니다. 특별한 에이전트 플러그인도 필요 없습니다. 셸 명령을 실행할 수 있는 AI라면 Hera를 쓸 수 있습니다.

---

## 릴리스 하이라이트

이번 릴리스의 핵심은 세 가지입니다: 더 많은 Unity 버전, 더 적은 토큰, 더 안전한 AI 검증.

| 하이라이트 | 쉬운 뜻 |
|:---|:---|
| **NEW: Unity 2022.3 LTS 지원** | Unity 6으로 올리지 않아도 쓸 수 있습니다. |
| **NEW: Unity 2023.2 지원** | Unity 6보다 낮은 버전에서도 커넥터와 문서 조회가 동작합니다. |
| **Unity 6000.3 / 6000.5 따로 확인** | Unity 6 안에서도 버전 차이가 있어서 따로 테스트했습니다. |
| **93 토큰 도구 목록** | `list --compact`는 자주 써도 부담이 작습니다. |
| **49-55 토큰 오브젝트 전달** | `find_gameobjects --ids`는 다음 명령에 필요한 ID만 보냅니다. |
| **시그니처: Game Feel Mode (Beta)** | Hera가 AI에게 게임플레이 손맛을 만드는 법을 알려줍니다 — 윤리 원칙 내장. |
| **시그니처: Game Feel UI Mode (Beta)** | Hera가 AI에게 정적인 UI가 아니라 살아 있는 게임 UI를 만드는 힌트를 줍니다. |
| **NEW: Ultra Hera** | 기본은 가볍게 확인하고, 중요한 요청은 더 꼼꼼한 Unity 검증으로 올립니다. |
| **NEW: uGUI 공식문서 fixer** | `ui_doc apply`가 열린 Unity Editor 버전에 맞는 공식 uGUI 규칙을 고르고 fixes/diagnostics를 보고합니다. |

측정한 버전:

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

Unity가 열리면 커넥터가 자동으로 시작합니다.

---

## 명령어

AI가 가장 자주 쓰는 명령어입니다.

| 명령어 | 하는 일 |
|:---|:---|
| `status` | 어떤 Unity Editor에 연결됐는지 보여 줍니다. |
| `doctor --json` | 설치, PATH, Unity 연결을 검사합니다. |
| `list --compact` | 작은 응답으로 도구 목록을 봅니다. |
| `console --type error` | Unity의 실제 에러를 읽습니다. |
| `exec "..."` | Unity 안에서 C#을 실행합니다. |
| `editor play --wait` | Play Mode에 들어가고 기다립니다. |
| `editor stop` | Play Mode를 멈춥니다. |
| `scene info` | 현재 씬 정보를 봅니다. |
| `find_gameobjects` | 열린 씬에서 오브젝트를 찾습니다. |
| `manage_assets` | `Assets/` 아래 프로젝트 에셋을 찾고, 만들고, 복사하고, 옮기고, 삭제합니다. |
| `manage_gameobject` | GameObject를 만들고, 복제하고, 옮기고, 이름을 바꿉니다. |
| `manage_components` | 컴포넌트를 추가, 삭제, 조회, 수정합니다. |
| `ui_doc` | Unity UI를 만들고 캡처합니다. |
| `game_feel` | 게임 필 레시피를 조회합니다 (screen shake, hit stop, honest juice 등). |
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

uGUI fixer는 juice 레시피와 별개입니다. `ui_doc apply`는 항상 공식문서 기반
`fixes` / `diagnostics`를 보고하고, Game Feel UI Mode (Beta)는 선택적으로 game-feel
가이드를 `agent_hint`에 붙입니다.

Unity에서 켭니다:

```text
HeraAgent -> Hera Settings -> Game Feel UI Mode (Beta)
```

같은 Hera Settings 패널에서 DOTween이 켜져 있으면 DOTween 방식의 트윈을 추천합니다. 없으면 coroutine이나 lerp 방식으로 안내합니다.

대표 레시피:

| UI 요소 | Juicy 힌트 |
|:---|:---|
| Button | hover 확대, press 눌림, release bounce, 클릭음, 햅틱. |
| Popup / panel | pop-in 등장, 화면 dim, 빠르고 조용한 퇴장. |
| Text | 줄별 등장, 숫자 카운트업, 떠오르는 데미지 텍스트. |
| Image / reward | pop-in, 희귀도 pulse, glow, hover lift. |
| Bar | 즉시 줄어드는 fill, 늦게 따라오는 chip bar, 낮은 수치 pulse, segment tick. |

자세한 명령 문서: [docs/COMMANDS.md](docs/COMMANDS.md#ui_doc)

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
| 2022.3 LTS | NEW · 지원 | `2022.3.62f2`에서 확인했습니다. |
| 2023.2 | NEW · 지원 | `2023.2.22f1`에서 확인했습니다. |
| 6000.0 - 6000.4 | 지원 | Unity 6입니다. |
| 6000.5+ | 지원 | 필요한 경우 Unity의 새 오브젝트 ID 방식을 사용합니다. |
| 2022.3 미만 | 미지원 | 최소 지원 버전은 Unity 2022.3입니다. |

---

## AI용 규칙 넣기

프로젝트에 Hera 규칙을 넣으면 AI가 추측하기 전에 Hera부터 사용합니다.

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

아니요. 일반 CLI입니다. 그래서 Codex, Claude Code, Cursor처럼 셸 명령을 실행할 수 있는 도구라면 사용할 수 있습니다.

### Python이 필요한가요?

아니요.

### Unity Editor가 여러 개 켜져 있으면요?

`--project`나 `--port`로 고를 수 있습니다.

```bash
hera-agent-unity --project MyGame status
hera-agent-unity --port 8091 status
```

### 연결이 안 되면 어떻게 하나요?

이 명령을 실행하세요:

```bash
hera-agent-unity doctor --json
```

그리고 Unity 패키지가 설치되어 있는지, Unity 컴파일이 끝났는지 확인하세요.

### 자세한 문서는 어디에 있나요?

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

Hera는 무료이며 MIT 라이선스로 제공됩니다. Hera가 시간을 아껴줬다면 개발을 후원할 수 있습니다:

[![Support on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/notnull92)

---

## 라이선스

MIT. [LICENSE](LICENSE)를 확인하세요.
