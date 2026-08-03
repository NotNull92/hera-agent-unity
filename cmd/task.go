package cmd

import (
	"encoding/json"
	"fmt"
	"io"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type taskCLI struct {
	store  *taskbridge.Store
	port   int
	output io.Writer
}

type taskView struct {
	TaskID        string           `json:"task_id"`
	Kind          taskbridge.Kind  `json:"kind"`
	State         taskbridge.State `json:"status"`
	StatusMessage string           `json:"status_message,omitempty"`
	Port          int              `json:"port"`
	UnderlyingID  string           `json:"underlying_id"`
	CreatedAt     string           `json:"created_at"`
	UpdatedAt     string           `json:"updated_at"`
	Result        json.RawMessage  `json:"result,omitempty"`
}

func taskCmd(config GlobalConfig, args []string) error {
	inst, err := discoverStatusInstance(config.Project, config.Port)
	if err != nil {
		return fmt.Errorf("discover Unity project for durable tasks: %w", err)
	}
	projectID, err := toolregistry.ProjectID(inst.ProjectPath)
	if err != nil {
		return fmt.Errorf("identify Unity project for durable tasks: %w", err)
	}
	return (taskCLI{
		store:  taskbridge.New(paths.StatusDir(), projectID),
		port:   inst.Port,
		output: os.Stdout,
	}).Run(args)
}

func (command taskCLI) Run(args []string) error {
	if len(args) == 0 {
		return fmt.Errorf("usage: hera-agent-unity task <list|status>")
	}
	switch args[0] {
	case "list":
		if len(args) != 1 {
			return fmt.Errorf("task list does not accept positional arguments")
		}
		tasks, err := command.store.List(command.port)
		if err != nil {
			return fmt.Errorf("list durable tasks: %w", err)
		}
		views := make([]taskView, len(tasks))
		for i := range tasks {
			views[i] = newTaskView(&tasks[i])
		}
		return json.NewEncoder(command.output).Encode(struct {
			Tasks []taskView `json:"tasks"`
		}{Tasks: views})
	case "status":
		if len(args) != 2 {
			return fmt.Errorf("usage: hera-agent-unity task status <task_id>")
		}
		task, err := command.store.Get(args[1])
		if err != nil {
			return fmt.Errorf("read durable task status: %w", err)
		}
		return json.NewEncoder(command.output).Encode(newTaskView(task))
	default:
		return fmt.Errorf("unknown task action %q; expected list or status", args[0])
	}
}

func newTaskView(task *taskbridge.Task) taskView {
	return taskView{
		TaskID: task.ID, Kind: task.Kind, State: task.State, StatusMessage: task.StatusMessage,
		Port: task.Port, UnderlyingID: task.UnderlyingID,
		CreatedAt: task.CreatedAt.Format(time.RFC3339Nano), UpdatedAt: task.UpdatedAt.Format(time.RFC3339Nano),
		Result: task.Result,
	}
}
