# Active Development Handoff

Current state (2026-10-19): **M1 and M2 are implemented; the support floor is now
Unity 6.6 (`6000.6`).** Connector `0.2.7` (unreleased) requires `6000.6+`, M1 =
contract hardening plus MCP removal (CLI `v0.3.0`), M2 = catalog rebaseline (CLI
`v0.3.1`). Fix B and M3 are complete. The user approved U1 → U8 in order; U1/U2 are complete; U3 adds the exec journal. O2 remains deferred. Per-release detail lives in `CHANGELOG.md`;
this file records only current state and what is open.

Step 6 fix A is implemented for **CLI v0.3.2 / Connector 0.2.1**, without a tag
or release: preserve integer literals in params/batch/flags, reject integers
outside signed 64-bit range before conversion, and document process-scoped
`instance_id` values. Persist `global_id` and explicitly re-resolve after restart.
Atomic batch hardening (B) and Reversible classification follow in the next paragraph;
M3 follows below; O2 remains outside this train.

Step 6 fix B is implemented for **CLI v0.3.3 / Connector 0.2.2**, without tags or
releases. Atomic preflight rejects unknown or ineligible items before approval,
locking, Undo groups, or execution. Twelve reviewed non-destructive actions
declare Undo coverage; screenshot no longer claims reversibility. Non-atomic
behavior and Undo group mechanics are unchanged. Implementation verification:
six live criteria passed; EditMode suite 29 passed / 0 failed / 1 skipped;
exact-source compile and Go/static gates passed. Independent review is separate.
The catalog changes only 13 reversible flags, with 34 tools / 133 actions and
2,368-byte compact rules unchanged. Its hash is now `93827d16…`; normalized
catalog size is 268,402 B (−11 B).

Fix A implementation verification on a disposable `6000.6.6f1` project: 48/48 boundary
requests passed (40 `OBJECT_NOT_FOUND`, 8 `INVALID_ARGUMENT`, 0 `INTERNAL_ERROR`);
Connector suite 28 passed / 0 failed / 1 skipped (Timeline package absent).
Normal install and restored manifest contain no test sources in the production
response file and no test assembly. Go/static gates and exact-source compilation
passed. Independent review and its separate fixture pass are tracked separately;
these implementation results do not substitute for that review.
Fix A kept catalog hash `3649a714…`, 34 tools / 133 actions. Compact rules increased
91 B to 2,368 B, within the 2,500 B rules budget; CLI summary became 2,845 B.

M3 is implemented for **Connector 0.2.3; CLI v0.3.3 unchanged**. Only the bounded
request-body byte loop detaches from Unity's context. No timer or queue changes.
Foreground read/write p50 decreased by about 50 ms; background p95 decreased
by about 95 ms, with no claim for its nearly unchanged p50. Idle CPU did not
materially regress in three five-second windows. Both Editor fixture suites
passed 29 / failed 0 / skipped 1; normal installation isolates test sources,
and exact-source/Go/package/catalog gates passed. Catalog hash `93827d16…`,
34 tools / 133 actions, and 2,368-byte rules remain unchanged. Independent
documentary verification passed; no independent runtime remeasurement is claimed. O2 remains deferred.

User direction (2026-10-19): OpenCode is excluded. Codex implements U1–U8 and
reports each committed, pushed, CI-green item to Claude for independent review.
Only dedicated disposable Editors may be launched/stopped. U3/U4 exec probes
require separate approval of exact snippets immediately before execution.
No tags/releases, user-project changes, O2, or Input Manager changes.

## Step 6 queue (2026-10-17)

Plan and evidence: `docs/report/plan-step6-application-2026-10-17.md`. Adopted
candidate A (contract hardening); the transport experiments were rejected on
measurement, and the 150 ms dispatch delay moved to M3.

| Step | State | What it changed |
|---|---|---|
| M1-a | Done | `retryable`/`state`/`retry_after_ms` on both envelopes; ingress answers `RELOADING` (503) instead of queueing; CLI retries only a declared-idempotent request |
| M1-b | Done | Heartbeat advertises `catalogSchemaHash` + three `*_v1` features; a hash match reuses the cached catalog. `main_thread_required`/`reload_window` live in tool detail, not the catalog — a new catalog key would break installed CLIs |
| M1-c | Done | Port-stability regression tests, now wired into `ReleaseGateTests`. No behavior change |
| M1-d | Done | MCP removal: `internal/mcpserver`, `mcp` command + tests, `tools/benchmark-mcp`, `server.json`, `mcp-publish.yml`, `cmd/help/mcp.txt`, MCP annotation mapping, npm `mcpName`, `docs/MCP.md`, all `HERA_MCP_*` |
| M2 | Done | Catalog rebaseline: `cli_summary` replaces `mcp_compact` in the payload report, the 12 former `duplicate` rows name their exact primitive (2 became `covered` in `0.1.3`, `set_timescale` was already covered, 9 stay `exec`), baseline regenerated. **34 tools / 133 actions unchanged** |
| Support floor | Done | Unity 6.6 (`6000.6`): `package.json` floor, `com.unity.ugui` declared, `instance_id` widened to 64-bit (EntityId `FromULong`/`ToULong`), two 6000.6 API updates |
| Pre-6.6 cleanup | Done | Every version-gated `#if`, the reflected int ↔ EntityId bridge, the docs-bucket selection, the pitfalls min-docs filter and the 6000.0/6000.3 docs bundles are gone; the shipped docs corpus is rebuilt from the Unity 6.6 archive, so `Docs:` reports `6000.6` |
| fix A | Done (candidate) | Integer literals survive `--params`, batch file/stdin, and numeric flags; the Connector rejects out-of-Int64 integers with `INVALID_ARGUMENT` and compares declared bounds without a double round-trip; `instance_id` is documented as Editor-process-scoped. Connector `0.2.1`, CLI `v0.3.2`. Independent verification on a separate fixture passed |
| fix B | Done (candidate) | Atomic fail-closed preflight and 12 Undo-covered action declarations; screenshot reversible=false. CLI `v0.3.3`, Connector `0.2.2`; independent verification follows implementation |
| M3 | Done (candidate); independent verification PASS | Connector `0.2.3` isolates bounded body byte reads from Unity context; no watchdog/tick or CLI changes. Measurements and rejected alternatives are in `docs/DECISION_LEDGER.md`. Independent verification was documentary: the raw before/after matrices were recomputed and matched the report, so no independent re-measurement is claimed |
| M4 | Implemented; local gates PASS (candidate) | CLI v0.3.4 / Connector 0.2.4: cases, JUnit/NUnit artifacts, contiguous test shards; suite 29/0/1, shard union/disjointness/repeatability and PlayMode/resume PASS. Catalog +636 B, 34/133 unchanged; independent static/evidence-recalculation review PASS; no independent live rerun |
| M5 | Done via U2 | Scratch marker/reload PASS; no product adoption |
| M6 | Implemented via U3 | Bounded metadata-only exec journal; no query surface |
| M7 | Approved after M6; not started | Transport token policy gate; detailed scope to be finalized |
| M8 | Approved after M7; not started | PDB research tooling, not distributed |
| U1 | Done; independent static/evidence review PASS | Connector 0.2.5 shard selector parity; unsharded/list unchanged |
| U2 | Done; independent evidence review PASS | Marker/reload confirmed; 425.85 ms off / 425.78 ms on, n=5 each; no product adoption |
| U3 | Independent verification PASS; classification follow-up verified | Connector 0.2.7 distinguishes no-compilation from cache misses; four approved probes miss/memory_hit/miss/disk_hit, reload ref paths unchanged |
| U4 | Implemented; local gates PASS (candidate) | CLI v0.3.5 native dialog diagnosis; one approved exec, opaque baseline timeout, explicit candidate state/title, recovery and 20/20 normal probes |
| U5 | Approved after U4 | Review profile assignments, then CLI profile discovery |
| U6 | Approved after U5 | M7 threat-model closure; no implementation |
| U7 | Approved after U6 | M8 PDB research outside the repository |
| U8 | Approved after U7 | Hub secondary install-path fallback |
| O2 | Deferred | Argument hash normalization; not included in M4–M8 |

**Compatibility for this release: `6000.6.6f1` is the single representative and
the only verified Editor.** `6000.0`-`6000.5` are out of scope by decision (their
Editors are no longer installed, and a partially verified range is not a
compatibility claim); those projects stay on Connector `0.1.3`. M2 changed no
connector code, so its evidence is the live `6000.6.6f1` catalog: 34 tools /
133 actions, `cli_summary` 2,754 B, `--fail-on-change` accepting the regenerated
baseline.

## Shipped in this queue

| Wave | Release | Surface |
|---|---|---|
| 1a | `0.0.90`, `0.0.92`–`0.0.94`, CLI `v0.2.3`/`v0.2.4` | `manage_editor get_selection`/`set_selection`, `scene hierarchy`, `profiler stats`, `manage_animation get_clip`/`get_controller` — all absorbed as actions on existing tools |
| 1b | `0.0.95` + CLI `v0.2.5` | Durable object handles (`guid:<32hex>[:<fileId>]`, `GlobalObjectId_V1-…`) everywhere targets resolve, via `Core/ObjectIdentity`; opt-in durable output; `data.tried` strategy reporting |
| 2 | `0.0.96` + CLI `v0.2.6` | `manage_settings` (physics/time/quality/player/audio get+set, `dry_run` previews, approval-gated writes) and `manage_editor get_tags_layers` |
| 3 | `0.0.97` + CLI `v0.2.7` | `bake` (lighting / built-in scene NavMesh / occlusion × start/status/cancel/clear) |
| 4 | `0.0.98` + CLI `v0.2.8` | `build` (Player build over the file bus + Build Settings management) |
| 5 | `0.0.99` + CLI `v0.2.9` | `test list` / `test cancel` / `--category` / `--assembly`, honest `NO_TESTS_MATCHED`, `manage_packages search` (`docs/DISCOVERY_SURFACE_DESIGN.md`) |
| 6 | `0.0.100` + CLI `v0.2.10` | `manage_prefab list_overrides` / `apply` / `revert` / `unpack`, `--child` component edits, `asset_type` on `create`, inactive-source fix (`docs/PREFAB_OVERRIDE_DESIGN.md`) |
| 7 | `0.0.101` + CLI `v0.2.11` | `manage_assets deps` — forward and reverse asset dependencies; closes survey candidate Q9 by measurement (`docs/ASSET_DEPENDENCY_DESIGN.md`) |

| 8 | `0.0.104` | Asset tools accept durable handles on existing-asset `--path` parameters, including sub-asset `guid:<guid>:<fileId>` (`docs/ASSET_HANDLE_DESIGN.md`) |

| 9 | `0.0.105` | Ten actions declare their output schema; eleven message-only actions recorded as needing none (`docs/OUTPUT_SCHEMA_DESIGN.md`) |

| 10 | `0.0.106` | `bake --area navmesh_surfaces` for the AI Navigation package, reflection-only and fail-closed (`docs/NAVMESH_SURFACE_DESIGN.md`) |
| 11 | `0.0.107` | `manage_settings set_player` writes `scripting_backend` and `api_compatibility_level` from a curated value set, reporting `recompile_triggered` and `build_target` (`docs/PLAYER_TOOLCHAIN_DESIGN.md`) |
| — | `0.0.108` | Fix: deferred command work runs on the editor update loop, so `build start` no longer queues a build that never begins in an unfocused Editor |
| 12 | `0.0.109` + CLI `v0.2.13` | Official Unity CLI `1.0.0-beta.3` parity audit: scene/GameObject/animation/Timeline/settings/editor-window coverage, build-option propagation, console/MCP fixes, and whole-repo dead/duplicate-code cleanup (`docs/UNITY_PIPELINE_PARITY_MATRIX.md`) |

## Shipped after the queue

| Release | Surface |
|---|---|
| CLI `v0.2.13` | Compact exposure becomes the MCP product default; hardened Editor request paths; smaller large-result serialization |
| `0.1.0`–`0.1.2` + CLI `v0.2.14` | `game_feel` corpus rebuilt as Unity-derived knowledge, 54 → 67 topics; unreachable bar recipe and multi-element composer removed. Tool and action counts unchanged. The CLI release also restores the Windows common-profile environment for launched Editors |
| CLI `v0.2.15` | `--yes` / `HERA_AGENT_APPROVE` answers an approval preflight in one invocation. It belongs to the operator's shell; an agent still may not approve on the user's behalf |
| CLI `v0.2.16` | A Connector that predates a requested action is named as version skew instead of a generic `UNKNOWN_ARGUMENT`; `APPROVAL_MISMATCH` lists the claims that differ |
| CLI `v0.2.17` | Two races that reported a healthy Editor as broken: a stale domain epoch on the first typed call after a compile, and a lock-file warning during restart |
| `0.1.3` | `manage_assets import` (one external file into `Assets/`) and `screenshot --camera` / `--max_resolution` — audit claims C5 and C12, both absorbed into existing tools |

Alongside the queue: `0.0.91` moved the support floor to Unity 6+ (three
compatibility buckets), and `0.0.90` fixed the `EntityIdCompat` round trip that
had made every emitted `instance_id` unresolvable on Unity 6000.3+.

Catalog now: **34 tools / 133 actions** (`manage_assets import` added in `0.1.3`). Every wave passed the feature
admission gate, regenerated `docs/metrics/catalog-payload-baseline.json` in the
same review, and was live-verified before release.

## Verification state

No verification debt. `0.0.102` is a fix-only release with no catalog
change. Each release ran its design's live matrix plus the
three-bucket gate (`6000.0.35f1`, `6000.3.5f2`, `6000.5.6f1`); evidence is in
`docs/UNITY_EDITOR_VERSION_INVENTORY.md` and the per-wave design documents.
`0.1.3` ran `HeraAgent.Editor.Tests` 26/26 on all three buckets, which covers
the cumulative Connector source including `0.1.0`–`0.1.2`.

**The release gate now has three steps, and skipping any of them has already
shipped a defect:**

1. `tools/verify-unity-package/compile-exact-source.ps1` for all three
   buckets. It fails on warnings as well as errors and needs no Editor launch.
   Skipped in wave 3 → `0.0.97` shipped unsuppressed deprecated-API warnings.
2. **Run `HeraAgent.Editor.Tests` in each bucket** (`testables` on for the run,
   manifest restored afterwards). Never run before wave 5 → seven stale
   expectations shipped red from `0.0.92` through `0.0.98`. Compiling the
   package and reading the console does not exercise them.
3. The wave's own live matrix on a disposable fixture.

`run-package-tests.ps1` restores the manifest bytes it captured at startup, so a
run killed before its `finally` block leaks its `testables` entry and every later
run preserves it silently — which also contaminates the catalog baseline the gate
measures, since test fixtures declare `[HeraTool]` classes that never ship. One
fixture was found in that state; the script now refuses to start instead.

**Do not use `EditorApplication.delayCall` for work a command starts and leaves
behind.** It does not run in an unfocused Editor, which is how Hera is normally
driven, so the work waits indefinitely after the caller was told it started.
Use `EditorUpdate.Once`; `EditorApplication.update` runs either way, as the
heartbeat demonstrates every second.

## Open items

- **Decided — no action: Unity's Input Manager deprecation notice (2026-10-19).**
  Unity 6.6 logs "This project uses Input Manager, which is marked for
  deprecation…" once per project load when the project keeps the legacy input
  handler (`activeInputHandler: 0`). It is the Editor's own notice; Hera does not
  change project input settings and the Connector declares no Input System
  dependency. No supported setting silences it while keeping legacy input, so the
  user chose to accept it: no product, project, or lab change.
  See `docs/DECISION_LEDGER.md`.

- **Pipeline parity claim audit (2026-08-18/19) is complete; its refuted claims
  are admission questions, not a queue.** Twelve claims about what Hera lacks
  were re-run against three live Editors by a second agent and then
  cross-checked: C2, C3, C11 confirmed, the rest refuted in whole or in part
  (`docs/handoffs/parity-claim-audit-2026-08-18.md`). Disposition so far:
  - C5 and the screenshot half of C12 shipped in `0.1.3`.
  - C9 (Project Auditor) was started and stopped the same day. It needs two
    version-split reflection paths, returns nothing without
    `com.unity.project-auditor-rules`, and has no defined answer shape.
    Reopening starts with one decision: whether that rules package lives in all
    three release-gate fixtures.
  - C7's "does not report attempted strategies" finding was withdrawn: the
    resolver dispatches on handle form and each failure already names its stage.
  - Still undecided, each needing the feature admission gate and a named
    failure first: caller-aware reclassification of the matrix (C1), a
    `package resolve` action whose completion the API cannot confirm (C4), a
    narrower authoring root — a missing capability, not a safety hole, with no
    blocked workflow on record (C6), per-record prefab apply/revert (C8, below),
    a read-only build-profile listing (C10, below), and a compile-operation
    status distinct from Editor liveness (C12).
  - The audit's standing lesson: every `duplicate` row assumes the caller has
    its own filesystem tooling or `exec`. A caller restricted to declared tool
    actions has neither, so "the caller can do it another way" must be checked
    against that caller before it closes a gap.

- **Active build-target switching and Unity 6 build profiles are rejected, not
  pending** (2026-08-13, `docs/DECISION_LEDGER.md`). No failure evidence exists,
  a switch costs a full project reimport that no agent loop can usefully drive,
  `exec` covers the rare scripted case, and automated cross-platform builds
  belong to batch-mode `-buildTarget` rather than an in-place switch of a
  running Editor. `build list_targets` already lets an agent report the active
  target and which platform support is installed. The audit (C10) weakened
  one leg of this: `exec` is not a fallback for an agent restricted to declared
  tool actions. The rejection stands on the other three; whether a read-only
  `list_build_profiles` should be judged separately from the switch itself is
  undecided.
- Deferred by locked designs: AI Navigation package
  `NavMeshSurface` baking — **done in wave 10**
  (`docs/NAVMESH_SURFACE_DESIGN.md`), lighting/navmesh
  settings areas and graphics-pipeline/input-axes settings
  (`docs/SETTINGS_SURFACE_DESIGN.md`), asset-tool path parameters accepting
  durable handles — **done in wave 8** (`docs/ASSET_HANDLE_DESIGN.md`).
- **The survey ingestion queue (Q1–Q11) is closed.** Q1–Q8 and Q10 shipped;
  Q11 closed in wave 5 with four of its five candidates dropped
  (`docs/DISCOVERY_SURFACE_DESIGN.md` D6); Q9 closed in wave 7 by measurement
  rather than implementation — Unity Search's query space proved to be a
  subset of `manage_assets find` + `find_gameobjects` + `menu list`, its
  `dep:` and `#property` queries returned nothing, and its index lag is
  intermittent, so only the reverse-dependency question survived and it is
  answered from `AssetDatabase` instead (`docs/ASSET_DEPENDENCY_DESIGN.md`).
  Any further work needs a new source of evidence, not another queue item.
- **Resolved (was an unresolved wave-6 observation).** The Test Runner
  condition seen in long Editor sessions traced to a callback-registration
  leak: `DisposeApi` guarded its unregister on `api != null`, but Unity
  destroys the `TestRunnerApi` ScriptableObject before `RunFinished` reaches
  Hera, and a destroyed object compares equal to null — so every run leaked one
  registration, and each leaked callback kept collecting later runs' results
  and rewriting the earlier run's result file. Only a domain reload cleared the
  accumulation, which is what made the symptom look session-dependent. Fixed in
  `0.0.102`; the leak count now stays at zero across repeated runs on all three
  buckets. Two earlier hypotheses were falsified by measurement first
  (`test cancel` over three cancel cycles, and a compile-reload racing a test
  start over four cycles).
- Per-record prefab apply/revert is still not shipped, but its stated blocker
  is gone. The audit (C8) measured that `(GlobalObjectId, property_path)` is
  unique per override record and byte-identical across a real Editor restart in
  all three buckets, so a reload-safe key exists. What remains is the decision
  to promote that composite key to a public contract
  (`docs/PREFAB_OVERRIDE_DESIGN.md` D2 predates this measurement).
- **Resolved.** The output-casing item was not a style preference: `bake`,
  `manage_editor`, and `manage_settings` returned typed result objects whose
  PascalCase names matched none of the snake_case properties their own schema
  declared, so a schema-driven consumer saw an empty result. Fixed in `0.0.103`
  with a naming strategy on the result classes, which regenerates the catalog
  byte-for-byte, plus a release-gate test over all 60 declared result types.
- Output schemas: **partly closed in wave 9** (`docs/OUTPUT_SCHEMA_DESIGN.md`).
  81 → 91 declared. Eleven message-only actions are recorded as needing none.
  33 remain undeclared, led by `describe_type` and `scene hierarchy`, which
  wave 9 cut for exceeding its payload budget and which the next slice must
  take first. `scene hierarchy` also needs a decision on recursive shapes —
  `SchemaUtility` refuses recursive DTO graphs by design, so it is either open
  `object[]` children or `$ref` support. `exec` stays undeclared on purpose:
  its payload is user-code-shaped and an open schema is the truthful one.
