# GeekNews 포스팅 적합성 분석 보고서

**작성일**: 2026-05-20
**대상**: hera-agent-unity (Lite/Pro/Landing 3개 레포)
**비교 대상**: oh-my-openagent (code-yeongyu/oh-my-openagent)

---

## 1. oh-my-openagent 분석 요약

| 항목 | 수치/상태 |
|:---|:---|
| **Stars** | 58,590 |
| **Forks** | 4,756 |
| **Open Issues** | 591 |
| **Language** | TypeScript (10.5M LoC), HTML, Python |
| **License** | Other (SUL-1.0 — Semi-Open) |
| **Latest Release** | v4.2.2 (2026-05-18) |
| **Release Frequency** | v4.2.0(5/15) → v4.2.1(5/18) → v4.2.2(5/18) — 활발 |
| **Issue Templates** | ✅ bug_report.yml, feature_request.yml, config.yml |
| **PR Template** | ✅ 있음 |
| **CHANGELOG** | ✅ Keep a Changelog 형식, 상세 |
| **Documentation** | ✅ docs/guide/(5개), docs/reference/(6개), docs/examples/(3개), 다국어 README(4개) |
| **Demo/GIF** | ❌ 정적 이미지(hero.jpg, omo-logo.png 등)만 있음 — **동영상/GIF 없음** |
| **Building in Public** | ✅ Discord 실시간 개발, AI 어시스턴트(Jobdori)가 유지보수 |
| **Homepage** | https://ohmyopenagent.com/ |

### oh-my-openagent 핵심 특징
- **OpenCode 에이전트 하네스** — Claude Code, Cursor, Kimi 등 다중 에이전트 지원
- **Discipline Agents** — Sisyphus(오케스트레이터), Hephaestus(딥워커), Prometheus(플래너)
- **Team Mode** — 리드 에이전트 + 최대 8개 병렬 멤버, tmux 시각화
- **Hash-Anchored Edit Tool** — LINE#ID 해시 기반 zero stale-line 에러
- **LSP + AST-Grep + MCP** 내장
- **Claude Code 호환** — 모든 hook, command, skill 그대로 작동

---

## 2. hera-agent-unity 3개 레포 현재 상태

### 2-1. hera-agent-unity (Lite) — Public

| 항목 | 수치/상태 |
|:---|:---|
| **Stars** | 0 |
| **Forks** | 0 |
| **Open Issues** | 0 |
| **License** | MIT |
| **Latest Release** | v0.0.23 (2026-05-19) |
| **Commits (5월)** | 85개 |
| **Go 코드** | ~5,247 LoC (core ~2,600) |
| **C# 코드** | ~3,988 LoC |
| **Issue Templates** | ✅ bug_report.md, feature_request.md, question.md (3개) |
| **CHANGELOG** | ✅ Keep a Changelog 형식, 상세 (v0.0.1 ~ v0.0.9) |
| **CI/CD** | ✅ ci.yml (build/vet/test/lint/format), release.yml (6플랫폼 cross-compile) |
| **Documentation** | ✅ docs/ 10개 문서 (ARCHITECTURE, COMMANDS, CSHARP_CONNECTOR, CUSTOM_TOOLS, DEVELOPMENT, GO_CLI, INDEX, SUGGESTIONS, TROUBLESHOOTING) |
| **Demo/GIF** | ✅ **6개 GIF** (docs/video/): install-status-uninstall, UPM-scene, status-connected, claude-code-scene-transition, move-scene, find-hera-agent-unity |
| **README** | ✅ 영어/한국어双语, Author 섹션, Architecture 다이어그램 |
| **Landing Page** | ✅ 별도 gh-pages (hera-agent-unity-landing) |

### 2-2. hera-agent-unity-pro — Private

| 항목 | 수치/상태 |
|:---|:---|
| **Private** | 예 (Patreon 구독자 전용) |
| **Latest Release** | v0.0.35 (2026-05-19) |
| **Commits (5월)** | 173개 |
| **Go 코드** | ~6,655 LoC |
| **C# 코드** | ~7,893 LoC |
| **Issue Templates** | ✅ 4개 (Bug, Feature, Installation/Setup, Question) — Lite보다 1개 더 많음 |
| **CHANGELOG** | ✅ 있음 |
| **Documentation** | ✅ docs/ 11개 문서 (Lite + ERROR_LOGGING_IMPROVEMENTS, HARNESS_ENGINEERING_DESIGN, SUGGESTIONS-PRO) |
| **Demo/GIF** | ❌ **GIF 없음** (assets/에 PNG/ICO만) |
| **README** | ✅ 영어 단일, Pro 전용 배지(CI, Unity 6000.0+), Lite 링크 |

### 2-3. hera-agent-unity-landing — Public (gh-pages)

| 항목 | 상태 |
|:---|:---|
| **Languages** | 한국어(index.html) + 영어(index.en.html) |
| **SEO** | ✅ OpenGraph, Twitter Card, JSON-LD (Schema.org SoftwareApplication), CSP, canonical |
| **Sections** | Hero, Features, How It Works, Benchmark, Pricing(Lite/Pro/Studio), FAQ |
| **Design** | ✅ 마스코트(Hera), 커스텀 CSS/JS, 반응형, 햄버거 메뉴 |
| **Discord 링크** | ✅ Footer에 추가됨 (최근 커밋) |

---

## 3. 비교 분석

### 3-1. oh-my-openagent의 강점

1. **압도적인 사회적 증명** — 58K+ stars, 4.7K+ forks, Google/Microsoft/Vercel/Deepgram 등에서 실사용
2. **성숙한 에코시스템** — 4개 언어 README, 14개 문서, Discord 커뮤니티, Building in Public
3. **빠른 릴리스 사이클** — 3일 내 3개 버전(v4.2.0→v4.2.2), 자동화된 CI/CD
4. **차별화된 기술** — Hash-Anchored Edit, Discipline Agents, Team Mode, IntentGate
5. **홈페이지** — ohmyopenagent.com (독자 도메인)

### 3-2. oh-my-openagent의 약점

1. **Demo 영상 부재** — 정적 이미지뿐, 실제 동작 GIF/영상 없음
2. **복잡성** — 설정/설치가 에이전트에게 위임되지만, 인간이 직접 하려면 진입장벽 있음
3. **License** — SUL-1.0 (Semi-Open), 완전한 오픈소스가 아님
4. **TypeScript 기반** — Node.js/npm 의존성, oh-my-openagent는 무겁음
5. **OpenCode 전용** — VS Code/Cursor 외 에이전트 지원은 제한적

### 3-3. hera-agent-unity의 차별화 포인트

| 차원 | oh-my-openagent | hera-agent-unity |
|:---|:---|:---|
| **타겟** | 범용 코딩 에이전트 | Unity 개발자 전용 |
| **런타임 의존성** | Node.js, npm, Python | **Zero** (단일 Go 바이너리) |
| **프로토콜** | JSON-RPC / MCP | **직접 HTTP POST** |
| **설치** | 에이전트가 설치 | **curl \| sh 한 줄** |
| **Unity 통합** | 없음 | **네이티브 C# 커넥터** |
| **Custom Tools** | Skill/Plugin 시스템 | **[HeraTool] 어트리뷰트** |
| **도메인 리로드** | 해당 없음 | **서바이브 + 자동 재연결** |
| **AI 에이전트 호환** | OpenCode/Cursor 중심 | **Any shell / Any agent** |
| **라이선스** | SUL-1.0 (Semi-Open) | **MIT (Lite)** |
| **Demo 품질** | 정적 이미지 | **6개 실제 GIF** |

**핵심 메시지**: oh-my-openagent가 "코딩 에이전트의 운영체제"라면, hera-agent-unity는 "Unity 에디터의 CLI 리모컨"이다. 완전히 다른 카테고리.

---

## 4. GeekNews 포스팅 적합성 판단

### 4-1. 보스의 철학 기준 점검

| 기준 | hera-agent-unity Lite | 평가 |
|:---|:---|:---|
| **Demo GIF** | ✅ 6개 GIF (설치, UPM, 상태, Claude Code 연동, 씬 전환, 에이전트 발견) | **통과** |
| **버전** | v0.0.23 (23번 릴리스, 3주간) | **통과** — 빠른 iteration |
| **Author** | ✅ Victor — 6년+ MMORPG 프로덕션, YouTube 채널, 이메일/깃헙 링크 | **통과** |
| **Issue Templates** | ✅ 3개 (Bug, Feature, Question) | **통과** |
| **CHANGELOG** | ✅ Keep a Changelog, 상세 | **통과** |
| **README 완성도** | ✅ 294줄, 영/한双语, 아키텍처 다이어그램, 커맨드 테이블, 커스텀 툴 예제 | **통과** |
| **문서** | ✅ 10개 docs/, TROUBLESHOOTING, DEVELOPMENT, ARCHITECTURE 등 | **통과** |
| **CI/CD** | ✅ GitHub Actions (CI + Release) | **통과** |
| **Landing Page** | ✅ gh-pages, SEO 최적화, Pricing 섹션 | **통과** |

### 4-2. 경쟁력 있는지

**Yes, 경쟁력 있음.** 이유:

1. **카테고리가 다름** — oh-my-openagent와 직접 비교 불가. Unity CLI 도구라는 틈새.
2. **Unity 개발자 Pain Point 정조준** — "AI가 Unity API를 추측하지 않고 직접 실행"
3. **Zero Dependency 설치 경험** — curl \| sh → UPM 추가 → done. 30초.
4. **실제 동작 증거** — 6개 GIF가 실제 Unity 에디터에서 동작하는 모습 보여줌
5. **"시작은 Lite로. 성장하면 Pro로."** — Freemium 모델이 GeekNews 독자에게 어필

### 4-3. 보완이 필요한 부분 (구체적)

#### P0 — 반드시 해결 (포스팅 전)

| # | 항목 | 현재 상태 | 개선 방향 |
|:---|:---|:---|:---|
| 1 | **Stars/커뮤니티 부재** | 0 stars, 0 issues, 0 forks | 포스팅 전에 지인/커뮤니티에서 초기 스타 10~20개 확보. GeekNews 독자는 social proof를 봄 |
| 2 | **GitHub Description 비어있음** | `null` | "Control Unity Editor from CLI. Zero deps. MIT." 등으로 채우기 |
| 3 | **Pro 레포 Demo GIF 부재** | Pro README에 GIF 없음 | Pro용 간단한 GIF 1~2개 (batch, introspection 등) 추가 또는 Lite GIF 재사용 |

#### P1 — 포스팅 직후 개선 (1주 내)

| # | 항목 | 현재 상태 | 개선 방향 |
|:---|:---|:---|:---|
| 4 | **Discord/커뮤니티 채널** | Landing에 링크만 있음 | 실제 활동 중인 Discord 서버 운영. Issue 대신 Discord로 초기 피드백 유도 |
| 5 | **GeekNews용 한글 요약본** | README.ko.md는 있음 | GeekNews 포맷에 맞는 "왜 만들었나" 스토리텔링용 요약 필요 |
| 6 | **Benchmark 수치** | Landing에 "Benchmark" 섹션 있음 | 실제 측정 데이터 채우기 (예: MCP vs hera-agent-unity 설치 시간, 토큰 사용량 비교) |
| 7 | **YouTube 데모 영상** | IndieAlchemist 채널 있음 | 1~2분 짧은 데모 영상 (GIF보다 임팩트 큼) |

#### P2 — 중기 개선 (1개월 내)

| # | 항목 | 현재 상태 | 개선 방향 |
|:---|:---|:---|:---|
| 8 | **GitHub Discussions** | 비활성화 | Q&A, 아이디어 교환용 Discussions 활성화 |
| 9 | **CONTRIBUTING.md** | 없음 | 오픈소스 기여 가이드 추가 |
| 10 | **Pro → Lite 기능 백포트** | Pro가 Lite 상위집합 원칙 | 주요 Pro 기능(batch, introspection)을 Lite에도 제한적으로 노출 → 업그레이드 유도 |
| 11 | **패키지 매니저 등록** | 없음 | Homebrew tap, scoop, winget 등록 — 설치 장벽 더 낮추기 |

---

## 5. 결론

### GeekNews에 올려도 되는가? **YES**

**이유:**
1. 완성도 기준(Demo GIF, 버전, Author, Issue Templates, CHANGELOG, 문서)을 **모두 충족**
2. oh-my-openagent와 **직접 경쟁하지 않음** — 다른 카테고리(Unity 전용 CLI)
3. **"Zero Dependency + 단일 바이너리"** 설치 경험이 GeekNews 독자(개발자)에게 강력한 어필 포인트
4. **Freemium 모델**("시작은 Lite로")이 GeekNews에서 자주 환영받는 구조
5. **6개 실제 GIF**가 제품의 실용성을 증명 — oh-my-openagent조차 GIF가 없음

**단, P0 항목 3개는 포스팅 전에 반드시 해결:**
- GitHub Description 채우기 (5분)
- 초기 스타 10~20개 확보 (지인/트위터/디스코드)
- Pro README에 GIF 추가 또는 Lite GIF로 대체

**포스팅 각도 제안:**
> "AI가 Unity를 추측하는 대신 직접 만지게 만든 CLI 도구 — Python 없이, 설정 없이, curl 한 줄로"

---

*보고서 끝*
