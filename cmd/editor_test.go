package cmd

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func TestEditorCmd_Play(t *testing.T) {
	send, params := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	if _, err := editorCmd(context.Background(), []string{"play"}, send, resolve, "editor"); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if (*params)["action"] != "play" {
		t.Errorf("expected action=play, got %v", (*params)["action"])
	}
	// Wait confirmation moved Go-side (waitForState polling); no longer sent as a wire param.
	if _, present := (*params)["wait_for_completion"]; present {
		t.Errorf("wait_for_completion should not be sent over HTTP anymore, got %v", (*params)["wait_for_completion"])
	}
}

func TestEditorCmd_PlayWait(t *testing.T) {
	origPollInterval := statusPollBaseInterval
	statusPollBaseInterval = 5 * time.Millisecond
	t.Cleanup(func() { statusPollBaseInterval = origPollInterval })

	send, params := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) {
		return &client.Instance{State: "playing"}, nil
	}
	resp, err := editorCmd(context.Background(), []string{"play", "--wait"}, send, resolve, "editor")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if (*params)["action"] != "play" {
		t.Errorf("expected action=play, got %v", (*params)["action"])
	}
	if resp.Message != "Entered play mode (confirmed)." {
		t.Errorf("expected confirmation message, got %q", resp.Message)
	}
}

func TestEditorCmd_Stop(t *testing.T) {
	send, params := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	if _, err := editorCmd(context.Background(), []string{"stop"}, send, resolve, "editor"); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if (*params)["action"] != "stop" {
		t.Errorf("expected action=stop, got %v", (*params)["action"])
	}
}

func TestEditorCmd_Pause(t *testing.T) {
	send, params := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	if _, err := editorCmd(context.Background(), []string{"pause"}, send, resolve, "editor"); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if (*params)["action"] != "pause" {
		t.Errorf("expected action=pause, got %v", (*params)["action"])
	}
}

func TestEditorCmd_Refresh(t *testing.T) {
	send, _ := mockSend("refresh_unity", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	if _, err := editorCmd(context.Background(), []string{"refresh"}, send, resolve, "editor"); err != nil {
		t.Errorf("unexpected error: %v", err)
	}
}

func TestEditorCmd_RefreshForce(t *testing.T) {
	send, params := mockSend("refresh_unity", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	if _, err := editorCmd(context.Background(), []string{"refresh", "--force"}, send, resolve, "editor"); err != nil {
		t.Errorf("unexpected error: %v", err)
	}
	if (*params)["force"] != true {
		t.Errorf("expected force=true, got %v", (*params)["force"])
	}
	if (*params)["mode"] != "force" {
		t.Errorf("expected mode=force, got %v", (*params)["mode"])
	}
}

func TestEditorCmd_RefreshCompileForce(t *testing.T) {
	send, params := mockSend("refresh_unity", t)
	resolve := func() (*client.Instance, error) {
		return &client.Instance{State: "ready"}, nil
	}
	if _, err := editorCmd(context.Background(), []string{"refresh", "--compile", "--force"}, send, resolve, "editor"); err != nil {
		t.Errorf("unexpected error: %v", err)
	}
	if (*params)["compile"] != "request" {
		t.Errorf("expected compile=request, got %v", (*params)["compile"])
	}
	if (*params)["force"] != true {
		t.Errorf("expected force=true, got %v", (*params)["force"])
	}
	if (*params)["mode"] != "force" {
		t.Errorf("expected mode=force, got %v", (*params)["mode"])
	}
}

func TestEditorCmd_RefreshCompileFailureDoesNotWait(t *testing.T) {
	resolveCalled := false
	send := func(cmd string, params interface{}) (*client.CommandResponse, error) {
		if cmd != "refresh_unity" {
			t.Errorf("send called with command %q, want refresh_unity", cmd)
		}
		return &client.CommandResponse{Success: false, Message: "blocked"}, nil
	}
	resolve := func() (*client.Instance, error) {
		resolveCalled = true
		return &client.Instance{State: "ready"}, nil
	}

	resp, err := editorCmd(context.Background(), []string{"refresh", "--compile"}, send, resolve, "editor")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if resp == nil || resp.Success {
		t.Fatalf("expected failed response, got %+v", resp)
	}
	if resolveCalled {
		t.Error("expected refresh failure to skip compilation wait")
	}
}

func TestEditorCmd_RefreshCompileWaitsForFreshHeartbeatAfterRequest(t *testing.T) {
	// Given
	origPollInterval := statusPollBaseInterval
	statusPollBaseInterval = 5 * time.Millisecond
	t.Cleanup(func() { statusPollBaseInterval = origPollInterval })

	project := "C:/Workspace/Project"
	home := writeInstanceFile(t, client.Instance{
		State:         "ready",
		ProjectPath:   project,
		Port:          8090,
		PID:           os.Getpid(),
		Timestamp:     time.Now().UnixMilli(),
		CompileErrors: true,
	})
	if _, err := client.DiscoverInstance(project, 0); err != nil {
		t.Fatalf("prime stale instance cache: %v", err)
	}

	instancePath := filepath.Join(home, ".hera-agent-unity", "instances", "test.json")
	compiling := client.Instance{
		State:       "compiling",
		ProjectPath: project,
		Port:        8090,
		PID:         os.Getpid(),
		Timestamp:   time.Now().UnixMilli(),
	}
	data, err := json.Marshal(compiling)
	if err != nil {
		t.Fatalf("marshal compiling heartbeat: %v", err)
	}
	if err := os.WriteFile(instancePath, data, 0o644); err != nil {
		t.Fatalf("write compiling heartbeat: %v", err)
	}

	resolve := func() (*client.Instance, error) {
		inst, err := client.DiscoverInstance(project, 0)
		if err != nil || inst.State != "compiling" {
			return inst, err
		}

		ready := *inst
		ready.State = "ready"
		ready.CompileErrors = false
		ready.Timestamp = time.Now().UnixMilli()
		data, err := json.Marshal(ready)
		if err != nil {
			return nil, err
		}
		if err := os.WriteFile(instancePath, data, 0o644); err != nil {
			return nil, err
		}
		client.ClearInstanceCache()
		return inst, nil
	}
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		return &client.CommandResponse{Success: true}, nil
	}

	// When
	resp, err := editorCmd(context.Background(), []string{"refresh", "--compile"}, send, resolve, "editor")

	// Then
	if err != nil {
		t.Fatalf("expected refresh to wait for the fresh heartbeat, got %v", err)
	}
	if resp.Message != "Refresh and compilation completed." {
		t.Fatalf("expected completion message, got %q", resp.Message)
	}
}

func TestEditorCmd_EmptyArgs(t *testing.T) {
	send, _ := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	_, err := editorCmd(context.Background(), nil, send, resolve, "editor")
	if err == nil {
		t.Error("expected error for empty args")
	}
}

func TestEditorCmd_UnknownAction(t *testing.T) {
	send, _ := mockSend("manage_editor", t)
	resolve := func() (*client.Instance, error) { return nil, nil }
	_, err := editorCmd(context.Background(), []string{"fly"}, send, resolve, "editor")
	if err == nil {
		t.Error("expected error for unknown action")
	}
}
