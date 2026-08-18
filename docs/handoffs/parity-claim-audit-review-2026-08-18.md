# Parity Claim Audit — 독립 검수 (2026-08-18)

## 1. Executive verdict

**`NOT DELIVERED`**

요구된 산출물 `docs/handoffs/parity-claim-audit-2026-08-18.md`가 **존재하지 않는다.**
저장소는 `c19e302`에서 clean이며 감사 결과 커밋이 없다.

```
$ ls -la docs/handoffs/parity-claim-audit-2026-08-18.md
ls: cannot access 'docs/handoffs/parity-claim-audit-2026-08-18.md': No such file or directory

$ git status --short --branch
## main...origin/main          (출력 없음 = clean)

$ git log --oneline -2
c19e302 docs(handoff): hand the parity conclusions to a second agent for audit
9ff1315 docs(readme): sync the release status to v0.2.16 across all four channels

$ ls docs/handoffs/
ACTIVE.md
hera-vnext-capability-migration-2026-08-09.md
```

보고서가 없으므로 C1~C12의 **판정(verdict) 자체가 존재하지 않는다.** 판정이
없으니 "판정이 출력과 연결되는가", "설계문서 인용만으로 결론냈는가",
"샘플만 호출하고 전수처럼 썼는가"는 **평가 불가**다. 이 검수는 남아 있는
원시 증거의 커버리지 평가로 축소된다.

**단, 실제 작업은 상당량 수행됐다.** 조작이나 허위 주장이 아니라 **인계 실패**다.
3버킷 Editor가 지금도 실행 중이고, 버킷별 원시 출력과 fixture 산출물이 남아 있다.

## 2. 가장 중대한 문제

1. **산출물 미작성** — 위 참조. 판정·근거·불확실성이 어디에도 기록되지 않았다.
2. **원시 증거가 gitignored 경로에만 존재** — `docs/report/parity-claim-audit-2026-08-18/`
   는 `.gitignore:28`(`docs/report/`)에 걸려 추적되지 않는다.

   ```
   $ git check-ignore -v docs/report/parity-claim-audit-2026-08-18/matrix-rows-raw.csv
   .gitignore:28:docs/report/	docs/report/parity-claim-audit-2026-08-18/matrix-rows-raw.csv
   ```

   커밋 불가·공유 불가이며 **삭제 시 복구 불가**다. 인계 문서는 `docs/handoffs/`를
   지정했다. 이 레포는 같은 날 `docs/report/` 파일 3개를 지워 영구 소실시킨 전례가 있다.
3. **C1의 핵심 요구가 미충족** — `covered` 행을 실제 Hera 호출로 검증한
   machine-readable 결과표가 없다. 남은 `matrix-rows-raw.csv`는 감사 대상 문서
   (`UNITY_PIPELINE_PARITY_MATRIX.md`)를 **재파싱한 것**이지 독립 검증이 아니다.
   컬럼이 `Name, Classification, Hera equivalent, Line` 뿐이고 호출 결과 컬럼이 없다.
4. **C6·C8·C10의 원시 로그 부재** — fixture에 산출물은 남았으나(§6) 명령·출력 로그가 없다.
5. **C11 로그에 요청 본문이 없다** — 응답 12건만 있고 어떤 query가 어떤 응답을 냈는지
   추적 불가(§5).

## 3. C1~C12 검수표

`Codex verdict` 열은 전부 **없음**이다(보고서 미작성). 아래는 원시 증거 커버리지만 평가한다.

| ID | 남은 원시 증거 | 커버리지 판단 | 미충족 사항 |
|---|---|---|---|
| C1 | `unity-source-commands.csv`(161행), `matrix-rows-raw.csv`(153행), `unity-list-6000.{0,3,5}.json`(해시 3개 상이), `unity-live-names-6000.0.txt`(142행), `hera-catalog-6000.{0,3,5}.json`(해시 3개 상이) | **부분** — 161 source-declared − 8 internal = 153 공개 명령이라는 매트릭스 헤더 수치는 **재현됨**. 그러나 `covered` 행 실호출 검증표가 없음 | 행별 실호출 결과표, 호출 못 한 행의 개별 BLOCKED 표기, 입력·출력·부작용 동등성 판단 |
| C2 | `c2-search-probe.cs`, `c2-create-query-6000.{0,3,5}.json`, `c2-settled-search-6000.{0,3,5}.json` | **양호** — 3버킷 × (직후/안정화 후) 분리 수집. `6000.3`·`6000.5`의 create-직후 출력이 바이트 동일(둘 다 0건 결과로 추정) | `ref:`/`dep:`/property query별 판정문, AssetDatabase 결과와의 대조표 |
| C3 | `signal-tick-probe.cs` | **미흡** — 접근성 프로브만 있고 **cadence 측정 파일이 없다**. 요구된 "컴파일·베이크 중 비포커스 측정" 증거 없음 | heartbeat 간격 측정 로그(3버킷) |
| C4 | `c4-package-resolve-6000.{0,3,5}.json`(해시 상이), `c4-recompile-status-6000.{0,5}.json` | **부분** — resolve는 3버킷. recompile-status는 **6000.3 누락** | `6000.3` recompile-status, 완료 신호 대안 탐색 결과 |
| C5 | `c5-external.png`(68바이트) | **미흡** — 입력 파일만 있고 워크플로 수행 로그 없음 | 파일시스템 도구 없는 호출자 관점 시도 기록, import settings 도달 가능성 |
| C6 | 원시 로그 **없음**. fixture에 `Assets/HeraParityAudit/C6`, `C6Root`, `C6OutsideHera` 잔재 존재 | **불충분** — 산출물로 보아 수행됐으나 명령·출력 미보존 | 실행 로그 전량 |
| C7 | 원시 로그 **없음**. `Test6.0.35f1/Assets/HeraParityAudit/C7/`에 `Container.asset`, `Handle.prefab`, `MovableMoved.mat` 잔재 | **불충분** — 산출물 이름이 sub-asset·move 시나리오와 일치하나 5개 도구 호출 결과가 없음 | `guid:`/`guid:fileId`/GlobalObjectId × 5도구 호출 로그, move 전후 대조 |
| C8 | 원시 로그 **없음**. fixture에 `C8` 폴더 잔재 | **불충분** | list_overrides/apply/revert/unpack 출력, 리로드 후 identity 유지 확인 |
| C9 | `c9-audit-start-6000.0.json`(1버킷) | **미흡** — 3버킷 중 1개. rules 패키지 설치 시도 기록 없음 | `6000.3`·`6000.5`, positive fixture 확보 시도 |
| C10 | 증거 **없음** | **없음** | ledger 행 확인, 승인 게이트 이후 exec fallback 동작 확인 |
| C11 | `c11-compact-mcp.txt`(25KB, UTF-16LE), `mcp-probe/main.go` | **양호** — §5 참조. 결정적 응답 2건 확보 | 12개 workflow별 query 귀속, shell caller 관점 분석 |
| C12 | `c12-camera-setup.cs` | **미흡** — 셋업 스크립트만 있고 공식/헤라 출력 대조가 없음 | recompile 5상태·카메라 지정·max_resolution·version gate 각각의 대조 출력 |

## 4. 3버킷 실행표

세 버킷 Editor가 **검수 시점에도 실행 중**이며 heartbeat가 살아 있다.

| 버킷 | 요구 버전 | 실제 heartbeat | 프로젝트 | port | state | Connector |
|---|---|---|---|---|---|---|
| 6000.0–6000.2 | 6000.0.35f1 | `6000.0.35f1` ✔ | `Test6.0.35f1` | 8093 | ready | manifest에 `com.notnull92.hera-agent-unity` 있음 |
| 6000.3–6000.4 | 6000.3.5f2 | `6000.3.5f2` ✔ | `test6000.3.5f2` | 8096 | ready | 동일 |
| 6000.5+ | 6000.5.6f1 | `6000.5.6f1` ✔ | `test6.5` | 8097 | ready | 동일 |
| (참고) 사용자 프로젝트 | — | `6000.3.5f2` | `Inventoria` | 8090 | ready | — |

공식 패키지 버전은 세 fixture 모두 manifest에 `"com.unity.pipeline": "0.5.0-exp.1"` 로
확인됐고, 이는 매트릭스 헤더의 baseline과 **일치**한다.

**미확인**: console compiler error 0건 증거는 `hera-console-errors-6000.0.json` 1건뿐이다
(내용: `matched: 0`, `total_in_console: 2`). `6000.3`·`6000.5`는 증거 없음 → 해당
버킷의 "compiler error 0" 주장은 **BLOCKED**.

Connector 버전이 `0.1.2`인지는 fixture manifest가 git URL 참조라 버전 문자열로
확정할 수 없다. `manage_packages list` 출력이 남아 있지 않아 **BLOCKED**.

## 5. C11 Compact MCP 결과표

로그에서 확인된 것:

- 서버 identity: `hera-agent-unity` `v0.2.16` ✔ (요구된 설치 버전과 일치)
- `list_tools` 결과에 Compact 3-tool 노출
- `tool_search` 응답 **12건** — 12개 workflow 수와 일치하며 모두 `"data": []`
- `tool_describe` → `{"code":"TOOL_NOT_FOUND","message":"tool \"exec\" was not found"}`
- `tool_call` → `{"code":"ARBITRARY_CODE_PERMISSION_REQUIRED","message":"arbitrary-code tool requires explicit server startup permission"}`

| 요구 구분 | 확인 여부 |
|---|---|
| 검색되지 않는가 | **부분** — `tool_search` 12건이 전부 빈 결과. 다만 요청 본문이 로그에 없어 **어느 workflow의 검색인지 귀속 불가** |
| describe되지 않는가 | **확인** — `exec`에 대해 `TOOL_NOT_FOUND` |
| tool_call 대상이 없는가 | **확인** — `ARBITRARY_CODE_PERMISSION_REQUIRED` |
| arbitrary-code 권한 때문에 막히는가 | **확인** — 위 코드가 명시적 |
| approval 때문에 막히는가 | 미확인 |
| 실행 가능하나 외부 filesystem 도구 필요한가 | 미확인 |
| Compact 단독 workflow 완결 여부 | 12개 개별 판정 없음 |
| shell caller 관점 분석 | 증거 없음 |

**평가**: C11의 핵심 주장(Compact 기본값에서 arbitrary-code 탈출구가 닫힌다)을
뒷받침하는 **결정적 응답 2건이 실제로 확보됐다.** 그러나 12개 workflow별 귀속과
shell caller 분석이 없어 "duplicate 12건 전부"로 일반화할 근거는 아직 부족하다.

## 6. 저장소 오염·복구 상태

**저장소(hera-agent-unity)**: 오염 없음.

```
$ git status --short --branch   → clean
$ git diff --name-only          → (없음)
```

생산 코드·package version·catalog baseline·generated contract·README·CHANGELOG
**전부 미변경**. 요구된 허용 범위(보고서 1개)조차 없으므로 오염이 아니라 **부재**다.

**fixture 오염**: 세 fixture 모두 잔재가 남아 있다.

| fixture | `Assets/HeraParityAudit/` 하위 | pipeline 패키지 |
|---|---|---|
| `Test6.0.35f1` | `C2 C5 C6 C6OutsideHera C6Root C7 C8 Import Scenes Tests` | `0.5.0-exp.1` 잔류 |
| `test6000.3.5f2` | `C2 C5 C6 …` | `0.5.0-exp.1` 잔류 |
| `test6.5` | `C2 C5 C6 …` | `0.5.0-exp.1` 잔류 |

세 곳 모두 일회용 fixture이므로 심각도는 낮으나 **정리되지 않았다**. Editor 프로세스
3개도 실행 중이다.

**사용자 프로젝트 `Inventoria`**: 오염 없음. `com.unity.pipeline` 미설치,
`Assets/HeraParityAudit` 없음. (git에 보이는 수정은 전투 튜닝 관련 진행 중 작업으로
이번 감사와 무관.)

**부수 발견**: `docs/report/26.08.18_report.md`가 재생성됐다(오늘 16:52). 오늘 오전
삭제된 파일과 동명이며 내용은 새 이슈다 — catalog domain epoch 불일치(재컴파일 직후 첫
`call` 실패, 2회 재현), `editor restart`의 `UnityLockfile` stale 삭제 실패 경고(2회 재현).
이 검수 범위 밖이지만 gitignored 경로라 **다시 소실될 수 있다.**

## 7. 실행한 최종 검증 명령과 exit code

저장소 무결성 검증이며 **C1~C12의 실Editor 감사를 증명하지 않는다.**

| 명령 | exit | 비고 |
|---|---|---|
| `go test ./...` | `0` | 실패 패키지 없음 |
| `go run ./tools/generate-runtime-contracts --check` | `0` | drift 없음 |
| `go run ./tools/sync-agent-guides --check` | `0` | 파생 가이드 동기 |
| `go run ./tools/validate-connector-package` | `0` | `connector package integrity PASS` |

## 8. 수정 없이 남겨야 할 후속 결정 목록

이 패스에서는 아무것도 구현·수정하지 않았다. 아래는 전부 사용자 결정 사항이다.

1. **재실행 vs 회수** — 원시 증거가 살아 있고 3버킷 Editor가 아직 떠 있으므로, Codex에게
   *보고서 작성만* 다시 시킬 수 있다. 다만 C3·C5·C6·C7·C8·C9·C10·C12는 로그가 없어
   재실행이 필요하다.
2. **증거 경로 규칙** — 원시 증거를 `docs/report/`(gitignored)에 두는 관행을 유지할지,
   감사 증거만 추적 경로로 옮길지. 현 상태로는 커밋도 공유도 안 되고 삭제 시 복구 불가다.
3. **C1 전수 검증의 비용** — `covered` 126행 실호출은 이번에 수행되지 않았다. 전수로 갈지,
   위험 기반 표본(입력·부작용이 비대칭인 행 우선)으로 축소할지.
4. **C11 일반화 범위** — 결정적 응답 2건은 확보됐다. 12개 workflow 개별 귀속까지 요구할지,
   "Compact 기본값에서 arbitrary-code 경로가 닫힌다"만 확정하고 나머지를 별건으로 둘지.
5. **fixture 정리** — 세 fixture의 `HeraParityAudit` 잔재·`com.unity.pipeline`·실행 중인
   Editor 3개를 정리할지, 재실행 대비로 보존할지.
6. **재생성된 `26.08.18_report.md`의 두 이슈** — catalog domain epoch 불일치와
   `UnityLockfile` 경고는 별도 트랙이다. 이번 감사 결론과 무관하게 처리 여부 결정 필요.
