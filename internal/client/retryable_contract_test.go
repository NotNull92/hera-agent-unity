package client

import (
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strconv"
	"strings"
	"testing"
)

// The retryable contract: a 503 carrying retryable=true proves the command was NOT
// executed, so an idempotent caller may repeat the identical request. Callers that did
// not declare idempotency receive the original envelope untouched.

func newRetryTestClient(t *testing.T, handler http.HandlerFunc) (*Client, *Instance) {
	t.Helper()
	server := httptest.NewServer(handler)
	t.Cleanup(server.Close)
	port, err := strconv.Atoi(strings.TrimPrefix(server.URL, "http://127.0.0.1:"))
	if err != nil {
		t.Fatalf("parse test server port: %v", err)
	}
	client := NewClient()
	client.httpClient = server.Client()
	return client, &Instance{Port: port, ProjectPath: t.TempDir(), PID: 4242}
}

func reloadingEnvelope() string {
	return `{"success":false,"code":"RELOADING","message":"The Editor is compiling/reloading",` +
		`"retryable":true,"state":"compiling","retry_after_ms":5}`
}

func TestRetryableReloadingIsRetriedForIdempotentCallers(t *testing.T) {
	calls := 0
	client, instance := newRetryTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls++
		if calls == 1 {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusServiceUnavailable)
			_, _ = w.Write([]byte(reloadingEnvelope()))
			return
		}
		_, _ = w.Write([]byte(`{"success":true,"message":"ok"}`))
	})

	resp, err := client.doWithReloadRetry(
		context.Background(),
		[]byte(`{"command":"refresh_unity","params":{}}`),
		instance,
		"/command",
		retryPolicy{allowRetry: true},
	)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("status = %d, want 200 after retry", resp.StatusCode)
	}
	if calls != 2 {
		t.Fatalf("calls = %d, want 2 (RELOADING then retry)", calls)
	}
}

func TestRetryableReloadingIsReturnedForNonIdempotentCallers(t *testing.T) {
	calls := 0
	client, instance := newRetryTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls++
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusServiceUnavailable)
		_, _ = w.Write([]byte(reloadingEnvelope()))
	})

	resp, err := client.doWithReloadRetry(
		context.Background(),
		[]byte(`{"command":"manage_gameobject","params":{}}`),
		instance,
		"/command",
		retryPolicy{allowRetry: false},
	)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusServiceUnavailable {
		t.Fatalf("status = %d, want 503 passthrough", resp.StatusCode)
	}
	if calls != 1 {
		t.Fatalf("calls = %d, want 1 (no retry without an idempotent caller)", calls)
	}
	body, _ := io.ReadAll(resp.Body)
	if !strings.Contains(string(body), "RELOADING") {
		t.Fatalf("envelope body was lost: %q", body)
	}
}

func TestNonRetryableServiceUnavailableIsNotRetried(t *testing.T) {
	calls := 0
	client, instance := newRetryTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls++
		w.WriteHeader(http.StatusServiceUnavailable)
		_, _ = w.Write([]byte(`{"success":false,"code":"SOMETHING_ELSE","message":"nope"}`))
	})

	resp, err := client.doWithReloadRetry(
		context.Background(),
		[]byte(`{"command":"refresh_unity","params":{}}`),
		instance,
		"/command",
		retryPolicy{allowRetry: true},
	)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	defer resp.Body.Close()
	if calls != 1 {
		t.Fatalf("calls = %d, want 1 (503 without retryable is not retried)", calls)
	}
}
