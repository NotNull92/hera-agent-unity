package mcpserver

import (
	"context"
	"encoding/json"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestNativeToolValidatesBeforeUnity(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	session, closeSession := startNativeTestSession(t, "core", sender)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name:      "scene",
		Arguments: map[string]any{},
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !result.IsError || sender.calls != 0 {
		t.Fatalf("result=%#v Unity calls=%d, want validation error before Unity", result, sender.calls)
	}
	assertStructuredCode(t, result, "INVALID_ARGUMENT")
}

func TestNativeToolPreservesHeraErrorCode(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: &client.CommandResponse{
		Success:     false,
		Code:        "SCENE_NOT_FOUND",
		Message:     "Scene was not found",
		Suggestions: []string{"Check the scene path"},
		Data:        json.RawMessage(`{"path":"Assets/Missing.unity"}`),
	}}
	session, closeSession := startNativeTestSession(t, "core", sender)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name:      "scene",
		Arguments: map[string]any{"action": "info"},
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !result.IsError {
		t.Fatalf("result=%#v, want tool error", result)
	}
	assertStructuredCode(t, result, "SCENE_NOT_FOUND")
}

func TestNativeMutationUsesOperationID(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	session, closeSession := startNativeTestSession(t, "core", sender)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name: "manage_gameobject",
		Arguments: map[string]any{
			"action": "create",
			"name":   "M9Probe",
		},
	})

	// Then
	if err != nil || result.IsError {
		t.Fatalf("CallTool() result=%#v error=%v", result, err)
	}
	if sender.options.OperationID == "" || sender.options.ClientKind != "mcp" || sender.options.Idempotent {
		t.Fatalf("send options=%#v", sender.options)
	}
	if sender.options.CatalogHash != nativeTestCatalogHash {
		t.Fatalf("catalog hash=%q", sender.options.CatalogHash)
	}
}

func TestNativeApprovalRequiredBeforeUnity(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse()}
	snapshot := nativeTestSnapshot(t)
	for index := range snapshot.Catalog.Tools {
		if snapshot.Catalog.Tools[index].Name == "manage_gameobject" {
			snapshot.Catalog.Tools[index].Safety.RequiresConfirmation = true
		}
	}
	session, closeSession := startNativeTestSessionWithSnapshot(t, "core", sender, snapshot)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name:      "manage_gameobject",
		Arguments: map[string]any{"action": "create", "name": "Blocked"},
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !result.IsError || sender.calls != 0 {
		t.Fatalf("result=%#v Unity calls=%d", result, sender.calls)
	}
	assertStructuredCode(t, result, "APPROVAL_REQUIRED")
}

func TestMutatingProfileRequiresOperationLedger(t *testing.T) {
	// Given
	config := enabledTestConfig()
	server := newServer(config)
	runtime := nativeRuntime{
		instance: &client.Instance{Port: 1234},
		snapshot: nativeTestSnapshot(t),
		sender:   &recordingToolSender{response: successResponse()},
		timeout:  1_000,
	}

	// When
	err := registerNativeTools(server, config, runtime)

	// Then
	if err == nil || !strings.Contains(err.Error(), client.FeatureOperationLedgerV1) {
		t.Fatalf("registerNativeTools() error=%v, want ledger requirement", err)
	}
}

func TestNativeAnnotationsIncludeActionSafetyRules(t *testing.T) {
	// Given
	tool := toolregistry.Tool{
		Title:  "Conditional",
		Safety: toolregistry.Safety{ReadOnly: true, Idempotent: true},
		Actions: []toolregistry.Action{{
			Safety: toolregistry.Safety{
				ReadOnly: true,
				Rules: []toolregistry.SafetyRule{{Safety: toolregistry.Safety{
					RiskClass: "destructive", Destructive: true,
				}}},
			},
		}},
	}

	// When
	annotations := nativeAnnotations(tool)

	// Then
	if annotations.DestructiveHint == nil || !*annotations.DestructiveHint || annotations.ReadOnlyHint {
		t.Fatalf("annotations=%#v", annotations)
	}
}
