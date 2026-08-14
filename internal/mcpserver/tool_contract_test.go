package mcpserver

import (
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestNativeMCPToolUsesHeraEnvelopeDataSchema(t *testing.T) {
	// Given
	tool := toolregistry.Tool{
		Name:        "items",
		InputSchema: json.RawMessage(`{"type":"object"}`),
		OutputSchema: json.RawMessage(`{
			"type":"object",
			"properties":{
				"success":{"type":"boolean"},
				"message":{"type":"string"},
				"data":{"type":"array","items":{"type":"string"}}
			}
		}`),
	}

	// When
	definition := nativeMCPTool(tool)

	// Then
	output := definition.OutputSchema.(map[string]any)
	properties := output["properties"].(map[string]any)
	data := properties["data"].(map[string]any)
	if data["type"] != "array" {
		t.Fatalf("data schema=%#v, want array without a nested Hera envelope", data)
	}
}

func TestNativeMCPToolPreservesDataOnlyOutputSchema(t *testing.T) {
	// Given
	tool := toolregistry.Tool{
		Name:         "items",
		InputSchema:  json.RawMessage(`{"type":"object"}`),
		OutputSchema: json.RawMessage(`{"type":"array","items":{"type":"integer"}}`),
	}

	// When
	definition := nativeMCPTool(tool)

	// Then
	output := definition.OutputSchema.(map[string]any)
	properties := output["properties"].(map[string]any)
	data := properties["data"].(map[string]any)
	if data["type"] != "array" {
		t.Fatalf("data schema=%#v, want original data-only schema", data)
	}
}
