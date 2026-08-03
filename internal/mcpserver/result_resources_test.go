package mcpserver

import (
	"context"
	"encoding/json"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/resultstore"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

const mcpTestProjectID = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

func TestInvokeToolPreservesProjectionControlsBeforeSpooling(t *testing.T) {
	runtime := resultTestRuntime(t, 1024)
	sender := &recordingToolSender{response: &client.CommandResponse{Success: true, Message: "OK", Data: json.RawMessage(`{"ids":[1,2]}`)}}
	runtime.instance = &client.Instance{Features: []string{client.FeatureOperationLedgerV1}}
	runtime.snapshot = &toolregistry.Snapshot{Catalog: &toolregistry.Catalog{CatalogHash: nativeTestCatalogHash}}
	runtime.sender = sender
	runtime.timeout = 1_000
	params := map[string]any{"ids_only": true, "limit": json.Number("2")}
	result, err := invokeTool(context.Background(), runtime, toolInvocation{
		tool: toolregistry.Tool{Name: "find_gameobjects", ContractMode: toolregistry.ContractLegacy,
			Safety: toolregistry.Safety{RiskClass: "read_only", ReadOnly: true, Idempotent: true}},
		params: params, operationID: "op_small_result",
	})
	if err != nil {
		t.Fatal(err)
	}
	sent := sender.params.(map[string]any)
	if sent["ids_only"] != true || sent["limit"] != json.Number("2") {
		t.Fatalf("projection controls sent to Unity = %#v", sent)
	}
	structured := result.StructuredContent.(map[string]any)
	if result.IsError || structured["data"] == nil {
		t.Fatalf("small projected result = %#v", result.StructuredContent)
	}
	if _, ok := structured["resource"]; ok {
		t.Fatalf("small result unexpectedly spooled: %#v", result.StructuredContent)
	}
}

func TestMaxInlineBytesDefaultsAndRejectsNegativeValues(t *testing.T) {
	config := enabledTestConfig()
	if got := config.maxInlineBytes(); got != DefaultMaxInlineBytes {
		t.Fatalf("maxInlineBytes() = %d, want %d", got, DefaultMaxInlineBytes)
	}
	config.MaxInlineBytes = -1
	if err := config.Validate(); err == nil {
		t.Fatal("negative inline byte limit passed validation")
	}
}

func TestSensitiveKeyAvoidsBenignTokenAndSecretNames(t *testing.T) {
	for _, key := range []string{"cancellationToken", "tokenCount", "secretDoor", "credentialStatus"} {
		if sensitiveKey(key) {
			t.Fatalf("benign key %q was classified as sensitive", key)
		}
	}
}

func TestSensitiveKeyRecognizesCredentialKeysAndSuffixes(t *testing.T) {
	for _, key := range []string{
		"token", "authorization", "api_key", "clientSecret", "databasePassword",
		"refresh_token", "sessionCookie", "connectionString",
	} {
		if !sensitiveKey(key) {
			t.Fatalf("credential key %q was not classified as sensitive", key)
		}
	}
}

func TestBoundedCommandResultSpoolsOversizedResult(t *testing.T) {
	runtime := resultTestRuntime(t, 96)
	payload := strings.Repeat("large-result-", 30)
	response := &client.CommandResponse{Success: true, Message: "OK", Data: json.RawMessage(`{"payload":"` + payload + `"}`)}
	result := boundedCommandResult(runtime, toolInvocation{
		tool: toolregistry.Tool{Name: "scene_info"}, operationID: "op_large_result",
	}, response)
	encoded, err := json.Marshal(result)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(string(encoded), payload) {
		t.Fatal("oversized payload remained inline")
	}
	structured := result.StructuredContent.(map[string]any)
	metadata, ok := structured["resource"].(map[string]any)
	if !ok || metadata["uri"] == "" || structured["truncated"] != true {
		t.Fatalf("spooled metadata = %#v", result.StructuredContent)
	}
	if structured["code"] != "RESULT_SPOOLED" {
		t.Fatalf("spooled code = %#v", structured["code"])
	}
	stored, err := runtime.results.Read(metadata["uri"].(string))
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(stored), payload) {
		t.Fatal("retrieved resource did not contain the complete result")
	}
}

func TestInlineCapCountsTextFallbackAndStructuredContent(t *testing.T) {
	runtime := resultTestRuntime(t, 300)
	message := strings.Repeat("failure-detail-", 14)
	response := &client.CommandResponse{Success: false, Code: "UNITY_FAILURE", Message: message}
	full, err := json.Marshal(responseEnvelope(response))
	if err != nil || len(full) >= runtime.maxInlineBytes {
		t.Fatalf("fixture envelope bytes = %d, want below cap", len(full))
	}
	result := boundedCommandResult(runtime, toolInvocation{
		tool: toolregistry.Tool{Name: "probe"}, operationID: "op_text_fallback_result",
	}, response)
	encoded, _ := json.Marshal(result)
	if strings.Contains(string(encoded), message) {
		t.Fatal("duplicated text fallback bypassed the inline cap")
	}
	structured := result.StructuredContent.(map[string]any)
	if structured["code"] != "RESULT_SPOOLED" || structured["unity_code"] != "UNITY_FAILURE" {
		t.Fatalf("spooled Unity error metadata = %#v", structured)
	}
}

func TestBoundedCommandResultDoesNotSpoolSensitiveOrArbitraryCode(t *testing.T) {
	for _, test := range []struct {
		name string
		tool toolregistry.Tool
		data string
	}{
		{name: "sensitive key", tool: toolregistry.Tool{Name: "probe"}, data: `{"api_token":"` + strings.Repeat("secret", 30) + `"}`},
		{name: "session token", tool: toolregistry.Tool{Name: "probe"}, data: `{"session_token":"` + strings.Repeat("session-secret", 30) + `"}`},
		{name: "arbitrary code", tool: toolregistry.Tool{Name: "exec", Safety: toolregistry.Safety{RiskClass: "arbitrary_code"}}, data: `{"value":"` + strings.Repeat("file-content", 30) + `"}`},
		{name: "sensitive message", tool: toolregistry.Tool{Name: "probe"}, data: `{"value":"` + strings.Repeat("safe", 30) + `"}`},
	} {
		t.Run(test.name, func(t *testing.T) {
			runtime := resultTestRuntime(t, 64)
			message := "OK"
			if test.name == "sensitive message" {
				message = `{"access_token":"` + strings.Repeat("message-secret", 20) + `"}`
			}
			result := boundedCommandResult(runtime, toolInvocation{tool: test.tool, operationID: "op_guarded_result"},
				&client.CommandResponse{Success: true, Message: message, Data: json.RawMessage(test.data)})
			encoded, _ := json.Marshal(result)
			if !result.IsError || strings.Contains(string(encoded), "secretsecret") || strings.Contains(string(encoded), "file-content") || strings.Contains(string(encoded), "message-secret") {
				t.Fatalf("guarded result = %s", encoded)
			}
			structured := result.StructuredContent.(map[string]any)
			if structured["code"] != "RESULT_RESOURCE_UNAVAILABLE" {
				t.Fatalf("guard code = %#v", structured["code"])
			}
			if _, ok := structured["resource"]; ok {
				t.Fatalf("guarded result was spooled: %#v", result.StructuredContent)
			}
		})
	}
}

func TestBoundedCommandResultUsesOperationSpecificArbitraryCodeSafety(t *testing.T) {
	tool := toolregistry.Tool{
		Name: "menu", Safety: toolregistry.Safety{
			RiskClass: "read_only", ReadOnly: true, Idempotent: true,
			Rules: []toolregistry.SafetyRule{{
				Operation: "execute", When: json.RawMessage(`{"action":{"const":"execute"}}`),
				Safety: toolregistry.Safety{RiskClass: "arbitrary_code"},
			}},
		},
	}
	response := &client.CommandResponse{
		Success: true, Message: "OK", Data: json.RawMessage(`{"value":"` + strings.Repeat("menu-result-", 30) + `"}`),
	}
	safeRuntime := resultTestRuntime(t, 64)
	safe := boundedCommandResult(safeRuntime, toolInvocation{
		tool: tool, params: map[string]any{"action": "list"}, operationID: "op_safe_menu_result",
	}, response)
	safeContent := safe.StructuredContent.(map[string]any)
	if safe.IsError || safeContent["code"] != "RESULT_SPOOLED" {
		t.Fatalf("safe menu result = %#v", safeContent)
	}
	riskyRuntime := resultTestRuntime(t, 64)
	risky := boundedCommandResult(riskyRuntime, toolInvocation{
		tool: tool, params: map[string]any{"action": "execute"}, operationID: "op_risky_menu_result",
	}, response)
	riskyContent := risky.StructuredContent.(map[string]any)
	if !risky.IsError || riskyContent["code"] != "RESULT_RESOURCE_UNAVAILABLE" || riskyContent["reason"] != "sensitive_result" {
		t.Fatalf("risky menu result = %#v", riskyContent)
	}
}

func TestNegotiatedTaskCompletionSpoolsOversizedResult(t *testing.T) {
	runtime := resultTestRuntime(t, 96)
	payload := strings.Repeat("task-result-", 30)
	response, err := json.Marshal(&client.CommandResponse{
		Success: true, Message: "tests done", Data: json.RawMessage(`{"payload":"` + payload + `"}`),
	})
	if err != nil {
		t.Fatal(err)
	}
	result, err := detailedTaskResult(runtime, &taskbridge.Task{
		State: taskbridge.StateCompleted, OperationID: "op_task_large_result", Result: response,
	})
	if err != nil {
		t.Fatal(err)
	}
	encoded, err := json.Marshal(result.Result)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(string(encoded), payload) {
		t.Fatal("oversized task completion remained inline")
	}
	structured := result.Result["structuredContent"].(map[string]any)
	resource := structured["resource"].(map[string]any)
	stored, err := runtime.results.Read(resource["uri"].(string))
	if err != nil || !strings.Contains(string(stored), payload) {
		t.Fatalf("stored task result = %s, error = %v", stored, err)
	}
}

func resultTestRuntime(t *testing.T, maxInline int) nativeRuntime {
	t.Helper()
	store, err := resultstore.New(mcpTestProjectID, resultstore.Options{
		Root: t.TempDir(), MaxBytes: 1 << 20, Retention: time.Hour,
	})
	if err != nil {
		t.Fatal(err)
	}
	return nativeRuntime{results: store, maxInlineBytes: maxInline}
}
