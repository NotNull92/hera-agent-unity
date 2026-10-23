//go:build !windows

package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

func addToPATH(installDir string) error {
	home, err := os.UserHomeDir()
	if err != nil {
		return err
	}

	exportLine := fmt.Sprintf(`export PATH="%s:$PATH"`, installDir)

	// Update .bashrc
	bashrc := filepath.Join(home, ".bashrc")
	if err := appendLineIfNotExists(bashrc, exportLine); err != nil {
		return err
	}

	// Update .zshrc if exists
	zshrc := filepath.Join(home, ".zshrc")
	if _, err := os.Stat(zshrc); err == nil {
		return appendLineIfNotExists(zshrc, exportLine)
	}

	return nil
}

func appendLineIfNotExists(path, line string) error {
	data, _ := os.ReadFile(path)
	content := string(data)
	if strings.Contains(content, line) {
		return nil
	}
	f, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		return err
	}
	defer f.Close()
	_, err = f.WriteString(line + "\n")
	return err
}
