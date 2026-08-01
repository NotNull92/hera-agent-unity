package mcpserver

import (
	"context"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const approvalMetadataKey = "hera/approval_token"

type invocationAuthorization struct {
	token       string
	operationID client.OperationID
	result      *mcp.CallToolResult
}

func authorizeInvocation(
	ctx context.Context,
	runtime nativeRuntime,
	invocation toolInvocation,
	action string,
	safety toolregistry.Safety,
) (invocationAuthorization, error) {
	if !safety.RequiresConfirmation {
		return invocationAuthorization{}, nil
	}
	if !instanceHasFeature(runtime.instance, client.FeatureApprovalV1) {
		return invocationAuthorization{result: errorResult(
			"APPROVAL_UNSUPPORTED", "Unity Connector does not advertise approval_v1", nil,
		)}, nil
	}
	if denied := approvalDenied(invocation.request); denied {
		return invocationAuthorization{result: errorResult("APPROVAL_DENIED", "operation was not approved", nil)}, nil
	}
	if token := approvalTokenFromRequest(invocation.request); token != "" {
		claims, err := policy.InspectApprovalToken(token)
		if err != nil {
			return invocationAuthorization{result: errorResult("INVALID_APPROVAL_TOKEN", err.Error(), nil)}, nil
		}
		return invocationAuthorization{token: token, operationID: claims.OperationID}, nil
	}
	if runtime.approver == nil {
		return invocationAuthorization{}, fmt.Errorf("approval preflight runtime is unavailable")
	}
	preflight, err := runtime.approver.PreflightApproval(ctx, runtime.instance, client.ApprovalPreflightRequest{
		Command:     invocation.tool.Name,
		Action:      action,
		Params:      invocation.params,
		OperationID: invocation.operationID,
		TimeoutMS:   runtime.timeout,
	})
	if err != nil {
		return invocationAuthorization{}, fmt.Errorf("preflight approval for %q: %w", invocation.tool.Name, err)
	}
	if runtime.mrtr && supportsFormElicitation(invocation.request) {
		return invocationAuthorization{result: &mcp.CallToolResult{
			InputRequests: mcp.InputRequestMap{"approval": &mcp.ElicitParams{Message: approvalMessage(preflight.Summary)}},
			RequestState:  preflight.Token,
		}}, nil
	}
	return invocationAuthorization{result: errorResult("APPROVAL_REQUIRED", "operation requires approval", preflight)}, nil
}

func approvalDenied(request *mcp.CallToolRequest) bool {
	if request == nil || request.Params == nil {
		return false
	}
	response, ok := request.Params.InputResponses["approval"].(*mcp.ElicitResult)
	return ok && response.Action != "accept"
}

func approvalTokenFromRequest(request *mcp.CallToolRequest) string {
	if request == nil || request.Params == nil {
		return ""
	}
	if response, ok := request.Params.InputResponses["approval"].(*mcp.ElicitResult); ok {
		if response.Action == "accept" {
			return request.Params.RequestState
		}
		return ""
	}
	if token, ok := request.Params.Meta[approvalMetadataKey].(string); ok {
		return token
	}
	return ""
}

func supportsFormElicitation(request *mcp.CallToolRequest) bool {
	if request == nil {
		return false
	}
	capabilities := request.ClientCapabilities()
	return capabilities != nil && capabilities.Elicitation != nil && capabilities.Elicitation.Form != nil
}

func approvalMessage(summary client.ApprovalSummary) string {
	return fmt.Sprintf(
		"Approve %s/%s; target=%s; side_effect=%s; reversible=%t; domain_reload=%t; external_or_package=%t; operation_id=%s",
		summary.Tool, summary.Action, summary.Target, summary.SideEffect, summary.Reversible,
		summary.MayReloadDomain, summary.ExternalImpact, summary.OperationID,
	)
}
