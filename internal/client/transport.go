package client

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"time"
)

const maxResponseSize = 50 * 1024 * 1024

func (c *Client) debugPost(url string, body []byte) {
	if c.Debug {
		fmt.Fprintf(os.Stderr, "[DBG] POST %s body=%s\n", url, debugBody(body))
	}
}

func debugBody(body []byte) string {
	var value any
	if json.Unmarshal(body, &value) != nil {
		return string(body)
	}
	redactTokenFields(value)
	encoded, err := json.Marshal(value)
	if err != nil {
		return string(body)
	}
	return string(encoded)
}

func redactTokenFields(value any) {
	switch typed := value.(type) {
	case map[string]any:
		for key, child := range typed {
			if key == "approval_token" || key == "token" {
				typed[key] = "[redacted]"
				continue
			}
			redactTokenFields(child)
		}
	case []any:
		for _, child := range typed {
			redactTokenFields(child)
		}
	}
}

func (c *Client) processHTTPResponse(resp *http.Response, label string, start time.Time) ([]byte, int, error) {
	defer resp.Body.Close()

	respBody, err := io.ReadAll(io.LimitReader(resp.Body, maxResponseSize+1))
	if c.Debug {
		fmt.Fprintf(os.Stderr, "[DBG] resp %d in %s body=%s\n",
			resp.StatusCode, time.Since(start).Truncate(time.Millisecond), debugBody(respBody))
	}
	if err != nil {
		return nil, 0, fmt.Errorf("read response for %s: %w", label, err)
	}
	if len(respBody) > maxResponseSize {
		return nil, 0, fmt.Errorf("response for %s exceeded maximum size of %d bytes", label, maxResponseSize)
	}

	if resp.StatusCode != http.StatusOK && len(respBody) == 0 {
		return nil, 0, fmt.Errorf("HTTP %d from Unity (%s)", resp.StatusCode, label)
	}

	return respBody, resp.StatusCode, nil
}

func (c *Client) Send(ctx context.Context, inst *Instance, command string, params any, timeoutMs int) (*CommandResponse, error) {
	return c.SendWithOptions(ctx, inst, command, params, timeoutMs, SendOptions{
		Idempotent: command == "list",
	})
}

func (c *Client) SendWithOptions(
	ctx context.Context,
	inst *Instance,
	command string,
	params any,
	timeoutMs int,
	options SendOptions,
) (*CommandResponse, error) {
	if params == nil {
		params = map[string]any{}
	}

	operationID := options.OperationID
	if operationID == "" {
		var err error
		operationID, err = NewOperationID()
		if err != nil {
			return nil, err
		}
	}
	hash, err := argumentsHash(params)
	if err != nil {
		return nil, err
	}
	clientKind := options.ClientKind
	if clientKind == "" {
		clientKind = "cli"
	}
	body, err := json.Marshal(CommandRequest{
		Command: command,
		Params:  params,
		Meta: RequestMeta{
			ProtocolVersion: ExecutionProtocolVersion,
			OperationID:     operationID,
			ArgumentsHash:   hash,
			ClientKind:      clientKind,
			ApprovalToken:   optionalToken(options.ApprovalToken),
			CatalogHash:     options.CatalogHash,
		},
	})
	if err != nil {
		return nil, err
	}

	url := fmt.Sprintf("http://127.0.0.1:%d/command", inst.Port)

	var cancel context.CancelFunc
	if timeoutMs > 0 {
		ctx, cancel = context.WithTimeout(ctx, time.Duration(timeoutMs)*time.Millisecond)
		defer cancel()
	}

	c.debugPost(url, body)
	start := time.Now()
	resp, err := c.doWithReloadRetry(ctx, body, inst, "/command", retryPolicy{
		allowRetry:           options.Idempotent || hasFeature(inst, FeatureOperationLedgerV1),
		unknownOnContextDone: !options.Idempotent,
		unknown: &OperationOutcomeUnknownError{
			Code:        "OPERATION_OUTCOME_UNKNOWN",
			OperationID: operationID,
			Command:     command,
			Project:     inst.ProjectPath,
			Port:        inst.Port,
		},
	})
	if err != nil {
		return nil, err
	}

	respBody, statusCode, err := c.processHTTPResponse(resp, fmt.Sprintf("command: %s", command), start)
	if err != nil {
		return nil, err
	}
	if len(respBody) == 0 {
		return &CommandResponse{
			Success: false,
			Message: fmt.Sprintf("%s failed (connection closed before response)", command),
		}, fmt.Errorf("connection closed before response for command: %s", command)
	}

	var result CommandResponse
	if err := json.Unmarshal(respBody, &result); err != nil {
		if statusCode != http.StatusOK {
			return nil, fmt.Errorf("HTTP %d from Unity returned an invalid error envelope: %w", statusCode, err)
		}
		return &CommandResponse{
			Success: true,
			Message: string(respBody),
		}, nil
	}
	if statusCode != http.StatusOK && result.Code == "" {
		return nil, fmt.Errorf("HTTP %d from Unity returned an error envelope without a code", statusCode)
	}

	diagnoseUnsupportedAction(command, params, &result)

	return &result, nil
}

// diagnoseUnsupportedAction rewrites a generic argument rejection into a
// version-skew diagnosis. The Connector accepts an "action" argument only for
// tools that declare an action contract, so rejecting "/action" on a request
// that carries one means the installed package predates that action rather than
// that the caller passed something malformed.
func diagnoseUnsupportedAction(command string, params any, result *CommandResponse) {
	if result.Success || result.Code != "UNKNOWN_ARGUMENT" {
		return
	}
	fields, ok := params.(map[string]any)
	if !ok {
		return
	}
	action, ok := fields["action"].(string)
	if !ok || action == "" {
		return
	}
	var detail struct {
		Path string `json:"path"`
	}
	if json.Unmarshal(result.Data, &detail) != nil || detail.Path != "/action" {
		return
	}
	result.Code = "CONNECTOR_UPDATE_REQUIRED"
	result.Message = fmt.Sprintf(
		"the Unity Connector installed in this project does not support the %q action of %q",
		action,
		command,
	)
	result.Suggestions = append(
		result.Suggestions,
		"Update the com.notnull92.hera-agent-unity package in this project, then retry.",
		"Run 'hera-agent-unity manage_packages list' to read the installed Connector version.",
	)
}

func optionalToken(token string) *string {
	if token == "" {
		return nil
	}
	return &token
}

func Send(ctx context.Context, inst *Instance, command string, params any, timeoutMs int) (*CommandResponse, error) {
	return DefaultClient.Send(ctx, inst, command, params, timeoutMs)
}

func (c *Client) SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest, timeoutMs int) (*BatchCommandResponse, error) {
	batchTimeout := 30 * time.Second
	if timeoutMs > 0 {
		batchTimeout = time.Duration(timeoutMs) * time.Millisecond
	} else if n := len(req.Commands); n > 0 {
		if calculated := time.Duration(n) * 15 * time.Second; calculated > batchTimeout {
			batchTimeout = calculated
		}
	}
	const maxTimeout = 5 * time.Minute
	if batchTimeout > maxTimeout {
		batchTimeout = maxTimeout
	}
	ctx, cancel := context.WithTimeout(ctx, batchTimeout)
	defer cancel()

	body, err := json.Marshal(req)
	if err != nil {
		return nil, fmt.Errorf("marshal batch request: %w", err)
	}

	c.debugPost(fmt.Sprintf("http://127.0.0.1:%d/commands", inst.Port), body)

	start := time.Now()
	resp, err := c.doWithReloadRetry(ctx, body, inst, "/commands", retryPolicy{
		allowRetry: false,
		unknown: &OperationOutcomeUnknownError{
			Code:    "OPERATION_OUTCOME_UNKNOWN",
			Command: "batch",
		},
	})
	if err != nil {
		return nil, err
	}

	respBody, statusCode, err := c.processHTTPResponse(resp, "batch", start)
	if err != nil {
		return nil, err
	}
	if len(respBody) == 0 {
		return nil, fmt.Errorf("connection closed before response for batch")
	}

	var result BatchCommandResponse
	if err := json.Unmarshal(respBody, &result); err != nil {
		return nil, fmt.Errorf("unmarshal batch response: %w", err)
	}
	if statusCode != http.StatusOK && result.Code == "" {
		return nil, fmt.Errorf("HTTP %d from Unity returned an error envelope without a code", statusCode)
	}

	return &result, nil
}

func SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest, timeoutMs int) (*BatchCommandResponse, error) {
	return DefaultClient.SendBatch(ctx, inst, req, timeoutMs)
}
