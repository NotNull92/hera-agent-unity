package cmd

import (
	"fmt"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
)

type instanceResolver func() (*client.Instance, error)

var statusPollInterval = 500 * time.Millisecond

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
		fmt.Println(tui.StatusPanel(
			fmt.Sprintf("Unity Editor — port %d", status.Port),
			[][2]string{
				{"State", tui.DotStatus(status.State)},
				{"Project", tui.PathStyle.Render(status.ProjectPath)},
				{"Version", status.UnityVersion},
				{"PID", fmt.Sprintf("%d", status.PID)},
			},
		))
		return nil
	}

	// Plain output — kept stable for script/AI parsing.
	fmt.Printf("Unity (port %d): %s\n", status.Port, status.State)
	fmt.Printf("  Project: %s\n", status.ProjectPath)
	fmt.Printf("  Version: %s\n", status.UnityVersion)
	fmt.Printf("  PID:     %d\n", status.PID)
	return nil
}

// pingCmd is a token-cheap liveness probe for agents. Reads the instance
// heartbeat file directly (no Unity HTTP round-trip), prints a single
// machine-parseable line, exits 0 if alive within 3s otherwise 1.
func pingCmd(project string, port int) error {
	inst, err := discoverStatusInstance(project, port)
	if err != nil {
		fmt.Println("alive=0")
		os.Exit(1)
		return nil
	}
	age := time.Since(time.UnixMilli(inst.Timestamp))
	alive := age <= 3*time.Second && inst.State != "stopped"
	flag := 0
	if alive {
		flag = 1
	}
	fmt.Printf("port=%d alive=%d state=%s age_ms=%d\n",
		inst.Port, flag, inst.State, age.Milliseconds())
	if !alive {
		os.Exit(1)
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
func waitForAlive(resolve instanceResolver, timeoutMs int) (*client.Instance, error) {
	baseline := time.Now().UnixMilli()
	inst, err := resolve()
	if err == nil {
		baseline = inst.Timestamp
		// Already fresh — check if timestamp was updated within the last second
		if time.Now().UnixMilli()-baseline < 1000 {
			return inst, nil
		}
	}

	narrate := !flagQuiet && (isHumanCommand() || flagNarrate)
	if narrate {
		fmt.Fprintf(os.Stderr, "Waiting for Unity...\n")
	}

	deadline := time.Now().Add(time.Duration(timeoutMs) * time.Millisecond)
	for time.Now().Before(deadline) {
		time.Sleep(statusPollInterval)
		inst, err = resolve()
		if err != nil {
			continue
		}
		if inst.Timestamp > baseline {
			if narrate {
				fmt.Fprintf(os.Stderr, "Unity is ready.\n")
			}
			return inst, nil
		}
	}

	return nil, fmt.Errorf("timed out waiting for Unity")
}

// waitForReady polls indefinitely until the heartbeat state becomes "ready".
// Returns true if compilation had errors.
func waitForReady(resolve instanceResolver) bool {
	narrate := !flagQuiet && (isHumanCommand() || flagNarrate)
	if narrate {
		fmt.Fprintf(os.Stderr, "Waiting for compilation...\n")
	}

	deadline := time.Now().Add(5 * time.Minute)
	for time.Now().Before(deadline) {
		time.Sleep(statusPollInterval)
		status, err := resolve()
		if err != nil {
			continue
		}
		if status.State == "ready" {
			if narrate {
				if status.CompileErrors {
					fmt.Fprintf(os.Stderr, "Compilation finished with errors.\n")
				} else {
					fmt.Fprintf(os.Stderr, "Compilation complete.\n")
				}
			}
			return status.CompileErrors
		}
	}

	if narrate {
		fmt.Fprintf(os.Stderr, "Timed out waiting for compilation (5m).\n")
	}
	return true
}
