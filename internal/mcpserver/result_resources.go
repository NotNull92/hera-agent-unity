package mcpserver

import (
	"context"
	"encoding/json"
	"errors"
	"strings"
	"unicode"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/policy"
	"github.com/NotNull92/hera-agent-unity/internal/resultstore"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const resultResourceTemplate = "hera-result://cache/{project_id}/{operation_id}/{result_hash}"

func boundedCommandResult(runtime nativeRuntime, invocation toolInvocation, response *client.CommandResponse) *mcp.CallToolResult {
	if runtime.results == nil || runtime.maxInlineBytes <= 0 {
		return commandResult(response)
	}
	full, err := json.Marshal(responseEnvelope(response))
	if err != nil {
		return unavailableResult("encoding_failed", "The Unity result could not be encoded.", 0)
	}
	inline, err := json.Marshal(commandResult(response))
	if err != nil {
		return unavailableResult("encoding_failed", "The Unity result could not be encoded.", len(full))
	}
	if len(inline) <= runtime.maxInlineBytes {
		return commandResult(response)
	}
	_, safety, safetyErr := policy.Resolve(invocation.tool, invocation.params)
	if safetyErr != nil || safety.RiskClass == "arbitrary_code" || containsSensitiveResult(full) {
		return unavailableResult("sensitive_result", "Oversized result was withheld because it may contain sensitive data.", len(full))
	}
	handle, err := runtime.results.Spool(string(invocation.operationID), full)
	if err != nil {
		return unavailableResult("storage_failed", "Oversized result could not be stored.", len(full))
	}
	size := handle.Bytes
	structured := map[string]any{
		"success":    response.Success,
		"code":       "RESULT_SPOOLED",
		"message":    "Result exceeded the inline limit and is available as an MCP resource.",
		"byte_size":  handle.Bytes,
		"sha256":     handle.Hash,
		"truncated":  true,
		"resource":   map[string]any{"uri": handle.URI, "mime_type": "application/json"},
		"agent_hint": "Read the resource handle, or call the tool again with supported projection controls such as limit, offset/cursor, fields, IDs-only, names-only, stacktrace mode, or depth.",
	}
	if response.Code != "" {
		structured["unity_code"] = response.Code
	}
	return &mcp.CallToolResult{
		Content: []mcp.Content{
			&mcp.TextContent{Text: "Result exceeded the inline limit; use the attached resource handle."},
			&mcp.ResourceLink{URI: handle.URI, Name: "Unity tool result", MIMEType: "application/json", Size: &size},
		},
		StructuredContent: structured,
		IsError:           !response.Success,
	}
}

func unavailableResult(reason, message string, byteSize int) *mcp.CallToolResult {
	return &mcp.CallToolResult{
		Content: []mcp.Content{&mcp.TextContent{Text: message}},
		StructuredContent: map[string]any{
			"success": false, "code": "RESULT_RESOURCE_UNAVAILABLE", "reason": reason,
			"message": message, "byte_size": byteSize, "truncated": true,
		},
		IsError: true,
	}
}

func containsSensitiveResult(data json.RawMessage) bool {
	if len(data) == 0 {
		return false
	}
	var value any
	if json.Unmarshal(data, &value) != nil {
		return true
	}
	return containsSensitiveValue(value)
}

func containsSensitiveValue(value any) bool {
	switch typed := value.(type) {
	case map[string]any:
		for key, child := range typed {
			if sensitiveKey(key) || containsSensitiveValue(child) {
				return true
			}
		}
	case []any:
		for _, child := range typed {
			if containsSensitiveValue(child) {
				return true
			}
		}
	case string:
		lower := strings.ToLower(typed)
		for _, marker := range []string{`"access_token"`, `"refresh_token"`, `"session_token"`, `"api_key"`, `"password"`, "authorization: bearer ", "-----begin private key-----"} {
			if strings.Contains(lower, marker) {
				return true
			}
		}
	}
	return false
}

func sensitiveKey(key string) bool {
	normalized := strings.Map(func(character rune) rune {
		if unicode.IsLetter(character) || unicode.IsDigit(character) {
			return unicode.ToLower(character)
		}
		return -1
	}, key)
	for _, marker := range []string{"password", "passwd", "secret", "credential", "authorization", "privatekey", "sshkey", "apikey", "apitoken", "accesstoken", "refreshtoken", "sessiontoken", "idtoken", "authtoken", "securitytoken", "bearertoken", "approvaltoken", "cookie", "connectionstring"} {
		if strings.Contains(normalized, marker) {
			return true
		}
	}
	return normalized == "token"
}

func registerResultResources(server *mcp.Server, store *resultstore.Store) {
	if store == nil {
		return
	}
	server.AddResourceTemplate(&mcp.ResourceTemplate{
		Name: "hera-unity-result", Title: "Stored Unity tool result",
		Description: "A complete oversized Unity tool result stored outside model-facing inline content.",
		MIMEType:    "application/json", URITemplate: resultResourceTemplate,
	}, func(_ context.Context, request *mcp.ReadResourceRequest) (*mcp.ReadResourceResult, error) {
		if request == nil || request.Params == nil {
			return nil, mcp.ResourceNotFoundError("")
		}
		data, err := store.Read(request.Params.URI)
		if err != nil {
			if errors.Is(err, resultstore.ErrNotFound) || errors.Is(err, resultstore.ErrInvalidHandle) || errors.Is(err, resultstore.ErrIntegrity) {
				return nil, mcp.ResourceNotFoundError(request.Params.URI)
			}
			return nil, err
		}
		return &mcp.ReadResourceResult{Contents: []*mcp.ResourceContents{{
			URI: request.Params.URI, MIMEType: "application/json", Text: string(data),
		}}}, nil
	})
}
