# Exact-source Unity package compile

Compile the repository's current Connector and TestRunner sources against an
existing Unity project's Bee response files without changing or launching the
Editor. Existing C# source inputs in those response files are discarded before
the current repository sources are appended, so stale package-cache or
source-injected paths cannot leak into the exact-source result:

```powershell
pwsh tools/verify-unity-package/compile-exact-source.ps1 `
  -ProjectPath $env:UNITY_PROJECT
```

Run this once for each supported compatibility bucket before a Connector
release. The project must already have compiled the Hera package so the two Bee
response files and package references exist.
## Run isolated package tests

The package test assembly is intentionally excluded from normal UPM consumers.
Temporarily add the package to a disposable project's `testables`, compile,
run the selected EditMode tests, restore `manifest.json` byte-for-byte, and
compile the restored project:

```powershell
pwsh tools/verify-unity-package/run-package-tests.ps1 `
  -ProjectPath $env:UNITY_PROJECT `
  -Filter HeraAgent.Tests
```

Before EditMode tests, the script exports the live built-in tool catalog and compares it with `docs/metrics/catalog-payload-baseline.json`. Any unreviewed contract change exits non-zero and prints the comparison report. Run this gate with a disposable blank project so project custom tools are not included.

The script always attempts restoration in `finally`. A missing test count,
failed test, restore hash mismatch, or post-restore compile failure is an error.
Never use a production project as the verification fixture.
## Run the compatibility matrix

Run exact-source compilation across the five supported Unity buckets with one
path-parameterized command. Missing projects are reported as `BLOCKED`, never
as `PASS`:

```powershell
pwsh tools/verify-unity-package/run-compatibility-matrix.ps1 `
  -Project6000_0_2 $env:HERA_UNITY_6000_0_2 `
  -Project6000_3_4 $env:HERA_UNITY_6000_3_4 `
  -Project6000_5Plus $env:HERA_UNITY_6000_5_PLUS
```

Add `-RuntimeBuckets "6000.5+"` only for fixtures explicitly marked disposable.
Runtime mode delegates to `run-package-tests.ps1`, so the package `testables`
entry is temporary and `manifest.json` must be restored byte-for-byte. The
script emits a `hera.compatibility-matrix/1` JSON summary and exits non-zero for
`FAIL`; it also exits non-zero for `BLOCKED` unless `-AllowBlocked` is explicit.
