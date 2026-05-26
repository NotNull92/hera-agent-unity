//go:build !windows

package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

func removeLineFromFile(path, line string) error {
	data, err := os.ReadFile(path)
	if err != nil {
		return err
	}
	lines := strings.Split(string(data), "\n")
	var out []string
	for _, l := range lines {
		if strings.TrimSpace(l) == strings.TrimSpace(line) {
			continue
		}
		out = append(out, l)
	}
	return os.WriteFile(path, []byte(strings.Join(out, "\n")), 0644)
}

func removeFromPATH(installDir string) error {
	home, err := os.UserHomeDir()
	if err != nil {
		return err
	}
	exportLine := fmt.Sprintf(`export PATH="%s:$PATH"`, installDir)
	_ = removeLineFromFile(filepath.Join(home, ".bashrc"), exportLine)
	_ = removeLineFromFile(filepath.Join(home, ".zshrc"), exportLine)

	// Update current session PATH immediately
	currentPath := os.Getenv("PATH")
	newPath := removePathEntry(currentPath, installDir)
	_ = os.Setenv("PATH", newPath)
	return nil
}

func removeBinaryAndDir(exe, installDir string) (deferred bool, err error) {
	binPath := filepath.Join(installDir, "hera-agent-unity-unity")
	if _, statErr := os.Stat(binPath); statErr == nil {
		if rmErr := os.Remove(binPath); rmErr != nil {
			return false, rmErr
		}
	}
	// Remove installDir if empty
	_ = os.Remove(installDir) // ignore error if not empty

	// Also remove the running binary itself if it's outside installDir
	if exe != "" && exe != binPath {
		_ = os.Remove(exe)
	}
	return false, nil
}
