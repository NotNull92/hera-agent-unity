package taskbridge

import (
	"errors"
	"os"
	"path/filepath"
	"testing"
)

func TestList_discoversOnlyPendingTasksForSelectedProjectAndPort(t *testing.T) {
	// Given
	root := t.TempDir()
	writeTaskFile(t, filepath.Join(root, "test-pending-8094-run-current.json"),
		`{"port":8094,"run_id":"run-current","project_id":"`+testProjectID+`","owner_pid":42}`)
	writeTaskFile(t, filepath.Join(root, "package-pending-8094-job-current.json"),
		`{"port":8094,"job_id":"job-current","project_id":"`+testProjectID+`","action":"add","owner_pid":42}`)
	writeTaskFile(t, filepath.Join(root, "test-pending-8094-run-other-project.json"),
		`{"port":8094,"run_id":"run-other-project","project_id":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","owner_pid":43}`)
	writeTaskFile(t, filepath.Join(root, "test-pending-8093-run-other-port.json"),
		`{"port":8093,"run_id":"run-other-port","project_id":"`+testProjectID+`","owner_pid":42}`)

	// When
	tasks, err := New(root, testProjectID).List(8094)

	// Then
	if err != nil {
		t.Fatalf("List: %v", err)
	}
	if len(tasks) != 2 {
		t.Fatalf("task count = %d, want 2", len(tasks))
	}
	got := map[Kind]*Task{}
	for i := range tasks {
		got[tasks[i].Kind] = &tasks[i]
	}
	if got[KindTest] == nil || got[KindTest].UnderlyingID != "run-current" || got[KindTest].Port != 8094 {
		t.Fatalf("test task = %#v", got[KindTest])
	}
	if got[KindPackage] == nil || got[KindPackage].UnderlyingID != "job-current" || got[KindPackage].Port != 8094 {
		t.Fatalf("package task = %#v", got[KindPackage])
	}
}

func TestList_handleReadsCompletedResultAfterPendingRecordIsCleared(t *testing.T) {
	// Given
	root := t.TempDir()
	runID := "run-completes-later"
	pendingPath := filepath.Join(root, "test-pending-8094-"+runID+".json")
	writeTaskFile(t, pendingPath,
		`{"port":8094,"run_id":"`+runID+`","project_id":"`+testProjectID+`","owner_pid":42}`)
	store := New(root, testProjectID)
	tasks, err := store.List(8094)
	if err != nil || len(tasks) != 1 {
		t.Fatalf("List tasks=%#v error=%v", tasks, err)
	}
	if err := os.Remove(pendingPath); err != nil {
		t.Fatalf("remove pending fixture: %v", err)
	}
	writeTaskFile(t, filepath.Join(root, "test-results-8094-"+runID+".json"),
		`{"success":true,"message":"done"}`)

	// When
	task, err := store.Get(tasks[0].ID)

	// Then
	if err != nil {
		t.Fatalf("Get: %v", err)
	}
	if task.State != StateCompleted || task.UnderlyingID != runID {
		t.Fatalf("task = %#v", task)
	}
}

func TestList_rejectsInvalidPort(t *testing.T) {
	// Given
	store := New(t.TempDir(), testProjectID)

	// When
	_, err := store.List(0)

	// Then
	if !errors.Is(err, ErrInvalidTaskID) {
		t.Fatalf("error = %v, want ErrInvalidTaskID", err)
	}
}
