package cmd

import (
	"bytes"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
)

const taskCLIProjectID = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

func TestTaskCLI_listPrintsActiveDurableTasks(t *testing.T) {
	// Given
	root := t.TempDir()
	writeTaskCLIFile(t, filepath.Join(root, "test-pending-8094-run-current.json"),
		`{"port":8094,"run_id":"run-current","project_id":"`+taskCLIProjectID+`","owner_pid":42}`)
	var output bytes.Buffer
	command := taskCLI{store: taskbridge.New(root, taskCLIProjectID), port: 8094, output: &output}

	// When
	err := command.Run([]string{"list"})

	// Then
	if err != nil {
		t.Fatalf("Run: %v", err)
	}
	var result struct {
		Tasks []taskView `json:"tasks"`
	}
	if err := json.Unmarshal(output.Bytes(), &result); err != nil {
		t.Fatalf("decode output: %v", err)
	}
	if len(result.Tasks) != 1 || result.Tasks[0].Kind != taskbridge.KindTest || result.Tasks[0].State != taskbridge.StateWorking {
		t.Fatalf("tasks = %#v", result.Tasks)
	}
	if result.Tasks[0].TaskID == "" || result.Tasks[0].UnderlyingID != "run-current" {
		t.Fatalf("task = %#v", result.Tasks[0])
	}
}

func TestTaskCLI_statusReadsMCPTaskHandleWithoutUnityRequest(t *testing.T) {
	// Given
	root := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	writeTaskCLIFile(t, filepath.Join(root, "test-pending-8094-"+runID+".json"),
		`{"port":8094,"run_id":"`+runID+`","project_id":"`+taskCLIProjectID+`","owner_pid":42}`)
	store := taskbridge.New(root, taskCLIProjectID)
	handle, err := store.Create(taskbridge.Start{
		Kind: taskbridge.KindTest, Port: 8094, UnderlyingID: runID,
		OperationID: "op_abcdef0123456789abcdef0123456789",
	})
	if err != nil {
		t.Fatalf("Create: %v", err)
	}
	var output bytes.Buffer
	command := taskCLI{store: store, port: 8094, output: &output}

	// When
	err = command.Run([]string{"status", handle.ID})

	// Then
	if err != nil {
		t.Fatalf("Run: %v", err)
	}
	var result taskView
	if err := json.Unmarshal(output.Bytes(), &result); err != nil {
		t.Fatalf("decode output: %v", err)
	}
	if result.TaskID != handle.ID || result.State != taskbridge.StateWorking || result.Port != 8094 {
		t.Fatalf("task = %#v", result)
	}
}

func writeTaskCLIFile(t *testing.T, path, content string) {
	t.Helper()
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatalf("write task fixture: %v", err)
	}
}
