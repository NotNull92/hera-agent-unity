package client

import (
	"context"
	"encoding/json"
	"errors"
	"net/http"
	"testing"
	"time"
)

func TestBlockingDialog(t *testing.T) {
	for _, tc := range []struct {
		name              string
		window            editorWindow
		fresh, dead, want bool
	}{
		{name: "modal", window: editorWindow{100, "#32770", "Save scene?", true}, want: true},
		{name: "other PID", window: editorWindow{200, "#32770", "Other", true}},
		{name: "hidden", window: editorWindow{100, "#32770", "Hidden", false}},
		{name: "main", window: editorWindow{100, "UnityContainerWndClass", "Main", true}},
		{name: "fresh", window: editorWindow{100, "#32770", "Fresh", true}, fresh: true},
		{name: "dead", window: editorWindow{100, "#32770", "Dead", true}, dead: true},
	} {
		t.Run(tc.name, func(t *testing.T) {
			c := NewClient()
			c.processDeadChecker = func(int) bool { return tc.dead }
			calls := 0
			c.enumerateWindows = func(int) []editorWindow { calls++; return []editorWindow{tc.window} }
			inst := &Instance{PID: 100, Timestamp: 1}
			if tc.fresh {
				inst.Timestamp = time.Now().UnixMilli()
			}
			title, found := c.BlockingDialog(inst)
			if found != tc.want || found && title != tc.window.Title {
				t.Fatalf("got %q/%v", title, found)
			}
			if (tc.fresh || tc.dead) && calls != 0 {
				t.Fatal("unnecessary window enumeration")
			}
		})
	}
}

func TestDialogTimeoutPreservesUnknownOutcome(t *testing.T) {
	inst := Instance{State: "ready", ProjectPath: "C:/Projects/modal", Port: 8090, PID: 100, Timestamp: 1}
	writeInstanceFiles(t, map[string]Instance{"target.json": inst})
	c := NewClient()
	c.processDeadChecker = func(int) bool { return false }
	c.enumerateWindows = func(int) []editorWindow { return []editorWindow{{100, "#32770", "Probe", true}} }
	calls := 0
	c.httpClient = &http.Client{Transport: roundTripFunc(func(r *http.Request) (*http.Response, error) {
		calls++
		<-r.Context().Done()
		return nil, r.Context().Err()
	})}
	_, err := c.SendWithOptions(context.Background(), &inst, "scene", nil, 10, SendOptions{OperationID: "op_modal_test"})
	var unknown *OperationOutcomeUnknownError
	var stalled *TargetUnresponsiveError
	if !errors.As(err, &unknown) || !errors.As(err, &stalled) || stalled.State != "blocked_by_dialog" || stalled.DialogTitle != "Probe" || calls != 1 {
		t.Fatalf("err=%v stalled=%#v calls=%d", err, stalled, calls)
	}
}

func TestDialogEnvelopeExplicitNoRetry(t *testing.T) {
	for _, state := range []string{"", "reloading", "blocked_by_dialog"} {
		raw, err := json.Marshal(CommandResponse{State: state})
		if err != nil {
			t.Fatal(err)
		}
		var value map[string]any
		if err = json.Unmarshal(raw, &value); err != nil {
			t.Fatal(err)
		}
		retry, present := value["retryable"]
		if state == "blocked_by_dialog" {
			if !present || retry != false {
				t.Fatal(string(raw))
			}
		} else if present {
			t.Fatal(string(raw))
		}
	}
}
