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
		fmt.Fprintf(os.Stderr, "[DBG] POST %s body=%s\n", url, string(body))
	}
}

func (c *Client) processHTTPResponse(resp *http.Response, label string, start time.Time) ([]byte, error) {
	defer resp.Body.Close()

	respBody, err := io.ReadAll(io.LimitReader(resp.Body, maxResponseSize+1))
	if c.Debug {
		fmt.Fprintf(os.Stderr, "[DBG] resp %d in %s body=%s\n",
			resp.StatusCode, time.Since(start).Truncate(time.Millisecond), string(respBody))
	}
	if err != nil {
		return nil, fmt.Errorf("read response for %s: %w", label, err)
	}
	if len(respBody) > maxResponseSize {
		return nil, fmt.Errorf("response for %s exceeded maximum size of %d bytes", label, maxResponseSize)
	}

	if resp.StatusCode != http.StatusOK {
		if len(respBody) > 0 {
			return nil, fmt.Errorf("HTTP %d from Unity: %s", resp.StatusCode, string(respBody))
		}
		return nil, fmt.Errorf("HTTP %d from Unity (%s)", resp.StatusCode, label)
	}

	return respBody, nil
}

func (c *Client) Send(inst *Instance, command string, params any, timeoutMs int) (*CommandResponse, error) {
	if params == nil {
		params = map[string]any{}
	}

	body, err := json.Marshal(CommandRequest{Command: command, Params: params})
	if err != nil {
		return nil, err
	}

	url := fmt.Sprintf("http://127.0.0.1:%d/command", inst.Port)

	ctx := context.Background()
	var cancel context.CancelFunc
	if timeoutMs > 0 {
		ctx, cancel = context.WithTimeout(ctx, time.Duration(timeoutMs)*time.Millisecond)
		defer cancel()
	}

	c.debugPost(url, body)
	start := time.Now()
	resp, err := c.doWithReloadRetry(ctx, body, inst, "/command")
	if err != nil {
		return nil, err
	}

	respBody, err := c.processHTTPResponse(resp, fmt.Sprintf("command: %s", command), start)
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
		return &CommandResponse{
			Success: true,
			Message: string(respBody),
		}, nil
	}

	return &result, nil
}

func Send(inst *Instance, command string, params any, timeoutMs int) (*CommandResponse, error) {
	return DefaultClient.Send(inst, command, params, timeoutMs)
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
	resp, err := c.doWithReloadRetry(ctx, body, inst, "/commands")
	if err != nil {
		return nil, err
	}

	respBody, err := c.processHTTPResponse(resp, "batch", start)
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

	return &result, nil
}

func SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest, timeoutMs int) (*BatchCommandResponse, error) {
	return DefaultClient.SendBatch(ctx, inst, req, timeoutMs)
}
