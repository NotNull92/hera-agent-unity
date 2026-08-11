# Active Development Handoff

Current workstream: **retired UI document authoring surface removed and verified.**

Current design and implementation plan:

- [`2026-08-12-ui-doc-removal-design.md`](../superpowers/specs/2026-08-12-ui-doc-removal-design.md)
- [`2026-08-12-ui-doc-removal.md`](../superpowers/plans/2026-08-12-ui-doc-removal.md)

## Codex continuation

From the repository root, start an interactive session with:

```powershell
codex "Read docs/handoffs/ACTIVE.md and its current design and plan. Follow AGENTS.md and CLAUDE.md. Verify git status and the Unity connection. Complete the retired UI document authoring-surface removal, retain generic uGUI tools and screenshot --overlay capture, preserve historical benchmark results, and do not touch TMP Importer or user project content. Do not commit, push, tag, or release without explicit instruction."
```

The completed T04 layout wave supports this removal decision. Its frozen
results and earlier handoffs remain historical evidence; do not rerun the
retired protocol or alter its captured result artifacts.

## Completion evidence

- `go test ./...` passed.
- Exact-source Connector compilation passed for `2022.3`, `2023.2`, `6000.0–6000.2`, `6000.3–6000.4`, and `6000.5+`.
- The disposable `6000.3.5f2` project discovered 30 tools and 75 actions with no `ui_doc`; package EditMode tests passed and the pre-test live console-error check was clean. The package-test manifest restoration later logged three Unity `PackageManager.UI.Internal` `ScriptableSingleton already exists` messages; no Connector frame appears in their stack.
- `screenshot --overlay` rendered a fresh 640×360 ScreenSpaceOverlay uGUI capture; two independent visual reviews passed.
- `docs/metrics/catalog-payload-baseline.json` was regenerated from that live catalog.

No commit, push, tag, or release was made.
