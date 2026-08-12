# Active Development Handoff

Current workstream: **editor-workflow surface queue.** Waves 1a, 1b, 2, 3, and
4 are complete and released. Per-release detail lives in `CHANGELOG.md`; this
file records only current state and what is open.

## Shipped in this queue

| Wave | Release | Surface |
|---|---|---|
| 1a | `0.0.90`, `0.0.92`–`0.0.94`, CLI `v0.2.3`/`v0.2.4` | `manage_editor get_selection`/`set_selection`, `scene hierarchy`, `profiler stats`, `manage_animation get_clip`/`get_controller` — all absorbed as actions on existing tools |
| 1b | `0.0.95` + CLI `v0.2.5` | Durable object handles (`guid:<32hex>[:<fileId>]`, `GlobalObjectId_V1-…`) everywhere targets resolve, via `Core/ObjectIdentity`; opt-in durable output; `data.tried` strategy reporting |
| 2 | `0.0.96` + CLI `v0.2.6` | `manage_settings` (physics/time/quality/player/audio get+set, `dry_run` previews, approval-gated writes) and `manage_editor get_tags_layers` |
| 3 | `0.0.97` + CLI `v0.2.7` | `bake` (lighting / built-in scene NavMesh / occlusion × start/status/cancel/clear) |
| 4 | `0.0.98` + CLI `v0.2.8` | `build` (Player build over the file bus + Build Settings management) |

Alongside the queue: `0.0.91` moved the support floor to Unity 6+ (three
compatibility buckets), and `0.0.90` fixed the `EntityIdCompat` round trip that
had made every emitted `instance_id` unresolvable on Unity 6000.3+.

Catalog now: **33 tools / 103 actions**. Every wave passed the feature
admission gate, regenerated `docs/metrics/catalog-payload-baseline.json` in the
same review, and was live-verified before release.

## Verification state

No verification debt. Each release ran its design's live matrix plus the
three-bucket gate (`6000.0.35f1`, `6000.3.5f2`, `6000.5.6f1`); evidence is in
`docs/UNITY_EDITOR_VERSION_INVENTORY.md` and the per-wave design documents.

**Process correction from wave 4:** `tools/verify-unity-package/compile-exact-source.ps1`
fails on warnings as well as errors, and it had not been run during wave 3 —
`0.0.97` shipped with unsuppressed deprecated-API warnings, fixed in `0.0.98`.
Run that script for all three buckets before every Connector release; it needs
no Editor launch and takes seconds.

## Open items

- Deferred by locked designs: active build-target switching and Unity 6 build
  profiles (`docs/BUILD_SURFACE_DESIGN.md`), AI Navigation package
  `NavMeshSurface` baking (`docs/BAKE_SURFACE_DESIGN.md`), lighting/navmesh
  settings areas and graphics-pipeline/input-axes settings
  (`docs/SETTINGS_SURFACE_DESIGN.md`), asset-tool path parameters accepting
  durable handles (`docs/TARGET_RESOLUTION_DESIGN.md`).
- Survey candidates not yet designed: Q9 Unity Search exposure, Q10 prefab
  overrides/unpack, Q11 assorted small gaps (`package_search`/`resolve`,
  `list_tests` + category filter, menu listing, `set_autotick`,
  `import_asset`).
