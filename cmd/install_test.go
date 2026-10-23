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

func TestCopyFileSameFile(t *testing.T) {
	path := filepath.Join(t.TempDir(), "binary")
	if err := os.WriteFile(path, []byte("keep me"), 0600); err != nil {
		t.Fatal(err)
	}
	if err := copyFile(path, path); err != nil {
		t.Fatal(err)
	}
	got, err := os.ReadFile(path)
	if err != nil || string(got) != "keep me" {
		t.Fatalf("content=%q, err=%v", got, err)
	}
}

func TestRemoveConfigDir(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)
	state := filepath.Join(home, ".hera-agent-unity")
	if err := os.MkdirAll(filepath.Join(state, "instances"), 0700); err != nil {
		t.Fatal(err)
	}
	if err := removeConfigDir(); err != nil {
		t.Fatal(err)
	}
	if _, err := os.Stat(state); !os.IsNotExist(err) {
		t.Fatalf("state remains: %v", err)
	}
	if err := removeConfigDir(); err != nil {
		t.Fatal(err)
	}
}

func TestAddToPATHUnixFailure(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("Unix shell config")
	}
	home := t.TempDir()
	t.Setenv("HOME", home)
	// A directory at the rc path rejects writes even when tests run as root.
	if err := os.Mkdir(filepath.Join(home, ".bashrc"), 0700); err != nil {
		t.Fatal(err)
	}
	if err := addToPATH("/test/bin"); err == nil {
		t.Fatal("expected shell config write error")
	}
}
