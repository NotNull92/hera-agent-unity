# Active Development Handoff

Current workstream: **editor-workflow surface queue, wave 1a.**

Planned wave 1a items, absorbed as actions on existing tools, one at a time:
selection round trip (shipped in Connector `0.0.90`), scene-tree single-call
dump, lightweight performance stats, animation read-back actions. Each item
passes the feature admission gate, regenerates the catalog payload baseline in
the same review, and is live-verified before release.

Shipped in Connector `0.0.90` (tag `connector-0.0.90`, commit `5a2f923`):

- `manage_editor get_selection` / `set_selection` — structured selection read
  with active object, mixed-target write (instance ids, hierarchy paths,
  Assets/ paths), empty list clears. Live-verified on `6000.3.5f2`.
- Fixed the `EntityIdCompat` id round trip on Unity 6000.3+:
  `EntityId.GetHashCode()` is not the id value there, so every emitted
  instance_id was unresolvable. Ids now go through Unity's EntityId → int
  conversion operator bound once per domain via reflection.
- The five-bucket UPM compatibility gate has NOT run for `0.0.90`; only
  `6000.3.5f2` is live-verified. The 6000.3+ code path is version-gated and
  pre-6000.3 branches are unchanged, but bucket verification remains open.

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
