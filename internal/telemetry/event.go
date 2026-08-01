package telemetry

import (
	"fmt"
	"reflect"
	"time"
)

const (
	SchemaVersion       = "hera.telemetry/2"
	LegacySchemaVersion = "hera.telemetry/1"
)

type Event struct {
	SchemaVersion string    `json:"schema_version"`
	Timestamp     time.Time `json:"timestamp"`
	Variant       string    `json:"variant"`

	BenchmarkRunID  string `json:"benchmark_run_id"`
	ConversationID  string `json:"conversation_id"`
	ModelCallID     string `json:"model_call_id"`
	HostToolCallID  string `json:"host_tool_call_id"`
	ProcessLaunchID string `json:"process_launch_id"`
	MCPRequestID    string `json:"mcp_request_id"`
	OperationID     string `json:"operation_id"`
	UnityRequestID  string `json:"unity_request_id"`
	TaskID          string `json:"task_id"`
	IDAccounting    string `json:"id_accounting"`
	TokenAccounting string `json:"token_accounting"`

	FirstAttemptSuccess  bool  `json:"first_attempt_success"`
	FinalTaskSuccess     bool  `json:"final_task_success"`
	WrongToolAction      int64 `json:"wrong_tool_action"`
	InvalidArgument      int64 `json:"invalid_argument"`
	ModelCalls           int64 `json:"model_calls"`
	HostCalls            int64 `json:"host_calls"`
	ProcessLaunches      int64 `json:"process_launches"`
	UnityHTTPRequests    int64 `json:"unity_http_requests"`
	RepairCalls          int64 `json:"repair_calls"`
	RawTokens            int64 `json:"raw_tokens"`
	CachedTokens         int64 `json:"cached_tokens"`
	BilledTokens         int64 `json:"billed_tokens"`
	ToolResultTokens     int64 `json:"tool_result_tokens"`
	ElapsedMS            int64 `json:"elapsed_ms"`
	DuplicateSideEffects int64 `json:"duplicate_side_effects"`
	UnsafeMutations      int64 `json:"unsafe_mutations"`
	ReloadRecoveries     int64 `json:"reload_recoveries"`
	HumanInterventions   int64 `json:"human_interventions"`
}

func (event Event) Validate() error {
	if event.SchemaVersion != SchemaVersion && event.SchemaVersion != LegacySchemaVersion {
		return fmt.Errorf("unsupported schema_version %q", event.SchemaVersion)
	}
	if event.Timestamp.IsZero() {
		return fmt.Errorf("timestamp is required")
	}
	required := map[string]string{
		"variant": event.Variant, "benchmark_run_id": event.BenchmarkRunID,
		"conversation_id": event.ConversationID, "model_call_id": event.ModelCallID,
		"host_tool_call_id": event.HostToolCallID, "process_launch_id": event.ProcessLaunchID,
		"mcp_request_id": event.MCPRequestID, "operation_id": event.OperationID,
		"unity_request_id": event.UnityRequestID, "task_id": event.TaskID,
	}
	if event.SchemaVersion == SchemaVersion {
		required["id_accounting"] = event.IDAccounting
		required["token_accounting"] = event.TokenAccounting
	}
	for name, value := range required {
		if value == "" {
			return fmt.Errorf("%s is required", name)
		}
	}
	value := reflect.ValueOf(event)
	typeOf := value.Type()
	for index := 0; index < value.NumField(); index++ {
		field := value.Field(index)
		if field.Kind() == reflect.Int64 && field.Int() < 0 {
			return fmt.Errorf("%s cannot be negative", typeOf.Field(index).Tag.Get("json"))
		}
	}
	return nil
}
