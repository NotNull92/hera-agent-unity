package policy

import (
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestResolveUsesMostSpecificParameterRule(t *testing.T) {
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

	_, safety, err := Resolve(tool, map[string]any{"clear": true})

	if err != nil {
		t.Fatal(err)
	}
	if safety.RiskClass != "destructive" || !safety.RequiresConfirmation {
		t.Fatalf("safety=%#v", safety)
	}
}

func TestResolveFallsBackWhenRuleDoesNotMatch(t *testing.T) {
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

	_, safety, err := Resolve(tool, map[string]any{"compile_only": false})

	if err != nil {
		t.Fatal(err)
	}
	if safety.RiskClass != "arbitrary_code" || safety.ReadOnly {
		t.Fatalf("safety=%#v", safety)
	}
}
