package client

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path/filepath"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
	"strings"
	"time"
)

// Client holds per-client configuration, an HTTP client, an instance cache,
// and a process-death checker.
// Use NewClient to create a fully initialized instance, or DefaultClient for
// the package-level singleton.
type Client struct {
	Debug              bool
	httpClient         *http.Client
	cache              *InstanceCache
	processDeadChecker func(int) bool
}

// NewClient creates a Client with the default HTTP client, instance cache,
// and OS-specific process-death checker.
func NewClient() *Client {
	return &Client{
		httpClient:         sharedHTTPClient,
		cache:              NewInstanceCache(),
		processDeadChecker: checkProcessDead,
	}
}

// DefaultClient is the package-level singleton used by the top-level Send
// and SendBatch convenience functions.
var DefaultClient = NewClient()

// sharedHTTPClient is the package-level singleton used by all Send() calls.
// Reusing it lets the connection pool keep idle keep-alive sockets open
// instead of allocating a fresh dialer + TCP handshake on every call.
// Per-request timeout comes from Request.Context so the singleton's own
// Timeout stays unset.
var sharedHTTPClient = &http.Client{
	Transport: &http.Transport{
		DialContext: (&net.Dialer{
			Timeout:   2 * time.Second,
			KeepAlive: 15 * time.Second,
		}).DialContext,
		MaxIdleConns:        8,
		MaxIdleConnsPerHost: 4,
		IdleConnTimeout:     30 * time.Second,
		DisableCompression:  true, // localhost, gzip cost > benefit
	},
}

// Instance represents a running Unity Editor discovered from ~/.hera-agent-unity/instances/.
type Instance struct {
	State         string `json:"state"`
	ProjectPath   string `json:"projectPath"`
	Port          int    `json:"port"`
	PID           int    `json:"pid"`
	UnityVersion  string `json:"unityVersion,omitempty"`
	Timestamp     int64  `json:"timestamp,omitempty"`
	CompileErrors bool   `json:"compileErrors,omitempty"`
}

// CommandRequest is the JSON body sent to Unity's HTTP server.
type CommandRequest struct {
	Command string      `json:"command"`
	Params  interface{} `json:"params"`
}

// BatchCommandItem is a single command inside a batch request.
type BatchCommandItem struct {
	Command string      `json:"command"`
	Params  interface{} `json:"params,omitempty"`
}

// BatchOptions controls batch execution behavior.
type BatchOptions struct {
	FailFast bool `json:"fail_fast"`
	// Atomic wraps the batch in a single editor Undo group and reverts it if
	// any command fails. Only Undo-aware editor mutations roll back — exec,
	// AssetDatabase and file-system effects are not transactional.
	Atomic bool `json:"atomic"`
}

// BatchCommandRequest sends multiple commands in one HTTP call.
type BatchCommandRequest struct {
	Commands []BatchCommandItem `json:"commands"`
	Options  BatchOptions       `json:"options"`
}

// BatchCommandResponse is the JSON body returned by POST /commands.
type BatchCommandResponse struct {
	Results   []CommandResponse `json:"results"`
	Completed int               `json:"completed"`
	Failed    int               `json:"failed"`
}

// CommandResponse is the JSON body returned by Unity.
// Data is raw JSON so callers can unmarshal into any shape.
// Timings carries optional phase measurements (e.g. compile_ms, execute_ms, total_ms).
// Code/Suggestions are populated by structured error envelopes (e.g. EXEC_COMPILE_ERROR).
// AgentHint carries a short operational next-action for agent consumers.
type CommandResponse struct {
	Success     bool             `json:"success"`
	Message     string           `json:"message"`
	Code        string           `json:"code,omitempty"`
	Suggestions []string         `json:"suggestions,omitempty"`
	AgentHint   string           `json:"agent_hint,omitempty"`
	Data        json.RawMessage  `json:"data,omitempty"`
	Timings     map[string]int64 `json:"timings,omitempty"`
}

// reloadRetryDelay paces dial retries while Unity's HTTP server is down between
// domain reloads. reloadRetryFallbackDeadline caps how long we keep trying when
// the caller set no per-command timeout, so a wedged editor can't hang us
// forever — a reloading-but-alive editor is followed until it's reachable.
const (
	reloadRetryDelay            = 500 * time.Millisecond
	reloadRetryFallbackDeadline = 60 * time.Second
	maxResponseSize             = 50 * 1024 * 1024 // 50 MB
)

// doWithReloadRetry sends the POST request and transparently retries while
// Unity's HTTP listener is down between domain reloads. Only the dial path
// (connection refused) retries — once a connection is established the request
// is considered delivered, so the response-read path never retries (no
// double-dispatch).
//
// A domain reload can rebind Unity to a NEW port, so each retry re-reads the
// heartbeat (fresh) and follows the instance by project rather than hammering
// the now-dead port. It keeps going until the editor answers, the editor is
// gone or stopped (discovery finds no live instance), the caller's context is
// cancelled, or the fallback deadline elapses — so a long reload no longer
// exhausts a fixed retry budget while it's still in progress.
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
		// Listener down — almost always a domain reload. Re-read the heartbeat
		// (bypassing the cache) to bail if the editor is gone/stopped and to
		// follow a port rebind so the next attempt targets the live listener.
		ClearInstanceCache()
		next, derr := DiscoverInstance(project, 0)
		if derr != nil {
			// No live instance for this project — editor gone or stopped
			// (DiscoverInstance filters out stopped, so a stopped editor
			// surfaces here, not as a reachable next.State == "stopped").
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
		strings.Contains(s, "actively refused") || // Windows wording
		strings.Contains(s, "No connection could be made")
}

// IsProcessDead is the public probe used by polling commands that want to
// detect a crashed Unity Editor without waiting for the heartbeat to stale.
// Returns true only when the OS confirms the process is gone.
func (c *Client) IsProcessDead(pid int) bool { return c.processDeadChecker(pid) }

// IsProcessDead delegates to DefaultClient.IsProcessDead.
func IsProcessDead(pid int) bool { return DefaultClient.IsProcessDead(pid) }

// ClearInstanceCache discards the cached scan result so the next call to
// ScanInstances reads from disk again. Useful in tests and after explicit
// install/uninstall flows where the cache could mask new state.
func (c *Client) ClearInstanceCache() { c.cache.Clear() }

// ClearInstanceCache delegates to DefaultClient.ClearInstanceCache.
func ClearInstanceCache() { DefaultClient.ClearInstanceCache() }

// ScanInstances reads all instance files from ~/.hera-agent-unity/instances/.
// Stale files whose PID is no longer running are automatically removed.
// Results are cached for instanceCacheTTL to keep multi-step workflows
// (batch, exec → console → exec, etc.) from re-stat'ing the dir on every hop.
func (c *Client) ScanInstances() ([]Instance, error) {
	if cached, ok := c.cache.Get(); ok {
		return cached, nil
	}

	dir := paths.InstancesDir()
	entries, err := os.ReadDir(dir)
	if err != nil {
		return nil, err
	}

	var instances []Instance
	for _, e := range entries {
		if e.IsDir() || !strings.HasSuffix(e.Name(), ".json") {
			continue
		}
		fp := filepath.Join(dir, e.Name())
		data, err := os.ReadFile(fp)
		if err != nil {
			continue
		}
		var inst Instance
		if err := json.Unmarshal(data, &inst); err != nil {
			continue
		}
		if inst.PID > 0 && c.processDeadChecker(inst.PID) {
			_ = os.Remove(fp)
			continue
		}
		instances = append(instances, inst)
	}

	c.cache.Set(instances)
	return instances, nil
}

// ScanInstances delegates to DefaultClient.ScanInstances.
func ScanInstances() ([]Instance, error) { return DefaultClient.ScanInstances() }

// FindByPort scans instance files and returns the instance matching the given port.
// If multiple instances share the same port, the one with the most recent timestamp wins.
func (c *Client) FindByPort(port int) (*Instance, error) {
	instances, err := c.ScanInstances()
	if err != nil {
		return nil, err
	}
	var best *Instance
	for i, inst := range instances {
		if inst.Port != port {
			continue
		}
		if best == nil || inst.Timestamp > best.Timestamp {
			best = &instances[i]
		}
	}
	if best == nil {
		return nil, fmt.Errorf("no instance on port %d", port)
	}
	return best, nil
}

// FindByPort delegates to DefaultClient.FindByPort.
func FindByPort(port int) (*Instance, error) { return DefaultClient.FindByPort(port) }

func isActiveInstance(inst Instance) bool {
	return inst.State != unitystate.Stopped && inst.Timestamp > 0
}

// FindActiveByPort is like FindByPort but skips stopped or incomplete instances.
// Used by polling paths (waitForAlive, waitForReady) that only care about live instances.
func (c *Client) FindActiveByPort(port int) (*Instance, error) {
	instances, err := c.ScanInstances()
	if err != nil {
		return nil, err
	}
	var best *Instance
	for i, inst := range instances {
		if inst.Port != port || !isActiveInstance(inst) {
			continue
		}
		if best == nil || inst.Timestamp > best.Timestamp {
			best = &instances[i]
		}
	}
	if best == nil {
		return nil, fmt.Errorf("no active instance on port %d", port)
	}
	return best, nil
}

// FindActiveByPort delegates to DefaultClient.FindActiveByPort.
func FindActiveByPort(port int) (*Instance, error) { return DefaultClient.FindActiveByPort(port) }

// DiscoverInstance finds a running Unity instance from ~/.hera-agent-unity/instances/.
// If port > 0, matches an active instance by port.
// If project is set, matches by project path substring.
// Otherwise returns the most recently active instance.
func (c *Client) DiscoverInstance(project string, port int) (*Instance, error) {
	if port > 0 {
		return c.FindActiveByPort(port)
	}

	instances, err := c.ScanInstances()
	if err != nil {
		return nil, fmt.Errorf("no Unity instances found.\nIs Unity running with the Connector package?\nExpected: %s", paths.InstancesDir())
	}

	// Filter out stopped instances
	var alive []Instance
	for _, inst := range instances {
		if !isActiveInstance(inst) {
			continue
		}
		alive = append(alive, inst)
	}

	if len(alive) == 0 {
		return nil, fmt.Errorf("no Unity instances running")
	}

	if project != "" {
		for _, inst := range alive {
			if strings.Contains(filepath.ToSlash(inst.ProjectPath), filepath.ToSlash(project)) {
				return &inst, nil
			}
		}
		return nil, fmt.Errorf("no Unity instance found for project: %s", project)
	}

	// Try to match by current working directory before falling back to timestamp
	if cwd, err := os.Getwd(); err == nil {
		cwdNorm := filepath.ToSlash(cwd)
		for _, inst := range alive {
			projNorm := filepath.ToSlash(inst.ProjectPath)
			if cwdNorm == projNorm || strings.HasPrefix(cwdNorm, projNorm+"/") {
				return &inst, nil
			}
		}
	}

	// Return the most recently updated
	best := alive[0]
	for _, inst := range alive[1:] {
		if inst.Timestamp > best.Timestamp {
			best = inst
		}
	}
	return &best, nil
}

// DiscoverInstance delegates to DefaultClient.DiscoverInstance.
func DiscoverInstance(project string, port int) (*Instance, error) {
	return DefaultClient.DiscoverInstance(project, port)
}

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

func (c *Client) Send(inst *Instance, command string, params interface{}, timeoutMs int) (*CommandResponse, error) {
	if params == nil {
		params = map[string]interface{}{}
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
		// Some commands (e.g. play mode entry) close the connection before responding.
		// Unity side should be fixed to send response before closing; until then,
		// treat empty response as error so crashes are not masked as success.
		return &CommandResponse{
			Success: false,
			Message: fmt.Sprintf("%s failed (connection closed before response)", command),
		}, fmt.Errorf("connection closed before response for command: %s", command)
	}

	var result CommandResponse
	if err := json.Unmarshal(respBody, &result); err != nil {
		// Unity sent a non-JSON body — treat as plain message.
		return &CommandResponse{
			Success: true,
			Message: string(respBody),
		}, nil
	}

	return &result, nil
}

// Send is a convenience wrapper that delegates to DefaultClient.Send.
func Send(inst *Instance, command string, params interface{}, timeoutMs int) (*CommandResponse, error) {
	return DefaultClient.Send(inst, command, params, timeoutMs)
}

// SendBatch sends multiple commands to Unity in a single HTTP request.
// If timeoutMs > 0 it is used; otherwise the timeout is derived from the
// command count (30s base + 15s per command, 5min cap).
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

// SendBatch is a convenience wrapper that delegates to DefaultClient.SendBatch.
func SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest, timeoutMs int) (*BatchCommandResponse, error) {
	return DefaultClient.SendBatch(ctx, inst, req, timeoutMs)
}
