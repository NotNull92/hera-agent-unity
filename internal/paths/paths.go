package paths

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

func BaseDir() string {
	home, err := os.UserHomeDir()
	if err != nil {
		home = "."
	}
	return filepath.Join(home, ".hera-agent-unity")
}

func InstancesDir() string     { return filepath.Join(BaseDir(), "instances") }
func StatusDir() string        { return filepath.Join(BaseDir(), "status") }
func ResultsDir() string       { return filepath.Join(BaseDir(), "results") }
func AssetConfigPath() string  { return filepath.Join(BaseDir(), "asset-config.json") }
func VersionCheckPath() string { return filepath.Join(BaseDir(), "version-check.json") }

func TestResultPath(port int, runID string) string {
	return filepath.Join(BaseDir(), "status", fmt.Sprintf("test-results-%d-%s.json", port, runID))
}

func TestPendingPath(port int, runID string) string {
	return filepath.Join(BaseDir(), "status", fmt.Sprintf("test-pending-%d-%s.json", port, runID))
}

func BuildResultPath(port int) string {
	return filepath.Join(BaseDir(), "status", fmt.Sprintf("build-result-%d.json", port))
}

func LegacyTestResultPath(port int) string {
	return filepath.Join(BaseDir(), "status", fmt.Sprintf("test-results-%d.json", port))
}

func PackageResultPath(port int, jobID string) string {
	return filepath.Join(BaseDir(), "status", fmt.Sprintf("package-result-%d-%s.json", port, jobID))
}

func OperationsDir(projectID string) string {
	return filepath.Join(StatusDir(), "operations", strings.ReplaceAll(projectID, ":", "_"))
}
