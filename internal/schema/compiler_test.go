package schema

import (
	"encoding/json"
	"errors"
	"testing"
)

func TestCompilerCache_Compile_reuses_catalog_hash(t *testing.T) {
	// Given
	cache := NewCompilerCache()
	definitions := []Definition{{
		Key:    "scene/input",
		Schema: json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{}}`),
	}}

	// When
	first, err := cache.Compile("sha256:catalog", definitions)
	if err != nil {
		t.Fatalf("first compile: %v", err)
	}
	second, err := cache.Compile("sha256:catalog", definitions)

	// Then
	if err != nil {
		t.Fatalf("second compile: %v", err)
	}
	if first != second {
		t.Fatal("compiled catalog was not reused")
	}
}

func TestCompiledCatalog_Validate_returns_JSON_pointer_when_input_invalid(t *testing.T) {
	// Given
	cache := NewCompilerCache()
	compiled, err := cache.Compile("sha256:catalog", []Definition{{
		Key: "scene/input",
		Schema: json.RawMessage(
			`{"type":"object","additionalProperties":false,"properties":{"action":{"type":"string"}}}`,
		),
	}})
	if err != nil {
		t.Fatalf("compile: %v", err)
	}

	// When
	err = compiled.Validate("scene/input", map[string]any{"unexpected": true})

	// Then
	var validationError *ValidationError
	if !errors.As(err, &validationError) {
		t.Fatalf("error = %v, want ValidationError", err)
	}
	if validationError.Pointer != "/unexpected" {
		t.Fatalf("pointer = %q, want /unexpected", validationError.Pointer)
	}
}

func TestCompilerCache_Compile_rejects_invalid_schema(t *testing.T) {
	// Given
	cache := NewCompilerCache()

	// When
	_, err := cache.Compile("sha256:catalog", []Definition{{
		Key:    "scene/input",
		Schema: json.RawMessage(`{"type":7}`),
	}})

	// Then
	if err == nil {
		t.Fatal("expected invalid schema error")
	}
}

func TestCompilerCache_bounds_compiled_catalogs(t *testing.T) {
	// Given
	cache := newCompilerCache(2)
	definitions := []Definition{{
		Key:    "scene/input",
		Schema: json.RawMessage(`{"type":"object"}`),
	}}

	// When
	for _, hash := range []string{"sha256:one", "sha256:two", "sha256:three"} {
		if _, err := cache.Compile(hash, definitions); err != nil {
			t.Fatalf("compile %s: %v", hash, err)
		}
	}

	// Then
	if len(cache.catalogs) != 2 {
		t.Fatalf("compiled catalogs = %d, want 2", len(cache.catalogs))
	}
	if _, ok := cache.catalogs["sha256:one"]; ok {
		t.Fatal("oldest compiled catalog was not evicted")
	}
}
