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
	for _, scenario := range []string{"foreign-stopped", "target-stopped", "normalized-stopped", "substring-stopped", "legacy-stopped", "legacy-pid-stopped", "restart", "dead-before-first-tick", "dead-no-heartbeat"} {
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
			case "legacy-pid-stopped":
				target.PID = 0
				current.State = "stopped"
				want = "stopped"
			case "restart":
				child := exec.Command(os.Args[0], "-test.run=^$")
				if err := child.Run(); err != nil {
					t.Fatal(err)
				}
				target.PID = child.Process.Pid
				want = "restarted"
			case "dead-before-first-tick", "dead-no-heartbeat":
				child := exec.Command(os.Args[0], "-test.run=^$")
				if err := child.Run(); err != nil {
					t.Fatal(err)
				}
				target.PID = child.Process.Pid
				current.PID = target.PID
				want = "no longer running"
				timeout = 7 * time.Second
			}
			if scenario != "dead-no-heartbeat" {
				write("target", current)
			}
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

func TestR1bIgnoresWorkerHeartbeat(t *testing.T) {
	for _, state := range []string{"ready", "stopped"} {
		t.Run(state, func(t *testing.T) {
			fixtureHome := t.TempDir()
			t.Setenv("HOME", fixtureHome)
			t.Setenv("USERPROFILE", fixtureHome)
			if err := os.MkdirAll(paths.InstancesDir(), 0700); err != nil {
				t.Fatal(err)
			}
			target := client.Instance{ProjectPath: filepath.Join(fixtureHome, "Project"), Port: 8090, PID: os.Getpid()}
			worker := target
			worker.PID = os.Getppid()
			worker.Port = 8091
			worker.State = state
			worker.Timestamp = time.Now().UnixMilli()
			if worker.PID <= 0 || worker.PID == target.PID || client.IsProcessDead(worker.PID) {
				t.Fatal("fixture requires a distinct live parent process")
			}
			data, err := json.Marshal(worker)
			if err != nil {
				t.Fatal(err)
			}
			if err := os.WriteFile(filepath.Join(paths.InstancesDir(), "project.json"), data, 0600); err != nil {
				t.Fatal(err)
			}
			resultPath := filepath.Join(fixtureHome, "dispatch-result.json")
			written := make(chan error, 1)
			time.AfterFunc(500*time.Millisecond, func() {
				written <- os.WriteFile(resultPath, []byte(`{"success":true,"message":"original result"}`), 0600)
			})
			resp, waitErr := WaitForFile(context.Background(), resultPath, target, 3*time.Second, "r1b")
			if err := <-written; err != nil {
				t.Fatal(err)
			}
			if waitErr != nil || resp == nil || !resp.Success || resp.Message != "original result" {
				t.Fatalf("worker heartbeat must not end the original wait: response=%+v err=%v", resp, waitErr)
			}
		})
	}
}
