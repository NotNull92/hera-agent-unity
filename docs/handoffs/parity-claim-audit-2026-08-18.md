# Pipeline Parity Claim Audit - Final Report (2026-08-18)

> **Status: COMPLETE.** `docs/CODEX_HANDOFF_PARITY_CLAIM_AUDIT.md`의 C1-C12를 모두 판정했다. 설계 문서의 결론을 근거로 재사용하지 않고, 공식 Unity CLI 소스, 공식 CLI 실호출, 설치된 Hera CLI `v0.2.16`, Connector `0.1.2`, 세 Unity 버킷의 live Editor 결과를 기준으로 판단했다. 반증된 항목은 이 패스에서 구현하지 않았다.

## 1. 최종 판정

| ID | 판정 | 핵심 결론 |
|---|---|---|
| C1 | `REFUTED` | 153개 명령 집합과 분류 수는 완전했지만, `covered`, `duplicate`, `conditional` 중 실제 출력과 caller model에 맞지 않는 행이 존재해 "complete and correct"라는 전체 주장은 거짓이다. |
| C2 | `CONFIRMED` | Unity Search는 create 직후 AssetDatabase보다 늦었고, 안정화 뒤에도 `dep:`와 `#property`는 추가 답을 주지 않았다. 단, 과거의 정확한 버킷별 1/0/0 표본은 재현되지 않았다. |
| C3 | `CONFIRMED` | `SignalTick`은 세 버킷 모두 non-public이며, auto-tick off + 최소화 상태에서 heartbeat, 실제 compile/domain reload, lighting bake가 포커스 복귀 없이 진행됐다. |
| C4 | `REFUTED` | `Client.Resolve()`는 실제로 `void`지만, 공식 Pipeline은 세 버킷 모두 완료 상태와 후속 `recompile_status`를 관측해 계약화한다. "불가능"은 틀렸다. |
| C5 | `REFUTED` | 공식 `import_asset`은 외부 절대경로를 직접 가져오지만 Hera에는 ingress 액션이 없다. 매트릭스가 주장한 대체 수단은 "exec/filesystem tool"이므로, 그 둘을 갖지 못한 caller(C11)에게는 duplicate가 아니다. |
| C6 | `REFUTED` | 공식 authoring root는 작업 단위로 쓰기 경계를 좁힌다. Hera의 `AssetPathGuard`는 `Assets/` containment를 실행 시점에 강제하지만 그 경계는 고정이고 더 좁힐 수단이 없다. 안전 구멍이 아니라 부재한 구성 능력이며 막힌 workflow 증거는 아직 없다. |
| C7 | `REFUTED` | durable handle coverage는 세 버킷에서 확인됐지만, resolver는 형식별 단일 경로를 사용하고 실패 단계도 이미 구체적으로 말한다. "attempted strategies를 보고하지 않는 남은 gap"이라는 절이 틀렸다. |
| C8 | `REFUTED` | `GlobalObjectId(target) + propertyPath + value + objectReference` 조합이 실제 domain reload 전후 세 버킷 모두 byte-identical했다. reload-safe per-record key가 없다는 전제는 틀렸다. |
| C9 | `REFUTED` | Project Auditor + Rules가 설치된 6000.0 fixture에서 실제 scan이 완료되고 18,995 issues를 반환했다. positive fixture는 현재 얻을 수 있다. |
| C10 | `REFUTED` | `exec` fallback은 non-interactive CLI에서 approval round trip이 필요하고 Compact MCP에서는 검색, 설명, 호출 자체가 막힌다. 모든 caller에게 유효한 fallback이 아니다. |
| C11 | `CONFIRMED` | Compact MCP default는 3-tool surface만 노출하며, 12개 duplicate workflow 검색은 모두 빈 결과, `exec` 직접 호출은 permission required였다. |
| C12 | `REFUTED` | 공식 `capture_game_view`의 camera/max-resolution 기능과 Hera screenshot은 동등하지 않고, 공식 compile operation 상태와 Hera Editor liveness 상태도 다른 질문에 답한다. |

**합계:** `CONFIRMED 3`, `REFUTED 9`, `BLOCKED 0`.

## 2. 감사 기준과 실행 환경

감사 대상 기준 revision은 handoff commit `c19e30217ba2f3586d09b8a8bdce49f416243c15`이다.

감사 도중 `main`과 `origin/main`은 다른 작업자의 동시 감사 커밋으로 `3bddd0618504d66a891618e625b43d8a7394b299`까지 전진했다. 그 변경에는 Go CLI target-discovery race fix, C1 read-only paired run, matrix와 문서 정정이 포함되지만 기준 commit 이후 `AgentConnector/` 변경은 없다. claim 판정용 live 호출은 설치된 CLI `v0.2.16`과 Connector package `0.1.2`를 사용했다.

공유 문서에는 머신 절대경로를 남기지 않는다. 아래 토큰을 사용한다.

| 토큰 | 의미 |
|---|---|
| `%HERA_AUDIT_FIXTURE_6000_0%` | Unity `6000.0.35f1` disposable fixture |
| `%HERA_AUDIT_FIXTURE_6000_3%` | Unity `6000.3.5f2` disposable fixture |
| `%HERA_AUDIT_FIXTURE_6000_5%` | Unity `6000.5.6f1` disposable fixture |
| `%HERA_AUDIT_EVIDENCE%` | 로컬 raw evidence 디렉터리, `docs/report/parity-claim-audit-2026-08-18/` |

| 항목 | 값 |
|---|---|
| Official Unity CLI | `1.0.0-beta.3` |
| `com.unity.pipeline` | `0.5.0-exp.1` |
| Hera CLI | `v0.2.16` |
| Hera Connector | `0.1.2` |
| Hera live catalog | 세 버킷 모두 `34 tools / 132 actions` |
| Connector release-gate tests | 세 버킷 모두 `26/26 PASS` |

## 3. 감사 방식

1. 공식 package source에서 `[CliCommand]` 선언을 직접 추출했다.
2. 내부 test command 8개를 제외한 public set과 matrix의 행 집합을 비교했다.
3. 공식 `unity list`와 Hera `list --catalog`를 세 버킷에서 다시 얻었다.
4. C2-C12는 같은 fixture state에서 공식 명령과 Hera 명령을 실제 호출했다.
5. Connector 관련 판단은 세 버킷 모두 실행했다. package 부재는 PASS로 바꾸지 않고 그대로 기록했다.
6. 승인 대상 작업은 `--yes`를 자동으로 붙이지 않았다. 안전 경계 자체가 쟁점인 경우 preflight 또는 dry-run 결과를 증거로 사용했다.
7. Connector production catalog baseline, test assembly 활성화, EditMode release-gate 26개, manifest byte-for-byte 복원을 세 버킷에서 다시 실행했다.

C1의 126 `covered` 행은 전부 live tool/action contract에 매핑했고, 120개의 고유 mapping으로 정규화했다. 그중 의미가 의심되는 행은 공식 명령과 Hera 명령을 같은 fixture에서 양쪽 실호출했다. 378개의 파괴적 paired mutation을 자동 승인하지는 않았다. C1은 universal claim이므로 C12와 C9의 실제 counterexample 하나만으로도 `complete and correct`가 반증되며, 추가 자동 승인은 판정을 바꾸지 않고 안전 규칙만 위반한다.

---

## C1 - parity matrix가 complete and correct인가

### 판정: `REFUTED`

공식 package source 추출 결과:

```text
[CliCommand] declarations       161
internal/test commands excluded   8
public commands                 153
```

제외한 내부/test 명령:

```text
job_test_cancellable
job_test_delayed_progress
job_test_wait
log_editor
progress_test_wait
test_structured
test_tagged
test_types
```

source public set과 기준 matrix를 exact set으로 비교한 결과:

```text
source_public                 153
matrix_rows                   153
source_missing_from_matrix      0
matrix_not_in_source            0
```

분류 수 또한 기준 문서와 일치했다.

```text
covered       126
duplicate      12
rejected        7
excluded        6
conditional     2
```

공식 live Editor surface는 각 버킷에서 142개였다. 나머지 11개는 runtime 또는 test 성격의 선언이다. Hera는 각 버킷에서 동일한 `34 tools / 132 actions`를 노출했다.

126개 `covered` 행의 Hera mapping을 live catalog에 대조했다. matrix의 축약 표현을 다음처럼 정규화하면 실제로 사라진 tool/action은 없었다.

```text
manage_gameobject name   -> manage_gameobject set_name
manage_gameobject active -> manage_gameobject set_active
manage_gameobject parent -> manage_gameobject set_parent
input mouse/click        -> input mouse 또는 input click
manage_packages/task     -> manage_packages 또는 task
```

동시 감사 runner가 read-only mapping 29개를 세 버킷에서 official/Hera paired call로 실행했다.

| Bucket | Paired rows | Both success | Both fail | One-sided disagreement |
|---|---:|---:|---:|---:|
| `6000.0.35f1` | `29` | `28` | `1` | `0` |
| `6000.3.5f2` | `29` | `29` | `0` | `0` |
| `6000.5.6f1` | `29` | `29` | `0` | `0` |

6000.0의 1쌍은 양쪽 모두 같은 `/Main Camera` fixture 부재로 실패했다. 한쪽만 성공한 row는 87회 중 0개였다. 이 lane은 read-only equivalence가 대체로 정확하다는 반증 방지 근거다. 동시에 `list_open_scenes`의 기존 `scene list --loaded false` 표기는 active Scene을 포함하지 않아 imprecise했고, actual response에 맞게 `scene info`로 정정됐다.

그러나 claim은 "이름이 존재한다"나 "read-only lane이 대체로 맞다"가 아니라 matrix 전체가 올바르다는 universal claim이다. 다음 actual output이 `covered`와 `conditional`의 정확성을 반증한다.

- `capture_game_view -> screenshot --view game`: 공식 camera/max-resolution이 Hera에 없다. 세 버킷 모두 동일하다.
- `recompile_status -> status`: 공식은 compile operation 결과를, Hera는 Editor liveness를 반환한다.
- `audit/audit_status -> conditional`: rules-enabled positive fixture가 실제로 완료됐다.
- 12개 `duplicate`: Compact MCP와 filesystem-less caller에서는 fallback이 닿지 않는다.

Connector release-gate를 세 버킷에서 다시 실행한 결과는 모두 다음과 같다.

```json
{"total":26,"passed":26,"failed":0,"skipped":0,"failures":[]}
```

### 실행 기반 보강 (교차검증)

이름 대조는 "존재한다"까지만 증명한다. `covered` 행을 실제로 두 표면에서 같은 fixture
상태에 대고 실행한 결과를 덧붙인다. 총 **123쌍**이며 한쪽만 성공한 행은 0건이다.

```text
read-only 29쌍  x 3버킷 : 6000.0 28 both-answered + 1 both-failed / 6000.3 29 / 6000.5 29
mutating  12행  x 3버킷 : 세 버킷 모두 12/12 both-answered
한쪽만 성공한 행         : 0
```

mutating 12행은 두 표면의 입력이 비대칭이거나 부작용의 동일성이 자명하지 않은 행,
즉 잘못된 `covered`가 숨을 만한 곳을 골랐다: `create_gameobject`, `set_transform`,
`add_component`, `set_component_properties`, `create_asset`, `copy_asset`, `move_asset`,
`delete_asset`, `set_material_properties`, `set_physics_settings`(dry_run),
`create_prefab`, `create_timeline`.

`move_asset`과 `delete_asset`은 승인 게이트 대상이다. `--yes`를 쓰지 않고 매 버킷마다
실제 preflight 후 동일 요청을 단일사용 `--approve`로 재호출해 통과시켰다. 증거:
`docs/report/parity-claim-audit-2026-08-18/c1-readonly-6000.{0,3,5}.jsonl`,
`c1-write-6000.{0,3,5}.jsonl`.

실행으로만 드러난 오분류가 **2건** 더 있다. 이름 대조로는 잡히지 않는다.

- `list_open_scenes -> scene list/info`: `scene list`는 Build Settings 씬 목록을 답한다.
  열린 씬을 답하는 것은 `scene info`뿐이다.
- `create_asset -> manage_assets create`: 이 액션은 ScriptableObject `.asset` 전용이라
  Material을 요청하면 `TYPE_NOT_FOUND`다. 실제 능력은 `manage_material create`,
  `manage_animation create_clip`, `manage_timeline create`, `manage_prefab create`로
  타입별로 나뉘어 있다.

두 행 모두 매트릭스를 수정했다. 이는 위 네 개 근거와 독립적으로 C1 `REFUTED`를 보강한다.

즉 matrix는 **명령 inventory로서는 완전하지만 capability classification으로서는 올바르지 않다.** 따라서 C1은 `REFUTED`다.

---

## C2 - Unity Search rejection 근거

### 판정: `CONFIRMED`

같은 Editor 호출 안에서 Material과 이를 참조하는 Prefab을 생성한 직후 다음을 비교했다.

```text
AssetDatabase reverse dependency scan
SearchService ref:<guid>
SearchService ref:<path>
SearchService dep:<guid>
SearchService dep:<path>
SearchService #m_Name=...
SearchService t:Material ...
```

즉시 결과:

| Bucket | AssetDatabase reverse | Unity Search |
|---|---:|---|
| `6000.0.35f1` | `1` | 모든 질의 `0` |
| `6000.3.5f2` | Hera reverse deps 즉시 `1` | synchronous Search가 공식 Pipeline 5초 main-thread limit 초과 |
| `6000.5.6f1` | Hera reverse deps 즉시 `1` | 동일 timeout |

인덱스 안정화 뒤 세 버킷의 결과는 같았다.

| Query | Count |
|---|---:|
| `ref:<guid>` | `0` |
| `ref:Assets/.../HeraC2Target.mat` | `1` |
| `dep:<guid>` | `0` |
| `dep:<path>` | `0` |
| `#m_Name=HeraC2Target` | `0` |
| `t:Material HeraC2Target` | `1` |

동시에 Hera `manage_assets deps --direction reverse`는 세 버킷 모두 정확히 참조 Prefab 1개를 반환했다.

따라서 다음은 확인됐다.

- Search index는 create 직후 AssetDatabase보다 늦다.
- `ref:<path>`가 안정화 후 찾은 답은 Hera reverse deps가 이미 즉시 찾은 답이다.
- `dep:`와 `#property` 계열은 이번 세 버킷에서 추가 결과를 주지 않았다.
- Search에서만 얻을 수 있는 답은 발견되지 않았다.

다만 commit `c56c1c1`에 적힌 정확한 historical sample, 즉 `6000.0=1`, `6000.3=0`, `6000.5=0`은 재현되지 않았다. 이번에는 6000.0도 즉시 `0`이었다. 이는 "intermittent lag" 결론과는 일치하지만, 과거 commit body의 버킷별 숫자를 고정 사실로 재인용해서는 안 된다.

---

## C3 - set_autotick이 실제 결과를 바꾸는가

### 판정: `CONFIRMED`

세 버킷에서 공식 명령으로 auto-tick을 끄고 지속 설정했다.

```bash
unity command set_autotick --project-path "%HERA_AUDIT_FIXTURE%" -- \
  --enable false --persist true
```

reflection 결과는 모두 동일했다.

```json
{
  "found": true,
  "is_public": false,
  "is_private": false,
  "signature": "Void SignalTick()"
}
```

즉 `EditorApplication.SignalTick`은 public API가 아닌 internal static method다.

그 상태에서 Unity 창을 실제로 최소화하고, heartbeat 10회, 실제 C# file 추가로 compile/domain reload, lighting bake를 실행했다.

| Bucket | Idle heartbeat min/avg/max | Compile | Domain epoch | Lighting bake |
|---|---|---|---|---|
| `6000.0.35f1` | `1089 / 1098.2 / 1102 ms` | exit `0`, `10546 ms` | changed | exit `0`, `completed`, `7136 ms` |
| `6000.3.5f2` | `1083 / 1085.3 / 1088 ms` | exit `0`, `11952 ms` | changed | exit `0`, `completed`, `3086 ms` |
| `6000.5.6f1` | `1070 / 1085.9 / 1092 ms` | exit `0`, `13077 ms` | changed | exit `0`, `completed`, `5451 ms` |

Compile 중 최대 heartbeat gap은 각각 `3076`, `3596`, `4063 ms`였고 state는 `ready -> compiling -> reloading`을 관측했다. Domain reload 동안 heartbeat assembly 자체가 재생성되므로 이 gap은 focus stall이 아니다. 세 compile은 포커스를 되돌리지 않고 끝났다. Bake도 세 버킷 모두 완료됐다.

모든 창은 측정 뒤 복원했고 auto-tick도 다시 켰다.

### 관측된 별도 incident

6000.0 fixture에서 C3 정리 뒤 공식 Pipeline은 `ready`인데 Hera listener heartbeat가 stale `reloading`으로 남는 lifecycle anomaly가 한 번 발생했다. compiler error는 없었다. disposable Scene을 저장하고 `EditorApplication.update` callback으로 해당 fixture만 정상 종료한 뒤 재실행해 `ready`, 34-tool catalog를 복구했다. 이 incident는 auto-tick off 상태의 compile/bake가 멈췄다는 증거가 아니며, 이후 concurrent commit `f09c589`의 target-discovery race 수정과도 구분해야 한다.

---

## C4 - package resolve는 void이므로 계약화할 수 없는가

### 판정: `REFUTED`

세 버킷 reflection 결과는 claim의 첫 전제와 일치했다.

```text
Void Resolve()
return_type = System.Void
parameters = []
```

그러나 "따라서 completion contract가 불가능하다"는 결론은 실제 공식 Pipeline 동작과 맞지 않았다.

세 버킷 모두:

```json
{
  "operation": "resolve",
  "status": "completed",
  "applied": true,
  "requiresRecompile": true
}
```

후속 `recompile_status`도 `completed` 또는 `idle`을 반환했고 Hera Editor는 다시 `ready`에 도달했다.

즉 `Client.Resolve()`의 반환형이 `void`인 것은 사실이지만, manifest/lock 관측과 후속 compile 상태를 묶어 completion을 계약화할 수 있다. 공식 Pipeline이 이미 그 형태를 구현한다. C4의 절대 주장 "impossible to contract"는 `REFUTED`다.

> **보강 (교차검증).** 계약화가 가능한 것과 그 계약이 정직한 것은 별개다. 이미 해석된
> fixture에서 `applied: true` 직후 `packages-lock.json`은 해시와 mtime이 모두 불변이었다:
>
> ```text
> before  ab212bee56f04e67123b88be0aaacb2f   2026-08-18 16:51:09.545664000
> after   ab212bee56f04e67123b88be0aaacb2f   2026-08-18 16:51:09.545664000
> ```
>
> no-op이므로 무변화가 정상이지만, 이 응답만으로는 "해석이 성공했다"와 "무조건 그렇게
> 보고한다"를 구분할 수 없다. Hera의 원래 거절 사유가 겨냥한 지점이 이것이다.

### capability implication

Hera에 추가할지는 별도 결정이다. 추가하려면 다음 evidence가 필요하다.

- resolve 전후 실제 unresolved manifest fixture
- package job/file-bus 완료 계약
- failure와 no-op 구분
- package mutation approval 및 operation ledger 영향
- 세 버킷 regression
- catalog payload baseline review

이 패스에서는 구현하지 않았다.

---

## C5 - import_asset은 duplicate인가

### 판정: `REFUTED`

외부 1x1 PNG를 fixture 밖 경로에 만들고 세 버킷에서 실행했다.

공식:

```bash
unity command import_asset --project-path "%HERA_AUDIT_FIXTURE%" -- \
  --source "%EXTERNAL_PNG%" \
  --path Assets/HeraParityAudit/C5/imported.png
```

결과: 세 버킷 모두 성공했고 asset importer가 생성됐다.

Hera:

```bash
hera-agent-unity --project "%HERA_AUDIT_FIXTURE%" \
  manage_assets copy \
  --path "%EXTERNAL_PNG%" \
  --new_path Assets/HeraParityAudit/C5/hera-copy.png
```

결과:

```json
{
  "success": false,
  "code": "INVALID_PATH",
  "message": "path must be under Assets/"
}
```

> **정정 (교차검증).** `manage_assets copy`는 매트릭스가 주장한 대체 수단이 아니다.
> 해당 행의 근거는 "existing exec/filesystem/atomic tool"이다. `copy`는 AssetDatabase
> 내부 복사 명령이라 외부 경로 거절이 설계대로다(재현됨: `INVALID_PATH: path must be
> under Assets/`). 이 시험은 Hera에 ingress 액션이 없다는 사실을 보여줄 뿐 주장된 대체
> 수단을 반증하지는 않는다. 실질 논거는 caller capability(=C11)이며 그것은 유효하다.

공식 import 뒤 Hera `manage_asset_import get`은 importer를 정상 조회했다. 따라서 importer 설정이 빠진 것이 핵심 gap은 아니다. gap은 **외부 파일을 Assets로 들여오는 ingress**다.

C11처럼 caller가 filesystem tool을 갖지 않는 경우에는 plain copy + refresh 경로가 존재하지 않는다. C5의 duplicate 분류는 caller capability를 가정하므로 `REFUTED`다.

---

## C6 - configurable authoring root는 duplicate인가

### 판정: `REFUTED`

세 버킷에서 공식 authoring root를 다음처럼 좁혔다.

```text
Assets/HeraParityAudit/C6
```

공식 `create_folder --dry_run` 결과:

- root 내부 `Inside`: accepted
- sibling `Assets/HeraParityAudit/C6Sibling`: rejected as outside authoring root

> **정정 (교차검증).** 최초 근거는 `call manage_assets --validate-only`가 sibling
> path에 `{"valid":true}`를 반환한다는 것이었다. 이 근거는 무효다. `--validate-only`는
> 스키마 검사이고 containment는 실행 시점에 강제된다:
>
> ```text
> --validate-only  {"action":"mkdir","path":"C:/Windows/Temp/EscapeTest"}  -> {"valid":true}
> 실제 실행        같은 입력  -> INVALID_PATH: path must be under Assets/ (got 'C:/Windows/Temp/EscapeTest')
> ```
>
> `valid:true`는 Assets/ 밖 경로에도 나오므로 안전 경계에 대해 아무것도 증명하지 않는다.
> 또한 `Assets/HeraParityAudit/C6Sibling`은 Assets/ 안이므로 Hera가 허용하는 것이 설계대로다.

정정된 근거로 다시 판정한다. `AssetPathGuard`는 `Assets/` containment를 실행 시점에
강제하며 이는 실측으로 확인된다. 그러나 그 경계는 고정이고 caller나 session 단위로 더
좁힐 수단이 없다. 공식 기능이 제공하는 것은 "쓰기 가능한 영역을 작업 단위로 좁히는
능력"이고 이를 대신하는 기존 Hera 표면은 없다. 따라서 `duplicate` 분류는 성립하지
않는다 — `REFUTED`. 다만 이는 안전 구멍이 아니라 **부재한 구성 능력**이며, 실제로 막힌
workflow 증거는 아직 없다.

---

## C7 - durable handle이 ObjectRef gap을 닫았는가

### 판정: `REFUTED`

handle coverage에 관한 첫 절은 세 버킷에서 확인됐다.

1. 하나의 container asset에 red, green, blue Material 세 개를 넣었다.
2. 각각 `guid:<guid>:<fileId>`를 발급했다.
3. `manage_material get`으로 각 색상을 분리해서 읽었다.
4. AnimationClip handle을 `manage_animation get_clip`에 전달했다.
5. 저장된 Scene의 Prefab instance GlobalObjectId를 `manage_prefab list_overrides`에 전달했다.
6. Material asset을 move한 뒤 기존 path와 GUID를 다시 비교했다.

세 버킷 모두:

- sub-asset handle이 red/green/blue를 정확히 분리했다.
- AnimationClip handle이 frame rate `24`인 clip을 읽었다.
- Prefab instance GlobalObjectId가 instance를 찾았다.
- move 뒤 old path는 `MATERIAL_NOT_FOUND`였다.
- 같은 GUID는 새 path의 Material을 찾았다.
- 같은 GUID가 `manage_asset_import get`, `manage_assets deps reverse`에서도 동작했다.
- move back 뒤 원래 path가 복구됐다.

그러나 claim의 두 번째 절, 즉 "failed resolution이 attempted strategies를 보고하지 않는 것이 remaining gap"이라는 해석은 실제 resolver 구조와 맞지 않았다. `ObjectIdentity.TryResolve`는 여러 fallback strategy를 순서대로 시도하지 않는다. 입력 prefix에 따라 하나의 resolution form을 결정한다.

추가 failure matrix:

| Input | 실제 메시지 |
|---|---|
| missing GUID | `no asset for guid '<guid>'` |
| valid GUID + missing fileId | `no sub-asset with fileId ... in '<path>'` |
| valid GUID + malformed fileId | `invalid fileId 'abc' in 'guid:...:abc'` |
| unresolved GlobalObjectId | scene/object가 없다는 정확한 GlobalObjectId failure |

각 실패는 어떤 형식과 단계에서 실패했는지 이미 말한다. 시도하지 않은 fallback 목록을 `data.tried`로 추가하는 것은 진단 gap을 닫는 일이 아니라 실제 알고리즘과 다른 서사를 만드는 일이다.

따라서 **durable handle coverage는 confirmed지만, C7 전체 claim의 remaining-gap 절은 refuted**다. 사용자가 요구한 단일 verdict는 `REFUTED`로 기록한다. 이 결과만으로 새 response field나 error-code refactor를 queue에 넣지 않는다.

---

## C8 - per-record prefab override를 reload 뒤 식별할 수 없는가

### 판정: `REFUTED`

Prefab instance에 Rigidbody mass override를 만든 뒤 `PropertyModification` 12개를 다음 key로 정렬했다.

```text
GlobalObjectId(target)
+ propertyPath
+ value
+ GlobalObjectId(objectReference)
```

그 다음 temporary Editor script를 추가해 실제 compile/domain reload를 발생시켰다. 세 버킷 모두 domain epoch가 변경됐다.

| Bucket | Before | After reload | Composite key |
|---|---:|---:|---|
| `6000.0.35f1` | `12` | `12` | byte-identical |
| `6000.3.5f2` | `12` | `12` | byte-identical |
| `6000.5.6f1` | `12` | `12` | byte-identical |

이번 fixture에서는 `(target GlobalObjectId, propertyPath)`만으로도 12개가 모두 unique였다. 따라서 "single override를 가리킬 identifier가 reload를 견디지 못한다"는 전제는 `REFUTED`다.

### capability implication

이 결과가 곧바로 per-record apply/revert 구현 승인을 뜻하지는 않는다. 다음이 필요하다.

- 같은 target/propertyPath가 여러 record로 나타나는 collision fixture
- removed component/object-reference override 처리
- reload 뒤 missing target error contract
- per-record mutation의 approval/reversibility 분류
- 세 버킷 regression
- action 또는 flag surface cost와 baseline review

이 패스에서는 구현하지 않았다.

---

## C9 - Project Auditor는 아직 conditional인가

### 판정: `REFUTED`

세 버킷 결과:

| Bucket | Package state | `audit` 결과 |
|---|---|---|
| `6000.0.35f1` | Project Auditor `3.0.1` + Rules `1.0.3` | `completed`, `issueCount: 18995` |
| `6000.3.5f2` | Auditor 없음 | `unavailable`, type not found |
| `6000.5.6f1` | module은 있으나 Rules 없음 | `unavailable`, no analysis modules |

positive fixture의 최종 status:

```json
{
  "status": "completed",
  "issueCount": 18995
}
```

즉 package/rules 조건에 따라 availability가 달라지는 것은 사실이지만, "positive non-empty result를 아직 얻지 못해 conditional로 남겨야 한다"는 전제는 더 이상 사실이 아니다. 구현 가능성은 입증됐으므로 C9는 `REFUTED`다.

### capability implication

Hera surface로 받을지는 별도 admission decision이다.

- package와 rules의 독립 감지
- reflection-only optional boundary
- async scan/task contract
- large CSV/result resource 처리
- package-present, package-absent, rules-absent 세 fixture regression
- payload baseline review

이 패스에서는 구현하지 않았다.

---

## C10 - build-target switching rejection의 exec fallback이 유효한가

### 판정: `REFUTED`

세 버킷에서 공식 `list_build_profiles`는 성공했다. fixture에 Build Profile asset이 없어 결과 배열은 비어 있었지만, 명령 자체와 계약은 존재했다.

같은 active target `StandaloneWindows64`에 대한 공식 `switch_build_target`은 완료됐다. 다른 target으로의 full reimport는 이번 감사에서 실행하지 않았다. 그 작업은 claim을 반증하는 데 필요하지 않고 fixture를 장시간 불안정하게 만들 수 있기 때문이다.

결정적인 반증은 fallback availability다.

Non-interactive Hera CLI:

```bash
hera-agent-unity --project "%HERA_AUDIT_FIXTURE%" exec --file c10-probe.cs
```

세 버킷 모두:

```json
{
  "success": false,
  "code": "APPROVAL_REQUIRED",
  "risk_class": "arbitrary_code"
}
```

Compact MCP에서는 C11과 같이 `exec`가 검색/설명되지 않고 직접 호출도 permission required다.

따라서 `exec` fallback은 다음 caller에게 동일하지 않다.

| Caller | Fallback |
|---|---|
| Interactive CLI | operator confirmation 뒤 가능 |
| Non-interactive CLI | approval token round trip 필요 |
| Compact MCP default | arbitrary-code permission 없이는 접근 불가 |
| Filesystem/code execution이 없는 caller | 대체 경로 없음 |

"exec가 있으므로 gap이 아니다"라는 rejection 근거는 caller model을 숨긴다. C10은 `REFUTED`다. 이것은 target switching을 즉시 구현하라는 결론이 아니다.

---

## C11 - 12 duplicate workflow가 Compact MCP에서 닿는가

### 판정: `CONFIRMED`

설치된 Hera CLI `v0.2.16`으로 실제 stdio MCP session을 열었다.

```text
HERA_MCP_ENABLED=1
HERA_MCP_EXPOSURE=compact
--allow-arbitrary-code 없음
```

`tools/list` 결과는 정확히 세 개였다.

```text
tool_search
tool_describe
tool_call
```

다음 12개 이름을 각각 `tool_search`했다.

```text
create_gameobjects
create_script
eval_file
get_authoring_root
import_asset
read_text_file
rename_asset
save_prefab_contents
set_authoring_root
set_target_framerate
set_timescale
write_text_file
```

12회 모두:

```json
{"success":true,"data":[],"message":"OK"}
```

추가 직접 호출:

```text
tool_describe(name=exec)       -> TOOL_NOT_FOUND
tool_call(name=exec)           -> ARBITRARY_CODE_PERMISSION_REQUIRED
tool_call(name=read_text_file) -> TOOL_NOT_FOUND
```

따라서 C11은 `CONFIRMED`다. 동시에 matrix의 `duplicate`는 기능 자체의 중복이 아니라 **특정 caller가 filesystem과 arbitrary code를 모두 가진다는 가정**에 의존한다. matrix taxonomy는 caller model을 별도 열로 표현해야 기계적으로 검증할 수 있다.

---

## C12 - 네 개의 covered judgment call

### 판정: `REFUTED`

C12는 compound claim이므로 한 항목의 실질적 비동등성만으로도 전체가 반증된다. 이번에는 두 개가 명확하게 달랐다.

### 1. `recompile_status -> status`

공식 결과는 compile operation에 답한다.

```json
{
  "status": "completed",
  "failed": false,
  "errors": []
}
```

Hera `status`는 Editor identity/liveness에 답한다.

```text
state=ready
project=<selected project>
unity=<version>
pid=<pid>
port=<port>
```

C3 compile 중 Hera는 `ready`, `compiling`, `reloading` heartbeat를 기록했고, 공식 `recompile_status`는 operation의 `completed/idle/failed`를 보고했다. 두 명령은 같은 질문이 아니다.

### 2. `capture_game_view -> screenshot --view game`

세 버킷에 `HeraC12Camera`를 만들고 공식 명령을 실행했다.

```bash
unity command capture_game_view --project-path "%HERA_AUDIT_FIXTURE%" -- \
  --camera HeraC12Camera \
  --width 128 \
  --height 64 \
  --max_resolution 32 \
  --save_path Temp/HeraParityAudit/c12-official.png
```

세 버킷 모두 PNG를 만들고 source camera를 `HeraC12Camera`로 보고했다.

같은 인자를 Hera에 전달한 결과:

```json
{"code":"UNKNOWN_ARGUMENT","data":{"path":"/camera"}}
{"code":"UNKNOWN_ARGUMENT","data":{"path":"/max_resolution"}}
```

따라서 단순 `covered`는 정확하지 않다.

### 3. tags/layers

공식 `get_tags_layers`와 Hera `manage_editor get_tags_layers`는 모두 실제 값 목록을 반환했다. 쓰기는 Hera의 `add_tag/remove_tag/add_layer/remove_layer`로 분해돼 있다. 이 부분은 capability가 존재한다.

### 4. version-gate code

source grep 결과 `FEATURE_UNAVAILABLE`, `VERSION_UNSUPPORTED`, `UNSUPPORTED_VERSION`라는 하나의 공통 code는 없었다. 그러나 current surface는 optional package 부재를 `PACKAGE_NOT_INSTALLED`처럼 구체적으로 fail-closed 처리하고, version별 API 차이는 compile bucket과 tool-specific unsupported response로 분리한다. 공통 code의 부재 자체는 독립 capability gap을 입증하지 않는다.

C12의 일부는 맞지만, compound claim의 `covered` equivalence가 실제 출력에서 깨졌으므로 최종 판정은 `REFUTED`다.

---

## 4. 세 버킷 release-gate 재검증

각 fixture에서 저장소 제공 스크립트를 실행했다.

```powershell
powershell -ExecutionPolicy Bypass \
  -File tools/verify-unity-package/run-package-tests.ps1 \
  -ProjectPath "%HERA_AUDIT_FIXTURE%" \
  -Filter HeraAgent.Tests \
  -TimeoutMs 900000 \
  -StabilizationSeconds 2
```

이 스크립트는 다음을 묶어서 검증한다.

1. production Connector compile
2. live catalog payload baseline
3. package `testables` 임시 활성화
4. `HeraAgent.Editor.Tests` 독립 compile
5. EditMode release-gate tests
6. manifest 원본 SHA-256 복원
7. 복원 뒤 production compile

| Bucket | Result |
|---|---|
| `6000.0.35f1` | `26/26 PASS`, exit `0` |
| `6000.3.5f2` | 첫 시도는 Editor PID 전환 5초 창과 겹쳐 discovery fail, 안정화 뒤 동일 재실행 `26/26 PASS`, exit `0` |
| `6000.5.6f1` | `26/26 PASS`, exit `0` |

세 manifest 모두 마지막에 `testables` 없이 복원됐다.

## 5. 반증된 claim의 queue 영향

이번 패스는 보고까지만 수행했다. 아래는 구현 목록이 아니라 queue decision에 필요한 admission-gate 질문이다.

| Claim | 실제로 열린 질문 |
|---|---|
| C1 | matrix를 caller-aware taxonomy로 다시 분류할지, `covered`를 exact/partial로 나눌지 |
| C4 | `package resolve` action이 실제 사용자 failure를 막는지, 기존 package task flow에 흡수할지 |
| C5 | filesystem-less caller를 위한 bounded external asset ingress가 필요한지 |
| C6 | 좁은 authoring root가 실제 agent safety policy로 필요한지 |
| C8 | composite override key를 public per-record apply/revert contract로 승격할지 |
| C9 | optional Project Auditor integration을 surface에 넣을지, release fixture에 rules package를 유지할지 |
| C10 | `list_build_profiles` read surface와 target switching mutation을 분리해 판단할지 |
| C12 | screenshot camera/max-resolution flag와 compile operation status를 각각 독립적으로 admission할지. generic version-gate code는 이번 감사에서 별도 gap으로 열지 않는다. |

각 제안은 `CLAUDE.md`의 feature admission gate를 다시 통과해야 한다.

- failure prevented
- existing surface reuse
- strict input/output and safety contract
- live regression evidence
- surface/payload/dependency cost
- reviewed catalog baseline

## 6. 남은 불확실성과 관측 메모

1. C2의 historical per-bucket immediate count는 재현되지 않았다. 결론은 유지되지만 commit body의 숫자는 수정 대상이다.
2. C10에서 다른 build target으로 실제 전환하지 않았다. full reimport를 실행하지 않아도 default Compact caller의 fallback 부재가 claim을 반증한다.
3. C1의 모든 126 mapping은 live contract까지 전수 확인했고, read-only 29 mapping x 3 buckets = 87 paired calls도 실행했다. one-sided disagreement는 0개였다. universal claim을 반증한 뒤 나머지 approval-gated mutation을 자동 승인하지 않았다.
4. raw evidence는 `%HERA_AUDIT_EVIDENCE%`에 남아 있으나 gitignored다. 이 보고서에 결정적 output을 전사한 이유다.
5. 감사 도중 `main`이 다른 작업자의 커밋으로 전진했다. Connector source는 기준 commit 이후 변경되지 않았고, 본 보고서는 concurrent Go/doc 변경을 되돌리지 않는다.

## 7. 변경 범위

이 보고서 수정 외에 다음을 하지 않았다.

- refuted capability 구현
- Connector version 변경
- CLI release tag 변경
- catalog baseline 변경
- README 변경
- fixture 결과를 production 프로젝트에 적용
- push

## 8. 최종 저장소 검증

저장소 검증은 동시 작업으로 branch가 `ea46af19`에서 `3bddd061`까지 전진하는 동안 새로 실행했다. 기준 commit 이후 Connector source 변경은 없었다.

| 검증 | 결과 |
|---|---|
| tracked Go files `gofmt -l` | output 0개 |
| `go build ./...` | PASS |
| `go vet ./...` | PASS |
| `go test ./...` | PASS |
| `go run ./tools/generate-runtime-contracts --check` | PASS |
| `go run ./tools/sync-agent-guides --check` | PASS |
| `go run ./tools/validate-connector-package` | `connector package integrity PASS` |
| `golangci-lint run ./...` | `0 issues` |
| `golangci-lint fmt --diff` | PASS |
| `npm ci --ignore-scripts` | PASS, vulnerabilities `0` |
| `npm test` | installer target + distribution metadata PASS |
| `npm pack --dry-run` | PASS |
| `git diff --check` | PASS |

검증 도중 다른 동시 작업자가 C1 read-only evidence와 C11 caller-model 설명을 추가했다. 해당 변경은 보존하고, 이 보고서에는 실제 output과 충돌하지 않는 부분만 병합했다.