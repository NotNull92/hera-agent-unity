package poll

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/logutil"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

// WaitForFile polls a filesystem result file until it appears, Unity stops,
// or the timeout expires. It uses exponential backoff starting at 100 ms and
// capping at 1.5 s to avoid burning I/O on long-running operations.
func WaitForFile(ctx context.Context, resultPath string, port int, timeout time.Duration, opName string) (*client.CommandResponse, error) {
	deadline := time.Now().Add(timeout)
	const pidCheckEvery = 5 * time.Second
	lastPidCheck := time.Now()
	var lastPid int

	interval := 100 * time.Millisecond
	const maxInterval = 1500 * time.Millisecond

	for time.Now().Before(deadline) {
		select {
		case <-ctx.Done():
			return nil, ctx.Err()
		case <-time.After(interval):
		}

		data, err := os.ReadFile(resultPath)
		if err == nil {
			var resp client.CommandResponse
			if err := json.Unmarshal(data, &resp); err != nil {
				return nil, fmt.Errorf("failed to parse %s: %w", opName, err)
			}
			_ = os.Remove(resultPath)
			return &resp, nil
		}

		// State check (cheap): instance writes "stopped" on graceful quit.
		inst, statusErr := client.FindByPortFresh(port)
		if statusErr == nil {
			if inst.State == unitystate.Stopped {
				return nil, fmt.Errorf("unity editor has stopped (port %d)", port)
			}
			lastPid = inst.PID
		}

		// PID liveness (more expensive): catches the case where Unity crashed
		// during a domain reload — state stays mid-transition forever otherwise.
		// Done every pidCheckEvery seconds rather than every tick.
		if lastPid > 0 && time.Since(lastPidCheck) >= pidCheckEvery {
			lastPidCheck = time.Now()
			if client.IsProcessDead(lastPid) {
				return nil, fmt.Errorf("unity editor process %d is no longer running", lastPid)
			}
		}

		if interval < maxInterval {
			interval = min(interval*2, maxInterval)
		}
	}

	return nil, fmt.Errorf("timed out waiting for %s", opName)
}

// WaitForAsyncJob wraps WaitForFile with log suppression for the known-harmless
// "Unsolicited response received on idle HTTP channel" noise that Go's net/http
// emits when Unity drops the connection during a domain reload.
func WaitForAsyncJob(ctx context.Context, resultPath string, port int, timeout time.Duration, opName string) (*client.CommandResponse, error) {
	original := log.Writer()
	log.SetOutput(logutil.NewSuppressWriter(os.Stderr, "Unsolicited response received on idle HTTP channel"))
	defer log.SetOutput(original)

	return WaitForFile(ctx, resultPath, port, timeout, opName)
}
