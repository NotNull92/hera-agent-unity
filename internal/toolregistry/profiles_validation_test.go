package toolregistry

import (
	"encoding/json"
	"strings"
	"testing"
)

func TestParseCatalog_rejects_arbitrary_code_in_normal_profile(t *testing.T) {
	// Given
	var document map[string]json.RawMessage
	if err := json.Unmarshal(validFixture(t), &document); err != nil {
		t.Fatal(err)
	}
	var tools []map[string]json.RawMessage
	if err := json.Unmarshal(document["tools"], &tools); err != nil {
		t.Fatal(err)
	}
	var safety map[string]any
	if err := json.Unmarshal(tools[0]["safety"], &safety); err != nil {
		t.Fatal(err)
	}
	safety["risk_class"] = "arbitrary_code"
	tools[0]["safety"] = mustMarshal(t, safety)
	document["tools"] = mustMarshal(t, tools)
	hash, err := computeCatalogHash(document)
	if err != nil {
		t.Fatal(err)
	}
	document["catalog_hash"] = mustMarshal(t, hash)

	// When
	_, err = ParseCatalog(mustMarshal(t, document))

	// Then
	if err == nil || !strings.Contains(err.Error(), "arbitrary-code") {
		t.Fatalf("ParseCatalog() error=%v", err)
	}
}
