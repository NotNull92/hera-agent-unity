# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.6] - 2026-05-27

### Fixed

- **`batch` help example used a non-existent action.** The
  `hera-agent-unity batch --help` text showed
  `{"command":"manage_editor","params":{"action":"refresh"}}` — but
  `manage_editor` only accepts play/stop/pause/set_active_tool/
  add_tag/remove_tag/add_layer/remove_layer, so users who copy-pasted
  the example hit `UNKNOWN_ACTION`. Swapped to the working
  `refresh_unity` / `compile:"request"` form.

- **`cmd/test.go` branched on a message string for the
  Test-Framework-missing case.** Now branches on `resp.Code ==
  "UNKNOWN_COMMAND"` to honor AGENT.md Rule 3 (code is stable; message
  is not). Drops the unused `strings` import. `CommandRouter`
  already emits `UNKNOWN_COMMAND` for this path, so it is a strict
  upgrade with no behaviour change.

### Changed

- **`humanCategories` AGENT.md doc no longer lists `upgrade`.** The
  word was never in `cmd/root.go`'s actual whitelist — invoking
  `hera-agent-unity upgrade` already returns `UNKNOWN_COMMAND` with
  `did_you_mean=["update"]`. Both `AGENT.md` and the embedded
  `cmd/AGENT.md` are aligned with the real surface.

### Removed

- **Dead `SetAssetInstalled` doc comment** in
  `internal/assetconfig/config.go` (function was removed in an
  earlier refactor; its orphan comment was sitting above
  `GetEnabledAssets` and misreading as its second doc line).
- **Dead `keyMap.FullHelp` / `ShortHelp` methods** in
  `internal/tui/assetconfig.go` — they satisfied
  `bubbles/help.KeyMap` but the asset-config TUI never imports
  `bubbles/help`, never constructs a `help.Model`, and never
  renders help in `View()`. Zero callers across the repo.

> UPM connector stays at v0.0.3 for this release — no C# changes.

## [0.0.5] - 2026-05-27

### Fixed

- **Go error wrapping switched from `%v` to `%w`.** Eight `fmt.Errorf`
  calls across `internal/client` and `cmd` now use `%w` so callers can
  unwrap with `errors.Is` / `errors.As`. This enables programmatic
  detection of `context.DeadlineExceeded`, `net.ErrClosed`, and other
  wrapped errors without string matching.

- **C# `ExecCompileCache` now disposes `SHA256` instances.** Both
  `ComputeKey` and `HashStrings` previously abandoned `SHA256.Create()`
  without disposal, creating finalizer pressure during repeated `exec`
  invocations. Each call site now wraps the instance in `using var`.

> UPM connector stays at v0.0.3 for this release. The C# fix above is
> committed to `main` but the package version will be bumped separately.

## [0.0.4] - 2026-05-27

### Added

- **AGENT.md Pitfall §4.13: PowerShell `exec` quoting.** Adds the
  missing rule that bit a Cursor / Claude Code session on Windows —
  PowerShell single quotes don't interpret backslash escapes, double
  quotes interpret `$` / backtick / `;`, and agents that spawn a fresh
  process per command lose `$code = @'...'@` between calls. The section
  documents three patterns that always work (stdin pipe + here-string,
  single-quoted strings without `\"` escapes, `exec --file` from disk)
  and the matching anti-patterns. `cmd/AGENT.md` is re-synced so
  `doctor --agent-rules` emits the new pitfall alongside the existing
  twelve.

> UPM connector unchanged in this release — still v0.0.3. Only the CLI
> binary is rebuilt so the embedded AGENT.md picks up the new pitfall.

## [0.0.3] - 2026-05-27

### Fixed

- **Console `--stacktrace user` filter widened.** Previously only five
  frame patterns were dropped (`UnityEngine.Debug:`, `EditorGUIUtility:`,
  `Unity.Entities.SystemState:`, `(at Library/`, `(at ./Library/`).
  Real-world exception traces leaked the synthetic exec wrapper
  (`__CliDynamic:Execute`), the hera-agent dispatcher itself
  (`HeraAgent.CommandRouter:*`, `HeraAgent.HttpServer:*`), reflection
  machinery (`System.Reflection.MethodBase:Invoke`,
  `System.Runtime.CompilerServices.AsyncTaskMethodBuilder…`), and the
  editor's update pump (`EditorApplication:Internal_CallUpdateFunctions`).
  All seven families now drop in `user` mode; `full` still returns
  everything verbatim.

### Added

- **`list --tool <name>` now includes an `examples` field.** The
  `HeraToolAttribute.Examples` / `ExampleDescriptions` properties
  restored in v0.0.2 were stored on the attribute but never surfaced
  in the schema response. `ToolDiscovery.GetToolSchema()` now zips
  the two arrays index-wise and emits a `[{call, description}, ...]`
  list. The slim `list` (no `--tool`) payload is intentionally
  unchanged — examples are deep-dive material.
- **`exec` schema now advertises `compile_only`, `stacktrace`, and
  `strict`.** These three flags were already wired end-to-end (v0.0.1
  ExecuteCsharp.HandleCommand reads them via `p.GetBool` / `p.Get`),
  but the `Parameters` nested class only declared the v0.0.1 base
  set. Schema-driven consumers (`list --tool exec`) couldn't see
  them. Three `[ToolParameter]` declarations added so the schema
  matches the actual surface.

## [0.0.2] - 2026-05-27

### Fixed

- **UPM package failed to compile** because the merged `HeraToolAttribute`
  was missing `Examples` and `ExampleDescriptions` properties that
  `DescribeType`, `FindMethod`, and `ListAssemblies` reference on their
  `[HeraTool(... Examples = new[] { ... })]` declarations. Adding the two
  properties back to the attribute (carried over from Pro) restores a
  clean Unity import. `ToolDiscovery` does not yet surface the examples
  in tool schemas — that is a future enhancement; v0.0.2 only restores
  the compile path so the CLI side of v0.0.1 becomes actually usable.

## [0.0.1] - 2026-05-27

Initial release of the unified `hera-agent-unity` — successor to
`hera-agent` (free lite) and `hera-agent-pro` (commercial). All
former Pro features ship free under MIT.

### Added

- Single Go CLI + C# UPM connector bridging Unity Editor over localhost HTTP.
- Built-in tools: `editor`, `exec`, `log`, `scene`, `console`, `test`,
  `menu`, `screenshot`, `profiler`, `reserialize`, `describe_type`,
  `find_method`, `list_assemblies`.
- Auto-discovery via heartbeat files under `~/.hera-agent-unity/instances/`.
- Batch execution (`batch`) for atomic multi-step workflows.
- `[HeraTool]` attribute-based custom tool registration with reflection scan.
- Unity pitfalls catalog surfaced through `describe_type`.
- Asset Config (`asset-config`) TUI + UPM editor window, sharing
  `~/.hera-agent-unity/asset-config.json`.
- Self-install (`install`), self-update (`update`), self-uninstall (`uninstall`),
  and self-diagnostic (`doctor`) commands.
- Cross-platform binaries (Linux, macOS, Windows × amd64/arm64).
