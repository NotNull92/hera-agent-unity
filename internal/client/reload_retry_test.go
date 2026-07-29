package client

import (
	"context"
	"errors"
	"io"
	"net/http"
	"strings"
	"testing"
	"time"
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
	originalDelay := reloadRetryDelay
	reloadRetryDelay = time.Millisecond
	t.Cleanup(func() { reloadRetryDelay = originalDelay })
	attempts := 0
	c := NewClient()
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

func jsonResponse(body string) *http.Response {
	return &http.Response{
		StatusCode:    http.StatusOK,
		ContentLength: int64(len(body)),
		Body:          io.NopCloser(strings.NewReader(body)),
	}
}
