package cmd

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"slices"
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
		if mismatched := approvalMismatches(claims, request, argumentsHash, operationID); len(mismatched) > 0 {
			response, responseErr := callPolicyResponse(
				"APPROVAL_MISMATCH",
				"approval token does not match this request: "+strings.Join(mismatched, ", "),
				map[string][]string{"mismatched": mismatched},
			)
			if response != nil && slices.Contains(mismatched, "arguments") {
				response.Suggestions = append(
					response.Suggestions,
					"Preflight and approve through the same command form. A bare command and "+
						"the same work sent through 'call' bind different argument objects, so a "+
						"token issued by one cannot approve the other.",
				)
			}
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

// approvalMismatches names every claim in the token that disagrees with the
// request being dispatched. A token binds to all of them at once, so naming the
// exact field turns an opaque rejection into an actionable one — most often
// "arguments", which is what a token issued through a different command form
// trips on.
func approvalMismatches(
	claims policy.ApprovalClaims,
	request callApprovalRequest,
	argumentsHash string,
	operationID client.OperationID,
) []string {
	var mismatched []string
	for _, claim := range []struct {
		name  string
		match bool
	}{
		{"tool", claims.Tool == request.tool.Name},
		{"action", claims.Action == request.action},
		{"arguments", claims.ArgumentsHash == argumentsHash},
		{"risk_class", claims.RiskClass == request.safety.RiskClass},
		{"project", claims.ProjectID == request.catalog.ProjectID},
		{"operation_id", operationID == "" || claims.OperationID == operationID},
	} {
		if !claim.match {
			mismatched = append(mismatched, claim.name)
		}
	}
	return mismatched
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
