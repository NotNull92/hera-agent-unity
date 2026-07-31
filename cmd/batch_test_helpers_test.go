package cmd

import (
	"io"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

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
func (m *mockFileInfo) Mode() os.FileMode  { return 0 }
func (m *mockFileInfo) ModTime() time.Time { return time.Time{} }
func (m *mockFileInfo) IsDir() bool        { return false }
func (m *mockFileInfo) Sys() interface{}   { return nil }

func testBatchRuntime(
	send SendBatchFunc,
	instance *client.Instance,
	timeoutMs int,
) batchRuntime {
	return batchRuntime{
		Config:    GlobalConfig{Timeout: time.Duration(timeoutMs) * time.Millisecond},
		Instance:  instance,
		SendBatch: send,
	}
}
