package client

import (
	"bytes"
	"context"
	"fmt"
	"net/http"
	"strings"
	"time"
)

const (
	reloadRetryDelay            = 500 * time.Millisecond
	reloadRetryFallbackDeadline = 60 * time.Second
)

func (c *Client) doWithReloadRetry(ctx context.Context, body []byte, inst *Instance, path string) (*http.Response, error) {
	port := inst.Port
	project := inst.ProjectPath
	deadline := time.Now().Add(reloadRetryFallbackDeadline)
	var lastErr error
	for {
		url := fmt.Sprintf("http://127.0.0.1:%d%s", port, path)
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
		if err != nil {
			return nil, err
		}
		req.Header.Set("Content-Type", "application/json")
		resp, err := c.httpClient.Do(req)
		if err == nil {
			return resp, nil
		}
		lastErr = err
		if !isConnectionRefused(err) {
			return nil, fmt.Errorf("cannot connect to Unity at port %d: %w", port, err)
		}
		if time.Now().After(deadline) {
			break
		}
		ClearInstanceCache()
		next, derr := DiscoverInstance(project, 0)
		if derr != nil {
			return nil, fmt.Errorf("cannot reach Unity for project %q (editor no longer running?): %w", project, lastErr)
		}
		port = next.Port
		select {
		case <-ctx.Done():
			return nil, ctx.Err()
		case <-time.After(reloadRetryDelay):
		}
	}
	return nil, fmt.Errorf("cannot connect to Unity for project %q after %s (still reloading?): %w",
		project, reloadRetryFallbackDeadline, lastErr)
}

func isConnectionRefused(err error) bool {
	if err == nil {
		return false
	}
	s := err.Error()
	return strings.Contains(s, "connection refused") ||
		strings.Contains(s, "actively refused") ||
		strings.Contains(s, "No connection could be made")
}
