package cmd

import (
	"bufio"
	"flag"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"runtime"
	"strings"
)

func parseEditorBootstrapFlags(args []string) (string, error) {
	flags := flag.NewFlagSet("editor bootstrap", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	hubRoot := flags.String("hub-root", envString("UNITY_HUB_EDITOR", ""), "Unity Hub Editor root")
	if err := flags.Parse(args); err != nil {
		return "", err
	}
	if flags.NArg() != 0 {
		return "", fmt.Errorf("unexpected editor bootstrap arguments: %s", strings.Join(flags.Args(), " "))
	}
	return *hubRoot, nil
}

func readUnityProjectVersion(project string) (string, string, error) {
	abs, err := filepath.Abs(project)
	if err != nil {
		return "", "", fmt.Errorf("resolve project path: %w", err)
	}
	if resolved, resolveErr := filepath.EvalSymlinks(abs); resolveErr == nil {
		abs = resolved
	}
	versionFile := filepath.Join(abs, "ProjectSettings", "ProjectVersion.txt")
	file, err := os.Open(versionFile)
	if err != nil {
		return "", "", fmt.Errorf("read Unity project metadata %s: %w", versionFile, err)
	}
	defer file.Close()
	scanner := bufio.NewScanner(file)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if version, ok := strings.CutPrefix(line, "m_EditorVersion:"); ok && strings.TrimSpace(version) != "" {
			return filepath.Clean(abs), strings.TrimSpace(version), nil
		}
	}
	if err := scanner.Err(); err != nil {
		return "", "", fmt.Errorf("read Unity project metadata %s: %w", versionFile, err)
	}
	return "", "", fmt.Errorf("m_EditorVersion is missing from %s", versionFile)
}

func resolveUnityExecutable(version, hubRoot string) (string, error) {
	if strings.TrimSpace(hubRoot) == "" {
		switch runtime.GOOS {
		case "windows":
			hubRoot = filepath.Join(os.Getenv("ProgramFiles"), "Unity", "Hub", "Editor")
		case "darwin":
			hubRoot = "/Applications/Unity/Hub/Editor"
		default:
			home, _ := os.UserHomeDir()
			hubRoot = filepath.Join(home, "Unity", "Hub", "Editor")
		}
	}
	var executable string
	switch runtime.GOOS {
	case "windows":
		executable = filepath.Join(hubRoot, version, "Editor", "Unity.exe")
	case "darwin":
		executable = filepath.Join(hubRoot, version, "Unity.app", "Contents", "MacOS", "Unity")
	default:
		executable = filepath.Join(hubRoot, version, "Editor", "Unity")
	}
	info, err := os.Stat(executable)
	if err != nil || info.IsDir() {
		return "", fmt.Errorf("Unity %s is not installed at %s; pass --hub-root or set UNITY_HUB_EDITOR", version, executable)
	}
	return filepath.Clean(executable), nil
}
