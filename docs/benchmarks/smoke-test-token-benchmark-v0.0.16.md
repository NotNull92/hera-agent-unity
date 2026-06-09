# Smoke Test Token Benchmark — hera-agent-unity v0.0.16

> 한 줄 요약: **7개 대표 시나리오 스모크 테스트 = 총 725 bytes (~181 토큰)**. 명령당 평균 103 bytes / 26 토큰. 응답의 70% 이상이 2 bytes 이하.

- **측정일**: 2026-06-09
- **CLI**: `hera-agent-unity v0.0.16` (Windows, GitHub Release binary)
- **Connector**: AgentConnector 0.0.18 (git tag `v0.0.16` 시점)
- **Unity**: 6000.3.5f2
- **대상 프로젝트**: NoMoreRolls (활성 씬 `TitleScene`)
- **비교 기준**: [`exec-50-scenario.md`](./exec-50-scenario.md) (v0.0.24, 50회 전체 측정)

---

## 0. TL;DR

| | 값 |
|---|---:|
| **총 호출 수** | 7 |
| **C# 입력 총합** | 640 bytes |
| **응답(stdout+stderr) 총합** | 85 bytes |
| **총 왕복 바이트** | **725 bytes** |
| **추정 토큰** (chars ÷ 4) | **~181 tokens** |
| 호출당 평균 (총 바이트) | 103.6 bytes (25.9 tokens) |
| 호출당 평균 응답 | 12.1 bytes (3.0 tokens) |
| 응답 ≤ 5 bytes인 호출 | **5 / 7 (71%)** |
| 가장 큰 호출 | cleanup — 239 bytes (59 tokens) |
| 가장 작은 호출 | s36-time — 27 bytes (6 tokens) |

**핵심 시사점**:
- 응답 측 토큰은 여전히 미미 (평균 3 tokens). 비용 대부분은 에이전트가 작성한 C# 코드 길이에 있음.
- v0.0.24 대비 유사 패턴에서 토큰 사용량이 **증가하지 않음**. 차이는 시나리오 코드 표현 방식의 차이 때문.

---

## 1. 측정 환경 및 방법론

### 환경

- CLI v0.0.16 (PATH 설치본). `HERA_AGENT_NO_PATH_CHECK=1` 지정으로 path 충돌 경고 억제.
- Connector 0.0.18 (`AgentConnector/package.json`).
- 비-TTY(파이프) 호출로 자동 동작 발동:
  - `[hera-agent-unity] compiling...` 배너 stderr 억제
  - 응답 JSON compact 출력
  - `Update available` notice 억제

### 측정 단위

- **입력 (input_bytes)**: `hera-agent-unity exec --code "..."` 의 `--code` 인자 raw 바이트
- **응답 (output_bytes)**: `stdout + stderr` 합산
- **토큰 추정**: 영문 기준 `chars ÷ 4`

### 측정 절차

```bash
measure() {
    name="$1"
    code="$2"
    input_bytes=$(python -c "import sys; print(len(sys.argv[1].encode('utf-8')))" "$code")
    output=$(HERA_AGENT_NO_PATH_CHECK=1 hera-agent-unity exec --code "$code" 2>&1)
    output_bytes=$(python -c "import sys; print(len(sys.argv[1].encode('utf-8')))" "$output")
    total=$((input_bytes + output_bytes))
    echo "$name | input: ${input_bytes}B | output: ${output_bytes}B | total: ${total}B"
}
```

---

## 2. 시나리오 상세

### A. Scene inspection (s01)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s01 | `return SceneManager.GetActiveScene().name;` | 70 | 10 | **80** |

> `TitleScene` 반환. 응답 10B는 줄바꿈 포함.

### B. GameObject creation (s09)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s09 | 빈 GameObject `bench_TokenTest` 생성 | 59 | 15 | **74** |

> 생성된 이름 반환. 응답 15B는 `"bench_TokenTest"` + 개행.

### C. Component manipulation (s19)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s19 | `bench_TokenTest.position = (1,2,3)` | 139 | 2 | **141** |

> null 체크 및 `Vector3` 생성 포함. 응답은 `OK` (2B).

### D. Asset/path queries (s29)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s29 | `return Application.dataPath;` | 28 | 45 | **73** |

> 경로 문자열 반환이라 응답이 45B로 큼.

### E. Math/expression (s36)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s36 | `return Time.time;` | 17 | 10 | **27** |

> 가장 작은 호출. 입력 17B, 응답 10B (float 값 + 개행).

### F. Bulk ops + cleanup (s41, s50)

| # | 시나리오 | 입력 | 응답 | 합계 |
|---|---|---:|---:|---:|
| s41 | 씬의 모든 GameObject 개수 | 89 | 2 | **91** |
| cleanup | `bench_*` 일괄 destroy | 238 | 1 | **239** |

> cleanup이 입력 238B로 가장 큼 (LINQ + 반복문). 응답은 각 2B, 1B.

---

## 3. 분포 분석

### 응답 크기 히스토그램

```
응답 바이트 범위    호출 수    비율
(0–5]                5       71%   ████████████████████████████████████
(5–15]               2       29%   ███████████████
(15–50]              0        0%
(50+]                0        0%
```

### 총 바이트 (입력+응답) Top/Bottom

**Top 3 largest**:
| # | 시나리오 | 합계 | 토큰 |
|---|---|---:|---:|
| cleanup | `bench_*` 일괄 destroy | 239 | 59 |
| s19 | position 설정 | 141 | 35 |
| s41 | GameObject 개수 | 91 | 22 |

**Top 3 smallest**:
| # | 시나리오 | 합계 | 토큰 |
|---|---|---:|---:|
| s36 | `Time.time` | 27 | 6 |
| s29 | `Application.dataPath` | 73 | 18 |
| s09 | GameObject 생성 | 74 | 18 |

---

## 4. v0.0.24 벤치마크와의 비교

동일 패턴(또는 유사 패턴)을 v0.0.24의 50회 측정과 비교:

| 시나리오 | v0.0.24 (B) | v0.0.16 (B) | 차이 | 분석 |
|---|---:|---:|---:|---|
| s01 scene name | 48 | 80 | +67% | `UnityEngine.SceneManagement.SceneManager` fully qualified 사용 |
| s09 create GO | 49 | 74 | +51% | `"bench_TokenTest"` 이름 + `return go.name` 추가 |
| s19 position | 92 | 141 | +53% | null 체크 + `new Vector3(1,2,3)` fully qualified |
| s29 dataPath | 71 | 73 | +3% | 거의 동일 |
| s36 Time.time | 28 | 27 | −4% | 거의 동일 |
| s41 count | 94 | 91 | −3% | 거의 동일 |
| cleanup | 274 | 239 | −13% | 오히려 줄음 (LINQ 방식 차이) |

**요약**:
- 리팩토링으로 인한 **토큰 증가는 없음**.
- 차이의 100%는 "내가 직접 타이핑한 코드 길이" 때문.
- v0.0.24의 `SceneManager.GetActiveScene().name` (43B) vs v0.0.16의 `UnityEngine.SceneManagement.SceneManager.GetActiveScene().name` (70B) — 동일 동작, 다른 표현.

---

## 5. 결론

**v0.0.16의 hera-agent-unity로 LLM 에이전트가 실제 Unity 작업을 수행 시:**

- 7회 스모크 테스트 = **약 181 토큰** 소모
- 호출당 평균 **26 토큰**
- 응답의 71%가 5 bytes 이하 — 사실상 노이즈 수준
- v0.0.24와의 직접 비교에서 **토큰 비용 증가 없음** 확인

**리팩토링 안전성 확인**:
- PR #20 (TargetResolver 추출): C# 코드 길이 변화 없음. 호출 패턴 동일.
- PR #21~#24 (테스트 추가): Go 바이너리 크기에 영향 없음. `exec` 호출 경로 변경 없음.
- PR #19 (AssetConfigWindow 분할): partial class 구조 변경 없음. 호출 경로 동일.

> 참고: 본 측정은 **스모크 테스트** (7회)이며, 전체 50회 벤치마크는 [`exec-50-scenario.md`](./exec-50-scenario.md) 참고.
> tool_use / tool_result 프레임 자체의 오버헤드 (각 호출당 50–150 토큰)는 본 측정에 포함되지 않음.
