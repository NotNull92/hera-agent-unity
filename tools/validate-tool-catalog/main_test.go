package main

import (
	"bytes"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestRun_validates_catalog_file_and_reports_summary(t *testing.T) {
	// Given
	var stdout bytes.Buffer
	fixture := filepath.Join(repoRoot(t), "internal", "toolregistry", "testdata", "catalog-v1.json")

	// When
	err := run([]string{"--file", fixture}, strings.NewReader(""), &stdout)

	// Then
	if err != nil {
		t.Fatalf("run validator: %v", err)
	}
	want := `{"schema_version":"hera.tool-catalog/1","catalog_hash":"sha256:3b059d40771c4b71b3aee5180df4d240cad0820d9ea9909e0764eccb748ddb0c","tools":1,"actions":1,"strict":1}` + "\n"
	if stdout.String() != want {
		t.Fatalf("stdout = %q, want %q", stdout.String(), want)
	}
}

func TestRun_rejects_corrupt_catalog(t *testing.T) {
	// Given
	var stdout bytes.Buffer

	// When
	err := run(nil, strings.NewReader(`{"schema_version":"hera.tool-catalog/1"}`), &stdout)

	// Then
	if err == nil {
		t.Fatal("expected validation error")
	}
	if stdout.Len() != 0 {
		t.Fatalf("stdout = %q, want empty", stdout.String())
	}
}

func repoRoot(t *testing.T) string {
	t.Helper()
	_, file, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatal("resolve test file")
	}
	root := filepath.Clean(filepath.Join(filepath.Dir(file), "..", ".."))
	if _, err := os.Stat(filepath.Join(root, "go.mod")); err != nil {
		t.Fatalf("resolve repository root: %v", err)
	}
	return root
}
