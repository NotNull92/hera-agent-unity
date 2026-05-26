package cmd

import "testing"

func TestFindAsset(t *testing.T) {
	assets := []ghAsset{
		{Name: "hera-agent-unity-unity-linux-amd64"},
		{Name: "hera-agent-unity-unity-darwin-arm64"},
		{Name: "hera-agent-unity-unity-windows-amd64.exe"},
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

	noMatch := []ghAsset{{Name: "hera-agent-unity-unity-plan9-mips"}}
	got = findAsset(noMatch)
	if got != nil {
		t.Error("findAsset: should return nil when no platform match")
	}
}
