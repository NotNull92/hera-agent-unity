package main

import (
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/telemetry"
)

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func run(args []string) error {
	if len(args) == 0 {
		return fmt.Errorf("usage: benchmark-mcp <prepare|run|summarize>")
	}
	switch args[0] {
	case "prepare":
		flags := flag.NewFlagSet("prepare", flag.ContinueOnError)
		unity := flags.String("unity", "", "Unity Editor executable")
		out := flags.String("out", "", "empty destination")
		connector := flags.String("connector", "", "AgentConnector path")
		if err := flags.Parse(args[1:]); err != nil {
			return err
		}
		return prepareFixture(*unity, *out, *connector)
	case "run":
		flags := flag.NewFlagSet("run", flag.ContinueOnError)
		options := runOptions{}
		flags.StringVar(&options.Hera, "hera", "", "exact-source Hera binary")
		flags.StringVar(&options.Project, "project", "", "disposable Unity fixture")
		flags.StringVar(&options.Output, "output", "", "telemetry JSONL")
		flags.StringVar(&options.RunID, "run-id", "", "benchmark run ID")
		if err := flags.Parse(args[1:]); err != nil {
			return err
		}
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Minute)
		defer cancel()
		return runBenchmark(ctx, options)
	case "summarize":
		flags := flag.NewFlagSet("summarize", flag.ContinueOnError)
		input := flags.String("input", "", "telemetry JSONL")
		if err := flags.Parse(args[1:]); err != nil {
			return err
		}
		events, err := telemetry.ReadJSONL(*input)
		if err != nil {
			return err
		}
		summary, err := telemetry.Summarize(events)
		if err != nil {
			return err
		}
		return json.NewEncoder(os.Stdout).Encode(summary)
	default:
		return fmt.Errorf("unknown command %q", args[0])
	}
}
