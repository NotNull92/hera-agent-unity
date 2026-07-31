package mcpserver

import (
	"context"
	"errors"
	"fmt"
	"io"
	"strings"

	"github.com/modelcontextprotocol/go-sdk/jsonrpc"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const serverClosingCode = -32004

func Run(ctx context.Context, config Config, transport mcp.Transport) error {
	if err := config.Validate(); err != nil {
		return err
	}
	if transport == nil {
		return fmt.Errorf("MCP transport is required")
	}
	return runServer(ctx, newServer(config), transport)
}

func runPrepared(ctx context.Context, config Config, transport mcp.Transport, runtime nativeRuntime) error {
	if err := config.Validate(); err != nil {
		return err
	}
	if transport == nil {
		return fmt.Errorf("MCP transport is required")
	}
	server := newServer(config)
	if err := registerNativeTools(server, config, runtime); err != nil {
		return fmt.Errorf("register MCP profile %q: %w", config.Profile, err)
	}
	return runServer(ctx, server, transport)
}

func runServer(ctx context.Context, server *mcp.Server, transport mcp.Transport) error {
	err := server.Run(ctx, transport)
	if isGracefulShutdown(err) {
		return nil
	}
	if err != nil {
		return fmt.Errorf("run MCP server: %w", err)
	}
	return nil
}

func isGracefulShutdown(err error) bool {
	if errors.Is(err, context.Canceled) || errors.Is(err, io.EOF) || errors.Is(err, mcp.ErrConnectionClosed) {
		return true
	}

	// The v1.7.0 stdio path preserves the stable JSON-RPC server-closing code
	// but formats its terminal EOF instead of wrapping it.
	var rpcErr *jsonrpc.Error
	return errors.As(err, &rpcErr) && rpcErr.Code == serverClosingCode && strings.HasSuffix(err.Error(), ": EOF")
}

func RunStdio(ctx context.Context, config Config) error {
	if err := config.Validate(); err != nil {
		return err
	}
	runtime, err := prepareNativeRuntime(ctx, config)
	if err != nil {
		return err
	}
	return runPrepared(ctx, config, &mcp.StdioTransport{}, runtime)
}
