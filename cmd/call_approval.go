package cmd

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
)

func (command *callCommand) resolveApproval(
	request callApprovalRequest,
) (string, client.OperationID, *client.CommandResponse, error) {
	operationID, err := parseOptionalOperationID(request.options.OperationID)
	if err != nil {
		return "", "", nil, err
	}
	if !request.safety.RequiresConfirmation {
		return "", operationID, nil, nil
	}
	if !instanceSupports(request.instance, client.FeatureApprovalV1) {
		response, responseErr := callPolicyResponse("APPROVAL_UNSUPPORTED", "Unity Connector does not advertise approval_v1", nil)
		return "", "", response, responseErr
	}
	if request.options.ApprovalToken != "" {
		claims, inspectErr := policy.InspectApprovalToken(request.options.ApprovalToken)
		if inspectErr != nil {
			return "", "", nil, inspectErr
		}
		argumentsHash, hashErr := client.ArgumentsHash(request.params)
		if hashErr != nil {
			return "", "", nil, hashErr
		}
		if claims.Tool != request.tool.Name || claims.Action != request.action || claims.ArgumentsHash != argumentsHash ||
			claims.RiskClass != request.safety.RiskClass || claims.ProjectID != request.catalog.ProjectID ||
			(operationID != "" && claims.OperationID != operationID) {
			response, responseErr := callPolicyResponse("APPROVAL_MISMATCH", "approval token does not match this request", nil)
			return "", "", response, responseErr
		}
		return request.options.ApprovalToken, claims.OperationID, nil, nil
	}
	if command.preflight == nil {
		return "", "", nil, fmt.Errorf("approval preflight is unavailable")
	}
	preflight, err := command.preflight(client.ApprovalPreflightRequest{
		Command:     request.tool.Name,
		Action:      request.action,
		Params:      request.params,
		OperationID: operationID,
	})
	if err != nil {
		return "", "", nil, err
	}
	if !command.interactive {
		response, responseErr := callPolicyResponse("APPROVAL_REQUIRED", "operation requires approval", preflight)
		return "", "", response, responseErr
	}
	if command.confirm == nil {
		return "", "", nil, fmt.Errorf("interactive approval prompt is unavailable")
	}
	approved, err := command.confirm(preflight.Summary)
	if err != nil {
		return "", "", nil, err
	}
	if !approved {
		response, responseErr := callPolicyResponse("APPROVAL_DENIED", "operation was not approved", preflight.Summary)
		return "", "", response, responseErr
	}
	return preflight.Token, preflight.OperationID, nil, nil
}

func parseOptionalOperationID(value string) (client.OperationID, error) {
	if value == "" {
		return "", nil
	}
	return client.ParseOperationID(value)
}

func instanceSupports(instance *client.Instance, feature string) bool {
	for _, candidate := range instance.Features {
		if candidate == feature {
			return true
		}
	}
	return false
}

func callPolicyResponse(code, message string, data any) (*client.CommandResponse, error) {
	encoded, err := json.Marshal(data)
	if err != nil {
		return nil, fmt.Errorf("encode approval response: %w", err)
	}
	return &client.CommandResponse{Success: false, Code: code, Message: message, Data: encoded}, nil
}

func promptCallApproval(input io.Reader, output io.Writer, summary client.ApprovalSummary) (bool, error) {
	if _, err := fmt.Fprintf(output, "Approval required\nTool/action: %s/%s\nTarget: %s\nSide effect: %s\nReversible: %t\nDomain reload: %t\nExternal/package impact: %t\nOperation ID: %s\nApprove? [y/N] ",
		summary.Tool, summary.Action, summary.Target, summary.SideEffect, summary.Reversible,
		summary.MayReloadDomain, summary.ExternalImpact, summary.OperationID); err != nil {
		return false, fmt.Errorf("write approval prompt: %w", err)
	}
	answer, err := bufio.NewReader(input).ReadString('\n')
	if err != nil && err != io.EOF {
		return false, fmt.Errorf("read approval response: %w", err)
	}
	return strings.EqualFold(strings.TrimSpace(answer), "y") || strings.EqualFold(strings.TrimSpace(answer), "yes"), nil
}
