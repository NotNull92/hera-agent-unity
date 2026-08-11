# Active Development Handoff

Current workstream: **retired UI document authoring surface removed, verified, and
swept for residue.**

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

## Residue sweep (Connector 0.0.88)

A full audit of the tracked tree found one live remnant: every `ui_slop` tell
carried a UI Toolkit check predicate that no caller read. `UiSlopStore.CheckFor`
and the `ui_slop` tool both serve `check_ugui` only, so the field was dropped from
the entry shape, the builder struct, and its required-field validation, and
stripped from all 49 taxonomy lines. The shipped bundle fell from 11434 to 9789
bytes. The stale UI Toolkit handoff document was deleted — every path it named was
already gone — and the naturalization-rule example moved from USS vocabulary to
`unity_docs` entries.

Three now-empty tool directories (`tools/build-uitk-schema`, `tools/html-to-uidoc`,
`tools/benchmark-ui-authoring`) were removed from disk; git does not track empty
directories, so they appear in no commit.

What remains by intent: the retired-key strippers in `internal/assetconfig/json.go`
and `HeraAgentAssetConfigWindow.Model.cs`, the `TestRetiredHelpTopicsAreAbsent`
guard, and the historical benchmark, changelog, and ledger records.

Sweep evidence:

- `gofmt`, `golangci-lint run` (0 issues), `golangci-lint fmt --diff`,
  `go test ./...`, and `sync-agent-guides --check` all passed.
- The regenerated bundle decodes to 49 entries across areas A–E with no
  `check_uitk` key and every field `UiSlopStore` reads present.
- All 23 assertions in `HeraAgent/Tests/UiSlopStore` were replayed against the new
  bundle and passed.

## Publication and live confirmation

Both commits were pushed to `main` and the sweep commit carries the
`connector-0.0.88` tag. The `main` CI run passed every step, including generated
runtime contract drift, agent guide drift, and Connector package integrity.

The connected `6000.3.5f2` project resolves the Connector from the git URL, so live
confirmation required re-resolving it there; it moved from `0.0.86` (commit
`a28c98a`) to `0.0.88` at commit `6c7916f`, which matches `main` HEAD. Its unpinned
git-URL dependency style was preserved. On the upgraded Editor:

- `editor refresh --compile` completed and the Editor returned to `ready`;
  `console --type error` matched 0 of 2 console entries.
- The catalog discovered 30 tools, and `ui_doc` is absent — `ui_slop` is the only
  remaining `ui_`-prefixed tool.
- `ui_slop tmp-italic` returned a single `check` field; the previously emitted
  `check_ugui` and `check_uitk` fields are gone.
- `ui_slop` indexed 49 tells across areas A–E.

Unrelated to this work, that project's `Packages/manifest.json` already carried an
uncommitted `com.unity.pipeline` entry (file mtime predates the re-resolve by over
two hours). The resolve materialized it into `packages-lock.json`; it is the
project owner's pending change, not part of this workstream.

No CLI release was cut. The only Go change is the maintainer-only
`tools/build-ui-slop-docs` builder, so no `v*` tag was pushed.
