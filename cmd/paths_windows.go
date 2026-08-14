//go:build windows

package cmd

import (
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

// legacyInstallDir returns the pre-WindowsApps install location
// (%LOCALAPPDATA%\hera-agent-unity). uninstall scrubs leftover binaries and PATH
// entries from this location for users who installed before v0.0.6.
func legacyInstallDir() (string, error) {
	root := strings.TrimSpace(os.Getenv("LOCALAPPDATA"))
	if root == "" || !filepath.IsAbs(root) {
		return "", errors.New("LOCALAPPDATA must be an absolute path")
	}
	return filepath.Join(root, "hera-agent-unity"), nil
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
