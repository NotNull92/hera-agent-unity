# hera-agent-unity — AI-Readable Documentation

> **For AI Agents**: This documentation is designed so that any AI reading it can immediately understand the project structure, modify code, and extend functionality without guessing.

## What This Project Is

`hera-agent-unity` is a **single-binary CLI tool** (written in Go) that controls **Unity Editor** (via a C# UPM connector) over **plain HTTP** on localhost.

Tests, TUI, batch engine, and asset-config layer sit on top — but the engine that talks to Unity stays lean.

**No WebSockets. No JSON-RPC. No Python. No persistent server process.**

```
┌─────────────────┐      HTTP POST      ┌─────────────────────┐
│   Go Binary     │  ◄──────────────►   │   Unity Editor      │
│   (thin core)   │   localhost:8090    │   (auto-starts)     │
│                 │                     │   C# Connector      │
└─────────────────┘                     └─────────────────────┘
```

**Repository**: `https://github.com/NotNull92/hera-agent-unity`

---

## Quick Mental Model

1. **Unity Editor opens** → C# `HttpServer` starts on localhost (port 8090+)
2. **C# `Heartbeat`** writes a JSON file to `~/.hera-agent-unity/instances/<hash>.json` every 1.0s
3. **Go CLI runs** → scans instance files → finds Unity → sends HTTP POST `/command`
4. **C# `CommandRouter`** receives the command → dispatches to the right tool handler
5. **JSON response** flows back to the terminal

---

## Documentation Map

| File | Purpose | Read This When |
|:---|:---|:---|
| [`INDEX.md`](INDEX.md) | This file — project overview | Starting here |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | System architecture, data flow, state machine | You need the big picture |
| [`GO_CLI.md`](GO_CLI.md) | Go CLI code structure, entry point, command dispatch | Modifying Go code |
| [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) | C# Unity side — HttpServer, CommandRouter, ToolDiscovery | Modifying C# code |
| [`COMMANDS.md`](COMMANDS.md) | All CLI commands, flags, parameters | Adding/changing commands |
| [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) | How to write new C# tools that the CLI can call | Extending functionality |
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | Build, test, lint, release workflows | Setting up dev env or releasing |
| [`benchmarks/token-reduction/README.md`](benchmarks/token-reduction/README.md) | Versioned token-reduction benchmarks for `list --compact` and `find_gameobjects` projections | Evaluating agent token cost by Unity version |

---

## Directory Structure

```
hera-agent-unity/
├── cmd/                          # Go CLI commands
│   ├── root.go                   # Entry point, flag parsing, response printing
│   ├── dispatch.go               # Standalone / Unity-backed command routing
│   ├── editor.go                 # editor play/stop/pause/refresh
│   ├── status.go                 # status, waitForAlive, waitForReady
│   ├── test.go                   # test runner
│   ├── update.go                 # self-update from GitHub
│   ├── version_check.go          # periodic update notice
│   ├── asset_config.go           # asset-config subcommand
│   ├── batch.go                  # batch command execution
│   ├── ui_doc.go                 # ui_doc dispatch + CLI-side sample/catalog
│   ├── manage_packages.go        # async package job polling
│   ├── unity_docs.go             # unity_docs passthrough
│   ├── doctor.go                 # self-diagnostic
│   ├── install.go / uninstall.go # installer hooks
│   └── *_test.go                 # Unit tests
├── internal/
│   ├── client/
│   │   ├── client.go             # HTTP client, instance discovery
│   │   ├── client_test.go
│   │   ├── process_unix.go       # Unix PID check
│   │   └── process_windows.go    # Windows PID check
│   ├── assetconfig/
│   │   └── config.go             # asset-config.json read/write
│   ├── tui/
│   │   └── assetconfig.go        # bubbletea TUI
│   ├── poll/
│   │   └── poll.go               # async job result polling
│   ├── paths/
│   │   └── paths.go              # hera state/config path helpers
│   └── unitystate/
│       └── state.go              # Unity state constants
├── AgentConnector/               # C# Unity package (UPM)
│   └── Editor/
│       ├── HttpServer.cs         # localhost HTTP server
│       ├── CommandRouter.cs      # command dispatch + locking + batch
│       ├── ToolDiscovery.cs      # reflection-based tool scanning + schemas
│       ├── Heartbeat.cs          # instance state file writer
│       ├── HeraAgentAssetConfigWindow.cs  # Hera > Settings window
│       ├── Attributes/           # [HeraTool], [ToolParameter]
│       ├── Core/                 # Response types, param coercion, type resolution, docs store
│       ├── Data/                 # Bundled Unity ScriptReference data
│       ├── Tools/                # Built-in tool implementations
│       └── TestRunner/           # Unity Test Framework integration
├── docs/                         # This documentation
├── tools/
│   └── build-unity-docs/         # Unity docs bundle generator
├── install.sh                    # macOS/Linux installer
├── install.ps1                   # Windows installer
├── go.mod                        # Go module
└── README.md                     # User-facing README
```

---

## Key Design Decisions

| Decision | Rationale |
|:---|:---|
| **HTTP, not WebSocket** | Simpler. No connection state to manage. CLI is stateless. |
| **Instance files, not process scanning** | Unity PID detection is OS-specific and fragile. JSON files are reliable. |
| **Reflection-based tool discovery** | No registration boilerplate. Drop a C# class with `[HeraTool]` — it works. |
| **SemaphoreSlim serialization** | Prevents race conditions when multiple CLI agents access same Unity. |
| **Domain-reload survival** | `[InitializeOnLoad]` + `AssemblyReloadEvents` ensures HTTP server restarts after script compilation. |
| **Go-side passthrough default** | Most commands need no Go code. CLI sends params to Unity, prints response. Only polling commands (editor, test) need Go logic. |

---

## Entry Points for Common Tasks

### Add a new CLI command
1. Create C# tool in `AgentConnector/Editor/Tools/` with `[HeraTool(Name = "command_name")]`
2. That's it. Default passthrough in `cmd/root.go` handles dispatch automatically.
3. If you need polling/waiting logic (like `editor play --wait`), add Go code in `cmd/<command>.go`.

### Modify an existing command
1. Edit the C# handler in `AgentConnector/Editor/Tools/`
2. If CLI flags changed, update `cmd/root.go` help text
3. Update `README.md` and `README.ko.md`

### Change how Go connects to Unity
1. `internal/client/client.go` — HTTP sending, instance discovery
2. `cmd/status.go` — `waitForAlive()`, `waitForReady()` polling logic

### Change Unity-side HTTP behavior
1. `AgentConnector/Editor/HttpServer.cs` — port selection, request handling
2. `AgentConnector/Editor/CommandRouter.cs` — dispatch locking

---

## Versioning Rules

**CLI (Go)** and **Connector (C#)** have **independent versions**.

| Component | Version Location | When to Bump |
|:---|:---|:---|
| CLI | Git tag `vX.Y.Z` | Go code changes |
| Connector | `AgentConnector/package.json` | C# code changes |

If both change, bump both. If only one changes, bump only that one.

---

## Verification Command

Run this before every commit:

```bash
go clean -testcache
gofmt -w .
~/go/bin/golangci-lint run ./...
go test ./...
```

---

## Next Steps

- Read [`ARCHITECTURE.md`](ARCHITECTURE.md) for the full system picture
- Read [`GO_CLI.md`](GO_CLI.md) if you need to modify Go code
- Read [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) if you need to modify C# code
- Read [`CUSTOM_TOOLS.md`](CUSTOM_TOOLS.md) to add new functionality
