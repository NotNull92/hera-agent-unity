package cmd

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

var editorBootstrapPollInterval = 250 * time.Millisecond

// Windows can keep the project lock handle open for a moment after the owning
// process dies, so a lock is only stale once it survives this window.
var editorLockReleaseTimeout = 3 * time.Second

type editorBootstrapRuntime struct {
	Scan   func() ([]client.Instance, error)
	Start  func(executable, project string) (int, error)
	Stop   func(pid int) error
	Dead   func(pid int) bool
	Remove func(path string) error
}

type editorBootstrapData struct {
	Action        string `json:"action"`
	ProjectPath   string `json:"project_path"`
	UnityVersion  string `json:"unity_version"`
	EditorPath    string `json:"editor_path"`
	PID           int    `json:"pid"`
	PreviousPID   int    `json:"previous_pid,omitempty"`
	Port          int    `json:"port"`
	State         string `json:"state"`
	HeartbeatSeen bool   `json:"heartbeat_seen"`
	LockWarning   string `json:"lock_warning,omitempty"`
}

func defaultEditorBootstrapRuntime() editorBootstrapRuntime {
	return editorBootstrapRuntime{
		Scan:   client.ScanInstancesFresh,
		Start:  startUnityEditor,
		Stop:   stopUnityEditor,
		Dead:   client.IsProcessDead,
		Remove: os.Remove,
	}
}

func isEditorBootstrapAction(args []string) bool {
	return len(args) > 0 && (args[0] == "launch" || args[0] == "restart")
}

func runEditorBootstrap(
	ctx context.Context,
	args []string,
	config GlobalConfig,
	runtime editorBootstrapRuntime,
) (*client.CommandResponse, error) {
	action := args[0]
	hubRoot, err := parseEditorBootstrapFlags(args[1:])
	if err != nil {
		return editorBootstrapFailure("EDITOR_BOOTSTRAP_INVALID", err.Error()), nil
	}
	if strings.TrimSpace(config.Project) == "" {
		return editorBootstrapFailure(
			"EDITOR_PROJECT_REQUIRED",
			"editor "+action+" requires --project with a Unity project path",
		), nil
	}
	if config.Port != 0 {
		return editorBootstrapFailure(
			"EDITOR_PROJECT_REQUIRED",
			"editor "+action+" selects by exact --project path; --port is not valid before startup",
		), nil
	}

	project, version, err := readUnityProjectVersion(config.Project)
	if err != nil {
		return editorBootstrapFailure("UNITY_PROJECT_INVALID", err.Error()), nil
	}
	executable, err := resolveUnityExecutable(version, hubRoot)
	if err != nil {
		return editorBootstrapFailure("UNITY_EDITOR_NOT_FOUND", err.Error()), nil
	}

	instances, err := runtime.Scan()
	if err != nil && !errors.Is(err, os.ErrNotExist) {
		return editorBootstrapFailure("EDITOR_DISCOVERY_FAILED", err.Error()), nil
	}
	running := findExactProjectInstance(instances, project, 0)
	previousPID := 0
	lockWarning := ""
	if action == "launch" && running != nil {
		return editorBootstrapFailure(
			"EDITOR_ALREADY_RUNNING",
			fmt.Sprintf("Unity is already running for %s (pid=%d, port=%d); use editor restart", project, running.PID, running.Port),
		), nil
	}
	if action == "restart" {
		if running == nil {
			return editorBootstrapFailure(
				"EDITOR_NOT_RUNNING",
				"no running Unity Editor heartbeat matches the exact project; use editor launch",
			), nil
		}
		previousPID = running.PID
	}

	deadline, cancel := context.WithTimeout(ctx, config.Timeout)
	defer cancel()
	if previousPID > 0 {
		if err := runtime.Stop(previousPID); err != nil {
			return editorBootstrapFailure("EDITOR_STOP_FAILED", fmt.Sprintf("stop Unity pid %d: %v", previousPID, err)), nil
		}
		if err := waitForEditorCondition(deadline, func() bool { return runtime.Dead(previousPID) }); err != nil {
			return editorBootstrapFailure("EDITOR_STOP_TIMEOUT", fmt.Sprintf("Unity pid %d did not exit before timeout", previousPID)), nil
		}
		lockPath := filepath.Join(project, "Temp", "UnityLockfile")
		if err := runtime.Remove(lockPath); err != nil && !errors.Is(err, os.ErrNotExist) {
			// The exiting Editor may still hold the handle, so the first
			// failure says nothing about the lock being stuck. Warn only about
			// one that outlives the release window.
			lockCtx, cancelLock := context.WithTimeout(deadline, editorLockReleaseTimeout)
			waitErr := waitForEditorCondition(lockCtx, func() bool {
				retryErr := runtime.Remove(lockPath)
				return retryErr == nil || errors.Is(retryErr, os.ErrNotExist)
			})
			cancelLock()
			if waitErr != nil {
				lockWarning = fmt.Sprintf("could not remove stale project lock: %v", err)
			}
		}
	}

	pid, err := runtime.Start(executable, project)
	if err != nil {
		return editorBootstrapFailure("EDITOR_LAUNCH_FAILED", fmt.Sprintf("start Unity %s: %v", version, err)), nil
	}
	var heartbeat *client.Instance
	err = waitForEditorCondition(deadline, func() bool {
		instances, scanErr := runtime.Scan()
		if scanErr != nil {
			return false
		}
		heartbeat = findExactProjectInstance(instances, project, pid)
		return heartbeat != nil
	})
	if err != nil {
		data := editorBootstrapData{
			Action: action, ProjectPath: project, UnityVersion: version,
			EditorPath: executable, PID: pid, PreviousPID: previousPID,
			LockWarning: lockWarning,
		}
		return editorBootstrapResponse(false, "EDITOR_HEARTBEAT_TIMEOUT",
			fmt.Sprintf("Unity pid %d started, but no matching project heartbeat arrived before timeout; do not launch it again blindly", pid), data), nil
	}

	data := editorBootstrapData{
		Action: action, ProjectPath: project, UnityVersion: version,
		EditorPath: executable, PID: pid, PreviousPID: previousPID,
		Port: heartbeat.Port, State: heartbeat.State, HeartbeatSeen: true,
		LockWarning: lockWarning,
	}
	return editorBootstrapResponse(true, "", fmt.Sprintf("Unity Editor %s completed and the project heartbeat is available.", action), data), nil
}

func findExactProjectInstance(instances []client.Instance, project string, pid int) *client.Instance {
	var latest *client.Instance
	for i := range instances {
		instance := instances[i]
		if instance.State == unitystate.Stopped || instance.Timestamp <= 0 || !sameProjectPath(instance.ProjectPath, project) {
			continue
		}
		if pid > 0 && instance.PID != pid {
			continue
		}
		if latest == nil || instance.Timestamp > latest.Timestamp {
			candidate := instance
			latest = &candidate
		}
	}
	return latest
}

func sameProjectPath(left, right string) bool {
	left = filepath.Clean(filepath.FromSlash(strings.TrimSpace(left)))
	right = filepath.Clean(filepath.FromSlash(strings.TrimSpace(right)))
	if runtime.GOOS == "windows" {
		return strings.EqualFold(left, right)
	}
	return left == right
}

func waitForEditorCondition(ctx context.Context, ready func() bool) error {
	if ready() {
		return nil
	}
	ticker := time.NewTicker(editorBootstrapPollInterval)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-ticker.C:
			if ready() {
				return nil
			}
		}
	}
}

func editorBootstrapFailure(code, message string) *client.CommandResponse {
	return &client.CommandResponse{Success: false, Code: code, Message: message}
}

func editorBootstrapResponse(success bool, code, message string, data editorBootstrapData) *client.CommandResponse {
	payload, _ := json.Marshal(data)
	return &client.CommandResponse{Success: success, Code: code, Message: message, Data: payload}
}
