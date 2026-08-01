package taskbridge

import (
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"
)

func TestPackageTaskSurvivesAdapterRestart(t *testing.T) {
	root := t.TempDir()
	pending := filepath.Join(root, "package-pending-8093-pkg-0123456789abcdef0123456789abcdef.json")
	writeTaskFile(t, pending, `{"job_id":"pkg-0123456789abcdef0123456789abcdef","port":8093,"action":"add","identifier":"com.example.package"}`)

	first := New(root)
	handle, err := first.Create(Start{
		Kind: KindPackage, Port: 8093, UnderlyingID: "pkg-0123456789abcdef0123456789abcdef",
		OperationID: "op_0123456789abcdef0123456789abcdef", Action: "add",
	})
	if err != nil {
		t.Fatal(err)
	}

	second := New(root)
	task, err := second.Get(handle.ID)
	if err != nil {
		t.Fatal(err)
	}
	if task.State != StateWorking || task.OperationID != "op_0123456789abcdef0123456789abcdef" {
		t.Fatalf("task=%#v", task)
	}

	if err := os.Remove(pending); err != nil {
		t.Fatal(err)
	}
	writeTaskFile(t, filepath.Join(root, "package-result-8093-pkg-0123456789abcdef0123456789abcdef.json"), `{"success":true,"message":"done","data":{"action":"add"}}`)
	task, err = New(root).Get(handle.ID)
	if err != nil {
		t.Fatal(err)
	}
	if task.State != StateCompleted || len(task.Result) == 0 {
		t.Fatalf("task=%#v", task)
	}
	var result map[string]any
	if err := json.Unmarshal(task.Result, &result); err != nil || result["success"] != true {
		t.Fatalf("result=%s error=%v", task.Result, err)
	}
}

func TestTestTaskSurvivesAdapterRestart(t *testing.T) {
	root := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	writeTaskFile(t, filepath.Join(root, "test-pending-8093-"+runID+".json"), `{"port":8093,"run_id":"`+runID+`","mode":"EditMode","owner_pid":42}`)
	handle, err := New(root).Create(Start{Kind: KindTest, Port: 8093, UnderlyingID: runID, OperationID: "op_abcdef0123456789abcdef0123456789"})
	if err != nil {
		t.Fatal(err)
	}
	if task, err := New(root).Get(handle.ID); err != nil || task.State != StateWorking {
		t.Fatalf("task=%#v error=%v", task, err)
	}
}

func TestTestTaskCompletionSurvivesAdapterRestart(t *testing.T) {
	root := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	resultPath := filepath.Join(root, "test-results-8093-"+runID+".json")
	writeTaskFile(t, resultPath, `{"success":false,"message":"test failed","code":"TESTS_FAILED"}`)
	handle, err := New(root).Create(Start{Kind: KindTest, Port: 8093, UnderlyingID: runID, OperationID: "op_abcdef0123456789abcdef0123456789"})
	if err != nil {
		t.Fatal(err)
	}
	task, err := New(root).Get(handle.ID)
	if err != nil {
		t.Fatal(err)
	}
	if task.State != StateCompleted {
		t.Fatalf("state=%q, want completed for a tool-level test failure", task.State)
	}
}

func TestCancellationReportsUnsupportedWithoutChangingTask(t *testing.T) {
	root := t.TempDir()
	jobID := "pkg-0123456789abcdef0123456789abcdef"
	writeTaskFile(t, filepath.Join(root, "package-pending-8093-"+jobID+".json"), `{"job_id":"`+jobID+`","port":8093,"action":"remove","identifier":"com.example.package"}`)
	handle, err := New(root).Create(Start{Kind: KindPackage, Port: 8093, UnderlyingID: jobID, OperationID: "op_0123456789abcdef0123456789abcdef"})
	if err != nil {
		t.Fatal(err)
	}
	result, err := New(root).Cancel(handle.ID)
	if err != nil {
		t.Fatal(err)
	}
	if result.Supported || result.Cancelled || result.Reason == "" {
		t.Fatalf("cancel=%#v", result)
	}
	if task, err := New(root).Get(handle.ID); err != nil || task.State != StateWorking {
		t.Fatalf("task=%#v error=%v", task, err)
	}
}

func TestRejectsMalformedTaskID(t *testing.T) {
	if _, err := New(t.TempDir()).Get("../status/secret"); err == nil {
		t.Fatal("Get accepted malformed task id")
	}
}

func TestRejectsOversizedTaskIDBeforeDecode(t *testing.T) {
	if _, err := New(t.TempDir()).Get("hera_task_" + string(make([]byte, 2048))); !errors.Is(err, ErrInvalidTaskID) {
		t.Fatalf("error=%v, want ErrInvalidTaskID", err)
	}
}

func TestTestCancellationReportsUnsupported(t *testing.T) {
	root := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	writeTaskFile(t, filepath.Join(root, "test-pending-8093-"+runID+".json"), `{"port":8093,"run_id":"`+runID+`","mode":"EditMode","owner_pid":42}`)
	handle, err := New(root).Create(Start{Kind: KindTest, Port: 8093, UnderlyingID: runID, OperationID: "op_abcdef0123456789abcdef0123456789"})
	if err != nil {
		t.Fatal(err)
	}
	cancel, err := New(root).Cancel(handle.ID)
	if err != nil || cancel.Supported || cancel.Cancelled || cancel.Reason == "" {
		t.Fatalf("cancel=%#v error=%v", cancel, err)
	}
}

func writeTaskFile(t *testing.T, path, content string) {
	t.Helper()
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatal(err)
	}
}
