package telemetry

import (
	"bytes"
	"path/filepath"
	"strings"
	"testing"
)

func TestJSONLRoundTripAndRejectsTrailingData(t *testing.T) {
	path := filepath.Join(t.TempDir(), "events.jsonl")
	recorder, err := NewJSONLRecorder(path)
	if err != nil {
		t.Fatal(err)
	}
	if err := recorder.Record(validEvent()); err != nil {
		t.Fatal(err)
	}
	events, err := ReadJSONL(path)
	if err != nil {
		t.Fatal(err)
	}
	if len(events) != 1 || events[0].TaskID != "task_1" {
		t.Fatalf("events = %#v", events)
	}

	bad := bytes.NewBufferString("{\"schema_version\":\"hera.telemetry/1\"} trailing\n")
	if _, err := DecodeJSONL(bad); err == nil {
		t.Fatal("DecodeJSONL accepted trailing data")
	}
}

func TestDecodeJSONLAcceptsLegacyV1WithoutAccountingDeclarations(t *testing.T) {
	legacy := strings.ReplaceAll(`{"schema_version":"hera.telemetry/1","timestamp":"1970-01-01T00:00:01Z","variant":"A","benchmark_run_id":"run","conversation_id":"conversation","model_call_id":"model","host_tool_call_id":"host","process_launch_id":"process","mcp_request_id":"mcp","operation_id":"operation","unity_request_id":"unity","task_id":"task","first_attempt_success":true,"final_task_success":true}`, `\"`, `"`) + "\n"
	events, err := DecodeJSONL(strings.NewReader(legacy))
	if err != nil {
		t.Fatal(err)
	}
	if len(events) != 1 || events[0].SchemaVersion != LegacySchemaVersion {
		t.Fatalf("events = %#v", events)
	}
}

func TestJSONLRecorderRefusesInvalidEvent(t *testing.T) {
	recorder, err := NewJSONLRecorder(filepath.Join(t.TempDir(), "events.jsonl"))
	if err != nil {
		t.Fatal(err)
	}
	event := validEvent()
	event.OperationID = ""
	if err := recorder.Record(event); err == nil {
		t.Fatal("Record accepted invalid event")
	}
}
