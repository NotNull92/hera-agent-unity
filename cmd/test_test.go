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

	_, err := testCmd(context.Background(), []string{"--mode", "EditMode"}, send, resolve, 0)
	if err == nil {
		t.Fatal("testCmd error = nil, want polling timeout")
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
