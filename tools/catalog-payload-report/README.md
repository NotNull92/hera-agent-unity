# Catalog Payload Report

This maintainer tool measures the internal normalized Unity tool catalog: total
normalized bytes, per-profile contract bytes, tool, action, and legacy
full-vs-selected-action describe sizes, and the `cli_summary` a caller pays
before any Unity work — the `list --names` array plus the machine-independent
compact agent rules. Token counts remain clearly labelled rough JSON estimates
rather than provider billing claims.

`cli_summary` deliberately excludes local asset-config opt-ins (enabled plugins,
Game Feel, De-slop, a stricter verification loop), so a checked-in baseline is
reproducible on any machine. Its default warning budget is 3,000 bytes
(`--warn-cli-summary-bytes`); `agent_rules_bytes` is the compact rules half that
`README.md` publishes as the reviewed 2,277-byte default.

## Generate a report

Export the live catalog from a disposable blank Unity project that contains the
candidate Connector and no project-specific `[HeraTool]` classes:

```powershell
go run . --project $env:HERA_UNITY_PROJECT list --catalog `
  --schema_version hera.tool-catalog/1 > catalog.json

go run ./tools/catalog-payload-report `
  --catalog catalog.json `
  --output docs/metrics/catalog-payload-baseline.json
```

## Compare with the reviewed baseline

```powershell
go run ./tools/catalog-payload-report `
  --catalog catalog.json `
  --compare docs/metrics/catalog-payload-baseline.json `
  --fail-on-change
```

`--fail-on-change` marks review required when the canonical catalog hash changed
or any measured surface grew. `--fail-on-growth` ignores a same-size contract
change and marks review required only for positive tool, action, description,
CLI summary, or normalized profile deltas. A directly built binary exits with code
`3`; `go run` returns its own non-zero status and prints `exit status 3`.

A failure does not mean growth is forbidden. It means the change must include:

- the user-visible failure or missing workflow being solved;
- why an existing tool action or flag could not carry it;
- strict input/output and safety contracts;
- regression and live Unity evidence; and
- an intentionally regenerated baseline reviewed in the same change.

`tools/verify-unity-package/run-package-tests.ps1` runs this comparison before
the package EditMode suite. Use a disposable blank project so project custom
tools do not contaminate the built-in Connector baseline.
