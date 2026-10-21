package paths

import (
	"path/filepath"
	"testing"
)

func TestOperationsDirMatchesConnector(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)
	if got, want := OperationsDir("sha256:abcd"), filepath.Join(home, ".hera-agent-unity", "status", "operations", "sha256_abcd"); got != want {
		t.Fatalf("got %q want %q", got, want)
	}
}
