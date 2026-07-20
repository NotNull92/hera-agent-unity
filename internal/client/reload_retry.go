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
		// Never leave the socket in the idle pool. Any command can end in a
		// domain reload — refresh --compile and editor play/stop always do, and
		// exec/menu can trigger one indirectly — and the reload closes Unity's
		// HttpListener. Mono answers that close by writing an empty
		// `200 OK / Content-Length: 0 / Connection: close` onto whatever
		// connections it still holds. A pooled connection would receive those
		// bytes with no request outstanding, and net/http logs them as
		// "Unsolicited response received on idle HTTP channel" — stderr noise
		// that reads like a failure even though the command succeeded.
		// Closing costs one localhost handshake per command, and a CLI process
		// sends exactly one command, so there is no reuse to give up.
		req.Close = true
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
		next, derr := c.DiscoverInstanceFresh(project, 0)
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
