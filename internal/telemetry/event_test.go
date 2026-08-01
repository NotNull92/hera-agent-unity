package telemetry

import (
	"strings"
	"testing"
	"time"
)

func TestEventValidateRequiresEveryCorrelationID(t *testing.T) {
	event := validEvent()
	event.UnityRequestID = ""
	if err := event.Validate(); err == nil || !strings.Contains(err.Error(), "unity_request_id") {
		t.Fatalf("Validate() error = %v", err)
	}
}

func TestEventValidateRejectsNegativeAccounting(t *testing.T) {
	event := validEvent()
	event.RawTokens = -1
	if err := event.Validate(); err == nil || !strings.Contains(err.Error(), "raw_tokens") {
		t.Fatalf("Validate() error = %v", err)
	}
}

func validEvent() Event {
	return Event{
		SchemaVersion: "hera.telemetry/1", Timestamp: time.Unix(1, 0).UTC(), Variant: "A",
		BenchmarkRunID: "run_1", ConversationID: "conversation_1", ModelCallID: "model_1",
		HostToolCallID: "host_1", ProcessLaunchID: "process_1", MCPRequestID: "mcp_1",
		OperationID: "operation_1", UnityRequestID: "unity_1", TaskID: "task_1",
		FirstAttemptSuccess: true, FinalTaskSuccess: true, ElapsedMS: 10,
	}
}
