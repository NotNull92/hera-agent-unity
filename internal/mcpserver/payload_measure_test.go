package mcpserver

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestMeasureProfileToolDefinitionsUsesRegisteredDefinitions(t *testing.T) {
	// Given
	snapshot := nativeTestSnapshot(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{
		config: enabledTestConfig(), snapshot: snapshot,
		sender: &recordingToolSender{response: successResponse()},
	})
	defer closeSession()

	// When
	listed, err := session.ListTools(context.Background(), nil)
	if err != nil {
		t.Fatal(err)
	}
	tools, err := snapshot.Catalog.ToolsForProfile("core")
	if err != nil {
		t.Fatal(err)
	}
	definitions := make([]*mcp.Tool, 0, len(tools))
	for _, tool := range tools {
		definitions = append(definitions, nativeMCPTool(tool))
	}
	encoded, err := json.Marshal(&mcp.ListToolsResult{Tools: definitions})
	if err != nil {
		t.Fatal(err)
	}
	measured, err := MeasureProfileToolDefinitions(snapshot.Catalog, "core")

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if measured.ToolCount != len(listed.Tools) || measured.Bytes != len(encoded) {
		t.Fatalf("measured=%#v listed_tools=%d listed_bytes=%d", measured, len(listed.Tools), len(encoded))
	}
}

func TestMeasureCompactToolDefinitionsUsesRegisteredDefinitions(t *testing.T) {
	// Given
	snapshot := nativeTestSnapshot(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{
		config: compactTestConfig(), snapshot: snapshot,
		sender: &recordingToolSender{response: successResponse()},
	})
	defer closeSession()

	// When
	listed, err := session.ListTools(context.Background(), nil)
	if err != nil {
		t.Fatal(err)
	}
	encoded, err := json.Marshal(&mcp.ListToolsResult{Tools: []*mcp.Tool{
		compactSearchTool(), compactDescribeTool(), compactCallTool(),
	}})
	if err != nil {
		t.Fatal(err)
	}
	measured, err := MeasureCompactToolDefinitions()

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if measured.ToolCount != len(listed.Tools) || measured.Bytes != len(encoded) {
		t.Fatalf("measured=%#v listed_tools=%d listed_bytes=%d", measured, len(listed.Tools), len(encoded))
	}
}
