package mcpserver

import (
	"log/slog"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const serverName = "hera-agent-unity"

func newServer(config Config) *mcp.Server {
	options := &mcp.ServerOptions{
		Capabilities: &mcp.ServerCapabilities{},
	}
	if config.Diagnostics != nil {
		options.Logger = slog.New(slog.NewTextHandler(config.Diagnostics, &slog.HandlerOptions{
			Level: slog.LevelError,
		}))
	}
	return mcp.NewServer(&mcp.Implementation{
		Name:        serverName,
		Title:       "Hera Agent Unity",
		Description: "Experimental stdio adapter for Hera Agent Unity",
		Version:     config.Version,
	}, options)
}
