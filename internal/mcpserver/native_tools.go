package mcpserver

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type toolInvocation struct {
	tool        toolregistry.Tool
	params      map[string]any
	operationID client.OperationID
	request     *mcp.CallToolRequest
}

func isSeedProfile(profile string) bool {
	return toolregistry.IsSeedProfile(profile)
}

func registerTools(server *mcp.Server, config Config, runtime nativeRuntime) error {
	if config.exposure() == ExposureCompact {
		return registerCompactTools(server, config, runtime)
	}
	return registerNativeTools(server, config, runtime)
}

func registerNativeTools(server *mcp.Server, config Config, runtime nativeRuntime) error {
	current, err := runtime.acquire()
	if err != nil {
		return err
	}
	if err := validateRuntime(config, current); err != nil {
		return err
	}
	profile := config.effectiveProfile()
	tools, err := current.snapshot.Catalog.ToolsForProfile(profile)
	if err != nil {
		return err
	}
	if profileMayMutate(tools) && !instanceHasFeature(current.instance, client.FeatureOperationLedgerV1) {
		return fmt.Errorf("profile %q contains mutations but Unity does not advertise %s", profile, client.FeatureOperationLedgerV1)
	}
	for index := range tools {
		tool := tools[index]
		server.AddTool(nativeMCPTool(tool), nativeToolHandler(runtime, tool.Name, profile))
	}
	return nil
}

func profileMayMutate(tools []toolregistry.Tool) bool {
	for _, tool := range tools {
		if !nativeAnnotations(tool).ReadOnlyHint {
			return true
		}
	}
	return false
}

func instanceHasFeature(instance *client.Instance, feature string) bool {
	for _, candidate := range instance.Features {
		if candidate == feature {
			return true
		}
	}
	return false
}

func nativeToolHandler(runtime nativeRuntime, toolName, profile string) mcp.ToolHandler {
	return func(ctx context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		current, err := runtime.acquire()
		if result, handled := catalogAvailabilityResult(err); handled {
			return result, nil
		}
		if err != nil {
			return nil, err
		}
		currentTool, ok := findProfileTool(current.snapshot.Catalog, toolName, profile)
		if !ok {
			return errorResult("TOOL_NOT_FOUND", fmt.Sprintf("tool %q was not found", toolName), nil), nil
		}
		params, inputErr := decodeToolArguments(request)
		if inputErr != nil {
			return errorResult("INVALID_ARGUMENT", inputErr.message, inputErr.data), nil
		}
		return invokeTool(ctx, current, toolInvocation{tool: currentTool, params: params, request: request})
	}
}

func findProfileTool(catalog *toolregistry.Catalog, name, profile string) (toolregistry.Tool, bool) {
	tools, err := catalog.ToolsForProfile(profile)
	if err != nil {
		return toolregistry.Tool{}, false
	}
	for _, tool := range tools {
		if tool.Name == name {
			return tool, true
		}
	}
	return toolregistry.Tool{}, false
}

func invokeTool(ctx context.Context, runtime nativeRuntime, invocation toolInvocation) (*mcp.CallToolResult, error) {
	tool := invocation.tool
	params := invocation.params
	if tool.ContractMode == toolregistry.ContractStrict {
		if runtime.snapshot.Schemas == nil {
			return nil, fmt.Errorf("strict schema cache is unavailable for %q", tool.Name)
		}
		if err := runtime.snapshot.Schemas.Validate(tool.Name+"/input", params); err != nil {
			return errorResult("INVALID_ARGUMENT", fmt.Sprintf("validate %s input: %v", tool.Name, err), nil), nil
		}
	}
	action, safety, err := policy.Resolve(tool, params)
	if err != nil {
		return errorResult("POLICY_RESOLUTION_FAILED", err.Error(), nil), nil
	}
	if invocation.operationID == "" {
		invocation.operationID, err = client.NewOperationID()
		if err != nil {
			return nil, fmt.Errorf("generate MCP operation id: %w", err)
		}
	}
	authorization, err := authorizeInvocation(ctx, runtime, invocation, action, safety)
	if err != nil {
		return nil, err
	}
	if authorization.result != nil {
		return authorization.result, nil
	}
	if authorization.operationID != "" {
		invocation.operationID = authorization.operationID
	}
	if policyErr := enforceNativePolicy(safety, authorization.token != ""); policyErr != nil {
		return errorResult(policyErr.code, policyErr.message, policyErr.data), nil
	}
	if !safety.ReadOnly && !instanceHasFeature(runtime.instance, client.FeatureOperationLedgerV1) {
		return errorResult("OPERATION_LEDGER_REQUIRED", "mutation requires Unity operation ledger support", nil), nil
	}
	response, err := runtime.sender.SendWithOptions(
		ctx,
		runtime.instance,
		tool.Name,
		params,
		runtime.timeout,
		client.SendOptions{
			OperationID:   invocation.operationID,
			ApprovalToken: authorization.token,
			Idempotent:    safety.Idempotent,
			ClientKind:    "mcp",
			CatalogHash:   runtime.snapshot.Catalog.CatalogHash,
		},
	)
	if err != nil {
		var unknown *client.OperationOutcomeUnknownError
		if errors.As(err, &unknown) {
			return errorResult(unknown.Code, unknown.Error(), map[string]any{
				"operation_id": string(unknown.OperationID),
				"tool":         unknown.Command,
				"project":      unknown.Project,
				"port":         unknown.Port,
			}), nil
		}
		return nil, fmt.Errorf("invoke Unity tool %q: %w", tool.Name, err)
	}
	if response == nil {
		return nil, fmt.Errorf("invoke Unity tool %q: empty response", tool.Name)
	}
	start, taskable, err := taskStart(tool.Name, params, response, string(invocation.operationID))
	if err != nil {
		return nil, err
	}
	if taskable {
		if runtime.tasks == nil {
			return nil, fmt.Errorf("MCP task bridge is unavailable")
		}
		task, createErr := runtime.tasks.Create(start)
		if createErr != nil {
			return nil, fmt.Errorf("create durable MCP task: %w", createErr)
		}
		if runtime.taskMode && supportsTasks(invocation.request) {
			result := boundedCommandResult(runtime, invocation, response)
			result.Meta = mcp.Meta{taskMarkerMeta: task.ID}
			return result, nil
		}
		resultPath, pathErr := runtime.tasks.ResultPath(task.ID)
		if pathErr != nil {
			return nil, pathErr
		}
		waitTimeout := time.Duration(runtime.timeout) * time.Millisecond
		if start.Kind == taskbridge.KindPackage {
			waitTimeout = 10 * time.Minute
		}
		response, err = poll.WaitForAsyncJob(ctx, resultPath, start.Port, waitTimeout, string(start.Kind)+" task")
		if err != nil {
			return nil, err
		}
	}
	return boundedCommandResult(runtime, invocation, response), nil
}

func decodeToolArguments(request *mcp.CallToolRequest) (map[string]any, *nativeToolError) {
	if request == nil || request.Params == nil {
		return nil, &nativeToolError{code: "INVALID_ARGUMENT", message: "tool arguments are required"}
	}
	raw := request.Params.Arguments
	if len(bytes.TrimSpace(raw)) == 0 {
		raw = json.RawMessage(`{}`)
	}
	decoder := json.NewDecoder(bytes.NewReader(raw))
	decoder.UseNumber()
	var params map[string]any
	if err := decoder.Decode(&params); err != nil || params == nil {
		return nil, &nativeToolError{code: "INVALID_ARGUMENT", message: "tool arguments must be a JSON object"}
	}
	if err := decoder.Decode(&struct{}{}); err != io.EOF {
		return nil, &nativeToolError{code: "INVALID_ARGUMENT", message: "tool arguments contain trailing JSON"}
	}
	return params, nil
}
