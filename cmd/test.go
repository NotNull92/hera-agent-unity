package cmd

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
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
	resumeRunID, resume, err := testResumeRunID(args)
	if err != nil {
		return nil, err
	}
	parsedParams, _, err := buildParams(args, nil)
	if err != nil {
		return nil, err
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
		return pollTestResults(ctx, inst.Port, resumeRunID, timeout)
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
	if filter, ok := parsedParams["filter"].(string); ok {
		params["filter"] = filter
	}

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

	return pollTestResults(ctx, meta.Port, meta.RunID, timeout)
}

func pollTestResults(ctx context.Context, port int, runID string, timeout time.Duration) (*client.CommandResponse, error) {
	ctx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	resultPath := paths.LegacyTestResultPath(port)
	if runID != "" {
		resultPath = paths.TestResultPath(port, runID)
	}

	resp, err := poll.WaitForAsyncJob(ctx, resultPath, port, timeout, "test results")
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

	return &client.CommandResponse{
		Success: false,
		Code:    "TEST_RUN_PENDING",
		Message: "Unity has not written the test result yet. The test run is still pending; a stale heartbeat during Test Runner work does not by itself mean the Editor is unresponsive.",
		Suggestions: []string{
			fmt.Sprintf("Resume this same run without starting another test: hera-agent-unity --port %d test --resume %s --timeout <milliseconds>", port, runID),
			"Check the Unity process or port separately only if the pending run never produces a result.",
		},
		Data: data,
	}, nil
}
