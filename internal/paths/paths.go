package paths

import (
	"fmt"
	"os"
	"path/filepath"
)

func baseDir() string {
	home, err := os.UserHomeDir()
	if err != nil {
		home = "."
	}
	return filepath.Join(home, ".hera-agent-unity")
}

func InstancesDir() string     { return filepath.Join(baseDir(), "instances") }
func StatusDir() string        { return filepath.Join(baseDir(), "status") }
func AssetConfigPath() string  { return filepath.Join(baseDir(), "asset-config.json") }
func VersionCheckPath() string { return filepath.Join(baseDir(), "version-check.json") }

func TestResultPath(port int) string {
	return filepath.Join(baseDir(), "status", fmt.Sprintf("test-results-%d.json", port))
}

func PackageResultPath(port int, jobID string) string {
	return filepath.Join(baseDir(), "status", fmt.Sprintf("package-result-%d-%s.json", port, jobID))
}
