package cmd

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
)

func TestTaskOperationStatus(t *testing.T) {
	const id = "op_lookup_12345"
	for _, state := range []string{"committed", "responded", "failed", "running", "received", "outcome_unknown", "not_recorded"} {
		t.Run(state, func(t *testing.T) {
			root := t.TempDir()
			dir := filepath.Join(root, "operations", "sha256_selected")
			if err := os.MkdirAll(dir, 0700); err != nil {
				t.Fatal(err)
			}
			response := `{"success":true,"data":{"value":9007199254740993}}`
			if state == "failed" {
				response = `{"success":false,"code":"MENU_EXECUTION_FAILED"}`
			}
			if state != "not_recorded" {
				writeTaskCLIFile(t, filepath.Join(dir, id+".json"), fmt.Sprintf(`{"schema_version":1,"operation_id":%q,"state":%q,"tool":"menu","action":null,"idempotent":false,"started_at_ms":1000,"committed_at_ms":2000,"response":%s}`, id, state, response))
			}
			// A record with the same ID in another project must not leak into this lookup.
			other := filepath.Join(root, "operations", "sha256_other")
			if err := os.MkdirAll(other, 0700); err != nil {
				t.Fatal(err)
			}
			writeTaskCLIFile(t, filepath.Join(other, id+".json"), `{"schema_version":1,"operation_id":"op_lookup_12345","state":"responded","tool":"foreign"}`)
			var out bytes.Buffer
			c := taskCLI{store: taskbridge.New(root, taskCLIProjectID), operationsDir: dir, output: &out}
			if err := c.Run([]string{"status", id}); err != nil {
				t.Fatal(err)
			}
			var got map[string]json.RawMessage
			if err := json.Unmarshal(out.Bytes(), &got); err != nil {
				t.Fatal(err)
			}
			if string(got["status"]) != fmt.Sprintf("%q", state) || string(got["kind"]) != `"operation"` || string(got["operation_id"]) != fmt.Sprintf("%q", id) {
				t.Fatalf("output: %s", out.Bytes())
			}
			if state == "not_recorded" {
				for _, key := range []string{"tool", "action", "idempotent"} {
					if _, ok := got[key]; ok {
						t.Errorf("not_recorded must omit %s: %s", key, out.Bytes())
					}
				}
				if len(got) != 4 {
					t.Errorf("not_recorded must have exactly four fields: %s", out.Bytes())
				}
			}
			terminal := state == "committed" || state == "responded" || state == "failed"
			if terminal {
				if string(got["result"]) != response {
					t.Fatalf("result: %s", got["result"])
				}
				if _, ok := got["status_message"]; ok {
					t.Fatalf("unexpected message: %s", out.Bytes())
				}
			} else if _, ok := got["result"]; ok {
				t.Fatalf("unexpected result: %s", out.Bytes())
			} else if len(got["status_message"]) == 0 {
				t.Fatal("missing message")
			}
			if state != "not_recorded" && (string(got["started_at"]) != `"1970-01-01T00:00:01Z"` || string(got["committed_at"]) != `"1970-01-01T00:00:02Z"` || string(got["action"]) != "null") {
				t.Fatalf("metadata: %s", out.Bytes())
			}
			if bytes.Count(out.Bytes(), []byte("\n")) != 1 {
				t.Fatal("not compact single-line JSON")
			}
		})
	}
}

func TestTaskOperationRejectsInvalidRecordsAndIDs(t *testing.T) {
	for _, id := range []string{"op_../x", "op_a/b", `op_a\b`, "op_x", "op_" + strings.Repeat("a", 126), "op_C:/absolute"} {
		var out bytes.Buffer
		c := taskCLI{store: taskbridge.New(t.TempDir(), taskCLIProjectID), output: &out}
		if err := c.Run([]string{"status", id}); err == nil || !strings.Contains(err.Error(), "invalid task id") {
			t.Errorf("%q: %v", id, err)
		}
	}
	for name, fixture := range map[string]string{"broken": "{", "schema": `{"schema_version":2}`, "missing_schema": `{}`, "null": "null"} {
		t.Run(name, func(t *testing.T) {
			dir := t.TempDir()
			writeTaskCLIFile(t, filepath.Join(dir, "op_lookup_12345.json"), fixture)
			var out bytes.Buffer
			c := taskCLI{store: taskbridge.New(dir, taskCLIProjectID), operationsDir: dir, output: &out}
			err := c.Run([]string{"status", "op_lookup_12345"})
			if err == nil || strings.Contains(err.Error(), "invalid task id") || out.Len() != 0 {
				t.Fatalf("error=%v output=%s", err, out.Bytes())
			}
		})
	}
}

func TestOperationOutcomeLookupHint(t *testing.T) {
	for _, id := range []client.OperationID{"op_lookup_12345", ""} {
		got := operationOutcomeResponse(&client.OperationOutcomeUnknownError{Code: "OPERATION_OUTCOME_UNKNOWN", OperationID: id, Project: "C:/Projects/My Game", Command: "menu"})
		want := `; check it with: hera-agent-unity --project "C:/Projects/My Game" task status op_lookup_12345`
		if id != "" && !strings.HasSuffix(got.Message, want) {
			t.Errorf("missing hint: %s", got.Message)
		}
		if id == "" && strings.Contains(got.Message, "task status") {
			t.Errorf("batch hint: %s", got.Message)
		}
	}
}
