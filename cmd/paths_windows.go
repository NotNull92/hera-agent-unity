//go:build windows

package cmd

import (
	"errors"
	"os"
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
