package mcpserver

import (
	"encoding/json"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func commandResult(response *client.CommandResponse) *mcp.CallToolResult {
	text := "OK"
	if !response.Success && response.Message != "" {
		text = response.Message
	}
	return &mcp.CallToolResult{
		Content:           []mcp.Content{&mcp.TextContent{Text: text}},
		StructuredContent: responseEnvelope(response),
		IsError:           !response.Success,
	}
}

func errorResult(code, message string, data any) *mcp.CallToolResult {
	response := &client.CommandResponse{Success: false, Code: code, Message: message}
	if data != nil {
		response.Data, _ = json.Marshal(data)
	}
	return commandResult(response)
}

func responseEnvelope(response *client.CommandResponse) map[string]any {
	envelope := map[string]any{
		"success": response.Success,
		"message": response.Message,
	}
	if response.Code != "" {
		envelope["code"] = response.Code
	}
	if len(response.Suggestions) > 0 {
		envelope["suggestions"] = response.Suggestions
	}
	if response.AgentHint != "" {
		envelope["agent_hint"] = response.AgentHint
	}
	if len(response.Data) > 0 {
		var data any
		if json.Unmarshal(response.Data, &data) == nil {
			envelope["data"] = data
		}
	}
	if len(response.Timings) > 0 {
		envelope["timings"] = response.Timings
	}
	return envelope
}
