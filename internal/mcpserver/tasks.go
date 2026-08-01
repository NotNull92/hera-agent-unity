package mcpserver

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/modelcontextprotocol/go-sdk/jsonrpc"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const (
	taskExtension  = "io.modelcontextprotocol/tasks"
	taskMarkerMeta = "io.hera-agent-unity/task-id"
)

type taskParams struct {
	mcp.ParamsBase
	TaskID string `json:"taskId"`
}

type updateTaskParams struct {
	mcp.ParamsBase
	TaskID         string         `json:"taskId"`
	InputResponses map[string]any `json:"inputResponses"`
}

type taskResult struct {
	mcp.ResultBase
	ResultType     string                `json:"resultType"`
	TaskID         string                `json:"taskId"`
	Status         taskbridge.State      `json:"status"`
	StatusMessage  string                `json:"statusMessage,omitempty"`
	CreatedAt      string                `json:"createdAt"`
	LastUpdatedAt  string                `json:"lastUpdatedAt"`
	TTLMS          *int64                `json:"ttlMs"`
	PollIntervalMS int64                 `json:"pollIntervalMs,omitempty"`
	Result         map[string]any        `json:"result,omitempty"`
	Error          *taskbridge.TaskError `json:"error,omitempty"`
}

type taskAckResult struct {
	mcp.ResultBase
	ResultType string `json:"resultType"`
	Supported  bool   `json:"supported"`
	Cancelled  bool   `json:"cancelled"`
	Reason     string `json:"reason,omitempty"`
}

func registerTaskBridge(server *mcp.Server, runtime nativeRuntime) error {
	if runtime.tasks == nil || !runtime.taskMode {
		return nil
	}
	server.AddReceivingMiddleware(taskResultMiddleware(runtime.tasks))
	if err := mcp.AddReceivingCustomMethod(server, "tasks/get", func(_ context.Context, _ *mcp.ServerSession, params *taskParams) (*taskResult, error) {
		if params == nil || params.TaskID == "" {
			return nil, invalidTaskParamsError()
		}
		task, err := runtime.tasks.Get(params.TaskID)
		if err != nil {
			return nil, taskMethodError(err)
		}
		return detailedTaskResult(runtime, task)
	}); err != nil {
		return err
	}
	if err := mcp.AddReceivingCustomMethod(server, "tasks/update", func(_ context.Context, _ *mcp.ServerSession, params *updateTaskParams) (*taskAckResult, error) {
		if params == nil || params.TaskID == "" {
			return nil, invalidTaskParamsError()
		}
		if _, err := runtime.tasks.Get(params.TaskID); err != nil {
			return nil, taskMethodError(err)
		}
		return nil, fmt.Errorf("task does not accept input updates")
	}); err != nil {
		return err
	}
	return mcp.AddReceivingCustomMethod(server, "tasks/cancel", func(_ context.Context, _ *mcp.ServerSession, params *taskParams) (*taskAckResult, error) {
		if params == nil || params.TaskID == "" {
			return nil, invalidTaskParamsError()
		}
		cancel, err := runtime.tasks.Cancel(params.TaskID)
		if err != nil {
			return nil, taskMethodError(err)
		}
		return &taskAckResult{ResultType: "complete", Supported: cancel.Supported, Cancelled: cancel.Cancelled, Reason: cancel.Reason}, nil
	})
}

func supportsTasks(request *mcp.CallToolRequest) bool {
	if request == nil {
		return false
	}
	capabilities := request.ClientCapabilities()
	if capabilities == nil || capabilities.Extensions == nil {
		return false
	}
	_, ok := capabilities.Extensions[taskExtension]
	return ok
}

func taskStart(toolName string, params map[string]any, response *client.CommandResponse, operationID string) (taskbridge.Start, bool, error) {
	if response == nil || !response.Success || response.Message != "running" {
		return taskbridge.Start{}, false, nil
	}
	kind := taskbridge.KindTest
	action := ""
	switch toolName {
	case "run_tests":
	case "manage_packages":
		action, _ = params["action"].(string)
		if action != "add" && action != "remove" && action != "embed" {
			return taskbridge.Start{}, false, nil
		}
		kind = taskbridge.KindPackage
	default:
		return taskbridge.Start{}, false, nil
	}
	var data struct {
		Port  int    `json:"port"`
		RunID string `json:"run_id"`
		JobID string `json:"job_id"`
	}
	if err := json.Unmarshal(response.Data, &data); err != nil {
		return taskbridge.Start{}, false, fmt.Errorf("decode asynchronous %s response: %w", toolName, err)
	}
	if kind == taskbridge.KindTest {
		if data.Port == 0 || data.RunID == "" {
			return taskbridge.Start{}, false, fmt.Errorf("run_tests returned running without durable run metadata")
		}
		return taskbridge.Start{Kind: taskbridge.KindTest, Port: data.Port, UnderlyingID: data.RunID, OperationID: operationID}, true, nil
	}
	if data.Port == 0 || data.JobID == "" {
		return taskbridge.Start{}, false, fmt.Errorf("manage_packages returned running without durable job metadata")
	}
	return taskbridge.Start{Kind: taskbridge.KindPackage, Port: data.Port, UnderlyingID: data.JobID, OperationID: operationID, Action: action}, true, nil
}

func taskResultMiddleware(store *taskbridge.Store) mcp.Middleware {
	return func(next mcp.MethodHandler) mcp.MethodHandler {
		return func(ctx context.Context, method string, request mcp.Request) (mcp.Result, error) {
			result, err := next(ctx, method, request)
			if err != nil || method != "tools/call" {
				return result, err
			}
			callResult, ok := result.(*mcp.CallToolResult)
			if !ok || callResult.Meta == nil {
				return result, nil
			}
			taskID, ok := callResult.Meta[taskMarkerMeta].(string)
			if !ok || taskID == "" {
				return result, nil
			}
			task, getErr := store.Get(taskID)
			if getErr != nil {
				return nil, taskMethodError(getErr)
			}
			return createTaskResult(task), nil
		}
	}
}

func createTaskResult(task *taskbridge.Task) *taskResult {
	return &taskResult{
		ResultType: "task", TaskID: task.ID, Status: task.State, StatusMessage: task.StatusMessage,
		CreatedAt:     task.CreatedAt.Format("2006-01-02T15:04:05.000Z07:00"),
		LastUpdatedAt: task.UpdatedAt.Format("2006-01-02T15:04:05.000Z07:00"), PollIntervalMS: 500,
	}
}

func detailedTaskResult(runtime nativeRuntime, task *taskbridge.Task) (*taskResult, error) {
	result := createTaskResult(task)
	result.ResultType = "complete"
	result.Error = task.Error
	if task.State == taskbridge.StateCompleted && len(task.Result) > 0 {
		var response client.CommandResponse
		if err := json.Unmarshal(task.Result, &response); err != nil {
			return nil, fmt.Errorf("decode task result: %w", err)
		}
		encoded, err := json.Marshal(boundedCommandResult(runtime, toolInvocation{
			operationID: client.OperationID(task.OperationID),
		}, &response))
		if err != nil {
			return nil, err
		}
		if err := json.Unmarshal(encoded, &result.Result); err != nil {
			return nil, err
		}
	}
	return result, nil
}

func taskMethodError(err error) error {
	if errors.Is(err, taskbridge.ErrInvalidTaskID) {
		return invalidTaskParamsError()
	}
	if errors.Is(err, taskbridge.ErrTaskNotFound) {
		return &jsonrpc.Error{Code: -32001, Message: "task not found"}
	}
	return err
}

func invalidTaskParamsError() error {
	return &jsonrpc.Error{Code: jsonrpc.CodeInvalidParams, Message: "taskId is required and must be a valid Hera task identifier"}
}
