# Active Development Handoff

Current workstream: **editor-workflow surface queue.** Waves 1a, 1b, 2, 3, 4,
5, 6, 7, and 8 are complete, and the survey queue is closed. Per-release detail lives in `CHANGELOG.md`; this file
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

Alongside the queue: `0.0.91` moved the support floor to Unity 6+ (three
compatibility buckets), and `0.0.90` fixed the `EntityIdCompat` round trip that
had made every emitted `instance_id` unresolvable on Unity 6000.3+.

Catalog now: **33 tools / 111 actions**. Every wave passed the feature
admission gate, regenerated `docs/metrics/catalog-payload-baseline.json` in the
same review, and was live-verified before release.

## Verification state

No verification debt. `0.0.102` is a fix-only release with no catalog
change. Each release ran its design's live matrix plus the
three-bucket gate (`6000.0.35f1`, `6000.3.5f2`, `6000.5.6f1`); evidence is in
`docs/UNITY_EDITOR_VERSION_INVENTORY.md` and the per-wave design documents.

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

## Open items

- Deferred by locked designs: active build-target switching and Unity 6 build
  profiles (`docs/BUILD_SURFACE_DESIGN.md`), AI Navigation package
  `NavMeshSurface` baking (`docs/BAKE_SURFACE_DESIGN.md`), lighting/navmesh
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
- Per-record prefab apply/revert is deferred: the record objects support it,
  but addressing "the Rigidbody override on /Player/Arm" needs an identifier
  that survives a reload, and none exists yet. `list_overrides` already returns
  the paths that would key it (`docs/PREFAB_OVERRIDE_DESIGN.md` D2).
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
