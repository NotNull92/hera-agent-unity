package cmd

import (
	"io/fs"
	"strings"
	"testing"
)

func TestHelpTopicsExistForRoutedCommands(t *testing.T) {
	topics := []string{
		"batch",
		"console",
		"doctor",
		"editor",
		"exec",
		"find_gameobjects",
		"html-to-uidoc",
		"input",
		"install",
		"list",
		"log",
		"manage_assets",
		"manage_components",
		"manage_gameobject",
		"manage_packages",
		"menu",
		"ping",
		"profiler",
		"reserialize",
		"scene",
		"screenshot",
		"status",
		"test",
		"ui_doc",
		"unity_docs",
		"uninstall",
		"update",
	}

	for _, topic := range topics {
		t.Run(topic, func(t *testing.T) {
			data, err := helpFS.ReadFile("help/" + topic + ".txt")
			if err != nil {
				t.Fatalf("missing help topic: %v", err)
			}
			if !strings.Contains(string(data), "hera-agent-unity") {
				t.Fatalf("help topic %q does not look like CLI help", topic)
			}
		})
	}
}

func TestHelpFilesAreReachableTopics(t *testing.T) {
	routed := map[string]bool{
		"batch":             true,
		"console":           true,
		"custom-tools":      true,
		"doctor":            true,
		"editor":            true,
		"exec":              true,
		"find_gameobjects":  true,
		"general":           true,
		"html-to-uidoc":     true,
		"input":             true,
		"install":           true,
		"list":              true,
		"log":               true,
		"manage_assets":     true,
		"manage_components": true,
		"manage_gameobject": true,
		"manage_packages":   true,
		"menu":              true,
		"ping":              true,
		"profiler":          true,
		"reserialize":       true,
		"scene":             true,
		"screenshot":        true,
		"setup":             true,
		"status":            true,
		"test":              true,
		"ui_doc":            true,
		"unity_docs":        true,
		"uninstall":         true,
		"update":            true,
	}

	entries, err := fs.ReadDir(helpFS, "help")
	if err != nil {
		t.Fatalf("read embedded help dir: %v", err)
	}
	for _, entry := range entries {
		if entry.IsDir() || !strings.HasSuffix(entry.Name(), ".txt") {
			continue
		}
		topic := strings.TrimSuffix(entry.Name(), ".txt")
		if !routed[topic] {
			t.Fatalf("help topic %q is not covered by the routed topic allowlist", topic)
		}
	}
}
