package telemetry

import (
	"bytes"
	"path/filepath"
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
