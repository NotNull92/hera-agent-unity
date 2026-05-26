# Troubleshooting

First step for any "it doesn't work" issue:

```
hera-agent-unity-unity doctor
```

This reports the running binary path, what `hera-agent-unity-unity` resolves to on PATH,
duplicate installs, shell-specific gotchas, and reachable Unity instances. No
Unity connection required.

If `hera-agent-unity-unity doctor` itself fails because the binary cannot be found, fall
through to **"`hera-agent-unity-unity` not found"** below.

---

## `hera-agent-unity-unity` not found

### Linux / macOS

The installer writes to `~/.local/bin`. Verify and refresh:

```sh
ls -l ~/.local/bin/hera-agent-unity
command -v hera-agent-unity
```

If `command -v` is empty, your shell rc file did not pick up the PATH change.
Restart the shell, or:

```sh
export PATH="$HOME/.local/bin:$PATH"
```

### Windows

The installer writes to `%LOCALAPPDATA%\Microsoft\WindowsApps`, which is on
the default Windows 10+ user PATH. Verify in a **new** terminal:

```powershell
Get-Command hera-agent-unity-unity
```

If still not found, refresh PATH in the current session:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' +
            [Environment]::GetEnvironmentVariable('Path','Machine')
```

### PowerShell `where` does not work the way you think

In PowerShell `where` is aliased to `Where-Object`, not the Windows `where.exe`
binary. The result is confusing failures like:

```
where hera-agent-unity-unity
# Where-Object: ScriptBlock parameter required.
```

Use one of these instead:

```powershell
Get-Command hera-agent-unity-unity      # PowerShell-native
where.exe hera-agent-unity-unity        # explicit .exe suffix forces Windows where
```

This is also why bundling `where hera-agent-unity-unity; where hera` in one command can
mislead callers: the first call may succeed via the `where.exe` resolver while
the second fails, and the overall exit code is non-zero.

---

## Running binary differs from PATH

If you see:

```
[hera-agent-unity-unity] warning: running binary differs from 'hera-agent-unity-unity' on PATH.
  running: /path/A/hera-agent-unity
  on PATH: /path/B/hera-agent-unity
```

You have two installs. Common cause: an older `go install` copy in
`$GOPATH/bin` shadowing (or being shadowed by) the script install.

Resolve by removing the older one:

```sh
hera-agent-unity-unity doctor      # lists all copies
rm /path/to/older/hera-agent-unity
```

To silence the warning without fixing the duplication (not recommended):

```sh
export HERA_AGENT_NO_PATH_CHECK=1
```

---

## Unity is not detected

`hera-agent-unity-unity doctor` will report `no Unity instances detected` when the
Connector package is not installed or Unity is not running.

Install the Connector in Unity:

1. Window → Package Manager → + → Add package from git URL
2. Paste:
   `https://github.com/NotNull92/hera-agent-unity-unity.git?path=AgentConnector`

The Connector starts automatically when Unity opens and writes a heartbeat
file to `~/.hera-agent-unity-unity/instances/`.

If you have multiple Unity instances open, select one explicitly:

```
hera-agent-unity-unity --project /path/to/MyProject scene info
hera-agent-unity-unity --port 8765 scene info
```

---

## `hera-agent-unity-unity.exe.bak` left behind after update

`update` swaps the binary via rename-dance: the old `.exe` becomes `.bak`, the
new download is renamed into place, then `.bak` is removed. On Windows the
outgoing process still holds an image-mapping on `.bak`, so direct removal
fails with "Access is denied" and a deferred `cmd.exe del` is scheduled
instead. If you see a leftover `hera-agent-unity-unity.exe.bak` next to the binary, it
either failed to delete (rare) or your terminal was closed before the
deferred delete fired. Safe to remove by hand. `uninstall` sweeps it too.

---

## Stale heartbeat

`hera-agent-unity-unity doctor` reports `(stale)` next to an instance whose last
heartbeat is older than 3 seconds. Causes:

- Unity is paused on a breakpoint or modal dialog.
- Unity is mid-domain-reload (after script recompile / play mode enter).
- The Connector assembly failed to load — check the Unity console for
  `[HeraAgent]` errors.

The CLI tolerates short stalls (`waitForAlive` polls until a new heartbeat
appears). If the staleness persists, restart Unity.
