package assetconfig

import (
	"bytes"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/protocol"
)

const (
	configLockRetryInterval = 25 * time.Millisecond
	configLockStaleAfter    = time.Duration(protocol.AssetConfigLockStaleAfterMilliseconds) * time.Millisecond
)

var (
	configLockTimeout     = 5 * time.Second
	configLockNow         = time.Now
	configLockSleep       = time.Sleep
	configLockProcessDead = checkConfigProcessDead
)

type configLockRecord struct {
	Version      int    `json:"version"`
	PID          int    `json:"pid"`
	AcquiredAtMS int64  `json:"acquired_at_ms"`
	Nonce        string `json:"nonce"`
}

func acquireConfigLock(path string) (func(), error) {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return nil, fmt.Errorf("create asset-config directory for lock: %w", err)
	}

	lockPath := path + ".lock"
	deadline := configLockNow().Add(configLockTimeout)
	for {
		lockFile, err := os.OpenFile(lockPath, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0o600)
		if err == nil {
			record, recordErr := newConfigLockRecord()
			if recordErr != nil {
				_ = lockFile.Close()
				_ = os.Remove(lockPath)
				return nil, recordErr
			}
			if writeErr := writeConfigLock(lockFile, record); writeErr != nil {
				_ = lockFile.Close()
				_ = os.Remove(lockPath)
				return nil, writeErr
			}
			return func() {
				_ = lockFile.Close()
				releaseOwnedConfigLock(lockPath, record.Nonce)
			}, nil
		}
		if !errors.Is(err, fs.ErrExist) {
			return nil, fmt.Errorf("create asset-config lock: %w", err)
		}
		if tryRecoverStaleConfigLock(lockPath) {
			continue
		}
		if configLockNow().After(deadline) {
			return nil, fmt.Errorf("asset-config is busy: timed out waiting for %s", lockPath)
		}
		configLockSleep(configLockRetryInterval)
	}
}

func newConfigLockRecord() (configLockRecord, error) {
	var nonce [16]byte
	if _, err := rand.Read(nonce[:]); err != nil {
		return configLockRecord{}, fmt.Errorf("generate asset-config lock nonce: %w", err)
	}
	return configLockRecord{
		Version:      protocol.AssetConfigLockVersion,
		PID:          os.Getpid(),
		AcquiredAtMS: configLockNow().UTC().UnixMilli(),
		Nonce:        hex.EncodeToString(nonce[:]),
	}, nil
}

func writeConfigLock(file *os.File, record configLockRecord) error {
	data, err := json.Marshal(record)
	if err != nil {
		return fmt.Errorf("encode asset-config lock: %w", err)
	}
	if _, err := file.Write(data); err != nil {
		return fmt.Errorf("write asset-config lock: %w", err)
	}
	if err := file.Sync(); err != nil {
		return fmt.Errorf("flush asset-config lock: %w", err)
	}
	return nil
}

func tryRecoverStaleConfigLock(lockPath string) bool {
	info, err := os.Stat(lockPath)
	if errors.Is(err, fs.ErrNotExist) {
		return true
	}
	if err != nil || configLockNow().Sub(info.ModTime()) < configLockStaleAfter {
		return false
	}

	first, err := os.ReadFile(lockPath)
	if errors.Is(err, fs.ErrNotExist) {
		return true
	}
	if err != nil {
		return false
	}
	var record configLockRecord
	valid := json.Unmarshal(first, &record) == nil &&
		record.Version == protocol.AssetConfigLockVersion && record.PID > 0 && record.Nonce != ""
	if valid && !configLockProcessDead(record.PID) {
		return false
	}

	second, err := os.ReadFile(lockPath)
	if errors.Is(err, fs.ErrNotExist) {
		return true
	}
	if err != nil || !bytes.Equal(first, second) {
		return false
	}
	if err := os.Remove(lockPath); err != nil && !errors.Is(err, fs.ErrNotExist) {
		return false
	}
	return true
}

func releaseOwnedConfigLock(lockPath, nonce string) {
	data, err := os.ReadFile(lockPath)
	if err != nil {
		return
	}
	var record configLockRecord
	if json.Unmarshal(data, &record) != nil || record.Version != protocol.AssetConfigLockVersion || record.Nonce != nonce {
		return
	}
	_ = os.Remove(lockPath)
}

func writeConfigAtomically(path string, data []byte) error {
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return fmt.Errorf("create asset-config directory: %w", err)
	}

	tmp, err := os.CreateTemp(dir, ".asset-config-*.tmp")
	if err != nil {
		return fmt.Errorf("create asset-config temporary file: %w", err)
	}
	tmpPath := tmp.Name()
	defer func() { _ = os.Remove(tmpPath) }()

	if err := tmp.Chmod(0o644); err != nil {
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
