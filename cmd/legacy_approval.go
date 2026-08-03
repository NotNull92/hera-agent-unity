package cmd

import (
	"fmt"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type legacyApprovalOptions struct {
	token         string
	preflight     callPreflightFunc
	sendOperation callSendFunc
	resolveAction func(string, map[string]any) (string, error)
	confirm       func(client.ApprovalSummary) (bool, error)
	interactive   bool
}

func withLegacyApproval(send SendFunc, options legacyApprovalOptions) SendFunc {
	return func(command string, rawParams interface{}) (*client.CommandResponse, error) {
		params, ok := rawParams.(map[string]any)
		if !ok {
			return nil, fmt.Errorf("prepare legacy approval for %q: parameters must be an object", command)
		}
		if options.token != "" {
			if options.sendOperation == nil {
				return nil, fmt.Errorf("approved operation sender is unavailable")
			}
			claims, err := policy.InspectApprovalToken(options.token)
			if err != nil {
				return nil, err
			}
			return options.sendOperation(command, params, client.SendOptions{
				OperationID:   claims.OperationID,
				ApprovalToken: options.token,
			})
		}

		response, err := send(command, params)
		if err != nil || response == nil || response.Code != "APPROVAL_REQUIRED" {
			return response, err
		}
		if options.preflight == nil {
			return callPolicyResponse(
				"APPROVAL_UNSUPPORTED",
				"Unity Connector does not advertise approval_v1",
				nil,
			)
		}
		action := legacyAction(params)
		if options.resolveAction != nil {
			action, err = options.resolveAction(command, params)
			if err != nil {
				return nil, err
			}
		}
		preflight, err := options.preflight(client.ApprovalPreflightRequest{
			Command: command,
			Action:  action,
			Params:  params,
		})
		if err != nil {
			return nil, err
		}
		if !options.interactive {
			return callPolicyResponse("APPROVAL_REQUIRED", "operation requires approval", preflight)
		}
		if options.confirm == nil {
			return nil, fmt.Errorf("interactive approval prompt is unavailable")
		}
		approved, err := options.confirm(preflight.Summary)
		if err != nil {
			return nil, err
		}
		if !approved {
			return callPolicyResponse("APPROVAL_DENIED", "operation was not approved", preflight.Summary)
		}
		if options.sendOperation == nil {
			return nil, fmt.Errorf("approved operation sender is unavailable")
		}
		return options.sendOperation(command, params, client.SendOptions{
			OperationID:   preflight.OperationID,
			ApprovalToken: preflight.Token,
		})
	}
}

func extractLegacyApproval(args []string) ([]string, string, error) {
	remaining := make([]string, 0, len(args))
	var token string
	for index := 0; index < len(args); index++ {
		if args[index] != "--approve" {
			remaining = append(remaining, args[index])
			continue
		}
		if token != "" {
			return nil, "", fmt.Errorf("--approve may be supplied only once")
		}
		if index+1 >= len(args) || args[index+1] == "" {
			return nil, "", fmt.Errorf("--approve requires a preflight token")
		}
		token = args[index+1]
		index++
	}
	return remaining, token, nil
}

func legacyAction(params map[string]any) string {
	if action, ok := params["action"].(string); ok {
		return action
	}
	return ""
}

func resolveLegacyAction(tool toolregistry.Tool, params map[string]any) string {
	candidate := legacyAction(params)
	if candidate == "" {
		if args, ok := params["args"].([]string); ok && len(args) > 0 {
			candidate = args[0]
		}
	}
	for _, action := range tool.Actions {
		if action.Name == candidate || slices.Contains(action.Aliases, candidate) {
			return action.Name
		}
	}
	return ""
}
