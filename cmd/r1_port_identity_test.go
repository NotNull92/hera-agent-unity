package cmd

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func TestR1ResumeAfterPortChange(t *testing.T) {
	for _, pending := range []bool{false, true} {
		t.Run(map[bool]string{false: "result", true: "pending"}[pending], func(t *testing.T) {
			fixtureHome := t.TempDir()
			t.Setenv("HOME", fixtureHome)
			t.Setenv("USERPROFILE", fixtureHome)
			if err := os.MkdirAll(paths.StatusDir(), 0700); err != nil {
				t.Fatal(err)
			}
			path := paths.TestResultPath(8090, "r1-run")
			body := `{"success":true,"message":"resumed"}`
			timeout := time.Second
			if pending {
				path = paths.TestPendingPath(8090, "r1-run")
				body = `{}`
				timeout = 0
			}
			if err := os.WriteFile(path, []byte(body), 0600); err != nil {
				t.Fatal(err)
			}
			resolve := func() (*client.Instance, error) {
				return &client.Instance{Port: 8091, ProjectPath: filepath.Join(fixtureHome, "Project One"), PID: os.Getpid()}, nil
			}
			resp, err := testCmd(context.Background(), []string{"--resume", "r1-run"}, func(string, any) (*client.CommandResponse, error) { t.Fatal("resume dispatched"); return nil, nil }, resolve, timeout)
			if err != nil {
				t.Fatal(err)
			}
			if pending {
				if resp.Code != "TEST_RUN_PENDING" || !strings.Contains(resp.Suggestions[0], `--project "`) || strings.Contains(resp.Suggestions[0], "--port") {
					t.Fatalf("response=%+v", resp)
				}
			} else if !resp.Success {
				t.Fatalf("response=%+v", resp)
			}
		})
	}
}

func TestR1PendingSuggestionUsesProject(t *testing.T) {
	fixtureHome := t.TempDir()
	t.Setenv("HOME", fixtureHome)
	t.Setenv("USERPROFILE", fixtureHome)
	if err := os.MkdirAll(paths.StatusDir(), 0700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(paths.TestPendingPath(8090, "r1-hint"), []byte(`{}`), 0600); err != nil {
		t.Fatal(err)
	}
	resolve := func() (*client.Instance, error) {
		return &client.Instance{Port: 8090, ProjectPath: filepath.Join(fixtureHome, "Project One")}, nil
	}
	resp, err := testCmd(context.Background(), []string{"--resume", "r1-hint"}, nil, resolve, 0)
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(resp.Suggestions[0], `--project "`) || strings.Contains(resp.Suggestions[0], "--port") {
		t.Fatalf("suggestion=%s", resp.Suggestions[0])
	}
}

func TestR1ResumePortAmbiguityAndLegacy(t *testing.T) {
	for _, scenario := range []string{"none", "both-same-port", "different-ports", "suffix-collision"} {
		t.Run(scenario, func(t *testing.T) {
			fixtureHome := t.TempDir()
			t.Setenv("HOME", fixtureHome)
			t.Setenv("USERPROFILE", fixtureHome)
			if err := os.MkdirAll(paths.StatusDir(), 0700); err != nil {
				t.Fatal(err)
			}
			files := []string{}
			want := 8091
			switch scenario {
			case "both-same-port":
				files = []string{paths.TestResultPath(8090, "r1"), paths.TestPendingPath(8090, "r1")}
				want = 8090
			case "different-ports":
				files = []string{paths.TestResultPath(8090, "r1"), paths.TestPendingPath(8092, "r1")}
			case "suffix-collision":
				files = []string{paths.TestResultPath(8090, "other-r1")}
			}
			for _, file := range files {
				if err := os.WriteFile(file, []byte(`{}`), 0600); err != nil {
					t.Fatal(err)
				}
			}
			got, err := testResumePort("r1", 8091)
			if scenario == "different-ports" {
				if err == nil || !strings.Contains(err.Error(), "ambiguous") {
					t.Fatalf("port=%d err=%v", got, err)
				}
				return
			}
			if err != nil || got != want {
				t.Fatalf("port=%d want=%d err=%v", got, want, err)
			}
		})
	}
}

func TestR1DispatchKeepsSelectedIdentity(t *testing.T) {
	for _, category := range []string{"test", "build", "manage_packages"} {
		t.Run(category, func(t *testing.T) {
			fixtureHome := t.TempDir()
			t.Setenv("HOME", fixtureHome)
			t.Setenv("USERPROFILE", fixtureHome)
			if err := os.MkdirAll(paths.InstancesDir(), 0700); err != nil {
				t.Fatal(err)
			}
			target := client.Instance{ProjectPath: filepath.Join(fixtureHome, "selected"), Port: 8090, PID: os.Getpid()}
			moved := target
			moved.Port = 8091
			moved.State = "stopped"
			moved.Timestamp = time.Now().UnixMilli()
			data, err := json.Marshal(moved)
			if err != nil {
				t.Fatal(err)
			}
			if err = os.WriteFile(filepath.Join(paths.InstancesDir(), "selected.json"), data, 0600); err != nil {
				t.Fatal(err)
			}
			runner := unityCommandRunner{instance: &target, config: GlobalConfig{Timeout: time.Second},
				resolve: func() (*client.Instance, error) {
					t.Fatal("must use dispatch instance, not rediscover")
					return nil, nil
				},
				send: func(string, any) (*client.CommandResponse, error) {
					return &client.CommandResponse{Success: true, Message: "running", Data: json.RawMessage(`{"port":8090,"run_id":"r1","job_id":"r1"}`)}, nil
				},
			}
			args := []string{}
			if category == "build" {
				args = []string{"start", "--wait"}
			}
			_, err = runner.Run(context.Background(), category, args)
			if err == nil || !strings.Contains(err.Error(), "stopped") {
				t.Fatalf("want selected editor stopped, got %v", err)
			}
		})
	}
}
