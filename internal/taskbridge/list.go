package taskbridge

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
)

type pendingRecord struct {
	Port      int    `json:"port"`
	RunID     string `json:"run_id"`
	JobID     string `json:"job_id"`
	ProjectID string `json:"project_id"`
	Action    string `json:"action"`
}

func (store *Store) List(port int) ([]Task, error) {
	if port < 1 || port > 65535 {
		return nil, ErrInvalidTaskID
	}
	entries, err := os.ReadDir(store.statusDir)
	if errors.Is(err, os.ErrNotExist) {
		return []Task{}, nil
	}
	if err != nil {
		return nil, err
	}

	tasks := make([]Task, 0)
	for _, entry := range entries {
		kind, ok := pendingKind(entry.Name())
		if entry.IsDir() || !ok {
			continue
		}
		data, stat, readErr := readRegularFile(filepath.Join(store.statusDir, entry.Name()))
		if readErr != nil {
			continue
		}
		var pending pendingRecord
		if json.Unmarshal(data, &pending) != nil || pending.Port != port || pending.ProjectID != store.projectID {
			continue
		}
		underlyingID := pending.RunID
		if kind == KindPackage {
			underlyingID = pending.JobID
		}
		key := taskKey{
			Version: 2, ProjectID: store.projectID, Kind: kind, Port: port, UnderlyingID: underlyingID,
			OperationID: discoveredOperationID(store.projectID, kind, port, underlyingID),
			Action:      pending.Action, CreatedMS: stat.ModTime().UTC().UnixMilli(),
		}
		if err := validateStart(Start{Kind: key.Kind, Port: key.Port, UnderlyingID: key.UnderlyingID, OperationID: key.OperationID}); err != nil {
			continue
		}
		taskID, encodeErr := encodeKey(key)
		if encodeErr != nil {
			return nil, encodeErr
		}
		task, getErr := store.Get(taskID)
		if errors.Is(getErr, ErrTaskNotFound) {
			continue
		}
		if getErr != nil {
			return nil, getErr
		}
		tasks = append(tasks, *task)
	}

	sort.Slice(tasks, func(i, j int) bool {
		if tasks[i].Kind != tasks[j].Kind {
			return tasks[i].Kind < tasks[j].Kind
		}
		return tasks[i].UnderlyingID < tasks[j].UnderlyingID
	})
	return tasks, nil
}

func pendingKind(name string) (Kind, bool) {
	switch {
	case strings.HasPrefix(name, "test-pending-") && strings.HasSuffix(name, ".json"):
		return KindTest, true
	case strings.HasPrefix(name, "package-pending-") && strings.HasSuffix(name, ".json"):
		return KindPackage, true
	default:
		return "", false
	}
}

func discoveredOperationID(projectID string, kind Kind, port int, underlyingID string) string {
	digest := sha256.Sum256([]byte(projectID + "\x00" + string(kind) + "\x00" + underlyingID + "\x00" + strconv.Itoa(port)))
	return "op_discovered_" + hex.EncodeToString(digest[:])
}
