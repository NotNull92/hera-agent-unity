package resultstore

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"time"
)

type cacheFile struct {
	path     string
	size     int64
	modified time.Time
}

func (store *Store) pruneLocked(protectedPath string) error {
	files := make([]cacheFile, 0)
	err := filepath.WalkDir(store.root, func(path string, entry os.DirEntry, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if entry.IsDir() || filepath.Ext(entry.Name()) != ".json" {
			return nil
		}
		info, err := entry.Info()
		if err != nil {
			return err
		}
		files = append(files, cacheFile{path: path, size: info.Size(), modified: info.ModTime()})
		return nil
	})
	if errors.Is(err, os.ErrNotExist) {
		return nil
	}
	if err != nil {
		return fmt.Errorf("scan result cache: %w", err)
	}
	cutoff := store.now().UTC().Add(-store.retention)
	kept := files[:0]
	var total int64
	for _, file := range files {
		if file.modified.Before(cutoff) {
			if err := os.Remove(file.path); err != nil && !os.IsNotExist(err) {
				return err
			}
			continue
		}
		kept = append(kept, file)
		total += file.size
	}
	slices.SortFunc(kept, func(left, right cacheFile) int {
		if order := left.modified.Compare(right.modified); order != 0 {
			return order
		}
		return strings.Compare(left.path, right.path)
	})
	for _, file := range kept {
		if total <= store.maxBytes {
			break
		}
		if file.path == protectedPath {
			continue
		}
		if err := os.Remove(file.path); err != nil && !os.IsNotExist(err) {
			return err
		}
		total -= file.size
	}
	return nil
}
