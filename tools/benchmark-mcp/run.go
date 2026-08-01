package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/telemetry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type runOptions struct{ Hera, Project, Output, RunID string }

type variant struct{ ID, Name, Exposure, Tool, Action string }

type executionMeasurement struct {
	Success           bool
	HostCalls         int64
	ProcessLaunches   int64
	UnityHTTPRequests int64
	MCPRequests       int64
	ToolResultTokens  int64
	HostToolCallID    string
	ProcessLaunchID   string
	MCPRequestID      string
	OperationID       string
}

var benchmarkVariants = []variant{
	{"A", "legacy-cli", "", "scene", "info"}, {"B", "typed-cli", "", "scene", "info"},
	{"C", "mcp-profile", "profile", "scene", "info"}, {"D", "mcp-compact", "compact", "scene", "info"},
	{"E", "mcp-full", "full", "scene", "info"},
}

func runBenchmark(ctx context.Context, options runOptions) error {
	if err := validateFixture(options.Project); err != nil {
		return err
	}
	if options.Hera == "" || options.Output == "" || options.RunID == "" {
		return fmt.Errorf("--hera, --output, and --run-id are required")
	}
	if _, err := os.Stat(options.Output); err == nil {
		return fmt.Errorf("refuse existing benchmark output %s", options.Output)
	} else if !os.IsNotExist(err) {
		return fmt.Errorf("inspect benchmark output: %w", err)
	}
	project, err := filepath.Abs(options.Project)
	if err != nil {
		return err
	}
	for _, current := range benchmarkVariants {
		measurement, err := executeVariant(ctx, options.Hera, project, current)
		if err != nil {
			return fmt.Errorf("warm variant %s (%s): %w", current.ID, current.Name, err)
		}
		if !measurement.Success {
			return fmt.Errorf("warm variant %s (%s) did not succeed", current.ID, current.Name)
		}
	}
	recorder, err := telemetry.NewJSONLRecorder(options.Output)
	if err != nil {
		return err
	}
	for _, current := range benchmarkVariants {
		started := time.Now()
		measurement, err := executeVariant(ctx, options.Hera, project, current)
		event := benchmarkEvent(options.RunID, current, time.Since(started), measurement)
		if recordErr := recorder.Record(event); recordErr != nil {
			return recordErr
		}
		if err != nil {
			return fmt.Errorf("variant %s (%s): %w", current.ID, current.Name, err)
		}
	}
	return nil
}

func executeVariant(ctx context.Context, binary, project string, current variant) (executionMeasurement, error) {
	hostID, err := newBoundaryID("host")
	if err != nil {
		return executionMeasurement{}, err
	}
	var measurement executionMeasurement
	switch current.ID {
	case "A":
		measurement, err = runCLI(ctx, binary, "--project", project, current.Tool, current.Action)
	case "B":
		input, marshalErr := json.Marshal(map[string]string{"action": current.Action})
		if marshalErr != nil {
			return executionMeasurement{}, marshalErr
		}
		measurement, err = runCLI(ctx, binary, "--project", project, "call", current.Tool, "--json", string(input))
	default:
		measurement, err = runMCP(ctx, binary, project, current)
	}
	measurement.HostToolCallID = hostID
	return measurement, err
}

func runCLI(ctx context.Context, binary string, arguments ...string) (executionMeasurement, error) {
	command := exec.CommandContext(ctx, binary, arguments...)
	command.Env = append(os.Environ(), "HERA_AGENT_DEBUG=1")
	var output, diagnostics bytes.Buffer
	command.Stdout, command.Stderr = &output, &diagnostics
	measurement := executionMeasurement{
		HostCalls: 1, ProcessLaunches: 1,
		UnityHTTPRequests: countUnityRequests(diagnostics.String()),
	}
	if err := command.Run(); err != nil {
		measurement.ProcessLaunchID = processID(command)
		measurement.UnityHTTPRequests = countUnityRequests(diagnostics.String())
		measurement.OperationID = observedOperationID(diagnostics.String())
		return measurement, fmt.Errorf("%w: %s", err, diagnostics.String())
	}
	measurement.Success = true
	measurement.ProcessLaunchID = processID(command)
	measurement.UnityHTTPRequests = countUnityRequests(diagnostics.String())
	measurement.OperationID = observedOperationID(diagnostics.String())
	measurement.ToolResultTokens = estimatedTokens(output.Bytes())
	return measurement, nil
}

func runMCP(ctx context.Context, binary, project string, current variant) (measurement executionMeasurement, err error) {
	args := []string{"--project", project, "mcp", "--transport", "stdio", "--exposure", current.Exposure, "--profile", "core"}
	command := exec.CommandContext(ctx, binary, args...)
	command.Env = append(os.Environ(), "HERA_AGENT_DEBUG=1", "HERA_MCP_ENABLED=1")
	var diagnostics lockedBuffer
	command.Stderr = &diagnostics
	measurement = executionMeasurement{ProcessLaunches: 1}
	observer := &requestObserver{}
	client := mcp.NewClient(&mcp.Implementation{Name: "hera-benchmark", Version: "1"}, nil)
	transport := observingTransport{inner: &mcp.CommandTransport{Command: command}, observer: observer}
	session, err := client.Connect(ctx, transport, nil)
	measurement.ProcessLaunchID = processID(command)
	if err != nil {
		measurement.UnityHTTPRequests = countUnityRequests(diagnostics.String())
		return measurement, fmt.Errorf("%w: %s", err, diagnostics.String())
	}
	defer func() {
		err = errors.Join(err, session.Close())
	}()
	name := current.Tool
	arguments := map[string]any{"action": current.Action}
	if current.Exposure == "compact" {
		name = "tool_call"
		arguments = map[string]any{"name": "scene", "arguments": arguments}
	}
	measurement.HostCalls, measurement.MCPRequests = 1, 1
	result, err := session.CallTool(ctx, &mcp.CallToolParams{Name: name, Arguments: arguments})
	measurement.MCPRequestID = observer.toolCallID()
	measurement.UnityHTTPRequests = countUnityRequests(diagnostics.String())
	measurement.OperationID = observedOperationID(diagnostics.String())
	if err != nil {
		return measurement, err
	}
	if result.IsError {
		return measurement, fmt.Errorf("MCP tool returned an error")
	}
	encoded, err := json.Marshal(result)
	if err != nil {
		return measurement, fmt.Errorf("encode MCP tool result: %w", err)
	}
	measurement.Success = true
	measurement.ToolResultTokens = estimatedTokens(encoded)
	return measurement, nil
}

func benchmarkEvent(runID string, current variant, elapsed time.Duration, measurement executionMeasurement) telemetry.Event {
	prefix := runID + "_" + current.ID
	return telemetry.Event{
		SchemaVersion: telemetry.SchemaVersion, Timestamp: time.Now().UTC(), Variant: current.ID,
		BenchmarkRunID: runID, ConversationID: prefix + "_conversation", ModelCallID: "not_applicable",
		HostToolCallID: measuredID(measurement.HostToolCallID), ProcessLaunchID: measuredID(measurement.ProcessLaunchID),
		MCPRequestID: optionalMeasuredID(measurement.MCPRequestID, measurement.MCPRequests),
		OperationID:  measuredID(measurement.OperationID), UnityRequestID: "not_available", TaskID: prefix + "_task",
		FirstAttemptSuccess: measurement.Success, FinalTaskSuccess: measurement.Success,
		HostCalls: measurement.HostCalls, ProcessLaunches: measurement.ProcessLaunches,
		UnityHTTPRequests: measurement.UnityHTTPRequests, ElapsedMS: elapsed.Milliseconds(), ModelCalls: 0,
		RawTokens: 0, CachedTokens: 0, BilledTokens: 0, ToolResultTokens: measurement.ToolResultTokens,
		RepairCalls: 0, WrongToolAction: 0, InvalidArgument: 0, DuplicateSideEffects: 0,
		UnsafeMutations: 0, ReloadRecoveries: 0, HumanInterventions: 0,
		IDAccounting:    "host=generated_at_host_boundary;process=os_pid;mcp=jsonrpc_request_id;operation=connector_request_meta;unity=not_exposed",
		TokenAccounting: "no_model_calls;tool_result=ceil(utf8_bytes/4)",
	}
}

func countUnityRequests(diagnostics string) int64 {
	return int64(strings.Count(diagnostics, "[DBG] POST http://127.0.0.1:"))
}

func estimatedTokens(data []byte) int64 {
	return int64((len(data) + 3) / 4)
}
