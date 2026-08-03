package cmd

import (
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// runLegacyToolCommand is the compatibility boundary for the original dynamic
// CLI syntax. Strict `call`, specialized commands, approval, and transport stay
// outside this adapter. Custom [HeraTool] names intentionally flow through the
// generic branch without a generated Go subcommand.
func runLegacyToolCommand(category string, args []string, send SendFunc) (*client.CommandResponse, error) {
	if category == "exec" {
		var err error
		args, err = readExecFileIfPresent(args)
		if err != nil {
			return nil, err
		}
		args = readStdinIfPiped(args)
	}

	params, _, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}
	if category == "exec" {
		if check, ok := params["check"].(bool); ok && check {
			params["compile_only"] = true
			delete(params, "check")
		}
	}
	request := newToolRequest(category, params)
	response, err := send(request.Command, request.Params)
	if err != nil {
		return nil, fmt.Errorf("invoke legacy tool %q: %w", category, err)
	}
	return response, nil
}
