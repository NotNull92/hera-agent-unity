package taskbridge

import (
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"time"
)

type State string

const (
	StateWorking       State = "working"
	StateInputRequired State = "input_required"
	StateCompleted     State = "completed"
	StateFailed        State = "failed"
	StateCancelled     State = "cancelled"
)

type Kind string

const (
	KindTest    Kind = "test"
	KindPackage Kind = "package"
)

type Progress struct {
	Current int `json:"current"`
	Total   int `json:"total"`
}

type TaskError struct {
	Code    int             `json:"code"`
	Message string          `json:"message"`
	Data    json.RawMessage `json:"data,omitempty"`
}

type Task struct {
	ID            string          `json:"taskId"`
	Kind          Kind            `json:"kind"`
	State         State           `json:"status"`
	StatusMessage string          `json:"statusMessage,omitempty"`
	Progress      *Progress       `json:"progress,omitempty"`
	OperationID   string          `json:"operationId,omitempty"`
	Result        json.RawMessage `json:"result,omitempty"`
	Error         *TaskError      `json:"error,omitempty"`
	CreatedAt     time.Time       `json:"createdAt"`
	UpdatedAt     time.Time       `json:"lastUpdatedAt"`
}

type Start struct {
	Kind         Kind
	Port         int
	UnderlyingID string
	OperationID  string
	Action       string
}

type CancelResult struct {
	Supported bool   `json:"supported"`
	Cancelled bool   `json:"cancelled"`
	Reason    string `json:"reason"`
}

type Store struct {
	statusDir string
	now       func() time.Time
}

var (
	ErrTaskNotFound  = errors.New("task not found")
	ErrInvalidTaskID = errors.New("invalid task id")
	safeID           = regexp.MustCompile(`^[A-Za-z0-9_-]{8,160}$`)
)

type taskKey struct {
	Version      int    `json:"v"`
	Kind         Kind   `json:"k"`
	Port         int    `json:"p"`
	UnderlyingID string `json:"u"`
	OperationID  string `json:"o"`
	Action       string `json:"a,omitempty"`
	CreatedMS    int64  `json:"c"`
}

func New(statusDir string) *Store {
	return &Store{statusDir: statusDir, now: time.Now}
}

func (store *Store) Create(start Start) (*Task, error) {
	if err := validateStart(start); err != nil {
		return nil, err
	}
	created := store.now().UTC()
	key := taskKey{
		Version: 1, Kind: start.Kind, Port: start.Port, UnderlyingID: start.UnderlyingID,
		OperationID: start.OperationID, Action: start.Action, CreatedMS: created.UnixMilli(),
	}
	id, err := encodeKey(key)
	if err != nil {
		return nil, err
	}
	return store.Get(id)
}

func (store *Store) Get(taskID string) (*Task, error) {
	key, err := decodeKey(taskID)
	if err != nil {
		return nil, err
	}
	created := time.UnixMilli(key.CreatedMS).UTC()
	task := &Task{
		ID: taskID, Kind: key.Kind, State: StateWorking, StatusMessage: "Unity operation is still running.",
		OperationID: key.OperationID, CreatedAt: created, UpdatedAt: created,
	}
	resultPath := store.resultPath(key)
	if data, stat, readErr := readRegularFile(resultPath); readErr == nil {
		if !json.Valid(data) {
			return nil, fmt.Errorf("read task result: invalid JSON")
		}
		task.State = StateCompleted
		task.StatusMessage = "Unity operation completed."
		task.Result = data
		task.UpdatedAt = latest(created, stat.ModTime().UTC())
		return task, nil
	} else if !errors.Is(readErr, os.ErrNotExist) {
		return nil, fmt.Errorf("read task result: %w", readErr)
	}

	pendingPath := store.pendingPath(key)
	if _, stat, readErr := readRegularFile(pendingPath); readErr == nil {
		task.UpdatedAt = latest(created, stat.ModTime().UTC())
		return task, nil
	} else if !errors.Is(readErr, os.ErrNotExist) {
		return nil, fmt.Errorf("read task pending state: %w", readErr)
	}
	return nil, ErrTaskNotFound
}

func latest(left, right time.Time) time.Time {
	if right.After(left) {
		return right
	}
	return left
}

func (store *Store) Cancel(taskID string) (*CancelResult, error) {
	task, err := store.Get(taskID)
	if err != nil {
		return nil, err
	}
	if task.State != StateWorking {
		return &CancelResult{Supported: false, Cancelled: false, Reason: "task is already terminal"}, nil
	}
	reason := "Unity Package Manager does not expose safe cancellation for package operations"
	if task.Kind == KindTest {
		reason = "the current Unity Test Framework integration does not expose verified run cancellation"
	}
	return &CancelResult{Supported: false, Cancelled: false, Reason: reason}, nil
}

func (store *Store) ResultPath(taskID string) (string, error) {
	key, err := decodeKey(taskID)
	if err != nil {
		return "", err
	}
	return store.resultPath(key), nil
}

func (store *Store) pendingPath(key taskKey) string {
	if key.Kind == KindPackage {
		return filepath.Join(store.statusDir, fmt.Sprintf("package-pending-%d-%s.json", key.Port, key.UnderlyingID))
	}
	return filepath.Join(store.statusDir, fmt.Sprintf("test-pending-%d-%s.json", key.Port, key.UnderlyingID))
}

func (store *Store) resultPath(key taskKey) string {
	if key.Kind == KindPackage {
		return filepath.Join(store.statusDir, fmt.Sprintf("package-result-%d-%s.json", key.Port, key.UnderlyingID))
	}
	return filepath.Join(store.statusDir, fmt.Sprintf("test-results-%d-%s.json", key.Port, key.UnderlyingID))
}

func validateStart(start Start) error {
	if start.Kind != KindTest && start.Kind != KindPackage {
		return fmt.Errorf("%w: unsupported kind %q", ErrInvalidTaskID, start.Kind)
	}
	if start.Port < 1 || start.Port > 65535 || !safeID.MatchString(start.UnderlyingID) || !safeID.MatchString(start.OperationID) {
		return ErrInvalidTaskID
	}
	return nil
}

func encodeKey(key taskKey) (string, error) {
	data, err := json.Marshal(key)
	if err != nil {
		return "", err
	}
	return "hera_task_" + base64.RawURLEncoding.EncodeToString(data), nil
}

func decodeKey(taskID string) (taskKey, error) {
	const prefix = "hera_task_"
	var key taskKey
	if len(taskID) <= len(prefix) || len(taskID) > 1024 || taskID[:len(prefix)] != prefix {
		return key, ErrInvalidTaskID
	}
	data, err := base64.RawURLEncoding.DecodeString(taskID[len(prefix):])
	if err != nil || json.Unmarshal(data, &key) != nil || key.Version != 1 {
		return taskKey{}, ErrInvalidTaskID
	}
	if err := validateStart(Start{Kind: key.Kind, Port: key.Port, UnderlyingID: key.UnderlyingID, OperationID: key.OperationID}); err != nil {
		return taskKey{}, err
	}
	return key, nil
}

func readRegularFile(path string) ([]byte, os.FileInfo, error) {
	stat, err := os.Stat(path)
	if err != nil {
		return nil, nil, err
	}
	if !stat.Mode().IsRegular() {
		return nil, nil, fmt.Errorf("not a regular file")
	}
	data, err := os.ReadFile(path)
	return data, stat, err
}
