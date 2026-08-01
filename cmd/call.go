package cmd

import (
	"context"
	"encoding/json"
	"fmt"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type callLoadFunc func(context.Context, *client.Instance) (*toolregistry.Snapshot, error)
type callSendFunc func(string, map[string]any, client.SendOptions) (*client.CommandResponse, error)
type callPreflightFunc func(client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error)

type callCommand struct {
	load          callLoadFunc
	send          SendFunc
	sendOperation callSendFunc
	preflight     callPreflightFunc
	confirm       func(client.ApprovalSummary) (bool, error)
	interactive   bool
	input         callInput
}

type callApprovalRequest struct {
	instance *client.Instance
	options  callOptions
	catalog  *toolregistry.Catalog
	tool     toolregistry.Tool
	action   string
	safety   toolregistry.Safety
	params   map[string]any
}

type toolRequest struct {
	Command string         `json:"command"`
	Params  map[string]any `json:"params"`
}

type callValidationResult struct {
	Valid  bool   `json:"valid"`
	Tool   string `json:"tool"`
	Action string `json:"action,omitempty"`
}

type callExplanation struct {
	Valid        bool                `json:"valid"`
	Tool         string              `json:"tool"`
	Action       string              `json:"action,omitempty"`
	Profile      string              `json:"profile,omitempty"`
	ContractMode string              `json:"contract_mode"`
	Safety       toolregistry.Safety `json:"safety"`
	Policy       policy.Assessment   `json:"policy"`
}

func newToolRequest(command string, params map[string]any) toolRequest {
	cloned := make(map[string]any, len(params))
	for key, value := range params {
		cloned[key] = value
	}
	return toolRequest{Command: command, Params: cloned}
}

func (command *callCommand) Run(
	ctx context.Context,
	instance *client.Instance,
	args []string,
) (*client.CommandResponse, error) {
	options, err := parseCallOptions(args)
	if err != nil {
		return nil, err
	}
	params, err := options.readParams(command.input)
	if err != nil {
		return nil, err
	}
	snapshot, err := command.load(ctx, instance)
	if err != nil {
		return nil, fmt.Errorf("load tool registry: %w", err)
	}
	tool, err := resolveCallTool(snapshot.Catalog, options.Tool, options.Profile)
	if err != nil {
		return nil, err
	}
	if snapshot.Schemas == nil || tool.ContractMode != toolregistry.ContractStrict {
		return nil, fmt.Errorf("tool %q does not provide a strict contract", tool.Name)
	}
	if err := snapshot.Schemas.Validate(tool.Name+"/input", params); err != nil {
		return nil, fmt.Errorf("validate call %q: %w", tool.Name, err)
	}

	action, safety, err := resolveCallSafety(tool, params)
	if err != nil {
		return nil, fmt.Errorf("resolve call safety %q: %w", tool.Name, err)
	}
	if options.Explain {
		return callDataResponse("Call explanation", callExplanation{
			Valid:        true,
			Tool:         tool.Name,
			Action:       action,
			Profile:      options.Profile,
			ContractMode: tool.ContractMode,
			Safety:       safety,
			Policy:       policy.Assess(safety),
		})
	}
	if options.ValidateOnly {
		return callDataResponse("Call is valid", callValidationResult{
			Valid:  true,
			Tool:   tool.Name,
			Action: action,
		})
	}
	approvalToken, operationID, approvalResponse, err := command.resolveApproval(callApprovalRequest{
		instance: instance,
		options:  options,
		catalog:  snapshot.Catalog,
		tool:     tool,
		action:   action,
		safety:   safety,
		params:   params,
	})
	if err != nil {
		return nil, err
	}
	if approvalResponse != nil {
		return approvalResponse, nil
	}
	request := newToolRequest(tool.Name, params)
	if command.sendOperation != nil {
		return command.sendOperation(request.Command, request.Params, client.SendOptions{
			OperationID:   operationID,
			ApprovalToken: approvalToken,
			Idempotent:    safety.Idempotent,
			CatalogHash:   snapshot.Catalog.CatalogHash,
		})
	}
	return command.send(request.Command, request.Params)
}

func resolveCallTool(
	catalog *toolregistry.Catalog,
	name string,
	profile string,
) (toolregistry.Tool, error) {
	if catalog == nil {
		return toolregistry.Tool{}, fmt.Errorf("tool catalog is unavailable")
	}
	for _, tool := range catalog.Tools {
		if tool.Name != name && !slices.Contains(tool.Aliases, name) {
			continue
		}
		if profile != "" && !slices.Contains(tool.Profiles, profile) {
			return toolregistry.Tool{}, fmt.Errorf(
				"tool %q is not available in profile %q",
				tool.Name,
				profile,
			)
		}
		return tool, nil
	}
	return toolregistry.Tool{}, fmt.Errorf("unknown tool %q", name)
}

func callDataResponse(message string, value any) (*client.CommandResponse, error) {
	data, err := json.Marshal(value)
	if err != nil {
		return nil, fmt.Errorf("encode call result: %w", err)
	}
	return &client.CommandResponse{
		Success: true,
		Message: message,
		Data:    data,
	}, nil
}
