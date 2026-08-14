package cmd

import (
	"context"
	"flag"
	"fmt"
	"io"
	"os"
	"strconv"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/mcpserver"
)

type mcpOptions struct {
	Transport          string
	Profile            string
	Exposure           string
	AllowArbitraryCode bool
	MRTR               bool
}

func parseMCPOptions(args []string) (mcpOptions, error) {
	options := mcpOptions{}
	flags := flag.NewFlagSet("mcp", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	flags.StringVar(&options.Transport, "transport", mcpserver.TransportStdio, "MCP transport")
	flags.StringVar(&options.Profile, "profile", envString("HERA_MCP_PROFILE", "core"), "fixed tool profile")
	flags.StringVar(&options.Exposure, "exposure", envString("HERA_MCP_EXPOSURE", mcpserver.ExposureCompact), "MCP tool exposure")
	flags.BoolVar(&options.AllowArbitraryCode, "allow-arbitrary-code", false, "allow advanced arbitrary-code tools at startup")
	flags.BoolVar(&options.MRTR, "mrtr", envBool("HERA_MCP_MRTR"), "enable negotiated multi-round-trip approval")
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
	maxInlineBytes, err := mcpMaxInlineBytes()
	if err != nil {
		return err
	}
	return mcpserver.RunStdio(ctx, mcpserver.Config{
		Enabled:            envBool("HERA_MCP_ENABLED"),
		Transport:          options.Transport,
		Profile:            options.Profile,
		Exposure:           options.Exposure,
		AllowArbitraryCode: options.AllowArbitraryCode,
		MRTR:               options.MRTR,
		Version:            Version,
		Project:            config.Project,
		Port:               config.Port,
		TimeoutMS:          config.TimeoutMillis(),
		MaxInlineBytes:     maxInlineBytes,
		Diagnostics:        os.Stderr,
	})
}

func mcpMaxInlineBytes() (int, error) {
	raw, configured := os.LookupEnv("HERA_MCP_MAX_INLINE_BYTES")
	if !configured {
		return mcpserver.DefaultMaxInlineBytes, nil
	}
	value, err := strconv.Atoi(strings.TrimSpace(raw))
	if err != nil || value <= 0 {
		return 0, fmt.Errorf("HERA_MCP_MAX_INLINE_BYTES must be a positive integer")
	}
	return value, nil
}
