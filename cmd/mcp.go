package cmd

import (
	"context"
	"flag"
	"fmt"
	"io"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/mcpserver"
)

type mcpOptions struct {
	Transport string
	Profile   string
}

func parseMCPOptions(args []string) (mcpOptions, error) {
	options := mcpOptions{}
	flags := flag.NewFlagSet("mcp", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	flags.StringVar(&options.Transport, "transport", mcpserver.TransportStdio, "MCP transport")
	flags.StringVar(&options.Profile, "profile", envString("HERA_MCP_PROFILE", "core"), "fixed tool profile")
	if err := flags.Parse(args); err != nil {
		return mcpOptions{}, fmt.Errorf("parse MCP flags: %w", err)
	}
	if flags.NArg() != 0 {
		return mcpOptions{}, fmt.Errorf("unexpected MCP argument %q", flags.Arg(0))
	}
	return options, nil
}

func mcpCmd(ctx context.Context, config GlobalConfig, args []string) error {
	options, err := parseMCPOptions(args)
	if err != nil {
		return err
	}
	return mcpserver.RunStdio(ctx, mcpserver.Config{
		Enabled:     envBool("HERA_MCP_ENABLED", false),
		Transport:   options.Transport,
		Profile:     options.Profile,
		Version:     Version,
		Project:     config.Project,
		Port:        config.Port,
		TimeoutMS:   config.TimeoutMillis(),
		Diagnostics: os.Stderr,
	})
}
