package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

const markerName = ".hera-mcp-benchmark-fixture.json"

type fixtureMarker struct {
	Schema     string `json:"schema"`
	Disposable bool   `json:"disposable"`
}

func validateFixture(project string) error {
	abs, err := filepath.Abs(project)
	if err != nil {
		return err
	}
	data, err := os.ReadFile(filepath.Join(abs, markerName))
	if err != nil {
		return fmt.Errorf("refuse benchmark outside a marked disposable fixture: %w", err)
	}
	var marker fixtureMarker
	if err := json.Unmarshal(data, &marker); err != nil {
		return fmt.Errorf("decode disposable fixture marker: %w", err)
	}
	if marker.Schema != "hera.mcp-benchmark-fixture/1" || !marker.Disposable {
		return fmt.Errorf("refuse benchmark: invalid disposable fixture marker")
	}
	if _, err := os.Stat(filepath.Join(abs, "Assets", "HeraAgent", "Editor", "HeraAgent.asmdef")); err != nil {
		return fmt.Errorf("refuse fixture without copied Hera Connector: %w", err)
	}
	if _, err := os.Stat(filepath.Join(abs, "Assets", "HeraAgent", "Editor", "Dependencies", "Newtonsoft.Json.dll")); err != nil {
		return fmt.Errorf("refuse fixture without copied Newtonsoft dependency: %w", err)
	}
	if _, err := os.Stat(filepath.Join(abs, "Assets", "HeraAgent", "Dependencies", "uGUI", "Runtime", "UGUI", "UnityEngine.UI.asmdef")); err != nil {
		return fmt.Errorf("refuse fixture without copied uGUI dependency: %w", err)
	}
	return nil
}

func prepareFixture(unity, destination, connector string) error {
	if unity == "" || destination == "" || connector == "" {
		return fmt.Errorf("--unity, --out, and --connector are required")
	}
	abs, err := filepath.Abs(destination)
	if err != nil {
		return err
	}
	entries, err := os.ReadDir(abs)
	if err == nil && len(entries) != 0 {
		return fmt.Errorf("refuse non-empty fixture destination %s", abs)
	}
	if err != nil && !os.IsNotExist(err) {
		return err
	}
	var output bytes.Buffer
	command := exec.Command(unity, createProjectArguments(abs)...)
	command.Stdout, command.Stderr = &output, &output
	if err := command.Run(); err != nil {
		return fmt.Errorf("create disposable Unity project: %w: %s", err, output.String())
	}
	return finalizeFixture(abs, connector, unity)
}

func createProjectArguments(destination string) []string {
	return []string{"-batchmode", "-quit", "-noUpm", "-createProject", destination, "-logFile", "-"}
}

func finalizeFixture(abs, connector, unity string) error {
	if _, err := os.Stat(filepath.Join(abs, "ProjectSettings", "ProjectSettings.asset")); err != nil {
		return fmt.Errorf("unity did not create a complete project: %w", err)
	}
	connectorAbs, err := filepath.Abs(connector)
	if err != nil {
		return err
	}
	source := filepath.Join(connectorAbs, "Editor")
	destination := filepath.Join(abs, "Assets", "HeraAgent", "Editor")
	if err := copyDirectorySkipping(source, destination, "Tests"); err != nil {
		return err
	}
	editorData := filepath.Join(filepath.Dir(unity), "Data")
	newtonsoft := filepath.Join(editorData, "Managed", "Newtonsoft.Json.dll")
	dependencyDir := filepath.Join(destination, "Dependencies")
	if err := os.MkdirAll(dependencyDir, 0o700); err != nil {
		return err
	}
	info, err := os.Stat(newtonsoft)
	if err != nil {
		return fmt.Errorf("locate Unity Newtonsoft assembly: %w", err)
	}
	if err := copyFile(newtonsoft, filepath.Join(dependencyDir, "Newtonsoft.Json.dll"), info.Mode()); err != nil {
		return err
	}
	ugui := filepath.Join(editorData, "Resources", "PackageManager", "BuiltInPackages", "com.unity.ugui")
	uguiDestination := filepath.Join(abs, "Assets", "HeraAgent", "Dependencies", "uGUI")
	if err := copyDirectorySkipping(ugui, uguiDestination, "Tests", "Documentation~"); err != nil {
		return err
	}
	marker := fixtureMarker{Schema: "hera.mcp-benchmark-fixture/1", Disposable: true}
	return writeFixtureMarker(filepath.Join(abs, markerName), marker)
}

func copyDirectorySkipping(source, destination string, skippedRoots ...string) error {
	if _, err := os.Stat(source); err != nil {
		return fmt.Errorf("read Connector Editor source: %w", err)
	}
	skipped := make(map[string]bool, len(skippedRoots))
	for _, name := range skippedRoots {
		skipped[name] = true
	}
	return filepath.Walk(source, func(path string, info os.FileInfo, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if info.Mode()&os.ModeSymlink != 0 {
			return fmt.Errorf("refuse Connector symlink %s", path)
		}
		relative, err := filepath.Rel(source, path)
		if err != nil {
			return err
		}
		if relative != "." {
			root := strings.Split(filepath.ToSlash(relative), "/")[0]
			if skipped[root] {
				if info.IsDir() {
					return filepath.SkipDir
				}
				return nil
			}
		}
		target := filepath.Join(destination, relative)
		if info.IsDir() {
			return os.MkdirAll(target, 0o700)
		}
		return copyFile(path, target, info.Mode())
	})
}

func copyFile(source, destination string, mode os.FileMode) error {
	input, err := os.Open(source)
	if err != nil {
		return err
	}
	defer input.Close()
	output, err := os.OpenFile(destination, os.O_CREATE|os.O_EXCL|os.O_WRONLY, mode.Perm())
	if err != nil {
		return err
	}
	if _, err := io.Copy(output, input); err != nil {
		output.Close()
		return err
	}
	return output.Close()
}

func writeFixtureMarker(path string, marker fixtureMarker) error {
	data, err := json.MarshalIndent(marker, "", "  ")
	if err != nil {
		return err
	}
	data = append(data, '\n')
	return os.WriteFile(path, data, 0o600)
}
