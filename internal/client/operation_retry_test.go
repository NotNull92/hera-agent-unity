package client

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"io"
	"net/http"
	"testing"
)

func TestArgumentsHashUsesCrossRuntimeCanonicalJSON(t *testing.T) {
	hash, err := argumentsHash(map[string]any{"html": "<tag>", "number": 1})
	if err != nil {
		t.Fatalf("argumentsHash: %v", err)
	}
	digest := sha256.Sum256([]byte(`{"html":"<tag>","number":1}`))
	want := "sha256:" + hex.EncodeToString(digest[:])
	if hash != want {
		t.Fatalf("argumentsHash = %q, want %q", hash, want)
	}
}

type operationRoundTripFunc func(*http.Request) (*http.Response, error)

func (fn operationRoundTripFunc) RoundTrip(request *http.Request) (*http.Response, error) {
	return fn(request)
}

func TestIdempotentRetryUsesSameOperationID(t *testing.T) {
	var bodies [][]byte
	client := NewClient()
	client.httpClient = &http.Client{
		Transport: operationRoundTripFunc(func(request *http.Request) (*http.Response, error) {
			body, err := io.ReadAll(request.Body)
			if err != nil {
				t.Fatalf("read request body: %v", err)
			}
			bodies = append(bodies, body)
			if len(bodies) == 1 {
				return &http.Response{
					StatusCode:    http.StatusOK,
					ContentLength: 0,
					Body:          io.NopCloser(bytes.NewReader(nil)),
					Header:        make(http.Header),
				}, nil
			}
			return &http.Response{
				StatusCode:    http.StatusOK,
				ContentLength: int64(len(`{"success":true,"message":"ok"}`)),
				Body:          io.NopCloser(bytes.NewBufferString(`{"success":true,"message":"ok"}`)),
				Header:        make(http.Header),
			}, nil
		}),
	}
	originalDelay := reloadRetryDelay
	reloadRetryDelay = 0
	t.Cleanup(func() { reloadRetryDelay = originalDelay })

	_, err := client.SendWithOptions(
		context.Background(),
		&Instance{Port: 8090, Features: []string{FeatureOperationLedgerV1}},
		"scene",
		map[string]any{"action": "save"},
		1_000,
		SendOptions{OperationID: OperationID("op_test_same_id"), Idempotent: true},
	)
	if err != nil {
		t.Fatalf("SendWithOptions: %v", err)
	}
	if len(bodies) != 2 {
		t.Fatalf("request count = %d, want 2", len(bodies))
	}
	var first, second CommandRequest
	if err := json.Unmarshal(bodies[0], &first); err != nil {
		t.Fatalf("decode first request: %v", err)
	}
	if err := json.Unmarshal(bodies[1], &second); err != nil {
		t.Fatalf("decode second request: %v", err)
	}
	if first.Meta.OperationID != second.Meta.OperationID {
		t.Fatalf("operation IDs differ: %q != %q", first.Meta.OperationID, second.Meta.OperationID)
	}
}

func TestLegacyConnectorDisablesMutationRetry(t *testing.T) {
	requests := 0
	client := NewClient()
	client.httpClient = &http.Client{
		Transport: operationRoundTripFunc(func(request *http.Request) (*http.Response, error) {
			requests++
			return &http.Response{
				StatusCode:    http.StatusOK,
				ContentLength: 0,
				Body:          io.NopCloser(bytes.NewReader(nil)),
				Header:        make(http.Header),
			}, nil
		}),
	}

	_, err := client.SendWithOptions(
		context.Background(),
		&Instance{Port: 8090},
		"scene",
		map[string]any{"action": "save"},
		1_000,
		SendOptions{OperationID: OperationID("op_test_legacy"), Idempotent: false},
	)
	if err == nil {
		t.Fatal("SendWithOptions error = nil, want unknown-outcome error")
	}
	unknown, ok := err.(*OperationOutcomeUnknownError)
	if !ok {
		t.Fatalf("error type = %T, want *OperationOutcomeUnknownError", err)
	}
	if unknown.Code != "OPERATION_OUTCOME_UNKNOWN" {
		t.Fatalf("error code = %q", unknown.Code)
	}
	if requests != 1 {
		t.Fatalf("request count = %d, want 1", requests)
	}
}
