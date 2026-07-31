package cmd

import (
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestResolveCallSafetyUsesMostSpecificParameterRule(t *testing.T) {
	// Given
	tool := toolregistry.Tool{
		Name: "console",
		Safety: toolregistry.Safety{
			RiskClass: "read_only",
			ReadOnly:  true,
			Rules: []toolregistry.SafetyRule{
				{
					Operation: "clear",
					When:      json.RawMessage(`{"clear":{"const":true}}`),
					Safety: toolregistry.Safety{
						RiskClass:            "destructive",
						Destructive:          true,
						RequiresConfirmation: true,
						SideEffectScope:      "editor_console",
					},
				},
			},
		},
	}

	// When
	_, safety, err := resolveCallSafety(tool, map[string]any{"clear": true})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if safety.RiskClass != "destructive" || !safety.RequiresConfirmation {
		t.Fatalf("safety=%#v", safety)
	}
}

func TestResolveCallSafetyFallsBackWhenRuleDoesNotMatch(t *testing.T) {
	// Given
	tool := toolregistry.Tool{
		Name: "exec",
		Safety: toolregistry.Safety{
			RiskClass: "arbitrary_code",
			Rules: []toolregistry.SafetyRule{
				{
					Operation: "compile_only",
					When:      json.RawMessage(`{"compile_only":{"const":true}}`),
					Safety: toolregistry.Safety{
						RiskClass: "read_only",
						ReadOnly:  true,
					},
				},
			},
		},
	}

	// When
	_, safety, err := resolveCallSafety(tool, map[string]any{"compile_only": false})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if safety.RiskClass != "arbitrary_code" || safety.ReadOnly {
		t.Fatalf("safety=%#v", safety)
	}
}
