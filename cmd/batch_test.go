package cmd

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func TestBatchCmd_Success(t *testing.T) {
	mockInst := &client.Instance{Port: 8090}

	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		return &client.BatchCommandResponse{
			Results: []client.CommandResponse{
				{Success: true, Message: "ok1"},
				{Success: true, Message: "ok2"},
			},
			Completed: 2,
			Failed:    0,
		}, nil
	}

	// Mock stdin with valid JSON so the "no file" path works.
	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	defer func() { batchStdin = oldStdin }()

	err := batchCmd(context.Background(), []string{}, mockSend, mockInst, 60000)
	if err != nil {
		t.Fatalf("expected no error, got %v", err)
	}
}

// mockFile implements the minimal io.Reader / os.File interface for testing.
type mockFile struct {
	data   []byte
	closed bool
}

func (m *mockFile) Read(p []byte) (n int, err error) {
	if len(m.data) == 0 {
		return 0, io.EOF
	}
	n = copy(p, m.data)
	m.data = m.data[n:]
	return n, nil
}

func (m *mockFile) Close() error {
	m.closed = true
	return nil
}

func (m *mockFile) Stat() (os.FileInfo, error) {
	return &mockFileInfo{}, nil
}

type mockFileInfo struct{}

func (m *mockFileInfo) Name() string       { return "mock" }
func (m *mockFileInfo) Size() int64        { return 0 }
func (m *mockFileInfo) Mode() os.FileMode  { return 0 } // not ModeCharDevice
func (m *mockFileInfo) ModTime() time.Time { return time.Time{} }
func (m *mockFileInfo) IsDir() bool        { return false }
func (m *mockFileInfo) Sys() interface{}   { return nil }

func TestBatchCmd_FailFast(t *testing.T) {
	mockInst := &client.Instance{Port: 8090}

	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		return &client.BatchCommandResponse{
			Results: []client.CommandResponse{
				{Success: true, Message: "ok1"},
				{Success: false, Message: "fail1"},
				{Success: true, Message: "ok2"}, // should not execute due to fail_fast
			},
			Completed: 3,
			Failed:    1,
		}, nil
	}

	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	defer func() { batchStdin = oldStdin }()

	err := batchCmd(context.Background(), []string{}, mockSend, mockInst, 60000)
	if err == nil {
		t.Fatal("expected error for failed batch, got nil")
	}
	if !strings.Contains(err.Error(), "1 failure") {
		t.Fatalf("expected '1 failure' in error, got: %v", err)
	}
}

func TestBatchCmd_File(t *testing.T) {
	tmpDir := t.TempDir()
	jsonPath := filepath.Join(tmpDir, "batch.json")
	content := `{"commands":[{"command":"manage_editor","params":{"action":"play"}}],"options":{"fail_fast":true}}`
	if err := os.WriteFile(jsonPath, []byte(content), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	mockInst := &client.Instance{Port: 8090}

	var capturedReq client.BatchCommandRequest
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		capturedReq = req
		return &client.BatchCommandResponse{
			Results:   []client.CommandResponse{{Success: true, Message: "played"}},
			Completed: 1,
			Failed:    0,
		}, nil
	}

	err := batchCmd(context.Background(), []string{"--file", jsonPath}, mockSend, mockInst, 60000)
	if err != nil {
		t.Fatalf("expected no error, got %v", err)
	}
	if len(capturedReq.Commands) != 1 {
		t.Fatalf("expected 1 command, got %d", len(capturedReq.Commands))
	}
	if capturedReq.Commands[0].Command != "manage_editor" {
		t.Fatalf("expected command 'manage_editor', got %q", capturedReq.Commands[0].Command)
	}
}

func TestBatchCmd_InvalidJSON(t *testing.T) {
	tmpDir := t.TempDir()
	jsonPath := filepath.Join(tmpDir, "bad.json")
	if err := os.WriteFile(jsonPath, []byte("not json"), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		return nil, errors.New("should not reach send")
	}

	err := batchCmd(context.Background(), []string{"--file", jsonPath}, mockSend, &client.Instance{Port: 8090}, 60000)
	if err == nil {
		t.Fatal("expected error for invalid JSON")
	}
	if !strings.Contains(err.Error(), "invalid JSON") {
		t.Fatalf("expected 'invalid JSON' in error, got: %v", err)
	}
}

func TestBatchCmd_UsesInitialInstance(t *testing.T) {
	initial := &client.Instance{Port: 8090}
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		if inst != initial {
			t.Fatal("batch send did not retain the initially resolved instance")
		}
		return &client.BatchCommandResponse{Results: []client.CommandResponse{{Success: true}}, Completed: 1}, nil
	}

	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	defer func() { batchStdin = oldStdin }()

	err := batchCmd(context.Background(), []string{}, mockSend, initial, 60000)
	if err != nil {
		t.Fatalf("batch command: %v", err)
	}
}

func TestBatchCmd_WhenServerRejectsEnvelope_ReturnsSentinel(t *testing.T) {
	initial := &client.Instance{Port: 8090}
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		return &client.BatchCommandResponse{
			Success: false,
			Code:    "HTTP_QUEUE_FULL",
			Message: "Too many pending requests; maximum is 64.",
		}, nil
	}

	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	defer func() { batchStdin = oldStdin }()

	_, _, err := captureBatchOutput(t, func() error {
		return batchCmd(context.Background(), []string{}, mockSend, initial, 60_000)
	})
	if !errors.Is(err, ErrCommandFailed) {
		t.Fatalf("batchCmd error = %v, want ErrCommandFailed", err)
	}
}

func TestBatchCmd_WritesStructuredRejectionToStderrAndReturnsSentinel(t *testing.T) {
	// Given
	initial := &client.Instance{Port: 8090}
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error) {
		return &client.BatchCommandResponse{
			Success: false,
			Code:    "HTTP_QUEUE_FULL",
			Message: "Too many pending requests; maximum is 64.",
			Data:    []byte(`{"maximum_pending":64}`),
		}, nil
	}

	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	t.Cleanup(func() { batchStdin = oldStdin })

	oldCompactJSON, oldQuiet, oldVerbose := flagCompactJSON, flagQuiet, flagVerbose
	flagCompactJSON, flagQuiet, flagVerbose = false, false, false
	t.Cleanup(func() {
		flagCompactJSON, flagQuiet, flagVerbose = oldCompactJSON, oldQuiet, oldVerbose
	})

	// When
	stdout, stderr, err := captureBatchOutput(t, func() error {
		return batchCmd(context.Background(), []string{}, mockSend, initial, 60_000)
	})

	// Then
	if !errors.Is(err, ErrCommandFailed) {
		t.Fatalf("batchCmd error = %v, want ErrCommandFailed", err)
	}
	if stdout != "" {
		t.Fatalf("stdout = %q, want empty for structured rejection", stdout)
	}

	var response client.BatchCommandResponse
	if err := json.Unmarshal([]byte(strings.TrimSpace(stderr)), &response); err != nil {
		t.Fatalf("stderr = %q, want one JSON rejection: %v", stderr, err)
	}
	if response.Code != "HTTP_QUEUE_FULL" {
		t.Fatalf("stderr code = %q, want HTTP_QUEUE_FULL", response.Code)
	}
}

func captureBatchOutput(t *testing.T, fn func() error) (string, string, error) {
	t.Helper()

	oldStdout, oldStderr := os.Stdout, os.Stderr
	stdoutReader, stdoutWriter, err := os.Pipe()
	if err != nil {
		t.Fatalf("open stdout pipe: %v", err)
	}
	stderrReader, stderrWriter, err := os.Pipe()
	if err != nil {
		_ = stdoutReader.Close()
		_ = stdoutWriter.Close()
		t.Fatalf("open stderr pipe: %v", err)
	}
	os.Stdout, os.Stderr = stdoutWriter, stderrWriter
	t.Cleanup(func() {
		os.Stdout, os.Stderr = oldStdout, oldStderr
		_ = stdoutReader.Close()
		_ = stdoutWriter.Close()
		_ = stderrReader.Close()
		_ = stderrWriter.Close()
	})

	callErr := fn()
	_ = stdoutWriter.Close()
	_ = stderrWriter.Close()
	os.Stdout, os.Stderr = oldStdout, oldStderr

	stdout, readStdoutErr := io.ReadAll(stdoutReader)
	if readStdoutErr != nil {
		t.Fatalf("read stdout: %v", readStdoutErr)
	}
	stderr, readStderrErr := io.ReadAll(stderrReader)
	if readStderrErr != nil {
		t.Fatalf("read stderr: %v", readStderrErr)
	}

	return string(stdout), string(stderr), callErr
}
