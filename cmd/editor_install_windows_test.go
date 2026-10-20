package cmd

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestResolveUnitySecondaryInstallPath(t *testing.T) {
	const version = "6000.6.6f1"
	for _, name := range []string{"valid", "default wins", "missing secondary exe", "empty", "corrupt", "wrong type", "relative", "flag wins", "env wins", "explicit missing"} {
		t.Run(name, func(t *testing.T) {
			root := t.TempDir()
			programFiles := filepath.Join(root, "program")
			appdata := filepath.Join(root, "appdata")
			secondary := filepath.Join(root, "secondary")
			explicit := filepath.Join(root, "explicit")
			t.Setenv("ProgramFiles", programFiles)
			t.Setenv("APPDATA", appdata)
			t.Setenv("UNITY_HUB_EDITOR", "")
			defaultExe := filepath.Join(programFiles, "Unity", "Hub", "Editor", version, "Editor", "Unity.exe")
			secondaryExe := filepath.Join(secondary, version, "Editor", "Unity.exe")
			writeExe := func(path string) {
				t.Helper()
				if e := os.MkdirAll(filepath.Dir(path), 0700); e != nil {
					t.Fatal(e)
				}
				if e := os.WriteFile(path, []byte("fixture; never executed"), 0600); e != nil {
					t.Fatal(e)
				}
			}
			data, _ := json.Marshal(secondary)
			switch name {
			case "empty":
				data = []byte(`""`)
			case "corrupt":
				data = []byte(`{`)
			case "wrong type":
				data = []byte(`{}`)
			case "relative":
				data = []byte(`"relative"`)
			}
			config := filepath.Join(appdata, "UnityHub", "secondaryInstallPath.json")
			if e := os.MkdirAll(filepath.Dir(config), 0700); e != nil {
				t.Fatal(e)
			}
			if e := os.WriteFile(config, data, 0600); e != nil {
				t.Fatal(e)
			}
			writeExe(secondaryExe)
			hubRoot := ""
			want := secondaryExe
			switch name {
			case "default wins":
				writeExe(defaultExe)
				want = defaultExe
			case "missing secondary exe":
				if e := os.Remove(secondaryExe); e != nil {
					t.Fatal(e)
				}
				want = ""
			case "empty", "corrupt", "wrong type", "relative":
				want = ""
			case "flag wins":
				t.Setenv("UNITY_HUB_EDITOR", secondary)
				var e error
				hubRoot, e = parseEditorBootstrapFlags([]string{"--hub-root", explicit})
				if e != nil {
					t.Fatal(e)
				}
				want = filepath.Join(explicit, version, "Editor", "Unity.exe")
				writeExe(want)
			case "env wins":
				t.Setenv("UNITY_HUB_EDITOR", explicit)
				var e error
				hubRoot, e = parseEditorBootstrapFlags(nil)
				if e != nil {
					t.Fatal(e)
				}
				want = filepath.Join(explicit, version, "Editor", "Unity.exe")
				writeExe(want)
			case "explicit missing":
				hubRoot = explicit
				want = ""
			}
			got, e := resolveUnityExecutable(version, hubRoot)
			if want != "" {
				if e != nil || got != want {
					t.Fatalf("got %q %v want %q", got, e, want)
				}
				return
			}
			if e == nil || got != "" {
				t.Fatalf("got %q %v", got, e)
			}
			if name == "missing secondary exe" && (!strings.Contains(e.Error(), defaultExe) || !strings.Contains(e.Error(), secondaryExe)) {
				t.Fatal(e)
			}
			if name == "explicit missing" && strings.Contains(e.Error(), secondary) {
				t.Fatal("explicit root fell back", e)
			}
		})
	}
}
