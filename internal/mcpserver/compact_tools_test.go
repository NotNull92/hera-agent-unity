package mcpserver

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestCompactExposureRegistersOnlyDiscoveryTools(t *testing.T) {
	// Given
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), nativeTestSnapshot(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	result, err := session.ListTools(context.Background(), nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	want := []string{"tool_call", "tool_describe", "tool_search"}
	if got := mcpToolNames(result.Tools); !equalStrings(got, want) {
		t.Fatalf("tools=%v, want %v", got, want)
	}
}

func TestCompactSearchRanksDynamicCustomToolDeterministically(t *testing.T) {
	// Given
	snapshot := snapshotWithDynamicTool(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshot, &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	first := callToolData(t, session, "tool_search", map[string]any{"query": "dynamic probe", "limit": 3})
	second := callToolData(t, session, "tool_search", map[string]any{"query": "dynamic probe", "limit": 3})

	// Then
	if firstJSON, secondJSON := mustJSON(t, first), mustJSON(t, second); firstJSON != secondJSON {
		t.Fatalf("search order changed: first=%s second=%s", firstJSON, secondJSON)
	}
	results := first.([]any)
	if len(results) == 0 || results[0].(map[string]any)["name"] != "dynamic_probe" {
		t.Fatalf("results=%#v, want dynamic_probe first", results)
	}
}

func TestCompactSearchReturnsActionsAndCompactSafetyWithoutSchemas(t *testing.T) {
	// Given
	snapshot := snapshotWithDynamicTool(t)
	for index := range snapshot.Catalog.Tools {
		if snapshot.Catalog.Tools[index].Name == "dynamic_probe" {
			snapshot.Catalog.Tools[index].Safety.Rules = []toolregistry.SafetyRule{{
				Operation: "inspect",
				Safety: toolregistry.Safety{
					RiskClass: "destructive", Destructive: true, RequiresConfirmation: true,
				},
			}}
		}
	}
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshot, &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	data := callToolData(t, session, "tool_search", map[string]any{"query": "dynamic probe"}).([]any)

	// Then
	result := data[0].(map[string]any)
	actions, ok := result["actions"].([]any)
	if !ok || len(actions) != 1 || actions[0] != "inspect" {
		t.Fatalf("actions=%#v, want inspect", actions)
	}
	if _, ok := result["input_schema"]; ok {
		t.Fatalf("search result exposed input_schema: %#v", result)
	}
	safety := result["safety"].(map[string]any)
	if _, ok := safety["rules"]; ok {
		t.Fatalf("search result exposed safety rules: %#v", safety)
	}
	if _, ok := safety["side_effect_scope"]; ok {
		t.Fatalf("search result exposed full safety metadata: %#v", safety)
	}
	if safety["risk_class"] != "conditional" || safety["destructive"] != true || safety["requires_confirmation"] != true || safety["read_only"] != false {
		t.Fatalf("search result did not conservatively summarize safety rules: %#v", safety)
	}
}

func TestCompactSearchSchemaOmitsIncludeSchema(t *testing.T) {
	// Given
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), nativeTestSnapshot(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	result, err := session.ListTools(context.Background(), nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	for _, tool := range result.Tools {
		if tool.Name != "tool_search" {
			continue
		}
		schema := tool.InputSchema.(map[string]any)
		properties := schema["properties"].(map[string]any)
		if _, ok := properties["include_schema"]; ok {
			t.Fatalf("tool_search schema still exposes include_schema: %#v", properties)
		}
		return
	}
	t.Fatal("tool_search was not registered")
}

func TestCompactDescribeReturnsCatalogIdentity(t *testing.T) {
	// Given
	snapshot := snapshotWithDynamicTool(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshot, &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	data := callToolData(t, session, "tool_describe", map[string]any{"name": "dynamic_probe"}).(map[string]any)

	// Then
	if data["catalog_hash"] != nativeTestCatalogHash || data["domain_epoch"] != "m10-test-epoch" {
		t.Fatalf("identity=%#v", data)
	}
	definition := data["tool"].(map[string]any)
	if definition["name"] != "dynamic_probe" || definition["contract_mode"] != toolregistry.ContractStrict {
		t.Fatalf("tool=%#v", definition)
	}
	if _, ok := definition["input_schema"]; ok {
		t.Fatalf("overview exposed input_schema: %#v", definition)
	}
	actions, ok := data["actions"].([]any)
	if !ok || len(actions) != 1 {
		t.Fatalf("actions=%#v, want inspect overview", actions)
	}
	action, ok := actions[0].(map[string]any)
	if !ok || action["name"] != "inspect" {
		t.Fatalf("actions=%#v, want inspect overview", actions)
	}
}

func TestCompactDescribeCanReturnOneAction(t *testing.T) {
	// Given
	snapshot := snapshotWithDynamicTool(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshot, &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	overview := callToolData(t, session, "tool_describe", map[string]any{"name": "dynamic_probe"}).(map[string]any)
	selected := callToolData(t, session, "tool_describe", map[string]any{"name": "dynamic_probe", "action": "show"}).(map[string]any)

	// Then
	action := selected["action"].(map[string]any)
	if action["name"] != "inspect" || selected["catalog_hash"] != nativeTestCatalogHash {
		t.Fatalf("selected action=%#v", selected)
	}
	if _, ok := action["input_schema"]; !ok {
		t.Fatalf("selected action omitted input_schema: %#v", action)
	}
	if _, ok := overview["actions"]; !ok {
		t.Fatalf("name-only describe omitted action overview: %#v", overview)
	}
}

func TestCompactDescribeReturnsSchemasForToolWithoutActions(t *testing.T) {
	// Given
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), nativeTestSnapshot(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	data := callToolData(t, session, "tool_describe", map[string]any{"name": "scene"}).(map[string]any)

	// Then
	if _, ok := data["input_schema"]; !ok {
		t.Fatalf("schema-only tool omitted input_schema: %#v", data)
	}
	if _, ok := data["output_schema"]; !ok {
		t.Fatalf("schema-only tool omitted output_schema: %#v", data)
	}
}

func TestCompactDescribeRejectsUnknownActionCompactly(t *testing.T) {
	// Given
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshotWithDynamicTool(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name: "tool_describe", Arguments: map[string]any{"name": "dynamic_probe", "action": "missing"},
	})

	// Then
	if err != nil || !result.IsError {
		t.Fatalf("result=%#v error=%v", result, err)
	}
	assertStructuredCode(t, result, "ACTION_NOT_FOUND")
}
func TestCompactCallUsesClientOperationIDForDynamicCustomTool(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshotWithDynamicTool(t), sender})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{Name: "tool_call", Arguments: map[string]any{
		"name": "dynamic_probe", "arguments": map[string]any{"action": "inspect"}, "operation_id": "op_client_fixture",
	}})

	// Then
	if err != nil || result.IsError {
		t.Fatalf("CallTool() result=%#v error=%v", result, err)
	}
	if sender.command != "dynamic_probe" || sender.options.OperationID != client.OperationID("op_client_fixture") || sender.options.ClientKind != "mcp" {
		t.Fatalf("sender=%#v", sender)
	}
}

func TestCompactCallRejectsInvalidClientOperationIDBeforeUnity(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshotWithDynamicTool(t), sender})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{Name: "tool_call", Arguments: map[string]any{
		"name": "dynamic_probe", "arguments": map[string]any{"action": "inspect"}, "operation_id": "bad id",
	}})

	// Then
	if err != nil || !result.IsError || sender.calls != 0 {
		t.Fatalf("result=%#v error=%v calls=%d", result, err, sender.calls)
	}
	assertStructuredCode(t, result, "INVALID_OPERATION_ID")
}

func TestCompactLegacyCallAppliesConservativePolicyBeforeUnity(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	snapshot := &toolregistry.Snapshot{Catalog: &toolregistry.Catalog{
		CatalogHash: nativeTestCatalogHash, DomainEpoch: "legacy", Tools: []toolregistry.Tool{{
			Name: "legacy_probe", Title: "Legacy Probe", Description: "Dynamic legacy tool", ContractMode: toolregistry.ContractLegacy,
			Profiles: []string{"compact"}, InputSchema: json.RawMessage(`{"type":"object"}`), OutputSchema: json.RawMessage(`{"type":"object"}`),
			Safety: toolregistry.Safety{RiskClass: "unspecified", Destructive: true, RequiresConfirmation: true},
		}}}, Exposure: toolregistry.ExposureCompactOnly}
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), snapshot, sender})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{Name: "tool_call", Arguments: map[string]any{
		"name": "legacy_probe", "arguments": map[string]any{},
	}})

	// Then
	if err != nil || !result.IsError || sender.calls != 0 {
		t.Fatalf("result=%#v error=%v calls=%d", result, err, sender.calls)
	}
	assertStructuredCode(t, result, "APPROVAL_REQUIRED")
}

func TestCompactSearchHidesArbitraryCodeWithoutStartupPermission(t *testing.T) {
	// Given
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), nativeTestSnapshot(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	data := callToolData(t, session, "tool_search", map[string]any{"query": "exec"}).([]any)

	// Then
	if len(data) != 0 {
		t.Fatalf("results=%#v, want no arbitrary-code tools", data)
	}
}

func TestCompactCallRejectsArbitraryCodeWithoutStartupPermission(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	session, closeSession := startConfiguredTestSession(t, testServerSetup{compactTestConfig(), nativeTestSnapshot(t), sender})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{Name: "tool_call", Arguments: map[string]any{
		"name": "exec", "arguments": map[string]any{"action": "inspect"},
	}})

	// Then
	if err != nil || !result.IsError || sender.calls != 0 {
		t.Fatalf("result=%#v error=%v calls=%d", result, err, sender.calls)
	}
	assertStructuredCode(t, result, "ARBITRARY_CODE_PERMISSION_REQUIRED")
}
