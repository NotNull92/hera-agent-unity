package cmd

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
)

type operationView struct {
	OperationID   string          `json:"operation_id"`
	Kind          string          `json:"kind"`
	Status        string          `json:"status"`
	Tool          string          `json:"tool"`
	Action        *string         `json:"action"`
	Idempotent    bool            `json:"idempotent"`
	StartedAt     string          `json:"started_at,omitempty"`
	CommittedAt   string          `json:"committed_at,omitempty"`
	Result        json.RawMessage `json:"result,omitempty"`
	StatusMessage string          `json:"status_message,omitempty"`
}

func (command taskCLI) operationStatus(id string) error {
	if _, err := client.ParseOperationID(id); err != nil {
		return fmt.Errorf("%w: %v", taskbridge.ErrInvalidTaskID, err)
	}
	data, err := os.ReadFile(filepath.Join(command.operationsDir, id+".json"))
	view := operationView{OperationID: id, Kind: "operation", Status: "not_recorded"}
	if errors.Is(err, os.ErrNotExist) {
		view.StatusMessage = "The operation may still be queued, may not have arrived, or may have been cleaned up after retention (24 hours, or 7 days for outcome_unknown); these cannot be distinguished, so check again after the Editor is ready."
		return json.NewEncoder(command.output).Encode(map[string]string{
			"operation_id":   view.OperationID,
			"kind":           view.Kind,
			"status":         view.Status,
			"status_message": view.StatusMessage,
		})
	}
	if err != nil {
		return fmt.Errorf("read operation record: %w", err)
	}
	var record struct {
		SchemaVersion int             `json:"schema_version"`
		OperationID   string          `json:"operation_id"`
		State         string          `json:"state"`
		Tool          string          `json:"tool"`
		Action        *string         `json:"action"`
		Idempotent    bool            `json:"idempotent"`
		StartedAtMS   int64           `json:"started_at_ms"`
		CommittedAtMS *int64          `json:"committed_at_ms"`
		Response      json.RawMessage `json:"response"`
	}
	if err := json.Unmarshal(data, &record); err != nil {
		return fmt.Errorf("decode operation record: %w", err)
	}
	if record.SchemaVersion != 1 {
		return fmt.Errorf("unsupported operation record schema_version: %d", record.SchemaVersion)
	}
	if record.OperationID != id {
		return fmt.Errorf("operation record ID does not match %q", id)
	}
	view.Status, view.Tool, view.Action, view.Idempotent = record.State, record.Tool, record.Action, record.Idempotent
	view.StartedAt = time.UnixMilli(record.StartedAtMS).UTC().Format(time.RFC3339Nano)
	if record.CommittedAtMS != nil {
		view.CommittedAt = time.UnixMilli(*record.CommittedAtMS).UTC().Format(time.RFC3339Nano)
	}
	switch record.State {
	case "committed", "failed", "responded":
		view.Result = record.Response
	case "running", "received":
		view.StatusMessage = "The operation has not finished yet."
	case "outcome_unknown":
		view.StatusMessage = "The outcome is unknown after a domain change; re-read the target state before deciding whether to resend."
	default:
		return fmt.Errorf("invalid operation record state: %q", record.State)
	}
	return json.NewEncoder(command.output).Encode(view)
}
