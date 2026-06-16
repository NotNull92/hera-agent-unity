<div align="center">

<img src="docs/assets/hera_logo.png" width="50%" alt="hera-agent-unity">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent-unity?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent-unity/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go](https://img.shields.io/badge/go-%5E1.24-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Unity](https://img.shields.io/badge/unity-6000.0%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**추측 대신 실측 — AI에게 살아 있는 Editor를 만지게 합니다.**

<sub>Go CLI 하나 · C# UPM 패키지 하나 · 런타임 의존성 0개 · MIT.</sub>

<br>

[설치](#설치) · [퀵 스타트](#퀵-스타트) · [쇼케이스](#쇼케이스) · [목업 → UI](#-목업--라이브-unity-ui) · [명령어](#명령어) · [배치](#배치--시나리오-자동화) · [커스텀 툴](#커스텀-툴) · [구조](#구조) · [FAQ](#faq)

[English](README.md) · **한국어**

</div>

---

## 왜 만들었는가

LLM은 여러분의 프로젝트를 모릅니다. 작년에 학습한 Unity API와 뭉뚱그린 패턴을 기억할 뿐이고, 그 간극은 매주 토큰과 시간으로 메우게 되죠.

**hera-agent-unity**는 바로 그 자리에 끼어듭니다.

AI가 코드를 넘겨짚기 전에 Editor에서 직접 돌려보고 결과를 가져옵니다. AI가 콘솔 에러를 짐작하기 전에 실제 로그를 종류별로 긁어옵니다. AI가 Play Mode 결과를 어림하기 전에 직접 진입해서 끝날 때까지 지켜봅니다. AI가 여러분 Unity 버전에 있지도 않은 API를 지어내기 전에, 살아 있는 어셈블리를 리플렉션으로 들여다봅니다.

중간 다리 같은 건 없습니다. Python도, WebSocket도, JSON-RPC도 없죠. Go 바이너리 하나, localhost HTTP, C# UPM 패키지 하나가 전부입니다. Unity Editor를 켜는 순간, Hera는 이미 그 안에 들어와 있습니다.

Hera는 명령에 답할 뿐, 넘겨짚거나 멋대로 가정하지 않습니다. 여러분의 Unity가 지금 이 순간 어떤 상태인지를 있는 그대로 건네줍니다.

> **추측은 비쌉니다. 실측, 그게 곧 명령입니다.**

```
┌─────────────┐       HTTP        ┌──────────────────┐
│   터미널    │ ◄──────────────► │   Unity Editor   │
│  (1바이너리) │   localhost:8090  │  (자동 시작)     │
└─────────────┘                   └──────────────────┘
```

**작은 Go CLI 하나, C# UPM 패키지 하나, 런타임 의존성 0개.**

> 테스트·TUI·배치 엔진·에셋 설정 레이어가 위에 얹혀 있지만, Unity와 직접 통신하는 엔진은 여전히 가볍습니다.

---

## 쇼케이스

hera-agent-unity를 주력으로 만든 게임 프로토타입 — 맹목적 코드 생성이 아니라, AI 에이전트가 Hera를 통해 살아있는 에디터를 직접 운전했습니다(씬 구성, 컴포넌트 연결, Play Mode 반복).

<div align="center">

<video src="https://github.com/user-attachments/assets/a2b31a46-b60d-4de6-8238-58cb67683388" controls muted loop playsinline width="80%"></video>

<sub><b>NoMoreRolls</b> — Unity 솔로 개발 게임 (3-tier 아키텍처, 9-soul 전투 시스템). Play Mode 캡처.</sub>

</div>

---

## 🎨 목업 → 라이브 Unity UI

> *에이전트에게 레퍼런스 스크린샷이나 HTML 목업을 건네세요. 픽셀 단위로 실측된 진짜 uGUI 계층이 — 어림이 아니라, 만들어지고 검증되어 — 돌아옵니다.*

AI한테 Unity UI를 만들라고 시켜 보면 헤맵니다. HTML/CSS는 빠삭한데, uGUI는 완전히 다른 세계거든요 — `RectTransform` anchor, pivot, stretch offset, 중첩된 `LayoutGroup`, 9-slice 스프라이트. 그래서 모델은 anchor 수학을 넘겨짚고, 다음 해상도에서 깨질 절대 좌표를 박아넣고 — 진짜 문제는 — 자기 결과를 레퍼런스와 **대조할 방법이 없다**는 겁니다. 대충 맞고 픽셀은 틀린 레이아웃을 받아 들고, 오후 내내 값을 손으로 미세조정하게 되죠.

**`ui_doc`는 UI를 에이전트의 Unity 최약점에서 결정론적이고 _스스로 교정하는_ 파이프라인으로 바꿉니다.** 에이전트는 진짜 능숙한 단 하나의 언어 — 컴팩트한 HTML 모양 JSON IR(`ui_doc/2`) — 로 설계하고, Hera가 정확한 Unity 쪽 번역을 수행합니다. 그리고 대부분의 AI 도구가 시도조차 안 하는 루프를 닫죠: 만든 결과를 레퍼런스와 **실측 비교**하고 차이를 고칩니다 — 눈대중하고 넘어가는 대신.

### 추측 대신 실측 루프

재현을 "대충 비슷"이 아니라 *충실하게* 만드는 핵심이고, **사람이 끼지 않은 채** 돕니다:

```
   reference image  (스크린샷 / 목업)
        │
        ▼
   1. sample    레퍼런스 이미지에서 진짜 색을 읽어냄
   2. author    ui_doc/2 IR 작성   (HTML 모양 JSON)
   3. apply     IR이 진짜 uGUI 계층이 됨
   4. capture   만든 그대로 렌더   →  PNG
   5. compare   레퍼런스와 diff 후 IR 수정
        │
        └──────────  capture가 레퍼런스와 맞을 때까지 반복
```

에이전트가 스크린샷에서 *진짜* 색을 읽어내고, UI를 만들고, 만든 그대로(오버레이 캔버스까지) 렌더하고, 타깃과 diff 떠서, 스스로 고칩니다.

### 5개 엔드포인트, 하나의 계약

| 엔드포인트 | 실행 위치 | 설명 |
|---|---|---|
| `export`     | 커넥터 | 라이브 UI 서브트리 → `ui_doc/2` IR 직렬화(기본값 생략). 에이전트가 구조를 지어내는 대신 프로젝트의 **실제** 구조 위에 설계를 올림. |
| `apply`      | 커넥터 | `--parent` 아래에 IR 빌드. `--mode create`(기본) 또는 **`upsert`**(이름으로 자식 매칭, **제자리** 편집 — export → edit → apply 왕복 완성). doc은 `--file`로 전달되어 에이전트 컨텍스트를 부풀리지 않음. |
| `gen_sprite` | 커넥터 | 절차적 스프라이트(`solid` / `rounded_rect` / `gradient` / `nine_slice`)를 베이크해 `Sprite`로 import — **외부 의존성 0**, 이미지 모델도, 에셋 팩도 없음. |
| `capture`    | 커넥터 | 라이브 UI 렌더 → PNG(시각 diff용). 일반 `screenshot`이 카메라 *이후* 합성해 조용히 누락하는 `ScreenSpaceOverlay` 캔버스까지 포함. |
| `sample`     | CLI     | 레퍼런스 이미지에서 **측정된** hex 색 읽기(`--at "x,y"` 포인트 / `--region "x,y,w,h"`, 정규화 [0,1]). 순수 stdlib 디코드 — Unity 왕복 없음, 오브젝트가 하나도 없는 상태에서도 사용 가능. |

### 스크린샷 한 장으로 게임 HUD 재구축

```bash
# 1 ─ 프로젝트의 실제 계층에 grounding (구조를 넘겨짚지 않기)
hera-agent-unity ui_doc export --path /Canvas/HUD

# 2 ─ 레퍼런스 아트에서 진짜 색 뽑아내기
hera-agent-unity ui_doc sample --image hud_ref.png --region "0,0,1,0.2"

# 3 ─ HTML 모양 JSON으로 IR 작성 후, Canvas 아래에 빌드
hera-agent-unity ui_doc apply --file hud.json --parent /Canvas --mode upsert

# 4 ─ 만든 그대로 렌더 → 레퍼런스와 비교 → 수정 → 반복
hera-agent-unity ui_doc capture --out hud_built.png
```

억지로 만든 데모가 아닙니다 — `capture`가 레퍼런스와 맞을 때까지 스크린샷 한 장으로 실제 게임 HUD를 재구축하며 **도그푸딩으로 탄생한** 기능입니다.

### 왜 실제로 통하는가

- **에이전트의 모국어로 설계.** 모델이 자주 틀리는 raw `m_AnchoredPosition` 수학이 아니라, HTML 모양 노드 트리.
- **절대 좌표가 아닌 진짜 레이아웃 시스템.** IR(`ui_doc/2`)이 `Horizontal` / `Vertical` / `Grid` `LayoutGroup`, `LayoutElement`, `ContentSizeFitter`, Image **fill** 모드(`fill.amount`로 만드는 정석 progress / HP 바), **9-slice** border, stretch **offset**, 텍스트 **color / align / font**를 담습니다. 설계상 반응형.
- **의존성 0 아트.** `gen_sprite`가 텍스처를 절차적으로 베이크 — `com.unity.vectorgraphics`도, 외부 이미지 생성기도, 설치할 것도 없음.
- **보이는 그대로 본다.** `capture`가 일반 스크린샷이 놓치는 오버레이 캔버스를 합성하므로 diff가 정직합니다.
- **어떤 프로젝트에서도 컴파일.** 모든 uGUI / TextMeshPro 타입이 `TypeCache`로 해석 — 커넥터는 `com.unity.ugui`에 **컴파일타임 의존성 0**을 유지.

전체 IR 레퍼런스: [`docs/UI_DOC_IR.md`](docs/UI_DOC_IR.md) · 모든 플래그: [`docs/COMMANDS.md`](docs/COMMANDS.md#ui_doc).

> `ui_doc`는 UI를 **제대로** 만듭니다. 아래 [**UI Juicy Mode**](#-ui-juicy-mode--그냥-작동하는-게-아니라-살아있는-ui)는 그걸 **살아있게** 만들고요 — 켜면 매 `apply`가 요소 타입별 Game UI/UX Bible juice 레시피를 `agent_hint`로 함께 실어 보냅니다.

---

## ✨ UI Juicy Mode — 그냥 작동하는 게 아니라 *살아있는* UI

> *동작하는 버튼과 누르는 맛이 있는 버튼의 차이. 이제 AI 에이전트도 그 차이를 압니다.*

AI가 UI를 만들면 작동은 하는데 — 죽어 있습니다. 가만히 있죠. 마우스를 올려도 반응이 없고, 클릭하면 툭 끊깁니다. 점수는 `0`에서 `1000`으로 한 프레임에 점프하고요. 기술적으로는 맞지만, 감각적으로는 밋밋합니다.

진짜 게임에는 *눈치채기보다 느끼게 되는* 작고 만족스러운 디테일이 가득합니다. 커서가 닿으면 살짝 부풀고, 누르면 찌그러졌다가, 통통 튀며 돌아오는 버튼. 깜빡 나타나는 게 아니라 *팡* 하고 튀어나오는 팝업. 위로 굴러 올라가는 점수. 떠오르며 사라지는 데미지 숫자. 디자이너들은 이걸 **"juice"**(혹은 *game feel*)라고 부릅니다 — 그리고 AI가 만든 UI에서 가장 먼저 빠지는 게 바로 이겁니다. 모델이 애초에 더할 생각을 안 하니까요.

**UI Juicy Mode는 그 기본값을 뒤집습니다.** 켜두면, 에이전트가 UI 요소를 만들 때마다 Hera가 그 요소를 살아있게 만드는 — *Game UI/UX Bible*에서 그대로 가져온, 숫자까지 박힌 구체적인 레시피를 함께 건넵니다. 그러면 에이전트가 애니메이션·사운드·피드백을 알아서 연결해 줍니다. 디자인 브리프도, 핑퐁도, "좀 더 다듬어진 느낌으로 해줄래?"도 필요 없습니다 — 그냥 juicy하게 나옵니다.

### 죽은 UI vs. Juicy한 UI

| | **Off** (기본) | **On** |
|---|---|---|
| **버튼** | 정적인 사각형. 클릭은 되는데 아무것도 안 움직임. | hover에 110%로 부풀고, press에 95%로 눌리고, 오버슈트로 통통 튀어 돌아오고, 클릭음 + 모바일 진동. |
| **팝업** | 즉시 뿅 나타남. | 오버슈트로 팡 하고 커지며 등장, 뒤 화면은 어둡게 딤 처리. |
| **점수 / HP / 골드** | `120 → 999` 툭 끊김. | 0.25초에 걸쳐 부드럽게 카운트업, 마지막 값에서 살짝 pop. |
| **데미지 숫자** | 텍스트 떴다가 사라짐. | 크게 펀치인 → 50px 떠오름 → 사라지며 페이드. 크리티컬은 더 크게 + 화면 흔들림. |

### 동작 방식

이건 **가이드일 뿐, 군더더기가 아닙니다.** Hera는 무거운 런타임 컴포넌트를 씬에 붙이지 않습니다. 대신 레시피가 `manage_ui create` 응답 안에 `agent_hint`로 실려 에이전트에게 돌아가고 — 에이전트가 평소대로 `manage_components` / `exec` 경로로 적용합니다. 커넥터는 Editor 전용으로 남고, 빌드는 깨끗하게 유지됩니다.

```jsonc
// hera-agent-unity manage_ui create button --name PlayButton   (Juicy Mode 켠 상태)
{
  "instance_id": -8420,
  "agent_hint": "[Hera] UI Juicy Mode is on — make this feel alive (Game UI/UX Bible).\n
    Button feel (Normal → Hover → Press → Release):\n
      - Hover:   100% → 110%, EaseOut 0.15s, +5% brightness\n
      - Press:   → 95%, EaseOut 0.05s (immediate), −10% color\n
      - Release: 95% → 110% → 100% with Back overshoot, 0.2s; click SFX; 10ms haptic on mobile\n
      - Disabled: desaturate ~50%, opacity 60–70%, block interaction\n
    Tweening: DOTween is enabled — use rt.DOScale(1.1f, 0.15f).SetEase(Ease.OutQuad) …"
}
```

모든 레시피는 **구체적입니다** — 정확한 스케일 퍼센트, 이징 커브, 초 단위 타이밍 — 그래서 에이전트가 추측 대신 실제 수치를 적용합니다. 요소 종류마다 맞춤 레시피가 있습니다:

| 요소 | 배우는 동작 |
|---|---|
| **버튼** | hover / press / release 상태머신 + 오버슈트, 클릭 SFX, 모바일 햅틱, disabled 스타일 |
| **패널 / 팝업** | 느리고 과장된 등장(pop-in), 화면 딤, 빠르고 조용한 퇴장 |
| **이미지** | 등장 시 pop-in, 희귀도 비례 보상 펄스 + glow, 피격 시 flash(흰색 + 트윈 중단 + 넉백), hover lift |
| **바** (filled image) | 체력/진행 바의 시그니처 juice — 즉시 fill 감소 + 지연된 "chip" 바 따라잡기, 낮은 값 위험 펄스, 세그먼트 틱 |
| **텍스트** | 줄별 스태거 등장, 숫자 카운트업(연속 콤보 시 상승 피치 SFX), 떠오르는 데미지 팝업 |
| **컨테이너** | 자식 요소 스태거 등장, 툭 끊지 않고 애니메이션되는 레이아웃 변화 |
| **캔버스** | 트윈 + 오디오 인프라 세팅, 큰 임팩트에 hit-pause, 모든 전환 애니메이션, UI 점유 면적 슬림하게 유지 |

**툴체인에 맞춰 적응합니다.** Hera Settings에서 DOTween이 켜져 있으면 레시피가 `DOScale` 계열 트윈(프로젝트 표준)을 쓰라고 안내하고 — 아니면 어떤 프로젝트에서도 동작하는 coroutine/lerp 방식으로 폴백합니다. 그리고 강한 모션은 항상 reduce-motion 옵션 뒤에 두라고 에이전트에게 상기시켜서, juice가 멀미가 되지 않게 합니다.

### 켜는 법

**옵트인**입니다 — 기본은 꺼져 있고, 토글 한 번이면 됩니다:

```bash
hera-agent-unity asset-config juicy on      # 켜기
hera-agent-unity asset-config juicy off     # 끄기
```

또는 Unity 안의 Hera Settings 창에서 **UI Juicy Mode**를 체크하세요. 그게 전부입니다 — 그때부터 에이전트가 만드는 모든 UI 요소가 살아서 나옵니다.

> **Maximum output for minimum input.** 당신은 "플레이 버튼 추가해줘"라고만 말합니다. 에이전트는 진짜 게임이 만든 것 같은 버튼을 내놓습니다.

---

## 설치

### CLI

**macOS / Linux**
```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.sh | sh
```

**Windows** (PowerShell)
```powershell
irm https://raw.githubusercontent.com/NotNull92/hera-agent-unity/main/install.ps1 | iex
```

<details>
<summary>다른 설치 방법</summary>

**`go install`** (모든 플랫폼)
```bash
go install github.com/NotNull92/hera-agent-unity@latest
```

**수동 설치** — [Releases](https://github.com/NotNull92/hera-agent-unity/releases)에서 플랫폼에 맞는 바이너리를 받은 뒤, 한 번 실행해서 PATH에 등록합니다:

```bash
chmod +x ./hera-agent-unity-<platform>
./hera-agent-unity-<platform> install
```

</details>

### Unity Connector

**Package Manager → Add package from git URL**
```
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

또는 `Packages/manifest.json`에 직접 추가:
```json
"com.notnull92.hera-agent-unity": "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector"
```

> 커넥터는 자동으로 시작합니다. 별도 설정 없음. Unity 6 (6000.0+)이 필요합니다.

### 호환 Unity 버전

| Unity 버전 | 상태 | 비고 |
|---|---|---|
| **6000.0 – 6000.4** | ✅ 지원 | Unity 6 (LTS + Tech stream) |
| **6000.5+** (베타 포함, 예: `6000.5.0b11`) | ✅ 지원 | **Connector 0.0.20+** 필요. Unity 6000.5가 기존 `EditorUtility.InstanceIDToObject` / `Object.GetInstanceID` API를 컴파일 에러로 승격(새 `EntityId` API로 대체)했고, 커넥터가 `UNITY_6000_5_OR_NEWER` 게이트로 자동 대응합니다. |
| **2022.x 이하** | ❌ 미지원 | 최소 Unity 6 (`6000.0`). |

> **Unity 6000.5+에서 구버전 커넥터의 증상:** **HeraAgent** 메뉴가 안 보이고 `hera-agent-unity status`가 인스턴스 없음으로 나옵니다(어셈블리 컴파일 실패 → 메뉴·HTTP 서버 미등록). 해결: 커넥터를 **0.0.20+**로 갱신 — Package Manager에서 패키지 업데이트하거나, git 패키지를 재해석(`Packages/packages-lock.json`의 해당 엔트리 삭제 후 에디터 포커스)한 뒤 재컴파일하세요.

---

## 퀵 스타트

이 CLI에서 사람이 직접 칠 명령은 사실상 네 개뿐입니다. 나머지 — `exec`, `editor`, `console`, `scene`, `batch`, `profiler`, `describe_type`, … — 는 운전대를 넘겨받은 **AI 에이전트**가 여러분 대신 알아서 호출하죠.

```bash
# 1. CLI 설치 (한 번만, 위 Installation 참고)
# 2. AgentConnector UPM 패키지가 설치된 Unity를 엽니다 — heartbeat가 뜸

# Unity 연결됐나?
hera-agent-unity status

# 이후 유지보수 차원에서:
hera-agent-unity update           # 최신 CLI 릴리스 받기
hera-agent-unity uninstall        # PATH에서 제거
```

**사람**이 할 일은 여기까지입니다 — 설치하고, 연결 한 번 확인하고, 가끔 업데이트. 진짜 작업은 다음 섹션부터예요. 에이전트한테 트리거 한 줄만 던지면 나머지는 알아서 굴러갑니다.

---

## AI 에이전트에게 운전대 넘기기

Claude Code, Codex, Cursor — 셸 명령을 돌릴 수 있는 에이전트면 뭐든 됩니다. 이렇게 한마디만 던지세요:

> **"hera-agent-unity CLI 도구 설치돼 있는지 확인하고 어떤 기능 있는지 파악해."**

에이전트가 알아서 CLI를 발견하고, `list`를 돌려보고, Unity를 조종하기 시작합니다.

### 호환성

hera-agent-unity는 JSON을 뱉는 평범한 CLI일 뿐입니다. 셸 명령을 돌릴 수 있는 코딩 에이전트면 무엇이든 붙습니다. 요즘 생태계는 **`AGENTS.md`(프로젝트 루트)** 를 멀티 툴 공통 규칙 파일로 모아가는 분위기니, 여기서 시작하면 됩니다.

| 에이전트               | 캐노니컬 경로                                  | 템플릿                                                    | 비고                                                        |
|------------------------|-----------------------------------------------|-----------------------------------------------------------|-------------------------------------------------------------|
| **OpenAI Codex** + AGENTS.md 인식 도구 | `AGENTS.md` (프로젝트 루트)         | [`examples/rules/AGENTS.md`](examples/rules/AGENTS.md)    | 크로스 툴 표준. 여기서 시작.                                 |
| **Claude Code CLI**    | `CLAUDE.md` (또는 `AGENTS.md`)                  | [`examples/rules/CLAUDE.md`](examples/rules/CLAUDE.md)    | `CLAUDE.md` 우선; `AGENTS.md` 인식도 확장 중.                |
| **Kimi Code CLI**      | `AGENTS.md` (프로젝트 루트)                     | [`examples/rules/AGENTS.md`](examples/rules/AGENTS.md)    | `AGENTS.md` 네이티브 인식 (`/init`로 생성). Agent Skills도 지원. |
| **Cursor**             | `.cursor/rules/hera-agent-unity.mdc`          | [`examples/rules/cursor.mdc`](examples/rules/cursor.mdc)  | 룰별 분리 파일 + YAML frontmatter 필수. `.cursorrules`는 **deprecated**. |
| **GitHub Copilot**     | `.github/copilot-instructions.md`             | [`examples/rules/copilot-instructions.md`](examples/rules/copilot-instructions.md) | 옵션: `.github/instructions/*.instructions.md`에 `applyTo` frontmatter로 파일 패턴별 지침 가능. |
| **Continue.dev**       | `.continuerules`                              | [`examples/rules/continuerules`](examples/rules/continuerules) | Plain markdown.                                             |
| **Google Antigravity** | `GEMINI.md` (또는 `AGENTS.md`)                  | [`examples/rules/GEMINI.md`](examples/rules/GEMINI.md)    | `GEMINI.md`가 `AGENTS.md`보다 우선. 온디맨드 스킬 `.agents/skills/hera-agent-unity/SKILL.md`. |

여러 툴을 같이 쓴다면, **`AGENTS.md` 하나를 원본**으로 두고 나머지 도구별 경로에는 한 줄짜리 stub(`> See AGENTS.md.`)만 남기는 게 제일 깔끔합니다. Cursor만 예외예요 — `.mdc`는 frontmatter가 있어야 룰이 켜지기 때문에 본문을 통째로 넣어둬야 합니다.

### 프로젝트당 1회 세팅 (강력 권장)

**정적 방식** — 본인 에이전트에 맞는 템플릿을 복사:

```bash
cp examples/rules/AGENTS.md <Unity 프로젝트>/AGENTS.md
cp examples/rules/cursor.mdc <Unity 프로젝트>/.cursor/rules/hera-agent-unity.mdc
```

**동적 방식** — CLI가 규칙 본문을 바로 뽑아 규칙 파일에 꽂아 줍니다:

```bash
# AGENTS.md / CLAUDE.md / Copilot / Continue.dev — plain markdown
hera-agent-unity doctor --agent-rules >> AGENTS.md

# Cursor — frontmatter 자동 prepend
hera-agent-unity doctor --agent-rules --format cursor > .cursor/rules/hera-agent-unity.mdc
```

어느 방식이든 핵심 지시와 자동 부트스트랩 절차가 함께 들어갑니다. 한 번 깔아두면 "hera-agent-unity 찾아봐" 한마디에 에이전트가 알아서 `doctor` + `status`를 돌리고 한 줄로 보고하죠 — 더 캐물을 것도 없이.

> **Cursor 주의** — Cursor의 `.mdc` 룰 파일은 **YAML frontmatter**(`description`, `globs`, `alwaysApply`)가 없으면 파싱은 되지만 룰이 활성화되지 않습니다. 템플릿을 쓰거나 `--format cursor` 플래그를 쓰세요 — plain markdown만 붙여놓으면 조용히 무시됩니다.

---

## 명령어

대상별로 묶었습니다. 각 항목의 자세한 플래그는 `hera-agent-unity <cmd> --help`로, 등록된 모든 도구(커스텀 포함)는 런타임에 `list`로 확인하세요.

### Editor & 런타임

| 명령어 | 설명 |
|---|---|
| `editor` | Play / stop / pause / refresh / recompile. |
| `exec`   | Unity 내부에서 C# 코드 실행 — 에디터 + 런타임 풀 액세스. |
| `log`    | csc 컴파일 없이 Unity 콘솔에 메시지 출력. |

### Scene & GameObject

| 명령어 | 설명 |
|---|---|
| `scene`              | 정보 / 로드 / 저장 / 목록 / 닫기. |
| `manage_gameobject`  | 생성 / 파괴 / 이동 / 부모 변경 / 활성 토글 / 이름 변경 / 트랜스폼 조회. |
| `manage_components`  | GameObject 의 component CRUD: `add` / `remove` / `list` / `get` / `set`. property 경로는 raw `SerializedProperty` 경로. |
| `find_gameobjects`   | 씬 GameObject 필터 (이름 / 태그 / 레이어 / 컴포넌트 / 경로 glob) + 페이지네이션. |
| `manage_prefab`      | 프리팹 에셋: `create`(GameObject → 프리팹) / `instantiate` / headless `add_component` / `remove_component`. |
| `manage_ui`          | uGUI 저작: `create`(UI 요소 + Canvas/EventSystem 자동 구성) / `get_rect` / `set_anchor`(명명 프리셋 그리드) / `set_rect`. raw `m_` 경로 없이 RectTransform anchor/pivot 수학 처리. **UI Juicy Mode** 켜면 `create` 가 DOTween-aware Game UI/UX Bible juice 레시피를 `agent_hint` 로 반환. |
| `ui_doc`             | HTML→Unity UI 파이프라인(uGUI): `export`(라이브 서브트리 → 컴팩트 `ui_doc/2` JSON, grounding 용) / `apply`(IR → UI; `--mode create` 또는 `upsert`로 기존 제자리 갱신; doc 은 `--file` 로) / `gen_sprite`(Tier-1 절차적 스프라이트 — solid/rounded_rect/gradient/nine_slice — 베이크 + import, 외부 의존성 0) / `capture`(라이브 오버레이 UI → PNG 렌더, 시각 검증용) / `sample`(레퍼런스 이미지에서 측정된 hex 색 읽기 — 포인트/영역, Unity 왕복 불필요). Juicy Mode 켜면 `apply` 의 `agent_hint` 에 juice 레시피가 요소 타입별로 dedup 되어 실림. |

### 에셋 · 머티리얼 · 셰이더

| 명령어 | 설명 |
|---|---|
| `manage_material`     | 머티리얼 에셋 CRUD: `create`(셰이더 지정) / `get` / `set`(프로퍼티 1개) / `set_shader`. |
| `manage_asset_import` | 에셋의 import 설정을 `AssetImporter`로 읽기/쓰기 후 reimport. |
| `describe_shader`     | 셰이더 프로퍼티(이름 / 타입 / range) 조회 또는 셰이더 이름 검색(`--list`). |

### Packages

| 명령어 | 설명 |
|---|---|
| `manage_packages` | `list`(동기) / `add` / `remove` / `embed`(비동기 — `job_id` 발급 후 결과 파일 폴링). |

### Console · 테스트 · 캡처

| 명령어 | 설명 |
|---|---|
| `console`     | 로그 읽기, 필터, 삭제. |
| `test`        | EditMode / PlayMode 테스트 실행. |
| `menu`        | 경로로 메뉴 항목 실행. |
| `screenshot`  | Scene 또는 Game 뷰 캡처. |
| `profiler`    | 프로파일러 계층 읽기, 녹화 제어. |
| `reserialize` | 텍스트 편집 후 Unity YAML 강제 재직렬화. |

### 인트로스펙션

| 명령어 | 설명 |
|---|---|
| `describe_type`   | 라이브 타입 리플렉션 — 멤버 / 시그니처 / **Unity 함정** + Manual 링크. |
| `find_method`     | 로드된 어셈블리 전반에서 메서드 이름 검색. |
| `list_assemblies` | 로드된 어셈블리 **이름** 목록 (`System.*` 제외; `--include_version` / `--include_location` 로 보강). |
| `unity_docs`      | 오프라인 Unity ScriptReference 조회 — 최소 `title / signature / summary` 응답 (~33 토큰). |

### 워크플로우

| 명령어 | 설명 |
|---|---|
| `batch` | 여러 명령을 한 HTTP 라운드트립에 원자적으로 실행. |
| `list`  | 등록된 도구 목록 — 이름만(`--names`) / 이름+설명(기본) / `--tool <name>` 전체 스키마. |

### 상태 · 유지보수

| 명령어 | 설명 |
|---|---|
| `status`       | 연결 상태 및 프로젝트 정보. |
| `ping`         | 토큰 절약형 라이브니스 프로브 (heartbeat 파일만 읽음 — HTTP 없음). |
| `doctor`       | 셀프 진단: PATH / 설치 / 셸 / Unity 도달성 (`--json` / `--agent-rules` 옵션). |
| `asset-config` | 옵션 에셋 통합 + **UI Juicy Mode** 토글 (TUI / `list` / `enable` / `disable` / `juicy on\|off` / `detect`). |
| `update`       | GitHub Releases에서 셀프 업데이트. |
| `install` / `uninstall` | PATH에 바이너리 등록 / 제거. |

막혔으면 `hera-agent-unity doctor`, 또는 [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

---

## What's New (post-v0.0.6 capability queue)

2026년 5월 28일에 확정한 5개 도구 큐를 전부 마무리했습니다. 하나같이 *`exec`의 csc 웜업 비용*이 크거나 *Unity API 호출을 C#으로 일일이 감싸는 구문 부담*이 커서, AI 에이전트 워크플로우가 영 매끄럽지 못했던 영역들이죠.

| 도구 | Connector | 무엇을 하나 |
|---|---|---|
| `manage_gameobject` | v0.0.5 | 생성 / 파괴 / 이동 / 부모 변경 / 활성 토글 / 이름 변경 / 트랜스폼 조회. shallow `instance_id` 반환은 rename / reparent 후에도 안정. |
| `manage_packages`   | v0.0.6 | `Client.Add` / `Remove` / `Embed` / `List` 드라이버. 비동기 작업(`job_id`)은 resolver 의 도메인 리로드를 `[InitializeOnLoad]` 워처 + 리로드 후 `Client.List` 검증으로 통과. |
| `find_gameobjects`  | v0.0.7 | 씬 GameObject 를 이름 / 태그 / 레이어 / 컴포넌트 / 경로 glob 로 필터, hierarchy 정렬 결과 위에 stable pagination. |
| `manage_components` | v0.0.8 | raw `SerializedProperty` 경로(`m_Mass`, `m_Materials.Array.data[0]`)로 컴포넌트 CRUD. 참조 필드는 InstanceID / asset path / `{instance_id\|asset_path}` envelope 셋 다 수용. 이후 모든 `manage_*` 가 재사용할 property-set 패턴 정립. |
| `unity_docs`        | v0.0.10 → **v0.0.12** | 오프라인 Unity 6 ScriptReference 조회. **31,581 entries 가 1.2 MiB gzipped JSONL 로 UPM 패키지 안에 포함** — 사용자 PC 의 docs 폴더 불필요, 네트워크 불필요, rate limit 없음. |
| `describe_shader` · `manage_material` · `manage_prefab` · `manage_asset_import` | **v0.0.14** | 자산 편집 묶음. `describe_shader`(셰이더 프로퍼티 조회/검색) → `manage_material`(머티리얼 CRUD, `SerializedPropertyValue` 재사용) / `manage_prefab`(headless `LoadPrefabContents` 편집) / `manage_asset_import`(`AssetImporter` import 설정, manage_components 패턴). |
| `manage_ui` | **v0.0.15** | uGUI 저작. `create` 가 UI 요소(canvas / panel / image / button / text / empty)를 Canvas + EventSystem 자동 구성과 함께 생성; `set_anchor` 는 Unity 명명 앵커 프리셋 그리드를 노출하고 rect 를 시각적으로 고정 유지(또는 `--snap` 으로 Alt+Shift 채움); `get_rect` / `set_rect` 로 RectTransform 편집 완성. UI/TMP 타입은 `TypeCache` 로 해석 → com.unity.ugui 없는 프로젝트에서도 커넥터 컴파일. 요소 프로퍼티 편집은 `manage_components` 담당. |
| `TargetResolver` + 테스트 + 벤치마크 | **v0.0.16** | `TargetResolver`가 `manage_components` / `manage_gameobject` / `manage_ui`의 공통 GameObject/Component 리졸루션을 추출. Go 테스트 커버리지 확대(`doctor`, `install`, `poll`, `assetconfig`). C# `HierarchyPath` 에디터 테스트. 스모크 테스트 벤치마크: 7회 호출 = 725B (~181T), 평균 26T/호출 — 리팩토링으로 토큰 비용 증가 없음 확인. |
| UI Juicy Mode | **v0.0.19** | AI가 만든 UI를 죽은 게 아니라 살아있게 만드는 옵트인 토글 — 켜면 `manage_ui create` 가 Game UI/UX Bible "juice" 레시피를 반환. 위의 전용 [**✨ UI Juicy Mode**](#-ui-juicy-mode--그냥-작동하는-게-아니라-살아있는-ui) 섹션 참고. |
| Unity 6000.5 호환 | **v0.0.20** | `Core/EntityIdCompat` shim(`UNITY_6000_5_OR_NEWER` 게이트)이 27개 호출부를 6000.5+ 에선 새 `EntityId` API 로, 6000.0–6000.4 에선 기존 `InstanceID` API 로 라우팅. Unity 6000.5 가 구 API 를 컴파일 에러로 승격 — shim 없으면 커넥터가 조용히 컴파일 실패하고 **HeraAgent 메뉴가 사라짐**. |
| `ui_doc` | **v0.0.21 → v0.0.27** | HTML→Unity UI 파이프라인(uGUI) — 에이전트의 최약점 영역. 컴팩트 JSON IR(`ui_doc/2`) 중심으로 `export` / `apply`(`create` + `upsert`) / `gen_sprite` / `capture` / `sample`. 풀 레이아웃 시스템(Horizontal / Vertical / Grid `LayoutGroup`, `LayoutElement`, `ContentSizeFitter`), Image fill 모드 + 9-slice, stretch offset, 텍스트 color / align / font, *추측 대신 실측* 검증 루프까지 성장. 외부 의존성 0; 여전히 `com.unity.ugui` 컴파일타임 링크 없음. 플래그십 [**🎨 목업 → 라이브 Unity UI**](#-목업--라이브-unity-ui) 섹션에서 루프 전체를 설명합니다. |
| 성능 & 신뢰성 | **v0.0.28** | 보수적 패스: 시작 시 VBCSCompiler pre-warm, `refs-meta.json` 참조 캐시(전체 AppDomain 스캔 생략), 인메모리 어셈블리 캐시 32 → 128, 타입별 `Serialize` 리플렉션 캐시, 알려진 경로 우선 컴파일러 탐지. CLI: `batch` nil-pointer 수정, compact/quiet `batch` 출력, `SendBatch` 의 도메인 리로드 재시도 재사용, 50 MB 응답 가드. 사용자 기본값 변경 없음. |
| `exec` macOS / Linux | **v0.0.31** | 스니펫 컴파일러가 Windows-PE `csc.exe` 를 macOS / Linux 에서 Unity 내장 **Mono** 호스트로 실행(매니지드 PE 는 직접 exec 불가) — `EXEC_LAUNCH_FAILED` 로 실패하던 것을 해결. `defaultCscPath` → `csc.exe` 설정이 exec 를 PE 로 향하게 한 케이스 대응. Windows 경로는 그대로. |
| `exec` Windows / Unity 6.5 | **v0.0.32 → v0.0.34** | 비-Latin(한국어 / 일본어 / 중국어) Windows + Unity 6.5+ 에서 `exec` 가 **아무것도 컴파일 못 하던** 문제 해결 — 모든 스니펫이 `System.Text.Encoding.CodePages` 로드 실패와 함께 `EXEC_COMPILE_ERROR`. Unity 6.5 가 SDK Roslyn 을 `DotNetSdk/sdk/<version>/Roslyn/bincore/csc.dll` 로 옮겼는데, resolver 가 번들 **Mono `csc.exe`** 로 단락 → CP949 콘솔에서 크래시. 이제 `csc.dll` 우선(재귀, 버전 무관) + `-utf8output` + UTF-8 BOM 강제 + stale Mono `csc.exe` `defaultCscPath` 무시. 6.3 회귀 없음 검증. |
| 발견 토큰 절감 | **v0.0.35** | 에이전트 발견 표면 경량화: `list` 기본이 도구별 JSON 스키마 제거(~−73%), `list --names` 는 bare 이름 배열(~−95%), `list_assemblies` 는 기본 bare 이름(`--include_version` 으로 보강, ~−49%), `find_method` / `describe_type` 는 중복 `is_static` 제거(시그니처가 이미 `static` 인코딩). |
| 도구 에러 중복 제거 | **CLI v0.0.24** | 실패한 도구 명령이 메시지를 두 번 출력 — compact JSON envelope + human `Error: command failed: …` 라인. 이제 AI-타겟 명령은 JSON 만 출력해 에러 토큰 절반. |
| `editor refresh --compile` 바운드 | **CLI v0.0.25** | 리컴파일 대기가 숨은 5분 캡을 쓰고 `--timeout` 을 무시 → 래핑 에이전트(Claude Code 120 초 bash 예산)가 돌아가는 프로세스를 백그라운드로 전환. 이제 `--timeout`(기본 60 초; 큰 프로젝트는 올림) 존중 + 타임아웃을 compile 에러와 구분 보고. |

### `unity_docs` — 설계 + 벤치마크 (v0.0.12)

구조 자체는 여느 "RAG 스타일" 조회 도구와 다르지 않습니다 — query → keyed retrieval → 최소한의 context만 에이전트에 반환. 다만 데이터셋이 워낙 작아서(~10 MiB HTML → 1.2 MiB gzipped JSONL) *커넥터 안에 통째로 박아넣었습니다*. 임베딩 모델도, vector DB도, 외부 API도 없습니다. `Dictionary<string, Entry>` 하나가 Unity 6 ScriptReference 전체를 담고, 게으른 3단계 pre-filter가 miss일 때의 "did you mean" 비용을 싸게 유지하죠.

**빌드 경로** (Unity 버전당 한 번, maintainer 실행):

```bash
go run ./tools/build-unity-docs \
    --in  <path-to-Documentation/en> \
    --out AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes
```

이 Go 스크립트는 커넥터가 *매 호출마다* 쓰던 것과 똑같은 정규식 세트(`h1.heading inherit`, `signature-CS sig-block`, `h3 Description`, `switch-link` 앵커, Unity 버전)를 적용해 정렬된 JSONL을 뽑아냅니다. 출력 경로에 `.gz`가 들어 있으면 gzip도 자동으로 걸리고요. 이렇게 커밋된 결과물이 UPM 패키지에 실려 모든 CLI 설치본에 그대로 따라갑니다.

**런타임 경로** — 커넥터는 `unity_docs` 첫 호출 때 번들을 게으르게 로드하고, 그다음부터는 전부 dict O(1) 조회 + miss일 때만 Levenshtein fallback입니다:

| 경로 | Connector `total_ms` |
|:---|:---:|
| Happy lookup (dict hit) | **< 1 ms** |
| Miss in-bucket — `Rigidbod` (1글자 typo) | 2 ms |
| Miss in-bucket — `MonoBehavior` (2글자 typo) | 4 ms |
| Miss with bucket fallback — `Wigidbody` (첫 글자 typo) | 2 ms |
| Miss no-match — `XyzNonsenseAbc` | 5 ms |

콜드 스타트(gzip 해제 + 31,581개 entry JSONL 파싱 + dict·prefix-bucket 빌드)는 ~1.7초인데, Unity 도메인마다 딱 한 번뿐입니다. 도메인 리로드가 일어나면 상태가 초기화되고, 다음 호출 때 다시 빌드되죠.

터미널에서 보이는 CLI 왕복 220–280ms는 **Go 바이너리 기동 + HTTP 왕복 + Unity 큐 디스패치**가 합쳐진 값으로, 모든 hera 도구가 똑같이 치르는 비용입니다 — `unity_docs`만의 얘기가 아니에요.

**응답 shape 의도적으로 최소화:**

```json
{ "title": "Rigidbody.mass",
  "signature": "public float mass;",
  "summary": "The mass of the rigidbody." }
```

응답 하나당 100–185 bytes, 에이전트 입장에선 약 33 input token입니다. 호출하는 쪽이 이미 알거나 충분히 유추할 수 있는 `query_normalized`, `scriptreference_url`, `unity_version` 같은 필드를 덜어내서, verbose 형태 대비 약 53% 줄었습니다.

**Miss 경로 스캔**은 비싼 Levenshtein DP 테이블을 돌리기 전에 값싼 3단계 pre-filter를 먼저 겁니다:

1. **Prefix bucket** — 키를 첫 글자(소문자) 기준으로 묶어둡니다(게으르게, `EnsureLoaded` 직후 한 번만 빌드). 가운데 글자만 틀린 오타라면 정확한 bucket에 걸려서 전체의 ~1/26만 훑으면 됩니다.
2. **Length filter** — `abs(len(a) - len(b)) > maxDistance`면 edit distance의 하한을 이미 넘는 셈이라, 길이 차가 큰 쌍은 DP 테이블도 안 만들고 바로 버립니다.
3. **Bounded Levenshtein** — DP 스캔 도중 한 행의 최솟값이 budget을 넘으면 곧장 중단하고, 호출하는 쪽이 비교 한 번으로 갈라낼 수 있도록 `maxDistance + 1` sentinel을 돌려줍니다.

bucket 결과가 0이면(첫 글자가 틀린 오타) 전체 키 집합으로 fallback합니다 — 위 표의 `Wigidbody` 행이 바로 그 경로가 발동하는 경우인데, 그래도 2ms 안에 `Rigidbody, Rigidbody2D`를 돌려줍니다.

---

## `exec` — 런타임 C# 실행

가장 강력한 명령어입니다. 에디터 + 런타임 풀 액세스. 보일러플레이트 없음.

```bash
# 평가
hera-agent-unity exec "return Application.dataPath;"
hera-agent-unity exec "return GameObject.FindObjectsOfType<Camera>().Length;"

# 씬 수정
hera-agent-unity exec "var go = new GameObject(\"Temp\"); return go.name;"

# ECS / 커스텀 어셈블리
hera-agent-unity exec "return World.All.Count;" --usings Unity.Entities

# 복잡한 코드는 stdin으로 파이프 — 셸 이스케이핑 회피
echo '
var scene = EditorSceneManager.GetActiveScene();
return scene.GetRootGameObjects().Length;
' | hera-agent-unity exec

# 또는 파일에서 로드
hera-agent-unity exec --file scripts/probe.cs
```

| 플래그                | 용도                                                                                                                                       |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `--usings ns,...`     | using 디렉티브 추가                                                                                                                        |
| `--file <path>`       | 디스크에서 코드 로드 (positional · stdin이 우선)                                                                                            |
| `--csc <path>`        | C# 컴파일러 경로 명시                                                                                                                      |
| `--dotnet <path>`     | dotnet 런타임 경로 명시                                                                                                                    |
| `--no-cache`          | 컴파일된 어셈블리 캐시 무시 (디버그용)                                                                                                     |
| `--depth N`           | 응답 객체 그래프 깊이 제한 (기본 3, 최대 8)                                                                                                |
| `--check`             | 컴파일만 검증하는 dry-run. 깨끗하게 컴파일되면 `SuccessResponse`, 실패 시 `EXEC_COMPILE_ERROR`. `Execute()` 호출도 부수 효과도 없음.        |
| `--stacktrace <mode>` | `EXEC_RUNTIME_ERROR` 스택 트레이스 모드: `none`(예외 타입만), `user`(기본 — 프레임워크 프레임 제거), `full`(원본).                          |
| `--strict`            | 스니펫 실행 중 발생한 `Debug.LogError` / `LogException` / `LogAssert`를 캡처해 `EXEC_LOGGED_ERROR`로 surface — 깨끗한 리턴도 실패로 뒤집힘. |

**동작 원리.** 코드는 static 메서드로 감싸진 뒤 시스템의 Roslyn(`csc`)으로 임시 DLL로 컴파일됩니다. 메모리 누수를 막으려고 수집 가능한(Collectible) `AssemblyLoadContext`에 올리고, 리플렉션으로 실행한 다음 결과를 JSON으로 직렬화하죠. 같은 소스 코드는 인메모리 캐시에서 바로 꺼내 씁니다 — warm call이면 csc 자체를 건너뜁니다.

기본 using에는 `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Reflection`, `System.Threading.Tasks`, `UnityEngine`, `UnityEngine.SceneManagement`, `UnityEditor`, `UnityEditor.SceneManagement`, `UnityEditorInternal`이 포함됩니다.

---

## 인트로스펙션 — `describe_type`, `find_method`, `list_assemblies`

이 세 명령어는 LLM이 학습 시점에 어렴풋이 기억하는 API가 아니라, **지금 여러분 Unity 프로젝트에 실제로 로드돼 있는 것**을 읽습니다.

```bash
# 무엇이 로드돼 있나? (System.* 기본 제외)
hera-agent-unity list_assemblies
hera-agent-unity list_assemblies --filter Unity.Entities

# 타입 검사 — 시그니처 + 큐레이션된 Unity 함정
hera-agent-unity describe_type UnityEditor.EditorApplication
hera-agent-unity describe_type AssetDatabase --members methods --limit 50

# 살아 있는 AppDomain 전체에서 메서드 이름 검색
hera-agent-unity find_method Refresh --namespace UnityEditor
```

`describe_type`은 스키마와 함께 `pitfalls` 배열을 반환합니다 — 짧고 안정적인 함정 노트 + Unity 6 Manual 링크. 커버리지: **Editor API**, **MonoBehaviour 생명주기**, **uGUI** (Canvas, RectTransform, EventSystem, LayoutGroup, ScrollRect, Selectable, Mask, CanvasGroup, …). 각 항목은 `{ text, doc_url }` 형식이라 에이전트가 시그니처 + 함정 + fetch 가능한 문서를 1회 라운드트립으로 받습니다.

`exec`와 짝지으면 dry-run 루프가 완성됩니다:

```
describe_type → 코드 작성 → exec → 수정 → exec
```

---

## 배치 — 시나리오 자동화

여러 단계 파이프라인을 CLI 호출 한 번에 실행합니다. CI, 자동화 스크립트, 큰 에이전트 플랜에 적합합니다.

```bash
hera-agent-unity batch --file workflow.json
```

```json
{
  "commands": [
    { "command": "refresh_unity", "params": { "compile": "request" } },
    { "command": "exec",          "params": { "code": "return EditorSceneManager.GetActiveScene().name;" } },
    { "command": "console",       "params": { "type": "error", "lines": 10 } }
  ],
  "options": { "fail_fast": true }
}
```

파일을 만들기 싫다면 stdin으로 파이프:

```bash
echo '{"commands":[{"command":"refresh_unity","params":{"compile":"request"}}]}' \
  | hera-agent-unity batch
```

`--dry-run`은 실행하지 않고 계획만 미리 보여줍니다. `fail_fast`는 첫 에러에서 바로 멈추고 어느 단계에서 깨졌는지 알려줍니다.

---

## 프로파일러

UI 없이 터미널에서 라이브 프로파일러를 읽습니다.

```bash
hera-agent-unity profiler enable                       # 녹화 시작
hera-agent-unity profiler hierarchy                    # 최상위 샘플 (마지막 프레임)
hera-agent-unity profiler hierarchy --depth 3          # 재귀 드릴다운
hera-agent-unity profiler hierarchy --root PlayerLoop --depth 5
hera-agent-unity profiler hierarchy --frames 30 --min 0.5 --sort self
hera-agent-unity profiler disable                      # 녹화 중지
hera-agent-unity profiler status
hera-agent-unity profiler clear
```

| 플래그            | 용도                                                |
|-------------------|----------------------------------------------------|
| `--depth N`       | 재귀 깊이 (0 = 무제한, 기본 1)                      |
| `--root <name>`   | 부분 문자열로 루트 샘플 매칭                        |
| `--frames N`      | 최근 N 프레임 평균                                  |
| `--from N --to N` | 명시적 프레임 범위 평균                             |
| `--parent ID`     | ID로 항목 드릴 인투                                 |
| `--min <ms>`      | 임계값 미만 항목 필터                               |
| `--sort <col>`    | `total`(기본), `self`, `calls`                      |
| `--thread N`      | 스레드 인덱스 (0 = 메인)                            |

---

## 커스텀 툴

Editor 어셈블리 아무 곳에나 C# 클래스를 두면 자동으로 발견됩니다 — 등록도, 코드젠도 없습니다.

```csharp
using HeraAgent;
using Newtonsoft.Json.Linq;
using UnityEngine;

[HeraTool(Name = "spawn", Group = "gameplay", Description = "지정 위치에 프리팹 스폰")]
public static class SpawnEnemy
{
    public class Parameters
    {
        [ToolParameter("X 월드 좌표", Required = true)] public float  X      { get; set; }
        [ToolParameter("Y 월드 좌표", Required = true)] public float  Y      { get; set; }
        [ToolParameter("Z 월드 좌표", Required = true)] public float  Z      { get; set; }
        [ToolParameter("프리팹 이름", Default = "Enemy")] public string Prefab { get; set; }
    }

    public static object HandleCommand(JObject args)
    {
        var p      = new ToolParams(args);
        var prefab = Resources.Load<GameObject>(p.Get("prefab", "Enemy"));
        var inst   = Object.Instantiate(prefab,
                        new Vector3(p.GetFloat("x"), p.GetFloat("y"), p.GetFloat("z")),
                        Quaternion.identity);
        return new SuccessResponse("Spawned", new { name = inst.name });
    }
}
```

호출:
```bash
hera-agent-unity spawn --x 1 --y 0 --z 5 --prefab Goblin
```

**규칙**
- `[HeraTool]`로 데코레이트
- `public static object HandleCommand(JObject parameters)` 노출 (인스턴스 메서드도 가능)
- `SuccessResponse(message, data)` 또는 `ErrorResponse(message)` 반환
- `Parameters`의 멤버는 `{ get; set; }` 프로퍼티 — 필드는 스키마 생성기가 보지 못합니다
- 클래스 이름은 자동 `snake_case` (`SpawnEnemy` → `spawn_enemy`); `Name =`으로 재정의 가능
- 에디터 시작 시와 스크립트 리컴파일마다 재발견됨
- Unity 메인 스레드에서 실행 — 모든 API가 안전합니다
- 중복 툴 이름은 콘솔에 경고로 표시되며 먼저 등록된 쪽이 이깁니다

`hera-agent-unity list`는 파라미터 스키마를 노출해서 에이전트가 소스 코드를 읽지 않고도 툴을 발견하고 호출할 수 있게 합니다.

---

## 성능

hera-agent-unity는 AI 에이전트 워크플로우의 토큰 비용을 거의 무시할 수준으로 유지하도록 설계되었습니다. 최근 스모크 테스트 벤치마크(v0.0.16, 7개 대표 시나리오)에서 오버헤드가 여전히 미미함을 확인했습니다:

| 지표 | 값 |
|---|---:|
| 총 호출 | 7 |
| 총 왕복 바이트 | **725 B** |
| 추정 토큰 (chars ÷ 4) | **~181 T** |
| 호출당 평균 | **26 T** |
| 5B 이하 응답 | **71%** |

**비용을 차지하는 것**: 바이트의 88%는 에이전트가 작성한 C# 코드(입력)이며, 응답이 아닙니다. 대표적인 `exec` 호출인 `return Time.time;`은 **총 6 토큰** — 응답 자체는 2–3 토큰에 불과합니다.

전체 50시나리오 벤치마크와 스모크 테스트 리포트는 [`docs/benchmarks/`](docs/benchmarks/)에서 확인할 수 있습니다.

---

## 구조

```
┌──────────────────┐           ┌──────────────────────────────────┐
│    CLI (Go)      │           │         Unity Editor             │
│                  │   HTTP    │                                  │
│  ┌────────────┐  │  POST     │  ┌────────────┐                  │
│  │ 인스턴스   │──┼──/command─┼─►│ HttpServer │ (localhost:8090+)│
│  │   발견     │  │           │  └─────┬──────┘                  │
│  └──────┬─────┘  │           │        │ ConcurrentQueue         │
│         │ read   │           │  ┌─────▼──────┐                  │
│  ┌──────▼─────┐  │           │  │  Command   │ EditorApplication│
│  │ Heartbeat  │  │           │  │  Router    │ .update           │
│  │   파일     │◄─┼───write───┼──│            │ (메인 스레드)    │
│  │ (instance) │  │           │  └─────┬──────┘                  │
│  └────────────┘  │           │        │ SemaphoreSlim(1,1)      │
│                  │           │  ┌─────▼──────┐                  │
│  ┌────────────┐  │           │  │    Tool    │ [HeraTool]       │
│  │  Backoff   │  │           │  │ Discovery  │ 리플렉션         │
│  │  폴링      │  │           │  └─────┬──────┘                  │
│  └────────────┘  │           │  ┌─────▼──────┐                  │
│                  │           │  │   핸들러   │ exec, editor,    │
│                  │           │  │            │ test, profiler…  │
└──────────────────┘           │  └────────────┘                  │
                               └──────────────────────────────────┘
```

| 원칙                              | 의미                                                                                                   |
|-----------------------------------|-------------------------------------------------------------------------------------------------------|
| **스테이트리스**                  | 매 요청은 독립적입니다. 세션도, 재연결 로직도 없습니다.                                                |
| **자동 발견**                     | `~/.hera-agent-unity/instances/` heartbeat 파일을 스캔. CWD / 프로젝트 / 포트로 매칭합니다.            |
| **도메인 리로드 안전**            | `[InitializeOnLoad]` + 어셈블리 리로드 이벤트로 스크립트 재컴파일에서 살아남습니다.                    |
| **메인 스레드 실행**              | 모든 툴 핸들러가 `ConcurrentQueue` + `EditorApplication.update`를 통해 메인 스레드로 마샬링됩니다.     |
| **파일시스템 크로스프로세스 버스**| Heartbeat · 테스트 결과 파일은 도메인 리로드 중 HTTP 서버 종료를 견뎌냅니다.                            |
| **원자적 쓰기**                   | Heartbeat은 `.tmp`에 쓰고 rename — CLI가 반쯤 쓴 JSON을 읽는 일이 없습니다.                            |

### AI 에이전트를 위한 엔지니어링

- **함수 타입 DI (Go).** 명령 핸들러는 `sendFn`과 `instanceResolver`를 주입받습니다. `resolve` 클로저는 매 호출마다 인스턴스를 재발견하므로, 도메인 리로드로 HTTP 포트가 바뀌어도 다음 명령이 자동으로 새 엔드포인트를 찾습니다.
- **3단계 오케스트레이션.** 컴파일을 트리거하는 명령은 `waitForAlive` → 전송 → `waitForReady` 순서입니다. 폴링은 1.5배 백오프 (100ms → 2s cap) — 보통의 2배보다 완만해서 Unity가 이미 ready로 돌아온 경우를 세밀하게 감지합니다.
- **컴파일 유예 기간.** `editor refresh --compile`은 `state == "compiling"`을 3초간 강제로 고정합니다. Unity가 실제 컴파일을 시작하기까지의 1–2프레임 지연 동안 폴링이 stale `"ready"`에 걸리지 않게 합니다.
- **도메인 리로드에도 살아남는 비동기 작업.** PlayMode 테스트와 Package Manager `add` / `remove` / `embed`는 모두 도메인 리로드를 유발해 HTTP 리스너와 in-flight Request 핸들을 파괴합니다. 각 작업은 결과를 `~/.hera-agent-unity/status/{test,package}-result-*.json`에 기록하고 CLI가 500ms 간격으로 폴링합니다. 리로드 후에는 `[InitializeOnLoad]` 훅이 watcher를 다시 부착하고(패키지의 경우 `Client.List`로 결과 검증), 결과 파일이 항상 생성되도록 합니다.
- **원자적 셀프 업데이트.** GitHub Releases → 백업 → rename → 정리, 실패 시 롤백. Windows에서는 실행 중인 `.exe`가 잠기므로 PowerShell 프로세스를 spawn해 `.bak`을 지연 삭제합니다.

---

## MCP와 비교

|                      | MCP 통합                          | hera-agent-unity                          |
|----------------------|-----------------------------------|-------------------------------------------|
| **설치**             | Python + uv + FastMCP + 설정 파일 | 단일 바이너리                              |
| **런타임 의존성**    | WebSocket 릴레이, 영구 프로세스    | 없음                                       |
| **프로토콜**         | JSON-RPC 2.0 over stdio           | 직접 HTTP POST                             |
| **세팅**             | 설정 생성, AI 클라이언트 재시작    | UPM 패키지 추가로 끝                       |
| **도메인 리로드**    | 복잡한 재연결 로직                | 스테이트리스 (파일시스템 버스)             |
| **커스텀 툴**        | `[Attribute]` 패턴                | 동일한 `[Attribute]` 패턴                  |
| **호환성**           | MCP 클라이언트 전용                | 어떤 셸, 어떤 에이전트, 어떤 스크립트도    |
| **다중 인스턴스**    | 수동 설정                         | CWD / 프로젝트 / 포트 자동 발견            |

---

## 글로벌 플래그 & 환경 변수

```bash
--port <N>          # 활성 heartbeat 포트로 Unity 인스턴스 선택
--project <path>    # 프로젝트 경로(부분 매칭)로 인스턴스 선택
--timeout <ms>      # 요청 타임아웃 ms (기본 60000)
--verbose           # 단계별 타이밍 + 진행 메시지를 stderr로
--quiet             # 장식성 진행 메시지 억제 (에러는 plain으로 그대로 출력)
--debug             # HTTP 요청/응답 본문 + 디스커버리 정보를 stderr로 덤프
--compact-json      # JSON을 들여쓰기 없이 — AI 에이전트용 페이로드 축소
--narrate           # tool 명령에서도 waitForAlive / waitForReady 진행 메시지 강제
                    # (기본: human-target 명령에만 narrate)
```

모든 글로벌 플래그는 동일한 의미의 `HERA_AGENT_*` 환경 변수 짝을 갖고 있어, 플래그가 생략됐을 때 CLI가 환경 변수를 읽습니다. 셸 프로필에 한 번 설정해두면 매 명령마다 플래그를 붙일 필요가 없습니다.

| 환경 변수                       | 등가 플래그                                                                    |
|---------------------------------|--------------------------------------------------------------------------------|
| `HERA_AGENT_PORT`               | `--port <N>`                                                                   |
| `HERA_AGENT_PROJECT`            | `--project <path>`                                                             |
| `HERA_AGENT_TIMEOUT_MS`         | `--timeout <ms>`                                                               |
| `HERA_AGENT_VERBOSE`            | `--verbose`                                                                    |
| `HERA_AGENT_QUIET`              | `--quiet`                                                                      |
| `HERA_AGENT_DEBUG`              | `--debug`                                                                      |
| `HERA_AGENT_COMPACT_JSON`       | `--compact-json`                                                               |
| `HERA_AGENT_NARRATE`            | `--narrate`                                                                    |
| `HERA_AGENT_NO_PATH_CHECK=1`    | 매 명령마다의 PATH 불일치 경고를 끔 (플래그 등가 없음)                          |
| `GITHUB_TOKEN`                  | release가 프라이빗 GitHub 미러에 있을 때 `update`가 사용하는 인증              |

---

## FAQ

<details>
<summary><strong>Unity가 "port 8090 is taken"이라고 합니다.</strong></summary>

커넥터는 8090, 8091, 8092, … 최대 10회까지 자동으로 다음 포트를 시도합니다. 전부 점유 중이면 좀비 Unity 프로세스나 다른 로컬 서비스가 포트를 잡고 있는지 확인하세요. CLI는 heartbeat 파일에서 실제 포트를 읽기 때문에 포트 번호는 사용자에게 투명합니다.

</details>

<details>
<summary><strong>Unity를 최소화하면 명령이 멈춥니다.</strong></summary>

원래 멈추지 않아야 합니다 — 커넥터는 큐에 작업을 넣을 때마다 `RepaintAllViews()`를 호출해서 포커스를 잃어도 update 루프가 돌게 만듭니다. 그래도 멈춘다면 UPM 패키지가 설치되어 있는지, Unity 콘솔에 `[Hera] HTTP server started on port XXXX`가 찍히는지 확인하세요.

</details>

<details>
<summary><strong>`exec`가 "Cannot find csc compiler"로 실패합니다.</strong></summary>

컴파일러는 .NET SDK, Visual Studio, 또는 Unity 내장 Roslyn에서 자동 탐지됩니다. 셋 다 없으면 [.NET SDK](https://dotnet.microsoft.com/download)를 설치하거나 경로를 직접 지정하세요:

```bash
hera-agent-unity exec "return 1+1;" --csc "C:\Program Files\dotnet\sdk\8.0.100\Roslyn\bincore\csc.dll"
```

macOS / Linux에서는 Windows-PE `csc.exe`가 Unity 내장 **Mono** 호스트를 통해 자동 실행됩니다(Connector 0.0.31+). 따라서 `.dll` Roslyn이 꼭 필요하진 않습니다 — `.NET` Roslyn 경로를 쓰고 싶을 때만 `--csc`를 `csc.dll`로 지정하세요.

</details>

<details>
<summary><strong>비-영어 Windows + Unity 6.5에서 `exec`가 `System.Text.Encoding.CodePages` 실패와 함께 `EXEC_COMPILE_ERROR`를 냅니다.</strong></summary>

모든 스니펫(`return 1+1;` 조차)이 실패하는 건, resolver가 Unity 내장 **Mono `csc.exe`**를 골랐기 때문입니다 — 이게 CP949 / Shift-JIS / GBK 콘솔에서 `System.Text.Encoding.CodePages` 로드에 실패해 크래시합니다. **커넥터를 0.0.34+로 갱신**하세요: .NET SDK Roslyn `csc.dll`(Unity 6.5가 `DotNetSdk/sdk/<version>/Roslyn/bincore/`로 옮긴 경로)을 우선하고 UTF-8 컴파일러 I/O를 강제합니다. git 패키지를 재해석(`Packages/packages-lock.json`의 해당 항목 삭제 → 에디터 포커스)하면 반영됩니다.

</details>

<details>
<summary><strong>Unity Editor가 여러 개 켜져 있으면 어떻게 고르나요?</strong></summary>

우선순위:
1. `--port` 플래그 (명시적)
2. `--project` 플래그 (경로 부분 매칭)
3. 현재 작업 디렉토리가 알려진 프로젝트 경로와 매칭
4. 가장 최근 heartbeat 타임스탬프 (폴백)

</details>

<details>
<summary><strong>CI에서 써도 안전한가요?</strong></summary>

네 — `batch`가 바로 그 용도로 만들어졌습니다. exit code가 명령별로 그대로 전파되고, `fail_fast`로 첫 에러에서 멈춥니다. `update` 명령과 버전 알림은 비대화식 실행에서 꺼둘 수 있고요.

</details>

</details>

---

## 이 도구를 쓰는 프로젝트

| 프로젝트                                                          | 설명                                                                                              |
|-------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| **NoMoreRolls**                                                   | Unity 솔로 개발 게임 — 3-tier 아키텍처, 9-soul 전투 시스템. hera-agent-unity로 빌드.                |

> 본인 프로젝트를 올리고 싶으면 이슈나 PR 환영합니다.

---

## 제작자

**Victor** — Unity/C# 개발자, 라이브 서비스 MMORPG 6년+ 프로덕션 경험.
[hera-agent-unity](https://github.com/NotNull92/hera-agent-unity)로 NoMoreRolls를 솔로 개발 중 · 유튜브 [IndieAlchemist](https://www.youtube.com/@IndieAlchemist).

[![GitHub](https://img.shields.io/badge/@NotNull92-181717?logo=github&logoColor=white&style=flat-square)](https://github.com/NotNull92)
[![Email](https://img.shields.io/badge/fatiger92@gmail.com-EA4335?logo=gmail&logoColor=white&style=flat-square)](mailto:fatiger92@gmail.com)

## 후원

hera-agent-unity는 무료 오픈소스입니다. 시간이나 토큰을 아껴줬다면 [후원](https://github.com/sponsors/NotNull92)으로 응원해 주세요.

후원금은 다음에 직접 쓰입니다:
- 새 엔진 지원 (Godot, Unreal)
- 깊어진 에이전트 통합 (Cursor, Windsurf, …)
- 문서, 영상 튜토리얼, 샘플 프로젝트

## 라이선스

MIT — [LICENSE](LICENSE) 참조.
