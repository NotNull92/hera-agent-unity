//go:build windows

package cmd

import (
	"os"
	"os/exec"
	"path/filepath"
)

// legacyInstallPaths returns the pre-WindowsApps install location
// (%LOCALAPPDATA%\hera-agent). uninstall scrubs leftover binaries and PATH
// entries from this location for users who installed before v0.0.6.
func legacyInstallPaths() (dir, bin string) {
	home, _ := os.UserHomeDir()
	dir = filepath.Join(home, "AppData", "Local", "hera-agent")
	bin = filepath.Join(dir, "hera-agent.exe")
	return
}

// runPowerShellWithArgs invokes powershell.exe with -Command "<script>" and
// the supplied positional args. The script can reference $args[0], $args[1],
// etc. to read them.
func runPowerShellWithArgs(script string, args ...string) error {
	psArgs := []string{"-Command", script}
	psArgs = append(psArgs, args...)
	cmd := exec.Command("powershell.exe", psArgs...)
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	return cmd.Run()
}
