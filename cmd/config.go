package cmd

import (
	"flag"
	"fmt"
	"io"
	"time"
)

type GlobalConfig struct {
	Project     string
	Port        int
	Timeout     time.Duration
	Verbose     bool
	Quiet       bool
	Debug       bool
	CompactJSON bool
	Narrate     bool
	AutoApprove bool
}

type WaitConfig struct {
	Timeout time.Duration
	Narrate bool
}

func (config GlobalConfig) TimeoutMillis() int {
	return int(config.Timeout / time.Millisecond)
}

func (config GlobalConfig) Wait(category string) WaitConfig {
	return WaitConfig{
		Timeout: config.Timeout,
		Narrate: !config.Quiet && (isHumanCommand(category) || config.Narrate),
	}
}

func parseGlobalConfig(args []string) (GlobalConfig, []string, error) {
	flagArgs, commandArgs := splitArgs(args)
	flags := flag.NewFlagSet("hera-agent-unity", flag.ContinueOnError)
	flags.SetOutput(io.Discard)

	var config GlobalConfig
	var timeoutMs int
	flags.IntVar(&config.Port, "port", envInt("HERA_AGENT_PORT", 0), "Select Unity instance by active heartbeat port")
	flags.StringVar(&config.Project, "project", envString("HERA_AGENT_PROJECT", ""), "Select Unity instance by project path")
	flags.IntVar(&timeoutMs, "timeout", envInt("HERA_AGENT_TIMEOUT_MS", 60000), "Request timeout in milliseconds")
	flags.BoolVar(&config.Verbose, "verbose", envBool("HERA_AGENT_VERBOSE"), "Print progress + per-phase timings to stderr")
	flags.BoolVar(&config.Quiet, "quiet", envBool("HERA_AGENT_QUIET"), "Suppress decorative progress messages")
	flags.BoolVar(&config.Debug, "debug", envBool("HERA_AGENT_DEBUG"), "Print HTTP request and response details")
	flags.BoolVar(&config.CompactJSON, "compact-json", envBool("HERA_AGENT_COMPACT_JSON"), "Output compact JSON")
	flags.BoolVar(&config.Narrate, "narrate", envBool("HERA_AGENT_NARRATE"), "Narrate wait progress")
	flags.BoolVar(&config.AutoApprove, "yes", envBool("HERA_AGENT_APPROVE"), "Answer approval preflights without prompting")
	if err := flags.Parse(flagArgs); err != nil {
		return GlobalConfig{}, nil, fmt.Errorf("flag parse error: %w", err)
	}
	if timeoutMs <= 0 {
		return GlobalConfig{}, nil, fmt.Errorf("--timeout must be greater than zero")
	}
	config.Timeout = time.Duration(timeoutMs) * time.Millisecond
	return config, commandArgs, nil
}
