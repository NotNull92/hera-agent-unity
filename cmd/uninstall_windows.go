//go:build windows

package cmd

import (
	"fmt"
	"os"
	"path/filepath"
)

func removeFromPATH(installDir string) error {
	// installDir is %LOCALAPPDATA%\Microsoft\WindowsApps — a Windows-managed
	// directory that's on the default user PATH. Removing it would break every
	// Store-app alias on the system. Leave it alone.
	//
	// Only legacy hera-agent-unity-unity PATH entries (from pre-WindowsApps installs)
	// need cleanup.
	legacyDir, _ := legacyInstallPaths()
	if legacyDir == "" {
		return nil
	}
	// Verify the legacy directory actually exists before trying to clean PATH.
	if _, err := os.Stat(legacyDir); os.IsNotExist(err) {
		return nil
	}
	// Use a single-line script so -Command treats the entire expression as one
	// statement. Multi-line strings cause PowerShell to parse subsequent lines as
	// separate commands, producing "GetFullPath" and "CommandNotFound" errors.
	script := `$legacy = $args[0]; if (-not $legacy) { exit 0 }; $norm = [System.IO.Path]::GetFullPath($legacy).TrimEnd('\'); $p = [Environment]::GetEnvironmentVariable("Path", "User"); $entries = if ($p) { $p -split ';' } else { @() }; $filtered = $entries | Where-Object { if (-not $_) { return $false }; try { return ([System.IO.Path]::GetFullPath($_).TrimEnd('\')) -ne $norm } catch { return $true } }; $new = $filtered -join ';'; if ($new -ne $p) { [Environment]::SetEnvironmentVariable("Path", $new, "User") }`
	if err := runPowerShellWithArgs(script, legacyDir); err != nil {
		return err
	}

	// Update current session for good measure
	currentPath := os.Getenv("PATH")
	newCurrentPath := removePathEntry(currentPath, legacyDir)
	_ = os.Setenv("PATH", newCurrentPath)
	return nil
}

func removeBinaryAndDir(exe, installDir string) (deferred bool, err error) {
	binPath := filepath.Join(installDir, "hera-agent-unity-unity.exe")

	// Remove the binary in installDir (WindowsApps). If it is the running
	// executable Windows locks it, so scheduleDelete falls back to a
	// hidden PowerShell process that deletes the file after we exit.
	if d, sErr := scheduleDelete(binPath); sErr != nil {
		fmt.Fprintf(os.Stderr, "warning: could not schedule delete of %s: %v\n", binPath, sErr)
	} else if d {
		deferred = true
	}

	// Sweep stale artifacts left by `update`: the rename-dance backup
	// (hera-agent-unity-unity.exe.bak) and any partial download (hera-agent-unity-unity-*.tmp).
	// Without this they accumulate forever and surface in 'doctor' as
	// confusing duplicates after the user already ran uninstall.
	if sweepArtifacts(installDir) {
		deferred = true
	}

	// installDir is WindowsApps (a shared OS directory) — never remove it.
	// We only own the binary and its sidecars inside.

	// Clean up the legacy install location (%LOCALAPPDATA%\hera-agent-unity-unity)
	// for users who installed before v0.0.6. RemoveAll instead of Remove
	// because .bak / .tmp leftovers leave the directory non-empty.
	legacyDir, _ := legacyInstallPaths()
	if legacyDir != "" {
		if _, statErr := os.Stat(legacyDir); statErr == nil {
			if rmErr := os.RemoveAll(legacyDir); rmErr != nil {
				fmt.Fprintf(os.Stderr, "warning: could not remove legacy dir %s: %v\n", legacyDir, rmErr)
			}
		}
	}

	// Remove the running binary itself if it lives outside installDir
	// (e.g. an ad-hoc `go build` copy the user is invoking directly).
	if exe != "" && exe != binPath {
		if d, sErr := scheduleDelete(exe); sErr != nil {
			fmt.Fprintf(os.Stderr, "warning: could not schedule delete of %s: %v\n", exe, sErr)
		} else if d {
			deferred = true
		}
	}
	return deferred, nil
}

func sweepArtifacts(dir string) (anyDeferred bool) {
	patterns := []string{"hera-agent-unity-unity.exe.bak", "hera-agent-unity-unity-*.tmp"}
	for _, pat := range patterns {
		matches, _ := filepath.Glob(filepath.Join(dir, pat))
		for _, m := range matches {
			d, err := scheduleDelete(m)
			if err != nil {
				fmt.Fprintf(os.Stderr, "warning: could not remove %s: %v\n", m, err)
				continue
			}
			if d {
				anyDeferred = true
			}
		}
	}
	return anyDeferred
}
