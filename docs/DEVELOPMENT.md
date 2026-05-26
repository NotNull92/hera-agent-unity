# Development Guide

This document covers the development environment setup, build process, testing, and release workflow for `hera-agent`.

---

## Prerequisites

| Tool | Version | Purpose |
|:---|:---|:---|
| Go | 1.24+ | CLI binary build |
| Unity Editor | 2021.3 LTS+ | C# connector development |
| golangci-lint | v2 | Static analysis and linting |
| Git | 2.40+ | Version management |

---

## Go Environment Setup

### 1. Verify Go Installation

```bash
go version  # Should be go1.24.x or higher
go env GOPATH
```

### 2. Install golangci-lint

```bash
go install github.com/golangci/golangci-lint/v2/cmd/golangci-lint@latest
# Verify: which golangci-lint
```

> **Important**: The project's `.golangci.yml` uses v2 syntax. You must install v2 or higher.

### 3. Install Dependencies

```bash
cd ~/Desktop/Cowork/hera-agent
go mod tidy
go mod download
```

---

## Unity Environment Setup

### 1. Install the Connector

**Via Package Manager (Git URL):**
```
https://github.com/NotNull92/hera-agent.git?path=AgentConnector
```

**Or via manifest.json:**
```json
"com.notnull92.hera-agent": "https://github.com/NotNull92/hera-agent.git?path=AgentConnector"
```

### 2. Assembly Definition

The connector uses `HeraAgent.asmdef`. If your custom tools need additional assembly references, add them to your own asmdef, not the connector's.

### 3. Build Settings

Edit → Project Settings → Editor:
- `Enter Play Mode Options`: Optional, but recommended for stability
- `Script Compilation During Play`: `Recompile And Continue Playing` recommended

---

## Local Build

### Full Verification Pipeline

Run this before every commit:

```bash
cd ~/Desktop/Cowork/hera-agent

go clean -testcache
gofmt -w .
~/go/bin/golangci-lint run ./...
go test ./...
```

### Cross-Compilation

```bash
# Windows (from macOS/Linux)
GOOS=windows GOARCH=amd64 go build -ldflags="-s -w" -o dist/hera-agent.exe

# macOS Intel
GOOS=darwin GOARCH=amd64 go build -ldflags="-s -w" -o dist/hera-agent-darwin-amd64

# macOS Apple Silicon
GOOS=darwin GOARCH=arm64 go build -ldflags="-s -w" -o dist/hera-agent-darwin-arm64

# Linux
GOOS=linux GOARCH=amd64 go build -ldflags="-s -w" -o dist/hera-agent-linux-amd64
```

> `-ldflags="-s -w"` strips debug symbols and DWARF tables, reducing binary size by ~30%.

---

## Testing

### Unit Tests

```bash
go test ./...
```

### Integration Tests

Requires Unity Editor to be open with the Connector installed:

```bash
go test -tags integration ./...
```

These are excluded from default test runs. CI skips them because Unity is not available.

### Testing Individual Commands

```bash
# Status (no Unity required for error case)
hera-agent status

# Editor control (requires Unity)
hera-agent editor play --wait
hera-agent editor stop

# C# execution (requires Unity)
echo 'Debug.Log("test");' | hera-agent exec

# List tools (requires Unity)
hera-agent list
```

---

## Linting

The project uses `golangci-lint` v2 with configuration in `.golangci.yml`:

```bash
~/go/bin/golangci-lint run ./...
```

Common issues and fixes:

| Issue | Fix |
|:---|:---|
| Unused variables | Remove or use `_ = variable` |
| Unchecked errors | Add `if err != nil { return err }` |
| Formatting | Run `gofmt -w .` |
| Shadowed variables | Rename inner-scope variable |

---

## Install Scripts

### install.ps1 (Windows)

1. Queries GitHub API for `latest` release
2. Downloads `.exe` to `$env:LOCALAPPDATA\hera-agent\`
3. Adds directory to `User` PATH environment variable (persistent)
4. **Current shell must be restarted** to pick up PATH changes

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://raw.githubusercontent.com/NotNull92/hera-agent/main/install.ps1 | iex"
```

### install.sh (macOS/Linux)

1. Queries GitHub API for `latest` release
2. Auto-detects OS/architecture
3. Installs to `~/.local/bin/`
4. Adds `~/.local/bin/` to PATH if needed

```bash
curl -fsSL https://raw.githubusercontent.com/NotNull92/hera-agent/main/install.sh | sh
```

---

## Release Workflow

### Versioning Rules

- **CLI (Go)**: Git tag `vX.Y.Z` → CI builds and publishes release
- **Connector (C#)**: `AgentConnector/package.json` version → manual update

They are **independent**. Bump only the component that changed.

### Manual Release Steps

1. **Run verification**
   ```bash
   go clean -testcache
   gofmt -w .
   ~/go/bin/golangci-lint run ./...
   go test ./...
   ```

2. **Bump version**
   - If Go changed: `git tag -a v0.0.2 -m "Release v0.0.2"`
   - If C# changed: edit `AgentConnector/package.json` version field
   - If both: do both

3. **Commit and push**
   ```bash
   git add .
   git commit -m "release: v0.0.2"
   git push origin main
   git push origin v0.0.2  # If tagging
   ```

4. **Wait for CI**
   ```bash
   gh run watch --exit-status
   ```

5. **Clean cache**
   ```bash
   go clean -cache -testcache
   ```

6. **Update installed CLI**
   ```bash
   hera-agent update
   ```

### CI/CD

GitHub Actions workflows:

| Workflow | File | Triggers |
|:---|:---|:---|
| CI | `.github/workflows/ci.yml` | Push, PR |
| Release | `.github/workflows/release.yml` | Tag push (`v*`) |

---

## Debugging

### Go CLI Debugging

```bash
# Verbose output
hera-agent status --timeout 60000
```

### Unity C# Debugging

Enable debug logging in Unity:

```csharp
// In Unity Console (during play mode or via startup)
HeraAgent.DebugLogging.Enabled = true;
```

This logs every request, response, and error to the Unity Console.

### Common Issues

| Symptom | Cause | Fix |
|:---|:---|:---|
| `no Unity instances found` | Unity not running or Connector not installed | Open Unity, verify Connector package |
| `cannot connect to Unity at port X` | Wrong port or Unity crashed | Check `hera-agent status`, verify port |
| `compilation finished with errors` | Script compilation failed | Check Unity Console, fix compile errors |
| `connection closed before response` | Unity closed connection early | Retry command; may be Unity-side timing issue |
| Tool not found in `list` | Class missing `[HeraTool]` or wrong name | Verify attribute and class name |
| PATH not found after install | IDE/terminal still using stale env block | Fully close and reopen the IDE or terminal app (not just the terminal tab) |

---

## Related Documentation

- [`INDEX.md`](INDEX.md) — Project overview
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
- [`GO_CLI.md`](GO_CLI.md) — Go CLI internals
- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
