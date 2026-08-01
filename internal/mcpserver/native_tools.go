package mcpserver

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type toolSender interface {
	SendWithOptions(context.Context, *client.Instance, string, any, int, client.SendOptions) (*client.CommandResponse, error)
}

type nativeRuntime struct {
	instance *client.Instance
	snapshot *toolregistry.Snapshot
	sender   toolSender
	timeout  int
}

type toolInvocation struct {
	tool        toolregistry.Tool
	params      map[string]any
	operationID client.OperationID
}

func isSeedProfile(profile string) bool {
	return toolregistry.IsSeedProfile(profile)
}

func prepareNativeRuntime(ctx context.Context, config Config) (nativeRuntime, error) {
	instance, err := client.DiscoverInstanceFresh(config.Project, config.Port)
	if err != nil {
		return nativeRuntime{}, fmt.Errorf("discover Unity for MCP startup: %w", err)
	}
	registry := toolregistry.NewRegistry(toolregistry.RegistryOptions{})
	snapshot, err := registry.Load(ctx, instance)
	if err != nil {
		return nativeRuntime{}, fmt.Errorf("load native tool catalog for MCP startup: %w", err)
	}
	if config.exposure() != ExposureCompact && (snapshot.Exposure != toolregistry.ExposureProfile || snapshot.Schemas == nil) {
		return nativeRuntime{}, fmt.Errorf("native strict tool catalog is required for MCP profile exposure")
	}
	return nativeRuntime{
		instance: instance,
		snapshot: snapshot,
		sender:   client.DefaultClient,
		timeout:  config.TimeoutMS,
	}, nil
}

func registerTools(server *mcp.Server, config Config, runtime nativeRuntime) error {
	if config.exposure() == ExposureCompact {
		return registerCompactTools(server, config, runtime)
	}
	return registerNativeTools(server, config, runtime)
}

func registerNativeTools(server *mcp.Server, config Config, runtime nativeRuntime) error {
	if runtime.instance == nil || runtime.snapshot == nil || runtime.snapshot.Catalog == nil || runtime.snapshot.Schemas == nil || runtime.sender == nil {
		return fmt.Errorf("native MCP runtime is incomplete")
	}
	profile := config.effectiveProfile()
	tools, err := runtime.snapshot.Catalog.ToolsForProfile(profile)
	if err != nil {
		return err
	}
	if profileMayMutate(tools) && !instanceHasFeature(runtime.instance, client.FeatureOperationLedgerV1) {
		return fmt.Errorf("profile %q contains mutations but Unity does not advertise %s", profile, client.FeatureOperationLedgerV1)
	}
	for index := range tools {
		tool := tools[index]
		server.AddTool(nativeMCPTool(tool), nativeToolHandler(runtime, tool))
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

func nativeMCPTool(tool toolregistry.Tool) *mcp.Tool {
	return &mcp.Tool{
		Name:         tool.Name,
		Title:        tool.Title,
		Description:  tool.Description,
		InputSchema:  json.RawMessage(tool.InputSchema),
		OutputSchema: envelopeSchema(tool.OutputSchema),
		Annotations:  nativeAnnotations(tool),
	}
}

func nativeToolHandler(runtime nativeRuntime, tool toolregistry.Tool) mcp.ToolHandler {
	return func(ctx context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		params, inputErr := decodeToolArguments(request)
		if inputErr != nil {
			return errorResult("INVALID_ARGUMENT", inputErr.message, inputErr.data), nil
		}
		return invokeTool(ctx, runtime, toolInvocation{tool: tool, params: params})
	}
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
	_, safety, err := policy.Resolve(tool, params)
	if err != nil {
		return errorResult("POLICY_RESOLUTION_FAILED", err.Error(), nil), nil
	}
	if policyErr := enforceNativePolicy(safety); policyErr != nil {
		return errorResult(policyErr.code, policyErr.message, policyErr.data), nil
	}
	if !safety.ReadOnly && !instanceHasFeature(runtime.instance, client.FeatureOperationLedgerV1) {
		return errorResult("OPERATION_LEDGER_REQUIRED", "mutation requires Unity operation ledger support", nil), nil
	}
	if invocation.operationID == "" {
		invocation.operationID, err = client.NewOperationID()
		if err != nil {
			return nil, fmt.Errorf("generate MCP operation id: %w", err)
		}
	}
	response, err := runtime.sender.SendWithOptions(
		ctx,
		runtime.instance,
		tool.Name,
		params,
		runtime.timeout,
		client.SendOptions{
			OperationID: invocation.operationID,
			Idempotent:  safety.Idempotent,
			ClientKind:  "mcp",
			CatalogHash: runtime.snapshot.Catalog.CatalogHash,
		},
	)
	if err != nil {
		return nil, fmt.Errorf("invoke Unity tool %q: %w", tool.Name, err)
	}
	if response == nil {
		return nil, fmt.Errorf("invoke Unity tool %q: empty response", tool.Name)
	}
	return commandResult(response), nil
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

func nativeAnnotations(tool toolregistry.Tool) *mcp.ToolAnnotations {
	safeties := flattenSafety(tool.Safety)
	for _, action := range tool.Actions {
		safeties = append(safeties, flattenSafety(action.Safety)...)
	}
	readOnly, idempotent := true, true
	destructive, openWorld := false, false
	for _, safety := range safeties {
		readOnly = readOnly && safety.ReadOnly
		idempotent = idempotent && safety.Idempotent
		destructive = destructive || safety.Destructive
		risk := strings.ToLower(safety.RiskClass)
		openWorld = openWorld || strings.Contains(risk, "package") || strings.Contains(risk, "external") || strings.Contains(risk, "network") || strings.Contains(risk, "arbitrary")
	}
	return &mcp.ToolAnnotations{
		Title:           tool.Title,
		ReadOnlyHint:    readOnly,
		DestructiveHint: boolPointer(destructive),
		IdempotentHint:  idempotent,
		OpenWorldHint:   boolPointer(openWorld),
	}
}

func flattenSafety(safety toolregistry.Safety) []toolregistry.Safety {
	flattened := []toolregistry.Safety{safety}
	for _, rule := range safety.Rules {
		flattened = append(flattened, flattenSafety(rule.Safety)...)
	}
	return flattened
}

func boolPointer(value bool) *bool { return &value }

func envelopeSchema(dataSchema json.RawMessage) map[string]any {
	var data any
	if json.Unmarshal(dataSchema, &data) != nil {
		data = map[string]any{}
	}
	return map[string]any{
		"type":                 "object",
		"additionalProperties": false,
		"required":             []string{"success", "message"},
		"properties": map[string]any{
			"success":     map[string]any{"type": "boolean"},
			"message":     map[string]any{"type": "string"},
			"code":        map[string]any{"type": "string"},
			"suggestions": map[string]any{"type": "array", "items": map[string]any{"type": "string"}},
			"agent_hint":  map[string]any{"type": "string"},
			"data":        data,
			"timings":     map[string]any{"type": "object", "additionalProperties": map[string]any{"type": "integer"}},
		},
	}
}
