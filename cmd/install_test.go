package cmd

import (
	"os"
	"path/filepath"
	"runtime"
	"testing"
)

func TestGetInstallPaths(t *testing.T) {
	dir := t.TempDir()

	t.Setenv("HOME", dir)
	t.Setenv("USERPROFILE", dir)

	d, bin := getInstallPaths()

	if runtime.GOOS == "windows" {
		wantDir := filepath.Join(dir, "AppData", "Local", "Microsoft", "WindowsApps")
		wantBin := filepath.Join(wantDir, "hera-agent-unity.exe")
		if d != wantDir {
			t.Errorf("getInstallPaths dir = %q, want %q", d, wantDir)
		}
		if bin != wantBin {
			t.Errorf("getInstallPaths bin = %q, want %q", bin, wantBin)
		}
	} else {
		wantDir := filepath.Join(dir, ".local", "bin")
		wantBin := filepath.Join(wantDir, "hera-agent-unity")
		if d != wantDir {
			t.Errorf("getInstallPaths dir = %q, want %q", d, wantDir)
		}
		if bin != wantBin {
			t.Errorf("getInstallPaths bin = %q, want %q", bin, wantBin)
		}
	}
}

func TestCopyFile(t *testing.T) {
	dir := t.TempDir()
	src := filepath.Join(dir, "src.txt")
	dst := filepath.Join(dir, "dst.txt")
	content := []byte("hello, world")

	if err := os.WriteFile(src, content, 0o644); err != nil {
		t.Fatal(err)
	}

	if err := copyFile(src, dst); err != nil {
		t.Fatalf("copyFile failed: %v", err)
	}

	got, err := os.ReadFile(dst)
	if err != nil {
		t.Fatal(err)
	}
	if string(got) != string(content) {
		t.Errorf("copied content = %q, want %q", got, content)
	}
}
