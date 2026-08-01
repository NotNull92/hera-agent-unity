package main

import (
	"bytes"
	"context"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os/exec"
	"strings"
	"sync"

	"github.com/modelcontextprotocol/go-sdk/jsonrpc"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type lockedBuffer struct {
	mu     sync.Mutex
	buffer bytes.Buffer
}

func (buffer *lockedBuffer) Write(data []byte) (int, error) {
	buffer.mu.Lock()
	defer buffer.mu.Unlock()
	return buffer.buffer.Write(data)
}

func (buffer *lockedBuffer) String() string {
	buffer.mu.Lock()
	defer buffer.mu.Unlock()
	return buffer.buffer.String()
}

type requestObserver struct {
	mu sync.Mutex
	id string
}

func (observer *requestObserver) observe(message jsonrpc.Message) {
	request, ok := message.(*jsonrpc.Request)
	if !ok || request.Method != "tools/call" {
		return
	}
	observer.mu.Lock()
	observer.id = fmt.Sprintf("jsonrpc_%v", request.ID.Raw())
	observer.mu.Unlock()
}

func (observer *requestObserver) toolCallID() string {
	observer.mu.Lock()
	defer observer.mu.Unlock()
	return observer.id
}

type observingTransport struct {
	inner    mcp.Transport
	observer *requestObserver
}

func (transport observingTransport) Connect(ctx context.Context) (mcp.Connection, error) {
	connection, err := transport.inner.Connect(ctx)
	if err != nil {
		return nil, err
	}
	return observingConnection{Connection: connection, observer: transport.observer}, nil
}

type observingConnection struct {
	mcp.Connection
	observer *requestObserver
}

func (connection observingConnection) Write(ctx context.Context, message jsonrpc.Message) error {
	connection.observer.observe(message)
	return connection.Connection.Write(ctx, message)
}

func newBoundaryID(prefix string) (string, error) {
	data := make([]byte, 12)
	if _, err := rand.Read(data); err != nil {
		return "", fmt.Errorf("generate %s ID: %w", prefix, err)
	}
	return prefix + "_" + hex.EncodeToString(data), nil
}

func processID(command *exec.Cmd) string {
	if command.Process == nil {
		return ""
	}
	return fmt.Sprintf("pid_%d", command.Process.Pid)
}

func observedOperationID(diagnostics string) string {
	var envelope struct {
		Meta struct {
			OperationID string `json:"operation_id"`
		} `json:"meta"`
	}
	var operationID string
	for _, line := range strings.Split(diagnostics, "\n") {
		index := strings.Index(line, " body=")
		if index < 0 || json.Unmarshal([]byte(line[index+6:]), &envelope) != nil {
			continue
		}
		if envelope.Meta.OperationID != "" {
			operationID = envelope.Meta.OperationID
		}
	}
	return operationID
}

func measuredID(value string) string {
	if value == "" {
		return "not_available"
	}
	return value
}

func optionalMeasuredID(value string, calls int64) string {
	if calls == 0 {
		return "not_applicable"
	}
	return measuredID(value)
}
