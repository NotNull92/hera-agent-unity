# Active Development Handoff

Current workstream: **editor-workflow surface queue.** Waves 1a, 1b, 2, 3, 4,
5, and 6 are complete. Per-release detail lives in `CHANGELOG.md`; this file
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
| 6 | `0.0.100` | `manage_prefab list_overrides` / `apply` / `revert` / `unpack`, `--child` component edits, `asset_type` on `create`, inactive-source fix (`docs/PREFAB_OVERRIDE_DESIGN.md`) |

Alongside the queue: `0.0.91` moved the support floor to Unity 6+ (three
compatibility buckets), and `0.0.90` fixed the `EntityIdCompat` round trip that
had made every emitted `instance_id` unresolvable on Unity 6000.3+.

Catalog now: **33 tools / 110 actions**. Every wave passed the feature
admission gate, regenerated `docs/metrics/catalog-payload-baseline.json` in the
same review, and was live-verified before release.

## Verification state

No verification debt. Each release ran its design's live matrix plus the
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
  durable handles (`docs/TARGET_RESOLUTION_DESIGN.md`).
- Survey candidates not yet designed: Q9 Unity Search exposure. Its overlap
  with `find_gameobjects` and `manage_assets find` has to be measured before a
  design gate opens. Q10 closed in wave 6; Q11 closed in wave 5; four of its five candidates were
  dropped with reasons recorded in `docs/DISCOVERY_SURFACE_DESIGN.md` §D6
  (menu listing already shipped; `set_autotick` prevents no measured failure;
  `Client.Resolve()` cannot report its own outcome; external-file import is
  already covered by the agent's own filesystem tools plus `editor refresh`).
- **Unresolved observation, wave 6:** after a long Editor session mixing
  interrupted CLI polls, repeated `test cancel` calls, and `editor refresh
  --compile` cycles, the Test Runner stopped completing runs entirely — every
  start returned a run guid, callbacks never fired, and `CancelTestRun`
  reported the guid as unknown. An Editor restart always cleared it, and the
  condition never reproduced from a clean session: a fresh session ran the
  gate suite green repeatedly, and an explicit cancel of a live 30-second
  PlayMode run followed by a full EditMode run was also green. The trigger was
  not isolated, so it is recorded rather than claimed fixed. Every three-bucket
  gate result was taken from a clean session.
- Per-record prefab apply/revert is deferred: the record objects support it,
  but addressing "the Rigidbody override on /Player/Arm" needs an identifier
  that survives a reload, and none exists yet. `list_overrides` already returns
  the paths that would key it (`docs/PREFAB_OVERRIDE_DESIGN.md` D2).
- Output-casing inconsistency, pre-existing and not addressed: tool payloads
  built from anonymous objects serialize snake_case, but the handful returned
  as typed result classes (`bake` all actions, `manage_editor get_tags_layers`)
  serialize PascalCase. Changing them breaks consumers of `0.0.94`–`0.0.99`,
  so it needs its own decision rather than a drive-by fix.
