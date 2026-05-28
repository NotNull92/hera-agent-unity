<!-- markdownlint-disable-file MD041 MD013 -->

## Summary

<!-- 1-3 sentences describing the change. -->

## Scope

- [ ] CLI (Go) — `cmd/`, `internal/`
- [ ] Connector (C#) — `AgentConnector/`
- [ ] Docs — `docs/`, `README*.md`, `AGENT.md`, `CLAUDE.md`
- [ ] CI / build — `.github/`, `scripts/`, `.golangci.yml`

## Version bump

- [ ] CLI code changed → `git tag vX.Y.Z` prepared (do NOT push the tag in this PR)
- [ ] Connector code changed → `AgentConnector/package.json` version bumped
- [ ] Both sides changed → bump both
- [ ] `CHANGELOG.md` `## [Unreleased]` section updated

## Automated verification (required)

- [ ] `go build ./...` passes
- [ ] `go vet ./...` passes
- [ ] `go test ./...` passes
- [ ] `golangci-lint run ./...` passes
- [ ] `golangci-lint fmt --diff` shows no changes
- [ ] `gofmt -w .` produces no diff

## Manual integration verification (Unity Editor open)

- [ ] `hera-agent-unity status` — state displays correctly
- [ ] `hera-agent-unity doctor --json` — `unity.instances[0].stale == false`
- [ ] `hera-agent-unity list --names` — tool listing intact
- [ ] `hera-agent-unity exec "return Application.dataPath;"` — returns the project path
- [ ] `hera-agent-unity console` — responds normally
- [ ] `hera-agent-unity editor refresh --compile` — waits until compilation finishes
- [ ] `hera-agent-unity editor play --wait && hera-agent-unity editor stop` — play cycle works

## Regression risk review

- [ ] No conflict with the CLAUDE.md "Hard Constraints" list
- [ ] Intended design preserved (`s_LockTimeout`, `humanCategories`, `splitArgs`, `gocritic` exclusion, etc.)
- [ ] No change to the response envelope shape (`success` / `message` / `code` / `data` / `timings`)
- [ ] Heartbeat atomic write pattern (`.tmp` → `File.Replace`) preserved
- [ ] Any new dependency has an explicit justification

## Related issues / context

<!-- Closes #N, Refs #M -->
