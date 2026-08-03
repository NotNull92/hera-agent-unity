package client

import (
	"os"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

func TestScanInstancesFresh_RetriesTransientHeartbeatReadFailure(t *testing.T) {
	// Given
	home := writeInstanceFiles(t, map[string]Instance{
		"live.json": {
			State:       unitystate.Ready,
			ProjectPath: "/projects/live",
			Port:        8095,
			PID:         100,
			Timestamp:   time.Now().UnixMilli(),
		},
	})
	t.Setenv("HOME", home)
	client := NewClient()
	client.processDeadChecker = func(int) bool { return false }
	readCalls := 0
	client.readFile = func(path string) ([]byte, error) {
		readCalls++
		if readCalls == 1 {
			return nil, os.ErrNotExist
		}
		return os.ReadFile(path)
	}
	client.sleep = func(time.Duration) {}

	// When
	instances, err := client.ScanInstancesFresh()

	// Then
	if err != nil {
		t.Fatalf("ScanInstancesFresh() error = %v", err)
	}
	if len(instances) != 1 {
		t.Fatalf("ScanInstancesFresh() returned %d instances, want 1", len(instances))
	}
	if instances[0].ProjectPath != "/projects/live" {
		t.Fatalf("ProjectPath = %q, want %q", instances[0].ProjectPath, "/projects/live")
	}
}

func TestScanInstancesFresh_RetriesTransientHeartbeatDecodeFailure(t *testing.T) {
	// Given
	home := writeInstanceFiles(t, map[string]Instance{
		"live.json": {
			State:       unitystate.Ready,
			ProjectPath: "/projects/live",
			Port:        8095,
			PID:         100,
			Timestamp:   time.Now().UnixMilli(),
		},
	})
	t.Setenv("HOME", home)
	client := NewClient()
	client.processDeadChecker = func(int) bool { return false }
	readCalls := 0
	client.readFile = func(path string) ([]byte, error) {
		readCalls++
		if readCalls == 1 {
			return []byte("{"), nil
		}
		return os.ReadFile(path)
	}
	client.sleep = func(time.Duration) {}

	// When
	instances, err := client.ScanInstancesFresh()

	// Then
	if err != nil {
		t.Fatalf("ScanInstancesFresh() error = %v", err)
	}
	if len(instances) != 1 {
		t.Fatalf("ScanInstancesFresh() returned %d instances, want 1", len(instances))
	}
	if instances[0].ProjectPath != "/projects/live" {
		t.Fatalf("ProjectPath = %q, want %q", instances[0].ProjectPath, "/projects/live")
	}
}
