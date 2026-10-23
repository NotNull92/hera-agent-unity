package cmd

import (
	"crypto/sha256"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestVerifyChecksum(t *testing.T) {
	path := filepath.Join(t.TempDir(), "binary")
	if err := os.WriteFile(path, []byte("binary"), 0600); err != nil {
		t.Fatal(err)
	}
	hash := fmt.Sprintf("%x", sha256.Sum256([]byte("binary")))
	for _, tc := range []struct {
		name, sums string
		valid      bool
	}{
		{"match", hash + "  asset.exe\n", true},
		{"binary marker", hash + " *asset.exe\r\n", true},
		{"mismatch", strings.Repeat("0", 64) + "  asset.exe\n", false},
		{"missing filename", hash + "  other.exe\n", false},
		{"empty", "", false},
		{"invalid hash", "oops  asset.exe\n", false},
		{"duplicate", hash + "  asset.exe\n" + hash + "  asset.exe\n", false},
	} {
		t.Run(tc.name, func(t *testing.T) {
			if err := verifyChecksum(path, "asset.exe", strings.NewReader(tc.sums)); (err == nil) != tc.valid {
				t.Fatalf("err=%v", err)
			}
		})
	}
	if findAsset([]githubAsset{{Name: "SHA256SUMS"}}) != nil {
		t.Fatal("selected checksum as binary")
	}
}

func TestFindAsset(t *testing.T) {
	assets := []githubAsset{
		{Name: "hera-agent-unity-linux-amd64"},
		{Name: "hera-agent-unity-darwin-arm64"},
		{Name: "hera-agent-unity-windows-amd64.exe"},
	}

	// findAsset uses runtime.GOOS/GOARCH, so we just verify it returns something on the current platform
	got := findAsset(assets)
	if got == nil {
		t.Error("findAsset: should find asset for current platform")
	}

	empty := findAsset(nil)
	if empty != nil {
		t.Error("findAsset: should return nil for empty list")
	}

	noMatch := []githubAsset{{Name: "hera-agent-unity-plan9-mips"}}
	got = findAsset(noMatch)
	if got != nil {
		t.Error("findAsset: should return nil when no platform match")
	}
}
