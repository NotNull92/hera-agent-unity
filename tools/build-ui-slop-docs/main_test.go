package main

import (
	"encoding/json"
	"os"
	"strings"
	"testing"
)

func TestTaxonomyIsUGUIOnly(t *testing.T) {
	raw, err := os.ReadFile("ui_slop.jsonl")
	if err != nil {
		t.Fatal(err)
	}

	retiredTerms := []string{"ui toolkit", "uitk", "uxml", ".uss", " uss", "uss ", "panelsettings", "pickingmode"}
	for lineNo, line := range strings.Split(strings.TrimSpace(string(raw)), "\n") {
		var entry Entry
		if err := json.Unmarshal([]byte(line), &entry); err != nil {
			t.Fatalf("line %d: %v", lineNo+1, err)
		}

		lower := strings.ToLower(line)
		for _, term := range retiredTerms {
			if strings.Contains(lower, term) {
				t.Errorf("line %d (%s): retired term %q remains", lineNo+1, entry.ID, term)
			}
		}
	}
}
