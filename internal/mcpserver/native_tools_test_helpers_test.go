package mcpserver

import (
	"context"
	"encoding/json"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type recordingToolSender struct {
	response       *client.CommandResponse
	calls          int
	command        string
	params         any
	options        client.SendOptions
	preflight      *client.ApprovalPreflight
	preflightCalls int
}

func (sender *recordingToolSender) PreflightApproval(
	_ context.Context,
	_ *client.Instance,
	_ client.ApprovalPreflightRequest,
) (*client.ApprovalPreflight, error) {
	sender.preflightCalls++
	return sender.preflight, nil
}

func (sender *recordingToolSender) SendWithOptions(
	_ context.Context,
	_ *client.Instance,
	command string,
	params any,
	_ int,
	options client.SendOptions,
) (*client.CommandResponse, error) {
	sender.calls++
	sender.command = command
	sender.params = params
	sender.options = options
	return sender.response, nil
}

const nativeTestCatalogHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

func startNativeTestSession(t *testing.T, profile string, sender toolSender) (*mcp.ClientSession, func()) {
	t.Helper()
	return startNativeTestSessionWithSnapshot(t, profile, sender, nativeTestSnapshot(t))
}

func startNativeTestSessionWithSnapshot(t *testing.T, profile string, sender toolSender, snapshot *toolregistry.Snapshot) (*mcp.ClientSession, func()) {
	t.Helper()
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	done := make(chan error, 1)
	config := enabledTestConfig()
	config.Profile = profile
	runtime := nativeRuntime{
		instance: &client.Instance{Port: 1234, Features: []string{client.FeatureApprovalV1, client.FeatureOperationLedgerV1}},
		snapshot: snapshot,
		sender:   sender,
		approver: sender.(approvalSender),
		timeout:  1_000,
	}
	go func() {
		done <- runPrepared(ctx, config, serverTransport, runtime)
	}()
	client := mcp.NewClient(&mcp.Implementation{Name: "native-test", Version: "test"}, nil)
	session, err := client.Connect(ctx, clientTransport, nil)
	if err != nil {
		cancel()
		t.Fatal(err)
	}
	return session, func() {
		_ = session.Close()
		cancel()
		select {
		case err := <-done:
			if err != nil {
				t.Errorf("server error: %v", err)
			}
		case <-time.After(time.Second):
			t.Error("server did not stop")
		}
	}
}

func nativeTestSnapshot(t *testing.T) *toolregistry.Snapshot {
	t.Helper()
	objectSchema := json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"action":{"type":"string"},"name":{"type":"string"}},"required":["action"]}`)
	outputSchema := json.RawMessage(`{"type":"object","additionalProperties":true}`)
	tools := []toolregistry.Tool{
		{
			Name: "console", Title: "Console", Description: "Inspect logs", ContractMode: toolregistry.ContractStrict,
			Profiles: []string{"diagnostics", "testing"}, InputSchema: objectSchema, OutputSchema: outputSchema,
			Safety: toolregistry.Safety{RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true},
		},
		{
			Name: "exec", Title: "Exec", Description: "Execute code", ContractMode: toolregistry.ContractStrict,
			Profiles: []string{"advanced"}, InputSchema: objectSchema, OutputSchema: outputSchema,
			Safety: toolregistry.Safety{RiskClass: "arbitrary_code", RequiresConfirmation: true},
		},
		{
			Name: "manage_assets", Title: "Manage Assets", Description: "Manage assets", ContractMode: toolregistry.ContractStrict,
			Profiles: []string{"assets"}, InputSchema: objectSchema, OutputSchema: outputSchema,
			Safety: toolregistry.Safety{RiskClass: "write", Reversible: true, SideEffectScope: "assets"},
		},
		{
			Name: "manage_gameobject", Title: "Manage GameObject", Description: "Manage scene objects", ContractMode: toolregistry.ContractStrict,
			Profiles: []string{"core", "scene", "ui"}, InputSchema: objectSchema, OutputSchema: outputSchema,
			Safety: toolregistry.Safety{RiskClass: "write", Reversible: true, SideEffectScope: "scene"},
		},
		{
			Name: "scene", Title: "Scene", Description: "Inspect scenes", ContractMode: toolregistry.ContractStrict,
			Profiles: []string{"core", "scene"}, InputSchema: objectSchema, OutputSchema: outputSchema,
			Safety: toolregistry.Safety{RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true},
		},
	}
	catalog := &toolregistry.Catalog{CatalogHash: nativeTestCatalogHash, Tools: tools}
	compiled, err := schema.NewCompilerCache().Compile(catalog.CatalogHash, catalog.SchemaDefinitions())
	if err != nil {
		t.Fatal(err)
	}
	return &toolregistry.Snapshot{Catalog: catalog, Schemas: compiled, Exposure: toolregistry.ExposureProfile}
}

func successResponse() *client.CommandResponse {
	return &client.CommandResponse{Success: true, Message: "OK", Data: json.RawMessage(`{"ok":true}`)}
}

func mcpToolNames(tools []*mcp.Tool) []string {
	names := make([]string, len(tools))
	for index, tool := range tools {
		names[index] = tool.Name
	}
	return names
}

func assertStructuredCode(t *testing.T, result *mcp.CallToolResult, want string) {
	t.Helper()
	encoded, err := json.Marshal(result.StructuredContent)
	if err != nil {
		t.Fatal(err)
	}
	var envelope client.CommandResponse
	if err := json.Unmarshal(encoded, &envelope); err != nil {
		t.Fatal(err)
	}
	if envelope.Code != want {
		t.Fatalf("code=%q envelope=%s, want %q", envelope.Code, encoded, want)
	}
}
