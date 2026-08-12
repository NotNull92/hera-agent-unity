# Active Development Handoff

Current workstream: **editor-workflow surface queue, wave 1a.**

Wave 1a is COMPLETE. All four items shipped as actions on existing tools:
selection round trip (Connector `0.0.90`), scene-tree single-call dump
(Connector `0.0.92` + CLI `v0.2.3`), lightweight performance stats (Connector
`0.0.93` + CLI `v0.2.4`), and animation read-back (Connector `0.0.94`). Each
item passed the feature admission gate, regenerated the catalog payload
baseline in the same review, and was live-verified before release.

Shipped in Connector `0.0.94` (tag `connector-0.0.94`):

- `manage_animation get_clip` / `get_controller` — read back authored clips
  (metadata + curve bindings, optional keyframes) and controllers
  (parameters, layers, states, transitions with conditions). Live-verified on
  `6000.3.5f2` with a full authoring round trip on disposable assets.

Open before wave 1b: run the three-bucket gate once covering
`0.0.92`–`0.0.94` (all three are version-agnostic C#; `0.0.93` reads render
statistics via reflection that degrades to `render_available=false`).

Shipped in Connector `0.0.93` + CLI `v0.2.4` (tags `connector-0.0.93`,
`v0.2.4`):

- `profiler stats` — one-call render/memory/frame snapshot without a capture;
  reflection-backed render statistics with an explicit `render_available`
  flag, times converted to ms. Live-verified on `6000.3.5f2` in edit and play
  mode. The three-bucket gate has not run for `0.0.92`/`0.0.93`; both changes
  are version-agnostic C# plus embedded help, and the reflection path
  degrades to `render_available=false` rather than failing.

Shipped in Connector `0.0.92` + CLI `v0.2.3` (tags `connector-0.0.92`,
`v0.2.3`, commit `8a9fe8a`):

- `scene hierarchy` — bounded GameObject tree dump of the loaded scenes or one
  subtree (`--root`), with `--depth`, `--max_nodes` budget + `truncated` flag,
  and optional `--components`. Live-verified on `6000.3.5f2`; the three-bucket
  gate has not yet run for `0.0.92` (changes are version-agnostic C# plus
  embedded help text).

Shipped in Connector `0.0.90` (tag `connector-0.0.90`, commit `5a2f923`):

- `manage_editor get_selection` / `set_selection` — structured selection read
  with active object, mixed-target write (instance ids, hierarchy paths,
  Assets/ paths), empty list clears. Live-verified on `6000.3.5f2`.
- Fixed the `EntityIdCompat` id round trip on Unity 6000.3+:
  `EntityId.GetHashCode()` is not the id value there, so every emitted
  instance_id was unresolvable. Ids now go through Unity's EntityId → int
  conversion operator bound once per domain via reflection.
Shipped in Connector `0.0.91` (tag `connector-0.0.91`, commit `db59ad0`):

- Hera now targets Unity 6+ only: package `"unity"` floor `6000.0`, the
  `2022.3`/`2023.2` docs bundles and legacy `unity_docs_6.0` alias removed
  (~3.5 MB smaller), pre-Unity 6 version branches collapsed, and the release
  gate reduced to three buckets (`6000.0`–`6000.2`, `6000.3`–`6000.4`,
  `6000.5+`). Live-verified on `6000.3.5f2`.
- The three-bucket compatibility gate PASSED for `0.0.90`+`0.0.91` in one pass
  (2026-08-12): `6000.0.35f1` and `6000.5.6f1` in Library-reset disposable
  fixtures, `6000.3.5f2` live. Full evidence, including the per-bucket
  instance-id round trip and the 6000.5 conversion-operator probe, is in
  `docs/UNITY_EDITOR_VERSION_INVENTORY.md`.

The retired UI-document authoring surface is fully removed. `ui_doc`,
`html-to-uidoc`, UI Toolkit authoring/version adapters, their benchmark runner,
captured A/B artifacts, implementation plans, and checkpoint handoffs are no
longer part of the repository.

The supported UI path is generic uGUI tooling (`manage_ui`,
`manage_components`, `manage_gameobject`, and `batch`) with
`screenshot --overlay` for visual verification. The bundled `ui_slop` taxonomy
is uGUI-only and contains 48 tells.

Current source versions:

- CLI: next release source after `v0.2.1`
- Connector: `0.0.89`

Historical release facts remain in `CHANGELOG.md` and `docs/DECISION_LEDGER.md`.
There is no retired benchmark or removal protocol to resume.
