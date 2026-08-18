# Pipeline Parity Claim Audit — Final Report (2026-08-18)

> **Status: FINAL.** C1–C12 each carry a verdict below. This file replaces the
> continuation handoff of the same name. Nothing was implemented in this pass;
> every refuted claim is left as a decision for the user.

Subject: twelve claims recorded in `docs/CODEX_HANDOFF_PARITY_CLAIM_AUDIT.md`
about capabilities present in the official Unity CLI / `com.unity.pipeline` and
absent in Hera.

## Verdict summary

| ID | Verdict | One line |
|---|---|---|
| C1 | `BLOCKED` (read-only lane `CONFIRMED`) | Counts and name-resolution reproduced for all 153 rows. The 29 read-only pairs were then executed on both surfaces in all three buckets — 87 paired runs, zero disagreements, one imprecise mapping found. The 86 mutating rows remain unexecuted. |
| C2 | `CONFIRMED (partial)` | Settled-index subset claim holds in all three buckets; immediate-lag reproduced on `6000.0` only. The historical per-bucket pattern did **not** reproduce. |
| C3 | `CONFIRMED (partial)` | `SignalTick` is non-public; idle cadence inside the claimed range; an unfocused Editor completed a compile unaided. Bake and minimized states unmeasured. |
| C4 | `REFUTED (in part)` / `BLOCKED (in part)` | A contract-shaped result *is* producible, so the absolute claim is wrong; but the result has no observable postcondition, and the decisive test needs an unresolved manifest. |
| C5 | `BLOCKED` | No workflow transcript exists and none was produced. |
| C6 | `BLOCKED` | Fixed root is source-verified; no real narrower-root workflow was demonstrated. |
| C7 | `CONFIRMED` | Durable handles resolve across the named tools and sub-asset disambiguation reproduced. The "attempted strategies" wording was withdrawn on re-examination: the resolver dispatches on handle form and each failure already names its exact stage. |
| C8 | `REFUTED` | A per-override identity survives a real Editor restart, unique and byte-identical, in all three buckets. |
| C9 | `REFUTED` | A positive Project Auditor fixture was obtained and completed with 18,995 issues. |
| C10 | `CONFIRMED (partial)` | The `exec` fallback costs an approval round trip for a non-interactive CLI caller and is entirely unavailable to a Compact MCP caller. |
| C11 | `CONFIRMED` | Compact MCP default cannot search, describe, or call the arbitrary-code path. |
| C12 | Split: `CONFIRMED` ×2, `BLOCKED` ×1 | No version-gate code exists; screenshot has no camera/cap options; the recompile-state equivalence could not be judged from the official output observed. |

## Environment

```text
CLI            hera-agent-unity v0.2.16
Connector      AgentConnector/package.json = 0.1.2
repo HEAD      c19e302, branch main, clean
official CLI   unity (…/AppData/Local/Unity/bin/unity)
pipeline pkg   com.unity.pipeline 0.5.0-exp.1 in all three fixtures
```

Fixture ports are re-discovered per command; the values below are point-in-time.

| Bucket | Fixture | Unity | Port at use |
|---|---|---|---|
| `6000.0`–`6000.2` | `Test6.0.35f1` | `6000.0.35f1` | 8093 → 8094 |
| `6000.3`–`6000.4` | `test6000.3.5f2` | `6000.3.5f2` | 8096 |
| `6000.5+` | `test6.5` | `6000.5.6f1` | 8097 |

`Inventoria` (user project, port 8090) was **not** targeted, mutated, or restarted.

---

## C1 — parity matrix completeness · `BLOCKED` (read-only lane `CONFIRMED`)

**Claim.** 153 public commands classified `126 covered / 12 duplicate / 7 rejected / 6 excluded / 2 conditional`, no `planned` row.

**What was verified.**

Classification counts, re-derived from the extracted rows rather than read from the matrix prose:

```text
matrix rows (excl. header row): 153
Counter({'covered': 126, 'duplicate': 12, 'rejected': 7, 'excluded': 6, 'conditional': 2})
```

Public-command derivation is consistent: `unity-source-commands.csv` holds 161 source-declared commands, and the eight named internal/test commands leave 153.

Every `covered` row was then resolved against the live catalog (`34 tools / 132 actions`, `catalog_hash sha256:c96ad0f1…`):

```text
covered rows whose named tool/action exists in live catalog: 112
covered rows unresolvable by literal name:                    14
```

All 14 were resolved by hand and **do** correspond to real capability. They fail
automated matching because the matrix's "Hera equivalent" column mixes CLI
surface syntax with catalog names and abbreviates actions:

| Matrix text | Actual catalog identity |
|---|---|
| `test`, `test list`, `test cancel`, `test --resume` | tool `run_tests` (+ CLI `test`), actions `list`, `cancel` |
| `editor play/stop/pause` | `manage_editor play/stop/pause` |
| `status`, `editor refresh --compile` | CLI surfaces, not catalog tools |
| `manage_gameobject name / active / parent` | `set_name` / `set_active` / `set_parent` |
| `manage_editor add/remove tag/layer` | `add_tag` / `remove_tag` / `add_layer` / `remove_layer` |

So **126 / 126 `covered` rows name something that exists.**

**Read-only lane executed.** Every `covered` row whose Hera action is
`read_only` was then run on both surfaces against the same fixture state, in all
three buckets. Results are in
`docs/report/parity-claim-audit-2026-08-18/c1-readonly-6000.{0,3,5}.jsonl`.

```text
6000.0   28 rows answered on both surfaces, 1 failed on both
6000.3   29 rows answered on both surfaces
6000.5   29 rows answered on both surfaces
rows where one surface succeeded and the other failed: 0
```

The single double-failure is `get_component_properties` on `6000.0`, where that
fixture's scene has no `/Main Camera`: Hera returned `TARGET_NOT_FOUND` and the
official command failed too. Agreement, not a gap.

Two read-only pairs were not run for lack of a fixture asset:
`get_animator_controller` and `get_timeline`.

**One imprecise mapping found by execution.** `list_open_scenes` was recorded as
`scene list/info`. `scene list` returns the Build Settings scene list, not the
open ones — only `scene info` answers the official command's question. Existence
matching would never have caught this; the row now names `scene info` first.

**Why still `BLOCKED`.** The 86 rows classified `write`, `destructive`,
`package_change`, or `arbitrary_code` were not executed. They mutate fixture
state or need an approval this pass was instructed not to grant, so the
equivalence claim for the majority of the matrix is still unproven.

**Exact blocker.** 61 `write`, 20 `destructive`, 3 `package_change`, and 2
`arbitrary_code` rows × 3 buckets, each needing a reversible fixture input and
an approval decision for the gated ones.

---

## C2 — Unity Search rejected on measured index lag · `CONFIRMED (partial)`

**Claim.** The Search index lags AssetDatabase intermittently, and Search's query spaces are subsets of existing Hera surfaces.

**Immediate (create-then-query in one call).**

| Bucket | Result |
|---|---|
| `6000.0.35f1` | AssetDatabase reverse = **1**; `ref:<guid>` = 0, `ref:<path>` = 0, `dep:<guid>` = 0, `dep:<path>` = 0, `#m_Name=…` = 0, `t:Material …` = 0 |
| `6000.3.5f2` | `COMMAND_FAILED — Pipeline server returned 400 Bad Request: Internal Server Error. Main thread operation timed out after 5000ms` |
| `6000.5.6f1` | same timeout |

**Settled index**, identical in all three buckets:

| Query | Count |
|---|---:|
| `ref:<guid>` | 0 |
| `ref:Assets/HeraParityAudit/C2/HeraC2Target.mat` | 1 |
| `dep:<guid>` | 0 |
| `dep:<path>` | 0 |
| `#m_Name=HeraC2Target` | 0 |
| `t:Material HeraC2Target` | 1 |

**Verdict.** The *substance* holds: at creation time AssetDatabase answered
correctly while every Search form returned nothing, and once settled only
`ref:<path>` and `t:` produce hits — `dep:` and `#property` return nothing in
any bucket, so those query spaces are empty rather than additive.

**Correction to the historical record.** Commit `c56c1c1` states the immediate
probe "returned the correct reference on `6000.0.35f1` and zero on `6000.3.5f2`
and `6000.5.6f1`". Today `6000.0` returned **zero**, and the other two buckets
did not answer at all. The lag phenomenon reproduces; the per-version pattern in
that commit body does not. Treat the commit's version-specific wording as
unreliable.

**Remaining blocker.** Immediate probe on `6000.3` / `6000.5` — the official
`eval_file` path times out at a fixed 5000 ms main-thread limit in those buckets.

---

## C3 — `set_autotick` dropped for no measurable gain · `CONFIRMED (partial)`

**Accessibility.**

```
$ hera-agent-unity --project <P3> find_method --pattern SignalTick --namespace UnityEditor
{"total":0,"truncated":false,"results":[]}

$ hera-agent-unity --project <P3> find_method --pattern SignalTick --namespace UnityEditor --include_private true
{"total":1,…,"methods":["static void SignalTick()"]}
```

`EditorApplication.SignalTick` is reachable only as a private member → the
"non-public, would need a reflection binding" half is **confirmed** on `6000.3`.

**Idle cadence, unfocused**, 20 s sample of heartbeat writes:

| Fixture | Unity | n | min | median | max |
|---|---|---:|---:|---:|---:|
| `test6000.3.5f2` | `6000.3.5f2` | 20 | 998.5 ms | **1000.5 ms** | 1002.5 ms |
| `test6.5` | `6000.5.6f1` | 18 | 1080.6 ms | **1093.6 ms** | 1102.8 ms |
| `Test6.0.35f1` | `6000.0.35f1` | 0 | — | — | — (no write in window; instance was `reloading`) |

Both measured buckets fall inside the claimed 1001–1109 ms band against a 1.0 s target.

**Under load** — compile triggered on an unfocused Editor:

```
compile exit: 0   elapsed 6.7s
{"refresh_triggered":true,"compile_requested":true,"force":false}
heartbeat gaps during compile: n=4 min=123.4 median=1222.5 max=4130.0 ms
state transitions: [('ready',0.0), ('compiling',0.3), ('reloading',2.1), ('ready',6.3)]
```

The 4.1 s gap sits inside `reloading` — the domain is being rebuilt, so the
`[InitializeOnLoad]` heartbeat cannot write. It is not throttling, and the
compile finished unaided while unfocused.

**Remaining blockers.** Bake-in-progress cadence, an actually minimized window,
and the `6000.0` bucket were not measured.

---

## C4 — `package resolve` cannot be contracted · `REFUTED (in part)` / `BLOCKED (in part)`

**Official output, all three buckets:**

```json
{"success":true,"operation":"resolve","status":"completed","applied":true,
 "dryRun":false,"requiresRecompile":true,"manifest":{…}}
```

So a contract-shaped answer **is** producible. The claim as written — the action
"could never report whether it worked" — is too absolute and is refuted on that
narrow point.

**Postcondition test** (`6000.3`, `packages-lock.json` around the call):

```
before  ab212bee56f04e67123b88be0aaacb2f   2026-08-18 16:51:09.545664000
after   ab212bee56f04e67123b88be0aaacb2f   2026-08-18 16:51:09.545664000
```

`applied: true` was returned while nothing observably changed. That is precisely
the objection Hera recorded: the field asserts an outcome the API cannot confirm.

**Blocker.** The fixture manifest was already fully resolved, so a no-op is the
expected result and this run cannot separate "resolved successfully" from
"asserted unconditionally". A decisive test needs a deliberately unresolved
manifest, plus `c4-recompile-status-6000.3.json`, which is still missing.

---

## C5 — `import_asset` is a duplicate · `BLOCKED`

Only `c5-external.png` (68 bytes) exists. No transcript of the workflow attempted
from a caller lacking filesystem tools, and no check of whether importer settings
are reachable through `manage_asset_import` after a plain copy. Not run in this
pass. **Blocker:** no executed workflow evidence in any bucket.

---

## C6 — configurable authoring root is a duplicate · `BLOCKED`

`Core/AssetPathGuard.cs` hard-codes the root (`"Assets"` / `"Assets/"`
containment at the normalization check), which confirms only the mechanism, not
the claim. Fixture residue (`HeraParityAudit/C6`, `C6Root`, `C6OutsideHera`)
shows containment work occurred, but no command transcript was preserved and none
was reproduced here. **Blocker:** no workflow requiring a narrower root was
demonstrated or ruled out.

---

## C7 — durable handles closed the ObjectRef gap · `CONFIRMED`

Run on `6000.3.5f2`, against `Assets/HeraParityAudit/C7/`.

**Resolution across tools:**

```
manage_animation get_clip --path guid:8a3e4e41…
  {"path":"Assets/HeraParityAudit/C7/C7.anim","guid":"8a3e4e41…","frame_rate":24.0,…}

manage_asset_import get --path guid:8a3e4e41…
  {"path":"Assets/HeraParityAudit/C7/C7.anim","importer_type":"AssetImporter",…}

manage_assets deps --direction forward --path guid:f1c24289…
  {"path":"Assets/HeraParityAudit/C7/MultiMat.asset","direction":"forward","total":1,…}

manage_material get --path guid:f1c24289…
  {"path":"Assets/HeraParityAudit/C7/MultiMat.asset","shader":"Universal Render Pipeline/Lit",…}
```

**Sub-asset disambiguation** on a three-material container:

| Handle | `_BaseColor` |
|---|---|
| `guid:f1c24289…` | (1, 0, 0) |
| `guid:f1c24289…:2534272535424156079` | (0, 0, 1) |
| `guid:f1c24289…:5622393183496161512` | (0, 1, 0) |
| plain path | (1, 0, 0) |

The path form reaches only the main asset; the `:fileId` form reaches each
sub-asset. This independently reproduces commit `8d17373`.

**The stated remaining gap does not hold** — withdrawn after re-examination. The
resolver does not try strategies in sequence; it dispatches on the handle's form
and reports the stage that failed, and every form names it precisely:

```
guid:000…0                    no asset for guid '000…0'.
guid:f1c2…:999999             no sub-asset with fileId 999999 in
                              'Assets/HeraParityAudit/C7/MultiMat.asset' (guid 'f1c2…').
guid:f1c2…:abc                invalid fileId 'abc' in 'guid:f1c2…:abc'.
GlobalObjectId_V1-1-deadbeef… could not parse GlobalObjectId '…'.
```

All four return `ASSET_NOT_FOUND`. A malformed handle is arguably an argument
error rather than a missing asset, but no failure evidence supports changing a
stable code.

**Partial coverage.** `manage_prefab` was not exercised (this fixture's `C7` has
no prefab; `Test6.0.35f1` does), and only the `6000.3` bucket was run.

---

## C8 — no per-override identity survives a reload · `REFUTED`

Twelve `PropertyModification` records captured before and after a **real Editor
restart** in each bucket:

```
6000.0   before=12  after_real=12  key_equal=True  unique(target,property_path)=12
6000.3   before=12  after_real=12  key_equal=True  unique(target,property_path)=12
6000.5   before=12  after_real=12  key_equal=True  unique(target,property_path)=12
```

Record shape:

```json
{"target":"GlobalObjectId_V1-1-896663be37de140439f1fd54a54f0448-508581834036379631-0",
 "property_path":"m_Name","value":"HeraC8PrefabInstance","object_reference":null}
```

`(target GlobalObjectId, property_path)` alone is unique across all twelve records
and byte-identical across the restart, in every bucket. The claim that no
identifier for a single override survives a reload is **refuted**.

**Not a capability gap yet.** Nothing here proves collision behaviour on larger
override sets, or that single-record apply/revert is semantically safe. Per
`CLAUDE.md` this would still need: the failure prevented, why `apply`/`revert`
cannot absorb it, contract and safety impact, regression evidence, surface cost,
and a baseline review. Not implemented in this pass, by instruction.

---

## C9 — no positive Project Auditor fixture is obtainable · `REFUTED`

`Test6.0.35f1` manifest now contains:

```text
"com.unity.project-auditor": "3.0.1"
"com.unity.project-auditor-rules": "1.0.3"
```

Terminal status and artifact:

```json
{"status":"completed","scanId":"d5cf6ca0","csvPath":"<fixture>/Temp/pipeline-audit/d5cf6ca0.csv","issueCount":18995}
```

```
4722602 bytes  d5cf6ca0.csv
header: Category,Severity,Areas,Description,RelativePath,Line,DescriptorId,Recommendation
row 1:  AssetIssue,Moderate,BuildSize,Asset 'DebugUIPersistentCanvas.prefab' is in a Resources folder,…,PAA3000,…
```

A reachable, non-empty positive configuration exists, obtained simply by
installing the rules package. The claim is **refuted**.

**Per-bucket status.** `6000.3` and `6000.5` fixtures contain no
`com.unity.project-auditor*` entry → those buckets are `BLOCKED`, not covered.

---

## C10 — `switch_build_target` rejection, incl. the `exec` fallback · `CONFIRMED (partial)`

The fallback that the rejection leans on is no longer free. Non-interactive CLI
caller:

```
$ hera-agent-unity --project <P3> exec 'return 1;'
{"success":false,"message":"operation requires approval","code":"APPROVAL_REQUIRED",
 "data":{"summary":{"tool":"exec","side_effect":"unity_editor_and_project",
 "reversible":false,…}, "token":"…", "operation_id":"op_0060c70e…"}}
```

Risk class in the token payload is `arbitrary_code`; the token is single-use. No
approval was granted and none was auto-approved, per the audit's safety rule.

Combined with C11, the fallback availability differs sharply by caller class:

| Caller | `exec` fallback |
|---|---|
| CLI, interactive TTY | prompt, then runs |
| CLI, non-interactive | `APPROVAL_REQUIRED` — second call with `--approve`, or an operator-set `--yes` |
| MCP, Compact default | not searchable, not describable, not callable — **no approval path at all** |

**Remaining blocker.** The batch-mode `-buildTarget` alternative named in the
rejection was not exercised, so the rejection's fourth argument is untested.

---

## C11 — Compact MCP closes the arbitrary-code escape route · `CONFIRMED`

Real stdio MCP client (`mcp-probe/main.go`) against installed CLI `v0.2.16`, with
`HERA_MCP_ENABLED=1`, `HERA_MCP_EXPOSURE=compact`, no `--allow-arbitrary-code`.

`tools/list` exposed exactly:

```text
tool_call   tool_describe   tool_search
```

Twelve `tool_search` calls, issued in this order — `create_gameobjects`,
`create_script`, `eval_file`, `get_authoring_root`, `import_asset`,
`read_text_file`, `rename_asset`, `save_prefab_contents`, `set_authoring_root`,
`set_target_framerate`, `set_timescale`, `write_text_file` — every one returned:

```json
{"data":[],"message":"OK","success":true}
```

Decisive follow-ups:

```text
tool_describe(name=exec)          TOOL_NOT_FOUND: tool "exec" was not found
tool_call(name=exec)              ARBITRARY_CODE_PERMISSION_REQUIRED
tool_call(name=read_text_file)    TOOL_NOT_FOUND
```

**Verdict.** Confirmed for the Compact MCP caller: the `duplicate` justification's
premise — that a blocked workflow can fall back to arbitrary code — does not hold
there, and the approval mechanism is never even reached.

**Evidentiary weakness to note.** The transcript records responses only, not
request bodies, so the twelve empty results are attributable to the twelve
workflows by the probe's source order, not by the log itself.

**Partial.** A plain shell caller deliberately denied filesystem tooling was not
constructed; C10's approval evidence covers only the arbitrary-code half of that
question.

---

## C12 — four "covered" judgment calls

**(a) `recompile_status` → `status` · `BLOCKED`.** Official output observed:

```json
{"status":"completed","failed":false,"errors":[]}
```

That is a three-field shape, not the five-state enum the claim describes, so the
"5 states vs Hera's" comparison could not be judged from it. For its part Hera
surfaced `ready → compiling → reloading → ready` during the C3 compile — it
reports `reloading`, which the official result does not. Neither is a subset of
the other; a decisive comparison needs the official state machine read from
package source.

**(b) `capture_game_view` → `screenshot --view game` · `CONFIRMED` (gap is real
but minor).** Hera's parameter set is `output_path`, `overwrite`, `isolated`,
`target`, `path`, `instance_id`, `width`. There is **no** camera-selection
parameter and **no** max-resolution cap.

**(c) `set_tags_layers` → `manage_editor` · `CONFIRMED`.** Live catalog shows
`add_tag`, `remove_tag`, `add_layer`, `remove_layer`, `get_tags_layers`. The
matrix's abbreviation is imprecise but the capability is present.

**(d) no version-gate response code · `CONFIRMED`, and not a defect.** There is
no `FEATURE_UNAVAILABLE`-style code, but re-examination found the concern
already handled two other ways, each consistent:

- an optional package that is absent returns `PACKAGE_NOT_INSTALLED` (`Bake`
  for AI Navigation, `ManageTimeline` for `com.unity.timeline`);
- a field this Unity version does not expose is reported in `skipped` with the
  reason `"not exposed by this Unity version"` (`ManageSettings`);
- version-specific APIs are gated at compile time with
  `#if UNITY_6000_x_OR_NEWER`, so the path does not exist to refuse.

There is no inconsistency left to unify.

---

## Repository integrity

Separate from the claim audit. Re-run at the end of this pass:

| Command | exit |
|---|---|
| `go test ./...` | `0` |
| `go run ./tools/generate-runtime-contracts --check` | `0` |
| `go run ./tools/sync-agent-guides --check` | `0` |
| `go run ./tools/validate-connector-package` | `0` (`connector package integrity PASS`) |

No production code, package version, catalog baseline, generated contract,
README, or CHANGELOG was modified. Tracked changes in this pass are this report
and the separate review document.

## Fixture residue — user decision required

Left in place deliberately so the blocked checks can resume:

- three fixture Editors still running (`Test6.0.35f1`, `test6000.3.5f2`, `test6.5`);
- `com.unity.pipeline@0.5.0-exp.1` installed in each;
- `Assets/HeraParityAudit/` in each;
- `com.unity.project-auditor` 3.0.1 + rules 1.0.3 in `Test6.0.35f1`;
- `Temp/pipeline-audit/*.csv` (4.7 MB) in `Test6.0.35f1`;
- no pre-audit backup set exists.

`Inventoria` is clean of all of the above.

Raw evidence remains in `docs/report/parity-claim-audit-2026-08-18/`, which is
**gitignored** — it is not committed, not shareable, and unrecoverable if deleted.
The decisive outputs are transcribed above for that reason.

## Open decisions — nothing was implemented

1. **C8 and C9 are refuted.** Neither refutation was turned into work. C8 would
   need the admission-gate evidence listed in its section; C9 needs a decision on
   whether a rules-enabled fixture becomes part of the release gate.
2. **C4's absolute wording is wrong** even though its conclusion survives. Whether
   to reword the recorded decision is the user's call — `docs/UNITY_PIPELINE_PARITY_MATRIX.md`
   and `docs/DISCOVERY_SURFACE_DESIGN.md` were left untouched.
3. **Commit `c56c1c1`'s per-bucket C2 wording does not reproduce.** The conclusion
   stands on other grounds; the version-specific sentence should not be cited again.
4. **C1 remains the large unfinished block**: 378 paired invocations. Decide
   between full execution and a declared risk-based subset.
5. **C5, C6, and the blocked halves of C2, C3, C4, C10, C12(a)** need targeted
   re-runs while the fixtures are alive.
6. **Matrix precision.** The "Hera equivalent" column mixes CLI syntax with catalog
   names and abbreviates actions, which is why 14 of 126 rows could not be matched
   automatically. Normalizing it would make future audits mechanical.
7. **Fixture cleanup or retention.**
