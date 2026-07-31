package cmd

import (
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func resolveCallSafety(
	tool toolregistry.Tool,
	params map[string]any,
) (string, toolregistry.Safety, error) {
	return policy.Resolve(tool, params)
}
