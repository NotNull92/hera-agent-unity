package cmd

import (
	"encoding/base64"
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestLegacyApprovalReturnsContinuablePreflightWhenNonInteractive(t *testing.T) {
	// Given
	baseCalls := 0
	operationCalls := 0
	preflight := testApprovalPreflight()
	send := withLegacyApproval(
		func(string, interface{}) (*client.CommandResponse, error) {
			baseCalls++
			return &client.CommandResponse{Success: false, Code: "APPROVAL_REQUIRED"}, nil
		},
		legacyApprovalOptions{
			preflight: func(client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error) {
				return preflight, nil
			},
			sendOperation: func(string, map[string]any, client.SendOptions) (*client.CommandResponse, error) {
				operationCalls++
				return &client.CommandResponse{Success: true}, nil
			},
		},
	)

	// When
	response, err := send("ui_doc", map[string]any{"action": "capture", "overwrite": true})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if response.Code != "APPROVAL_REQUIRED" || baseCalls != 1 || operationCalls != 0 {
		t.Fatalf("response=%#v base calls=%d operation calls=%d", response, baseCalls, operationCalls)
	}
	var returned client.ApprovalPreflight
	if err := json.Unmarshal(response.Data, &returned); err != nil {
		t.Fatal(err)
	}
	if returned.Token != preflight.Token || returned.OperationID != preflight.OperationID {
		t.Fatalf("preflight=%#v", returned)
	}
}

func TestResolveLegacyActionDistinguishesDefaultArgumentFromNamedAction(t *testing.T) {
	// Given
	menu := toolregistry.Tool{Actions: []toolregistry.Action{{Name: "list"}}}
	scene := toolregistry.Tool{Actions: []toolregistry.Action{{Name: "info"}, {Name: "close"}}}

	// When
	menuAction := resolveLegacyAction(menu, map[string]any{
		"args": []string{"HeraAgent/Tests/UiDocApply"},
	})
	sceneAction := resolveLegacyAction(scene, map[string]any{
		"args": []string{"info"},
	})

	// Then
	if menuAction != "" || sceneAction != "info" {
		t.Fatalf("menu action=%q scene action=%q", menuAction, sceneAction)
	}
}

func TestLegacyApprovalTokenDispatchesExactlyOnce(t *testing.T) {
	// Given
	baseCalls := 0
	operationCalls := 0
	claims, err := json.Marshal(policy.ApprovalClaims{
		Version: 1, OperationID: "op_legacy_approved", Tool: "exec",
		ArgumentsHash: "sha256:test", RiskClass: "arbitrary_code",
		ProjectID: "project", ExpiresAtMS: 4_102_444_800_000, SingleUse: true,
	})
	if err != nil {
		t.Fatal(err)
	}
	token := base64.RawURLEncoding.EncodeToString(claims) + ".signature"
	var sentOptions client.SendOptions
	send := withLegacyApproval(
		func(string, interface{}) (*client.CommandResponse, error) {
			baseCalls++
			return &client.CommandResponse{Success: false, Code: "APPROVAL_REQUIRED"}, nil
		},
		legacyApprovalOptions{
			token: token,
			sendOperation: func(_ string, _ map[string]any, options client.SendOptions) (*client.CommandResponse, error) {
				operationCalls++
				sentOptions = options
				return &client.CommandResponse{Success: true}, nil
			},
		},
	)

	// When
	response, err := send("exec", map[string]any{"code": "return null;"})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !response.Success || baseCalls != 0 || operationCalls != 1 ||
		sentOptions.ApprovalToken != token || sentOptions.OperationID != "op_legacy_approved" {
		t.Fatalf("response=%#v base=%d operation=%d options=%#v", response, baseCalls, operationCalls, sentOptions)
	}
}

func TestExtractLegacyApprovalRemovesTokenFromToolArguments(t *testing.T) {
	// Given
	args := []string{"capture", "--out", "capture.png", "--approve", "signed-token"}

	// When
	remaining, token, err := extractLegacyApproval(args)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if token != "signed-token" || len(remaining) != 3 || remaining[2] != "capture.png" {
		t.Fatalf("remaining=%#v token=%q", remaining, token)
	}
}
