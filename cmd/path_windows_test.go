//go:build windows

package cmd

import (
	"fmt"
	"testing"
	"time"

	"golang.org/x/sys/windows/registry"
)

func TestFilterLegacyPATH(t *testing.T) {
	for _, kind := range []uint32{registry.SZ, registry.EXPAND_SZ} {
		for _, tc := range []struct{ value, want string }{
			{`%LOCALAPPDATA%\Microsoft\WindowsApps;%TOOLS%\bin`, `%LOCALAPPDATA%\Microsoft\WindowsApps;%TOOLS%\bin`},
			{`;%TOOLS%\bin;; C:\\old\\HeRa-Agent-Unity\\;C:\unity-agent-cli-pro;`, `%TOOLS%\bin`},
			{`C:\hera-agent-unity-extra;C:\hera-agent-unity\bin`, `C:\hera-agent-unity-extra;C:\hera-agent-unity\bin`},
			{"", ""},
		} {
			got, gotKind, changed := filterLegacyPATH(tc.value, kind)
			if got != tc.want || gotKind != kind || changed != (tc.value != tc.want) {
				t.Fatalf("got %q/%d/%v", got, gotKind, changed)
			}
		}
	}
}

func TestScrubLegacyPATHTemporaryRegistry(t *testing.T) {
	path := fmt.Sprintf(`Software\hera-agent-unity-test-%d`, time.Now().UnixNano())
	key, _, err := registry.CreateKey(registry.CURRENT_USER, path, registry.ALL_ACCESS)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = key.Close(); _ = registry.DeleteKey(registry.CURRENT_USER, path) })
	for _, kind := range []uint32{registry.EXPAND_SZ, registry.SZ} {
		value := `%TOOLS%\bin;C:\hera-agent-unity;`
		if kind == registry.EXPAND_SZ {
			err = key.SetExpandStringValue("Path", value)
		} else {
			err = key.SetStringValue("Path", value)
		}
		if err != nil {
			t.Fatal(err)
		}
		if changed, err := scrubLegacyPATHKey(key); err != nil || !changed {
			t.Fatalf("changed=%v err=%v", changed, err)
		}
		got, gotKind, err := key.GetStringValue("Path")
		if err != nil || got != `%TOOLS%\bin` || gotKind != kind {
			t.Fatalf("value=%q type=%d err=%v", got, gotKind, err)
		}
		if changed, err := scrubLegacyPATHKey(key); err != nil || changed {
			t.Fatalf("unchanged rewrite: %v %v", changed, err)
		}
	}
}
