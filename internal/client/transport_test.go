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
