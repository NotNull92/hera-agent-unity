# ui_doc Complete Removal Design

## Goal

Remove the `ui_doc` authoring pipeline from Hera. UI creation remains on the
generic `manage_ui`, `manage_components`, `manage_gameobject`, and `batch`
surfaces.

## Scope

- Remove the public `ui_doc` command, all five actions, its CLI-local
  `sample`/`catalog` helpers, and the `html-to-uidoc` converter.
- Remove the dedicated uGUI document IR, fixer, and document-application
  tests.
- Remove the version-specific UI Toolkit schema bundles and UI-document
  generation path. `ui_system=uitk` is no longer a supported authoring mode;
  generic uGUI `manage_ui` remains supported.
- Move ScreenSpaceOverlay capture into the existing `screenshot` tool, rather
  than add a new top-level tool. The benchmark and visual-QA path will request
  that neutral capture mode.
- Remove live help, README, command catalog, agent guidance, examples, and
  benchmark runner code that describes `ui_doc` as a supported path.

## Compatibility and Evidence

The change intentionally removes the public command and its authored IR; it
does not provide a compatibility alias. Historical benchmark outputs,
changelog entries, decision-ledger rows, and prior handoffs remain immutable
evidence and may continue to mention the retired command. Current handoffs
will state that the feature was removed and point to the historical evidence.

Existing dirty benchmark changes are preserved. The removal does not modify a
user Unity project or TMP importer settings.

## Validation

- Add or update targeted tests so command/help/catalog discovery no longer
  exposes `ui_doc` or `html-to-uidoc`, and the overlay screenshot path produces
  a PNG.
- Run the Go formatting, lint, and test gates.
- Compile the connector and perform a live Unity manual QA: confirm the
  retired command is absent from discovery, create a uGUI element with generic
  tools, capture it through `screenshot`, and verify zero console errors.
- Record unavailable Unity compatibility buckets as blocked rather than passed.
