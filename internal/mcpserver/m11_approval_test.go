package mcpserver

import (
	"context"
	"encoding/base64"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestMRTRUnsupportedFallback(t *testing.T) {
	// Given
	preflight := testMCPApprovalPreflight()
	sender := &recordingToolSender{response: successResponse(), preflight: preflight}
	config := enabledTestConfig()
	config.Profile = "advanced"
	config.AllowArbitraryCode = true
	config.MRTR = true
	snapshot := nativeTestSnapshot(t)
	session, closeSession := startConfiguredTestSession(t, testServerSetup{config, snapshot, sender})
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name: "exec", Arguments: map[string]any{"action": "run"},
	})

	// Then
	if err != nil || !result.IsError || sender.calls != 0 || sender.preflightCalls != 1 {
		t.Fatalf("result=%#v error=%v calls=%d preflights=%d", result, err, sender.calls, sender.preflightCalls)
	}
	assertStructuredCode(t, result, "APPROVAL_REQUIRED")
}

func TestMRTRApprovalDispatchesWithBoundToken(t *testing.T) {
	// Given
	preflight := testMCPApprovalPreflight()
	sender := &recordingToolSender{response: successResponse(), preflight: preflight}
	config := enabledTestConfig()
	config.Profile = "advanced"
	config.AllowArbitraryCode = true
	config.MRTR = true
	snapshot := nativeTestSnapshot(t)
	session, closeSession := startConfiguredTestSessionWithClient(
		t,
		testServerSetup{config, snapshot, sender},
		&mcp.ClientOptions{ElicitationHandler: func(
			context.Context,
			*mcp.ElicitRequest,
		) (*mcp.ElicitResult, error) {
			return &mcp.ElicitResult{Action: "accept"}, nil
		}},
	)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name: "exec", Arguments: map[string]any{"action": "run"},
	})

	// Then
	if err != nil || result.IsError || sender.preflightCalls != 1 || sender.calls != 1 {
		t.Fatalf("result=%#v error=%v calls=%d preflights=%d", result, err, sender.calls, sender.preflightCalls)
	}
	if sender.options.ApprovalToken != preflight.Token || sender.options.OperationID != "op_mcp_approval" {
		t.Fatalf("options=%#v", sender.options)
	}
}

func TestMRTRDeniedApprovalCausesZeroMutation(t *testing.T) {
	// Given
	sender := &recordingToolSender{response: successResponse(), preflight: testMCPApprovalPreflight()}
	config := enabledTestConfig()
	config.Profile = "advanced"
	config.AllowArbitraryCode = true
	config.MRTR = true
	snapshot := nativeTestSnapshot(t)
	session, closeSession := startConfiguredTestSessionWithClient(
		t,
		testServerSetup{config, snapshot, sender},
		&mcp.ClientOptions{ElicitationHandler: func(
			context.Context,
			*mcp.ElicitRequest,
		) (*mcp.ElicitResult, error) {
			return &mcp.ElicitResult{Action: "decline"}, nil
		}},
	)
	defer closeSession()

	// When
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{
		Name: "exec", Arguments: map[string]any{"action": "run"},
	})

	// Then
	if err != nil || !result.IsError || sender.calls != 0 || sender.preflightCalls != 1 {
		t.Fatalf("result=%#v error=%v calls=%d preflights=%d", result, err, sender.calls, sender.preflightCalls)
	}
	assertStructuredCode(t, result, "APPROVAL_DENIED")
}

func testMCPApprovalPreflight() *client.ApprovalPreflight {
	claims := `{"version":1,"operation_id":"op_mcp_approval","tool":"exec","action":"run","arguments_hash":"sha256:test","risk_class":"arbitrary_code","project_id":"test-project","expires_at_ms":4102444800000,"single_use":true}`
	return &client.ApprovalPreflight{
		Token:       base64.RawURLEncoding.EncodeToString([]byte(claims)) + ".signature",
		OperationID: "op_mcp_approval", ExpiresAtMS: 4_102_444_800_000,
		Summary: client.ApprovalSummary{Tool: "exec", Action: "run", SideEffect: "arbitrary_code", OperationID: "op_mcp_approval"},
	}
}
