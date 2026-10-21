# Contributing

Hera is a public product that its author uses and improves daily while making a game.

## What starts work

- A failure encountered in the author's game.
- A security report.
- A reproduced external bug.

Feature requests and speculative improvements wait.

## New features need a reproduced failure

Describe the reproduced failure, explain why the existing surface cannot solve it, and include a regression test. Keep proposals within [What Hera is not](README.md#what-hera-is-not).

## Response targets

The target for a first response is two weeks for issues and pull requests, and one week for [private security reports](SECURITY.md). These are response targets, not resolution deadlines. During game crunch, only security and data-loss fixes proceed; other reports receive an acknowledgment.

## Pull requests

External contributions may be merged or reimplemented with contributor attribution retained.

## Compatibility

Hera remains on 0.x. Commands, flags, output fields, and error codes remain deprecated for at least one minor release before removal, with a warning and a CHANGELOG entry. Removal happens only in a minor release (`0.x` to `0.(x+1)`), never a patch release.

## Before you open a pull request

See the [development guide](docs/DEVELOPMENT.md) for setup and workflows, and [Verification](CLAUDE.md#verification) for repository checks. Run:

```sh
gofmt -w .
golangci-lint run ./...
golangci-lint fmt --diff
go test ./...
go run ./tools/sync-agent-guides --check
```
