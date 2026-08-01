package telemetry

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
	"sync"
)

type JSONLRecorder struct {
	path string
	mu   sync.Mutex
}

func NewJSONLRecorder(path string) (*JSONLRecorder, error) {
	if strings.TrimSpace(path) == "" {
		return nil, fmt.Errorf("telemetry path is required")
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		return nil, fmt.Errorf("create telemetry directory: %w", err)
	}
	return &JSONLRecorder{path: path}, nil
}

func (recorder *JSONLRecorder) Record(event Event) error {
	if err := event.Validate(); err != nil {
		return err
	}
	line, err := json.Marshal(event)
	if err != nil {
		return fmt.Errorf("encode telemetry event: %w", err)
	}
	recorder.mu.Lock()
	defer recorder.mu.Unlock()
	file, err := os.OpenFile(recorder.path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o600)
	if err != nil {
		return fmt.Errorf("open telemetry JSONL: %w", err)
	}
	defer file.Close()
	line = append(line, '\n')
	if _, err := file.Write(line); err != nil {
		return fmt.Errorf("append telemetry JSONL: %w", err)
	}
	return file.Sync()
}

func ReadJSONL(path string) ([]Event, error) {
	file, err := os.Open(path)
	if err != nil {
		return nil, fmt.Errorf("open telemetry JSONL: %w", err)
	}
	defer file.Close()
	return DecodeJSONL(file)
}

func DecodeJSONL(reader io.Reader) ([]Event, error) {
	scanner := bufio.NewScanner(reader)
	scanner.Buffer(make([]byte, 64*1024), 1024*1024)
	var events []Event
	for line := 1; scanner.Scan(); line++ {
		decoder := json.NewDecoder(strings.NewReader(scanner.Text()))
		decoder.DisallowUnknownFields()
		var event Event
		if err := decoder.Decode(&event); err != nil {
			return nil, fmt.Errorf("decode telemetry line %d: %w", line, err)
		}
		if decoder.Decode(&struct{}{}) != io.EOF {
			return nil, fmt.Errorf("decode telemetry line %d: trailing data", line)
		}
		if err := event.Validate(); err != nil {
			return nil, fmt.Errorf("validate telemetry line %d: %w", line, err)
		}
		events = append(events, event)
	}
	if err := scanner.Err(); err != nil {
		return nil, fmt.Errorf("scan telemetry JSONL: %w", err)
	}
	return events, nil
}
