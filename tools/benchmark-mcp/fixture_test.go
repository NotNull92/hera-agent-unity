package main

import (
	"os"
	"path/filepath"
	"testing"
)

func TestFinalizeFixtureCopiesConnectorWithoutManifestMutation(t *testing.T) {
	root := filepath.Join(t.TempDir(), "fixture")
	connector := filepath.Join(t.TempDir(), "AgentConnector")
	unity := createUnityDependencies(t)
	createProjectSettings(t, root)
	createConnectorSource(t, connector)
	manifestPath := filepath.Join(root, "Packages", "manifest.json")
	manifest := []byte("{\"dependencies\":{}}\n")
	if err := os.WriteFile(manifestPath, manifest, 0o600); err != nil {
		t.Fatal(err)
	}
	if err := finalizeFixture(root, connector, unity); err != nil {
		t.Fatal(err)
	}
	if err := validateFixture(root); err != nil {
		t.Fatal(err)
	}
	gotManifest, err := os.ReadFile(manifestPath)
	if err != nil {
		t.Fatal(err)
	}
	if string(gotManifest) != string(manifest) {
		t.Fatalf("manifest changed to %s", gotManifest)
	}
	if _, err := os.Stat(filepath.Join(root, "Assets", "HeraAgent", "Editor", "HeraAgent.asmdef")); err != nil {
		t.Fatalf("copied Connector: %v", err)
	}
	if _, err := os.Stat(filepath.Join(root, "Assets", "HeraAgent", "Editor", "Tests")); !os.IsNotExist(err) {
		t.Fatalf("Connector tests were copied: %v", err)
	}
	if _, err := os.Stat(filepath.Join(root, "Assets", "HeraAgent", "Editor", "Dependencies", "Newtonsoft.Json.dll")); err != nil {
		t.Fatalf("copied Newtonsoft: %v", err)
	}
	if _, err := os.Stat(filepath.Join(root, "Assets", "HeraAgent", "Dependencies", "uGUI", "Runtime", "UGUI", "UnityEngine.UI.asmdef")); err != nil {
		t.Fatalf("copied uGUI: %v", err)
	}
}

func TestValidateFixtureRefusesUnmarkedProject(t *testing.T) {
	if err := validateFixture(t.TempDir()); err == nil {
		t.Fatal("accepted unmarked project")
	}
}

func TestPrepareFixtureRefusesNonEmptyDestination(t *testing.T) {
	root := t.TempDir()
	if err := os.WriteFile(filepath.Join(root, "user.asset"), []byte("keep"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := prepareFixture("unity", root, t.TempDir()); err == nil {
		t.Fatal("accepted non-empty destination")
	}
}

func TestCreateProjectArgumentsDisableUPM(t *testing.T) {
	arguments := createProjectArguments("C:/fixture")
	found := false
	for _, argument := range arguments {
		found = found || argument == "-noUpm"
	}
	if !found {
		t.Fatalf("arguments = %v", arguments)
	}
}

func createProjectSettings(t *testing.T, root string) {
	t.Helper()
	path := filepath.Join(root, "ProjectSettings")
	if err := os.MkdirAll(path, 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.MkdirAll(filepath.Join(root, "Packages"), 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(path, "ProjectSettings.asset"), []byte("fixture\n"), 0o600); err != nil {
		t.Fatal(err)
	}
}

func createConnectorSource(t *testing.T, root string) {
	t.Helper()
	path := filepath.Join(root, "Editor")
	if err := os.MkdirAll(path, 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(path, "HeraAgent.asmdef"), []byte("{}\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.MkdirAll(filepath.Join(path, "Tests"), 0o700); err != nil {
		t.Fatal(err)
	}
}

func createUnityDependencies(t *testing.T) string {
	t.Helper()
	editor := filepath.Join(t.TempDir(), "Editor")
	managed := filepath.Join(editor, "Data", "Managed")
	ugui := filepath.Join(editor, "Data", "Resources", "PackageManager", "BuiltInPackages", "com.unity.ugui", "Runtime", "UGUI")
	for _, path := range []string{managed, ugui} {
		if err := os.MkdirAll(path, 0o700); err != nil {
			t.Fatal(err)
		}
	}
	if err := os.WriteFile(filepath.Join(managed, "Newtonsoft.Json.dll"), []byte("dll"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(ugui, "UnityEngine.UI.asmdef"), []byte("{}"), 0o600); err != nil {
		t.Fatal(err)
	}
	return filepath.Join(editor, "Unity.exe")
}
