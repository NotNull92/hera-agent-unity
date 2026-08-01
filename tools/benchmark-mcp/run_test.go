package main

import (
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/telemetry"
	"github.com/modelcontextprotocol/go-sdk/jsonrpc"
)

func TestBenchmarkDefinesAThroughEExactlyOnce(t *testing.T) {
	want := []string{"A", "B", "C", "D", "E"}
	if len(benchmarkVariants) != len(want) {
		t.Fatalf("variants = %#v", benchmarkVariants)
	}
	for index, id := range want {
		if benchmarkVariants[index].ID != id {
			t.Fatalf("variant %d = %q", index, benchmarkVariants[index].ID)
		}
	}
}

func TestBenchmarkUsesReadOnlySceneInfoForEveryVariant(t *testing.T) {
	for _, current := range benchmarkVariants {
		if current.Tool != "scene" || current.Action != "info" {
			t.Fatalf("unsafe benchmark variant = %#v", current)
		}
	}
}

func TestBenchmarkEventCapturesCompleteZeroBasedAccounting(t *testing.T) {
	measurement := executionMeasurement{
		Success: true, HostCalls: 1, ProcessLaunches: 1,
		UnityHTTPRequests: 2, MCPRequests: 1, ToolResultTokens: 17,
		HostToolCallID: "host_observed", ProcessLaunchID: "pid_42",
		MCPRequestID: "jsonrpc_7", OperationID: "op_observed",
	}
	event := benchmarkEvent("run_1", benchmarkVariants[2], 25*time.Millisecond, measurement)
	if err := event.Validate(); err != nil {
		t.Fatal(err)
	}
	summary, err := telemetry.Summarize([]telemetry.Event{event})
	if err != nil {
		t.Fatal(err)
	}
	if summary.Tasks != 1 || summary.ModelCalls != 0 || summary.HostCalls != 1 || summary.ProcessLaunches != 1 || summary.UnityHTTPRequests != 2 || summary.ToolResultTokens != 17 {
		t.Fatalf("summary = %#v", summary)
	}
	if event.ModelCallID != "not_applicable" || event.MCPRequestID == "not_applicable" {
		t.Fatalf("correlation IDs = %#v", event)
	}
	if event.ProcessLaunchID != "pid_42" || event.OperationID != "op_observed" {
		t.Fatalf("observed IDs = %#v", event)
	}
}

func TestObserversCaptureProtocolAndConnectorIDs(t *testing.T) {
	id, err := jsonrpc.MakeID(float64(7))
	if err != nil {
		t.Fatal(err)
	}
	observer := &requestObserver{}
	observer.observe(&jsonrpc.Request{ID: id, Method: "tools/call"})
	if got := observer.toolCallID(); got != "jsonrpc_7" {
		t.Fatalf("MCP request ID = %q", got)
	}
	diagnostics := `[DBG] POST http://127.0.0.1:8090/command body={"meta":{"operation_id":"op_real"}}`
	if got := observedOperationID(diagnostics); got != "op_real" {
		t.Fatalf("operation ID = %q", got)
	}
}

func TestMeasurementHelpersCountObservedPayloads(t *testing.T) {
	diagnostics := "[DBG] POST http://127.0.0.1:8090/command body={}\nother\n[DBG] POST http://127.0.0.1:8090/command body={}\n"
	if got := countUnityRequests(diagnostics); got != 2 {
		t.Fatalf("requests = %d", got)
	}
	if got := estimatedTokens([]byte("12345")); got != 2 {
		t.Fatalf("tokens = %d", got)
	}
}
