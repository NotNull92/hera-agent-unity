package cmd

import (
	"context"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity-pro/internal/client"
)

func TestBatchCmd_Success(t *testing.T) {
	mockInst := &client.Instance{Port: 8090}
	mockResolve := func() (*client.Instance, error) { return mockInst, nil }

	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error) {
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

	err := batchCmd(context.Background(), []string{}, mockSend, mockResolve)
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
	mockResolve := func() (*client.Instance, error) { return mockInst, nil }

	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error) {
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

	err := batchCmd(context.Background(), []string{}, mockSend, mockResolve)
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
	mockResolve := func() (*client.Instance, error) { return mockInst, nil }

	var capturedReq client.BatchCommandRequest
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error) {
		capturedReq = req
		return &client.BatchCommandResponse{
			Results:   []client.CommandResponse{{Success: true, Message: "played"}},
			Completed: 1,
			Failed:    0,
		}, nil
	}

	err := batchCmd(context.Background(), []string{"--file", jsonPath}, mockSend, mockResolve)
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

	mockResolve := func() (*client.Instance, error) { return &client.Instance{Port: 8090}, nil }
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error) {
		return nil, errors.New("should not reach send")
	}

	err := batchCmd(context.Background(), []string{"--file", jsonPath}, mockSend, mockResolve)
	if err == nil {
		t.Fatal("expected error for invalid JSON")
	}
	if !strings.Contains(err.Error(), "invalid JSON") {
		t.Fatalf("expected 'invalid JSON' in error, got: %v", err)
	}
}

func TestBatchCmd_ResolveError(t *testing.T) {
	mockResolve := func() (*client.Instance, error) { return nil, fmt.Errorf("no instance") }
	mockSend := func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest) (*client.BatchCommandResponse, error) {
		return nil, errors.New("should not reach send")
	}

	oldStdin := batchStdin
	batchStdin = &mockFile{data: []byte(`{"commands":[{"command":"list"}]}`)}
	defer func() { batchStdin = oldStdin }()

	err := batchCmd(context.Background(), []string{}, mockSend, mockResolve)
	if err == nil {
		t.Fatal("expected error for resolve failure")
	}
	if !strings.Contains(err.Error(), "no instance") {
		t.Fatalf("expected 'no instance' in error, got: %v", err)
	}
}
