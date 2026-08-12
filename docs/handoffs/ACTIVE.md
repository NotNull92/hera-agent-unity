# Active Development Handoff

Current workstream: **none.**

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
