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
	main := editorWindow{PID: 100, HWND: 1, Class: "UnityContainerWndClass", Title: "Main", Visible: true}
	modal := editorWindow{PID: 100, HWND: 2, Owner: 1, Class: "#32770", Title: "Save scene?", Visible: true, Enabled: true}
	custom := modal
	custom.Class = "UnityContainerWndClass"
	custom.Title = "Custom"
	progress := modal
	progress.Owner = 0
	progress.Title = "Progress"
	enabledMain := main
	enabledMain.Enabled = true
	hidden := modal
	hidden.Visible = false
	foreign := modal
	foreign.PID = 200
	lower := custom
	lower.Enabled = false
	top := custom
	top.HWND, top.Owner, top.Title = 3, 2, "Top"
	empty := custom
	empty.Title = ""
	floating := enabledMain
	floating.HWND, floating.Title = 4, "Floating"
	for _, tc := range []struct {
		name              string
		windows           []editorWindow
		title             string
		fresh, dead, want bool
	}{
		{name: "modal", windows: []editorWindow{main, modal}, want: true, title: modal.Title},
		{name: "custom", windows: []editorWindow{main, custom}, want: true, title: custom.Title},
		{name: "progress", windows: []editorWindow{enabledMain, progress}},
		{name: "other PID", windows: []editorWindow{main, foreign}},
		{name: "hidden", windows: []editorWindow{main, hidden}},
		{name: "main only disabled", windows: []editorWindow{main}},
		{name: "nested", windows: []editorWindow{main, lower, top}, want: true, title: top.Title},
		{name: "floating", windows: []editorWindow{floating, main, custom}, want: true, title: custom.Title},
		{name: "owned fallback", windows: []editorWindow{main, floating, {PID: 100, HWND: 3, Owner: 4, Visible: true, Enabled: true, Title: "Owned"}}, want: true, title: "Owned"},
		{name: "unowned fallback", windows: []editorWindow{main, floating}, want: true, title: floating.Title},
		{name: "empty title", windows: []editorWindow{main, empty}, want: true},
		{name: "fresh", windows: []editorWindow{main, modal}, fresh: true},
		{name: "dead", windows: []editorWindow{main, modal}, dead: true},
	} {
		t.Run(tc.name, func(t *testing.T) {
			c := NewClient()
			c.processDeadChecker = func(int) bool { return tc.dead }
			calls := 0
			c.enumerateWindows = func(int) []editorWindow { calls++; return tc.windows }
			inst := &Instance{PID: 100, Timestamp: 1}
			if tc.fresh {
				inst.Timestamp = time.Now().UnixMilli()
			}
			title, found := c.BlockingDialog(inst)
			if found != tc.want || found && title != tc.title {
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
	c.enumerateWindows = func(int) []editorWindow {
		return []editorWindow{
			{PID: 100, HWND: 1, Visible: true},
			{PID: 100, HWND: 2, Owner: 1, Class: "#32770", Title: "Probe", Visible: true, Enabled: true},
		}
	}
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
