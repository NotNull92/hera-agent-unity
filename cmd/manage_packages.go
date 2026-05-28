package cmd

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// managePackagesCmd dispatches to manage_packages on the connector. list is
// synchronous (the C# handler blocks on the ListRequest within its own 60s
// budget). add / remove / embed return immediately with a job_id; we then
// poll ~/.hera-agent-unity/status/package-result-PORT-JOBID.json the same
// way cmd/test.go waits on PlayMode results.
func managePackagesCmd(args []string, send sendFn, port int) (*client.CommandResponse, error) {
	params, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}

	resp, err := send("manage_packages", params)
	if err != nil {
		return nil, err
	}

	// list / errors / direct success: nothing to poll for.
	if !resp.Success || resp.Message != "running" {
		return resp, nil
	}

	var meta struct {
		JobID      string `json:"job_id"`
		Port       int    `json:"port"`
		Action     string `json:"action"`
		Identifier string `json:"identifier"`
	}
	if uerr := json.Unmarshal(resp.Data, &meta); uerr != nil || meta.JobID == "" {
		return resp, nil
	}

	if isHumanCommand() || flagVerbose {
		fmt.Fprintf(os.Stderr,
			"Package job %s (%s %s) running, waiting for completion...\n",
			meta.JobID, meta.Action, meta.Identifier)
	}

	// Package installs trigger a domain reload that drops the HTTP listener
	// mid-response, same as PlayMode tests — suppress the noisy Go log so it
	// doesn't bleed into the operator's terminal.
	original := log.Writer()
	log.SetOutput(&suppressWriter{w: os.Stderr, suppress: "Unsolicited response received on idle HTTP channel"})
	defer log.SetOutput(original)

	return pollPackageJob(port, meta.JobID)
}

func pollPackageJob(port int, jobID string) (*client.CommandResponse, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return nil, fmt.Errorf("cannot determine home directory: %w", err)
	}

	resultPath := filepath.Join(home, ".hera-agent-unity", "status",
		fmt.Sprintf("package-result-%d-%s.json", port, jobID))
	deadline := time.Now().Add(10 * time.Minute)
	const pidCheckEvery = 5 * time.Second
	lastPidCheck := time.Now()
	var lastPid int

	for time.Now().Before(deadline) {
		time.Sleep(500 * time.Millisecond)

		data, rerr := os.ReadFile(resultPath)
		if rerr == nil {
			_ = os.Remove(resultPath)
			var resp client.CommandResponse
			if jerr := json.Unmarshal(data, &resp); jerr != nil {
				return nil, fmt.Errorf("failed to parse package result: %w", jerr)
			}
			return &resp, nil
		}

		inst, statusErr := client.FindByPort(port)
		if statusErr == nil {
			if inst.State == "stopped" {
				return nil, fmt.Errorf("unity editor has stopped (port %d)", port)
			}
			lastPid = inst.PID
		}

		if lastPid > 0 && time.Since(lastPidCheck) >= pidCheckEvery {
			lastPidCheck = time.Now()
			if client.IsProcessDead(lastPid) {
				return nil, fmt.Errorf("unity editor process %d is no longer running", lastPid)
			}
		}
	}

	return nil, fmt.Errorf("timed out waiting for package job %s (10m)", jobID)
}
