package cmd

import (
	"context"
	"errors"
	"fmt"
	"os"
	"slices"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

type instanceResolver func() (*client.Instance, error)

// statusPollBaseInterval is the initial polling interval used by waitForAlive,
// waitForState, and waitForReady. It is a variable (not a const) so tests can
// shorten it to avoid sleeping for hundreds of milliseconds.
var statusPollBaseInterval = 500 * time.Millisecond

func statusCmd(inst *client.Instance) error {
	status, err := client.FindByPort(inst.Port)
	if err != nil {
		return fmt.Errorf("no status for port %d — Unity may not be running", inst.Port)
	}

	age := time.Since(time.UnixMilli(status.Timestamp))
	if age > 3*time.Second {
		fmt.Fprintf(os.Stderr, "Unity (port %d): not responding (last heartbeat %s ago)\n", status.Port, age.Truncate(time.Second))
		return nil
	}

	if tui.ColorEnabled() {
		rows := [][2]string{
			{"State", tui.DotStatus(status.State)},
			{"Project", tui.PathStyle.Render(status.ProjectPath)},
			{"Version", status.UnityVersion},
		}
		if status.DocsVersion != "" {
			rows = append(rows, [2]string{"Docs", status.DocsVersion})
		}
		if status.Compiler != nil {
			rows = append(rows, [2]string{"Compiler", compilerStatus(status.Compiler)})
		}
		rows = append(rows, [2]string{"PID", fmt.Sprintf("%d", status.PID)})
		fmt.Println(tui.StatusPanel(
			fmt.Sprintf("Unity Editor — port %d", status.Port),
			rows,
		))
		return nil
	}

	// Plain output — kept stable for script/AI parsing.
	fmt.Printf("Unity (port %d): %s\n", status.Port, status.State)
	fmt.Printf("  Project: %s\n", status.ProjectPath)
	fmt.Printf("  Version: %s\n", status.UnityVersion)
	if status.DocsVersion != "" {
		fmt.Printf("  Docs:    %s\n", status.DocsVersion)
	}
	if status.Compiler != nil {
		fmt.Printf("  Compiler: %s\n", compilerStatus(status.Compiler))
	}
	fmt.Printf("  PID:     %d\n", status.PID)
	return nil
}

func compilerStatus(info *client.CompilerInfo) string {
	if info == nil {
		return "unknown"
	}
	if info.Error != "" {
		return "error: " + info.Error
	}
	csc := info.CscKind
	if csc == "" {
		csc = "unknown"
	}
	dotnet := info.DotnetKind
	if dotnet == "" {
		dotnet = "unknown"
	}
	return fmt.Sprintf("csc=%s dotnet=%s", csc, dotnet)
}

// pingCmd is a token-cheap liveness probe for agents. Reads the instance
// heartbeat file directly (no Unity HTTP round-trip), prints a single
// machine-parseable line, exits 0 if alive within 3s otherwise 1.
func pingCmd(project string, port int) error {
	inst, err := discoverStatusInstance(project, port)
	if err != nil {
		fmt.Println("alive=0")
		return fmt.Errorf("ping: not alive")
	}
	age := time.Since(time.UnixMilli(inst.Timestamp))
	alive := age <= 3*time.Second && inst.State != unitystate.Stopped
	flag := 0
	if alive {
		flag = 1
	}
	fmt.Printf("port=%d alive=%d state=%s age_ms=%d\n",
		inst.Port, flag, inst.State, age.Milliseconds())
	if !alive {
		return fmt.Errorf("ping: not alive (state=%s, age_ms=%d)", inst.State, age.Milliseconds())
	}
	return nil
}

func discoverStatusInstance(project string, port int) (*client.Instance, error) {
	if port > 0 {
		return client.FindByPort(port)
	}
	return client.DiscoverInstance(project, 0)
}

// waitForAlive resolves the current target instance, then polls until a newer heartbeat appears.
// This keeps following the same project even if Unity rebinds to a new port during reload.
func waitForAlive(ctx context.Context, resolve instanceResolver, timeoutMs int, category string) (*client.Instance, error) {
	baseline := time.Now().UnixMilli()
	inst, err := resolve()
	if err == nil {
		baseline = inst.Timestamp
		// Already fresh — check if timestamp was updated within the last second
		if time.Now().UnixMilli()-baseline < 1000 {
			return inst, nil
		}
	}

	if shouldNarrate(category) {
		fmt.Fprintf(os.Stderr, "Waiting for Unity...\n")
	}

	var result *client.Instance
	err = poll.ExponentialBackoffLoop(
		ctx,
		time.Duration(timeoutMs)*time.Millisecond,
		statusPollBaseInterval,
		1500*time.Millisecond,
		func() bool {
			inst, err = resolve()
			if err != nil {
				return false
			}
			if inst.Timestamp > baseline {
				result = inst
				return true
			}
			return false
		},
	)
	if err != nil {
		if errors.Is(err, context.Canceled) {
			return nil, err
		}
		return nil, fmt.Errorf("timed out waiting for Unity")
	}
	if shouldNarrate(category) {
		fmt.Fprintf(os.Stderr, "Unity is ready.\n")
	}
	return result, nil
}

// waitForState polls the heartbeat until inst.State matches one of targets,
// or the deadline elapses. Used to confirm play/stop completion without
// holding the HTTP connection through the domain reload that play-mode
// entry triggers — the listener is stopped mid-response, so the only
// reliable confirmation channel is the filesystem heartbeat.
func waitForState(ctx context.Context, resolve instanceResolver, timeoutMs int, category string, targets ...string) error {
	if shouldNarrate(category) {
		fmt.Fprintf(os.Stderr, "Waiting for state %v...\n", targets)
	}
	var matchedState string
	err := poll.ExponentialBackoffLoop(
		ctx,
		time.Duration(timeoutMs)*time.Millisecond,
		statusPollBaseInterval,
		1500*time.Millisecond,
		func() bool {
			inst, err := resolve()
			if err != nil {
				return false
			}
			if slices.Contains(targets, inst.State) {
				matchedState = inst.State
				return true
			}
			return false
		},
	)
	if err != nil {
		if errors.Is(err, context.Canceled) {
			return err
		}
		return fmt.Errorf("timed out waiting for state %v", targets)
	}
	if shouldNarrate(category) {
		fmt.Fprintf(os.Stderr, "State is now %s.\n", matchedState)
	}
	return nil
}

// waitForReady polls indefinitely until the heartbeat state becomes "ready".
// Returns true if compilation had errors.
// waitForReady polls until the editor returns to Ready or timeoutMs elapses.
// It is bounded by the caller's --timeout (default 60s) rather than a hidden
// multi-minute cap, so a slow or stuck domain reload can't block the CLI long
// enough for a wrapping agent (e.g. Claude Code's 120s bash timeout) to
// background the process. ready=false means it timed out still compiling —
// distinct from a clean compile that produced errors (ready=true, hasErrors=true).
func waitForReady(ctx context.Context, resolve instanceResolver, timeoutMs int, category string) (ready, hasErrors bool, waitErr error) {
	// Guard against a missing/zero timeout (e.g. a caller that never parsed
	// flags) collapsing the deadline to "now" and timing out instantly.
	if timeoutMs <= 0 {
		timeoutMs = 60000
	}
	if shouldNarrate(category) {
		fmt.Fprintf(os.Stderr, "Waiting for compilation...\n")
	}

	err := poll.ExponentialBackoffLoop(
		ctx,
		time.Duration(timeoutMs)*time.Millisecond,
		statusPollBaseInterval,
		1500*time.Millisecond,
		func() bool {
			status, err := resolve()
			if err != nil {
				return false
			}
			if status.State == unitystate.Ready {
				hasErrors = status.CompileErrors
				return true
			}
			return false
		},
	)
	if err != nil {
		if errors.Is(err, context.Canceled) {
			return false, false, err
		}
		if shouldNarrate(category) {
			fmt.Fprintf(os.Stderr, "Still compiling after %ds (timed out waiting).\n", timeoutMs/1000)
		}
		return false, false, nil
	}
	if shouldNarrate(category) {
		if hasErrors {
			fmt.Fprintf(os.Stderr, "Compilation finished with errors.\n")
		} else {
			fmt.Fprintf(os.Stderr, "Compilation complete.\n")
		}
	}
	return true, hasErrors, nil
}

func shouldNarrate(category string) bool {
	return !flagQuiet && (isHumanCommand(category) || flagNarrate)
}
