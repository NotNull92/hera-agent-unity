package mcpserver

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/jsonrpc"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type asyncTaskSender struct {
	response *client.CommandResponse
	started  func()
}

func (sender *asyncTaskSender) SendWithOptions(context.Context, *client.Instance, string, any, int, client.SendOptions) (*client.CommandResponse, error) {
	sender.started()
	return sender.response, nil
}

func TestTaskFallbackBlocksUntilResult(t *testing.T) {
	statusDir := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	resultPath := filepath.Join(statusDir, "test-results-8093-"+runID+".json")
	sender := &asyncTaskSender{response: runningTestResponse(runID), started: func() {
		writeMCPTaskFile(t, filepath.Join(statusDir, "test-pending-8093-"+runID+".json"), `{"run_id":"`+runID+`","port":8093}`)
		go func() {
			time.Sleep(25 * time.Millisecond)
			_ = os.WriteFile(resultPath, []byte(`{"success":true,"message":"tests done","data":{"passed":1}}`), 0o600)
		}()
	}}
	runtime := taskTestRuntime(statusDir, sender, false)
	started := time.Now()

	result, err := invokeTool(context.Background(), runtime, toolInvocation{
		tool: taskTestTool(), params: map[string]any{"mode": "EditMode"},
		operationID: "op_0123456789abcdef0123456789abcdef", request: negotiatedTaskRequest(),
	})
	if err != nil {
		t.Fatal(err)
	}
	if result.IsError || time.Since(started) < 20*time.Millisecond {
		t.Fatalf("result=%#v elapsed=%s", result, time.Since(started))
	}
	if _, err := os.Stat(resultPath); !os.IsNotExist(err) {
		t.Fatalf("fallback did not consume result file: %v", err)
	}
}

func TestPackageTaskAdapterPreservesJobIdentity(t *testing.T) {
	jobID := "pkg-0123456789abcdef0123456789abcdef"
	data, _ := json.Marshal(map[string]any{"port": 8093, "job_id": jobID, "action": "embed", "identifier": "com.example.package"})
	start, taskable, err := taskStart("manage_packages", map[string]any{"action": "embed", "identifier": "com.example.package"}, &client.CommandResponse{
		Success: true, Message: "running", Data: data,
	}, "op_0123456789abcdef0123456789abcdef")
	if err != nil || !taskable {
		t.Fatalf("start=%#v taskable=%t error=%v", start, taskable, err)
	}
	if start.Kind != taskbridge.KindPackage || start.UnderlyingID != jobID || start.Action != "embed" {
		t.Fatalf("start=%#v", start)
	}
}

func TestPackageListIsNotGeneralizedIntoTask(t *testing.T) {
	data, _ := json.Marshal(map[string]any{"port": 8093, "job_id": "pkg-0123456789abcdef0123456789abcdef"})
	_, taskable, err := taskStart("manage_packages", map[string]any{"action": "list"}, &client.CommandResponse{
		Success: true, Message: "running", Data: data,
	}, "op_0123456789abcdef0123456789abcdef")
	if err != nil || taskable {
		t.Fatalf("taskable=%t error=%v", taskable, err)
	}
}

func TestUnrelatedRunningResponseIsNotGeneralizedIntoTask(t *testing.T) {
	_, taskable, err := taskStart("custom_job", map[string]any{}, &client.CommandResponse{
		Success: true, Message: "running", Data: json.RawMessage(`not-json`),
	}, "op_0123456789abcdef0123456789abcdef")
	if err != nil || taskable {
		t.Fatalf("taskable=%t error=%v", taskable, err)
	}
}

func TestPackageTaskFallbackBlocksUntilResult(t *testing.T) {
	statusDir := t.TempDir()
	jobID := "pkg-0123456789abcdef0123456789abcdef"
	data, _ := json.Marshal(map[string]any{"port": 8093, "job_id": jobID, "action": "remove", "identifier": "com.example.package"})
	sender := &asyncTaskSender{response: &client.CommandResponse{Success: true, Message: "running", Data: data}, started: func() {
		writeMCPTaskFile(t, filepath.Join(statusDir, "package-pending-8093-"+jobID+".json"), `{"job_id":"`+jobID+`","port":8093,"action":"remove","identifier":"com.example.package"}`)
		writeMCPTaskFile(t, filepath.Join(statusDir, "package-result-8093-"+jobID+".json"), `{"success":true,"message":"removed"}`)
	}}
	runtime := taskTestRuntime(statusDir, sender, false)
	result, err := invokeTool(context.Background(), runtime, toolInvocation{
		tool: packageTaskTestTool(), params: map[string]any{"action": "remove", "identifier": "com.example.package"},
		operationID: "op_0123456789abcdef0123456789abcdef", request: plainTaskRequest(),
	})
	if err != nil || result.IsError {
		t.Fatalf("result=%#v error=%v", result, err)
	}
}

func TestNegotiatedTaskReturnsDurableHandle(t *testing.T) {
	statusDir := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	sender := &asyncTaskSender{response: runningTestResponse(runID), started: func() {
		writeMCPTaskFile(t, filepath.Join(statusDir, "test-pending-8093-"+runID+".json"), `{"run_id":"`+runID+`","port":8093}`)
	}}
	runtime := taskTestRuntime(statusDir, sender, true)

	result, err := invokeTool(context.Background(), runtime, toolInvocation{
		tool: taskTestTool(), params: map[string]any{"mode": "EditMode"},
		operationID: "op_0123456789abcdef0123456789abcdef", request: negotiatedTaskRequest(),
	})
	if err != nil {
		t.Fatal(err)
	}
	taskID, ok := result.Meta[taskMarkerMeta].(string)
	if !ok || taskID == "" {
		t.Fatalf("result meta=%#v", result.Meta)
	}
	if task, err := taskbridge.New(statusDir, mcpTestProjectID).Get(taskID); err != nil || task.State != taskbridge.StateWorking {
		t.Fatalf("task=%#v error=%v", task, err)
	}
}

func TestTaskMiddlewareReturnsExtensionShape(t *testing.T) {
	statusDir := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	writeMCPTaskFile(t, filepath.Join(statusDir, "test-pending-8093-"+runID+".json"), `{"run_id":"`+runID+`","port":8093}`)
	store := taskbridge.New(statusDir, mcpTestProjectID)
	task, err := store.Create(taskbridge.Start{Kind: taskbridge.KindTest, Port: 8093, UnderlyingID: runID, OperationID: "op_0123456789abcdef0123456789abcdef"})
	if err != nil {
		t.Fatal(err)
	}
	middleware := taskResultMiddleware(store)
	result, err := middleware(func(context.Context, string, mcp.Request) (mcp.Result, error) {
		return &mcp.CallToolResult{Meta: mcp.Meta{taskMarkerMeta: task.ID}}, nil
	})(context.Background(), "tools/call", negotiatedTaskRequest())
	if err != nil {
		t.Fatal(err)
	}
	created, ok := result.(*taskResult)
	if !ok || created.ResultType != "task" || created.TaskID != task.ID || created.Status != taskbridge.StateWorking {
		t.Fatalf("result=%#v", result)
	}
}

func TestTaskExtensionAdvertisesAndServesDurableState(t *testing.T) {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	statusDir := t.TempDir()
	runID := "0123456789abcdef0123456789abcdef"
	writeMCPTaskFile(t, filepath.Join(statusDir, "test-pending-8093-"+runID+".json"), `{"run_id":"`+runID+`","port":8093}`)
	store := taskbridge.New(statusDir, mcpTestProjectID)
	task, err := store.Create(taskbridge.Start{Kind: taskbridge.KindTest, Port: 8093, UnderlyingID: runID, OperationID: "op_0123456789abcdef0123456789abcdef"})
	if err != nil {
		t.Fatal(err)
	}

	server := newServerWithTasks(enabledTestConfig(), true)
	if err := registerTaskBridge(server, nativeRuntime{tasks: store, taskMode: true}); err != nil {
		t.Fatal(err)
	}
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	done := make(chan error, 1)
	go func() { done <- runServer(ctx, server, serverTransport) }()
	clientRuntime := mcp.NewClient(&mcp.Implementation{Name: "task-test", Version: "test"}, &mcp.ClientOptions{Capabilities: &mcp.ClientCapabilities{}})
	if err := mcp.AddSendingCustomMethod[*taskParams, *taskResult](clientRuntime, "tasks/get"); err != nil {
		t.Fatal(err)
	}
	if err := mcp.AddSendingCustomMethod[*taskParams, *taskAckResult](clientRuntime, "tasks/cancel"); err != nil {
		t.Fatal(err)
	}
	session, err := clientRuntime.Connect(ctx, clientTransport, nil)
	if err != nil {
		t.Fatal(err)
	}
	defer func() {
		_ = session.Close()
		select {
		case err := <-done:
			if err != nil {
				t.Errorf("server error: %v", err)
			}
		case <-time.After(time.Second):
			t.Error("server did not stop")
		}
	}()
	if _, ok := session.InitializeResult().Capabilities.Extensions[taskExtension]; !ok {
		t.Fatalf("extensions=%#v", session.InitializeResult().Capabilities.Extensions)
	}
	got, err := mcp.CallCustomMethod[*taskParams, *taskResult](ctx, session, "tasks/get", &taskParams{TaskID: task.ID})
	if err != nil || got.Status != taskbridge.StateWorking || got.ResultType != "complete" {
		t.Fatalf("get=%#v error=%v", got, err)
	}
	cancelResult, err := mcp.CallCustomMethod[*taskParams, *taskAckResult](ctx, session, "tasks/cancel", &taskParams{TaskID: task.ID})
	if err != nil || cancelResult.Supported || cancelResult.Cancelled || cancelResult.Reason == "" {
		t.Fatalf("cancel=%#v error=%v", cancelResult, err)
	}
	encoded, err := json.Marshal(cancelResult)
	if err != nil || !bytes.Contains(encoded, []byte(`"supported":false`)) || !bytes.Contains(encoded, []byte(`"cancelled":false`)) {
		t.Fatalf("cancel JSON=%s error=%v", encoded, err)
	}
	_, err = mcp.CallCustomMethod[*taskParams, *taskResult](ctx, session, "tasks/get", &taskParams{TaskID: "invalid"})
	var rpcErr *jsonrpc.Error
	if !errors.As(err, &rpcErr) || rpcErr.Code != jsonrpc.CodeInvalidParams {
		t.Fatalf("invalid task error=%v", err)
	}
}

func taskTestRuntime(statusDir string, sender toolSender, taskMode bool) nativeRuntime {
	tool := taskTestTool()
	return nativeRuntime{
		instance: &client.Instance{Port: 8093, Features: []string{client.FeatureOperationLedgerV1, client.FeatureTaskBridgeV1}},
		snapshot: &toolregistry.Snapshot{Catalog: &toolregistry.Catalog{ProjectID: mcpTestProjectID, CatalogHash: nativeTestCatalogHash, Tools: []toolregistry.Tool{tool}}},
		sender:   sender, timeout: 2_000, tasks: taskbridge.New(statusDir, mcpTestProjectID), taskMode: taskMode,
	}
}

func taskTestTool() toolregistry.Tool {
	return toolregistry.Tool{Name: "run_tests", ContractMode: toolregistry.ContractLegacy, Safety: toolregistry.Safety{
		RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true,
	}}
}

func packageTaskTestTool() toolregistry.Tool {
	return toolregistry.Tool{Name: "manage_packages", ContractMode: toolregistry.ContractLegacy, Safety: toolregistry.Safety{
		RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true,
	}}
}

func runningTestResponse(runID string) *client.CommandResponse {
	data, _ := json.Marshal(map[string]any{"port": 8093, "run_id": runID})
	return &client.CommandResponse{Success: true, Message: "running", Data: data}
}

func plainTaskRequest() *mcp.CallToolRequest {
	return &mcp.CallToolRequest{Params: &mcp.CallToolParamsRaw{Name: "run_tests", Arguments: json.RawMessage(`{"mode":"EditMode"}`)}}
}

func negotiatedTaskRequest() *mcp.CallToolRequest {
	request := plainTaskRequest()
	request.Params.Meta = mcp.Meta{
		mcp.MetaKeyClientCapabilities: map[string]any{"extensions": map[string]any{taskExtension: map[string]any{}}},
	}
	return request
}

func writeMCPTaskFile(t *testing.T, path, content string) {
	t.Helper()
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatal(err)
	}
}
