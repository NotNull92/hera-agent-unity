package cmd

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
)

func testCmd(ctx context.Context, args []string, send SendFunc, _ instanceResolver, timeout time.Duration) (*client.CommandResponse, error) {
	parsedParams, _, err := buildParams(args, nil)
	if err != nil {
		return nil, err
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

	return poll.WaitForAsyncJob(ctx, resultPath, port, timeout, "test results")
}
