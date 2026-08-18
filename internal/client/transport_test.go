package client

import (
	"context"
	"encoding/json"
	"errors"
	"net"
	"net/http"
	"net/http/httptest"
	"strconv"
	"testing"
)

func TestClientSend_WhenRootContextCancelled_ReturnsCancellation(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	_, err := NewClient().Send(ctx, &Instance{Port: 1}, "scene", nil, 60_000)
	if !errors.Is(err, context.Canceled) {
		t.Fatalf("Send error = %v, want context cancellation", err)
	}
}

func TestClientSend_WhenServerRejectsWithEnvelope_PreservesMachineReadableError(t *testing.T) {
	// Given: Unity rejects an oversized command with a structured non-200 envelope.
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/command" {
			t.Fatalf("path = %q, want /command", r.URL.Path)
		}
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusRequestEntityTooLarge)
		if err := json.NewEncoder(w).Encode(CommandResponse{
			Success: false,
			Code:    "HTTP_REQUEST_BODY_TOO_LARGE",
			Message: "Request body exceeds 1048576 bytes.",
			Data:    json.RawMessage(`{"maximum_bytes":1048576}`),
		}); err != nil {
			t.Fatalf("encode response: %v", err)
		}
	}))
	t.Cleanup(server.Close)

	_, portText, err := net.SplitHostPort(server.Listener.Addr().String())
	if err != nil {
		t.Fatalf("split listener address: %v", err)
	}
	port, err := strconv.Atoi(portText)
	if err != nil {
		t.Fatalf("parse listener port: %v", err)
	}
	client := NewClient()
	client.httpClient = server.Client()

	// When: the CLI sends a normal command through the transport.
	response, err := client.Send(context.Background(), &Instance{Port: port}, "list", nil, 1_000)

	// Then: the caller receives the stable envelope instead of a string-only transport error.
	if err != nil {
		t.Fatalf("Send error = %v, want nil", err)
	}
	if response == nil {
		t.Fatal("Send response = nil, want structured rejection")
	}
	if response.Success {
		t.Fatal("response.Success = true, want false")
	}
	if response.Code != "HTTP_REQUEST_BODY_TOO_LARGE" {
		t.Fatalf("response.Code = %q, want HTTP_REQUEST_BODY_TOO_LARGE", response.Code)
	}
	if string(response.Data) != `{"maximum_bytes":1048576}` {
		t.Fatalf("response.Data = %s, want maximum_bytes payload", response.Data)
	}
}

func TestClientSendBatch_WhenServerRejectsWithEnvelope_PreservesMachineReadableError(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/commands" {
			t.Fatalf("path = %q, want /commands", r.URL.Path)
		}
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusTooManyRequests)
		if err := json.NewEncoder(w).Encode(map[string]any{
			"success": false,
			"code":    "HTTP_QUEUE_FULL",
			"message": "Too many pending requests; maximum is 64.",
			"data":    map[string]int{"maximum_pending": 64},
		}); err != nil {
			t.Fatalf("encode response: %v", err)
		}
	}))
	t.Cleanup(server.Close)

	_, portText, err := net.SplitHostPort(server.Listener.Addr().String())
	if err != nil {
		t.Fatalf("split listener address: %v", err)
	}
	port, err := strconv.Atoi(portText)
	if err != nil {
		t.Fatalf("parse listener port: %v", err)
	}
	client := NewClient()
	client.httpClient = server.Client()

	response, err := client.SendBatch(context.Background(), &Instance{Port: port}, BatchCommandRequest{
		Commands: []BatchCommandItem{{Command: "list"}},
	}, 1_000)
	if err != nil {
		t.Fatalf("SendBatch error = %v, want nil", err)
	}
	if response == nil {
		t.Fatal("SendBatch response = nil, want structured rejection")
	}
	if response.Success {
		t.Fatal("response.Success = true, want false")
	}
	if response.Code != "HTTP_QUEUE_FULL" {
		t.Fatalf("response.Code = %q, want HTTP_QUEUE_FULL", response.Code)
	}
	if string(response.Data) != `{"maximum_pending":64}` {
		t.Fatalf("response.Data = %s, want maximum_pending payload", response.Data)
	}
}

func TestClientSend_WhenConnectorPredatesAnAction_ReportsVersionSkew(t *testing.T) {
	cases := []struct {
		name     string
		envelope CommandResponse
		wantCode string
	}{
		{
			// An older Connector declares no action contract for the tool, so
			// it rejects "action" as an argument it has never heard of.
			name: "action argument rejected",
			envelope: CommandResponse{
				Success: false,
				Code:    "UNKNOWN_ARGUMENT",
				Message: "Validation failed at '/action'.",
				Data:    json.RawMessage(`{"path":"/action","expected":"async_results, filter, mode","actual":"String"}`),
			},
			wantCode: "CONNECTOR_UPDATE_REQUIRED",
		},
		{
			// A rejection anywhere else is an ordinary caller mistake and must
			// keep its own code.
			name: "unrelated argument rejected",
			envelope: CommandResponse{
				Success: false,
				Code:    "UNKNOWN_ARGUMENT",
				Message: "Validation failed at '/limitt'.",
				Data:    json.RawMessage(`{"path":"/limitt","expected":"filter, limit, mode","actual":"Integer"}`),
			},
			wantCode: "UNKNOWN_ARGUMENT",
		},
	}

	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			// Given: Unity answers with the older Connector's validation envelope.
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				w.Header().Set("Content-Type", "application/json")
				if err := json.NewEncoder(w).Encode(testCase.envelope); err != nil {
					t.Errorf("encode response: %v", err)
				}
			}))
			t.Cleanup(server.Close)

			_, portText, err := net.SplitHostPort(server.Listener.Addr().String())
			if err != nil {
				t.Fatalf("split listener address: %v", err)
			}
			port, err := strconv.Atoi(portText)
			if err != nil {
				t.Fatalf("parse listener port: %v", err)
			}
			client := NewClient()
			client.httpClient = server.Client()

			// When: the CLI sends an action the running Connector may not know.
			response, err := client.Send(
				context.Background(),
				&Instance{Port: port},
				"run_tests",
				map[string]any{"action": "list", "mode": "EditMode"},
				1_000,
			)

			// Then: only the action rejection is reframed as a version skew.
			if err != nil {
				t.Fatalf("Send error = %v, want nil", err)
			}
			if response.Code != testCase.wantCode {
				t.Fatalf("code = %q, want %q", response.Code, testCase.wantCode)
			}
			if testCase.wantCode == "CONNECTOR_UPDATE_REQUIRED" && len(response.Suggestions) == 0 {
				t.Fatalf("response = %#v, want update suggestions", response)
			}
		})
	}
}
