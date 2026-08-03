package client

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
)

var ErrProjectNotFound = errors.New("unity project not found")

type AmbiguousProjectError struct {
	Query   string
	Matches []string
}

func (err *AmbiguousProjectError) Error() string {
	return fmt.Sprintf("multiple Unity projects match %q: %s", err.Query, strings.Join(err.Matches, ", "))
}

type TargetMismatchError struct {
	Port            int
	ExpectedProject string
	ActualProject   string
}

type TargetUnresponsiveError struct {
	Project string
	Port    int
	PID     int
	State   string
	Cause   error
}

type TargetRestartedError struct {
	Project     string
	PreviousPID int
	CurrentPID  int
	Port        int
	State       string
	Cause       error
}

func (err *TargetRestartedError) Error() string {
	return fmt.Sprintf(
		"Unity project %q restarted while the request was pending (pid %d -> %d, port=%d, state=%s)",
		err.Project,
		err.PreviousPID,
		err.CurrentPID,
		err.Port,
		err.State,
	)
}

func (err *TargetRestartedError) Unwrap() error {
	return err.Cause
}

type TargetLostError struct {
	Project      string
	PreviousPort int
	Cause        error
}

func (err *TargetLostError) Error() string {
	return fmt.Sprintf(
		"Unity project %q disappeared while waiting for a response from port %d",
		err.Project,
		err.PreviousPort,
	)
}

func (err *TargetLostError) Unwrap() error {
	return err.Cause
}

func (err *TargetUnresponsiveError) Error() string {
	return fmt.Sprintf(
		"Unity project %q did not respond on port %d within the request timeout (pid=%d, state=%s)",
		err.Project,
		err.Port,
		err.PID,
		err.State,
	)
}

func (err *TargetUnresponsiveError) Unwrap() error {
	return err.Cause
}

func (err *TargetMismatchError) Error() string {
	return fmt.Sprintf(
		"Unity port %d belongs to project %q, not requested project %q",
		err.Port,
		err.ActualProject,
		err.ExpectedProject,
	)
}

func normalizeProjectPath(path string) string {
	normalized := filepath.ToSlash(filepath.Clean(filepath.FromSlash(strings.TrimSpace(path))))
	if runtime.GOOS == "windows" {
		return strings.ToLower(normalized)
	}
	return normalized
}

func projectPathsEqual(left, right string) bool {
	return normalizeProjectPath(left) == normalizeProjectPath(right)
}

func resolveProjectInstance(instances []Instance, query string) (*Instance, error) {
	normalizedQuery := normalizeProjectPath(query)
	var exact *Instance
	partialByPath := make(map[string]Instance)
	for i := range instances {
		instance := instances[i]
		normalizedPath := normalizeProjectPath(instance.ProjectPath)
		if normalizedPath == normalizedQuery {
			if exact == nil || instance.Timestamp > exact.Timestamp {
				candidate := instance
				exact = &candidate
			}
			continue
		}
		if !strings.Contains(normalizedPath, normalizedQuery) {
			continue
		}
		current, exists := partialByPath[normalizedPath]
		if !exists || instance.Timestamp > current.Timestamp {
			partialByPath[normalizedPath] = instance
		}
	}
	if exact != nil {
		return exact, nil
	}
	if len(partialByPath) == 1 {
		for _, instance := range partialByPath {
			candidate := instance
			return &candidate, nil
		}
	}
	if len(partialByPath) > 1 {
		matches := make([]string, 0, len(partialByPath))
		for _, instance := range partialByPath {
			matches = append(matches, instance.ProjectPath)
		}
		sort.Strings(matches)
		return nil, &AmbiguousProjectError{Query: query, Matches: matches}
	}
	return nil, fmt.Errorf("%w: %s", ErrProjectNotFound, query)
}

func (c *Client) inspectTarget(project string, port int) (*Instance, error) {
	if project == "" {
		return nil, nil
	}
	instances, err := c.ScanInstancesFresh()
	if err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return nil, nil
		}
		return nil, fmt.Errorf("inspect Unity target: %w", err)
	}
	alive := make([]Instance, 0, len(instances))
	var portOwner *Instance
	for i := range instances {
		instance := instances[i]
		if !isActiveInstance(instance) {
			continue
		}
		alive = append(alive, instance)
		if instance.Port == port && (portOwner == nil || instance.Timestamp > portOwner.Timestamp) {
			candidate := instance
			portOwner = &candidate
		}
	}
	if portOwner != nil && !projectPathsEqual(portOwner.ProjectPath, project) {
		return nil, &TargetMismatchError{
			Port:            port,
			ExpectedProject: project,
			ActualProject:   portOwner.ProjectPath,
		}
	}
	target, err := resolveProjectInstance(alive, project)
	if errors.Is(err, ErrProjectNotFound) {
		return nil, nil
	}
	return target, err
}
