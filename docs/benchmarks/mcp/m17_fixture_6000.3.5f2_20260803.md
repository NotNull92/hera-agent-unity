# M17 disposable-fixture integration — Unity 6000.3.5f2

Date: 2026-08-03

Repository source: `006b8e7282afa971c05c64477d7983986b7f9117`

Connector package: `0.0.75`

Fixture: `M17Fixture6000.3.5f2`, marked with
`hera.mcp-benchmark-fixture/1` and `disposable=true`

## Result

The complete fourteen-case section 28.3 matrix is **PASS** in one marked
disposable fixture. The fixture was explicitly selected by project path for
every Hera request; the simultaneously open Inventoria and Unity 6000.5
projects were not targeted.

This completes M17 PASS A evidence collection. The required independent PASS B
review audited the retained evidence and final repository state, reported no
findings, and recommended `APPROVE`. Milestone M17 is therefore **PASS**. Typed
CLI and the existing CLI remain the production default. MCP remains
experimental, unreleased, stdio-only, and default-off.

## Durable artifacts

- [Bounded Unity transcript](m17_fixture_6000.3.5f2_20260803_unity.jsonl)
- [Cleanup and restoration proof](m17_fixture_6000.3.5f2_20260803_restore.txt)
- This report

The artifacts contain no absolute machine paths and retain no approval token.

## Section 28.3 matrix

| Scenario | Result | Retained observation |
|---|---|---|
| query | PASS | `scene info` returned clean `SampleScene`, root count 2. |
| GameObject creation | PASS | Created `__HeraM17FixtureRoot` at `(17,0,0)` and reread it by hierarchy path. |
| component mutation | PASS | Added `BoxCollider`, set `m_IsTrigger=true`, and reread the property as `true`. |
| UI creation | PASS | The configured UI Toolkit backend created a `UIDocument` root plus UXML, USS, and `PanelSettings` assets. |
| asset mutation | PASS | Created a fixture folder, copied `DefaultVolumeProfile` to `VolumeCopy.asset`, and found the copy through `manage_assets`. |
| Play Mode | PASS | Entered Play Mode, observed `playing`, stopped, then observed `ready`; console errors remained zero. |
| tests | PASS | A one-case fixture NUnit assembly ran through the real `test` CLI command and returned total=1, passed=1, failed=0. |
| package job | PASS | An approved asynchronous add installed `com.unity.ai.navigation` 2.0.14; a second approved job removed it and reread found zero remaining. |
| domain reload | PASS | Package, test-assembly, and custom-tool changes survived reload and project-targeted discovery followed the fixture across port changes. |
| invalid argument repair | PASS | A two-element position array was rejected by strict validation before Unity; the documented `"17,0,0"` form succeeded. |
| missing target | PASS | A guaranteed-missing hierarchy path returned stable code `TARGET_NOT_FOUND`. |
| destructive approve/deny | PASS | An unapproved destructive batch returned `APPROVAL_REQUIRED` with completed=0 and preserved its target; a bound single-use approval then deleted exactly that target. |
| batch | PASS | A repaired read batch completed both scene and GameObject queries; the destructive batch failed closed before executing an item. |
| custom-tool reload | PASS | Temporary strict tool `m17_fixture_probe` changed the catalog 31→32, returned `Value=17`, and disappeared after cleanup, restoring 32→31. |

## Test command correction

The first observation was taken after Unity had written its passing
`TestResults.xml` but before the waiting CLI process consumed the run-scoped
result. A diagnostic rerun then mistakenly used `--timeout 300`, whose unit is
milliseconds, not seconds. That 300 ms run timed out while Unity later wrote a
valid result file.

No Connector result-bridge defect was present. With the production source
restored and the intended `--timeout 300000`, the real command completed in
12.4 seconds and returned:

```json
{"total":1,"passed":1,"failed":0,"skipped":0,"failures":[],"passes":["HeraM17Fixture.Tests.HeraM17FixtureSmokeTests.FixtureSmokePasses"]}
```

A speculative delayed-start change was rejected during investigation because
it prevented Unity Test Framework execution from starting. It and its test
scaffolding were fully removed; repository production code is unchanged.

## Repository gates

The final source state passed:

- `gofmt -l .` empty
- `go vet ./...`
- `go build ./...`
- `go test -count=1 ./...`
- `golangci-lint run ./...` with `0 issues.`
- `go run ./tools/sync-agent-guides --check`
- `git diff --check`

## Cleanup

All temporary GameObjects, UI assets, copied assets, fixture scripts, test
assembly, and custom tool were removed. `SampleScene` was reloaded from disk.
The final Editor state was `ready`, `dirty=false`, root count 2, tool count 31,
fixture test assembly count 0, and console error count 0.

The package manifest, package lock, and scene matched their pre-run SHA-256
hashes byte-for-byte. The Git Connector dependency was restored before the
temporary local source and `testables` setting were removed.

## Independent PASS B

The read-only reviewer verified all ten M17 evidence categories, every
section 28.3 scenario, cleanup hashes, timeout semantics, absence of secrets and
machine paths, guide consistency, rollback viability, and the absence of
automatic MCP promotion. It returned `APPROVE` with no findings. Its residual
risk is that the live Unity scenarios were audited from retained artifacts
rather than re-executed during the read-only review.
