package client

import (
	"context"
	"errors"
	"io"
	"net/http"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

type roundTripFunc func(*http.Request) (*http.Response, error)

func (f roundTripFunc) RoundTrip(req *http.Request) (*http.Response, error) {
	return f(req)
}

func Test_ClientSend_retries_when_reload_temporarily_removes_instance_file(t *testing.T) {
	// Given
	originalDelay := reloadRetryDelay
	reloadRetryDelay = time.Millisecond
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	t.Setenv("HOME", t.TempDir())
	t.Setenv("USERPROFILE", t.TempDir())
	attempts := 0
	c := NewClient()
	c.httpClient = &http.Client{Transport: roundTripFunc(func(*http.Request) (*http.Response, error) {
		attempts++
		if attempts == 1 {
			return nil, errors.New("connectex: No connection could be made because the target machine actively refused it")
		}
		return jsonResponse(`{"success":true,"message":"ok"}`), nil
	})}

	// When
	response, err := c.Send(context.Background(), &Instance{Port: 8090, ProjectPath: "C:/Project"}, "list", nil, 2_000)

	// Then
	if err != nil {
		t.Fatalf("Send error = %v, want retry success", err)
	}
	if !response.Success {
		t.Fatalf("response.Success = false, want true")
	}
	if attempts != 2 {
		t.Fatalf("attempts = %d, want 2", attempts)
	}
}

func Test_ClientSend_retries_when_reload_returns_empty_success_response(t *testing.T) {
	// Given
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Project",
			Port:        8090,
			PID:         100,
			Timestamp:   1000,
		},
	})
	originalDelay := reloadRetryDelay
	reloadRetryDelay = time.Millisecond
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	attempts := 0
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(*http.Request) (*http.Response, error) {
		attempts++
		if attempts == 1 {
			return &http.Response{
				StatusCode:    http.StatusOK,
				ContentLength: 0,
				Body:          io.NopCloser(strings.NewReader("")),
			}, nil
		}
		return jsonResponse(`{"success":true,"message":"ok"}`), nil
	})}

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()

	// When
	response, err := c.Send(ctx, &Instance{Port: 8090, ProjectPath: "C:/Project"}, "list", nil, 2_000)

	// Then
	if err != nil {
		t.Fatalf("Send error = %v, want retry success", err)
	}
	if !response.Success {
		t.Fatalf("response.Success = false, want true")
	}
	if attempts != 2 {
		t.Fatalf("attempts = %d, want 2", attempts)
	}
}

func Test_ClientSend_rejects_reused_port_owned_by_another_project(t *testing.T) {
	// Given
	stubIsProcessDead(t, map[int]bool{})
	writeInstanceFiles(t, map[string]Instance{
		"inventoria.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Projects/Inventoria",
			Port:        8090,
			PID:         200,
			Timestamp:   2000,
		},
	})
	originalDelay := reloadRetryDelay
	reloadRetryDelay = 0
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(*http.Request) (*http.Response, error) {
		return nil, errors.New("connectex: No connection could be made because the target machine actively refused it")
	})}

	// When
	_, err := c.Send(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100},
		"list",
		nil,
		100,
	)

	// Then
	var mismatch *TargetMismatchError
	if !errors.As(err, &mismatch) {
		t.Fatalf("error = %v, want *TargetMismatchError", err)
	}
}

func Test_ClientSend_reports_observed_target_when_request_times_out(t *testing.T) {
	// Given
	stubIsProcessDead(t, map[int]bool{})
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Projects/test6.5",
			Port:        8090,
			PID:         100,
			Timestamp:   2000,
		},
	})
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
		<-request.Context().Done()
		return nil, request.Context().Err()
	})}

	// When
	_, err := c.Send(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100},
		"list",
		nil,
		10,
	)

	// Then
	var unresponsive *TargetUnresponsiveError
	if !errors.As(err, &unresponsive) {
		t.Fatalf("error = %v, want *TargetUnresponsiveError", err)
	}
	if unresponsive.State != unitystate.Ready || unresponsive.Project != "C:/Projects/test6.5" {
		t.Fatalf("unresponsive target = %#v", unresponsive)
	}
}

func Test_ClientSend_preserves_unknown_outcome_for_mutation_timeout(t *testing.T) {
	// Given
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Projects/test6.5",
			Port:        8090,
			PID:         100,
			Timestamp:   2000,
		},
	})
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
		<-request.Context().Done()
		return nil, request.Context().Err()
	})}

	// When
	_, err := c.SendWithOptions(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100},
		"scene",
		map[string]any{"action": "save"},
		10,
		SendOptions{OperationID: OperationID("op_timeout_mutation"), Idempotent: false},
	)

	// Then
	var unknown *OperationOutcomeUnknownError
	if !errors.As(err, &unknown) {
		t.Fatalf("error = %v, want *OperationOutcomeUnknownError", err)
	}
	var unresponsive *TargetUnresponsiveError
	if !errors.As(err, &unresponsive) {
		t.Fatalf("error cause = %v, want *TargetUnresponsiveError", err)
	}
}

func Test_ClientSend_reports_editor_restart_when_pid_changes_during_timeout(t *testing.T) {
	// Given
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Projects/test6.5",
			Port:        8092,
			PID:         200,
			Timestamp:   2000,
		},
	})
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
		<-request.Context().Done()
		return nil, request.Context().Err()
	})}

	// When
	_, err := c.Send(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100},
		"list",
		nil,
		10,
	)

	// Then
	var restarted *TargetRestartedError
	if !errors.As(err, &restarted) {
		t.Fatalf("error = %v, want *TargetRestartedError", err)
	}
	if restarted.PreviousPID != 100 || restarted.CurrentPID != 200 || restarted.Port != 8092 {
		t.Fatalf("restarted target = %#v", restarted)
	}
}

func Test_ClientSend_reports_target_lost_when_heartbeat_disappears_during_timeout(t *testing.T) {
	// Given
	writeInstanceFiles(t, map[string]Instance{})
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
		<-request.Context().Done()
		return nil, request.Context().Err()
	})}

	// When
	_, err := c.Send(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100},
		"list",
		nil,
		10,
	)

	// Then
	var lost *TargetLostError
	if !errors.As(err, &lost) {
		t.Fatalf("error = %v, want *TargetLostError", err)
	}
	if lost.Project != "C:/Projects/test6.5" || lost.PreviousPort != 8090 {
		t.Fatalf("lost target = %#v", lost)
	}
}

func jsonResponse(body string) *http.Response {
	return &http.Response{
		StatusCode:    http.StatusOK,
		ContentLength: int64(len(body)),
		Body:          io.NopCloser(strings.NewReader(body)),
	}
}
