package toolregistry

import (
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"path/filepath"
	"runtime"
	"strings"
)

func ProjectID(projectPath string) (string, error) {
	if strings.TrimSpace(projectPath) == "" {
		return "", fmt.Errorf("project path is required")
	}
	absolute, err := filepath.Abs(projectPath)
	if err != nil {
		return "", fmt.Errorf("normalize project path: %w", err)
	}
	normalized := strings.TrimRight(filepath.ToSlash(filepath.Clean(absolute)), "/")
	if runtime.GOOS == "windows" {
		normalized = strings.ToLower(normalized)
	}
	digest := sha256.Sum256([]byte(normalized))
	return "sha256:" + hex.EncodeToString(digest[:]), nil
}
