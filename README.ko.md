<div align="center">

<img src="docs/assets/hera_lite.png?v=2" width="50%" alt="hera-agent banner">

<br>

[![Release](https://img.shields.io/github/v/release/NotNull92/hera-agent?style=flat-square&logo=github&color=00d4aa)](https://github.com/NotNull92/hera-agent/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square&color=blue)](LICENSE)
[![Go Version](https://img.shields.io/badge/go-%5E1.22-00ADD8?style=flat-square&logo=go)](https://go.dev)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-ff69b4?style=flat-square)]()

**추측 대신 실측 — AI에게 살아 있는 Unity를 만지게 합니다.**

<br><br>

**설치 → 연결 → AI가 Unity를 직접 제어**

<img src="docs/video/hera-agent-install-status(not-connected-unity)-uninstall.gif" width="80%" alt="hera-agent CLI 설치, 상태 확인, 제거">

<br><br>

**UPM 패키지 설치**

<img src="docs/video/hera-agent-install-UPM-scene.gif" width="80%" alt="Unity UPM 패키지 설치">

<br><br>

**Unity 연결 상태 확인**

<img src="docs/video/hera-agent-status-connected-unity.gif" width="80%" alt="hera-agent Unity 연결 상태">

<br><br>

**Claude Code + hera-agent 씬 전환**

<img src="docs/video/claude-code-hera-agent-scene-transition.gif" width="80%" alt="Claude Code가 hera-agent로 씬 전환">

<br><br>

[Installation](#설치) · [Quick Start](#퀵-스타트) · [Commands](#명령어) · [Custom Tools](#커스텀-툴) · [Architecture](#구조)

</div>

---

## Hera

LLM은 당신의 Unity를 모릅니다. 작년에 학습한 API와 일반화된 패턴을 기억할 뿐입니다. 당신은 매주 그 격차를 토큰과 시간으로 갚고 있습니다.

Hera는 그 사이에 섭니다.

AI가 코드를 추측하기 전에, Hera가 Editor에서 직접 실행하고 결과를 회수합니다. AI가 콘솔 에러를 가정하기 전에, Hera가 실제 로그를 type별로 가져옵니다. AI가 Play Mode 결과를 짐작하기 전에, Hera가 직접 돌리고 끝날 때까지 기다립니다.

중간 서버는 없습니다. Python도, WebSocket도, JSON-RPC도 없습니다. 하나의 Go 바이너리, localhost HTTP, C# UPM 패키지 하나. Unity Editor가 열리면 Hera는 이미 거기 있습니다.

Hera는 명령에 응답합니다 — 추론하지 않고, 가정하지 않고. 당신의 Unity가 지금 이 순간 무엇인지를 있는 그대로 가져옵니다.

추측은 비쌉니다. 실측은 명령입니다.

```
┌─────────────┐      HTTP      ┌─────────────────┐
│   터미널    │ ◄────────────► │   Unity Editor  │
│  (1바이너리) │   port 8090    │   (자동 시작)   │
└─────────────┘                └─────────────────┘
```

**Go 핵심 약 800줄, C# 약 2,300줄. 그 이상은 없습니다.**

> 테스트, TUI, 플랫폼 어댑터 등 부가 코드가 약 2,200줄 더 있지만, Unity와 직접 통신하는 엔진은 여전히 가볍습니다.

---

## 설치

**macOS / Linux**
```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent/main/install.sh | sh
```

**Windows** (PowerShell)
```powershell
irm https://raw.githubusercontent.com/NotNull92/hera-agent/main/install.ps1 | iex
```

<details>
<summary>다른 설치 방법</summary>

**`go install`** (어떤 플랫폼이든)
```bash
go install github.com/NotNull92/hera-agent@latest
```

**수동 다운로드** — [Releases](https://github.com/NotNull92/hera-agent/releases)에서 플랫폼에 맞는 바이너리를 받으세요.

</details>

---

## 퀵 스타트

### 1. Unity Connector 설치

**Package Manager → Add package from git URL**
```
https://github.com/NotNull92/hera-agent.git?path=AgentConnector
```

또는 `Packages/manifest.json`에 직접 추가:
```json
"com.notnull92.hera-agent": "https://github.com/NotNull92/hera-agent.git?path=AgentConnector"
```

> 커넥터는 자동으로 시작됩니다. 별도 설정은 필요 없습니다.

### 2. 명령어 실행

```bash
# Unity 연결됐나? (포트 찾기 같은 거 없음)
hera-agent status

# 터미널에서 Play Mode 진입 — 실제로 들어갈 때까지 대기
hera-agent editor play --wait

# C# 코드를 Unity 안에서 직접 실행 — 재컴파일도 재시작도 없음
hera-agent exec "return EditorSceneManager.GetActiveScene().name;"

# 스크린샷 없이 AI가 읽고 행동할 수 있는 에러 출력
hera-agent console --type error
```

### 3. AI 에이전트에게 넘기기

**발견** — 터미널에서 Claude Code CLI 또는 Codex를 열고 입력:

> **"hera-agent cli 도구 설치되어 있는지 확인하고 파악해"**

에이전트가 hera-agent를 발견해서 명령어를 나열하고, 알아서 Unity를 조종하기 시작합니다.

**규칙으로 고정 (권장)** — Unity 프로젝트의 `CLAUDE.md`, `AGENTS.md` 등 AI 에이전트가 읽는 설정 문서에 다음 한 줄을 추가하세요:

> **"유니티 작업의 경우 반드시 hera-agent를 사용해"**

매번 LLM이 학습 데이터에서 Unity API를 추측하는 대신, 항상 라이브 에디터에 손을 뻗게 만듭니다.

---

## 명령어

| 명령어 | 기능 |
|---------|-------------|
| `editor` | Play, stop, pause, refresh |
| `exec` | Unity 내부에서 C# 코드 실행 (`--file <path>` 로 긴 코드 로드, `--depth N` 으로 응답 깊이 조절) |
| `log` | csc 컴파일 없이 Unity 콘솔에 메시지 출력 |
| `ping` | 토큰 절약형 헬스 프로브 (heartbeat 파일만 읽음) |
| `scene` | 씬 조회/로드/저장/목록/언로드 |
| `console` | 로그 읽기, 필터링, 삭제 |
| `test` | EditMode / PlayMode 테스트 실행 |
| `menu` | 메뉴 항목 경로로 실행 |
| `screenshot` | Scene 또는 Game 뷰 캡처 |
| `profiler` | 프로파일러 계층 읽기, 녹화 제어 |
| `reserialize` | 텍스트 편집 후 YAML 정제 |
| `list` | 슬림 출력 (기본); `--names` / `--tool <name>` 으로 토큰 효율 조회 |
| `status` | 연결 상태 및 프로젝트 정보 |
| `update` | 바이너리 자동 업데이트 |
| `install` | CLI를 PATH에 설치 |
| `uninstall` | PATH에서 CLI 제거 |

---

## `exec` 명령어

가장 강력한 기능입니다. 보일러플레이트 없이 전체 런타임에 접근할 수 있습니다.

```bash
# 모든 것을 검사
hera-agent exec "return World.All.Count;" --usings Unity.Entities

# 씬 수정
hera-agent exec "var go = new GameObject(\"Temp\"); return go.name;"

# stdin으로 파이프 (셸 이스케이핑 문제 해결)
echo '
var scene = EditorSceneManager.GetActiveScene();
return scene.GetRootGameObjects().Length;
' | hera-agent exec
```

실제 C#을 컴파일하고 실행하므로 **Unity의 모든 API를 호출할 수 있습니다.** ECS World 검색, 에셋 수정, 에디터 내부 API 호출도 모두 가능합니다. 별도의 커스텀 툴 작성이 필요 없습니다.

---

## 커스텀 툴

C# 클래스를 Editor 어셈블리에 두면 자동으로 발견됩니다.

```csharp
using HeraAgent;
using Newtonsoft.Json.Linq;

[HeraTool(Name = "spawn", Group = "gameplay")]
public static class SpawnEnemy
{
    public class Parameters
    {
        [ToolParameter("X 좌표", Required = true)] public float X;
        [ToolParameter("Y 좌표", Required = true)] public float Y;
        [ToolParameter("Z 좌표", Required = true)] public float Z;
        [ToolParameter("프리팹 이름", DefaultValue = "Enemy")] public string Prefab;
    }

    public static object HandleCommand(JObject args)
    {
        var p = new ToolParams(args);
        var prefab = Resources.Load<GameObject>(p.Get("prefab", "Enemy"));
        var inst = Object.Instantiate(prefab, new Vector3(p.GetFloat("x"), p.GetFloat("y"), p.GetFloat("z")), Quaternion.identity);
        return new SuccessResponse("생성 완료", new { name = inst.name });
    }
}
```

호출:
```bash
hera-agent spawn --x 1 --y 0 --z 5 --prefab Goblin
```

`hera-agent list`는 파라미터 스키마를 노출해서 AI 에이전트가 소스 코드를 읽지 않고도 툴을 발견하고 호출할 수 있게 합니다.

---

## 구조

```
┌─────────────┐         ┌─────────────────────────────┐
│   CLI Go    │         │      Unity Editor           │
│  (약 800 LoC) │◄───────►│  ┌─────────────────────┐    │
│             │  HTTP   │  │   HttpServer        │    │
│ • 자동 발견 │  8090+  │  │   (localhost)       │    │
│ • 명령 전송 │         │  └──────────┬──────────┘    │
│ • 결과 출력  │         │             │ 리플렉션      │
│             │         │  ┌──────────▼──────────┐    │
└─────────────┘         │  │   [HeraTool]        │    │
                        │  │   클래스            │    │
                        │  └─────────────────────┘    │
                        └─────────────────────────────┘
```

- **스테이트리스** — 매 요청은 독립적입니다. 재연결 로직이 없습니다.
- **자동 발견** — `~/.hera-agent/instances/`를 스캔하여 실행 중인 Unity 에디터를 찾습니다.
- **도메인 리로드 대응** — 커넥터는 스크립트 재컴파일에도 살아남며 자동으로 복구합니다.
- **메인 쓰레드 실행** — 모든 툴 핸들러는 Unity 메인 쓰레드에서 실행됩니다. 모든 API가 안전합니다.

---

## MCP와 비교

| | MCP 통합 | hera-agent |
|---|:---:|:---:|
| **설치** | Python + uv + FastMCP + 설정 파일 | 단일 바이너리 |
| **런타임 의존성** | WebSocket 릴레이, 영구 프로세스 | 없음 |
| **프로토콜** | JSON-RPC 2.0 over stdio | 직접 HTTP POST |
| **설정** | MCP 설정 생성, AI 클라이언트 재시작 | 패키지 추가하면 끝 |
| **도메인 리로드** | 복잡한 재연결 로직 | 스테이트리스 |
| **커스텀 툴** | `[Attribute]` 패턴 | 동일한 `[Attribute]` 패턴 |
| **호환성** | MCP 클라이언트 전용 | 어떤 셸에서든, 어떤 에이전트와도 |

---

## 글로벌 플래그

```bash
--port <N>       # 자동 발견 무시
--project <path> # 프로젝트 경로로 선택
--timeout <ms>   # HTTP 타임아웃 (기본: 120초)
```

---

## 제작자

**Victor** — Unity/C# 개발자, 라이브 서비스 MMORPG 6년+ 프로덕션 경험  
[hera-agent](https://github.com/NotNull92/hera-agent)로 [NoMoreRolls](https://github.com/NotNull92)를 솔로 개발 중 · 유튜브 [IndieAlchemist](https://www.youtube.com/@IndieAlchemist)

[![GitHub](https://img.shields.io/badge/@NotNull92-181717?logo=github&logoColor=white&style=flat-square)](https://github.com/NotNull92)
[![Email](https://img.shields.io/badge/fatiger92@gmail.com-EA4335?logo=gmail&logoColor=white&style=flat-square)](mailto:fatiger92@gmail.com)

## 라이선스

MIT
