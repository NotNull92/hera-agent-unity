package toolregistry

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"slices"
	"strings"
)

func (cache *CatalogCache) projectDir(projectID string) string {
	return filepath.Join(cache.root, strings.TrimPrefix(projectID, "sha256:"))
}

func (cache *CatalogCache) entryPath(key CacheKey) string {
	filename := strings.TrimPrefix(key.CatalogHash, "sha256:") + ".json"
	return filepath.Join(cache.projectDir(key.ProjectID), filename)
}

func writeAtomic(path string, data []byte) (resultErr error) {
	directory := filepath.Dir(path)
	if err := os.MkdirAll(directory, 0o700); err != nil {
		return fmt.Errorf("create catalog cache directory: %w", err)
	}
	file, err := os.CreateTemp(directory, ".catalog-*.tmp")
	if err != nil {
		return fmt.Errorf("create catalog cache temp file: %w", err)
	}
	tempPath := file.Name()
	defer func() {
		cleanupErr := os.Remove(tempPath)
		if os.IsNotExist(cleanupErr) {
			cleanupErr = nil
		}
		resultErr = errors.Join(resultErr, cleanupErr)
	}()
	if err := file.Chmod(0o600); err != nil {
		_ = file.Close()
		return fmt.Errorf("secure catalog cache temp file: %w", err)
	}
	if _, err := file.Write(data); err != nil {
		_ = file.Close()
		return fmt.Errorf("write catalog cache temp file: %w", err)
	}
	if err := file.Sync(); err != nil {
		_ = file.Close()
		return fmt.Errorf("sync catalog cache temp file: %w", err)
	}
	if err := file.Close(); err != nil {
		return fmt.Errorf("close catalog cache temp file: %w", err)
	}
	if err := os.Rename(tempPath, path); err != nil {
		return fmt.Errorf("commit catalog cache: %w", err)
	}
	return nil
}

func (cache *CatalogCache) prune(projectID string) error {
	directory := cache.projectDir(projectID)
	entries, err := os.ReadDir(directory)
	if err != nil {
		return fmt.Errorf("read catalog cache for pruning: %w", err)
	}
	type cacheFile struct {
		path    string
		modTime int64
	}
	files := make([]cacheFile, 0, len(entries))
	for _, entry := range entries {
		if entry.IsDir() || filepath.Ext(entry.Name()) != ".json" {
			continue
		}
		info, statErr := entry.Info()
		if statErr != nil {
			return fmt.Errorf("stat catalog cache entry: %w", statErr)
		}
		files = append(files, cacheFile{
			path:    filepath.Join(directory, entry.Name()),
			modTime: info.ModTime().UnixNano(),
		})
	}
	slices.SortFunc(files, func(left, right cacheFile) int {
		switch {
		case left.modTime > right.modTime:
			return -1
		case left.modTime < right.modTime:
			return 1
		default:
			return strings.Compare(left.path, right.path)
		}
	})
	if len(files) <= cache.maxEntries {
		return nil
	}
	for _, file := range files[cache.maxEntries:] {
		if err := os.Remove(file.path); err != nil {
			return fmt.Errorf("remove old catalog cache entry: %w", err)
		}
	}
	return nil
}
