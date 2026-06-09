package poll

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// ---------- ExponentialBackoffLoop tests ----------

func TestExponentialBackoffLoop_ImmediateSuccess(t *testing.T) {
	start := time.Now()
	err := ExponentialBackoffLoop(100*time.Millisecond, 10*time.Millisecond, 50*time.Millisecond, func() bool {
		return true
	})
	elapsed := time.Since(start)

	if err != nil {
		t.Fatalf("expected nil, got %v", err)
	}
	if elapsed < 5*time.Millisecond || elapsed > 40*time.Millisecond {
		t.Fatalf("expected ~10ms sleep, elapsed=%v", elapsed)
	}
}

func TestExponentialBackoffLoop_Timeout(t *testing.T) {
	start := time.Now()
	err := ExponentialBackoffLoop(100*time.Millisecond, 10*time.Millisecond, 50*time.Millisecond, func() bool {
		return false
	})
	elapsed := time.Since(start)

	if err == nil {
		t.Fatal("expected timeout error, got nil")
	}
	if !strings.Contains(err.Error(), "timed out") {
		t.Fatalf("expected 'timed out' error, got %v", err)
	}
	// With base=10ms, sleeps are 10+20+40+50... The loop exits after the deadline.
	// Elapsed should be roughly between 100ms and 200ms.
	if elapsed < 80*time.Millisecond || elapsed > 200*time.Millisecond {
		t.Fatalf("expected elapsed around 100-200ms, got %v", elapsed)
	}
}

func TestExponentialBackoffLoop_IntervalDoubles(t *testing.T) {
	var calls []time.Time
	timeout := 200 * time.Millisecond
	base := 10 * time.Millisecond
	max := 100 * time.Millisecond

	err := ExponentialBackoffLoop(timeout, base, max, func() bool {
		calls = append(calls, time.Now())
		return false
	})
	if err == nil {
		t.Fatal("expected timeout error")
	}
	if len(calls) < 4 {
		t.Fatalf("expected at least 4 condition calls, got %d", len(calls))
	}

	// Compute deltas between successive condition calls; each delta approximates the sleep interval.
	deltas := make([]time.Duration, len(calls)-1)
	for i := 0; i < len(deltas); i++ {
		deltas[i] = calls[i+1].Sub(calls[i])
	}

	// Build expected intervals: base*2, base*4, base*8, ... capped at max.
	expected := make([]time.Duration, len(deltas))
	cur := base * 2
	for i := 0; i < len(expected); i++ {
		if cur > max {
			cur = max
		}
		expected[i] = cur
		cur *= 2
	}

	const tolerance = 25 * time.Millisecond
	for i := 0; i < len(deltas); i++ {
		exp := expected[i]
		// Only check while we haven't yet hit the capped max.
		if exp >= max && i > 0 && expected[i-1] == max {
			break
		}
		if deltas[i] < exp-tolerance || deltas[i] > exp+tolerance {
			t.Fatalf("interval %d: expected ~%v, got %v", i, exp, deltas[i])
		}
	}
}

// ---------- WaitForFile tests ----------

func TestWaitForFile_FileAppears(t *testing.T) {
	dir := t.TempDir()
	resultPath := filepath.Join(dir, "result.json")

	resp := client.CommandResponse{
		Success: true,
		Message: "all good",
		Code:    "OK",
	}
	data, err := json.Marshal(resp)
	if err != nil {
		t.Fatalf("marshal: %v", err)
	}
	if err := os.WriteFile(resultPath, data, 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	// Use a long timeout; the file already exists so it should return after one sleep (100ms).
	got, err := WaitForFile(resultPath, 0, 5*time.Second, "test-op")
	if err != nil {
		t.Fatalf("WaitForFile error: %v", err)
	}
	if got == nil {
		t.Fatal("expected response, got nil")
	}
	if !got.Success || got.Message != "all good" || got.Code != "OK" {
		t.Fatalf("unexpected response: %+v", got)
	}

	// File should be removed on success.
	if _, err := os.Stat(resultPath); !os.IsNotExist(err) {
		t.Fatal("expected result file to be removed after reading")
	}
}

func TestWaitForFile_Timeout(t *testing.T) {
	dir := t.TempDir()
	resultPath := filepath.Join(dir, "nonexistent.json")

	start := time.Now()
	_, err := WaitForFile(resultPath, 0, 100*time.Millisecond, "test-op")
	elapsed := time.Since(start)

	if err == nil {
		t.Fatal("expected error, got nil")
	}
	if !strings.Contains(err.Error(), "timed out waiting for test-op") {
		t.Fatalf("expected timeout error, got %v", err)
	}
	// Should sleep once (100ms) then exit; allow margin.
	if elapsed < 80*time.Millisecond || elapsed > 250*time.Millisecond {
		t.Fatalf("expected elapsed ~100-250ms, got %v", elapsed)
	}
}

func TestWaitForFile_InvalidJSON(t *testing.T) {
	dir := t.TempDir()
	resultPath := filepath.Join(dir, "bad.json")
	if err := os.WriteFile(resultPath, []byte("not json"), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	_, err := WaitForFile(resultPath, 0, 5*time.Second, "test-op")
	if err == nil {
		t.Fatal("expected error for invalid JSON")
	}
	if !strings.Contains(err.Error(), "failed to parse test-op") {
		t.Fatalf("expected parse error, got %v", err)
	}
}

func TestWaitForFile_UnityStopped(t *testing.T) {
	// Detecting "unity editor has stopped" requires controlling the behaviour of
	// client.FindByPort, which reads live instance state from the filesystem.
	// There is no injection point for a mock, so this path is skipped.
	t.Skip("requires mocking client.FindByPort / live instance state")
}

// WaitForAsyncJob is a thin wrapper; we just verify it delegates and does not panic.
func TestWaitForAsyncJob_Delegates(t *testing.T) {
	dir := t.TempDir()
	resultPath := filepath.Join(dir, "async.json")

	resp := client.CommandResponse{Success: true, Message: "async done"}
	data, _ := json.Marshal(resp)
	_ = os.WriteFile(resultPath, data, 0644)

	got, err := WaitForAsyncJob(resultPath, 0, 5*time.Second, "async-op")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if got == nil || !got.Success {
		t.Fatalf("unexpected response: %+v", got)
	}
}

// Optional: verify that WaitForFile returns a wrapped error when JSON unmarshalling fails.
func TestWaitForFile_ParseErrorIsWrapped(t *testing.T) {
	dir := t.TempDir()
	resultPath := filepath.Join(dir, "bad.json")
	_ = os.WriteFile(resultPath, []byte("{bad"), 0644)

	_, err := WaitForFile(resultPath, 0, 5*time.Second, "my-op")
	if err == nil {
		t.Fatal("expected error")
	}
	// The error should wrap the JSON syntax error.
	if !strings.Contains(err.Error(), "failed to parse my-op") {
		t.Fatalf("unexpected error text: %v", err)
	}
}
