package cmd

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
)

func isTestResume(category string, args []string) bool {
	if category != "test" {
		return false
	}
	for _, arg := range args {
		if arg == "--resume" {
			return true
		}
	}
	return false
}

func testResumeRunID(args []string) (string, bool, error) {
	for i, arg := range args {
		if arg != "--resume" {
			continue
		}
		if i+1 >= len(args) || len(args[i+1]) >= 2 && args[i+1][:2] == "--" {
			return "", true, fmt.Errorf("--resume requires a run_id")
		}
		runID := args[i+1]
		if runID == "" || len(runID) > 128 {
			return "", true, fmt.Errorf("invalid --resume run_id")
		}
		for _, ch := range runID {
			if ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '-' || ch == '_' {
				continue
			}
			return "", true, fmt.Errorf("invalid --resume run_id")
		}
		return runID, true, nil
	}
	return "", false, nil
}

func testCmd(ctx context.Context, args []string, send SendFunc, resolve instanceResolver, timeout time.Duration) (*client.CommandResponse, error) {
	if _, err := parseTestOptions(args); err != nil {
		return &client.CommandResponse{Code: "INVALID_ARGUMENT", Message: err.Error()}, nil
	}
	resumeRunID, resume, err := testResumeRunID(args)
	if err != nil {
		return nil, err
	}
	parsedParams, positional, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}

	// `test list` / `test cancel` answer directly — neither starts a run, so
	// neither has a result file to poll for.
	if len(positional) > 0 {
		action := positional[0]
		if action != "list" && action != "cancel" {
			return nil, fmt.Errorf("unknown test subcommand %q (expected list or cancel)", action)
		}
		params := map[string]interface{}{"action": action}
		for _, key := range []string{"mode", "filter", "category", "assembly", "limit"} {
			if v, ok := parsedParams[key]; ok {
				params[key] = v
			}
		}
		return sendRunTests(send, params)
	}

	if resume {
		if resolve == nil {
			return nil, fmt.Errorf("--resume requires a selected Unity Editor")
		}
		inst, err := resolve()
		if err != nil {
			return nil, fmt.Errorf("resolve Unity Editor for test resume: %w", err)
		}
		fmt.Fprintf(os.Stderr, "Resuming Unity test run %s on port %d...\n", resumeRunID, inst.Port)
		port, err := testResumePort(resumeRunID, inst.Port)
		if err != nil {
			return nil, err
		}
		return pollTestResults(ctx, *inst, port, resumeRunID, timeout)
	}

	mode := "EditMode"
	if m, ok := parsedParams["mode"].(string); ok {
		mode = m
	}

	if mode != "EditMode" && mode != "PlayMode" {
		return nil, fmt.Errorf("--mode must be EditMode or PlayMode, got: %s", mode)
	}

	params := map[string]interface{}{
		"mode":          mode,
		"async_results": true,
	}
	for _, key := range []string{"filter", "category", "assembly"} {
		if v, ok := parsedParams[key].(string); ok {
			params[key] = v
		}
	}
	for _, key := range []string{"shard-index", "shard-count"} {
		if value, ok := parsedParams[key]; ok {
			params[strings.ReplaceAll(key, "-", "_")] = value
		}
	}

	target := client.Instance{}
	if resolve != nil {
		inst, err := resolve()
		if err != nil {
			return nil, err
		}
		target = *inst
	}
	resp, err := sendRunTests(send, params)
	if err != nil {
		return nil, err
	}

	if resp.Message != "running" {
		return resp, nil
	}

	var meta struct {
		Port  int    `json:"port"`
		RunID string `json:"run_id"`
	}
	if err := json.Unmarshal(resp.Data, &meta); err != nil || meta.Port <= 0 {
		return resp, nil
	}

	fmt.Fprintf(os.Stderr, "%s tests running, waiting for results...\n", mode)

	target.Port = meta.Port
	return pollTestResults(ctx, target, meta.Port, meta.RunID, timeout)
}

// The connector only registers run_tests when the Unity Test Framework package
// is present (asmdef define constraint), so a missing tool means a missing
// package rather than an outdated connector.
func sendRunTests(send SendFunc, params map[string]interface{}) (*client.CommandResponse, error) {
	resp, err := send("run_tests", params)
	if err != nil {
		return nil, err
	}
	if !resp.Success && resp.Code == "UNKNOWN_COMMAND" {
		return nil, fmt.Errorf(
			"'run_tests' is not available.\n" +
				"Install the Unity Test Framework package:\n" +
				"  Window > Package Manager > search 'Test Framework' > Install")
	}
	return resp, nil
}

func pollTestResults(ctx context.Context, target client.Instance, port int, runID string, timeout time.Duration) (*client.CommandResponse, error) {
	ctx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	resultPath := paths.LegacyTestResultPath(port)
	if runID != "" {
		resultPath = paths.TestResultPath(port, runID)
	}

	resp, err := poll.WaitForFile(ctx, resultPath, target, timeout, "test results")
	if err == nil || runID == "" {
		return resp, err
	}
	if !errors.Is(err, context.DeadlineExceeded) && !errors.Is(err, poll.ErrWaitTimeout) {
		return nil, err
	}

	pendingPath := paths.TestPendingPath(port, runID)
	if _, statErr := os.Stat(pendingPath); statErr != nil {
		return nil, err
	}

	data, marshalErr := json.Marshal(struct {
		Status string `json:"status"`
		Port   int    `json:"port"`
		RunID  string `json:"run_id"`
	}{
		Status: "running",
		Port:   port,
		RunID:  runID,
	})
	if marshalErr != nil {
		return nil, marshalErr
	}

	selector := fmt.Sprintf("--port %d", port)
	if target.ProjectPath != "" {
		selector = fmt.Sprintf(`--project "%s"`, target.ProjectPath)
	}
	return &client.CommandResponse{
		Success: false,
		Code:    "TEST_RUN_PENDING",
		Message: "Unity has not written the test result yet. The test run is still pending; a stale heartbeat during Test Runner work does not by itself mean the Editor is unresponsive.",
		Suggestions: []string{
			fmt.Sprintf("Resume this same run without starting another test: hera-agent-unity %s test --resume %s --timeout <milliseconds>", selector, runID),
			"Check the Unity process or port separately only if the pending run never produces a result.",
		},
		Data: data,
	}, nil
}

// Pending and result may coexist for one dispatch port while completion is written.
func testResumePort(runID string, fallback int) (int, error) {
	ports := make(map[int]bool)
	for _, prefix := range []string{"test-pending-", "test-results-"} {
		matches, err := filepath.Glob(filepath.Join(paths.StatusDir(), prefix+"*-"+runID+".json"))
		if err != nil {
			return 0, err
		}
		for _, match := range matches {
			raw := strings.TrimSuffix(strings.TrimPrefix(filepath.Base(match), prefix), "-"+runID+".json")
			port, err := strconv.Atoi(raw)
			if err != nil || port <= 0 {
				continue
			}
			ports[port] = true
		}
	}
	if len(ports) > 1 {
		return 0, fmt.Errorf("ambiguous test run %q: records exist on multiple dispatch ports", runID)
	}
	for port := range ports {
		return port, nil
	}
	return fallback, nil
}
