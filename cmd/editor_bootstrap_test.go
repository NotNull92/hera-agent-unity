package cmd

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func TestEditorBootstrapLaunchWaitsForExactNewHeartbeat(t *testing.T) {
	project, hubRoot, executable := writeBootstrapProject(t, "6000.3.5f2")
	originalInterval := editorBootstrapPollInterval
	editorBootstrapPollInterval = time.Millisecond
	t.Cleanup(func() { editorBootstrapPollInterval = originalInterval })

	started := false
	runtime := editorBootstrapRuntime{
		Scan: func() ([]client.Instance, error) {
			if !started {
				return nil, nil
			}
			return []client.Instance{
				{ProjectPath: filepath.Join(filepath.Dir(project), "Other"), PID: 222, Port: 8091, State: "ready", Timestamp: 2},
				{ProjectPath: project, PID: 222, Port: 8092, State: "compiling", Timestamp: 3},
			}, nil
		},
		Start: func(gotExecutable, gotProject string) (int, error) {
			if gotExecutable != executable || gotProject != project {
				t.Fatalf("start args = %q, %q", gotExecutable, gotProject)
			}
			started = true
			return 222, nil
		},
		Stop:   func(int) error { t.Fatal("launch must not stop a process"); return nil },
		Dead:   func(int) bool { return false },
		Remove: func(string) error { t.Fatal("launch must not remove a lock"); return nil },
	}

	resp, err := runEditorBootstrap(context.Background(), []string{"launch", "--hub-root", hubRoot}, GlobalConfig{
		Project: project,
		Timeout: 100 * time.Millisecond,
	}, runtime)
	if err != nil {
		t.Fatal(err)
	}
	if !resp.Success || resp.Code != "" {
		t.Fatalf("response = %#v", resp)
	}
	if !started {
		t.Fatal("Unity process was not started")
	}
}

func TestEditorBootstrapRestartStopsOnlyExactProjectAndRemovesItsLock(t *testing.T) {
	project, hubRoot, _ := writeBootstrapProject(t, "6000.3.5f2")
	originalInterval := editorBootstrapPollInterval
	editorBootstrapPollInterval = time.Millisecond
	t.Cleanup(func() { editorBootstrapPollInterval = originalInterval })

	started := false
	stoppedPID := 0
	removedPath := ""
	runtime := editorBootstrapRuntime{
		Scan: func() ([]client.Instance, error) {
			if started {
				return []client.Instance{{ProjectPath: project, PID: 333, Port: 8094, State: "ready", Timestamp: 4}}, nil
			}
			return []client.Instance{
				{ProjectPath: filepath.Join(filepath.Dir(project), "Other"), PID: 111, Port: 8091, State: "ready", Timestamp: 9},
				{ProjectPath: project, PID: 222, Port: 8092, State: "ready", Timestamp: 3},
			}, nil
		},
		Start: func(_, gotProject string) (int, error) {
			if gotProject != project {
				t.Fatalf("start project = %q", gotProject)
			}
			started = true
			return 333, nil
		},
		Stop: func(pid int) error {
			stoppedPID = pid
			return nil
		},
		Dead: func(pid int) bool { return pid == stoppedPID },
		Remove: func(path string) error {
			removedPath = path
			return os.ErrNotExist
		},
	}

	resp, err := runEditorBootstrap(context.Background(), []string{"restart", "--hub-root", hubRoot}, GlobalConfig{
		Project: project,
		Timeout: 100 * time.Millisecond,
	}, runtime)
	if err != nil {
		t.Fatal(err)
	}
	if !resp.Success {
		t.Fatalf("response = %#v", resp)
	}
	if stoppedPID != 222 {
		t.Fatalf("stopped pid = %d, want exact project pid 222", stoppedPID)
	}
	if removedPath != filepath.Join(project, "Temp", "UnityLockfile") {
		t.Fatalf("removed path = %q", removedPath)
	}
}

func TestEditorBootstrapRejectsPortSelectorBeforeMutation(t *testing.T) {
	called := false
	resp, err := runEditorBootstrap(context.Background(), []string{"restart"}, GlobalConfig{
		Project: "C:/Project",
		Port:    8090,
		Timeout: time.Second,
	}, editorBootstrapRuntime{Scan: func() ([]client.Instance, error) {
		called = true
		return nil, nil
	}})
	if err != nil {
		t.Fatal(err)
	}
	if resp.Success || resp.Code != "EDITOR_PROJECT_REQUIRED" {
		t.Fatalf("response = %#v", resp)
	}
	if called {
		t.Fatal("runtime was touched before selector validation")
	}
}

func TestUnityEditorArgumentsUseNormalPackageManagerStartup(t *testing.T) {
	args := unityEditorArguments("C:/Project")
	if len(args) != 2 || args[0] != "-projectPath" || args[1] != "C:/Project" {
		t.Fatalf("Unity args = %q", args)
	}
}

func writeBootstrapProject(t *testing.T, version string) (string, string, string) {
	t.Helper()
	project := filepath.Join(t.TempDir(), "Project")
	if err := os.MkdirAll(filepath.Join(project, "ProjectSettings"), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(project, "ProjectSettings", "ProjectVersion.txt"), []byte("m_EditorVersion: "+version+"\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	hubRoot := filepath.Join(t.TempDir(), "Hub", "Editor")
	var executable string
	switch runtime.GOOS {
	case "windows":
		executable = filepath.Join(hubRoot, version, "Editor", "Unity.exe")
	case "darwin":
		executable = filepath.Join(hubRoot, version, "Unity.app", "Contents", "MacOS", "Unity")
	default:
		executable = filepath.Join(hubRoot, version, "Editor", "Unity")
	}
	if err := os.MkdirAll(filepath.Dir(executable), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(executable, []byte("test"), 0o755); err != nil {
		t.Fatal(err)
	}
	return filepath.Clean(project), hubRoot, filepath.Clean(executable)
}

func TestEditorRestartDoesNotWarnAboutALockTheExitingEditorStillHeld(t *testing.T) {
	// Given: the first removal loses the race with the exiting Editor's handle,
	// which is what Windows reports right after the process dies.
	project, hubRoot, _ := writeBootstrapProject(t, "6000.3.5f2")
	originalInterval := editorBootstrapPollInterval
	editorBootstrapPollInterval = time.Millisecond
	t.Cleanup(func() { editorBootstrapPollInterval = originalInterval })

	started := false
	stoppedPID := 0
	removeCalls := 0
	runtime := editorBootstrapRuntime{
		Scan: func() ([]client.Instance, error) {
			if started {
				return []client.Instance{{ProjectPath: project, PID: 333, Port: 8094, State: "ready", Timestamp: 4}}, nil
			}
			return []client.Instance{{ProjectPath: project, PID: 222, Port: 8092, State: "ready", Timestamp: 3}}, nil
		},
		Start: func(string, string) (int, error) { started = true; return 333, nil },
		Stop:  func(pid int) error { stoppedPID = pid; return nil },
		Dead:  func(pid int) bool { return pid == stoppedPID },
		Remove: func(string) error {
			removeCalls++
			if removeCalls == 1 {
				return fmt.Errorf("The process cannot access the file because it is being used by another process.")
			}
			return os.ErrNotExist
		},
	}

	// When
	resp, err := runEditorBootstrap(context.Background(), []string{"restart", "--hub-root", hubRoot}, GlobalConfig{
		Project: project,
		Timeout: time.Second,
	}, runtime)

	// Then: the lock did release, so the restart reports no warning.
	if err != nil {
		t.Fatal(err)
	}
	var data editorBootstrapData
	if unmarshalErr := json.Unmarshal(resp.Data, &data); unmarshalErr != nil {
		t.Fatal(unmarshalErr)
	}
	if data.LockWarning != "" {
		t.Fatalf("lock_warning = %q, want none once the lock released", data.LockWarning)
	}
	if removeCalls < 2 {
		t.Fatalf("remove calls = %d, want a retry after the first failure", removeCalls)
	}
}

func TestEditorRestartStillWarnsAboutALockThatNeverReleases(t *testing.T) {
	// Given: removal keeps failing for the whole release window.
	project, hubRoot, _ := writeBootstrapProject(t, "6000.3.5f2")
	originalInterval := editorBootstrapPollInterval
	editorBootstrapPollInterval = time.Millisecond
	t.Cleanup(func() { editorBootstrapPollInterval = originalInterval })
	originalRelease := editorLockReleaseTimeout
	editorLockReleaseTimeout = 10 * time.Millisecond
	t.Cleanup(func() { editorLockReleaseTimeout = originalRelease })

	started := false
	stoppedPID := 0
	runtime := editorBootstrapRuntime{
		Scan: func() ([]client.Instance, error) {
			if started {
				return []client.Instance{{ProjectPath: project, PID: 333, Port: 8094, State: "ready", Timestamp: 4}}, nil
			}
			return []client.Instance{{ProjectPath: project, PID: 222, Port: 8092, State: "ready", Timestamp: 3}}, nil
		},
		Start:  func(string, string) (int, error) { started = true; return 333, nil },
		Stop:   func(pid int) error { stoppedPID = pid; return nil },
		Dead:   func(pid int) bool { return pid == stoppedPID },
		Remove: func(string) error { return fmt.Errorf("locked by another process") },
	}

	// When
	resp, err := runEditorBootstrap(context.Background(), []string{"restart", "--hub-root", hubRoot}, GlobalConfig{
		Project: project,
		Timeout: time.Second,
	}, runtime)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	var data editorBootstrapData
	if unmarshalErr := json.Unmarshal(resp.Data, &data); unmarshalErr != nil {
		t.Fatal(unmarshalErr)
	}
	if data.LockWarning == "" {
		t.Fatal("lock_warning is empty, want the surviving lock reported")
	}
}
