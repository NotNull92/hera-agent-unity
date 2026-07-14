package assetconfig

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"time"
)

const configLockRetryInterval = 25 * time.Millisecond

var configLockTimeout = 5 * time.Second

func acquireConfigLock(path string) (func(), error) {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil {
		return nil, fmt.Errorf("create asset-config directory for lock: %w", err)
	}

	lockPath := path + ".lock"
	deadline := time.Now().Add(configLockTimeout)

	for {
		lockFile, err := os.OpenFile(lockPath, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0600)
		if err == nil {
			return func() {
				_ = lockFile.Close()
				_ = os.Remove(lockPath)
			}, nil
		}
		if !errors.Is(err, fs.ErrExist) {
			return nil, fmt.Errorf("create asset-config lock: %w", err)
		}
		if time.Now().After(deadline) {
			return nil, fmt.Errorf("asset-config is busy: timed out waiting for %s", lockPath)
		}
		time.Sleep(configLockRetryInterval)
	}
}

func writeConfigAtomically(path string, data []byte) error {
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0755); err != nil {
		return fmt.Errorf("create asset-config directory: %w", err)
	}

	tmp, err := os.CreateTemp(dir, ".asset-config-*.tmp")
	if err != nil {
		return fmt.Errorf("create asset-config temporary file: %w", err)
	}
	tmpPath := tmp.Name()
	defer func() { _ = os.Remove(tmpPath) }()

	if err := tmp.Chmod(0644); err != nil {
		_ = tmp.Close()
		return fmt.Errorf("set asset-config temporary file permissions: %w", err)
	}
	if _, err := tmp.Write(data); err != nil {
		_ = tmp.Close()
		return fmt.Errorf("write asset-config temporary file: %w", err)
	}
	if err := tmp.Sync(); err != nil {
		_ = tmp.Close()
		return fmt.Errorf("flush asset-config temporary file: %w", err)
	}
	if err := tmp.Close(); err != nil {
		return fmt.Errorf("close asset-config temporary file: %w", err)
	}
	if err := os.Rename(tmpPath, path); err != nil {
		return fmt.Errorf("replace asset-config: %w", err)
	}

	return nil
}

func preserveCurrentExtensions(path string, cfg *AssetConfig) error {
	data, err := os.ReadFile(path)
	if errors.Is(err, fs.ErrNotExist) {
		return nil
	}
	if err != nil {
		return fmt.Errorf("read current asset-config: %w", err)
	}

	var current AssetConfig
	if err := json.Unmarshal(data, &current); err != nil {
		return fmt.Errorf("read malformed current asset-config: %w", err)
	}

	cfg.extra = mergeRawMessages(cfg.extra, current.extra)
	currentByID := make(map[string]AssetEntry, len(current.Assets))
	for _, entry := range current.Assets {
		currentByID[entry.ID] = entry
	}
	present := make(map[string]bool, len(cfg.Assets))
	for i := range cfg.Assets {
		present[cfg.Assets[i].ID] = true
		if currentEntry, ok := currentByID[cfg.Assets[i].ID]; ok {
			cfg.Assets[i].extra = mergeRawMessages(cfg.Assets[i].extra, currentEntry.extra)
		}
	}
	for _, entry := range current.Assets {
		if !present[entry.ID] {
			entry.extra = cloneRawMessages(entry.extra)
			cfg.Assets = append(cfg.Assets, entry)
		}
	}

	return nil
}
