package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
)

func testCmd(args []string, send SendFunc, resolve instanceResolver) (*client.CommandResponse, error) {
	flags := parseSubFlags(args)

	mode := "EditMode"
	if m, ok := flags["mode"]; ok {
		mode = m
	}

	if mode != "EditMode" && mode != "PlayMode" {
		return nil, fmt.Errorf("--mode must be EditMode or PlayMode, got: %s", mode)
	}

	params := map[string]interface{}{
		"mode": mode,
	}
	if filter, ok := flags["filter"]; ok {
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

	// EditMode: results returned directly in response
	if mode == "EditMode" {
		return resp, nil
	}

	// PlayMode: Unity returns "running", poll results file
	if resp.Message != "running" {
		return resp, nil
	}

	inst, err := resolve()
	if err != nil {
		return nil, err
	}

	fmt.Fprintln(os.Stderr, "PlayMode tests running, waiting for results...")

	return pollTestResults(inst.Port)
}

func pollTestResults(port int) (*client.CommandResponse, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return nil, fmt.Errorf("cannot determine home directory: %w", err)
	}

	resultsPath := filepath.Join(home, ".hera-agent-unity", "status", fmt.Sprintf("test-results-%d.json", port))
	return poll.WaitForAsyncJob(resultsPath, port, 10*time.Minute, "test results")
}
