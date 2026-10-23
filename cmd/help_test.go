package cmd

import (
	"errors"
	"io/fs"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"testing"
)

func TestHelpTopicsExistForRoutedCommands(t *testing.T) {
	topics := []string{
		"batch",
		"call",
		"console",
		"doctor",
		"editor",
		"exec",
		"find_gameobjects",
		"input",
		"install",
		"list",
		"log",
		"manage_assets",
		"manage_animation",
		"manage_components",
		"manage_gameobject",
		"manage_packages",
		"manage_prefab",
		"manage_timeline",
		"menu",
		"ping",
		"profiler",
		"reserialize",
		"scene",
		"screenshot",
		"status",
		"test",
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
		"describe_type":       true,
		"find_method":         true,
		"list_assemblies":     true,
		"describe_shader":     true,
		"manage_material":     true,
		"manage_asset_import": true,
		"manage_ui":           true,
		"game_feel":           true,
		"ui_slop":             true,

		"bake":              true,
		"build":             true,
		"batch":             true,
		"call":              true,
		"console":           true,
		"custom-tools":      true,
		"doctor":            true,
		"editor":            true,
		"exec":              true,
		"find_gameobjects":  true,
		"general":           true,
		"input":             true,
		"install":           true,
		"list":              true,
		"log":               true,
		"manage_assets":     true,
		"manage_animation":  true,
		"manage_components": true,
		"manage_gameobject": true,
		"manage_packages":   true,
		"manage_prefab":     true,
		"manage_settings":   true,
		"manage_timeline":   true,
		"menu":              true,
		"ping":              true,
		"profiler":          true,
		"reserialize":       true,
		"scene":             true,
		"screenshot":        true,
		"setup":             true,
		"status":            true,
		"task":              true,
		"test":              true,
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

func TestRetiredHelpTopicsAreAbsent(t *testing.T) {
	for _, topic := range []string{"html-to-uidoc", "ui_doc"} {
		t.Run(topic, func(t *testing.T) {
			_, err := helpFS.ReadFile("help/" + topic + ".txt")
			if !errors.Is(err, fs.ErrNotExist) {
				t.Fatalf("retired help topic %q is still embedded", topic)
			}
		})
	}
}

func TestInputHelpWarnsAboutGitBashPathConversion(t *testing.T) {
	data, err := helpFS.ReadFile("help/input.txt")
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(data), "MSYS_NO_PATHCONV=1") {
		t.Fatal("input help must explain how Git Bash preserves Unity hierarchy paths")
	}
}

func TestGeneralHelpMakesTimeoutUnitsUnambiguous(t *testing.T) {
	data, err := helpFS.ReadFile("help/general.txt")
	if err != nil {
		t.Fatal(err)
	}
	help := string(data)
	if !strings.Contains(help, "--timeout 120000") || !strings.Contains(help, "2 minutes") {
		t.Fatal("general help must include a concrete millisecond timeout example")
	}
}

func TestRoutedToolHelp(t *testing.T) {
	// Generic dispatch accepts every HeraTool name; specialized routes also have aliases.
	files, err := filepath.Glob("../AgentConnector/Editor/Tools/*.cs")
	if err != nil {
		t.Fatal(err)
	}
	name := regexp.MustCompile(`(?s)\[HeraTool\(\s*Name\s*=\s*"([^"]+)"`)
	aliases := map[string]string{"execute_csharp": "exec", "read_console": "console", "manage_scene": "scene", "manage_editor": "manage_editor", "execute_menu_item": "menu", "editor_screenshot": "screenshot", "reserialize_assets": "reserialize", "manage_profiler": "profiler", "log_to_console": "log", "refresh_unity": "editor", "detect_assets": "asset-config"}
	for _, file := range files {
		data, err := os.ReadFile(file)
		if err != nil {
			t.Fatal(err)
		}
		match := name.FindSubmatch(data)
		if match == nil {
			continue
		}
		topic := string(match[1])
		if alias, ok := aliases[topic]; ok {
			topic = alias
		}
		if topic == "asset-config" {
			continue
		} // Dedicated help renderer.
		if _, err := helpFS.ReadFile("help/" + topic + ".txt"); err != nil {
			t.Errorf("%s: %v", topic, err)
		}
	}
}
