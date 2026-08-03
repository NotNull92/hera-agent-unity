package client

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strconv"
	"strings"
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

func TestDebugBodyRedactsApprovalTokens(t *testing.T) {
	got := debugBody([]byte(`{"meta":{"approval_token":"command-secret"},"data":{"token":"preflight-secret"}}`))
	if strings.Contains(got, "command-secret") || strings.Contains(got, "preflight-secret") ||
		strings.Count(got, "[redacted]") != 2 {
		t.Fatalf("debug body=%s", got)
	}
}

func TestSendWithOptionsCarriesApprovalToken(t *testing.T) {
	// Given
	token := "approval-token"
	var got CommandRequest
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		defer request.Body.Close()
		if err := json.NewDecoder(request.Body).Decode(&got); err != nil {
			t.Fatal(err)
		}
		_ = json.NewEncoder(writer).Encode(CommandResponse{Success: true, Message: "OK"})
	}))
	defer server.Close()
	parsedURL, err := url.Parse(server.URL)
	if err != nil {
		t.Fatal(err)
	}
	port, err := strconv.Atoi(parsedURL.Port())
	if err != nil || !strings.HasPrefix(parsedURL.Host, "127.0.0.1:") {
		t.Fatalf("fixture URL=%q port error=%v", server.URL, err)
	}

	// When
	_, err = DefaultClient.SendWithOptions(context.Background(), &Instance{Port: port}, "scene", map[string]any{"action": "close"}, 1000, SendOptions{ApprovalToken: token})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if got.Meta.ApprovalToken == nil || *got.Meta.ApprovalToken != token {
		t.Fatalf("approval_token=%v", got.Meta.ApprovalToken)
	}
	if got.Meta.ProtocolVersion != ExecutionProtocolVersion {
		t.Fatalf("protocol_version=%q, want %q", got.Meta.ProtocolVersion, ExecutionProtocolVersion)
	}
}

func TestPreflightSendsArgumentsForConnectorAuthority(t *testing.T) {
	// Given
	var got approvalPreflightWireRequest
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		defer request.Body.Close()
		if request.URL.Path != "/approval/preflight" {
			t.Fatalf("path=%q", request.URL.Path)
		}
		if err := json.NewDecoder(request.Body).Decode(&got); err != nil {
			t.Fatal(err)
		}
		_ = json.NewEncoder(writer).Encode(CommandResponse{
			Success: true,
			Message: "Approval preflight",
			Data:    json.RawMessage(`{"token":"signed-token","operation_id":"op_preflight_test","expires_at_ms":4102444800000,"summary":{"tool":"exec","target":"parameters: code","side_effect":"unity_editor_and_project","reversible":false,"may_reload_domain":false,"external_impact":false,"operation_id":"op_preflight_test"}}`),
		})
	}))
	defer server.Close()
	parsedURL, err := url.Parse(server.URL)
	if err != nil {
		t.Fatal(err)
	}
	port, err := strconv.Atoi(parsedURL.Port())
	if err != nil {
		t.Fatal(err)
	}

	// When
	_, err = DefaultClient.PreflightApproval(context.Background(), &Instance{Port: port}, ApprovalPreflightRequest{
		Command: "exec", Params: map[string]any{"code": "return null;"}, OperationID: "op_preflight_test",
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	arguments, ok := got.Arguments.(map[string]any)
	if !ok || arguments["code"] != "return null;" || got.Tool != "exec" {
		t.Fatalf("request=%#v", got)
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
