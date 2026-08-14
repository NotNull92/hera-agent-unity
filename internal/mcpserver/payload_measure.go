package mcpserver

import (
	"encoding/json"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

// ToolDefinitionPayload is the serialized MCP tools/list definition footprint.
type ToolDefinitionPayload struct {
	ToolCount int
	Bytes     int
}

// MeasureCompactToolDefinitions measures the fixed Compact discovery surface.
func MeasureCompactToolDefinitions() (ToolDefinitionPayload, error) {
	return measureToolDefinitions([]*mcp.Tool{
		compactSearchTool(), compactDescribeTool(), compactCallTool(),
	})
}

// MeasureProfileToolDefinitions measures one catalog-owned native profile.
func MeasureProfileToolDefinitions(catalog *toolregistry.Catalog, profile string) (ToolDefinitionPayload, error) {
	tools, err := catalog.ToolsForProfile(profile)
	if err != nil {
		return ToolDefinitionPayload{}, err
	}
	definitions := make([]*mcp.Tool, 0, len(tools))
	for _, tool := range tools {
		definitions = append(definitions, nativeMCPTool(tool))
	}
	return measureToolDefinitions(definitions)
}

func measureToolDefinitions(tools []*mcp.Tool) (ToolDefinitionPayload, error) {
	encoded, err := json.Marshal(&mcp.ListToolsResult{Tools: tools})
	if err != nil {
		return ToolDefinitionPayload{}, fmt.Errorf("encode MCP tool definitions: %w", err)
	}
	return ToolDefinitionPayload{ToolCount: len(tools), Bytes: len(encoded)}, nil
}
