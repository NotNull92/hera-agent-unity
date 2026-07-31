package mcpserver

import (
	"context"
	"errors"
	"io"
	"strings"
	"testing"
	"time"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type nopWriteCloser struct {
	io.Writer
}

func (nopWriteCloser) Close() error { return nil }

func enabledTestConfig() Config {
	return Config{
		Enabled:   true,
		Transport: TransportStdio,
		Profile:   "core",
		Version:   "test",
		TimeoutMS: 1_000,
	}
}

func TestMCPGracefulEOF(t *testing.T) {
	// Given
	request := `{"jsonrpc":"2.0","id":1,"method":"server/discover","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"eof-test","version":"test"},"io.modelcontextprotocol/clientCapabilities":{}}}}` + "\n"
	transport := &mcp.IOTransport{
		Reader: io.NopCloser(strings.NewReader(request)),
		Writer: nopWriteCloser{Writer: io.Discard},
	}

	// When
	err := Run(context.Background(), enabledTestConfig(), transport)

	// Then
	if err != nil {
		t.Fatalf("Run() error = %v, want nil", err)
	}
}

func TestMCPContextCancellation(t *testing.T) {
	// Given
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	serverCtx, stopServer := context.WithCancel(ctx)
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	done := make(chan error, 1)
	go func() {
		done <- Run(serverCtx, enabledTestConfig(), serverTransport)
	}()
	client := mcp.NewClient(&mcp.Implementation{Name: "hera-mcp-test", Version: "test"}, nil)
	session, err := client.Connect(ctx, clientTransport, nil)
	if err != nil {
		t.Fatalf("Connect() error = %v", err)
	}

	// When
	stopServer()

	// Then
	select {
	case err := <-done:
		if err != nil {
			t.Fatalf("Run() error = %v, want nil", err)
		}
	case <-ctx.Done():
		t.Fatalf("server did not stop after cancellation: %v", ctx.Err())
	}
	if err := session.Close(); err != nil && !errors.Is(err, mcp.ErrConnectionClosed) {
		t.Fatalf("Close() error = %v", err)
	}
}

func TestMCPUnsupportedTransportRejected(t *testing.T) {
	// Given
	config := enabledTestConfig()
	config.Transport = "http"

	// When
	err := config.Validate()

	// Then
	if !errors.Is(err, ErrUnsupportedTransport) {
		t.Fatalf("Validate() error = %v, want ErrUnsupportedTransport", err)
	}
}

func TestMCPFeatureFlag(t *testing.T) {
	// Given
	config := enabledTestConfig()
	config.Enabled = false

	// When
	err := config.Validate()

	// Then
	if !errors.Is(err, ErrDisabled) {
		t.Fatalf("Validate() error = %v, want ErrDisabled", err)
	}
}

func TestMCPDiscoveryExposesIdentityWithoutUnityTools(t *testing.T) {
	// Given
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	done := make(chan error, 1)
	go func() {
		done <- Run(ctx, enabledTestConfig(), serverTransport)
	}()

	client := mcp.NewClient(&mcp.Implementation{Name: "hera-mcp-test", Version: "test"}, nil)

	// When
	session, err := client.Connect(ctx, clientTransport, nil)
	if err != nil {
		t.Fatalf("Connect() error = %v", err)
	}
	result := session.InitializeResult()

	// Then
	if result.ProtocolVersion != "2026-07-28" {
		t.Errorf("protocol version = %q, want 2026-07-28", result.ProtocolVersion)
	}
	if result.ServerInfo == nil || result.ServerInfo.Name != serverName {
		t.Errorf("server info = %#v, want name %q", result.ServerInfo, serverName)
	}
	if result.Capabilities == nil {
		t.Fatal("server capabilities are nil")
	}
	if result.Capabilities.Tools != nil {
		t.Fatalf("tools capability = %#v, want nil", result.Capabilities.Tools)
	}
	if err := session.Close(); err != nil {
		t.Fatalf("Close() error = %v", err)
	}
	select {
	case err := <-done:
		if err != nil {
			t.Fatalf("Run() error = %v, want nil", err)
		}
	case <-ctx.Done():
		t.Fatalf("server did not stop: %v", ctx.Err())
	}
}
