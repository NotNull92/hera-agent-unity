package cmd

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func TestTestCmd_pollsResultFile_whenModeIsEditMode(t *testing.T) {
	// Given
	const port = 39123
	const runID = "test-run-39123"
	resultPath := paths.TestResultPath(port, runID)
	if err := os.MkdirAll(filepath.Dir(resultPath), 0o700); err != nil {
		t.Fatalf("create result directory: %v", err)
	}
	if err := os.WriteFile(resultPath, []byte(`{"success":true,"message":"All 1 test(s) passed.","data":{"total":1,"passed":1}}`), 0o600); err != nil {
		t.Fatalf("write result file: %v", err)
	}
	t.Cleanup(func() { _ = os.Remove(resultPath) })

	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		if command != "run_tests" {
			t.Fatalf("command = %q, want run_tests", command)
		}
		values, ok := params.(map[string]interface{})
		if !ok || values["async_results"] != true {
			t.Fatalf("async_results = %v, want true", values["async_results"])
		}
		return &client.CommandResponse{
			Success: true,
			Message: "running",
			Data:    json.RawMessage(`{"port":39123,"run_id":"test-run-39123"}`),
		}, nil
	}
	resolve := func() (*client.Instance, error) {
		return &client.Instance{Port: port}, nil
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()

	// When
	response, err := testCmd(ctx, []string{"--mode", "EditMode"}, send, resolve, time.Second)

	// Then
	if err != nil {
		t.Fatalf("testCmd: %v", err)
	}
	if response.Message != "All 1 test(s) passed." {
		t.Fatalf("response message = %q", response.Message)
	}
}

func TestTestCmd_returnsTimeout_whenResultFileDoesNotArrive(t *testing.T) {
	// Given
	const port = 39124
	const runID = "test-run-39124"
	pendingPath := paths.TestPendingPath(port, runID)
	if err := os.MkdirAll(filepath.Dir(pendingPath), 0o700); err != nil {
		t.Fatalf("create pending directory: %v", err)
	}
	if err := os.WriteFile(pendingPath, []byte(`{"port":39124,"run_id":"test-run-39124"}`), 0o600); err != nil {
		t.Fatalf("write pending file: %v", err)
	}
	t.Cleanup(func() { _ = os.Remove(pendingPath) })

	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		return &client.CommandResponse{
			Success: true,
			Message: "running",
			Data:    json.RawMessage(`{"port":39124,"run_id":"test-run-39124"}`),
		}, nil
	}
	resolve := func() (*client.Instance, error) {
		return &client.Instance{Port: 39124}, nil
	}

	// When
	response, err := testCmd(context.Background(), []string{"--mode", "EditMode"}, send, resolve, 0)

	// Then
	if err != nil {
		t.Fatalf("testCmd: %v", err)
	}
	if response.Success {
		t.Fatal("response success = true, want pending result")
	}
	if response.Code != "TEST_RUN_PENDING" {
		t.Fatalf("response code = %q, want TEST_RUN_PENDING", response.Code)
	}
	var data struct {
		Port  int    `json:"port"`
		RunID string `json:"run_id"`
	}
	if err := json.Unmarshal(response.Data, &data); err != nil {
		t.Fatalf("decode response data: %v", err)
	}
	if data.Port != port || data.RunID != runID {
		t.Fatalf("response data = %+v, want port=%d run_id=%q", data, port, runID)
	}
	if len(response.Suggestions) == 0 {
		t.Fatal("response suggestions are empty")
	}
	if _, err := os.Stat(pendingPath); err != nil {
		t.Fatalf("pending file must remain resumable: %v", err)
	}
}

func TestTestCmd_resumesExistingRun_withoutStartingAnotherTest(t *testing.T) {
	// Given
	const port = 39127
	const runID = "test-run-39127"
	resultPath := paths.TestResultPath(port, runID)
	if err := os.MkdirAll(filepath.Dir(resultPath), 0o700); err != nil {
		t.Fatalf("create result directory: %v", err)
	}
	if err := os.WriteFile(resultPath, []byte(`{"success":true,"message":"Resumed result","data":{"total":1,"passed":1}}`), 0o600); err != nil {
		t.Fatalf("write result file: %v", err)
	}
	t.Cleanup(func() { _ = os.Remove(resultPath) })

	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		t.Fatalf("send called while resuming: command=%q params=%v", command, params)
		return nil, nil
	}
	resolve := func() (*client.Instance, error) {
		return &client.Instance{Port: port}, nil
	}

	// When
	response, err := testCmd(context.Background(), []string{"--resume", runID}, send, resolve, time.Second)

	// Then
	if err == nil {
		if response.Message != "Resumed result" {
			t.Fatalf("response message = %q, want Resumed result", response.Message)
		}
		return
	}
	t.Fatalf("testCmd: %v", err)
}

func TestIsTestResume_skipsUnityHeartbeatWait_onlyForResume(t *testing.T) {
	// Given
	tests := []struct {
		name     string
		category string
		args     []string
		want     bool
	}{
		{name: "resume test", category: "test", args: []string{"--resume", "run-1"}, want: true},
		{name: "new test", category: "test", args: []string{"--mode", "EditMode"}, want: false},
		{name: "different command", category: "console", args: []string{"--resume", "run-1"}, want: false},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			// When
			got := isTestResume(tt.category, tt.args)

			// Then
			if got != tt.want {
				t.Fatalf("isTestResume() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestTestCmd_rejectsInvalidResumeRunID_withoutStartingTest(t *testing.T) {
	// Given
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		t.Fatalf("send called for invalid resume: command=%q params=%v", command, params)
		return nil, nil
	}

	// When
	_, err := testCmd(context.Background(), []string{"--resume", "../../other-file"}, send, nil, time.Second)

	// Then
	if err == nil {
		t.Fatal("testCmd error = nil, want invalid run_id error")
	}
}

func TestTestCmd_pollsLegacyResultFile_whenRunIDIsMissing(t *testing.T) {
	const port = 39125
	resultPath := paths.LegacyTestResultPath(port)
	if err := os.MkdirAll(filepath.Dir(resultPath), 0o700); err != nil {
		t.Fatalf("create result directory: %v", err)
	}
	if err := os.WriteFile(resultPath, []byte(`{"success":true,"message":"Legacy result"}`), 0o600); err != nil {
		t.Fatalf("write legacy result file: %v", err)
	}
	t.Cleanup(func() { _ = os.Remove(resultPath) })

	response := &client.CommandResponse{
		Success: true,
		Message: "running",
		Data:    json.RawMessage(`{"port":39125}`),
	}
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		return response, nil
	}

	got, err := testCmd(context.Background(), []string{"--mode", "EditMode"}, send, nil, time.Second)
	if err != nil {
		t.Fatalf("testCmd: %v", err)
	}
	if got.Message != "Legacy result" {
		t.Fatalf("response message = %q", got.Message)
	}
}

func TestTestCmd_returnsRunningResponse_whenPortMetadataIsMalformed(t *testing.T) {
	response := &client.CommandResponse{
		Success: true,
		Message: "running",
		Data:    json.RawMessage(`{"run_id":"test-run-39126"}`),
	}
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		return response, nil
	}

	got, err := testCmd(context.Background(), []string{"--mode", "EditMode"}, send, nil, time.Second)
	if err != nil {
		t.Fatalf("testCmd: %v", err)
	}
	if got != response {
		t.Fatal("testCmd did not return the direct running response")
	}
}
