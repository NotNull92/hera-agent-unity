package main

import (
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestBuildReportIsDeterministicAndSeparatesRawBytesFromEstimates(t *testing.T) {
	catalog := &toolregistry.Catalog{
		CatalogHash: "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
		Tools: []toolregistry.Tool{{
			Name: "alpha", Title: "Alpha", Description: "Inspect alpha", ContractMode: toolregistry.ContractStrict,
			Profiles:     []string{"core", "full"},
			InputSchema:  json.RawMessage(`{"type":"object"}`),
			OutputSchema: json.RawMessage(`{"type":"object"}`),
			Actions: []toolregistry.Action{{
				Name: "inspect", Description: "Inspect",
				InputSchema:  json.RawMessage(`{"type":"object"}`),
				OutputSchema: json.RawMessage(`{"type":"object"}`),
			}},
		}},
	}
	options := reportOptions{Largest: 10, WarnProfileBytes: 1, WarnToolBytes: 1}
	first, err := buildReport(catalog, 123, options)
	if err != nil {
		t.Fatal(err)
	}
	second, err := buildReport(catalog, 123, options)
	if err != nil {
		t.Fatal(err)
	}
	firstJSON, _ := json.Marshal(first)
	secondJSON, _ := json.Marshal(second)
	if string(firstJSON) != string(secondJSON) {
		t.Fatalf("reports differ:\n%s\n%s", firstJSON, secondJSON)
	}
	if first.InputBytes != 123 || first.NormalizedCatalogBytes == 0 || first.RoughTokens.Central == 0 {
		t.Fatalf("size fields=%#v", first)
	}
	if len(first.Profiles) != 2 || first.Profiles[0].Name != "core" || first.Profiles[1].Name != "full" {
		t.Fatalf("profiles=%#v", first.Profiles)
	}
	if len(first.Warnings) == 0 {
		t.Fatal("warning budgets produced no warnings")
	}
	if len(first.ActionDescribeSavings) != 1 || first.ActionDescribeSavings[0].ActionBytes == 0 {
		t.Fatalf("action describe savings=%#v", first.ActionDescribeSavings)
	}
}

func TestUnwrapCatalogAcceptsHeraEnvelope(t *testing.T) {
	raw := []byte(`{"success":true,"data":{"schema_version":"hera.tool-catalog/1"}}`)
	got, err := unwrapCatalog(raw)
	if err != nil || string(got) != `{"schema_version":"hera.tool-catalog/1"}` {
		t.Fatalf("got=%s error=%v", got, err)
	}
}
