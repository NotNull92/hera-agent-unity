package mcpserver

import (
	"encoding/json"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func nativeMCPTool(tool toolregistry.Tool) *mcp.Tool {
	return &mcp.Tool{
		Name:         tool.Name,
		Title:        tool.Title,
		Description:  tool.Description,
		InputSchema:  json.RawMessage(tool.InputSchema),
		OutputSchema: envelopeSchema(outputDataSchema(tool.OutputSchema)),
		Annotations:  nativeAnnotations(tool),
	}
}

func outputDataSchema(outputSchema json.RawMessage) json.RawMessage {
	var schema struct {
		Properties map[string]json.RawMessage `json:"properties"`
	}
	if json.Unmarshal(outputSchema, &schema) != nil ||
		!schemaPropertyHasType(schema.Properties["success"], "boolean") ||
		!schemaPropertyHasType(schema.Properties["message"], "string") {
		return outputSchema
	}
	if data, ok := schema.Properties["data"]; ok {
		return data
	}
	return outputSchema
}

func schemaPropertyHasType(property json.RawMessage, want string) bool {
	var schema struct {
		Type string `json:"type"`
	}
	return json.Unmarshal(property, &schema) == nil && schema.Type == want
}

func nativeAnnotations(tool toolregistry.Tool) *mcp.ToolAnnotations {
	safeties := flattenSafety(tool.Safety)
	for _, action := range tool.Actions {
		safeties = append(safeties, flattenSafety(action.Safety)...)
	}
	readOnly, idempotent := true, true
	destructive, openWorld := false, false
	for _, safety := range safeties {
		readOnly = readOnly && safety.ReadOnly
		idempotent = idempotent && safety.Idempotent
		destructive = destructive || safety.Destructive
		risk := strings.ToLower(safety.RiskClass)
		openWorld = openWorld || strings.Contains(risk, "package") || strings.Contains(risk, "external") || strings.Contains(risk, "network") || strings.Contains(risk, "arbitrary")
	}
	return &mcp.ToolAnnotations{
		Title:           tool.Title,
		ReadOnlyHint:    readOnly,
		DestructiveHint: boolPointer(destructive),
		IdempotentHint:  idempotent,
		OpenWorldHint:   boolPointer(openWorld),
	}
}

func flattenSafety(safety toolregistry.Safety) []toolregistry.Safety {
	flattened := []toolregistry.Safety{safety}
	for _, rule := range safety.Rules {
		flattened = append(flattened, flattenSafety(rule.Safety)...)
	}
	return flattened
}

func boolPointer(value bool) *bool { return &value }

func envelopeSchema(dataSchema json.RawMessage) map[string]any {
	var data any
	if json.Unmarshal(dataSchema, &data) != nil {
		data = map[string]any{}
	}
	return map[string]any{
		"type":                 "object",
		"additionalProperties": false,
		"required":             []string{"success", "message"},
		"properties": map[string]any{
			"success":     map[string]any{"type": "boolean"},
			"message":     map[string]any{"type": "string"},
			"code":        map[string]any{"type": "string"},
			"suggestions": map[string]any{"type": "array", "items": map[string]any{"type": "string"}},
			"agent_hint":  map[string]any{"type": "string"},
			"data":        data,
			"timings":     map[string]any{"type": "object", "additionalProperties": map[string]any{"type": "integer"}},
		},
	}
}
