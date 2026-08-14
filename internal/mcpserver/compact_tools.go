package mcpserver

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type compactSearchInput struct {
	Query   string `json:"query"`
	Profile string `json:"profile,omitempty"`
	Limit   int    `json:"limit,omitempty"`
}

type compactDescribeInput struct {
	Name   string `json:"name"`
	Action string `json:"action,omitempty"`
}

type compactCallInput struct {
	Name        string          `json:"name"`
	Arguments   json.RawMessage `json:"arguments"`
	OperationID string          `json:"operation_id,omitempty"`
}

func registerCompactTools(server *mcp.Server, config Config, runtime nativeRuntime) error {
	current, err := runtime.acquire()
	if err != nil {
		return err
	}
	if err := validateRuntime(config, current); err != nil {
		return err
	}
	server.AddTool(compactSearchTool(), compactSearchRuntimeHandler(runtime, config.AllowArbitraryCode))
	server.AddTool(compactDescribeTool(), compactDescribeRuntimeHandler(runtime, config.AllowArbitraryCode))
	server.AddTool(compactCallTool(), compactCallHandler(runtime, config.AllowArbitraryCode))
	return nil
}

func compactSearchRuntimeHandler(runtime nativeRuntime, allowArbitraryCode bool) mcp.ToolHandler {
	return func(ctx context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		current, result, err := acquireCatalogRuntime(runtime)
		if result != nil || err != nil {
			return result, err
		}
		return compactSearchHandler(current.snapshot.Catalog, allowArbitraryCode)(ctx, request)
	}
}

func compactDescribeRuntimeHandler(runtime nativeRuntime, allowArbitraryCode bool) mcp.ToolHandler {
	return func(ctx context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		current, result, err := acquireCatalogRuntime(runtime)
		if result != nil || err != nil {
			return result, err
		}
		return compactDescribeHandler(current.snapshot.Catalog, allowArbitraryCode)(ctx, request)
	}
}

func acquireCatalogRuntime(runtime nativeRuntime) (nativeRuntime, *mcp.CallToolResult, error) {
	current, err := runtime.acquire()
	if result, handled := catalogAvailabilityResult(err); handled {
		return nativeRuntime{}, result, nil
	}
	return current, nil, err
}

func compactSearchTool() *mcp.Tool {
	return &mcp.Tool{
		Name: "tool_search", Title: "Tool Search", Description: "Search the live Unity tool catalog deterministically",
		InputSchema:  json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"query":{"type":"string","minLength":1},"profile":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":100}},"required":["query"]}`),
		OutputSchema: envelopeSchema(json.RawMessage(`{"type":"array","items":{"type":"object"}}`)),
		Annotations:  &mcp.ToolAnnotations{Title: "Tool Search", ReadOnlyHint: true, DestructiveHint: boolPointer(false), IdempotentHint: true, OpenWorldHint: boolPointer(false)},
	}
}

func compactDescribeTool() *mcp.Tool {
	return &mcp.Tool{
		Name: "tool_describe", Title: "Tool Describe", Description: "Describe one live normalized Unity tool contract",
		InputSchema:  json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"name":{"type":"string","minLength":1},"action":{"type":"string","minLength":1}},"required":["name"]}`),
		OutputSchema: envelopeSchema(json.RawMessage(`{"type":"object"}`)),
		Annotations:  &mcp.ToolAnnotations{Title: "Tool Describe", ReadOnlyHint: true, DestructiveHint: boolPointer(false), IdempotentHint: true, OpenWorldHint: boolPointer(false)},
	}
}

func compactCallTool() *mcp.Tool {
	return &mcp.Tool{
		Name: "tool_call", Title: "Tool Call", Description: "Validate, authorize, and invoke one live Unity tool",
		InputSchema:  json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"name":{"type":"string","minLength":1},"arguments":{"type":"object"},"operation_id":{"type":"string","minLength":1}},"required":["name","arguments"]}`),
		OutputSchema: envelopeSchema(json.RawMessage(`{}`)),
		Annotations:  &mcp.ToolAnnotations{Title: "Tool Call", ReadOnlyHint: false, DestructiveHint: boolPointer(true), IdempotentHint: false, OpenWorldHint: boolPointer(true)},
	}
}

func compactSearchHandler(catalog *toolregistry.Catalog, allowArbitraryCode bool) mcp.ToolHandler {
	return func(_ context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		input, inputErr := decodeCompactInput[compactSearchInput](request)
		if inputErr != nil {
			return errorResult(inputErr.code, inputErr.message, inputErr.data), nil
		}
		if input.Query == "" {
			return errorResult("INVALID_ARGUMENT", "tool_search query is required", nil), nil
		}
		if input.Profile != "" && input.Profile != "compact" && !isSupportedProfile(input.Profile) {
			return errorResult("INVALID_ARGUMENT", "unsupported tool_search profile", nil), nil
		}
		limit := input.Limit
		if limit == 0 {
			limit = 5
		}
		if limit < 1 || limit > 100 {
			return errorResult("INVALID_ARGUMENT", "tool_search limit must be between 1 and 100", nil), nil
		}
		return dataResult(searchCatalog(catalog, catalogSearch{
			query: input.Query, profile: input.Profile, limit: limit,
			allowArbitraryCode: allowArbitraryCode,
		}))
	}
}

func compactDescribeHandler(catalog *toolregistry.Catalog, allowArbitraryCode bool) mcp.ToolHandler {
	return func(_ context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		input, inputErr := decodeCompactInput[compactDescribeInput](request)
		if inputErr != nil {
			return errorResult(inputErr.code, inputErr.message, inputErr.data), nil
		}
		tool, ok := findCatalogTool(catalog, input.Name)
		if !ok {
			return errorResult("TOOL_NOT_FOUND", fmt.Sprintf("tool %q was not found", input.Name), nil), nil
		}
		if toolregistry.ToolHasArbitraryCode(tool) && !allowArbitraryCode {
			return errorResult("TOOL_NOT_FOUND", fmt.Sprintf("tool %q was not found", input.Name), nil), nil
		}
		if input.Action == "" {
			return dataResult(describeToolOverview(catalog, tool))
		}
		action, ok := findCatalogAction(tool, input.Action)
		if !ok {
			return errorResult(
				"ACTION_NOT_FOUND",
				fmt.Sprintf("action %q was not found for tool %q", input.Action, tool.Name),
				map[string]any{"tool": tool.Name, "available_actions": catalogActionNames(tool)},
			), nil
		}
		return dataResult(compactActionDescription{
			Tool:   compactIdentity(tool),
			Action: action, ToolSafety: tool.Safety,
			CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch,
		})
	}
}

func compactCallHandler(runtime nativeRuntime, allowArbitraryCode bool) mcp.ToolHandler {
	return func(ctx context.Context, request *mcp.CallToolRequest) (*mcp.CallToolResult, error) {
		current, result, err := acquireCatalogRuntime(runtime)
		if result != nil || err != nil {
			return result, err
		}
		input, inputErr := decodeCompactInput[compactCallInput](request)
		if inputErr != nil {
			return errorResult(inputErr.code, inputErr.message, inputErr.data), nil
		}
		tool, ok := findCatalogTool(current.snapshot.Catalog, input.Name)
		if !ok {
			return errorResult("TOOL_NOT_FOUND", fmt.Sprintf("tool %q was not found", input.Name), nil), nil
		}
		if toolregistry.ToolHasArbitraryCode(tool) && !allowArbitraryCode {
			return errorResult("ARBITRARY_CODE_PERMISSION_REQUIRED", "arbitrary-code tool requires explicit server startup permission", nil), nil
		}
		params, paramsErr := decodeJSONObject(input.Arguments)
		if paramsErr != nil {
			return errorResult(paramsErr.code, paramsErr.message, paramsErr.data), nil
		}
		var operationID client.OperationID
		if input.OperationID != "" {
			var err error
			operationID, err = client.ParseOperationID(input.OperationID)
			if err != nil {
				return errorResult("INVALID_OPERATION_ID", err.Error(), nil), nil
			}
		}
		return invokeTool(ctx, current, toolInvocation{tool: tool, params: params, operationID: operationID, request: request})
	}
}

func decodeCompactInput[T any](request *mcp.CallToolRequest) (T, *nativeToolError) {
	var input T
	if request == nil || request.Params == nil {
		return input, &nativeToolError{code: "INVALID_ARGUMENT", message: "tool arguments are required"}
	}
	decoder := json.NewDecoder(bytes.NewReader(request.Params.Arguments))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&input); err != nil {
		return input, &nativeToolError{code: "INVALID_ARGUMENT", message: "invalid compact tool arguments", data: err.Error()}
	}
	if err := decoder.Decode(&struct{}{}); err != io.EOF {
		return input, &nativeToolError{code: "INVALID_ARGUMENT", message: "tool arguments contain trailing JSON"}
	}
	return input, nil
}

func decodeJSONObject(raw json.RawMessage) (map[string]any, *nativeToolError) {
	decoder := json.NewDecoder(bytes.NewReader(raw))
	decoder.UseNumber()
	var value map[string]any
	if err := decoder.Decode(&value); err != nil || value == nil {
		return nil, &nativeToolError{code: "INVALID_ARGUMENT", message: "arguments must be a JSON object"}
	}
	if err := decoder.Decode(&struct{}{}); err != io.EOF {
		return nil, &nativeToolError{code: "INVALID_ARGUMENT", message: "arguments contain trailing JSON"}
	}
	return value, nil
}

func findCatalogAction(tool toolregistry.Tool, name string) (toolregistry.Action, bool) {
	for _, action := range tool.Actions {
		if action.Name == name || slices.Contains(action.Aliases, name) {
			return action, true
		}
	}
	return toolregistry.Action{}, false
}

func catalogActionNames(tool toolregistry.Tool) []string {
	names := make([]string, len(tool.Actions))
	for index, action := range tool.Actions {
		names[index] = action.Name
	}
	return names
}

func findCatalogTool(catalog *toolregistry.Catalog, name string) (toolregistry.Tool, bool) {
	for _, tool := range catalog.Tools {
		if tool.Name == name || slices.Contains(tool.Aliases, name) {
			return tool, true
		}
	}
	return toolregistry.Tool{}, false
}
