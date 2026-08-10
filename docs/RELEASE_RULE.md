# Release Rules
<!-- last-analyzed: 2026-08-10T12:45:00Z -->

## Version Sources

- CLI release version: annotated Git tag `vX.Y.Z`; release builds inject it through `-ldflags "-X main.Version=${VERSION}"`.
- npm wrapper: `npm/package.json` and `npm/package-lock.json` root package version.
- Unity Connector: `AgentConnector/package.json` `version`; intentionally separate from the CLI release.
- Public release documentation: `README.md`, `README.ko.md`, and `CHANGELOG.md`.
- No standalone release/bump script is present.

## Release Trigger

- Pushing a `v*` tag triggers `.github/workflows/release.yml`.
- Connector snapshots are pinned separately with `connector-<version>` tags.

## Test Gate

- CI runs `go build ./...`, `go vet ./...`, `go test ./...`, Connector package integrity, golangci-lint, and formatting drift checks.
- Release CI validates Connector package integrity and builds Linux, macOS, and Windows binaries before creating the GitHub Release.
- Local release preparation additionally runs npm installer tests, npm dry-run packaging, guide-sync, catalog validation, and dependency verification.

## Registry / Distribution

- GitHub Actions creates a GitHub Release and uploads native binaries after a successful `v*` tag build.
- The npm wrapper is published by `.github/workflows/npm-publish.yml` through npm trusted publishing and GitHub OIDC. Stable release publication can trigger it, and maintainers can dispatch it with a matching `release_tag`.
- The Unity Connector is installed from the Git repository and can be pinned to `connector-<version>`.
- OpenUPM mirrors the tagged Connector package; registry `latest` is `0.0.86` as verified on 2026-08-10.
- The publisher-owned Codex catalog lives at `.agents/plugins/marketplace.json`; HOL mirrors are scanner-gated by `.github/workflows/hol-plugin-scanner.yml`.
- The shipped MCP adapter is registered in the official MCP Registry; successful npm trusted publication triggers the matching OIDC-backed Registry publication workflow.

## Release Notes Strategy

- `CHANGELOG.md` follows Keep a Changelog and Semantic Versioning.
- GitHub release notes are generated automatically by `softprops/action-gh-release`.

## CI Workflow Files

- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `.github/workflows/benchmark.yml`
- `.github/workflows/npm-publish.yml`
- `.github/workflows/hol-plugin-scanner.yml`

## Current publication gaps

- No blocking gap remains for GitHub Release, npm, OpenUPM, or official MCP Registry publication. Curated third-party listings remain separately owned distribution channels.
