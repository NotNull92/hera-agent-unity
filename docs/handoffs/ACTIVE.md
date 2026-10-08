# Active Development Handoff

Current state (2026-08-19): **no queue is in progress.** The editor-workflow
surface queue (waves 1a–12) and the survey queue are closed, and the pipeline
parity claim audit that followed is complete. Current releases are Connector
`0.1.3` and CLI `v0.2.17`. Per-release detail lives in `CHANGELOG.md`; this file
records only current state and what is open.

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
    its own filesystem tooling or `exec`. A compact-default MCP client has
    neither, so "the caller can do it another way" must be checked against that
    caller before it closes a gap.

- **Active build-target switching and Unity 6 build profiles are rejected, not
  pending** (2026-08-13, `docs/DECISION_LEDGER.md`). No failure evidence exists,
  a switch costs a full project reimport that no agent loop can usefully drive,
  `exec` covers the rare scripted case, and automated cross-platform builds
  belong to batch-mode `-buildTarget` rather than an in-place switch of a
  running Editor. `build list_targets` already lets an agent report the active
  target and which platform support is installed. The audit (C10) weakened
  one leg of this: `exec` is not a fallback for a compact-default MCP client.
  The rejection stands on the other three; whether a read-only
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
