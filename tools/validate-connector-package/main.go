package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"strings"
)

type asmdef struct {
	Name       string   `json:"name"`
	References []string `json:"references"`
}

func main() {
	if err := validate("AgentConnector"); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	fmt.Println("connector package integrity PASS")
}

func validate(root string) error {
	return filepath.WalkDir(root, func(path string, entry fs.DirEntry, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if entry.IsDir() || strings.HasSuffix(path, ".meta") {
			return nil
		}
		if _, err := os.Stat(path + ".meta"); errors.Is(err, os.ErrNotExist) {
			return fmt.Errorf("missing Unity meta file: %s.meta", filepath.ToSlash(path))
		} else if err != nil {
			return err
		}
		if filepath.Ext(path) != ".asmdef" {
			return nil
		}
		data, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		var definition asmdef
		if err := json.Unmarshal(data, &definition); err != nil {
			return fmt.Errorf("parse %s: %w", filepath.ToSlash(path), err)
		}
		seen := make(map[string]struct{}, len(definition.References))
		for _, reference := range definition.References {
			if _, exists := seen[reference]; exists {
				return fmt.Errorf("%s has duplicate reference %q", filepath.ToSlash(path), reference)
			}
			seen[reference] = struct{}{}
		}
		return nil
	})
}
