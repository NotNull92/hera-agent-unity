package policy

import "github.com/NotNull92/hera-agent-unity/internal/toolregistry"

type Assessment struct {
	RiskClass        string `json:"risk_class"`
	SideEffectScope  string `json:"side_effect_scope"`
	ReadOnly         bool   `json:"read_only"`
	Destructive      bool   `json:"destructive"`
	RequiresApproval bool   `json:"requires_approval"`
	Enforced         bool   `json:"enforced"`
}

func Assess(safety toolregistry.Safety) Assessment {
	return Assessment{
		RiskClass:        safety.RiskClass,
		SideEffectScope:  safety.SideEffectScope,
		ReadOnly:         safety.ReadOnly,
		Destructive:      safety.Destructive,
		RequiresApproval: safety.RequiresConfirmation,
		Enforced:         false,
	}
}
