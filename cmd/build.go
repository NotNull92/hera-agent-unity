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

// buildCmd routes the build tool. Every action is a normal passthrough; the
// one Go-side addition is `build start --wait`, which — after the Connector
// queues the build — polls the file-bus result file the way `test` does,
// because the Editor blocks (and stops answering HTTP) for the whole build.
func buildCmd(
	ctx context.Context,
	args []string,
	send SendFunc,
	timeout time.Duration,
) (*client.CommandResponse, error) {
	wait := false
	filtered := make([]string, 0, len(args))
	for _, arg := range args {
		if arg == "--wait" {
			wait = true
			continue
		}
		filtered = append(filtered, arg)
	}

	resp, err := runLegacyToolCommand("build", filtered, send)
	if err != nil || resp == nil || !resp.Success || !wait {
		return resp, err
	}
	if len(filtered) == 0 || filtered[0] != "start" {
		return resp, nil
	}

	var meta struct {
		Port int `json:"Port"`
	}
	if unmarshalErr := json.Unmarshal(resp.Data, &meta); unmarshalErr != nil || meta.Port <= 0 {
		return resp, nil
	}

	// Builds routinely outlast the 60s request default; --wait floors the
	// poll at 15 minutes and a larger --timeout extends it further.
	if timeout < 15*time.Minute {
		timeout = 15 * time.Minute
	}
	fmt.Fprintln(os.Stderr, "build queued; the Editor blocks while building, waiting for the report...")
	return poll.WaitForFile(ctx, paths.BuildResultPath(meta.Port), meta.Port, timeout, "build report")
}
