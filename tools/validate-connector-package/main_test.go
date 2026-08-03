package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestValidateRejectsMissingMeta(t *testing.T) {
	root := t.TempDir()
	writeFile(t, filepath.Join(root, "Editor", "Tool.cs"), "class Tool {}")
	if err := validate(root); err == nil || !strings.Contains(err.Error(), "missing Unity meta file") {
		t.Fatalf("error=%v", err)
	}
}

func TestValidateRejectsDuplicateAssemblyReference(t *testing.T) {
	root := t.TempDir()
	path := filepath.Join(root, "Editor", "Tests.asmdef")
	writeFile(t, path, `{"name":"Tests","references":["UnityEditor.TestRunner","UnityEditor.TestRunner"]}`)
	writeFile(t, path+".meta", "fileFormatVersion: 2")
	if err := validate(root); err == nil || !strings.Contains(err.Error(), "duplicate reference") {
		t.Fatalf("error=%v", err)
	}
}

func TestValidateAcceptsPackage(t *testing.T) {
	root := t.TempDir()
	path := filepath.Join(root, "Editor", "Tests.asmdef")
	writeFile(t, path, `{"name":"Tests","references":["HeraAgent.Editor","UnityEditor.TestRunner"]}`)
	writeFile(t, path+".meta", "fileFormatVersion: 2")
	if err := validate(root); err != nil {
		t.Fatal(err)
	}
}

func writeFile(t *testing.T, path, contents string) {
	t.Helper()
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, []byte(contents), 0o600); err != nil {
		t.Fatal(err)
	}
}
