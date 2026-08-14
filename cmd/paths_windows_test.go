//go:build windows

package cmd

import (
	"path/filepath"
	"testing"
)

func TestLegacyInstallDir_WhenLocalAppDataIsSet_ReturnsLegacyDirectory(t *testing.T) {
	// Given: Windows exposes the canonical per-user local application root.
	localAppData := t.TempDir()
	t.Setenv("LOCALAPPDATA", localAppData)

	// When: the pre-WindowsApps install directory is resolved.
	dir, err := legacyInstallDir()

	// Then: the result stays below LOCALAPPDATA.
	if err != nil {
		t.Fatal(err)
	}
	want := filepath.Join(localAppData, "hera-agent-unity")
	if dir != want {
		t.Fatalf("legacyInstallDir() = %q, want %q", dir, want)
	}
}

func TestLegacyInstallDir_WhenLocalAppDataIsMissing_ReturnsError(t *testing.T) {
	// Given: no safe absolute legacy root is available.
	t.Setenv("LOCALAPPDATA", "")

	// When: the legacy install directory is resolved.
	dir, err := legacyInstallDir()

	// Then: cleanup cannot fall back to a relative path.
	if err == nil {
		t.Fatalf("legacyInstallDir() = %q, want error", dir)
	}
	if dir != "" {
		t.Fatalf("legacyInstallDir() = %q, want empty path on error", dir)
	}
}

func TestLegacyInstallDir_WhenLocalAppDataIsRelative_ReturnsError(t *testing.T) {
	// Given: an inherited environment contains a relative cleanup root.
	t.Setenv("LOCALAPPDATA", filepath.Join("relative", "local"))

	// When: the legacy install directory is resolved.
	dir, err := legacyInstallDir()

	// Then: recursive cleanup cannot target the current working directory.
	if err == nil {
		t.Fatalf("legacyInstallDir() = %q, want error", dir)
	}
	if dir != "" {
		t.Fatalf("legacyInstallDir() = %q, want empty path on error", dir)
	}
}
