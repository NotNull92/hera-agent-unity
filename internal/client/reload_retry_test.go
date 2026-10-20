package client

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
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

func Test_ClientSend_keeps_last_port_while_heartbeat_reports_port_zero_during_reload(t *testing.T) {
	// Given: during a Play-entry domain reload the connector's heartbeat reads
	// state=reloading, port=0 (no listener yet). Port 0 is a marker, not a target.
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Reloading,
			ProjectPath: "C:/Project",
			Port:        0,
			PID:         100,
			Timestamp:   1000,
		},
	})
	originalDelay := reloadRetryDelay
	reloadRetryDelay = time.Millisecond
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	var ports []string
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(req *http.Request) (*http.Response, error) {
		ports = append(ports, req.URL.Port())
		if req.URL.Port() == "0" {
			return nil, errors.New("dial tcp 127.0.0.1:0: connectex: The requested address is not valid in its context.")
		}
		if len(ports) < 3 {
			return nil, errors.New("connectex: No connection could be made because the target machine actively refused it")
		}
		return jsonResponse(`{"success":true,"message":"ok"}`), nil
	})}
	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()

	// When
	response, err := c.Send(ctx, &Instance{
		Port:        8090,
		ProjectPath: "C:/Project",
		PID:         100,
		Features:    []string{FeatureOperationLedgerV1},
	}, "manage_editor", nil, 2_000)

	// Then
	if err != nil {
		t.Fatalf("Send error = %v, want retry on the last valid port (dialed ports %v)", err, ports)
	}
	if !response.Success {
		t.Fatalf("response.Success = false, want true")
	}
	for _, port := range ports {
		if port != "8090" {
			t.Fatalf("dialed ports = %v, want only 8090 while the heartbeat reports port 0", ports)
		}
	}
	if len(ports) != 3 {
		t.Fatalf("attempts = %d, want 3", len(ports))
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

func Test_ClientSend_preserves_unknown_outcome_for_ledger_mutation_timeout(t *testing.T) {
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

	_, err := c.SendWithOptions(
		context.Background(),
		&Instance{
			Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: 100,
			Features: []string{FeatureOperationLedgerV1},
		},
		"scene",
		map[string]any{"action": "save"},
		10,
		SendOptions{OperationID: OperationID("op_timeout_ledger"), Idempotent: false},
	)

	var unknown *OperationOutcomeUnknownError
	if !errors.As(err, &unknown) {
		t.Fatalf("error = %v, want *OperationOutcomeUnknownError", err)
	}
	if unknown.OperationID != "op_timeout_ledger" ||
		unknown.Project != "C:/Projects/test6.5" || unknown.Port != 8090 {
		t.Fatalf("unknown = %#v", unknown)
	}
	var unresponsive *TargetUnresponsiveError
	if !errors.As(err, &unresponsive) {
		t.Fatalf("error cause = %v, want *TargetUnresponsiveError", err)
	}
}

func Test_ClientSend_reports_editor_restart_when_pid_changes_during_timeout(t *testing.T) {
	child := exec.Command(os.Args[0], "-test.run=^$")
	if err := child.Run(); err != nil {
		t.Fatal(err)
	}
	// Given
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {
			State:       unitystate.Ready,
			ProjectPath: "C:/Projects/test6.5",
			Port:        8092,
			PID:         os.Getpid(),
			Timestamp:   2000,
		},
	})
	c := NewClient()
	c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
		<-request.Context().Done()
		return nil, request.Context().Err()
	})}

	// When
	_, err := c.Send(
		context.Background(),
		&Instance{Port: 8090, ProjectPath: "C:/Projects/test6.5", PID: child.Process.Pid},
		"list",
		nil,
		10,
	)

	// Then
	var restarted *TargetRestartedError
	if !errors.As(err, &restarted) {
		t.Fatalf("error = %v, want *TargetRestartedError", err)
	}
	if restarted.PreviousPID != child.Process.Pid || restarted.CurrentPID != os.Getpid() || restarted.Port != 8092 {
		t.Fatalf("restarted target = %#v", restarted)
	}
}

func TestReloadTimeoutIgnoresLiveForeignPID(t *testing.T) {
	for _, modal := range []bool{false, true} {
		t.Run(fmt.Sprint(modal), func(t *testing.T) {
			original := Instance{PID: os.Getpid(), Port: 8090, ProjectPath: "C:/Projects/worker", Timestamp: 1, State: "ready"}
			worker := original
			worker.PID, worker.Port, worker.Timestamp = os.Getppid(), 8092, time.Now().UnixMilli()
			if worker.PID <= 0 || worker.PID == original.PID || IsProcessDead(worker.PID) {
				t.Fatal("fixture requires a distinct live parent")
			}
			writeInstanceFiles(t, map[string]Instance{"target.json": worker})
			c := NewClient()
			c.enumerateWindows = func(pid int) []editorWindow {
				if pid != original.PID {
					t.Fatalf("probed worker PID %d instead of original %d", pid, original.PID)
				}
				if !modal {
					return nil
				}
				return []editorWindow{
					{PID: pid, HWND: 1, Visible: true},
					{PID: pid, HWND: 2, Owner: 1, Enabled: true, Visible: true, Class: "UnityContainerWndClass", Title: "Original modal"},
				}
			}
			c.httpClient = &http.Client{Transport: roundTripFunc(func(request *http.Request) (*http.Response, error) {
				<-request.Context().Done()
				return nil, request.Context().Err()
			})}
			_, err := c.Send(context.Background(), &original, "list", nil, 10)
			var restarted *TargetRestartedError
			var unresponsive *TargetUnresponsiveError
			if errors.As(err, &restarted) || !errors.As(err, &unresponsive) {
				t.Fatalf("want unresponsive live original, got %v", err)
			}
			if unresponsive.PID != original.PID || unresponsive.Project != original.ProjectPath || unresponsive.Port != original.Port {
				t.Fatalf("reported foreign target: %#v", unresponsive)
			}
			if (unresponsive.State == "blocked_by_dialog") != modal || modal && unresponsive.DialogTitle != "Original modal" {
				t.Fatalf("incorrect modal diagnosis: %#v", unresponsive)
			}
		})
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

func Test_ClientSend_keeps_last_port_after_retryable_503_while_heartbeat_reports_port_zero(t *testing.T) {
	writeInstanceFiles(t, map[string]Instance{
		"target.json": {State: unitystate.Reloading, ProjectPath: "C:/Project", Port: 0, PID: 100, Timestamp: 1000},
	})
	originalDelay := reloadRetryDelay
	reloadRetryDelay = time.Millisecond
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	var ports []string
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.httpClient = &http.Client{Transport: roundTripFunc(func(req *http.Request) (*http.Response, error) {
		ports = append(ports, req.URL.Port())
		if req.URL.Port() == "0" {
			return nil, errors.New("dial tcp 127.0.0.1:0: connectex: The requested address is not valid in its context.")
		}
		if len(ports) == 1 {
			return &http.Response{StatusCode: http.StatusServiceUnavailable, Header: http.Header{},
				Body: io.NopCloser(strings.NewReader(`{"success":false,"code":"RELOADING","retryable":true,"state":"reloading","retry_after_ms":1}`))}, nil
		}
		return jsonResponse(`{"success":true,"message":"ok"}`), nil
	})}
	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()
	response, err := c.Send(ctx, &Instance{Port: 8090, ProjectPath: "C:/Project", PID: 100, Features: []string{FeatureOperationLedgerV1}}, "manage_editor", nil, 2_000)
	if err != nil || !response.Success || len(ports) != 2 || ports[1] != "8090" {
		t.Fatalf("err=%v ports=%v, want 503 then a retry on 8090", err, ports)
	}
}
