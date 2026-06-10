package cmd

import (
	"encoding/json"
	"fmt"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
)

// managePackagesCmd dispatches to manage_packages on the connector. list is
// synchronous (the C# handler blocks on the ListRequest within its own 60s
// budget). add / remove / embed return immediately with a job_id; we then
// poll ~/.hera-agent-unity/status/package-result-PORT-JOBID.json the same
// way cmd/test.go waits on PlayMode results.
func managePackagesCmd(args []string, send SendFunc, resolve instanceResolver) (*client.CommandResponse, error) {
	params, _, err := buildParams(args, nil)
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

	if isHumanCommand("manage_packages") || flagVerbose {
		fmt.Fprintf(os.Stderr,
			"Package job %s (%s %s) running, waiting for completion...\n",
			meta.JobID, meta.Action, meta.Identifier)
	}

	port := meta.Port
	if port == 0 {
		inst, err := resolve()
		if err != nil {
			return nil, err
		}
		port = inst.Port
	}

	return pollPackageJob(port, meta.JobID)
}

func pollPackageJob(port int, jobID string) (*client.CommandResponse, error) {
	return poll.WaitForAsyncJob(paths.PackageResultPath(port, jobID), port, 10*time.Minute, fmt.Sprintf("package job %s", jobID))
}
