# Dependency Review: JSON Schema Validator

- **Milestone:** M5 Go registry, cache, and validation
- **Review date:** 2026-07-31
- **Module:** `github.com/santhosh-tekuri/jsonschema/v6`
- **Exact version:** `v6.0.2`
- **License:** Apache License 2.0
- **Reason:** M5 requires a Go validator that compiles the Connector's JSON
  Schema Draft 2020-12 input and output contracts once per catalog hash. This
  module implements Draft 2020-12 directly and exposes compilation and
  instance-validation APIs without coupling the registry to the CLI package.
- **Transitive impact:** The production dependency graph adds no new runtime
  module beyond this validator. Its only non-standard runtime module is
  `golang.org/x/text`, which was already pinned by the repository at `v0.38.0`.
  `github.com/dlclark/regexp2@v1.11.0` appears only in the validator's upstream
  test graph and is not linked into Hera.
- **Security scan result:** The OSV API returned zero advisories for
  `github.com/santhosh-tekuri/jsonschema/v6@v6.0.2` and for its upstream-test
  dependency `github.com/dlclark/regexp2@v1.11.0` on 2026-07-31. The result is
  point-in-time evidence, not a guarantee against future disclosures.
- **Rollback version:** None; this is the first accepted version. Roll back to
  the M4 baseline by removing the module pin together with
  `internal/schema`, `internal/toolregistry`, and
  `tools/validate-tool-catalog`, then return M5 to `PENDING`.

## Verification evidence

```text
go list -m -json github.com/santhosh-tekuri/jsonschema/v6
  Version: v6.0.2
  GoVersion: 1.21

go list -deps -f '{{with .Module}}{{.Path}} {{.Version}}{{end}}' ./internal/schema
  github.com/santhosh-tekuri/jsonschema/v6 v6.0.2
  golang.org/x/text v0.38.0

OSV query, ecosystem=Go, module=github.com/santhosh-tekuri/jsonschema/v6,
version=6.0.2
  {"vulns":[]}
```

Primary references:

- Package and version metadata:
  <https://pkg.go.dev/github.com/santhosh-tekuri/jsonschema/v6@v6.0.2>
- License:
  <https://github.com/santhosh-tekuri/jsonschema/blob/v6.0.2/LICENSE>
- OSV query API:
  <https://google.github.io/osv.dev/api/>
