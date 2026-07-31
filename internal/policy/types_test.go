package policy

import (
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestAssessReportsCatalogSafetyWithoutEnforcement(t *testing.T) {
	// Given
	safety := toolregistry.Safety{
		RiskClass:            "destructive",
		Destructive:          true,
		RequiresConfirmation: true,
		SideEffectScope:      "scene",
	}

	// When
	assessment := Assess(safety)

	// Then
	if assessment.RiskClass != "destructive" ||
		!assessment.Destructive ||
		!assessment.RequiresApproval ||
		assessment.Enforced {
		t.Fatalf("assessment=%#v", assessment)
	}
}
