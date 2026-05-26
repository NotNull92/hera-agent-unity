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
	"strings"
	"time"
)

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
		MaxIdleConns:        4,
		MaxIdleConnsPerHost: 2,
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

// doWithReloadRetry sends the POST request and transparently retries on
// connection-refused errors while Unity's HTTP server is down between
// domain reloads. Only the network-transport error path retries; once a
// connection is established the request is considered delivered (no
// double-dispatch). Cap is reloadRetryMax * reloadRetryDelay (~5s).
func doWithReloadRetry(ctx context.Context, url string, body []byte, port int) (*http.Response, error) {
	const reloadRetryMax = 10
	const reloadRetryDelay = 500 * time.Millisecond
	var lastErr error
	for attempt := 0; attempt <= reloadRetryMax; attempt++ {
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
		if err != nil {
			return nil, err
		}
		req.Header.Set("Content-Type", "application/json")
		resp, err := sharedHTTPClient.Do(req)
		if err == nil {
			return resp, nil
		}
		lastErr = err
		if !isConnectionRefused(err) {
			return nil, fmt.Errorf("cannot connect to Unity at port %d: %v", port, err)
		}
		if attempt == reloadRetryMax {
			break
		}
		// Heartbeat read is cheap; bail out early if Unity is reported stopped
		// so we don't waste 5s retrying an editor that's already gone.
		if inst, hbErr := FindByPort(port); hbErr == nil && inst.State == "stopped" {
			return nil, fmt.Errorf("unity at port %d has stopped", port)
		}
		select {
		case <-ctx.Done():
			return nil, ctx.Err()
		case <-time.After(reloadRetryDelay):
		}
	}
	return nil, fmt.Errorf("cannot connect to Unity at port %d after %d retries: %v",
		port, reloadRetryMax, lastErr)
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

// isProcessDead returns true only when the process is confirmed to not exist.
// Permission errors or transient failures return false (not confirmed dead),
// so the instance file is preserved.
// Defaults to the OS-specific implementation; overridden in tests.
var isProcessDead = checkProcessDead

// IsProcessDead is the public probe used by polling commands that want to
// detect a crashed Unity Editor without waiting for the heartbeat to stale.
// Returns true only when the OS confirms the process is gone.
func IsProcessDead(pid int) bool { return isProcessDead(pid) }

func instancesDir() string {
	home, _ := os.UserHomeDir()
	return filepath.Join(home, ".hera-agent-unity", "instances")
}

// ScanInstances reads all instance files from ~/.hera-agent-unity/instances/.
// Stale files whose PID is no longer running are automatically removed.
func ScanInstances() ([]Instance, error) {
	dir := instancesDir()
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
		if inst.PID > 0 && isProcessDead(inst.PID) {
			_ = os.Remove(fp)
			continue
		}
		instances = append(instances, inst)
	}
	return instances, nil
}

// FindByPort scans instance files and returns the instance matching the given port.
// If multiple instances share the same port, the one with the most recent timestamp wins.
func FindByPort(port int) (*Instance, error) {
	instances, err := ScanInstances()
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

func isActiveInstance(inst Instance) bool {
	return inst.State != "stopped" && inst.Timestamp > 0
}

// FindActiveByPort is like FindByPort but skips stopped or incomplete instances.
// Used by polling paths (waitForAlive, waitForReady) that only care about live instances.
func FindActiveByPort(port int) (*Instance, error) {
	instances, err := ScanInstances()
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

// DiscoverInstance finds a running Unity instance from ~/.hera-agent-unity/instances/.
// If port > 0, matches an active instance by port.
// If project is set, matches by project path substring.
// Otherwise returns the most recently active instance.
func DiscoverInstance(project string, port int) (*Instance, error) {
	if port > 0 {
		return FindActiveByPort(port)
	}

	instances, err := ScanInstances()
	if err != nil {
		return nil, fmt.Errorf("no Unity instances found.\nIs Unity running with the Connector package?\nExpected: %s", instancesDir())
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

func Send(inst *Instance, command string, params interface{}, timeoutMs int) (*CommandResponse, error) {
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

	resp, err := doWithReloadRetry(ctx, url, body, inst.Port)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		var body []byte
		body, _ = io.ReadAll(resp.Body)
		if len(body) > 0 {
			return nil, fmt.Errorf("HTTP %d from Unity: %s", resp.StatusCode, string(body))
		}
		return nil, fmt.Errorf("HTTP %d from Unity (command: %s)", resp.StatusCode, command)
	}

	respBody, err := io.ReadAll(resp.Body)
	if err != nil || len(respBody) == 0 {
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

// SendBatch sends multiple commands to Unity in a single HTTP request.
// Timeout is derived from the command count (30s base + 15s per command, 5min cap).
func SendBatch(ctx context.Context, inst *Instance, req BatchCommandRequest) (*BatchCommandResponse, error) {
	batchTimeout := 30 * time.Second
	if n := len(req.Commands); n > 0 {
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
		return nil, fmt.Errorf("marshal batch request: %v", err)
	}

	url := fmt.Sprintf("http://127.0.0.1:%d/commands", inst.Port)

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
	if err != nil {
		return nil, err
	}
	httpReq.Header.Set("Content-Type", "application/json")

	resp, err := sharedHTTPClient.Do(httpReq)
	if err != nil {
		return nil, fmt.Errorf("cannot connect to Unity at port %d: %v", inst.Port, err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		respBody, _ := io.ReadAll(resp.Body)
		if len(respBody) > 0 {
			return nil, fmt.Errorf("HTTP %d from Unity: %s", resp.StatusCode, string(respBody))
		}
		return nil, fmt.Errorf("HTTP %d from Unity (batch)", resp.StatusCode)
	}

	respBody, err := io.ReadAll(resp.Body)
	if err != nil || len(respBody) == 0 {
		return nil, fmt.Errorf("connection closed before response for batch")
	}

	var result BatchCommandResponse
	if err := json.Unmarshal(respBody, &result); err != nil {
		return nil, fmt.Errorf("unmarshal batch response: %v", err)
	}

	return &result, nil
}
