package mcpserver

import (
	"errors"
	"fmt"
	"io"
)

const TransportStdio = "stdio"

var (
	ErrDisabled             = errors.New("MCP server is disabled")
	ErrUnsupportedTransport = errors.New("unsupported MCP transport")
)

type Config struct {
	Enabled            bool
	Transport          string
	Profile            string
	Exposure           string
	AllowArbitraryCode bool
	MRTR               bool
	Version            string
	Project            string
	Port               int
	TimeoutMS          int
	Diagnostics        io.Writer
}

func (config Config) Validate() error {
	if !config.Enabled {
		return fmt.Errorf("%w; set HERA_MCP_ENABLED=1 to enable the experimental server", ErrDisabled)
	}
	if config.Transport != TransportStdio {
		return fmt.Errorf("%w %q; only stdio is available", ErrUnsupportedTransport, config.Transport)
	}
	if config.Version == "" {
		return fmt.Errorf("MCP server version is required")
	}
	if err := config.validateExposure(); err != nil {
		return err
	}
	if config.TimeoutMS <= 0 {
		return fmt.Errorf("MCP timeout must be greater than zero")
	}
	return nil
}
