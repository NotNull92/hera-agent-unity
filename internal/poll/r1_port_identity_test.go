package poll

import (
	"context"
	"encoding/json"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func TestR1LivenessAfterPortChange(t *testing.T) {
	for _, scenario := range []string{"foreign-stopped", "target-stopped", "normalized-stopped", "substring-stopped", "legacy-stopped", "restart", "dead-before-first-tick"} {
		t.Run(scenario, func(t *testing.T) {
			fixtureHome := t.TempDir()
			t.Setenv("HOME", fixtureHome)
			t.Setenv("USERPROFILE", fixtureHome)
			if err := os.MkdirAll(paths.InstancesDir(), 0700); err != nil {
				t.Fatal(err)
			}
			target := client.Instance{ProjectPath: filepath.Join(fixtureHome, "Project"), Port: 8090, PID: os.Getpid(), State: "ready", Timestamp: time.Now().UnixMilli()}
			current := target
			current.Port = 8091
			write := func(name string, inst client.Instance) {
				t.Helper()
				data, err := json.Marshal(inst)
				if err != nil {
					t.Fatal(err)
				}
				if err = os.WriteFile(filepath.Join(paths.InstancesDir(), name+".json"), data, 0600); err != nil {
					t.Fatal(err)
				}
			}
			want := ""
			timeout := 350 * time.Millisecond
			switch scenario {
			case "foreign-stopped":
				foreign := target
				foreign.ProjectPath += "-other"
				foreign.State = "stopped"
				write("foreign", foreign)
			case "target-stopped":
				current.State = "stopped"
				want = "stopped"
			case "normalized-stopped":
				current.ProjectPath += string(os.PathSeparator) + "."
				current.State = "stopped"
				want = "stopped"
			case "substring-stopped":
				current.ProjectPath += "-other"
				current.State = "stopped"
			case "legacy-stopped":
				target.ProjectPath = ""
				current.Port = target.Port
				current.State = "stopped"
				want = "stopped"
			case "restart":
				target.PID = os.Getpid() + 100000
				want = "restarted"
			case "dead-before-first-tick":
				child := exec.Command(os.Args[0], "-test.run=^$")
				if err := child.Run(); err != nil {
					t.Fatal(err)
				}
				target.PID = child.Process.Pid
				current.PID = target.PID
				want = "no longer running"
				timeout = 7 * time.Second
			}
			write("target", current)
			_, err := WaitForFile(context.Background(), filepath.Join(fixtureHome, "absent"), target, timeout, "r1")
			if want == "" {
				if !errors.Is(err, ErrWaitTimeout) {
					t.Fatalf("unrelated port owner affected wait: %v", err)
				}
			} else if err == nil || !strings.Contains(err.Error(), want) {
				t.Fatalf("want %q, got %v", want, err)
			}
		})
	}
}
