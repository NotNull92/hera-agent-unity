# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.9] - 2025-05-15

### Fixed
- Windows `uninstall` command PowerShell script parsing error
  - Multi-line script caused `GetFullPath` exception and `CommandNotFoundException`
  - Compressed to single-line expression for `-Command` compatibility
- Windows `uninstall` self-deletion "Access is denied" error
  - Uses deferred deletion (`cmd /c timeout && del`) when direct removal fails
- Added `$legacy` empty-string guard and legacy directory existence check

## [0.0.8] - 2025-05-15

### Changed
- Author section enhanced with professional background and contact links
- Issue templates added for bug reports, feature requests, and questions
- README.ko.md Author section synchronized with English version
- docs/issues/powershell-exec-escaping.md updated to Lite standard
- docs/porting/ removed (pro-specific content)
- README Commands table updated with install/uninstall commands

## [0.0.7] - 2025-05-13

### Added
- Install flow: AI agent discovery prompt and rule anchoring after installation
- Porting guide for synchronizing changes with hera-agent-pro

### Changed
- README banner updated to `hera_lite.png`
- Log prefix unified from `[UnityCliConnector]` to `[Hera]`

## [0.0.6] - 2025-05-12

### Fixed
- Windows install location moved to `%LOCALAPPDATA%\Microsoft\WindowsApps` to eliminate IDE restart requirement

## [0.0.5] - 2025-05-11

### Fixed
- Uninstall: IDE restart guidance consistency and Common Issues updated

## [0.0.4] - 2025-05-10

### Fixed
- Windows PATH double-backslash bug
- IDE recognition missing guidance

### Added
- PROGRESS.json for tracking development milestones
- Change checklist expanded to include docs/ and CLAUDE.md

## [0.0.3] - 2025-05-09

### Changed
- Namespace and attributes renamed from `UnityCliConnector` to `HeraAgent`
- README install and QuickStart simplified
- Demo GIF placeholder added

## [0.0.2] - 2025-05-08

### Added
- Windows uninstall functions (`removeFromPATH`, `removeBinaryAndDir`)
- Install/uninstall commands

### Changed
- Rebrand from `unity-agent-cli` to `hera-agent`
- README elevated to brand manifesto tone
- Release binary names unified to `hera-agent-*`
- Connector displayName changed to `Hera Agent Lite`

## [0.0.1] - 2025-05-07

### Added
- Initial release: Control Unity Editor from terminal
- Core commands: `editor`, `exec`, `console`, `test`, `menu`, `screenshot`, `profiler`, `reserialize`, `list`, `status`
- Auto-start C# connector with `[HeraTool]` attribute-based tool registration
- Cross-platform support (Linux, macOS, Windows)
- HTTP bridge between Go CLI and Unity Editor
