package mcpserver

import (
	"errors"

	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func enforceNativePolicy(safety toolregistry.Safety, approved bool) *nativeToolError {
	if safety.RequiresConfirmation && approved {
		return nil
	}
	if err := policy.EnforceNative(safety); err != nil {
		if errors.Is(err, policy.ErrApprovalRequired) {
			return &nativeToolError{code: "APPROVAL_REQUIRED", message: "operation requires approval"}
		}
		return &nativeToolError{code: "POLICY_REJECTED", message: err.Error()}
	}
	return nil
}

type nativeToolError struct {
	code    string
	message string
	data    any
}
