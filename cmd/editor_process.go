package cmd

import (
	"fmt"
	"os/exec"
)

func startUnityEditor(executable, project string) (int, error) {
	command := exec.Command(executable, unityEditorArguments(project)...)
	configureUnityEditorProcess(command)
	if err := command.Start(); err != nil {
		return 0, err
	}
	pid := command.Process.Pid
	if err := command.Process.Release(); err != nil {
		return 0, fmt.Errorf("release Unity process handle: %w", err)
	}
	return pid, nil
}

func unityEditorArguments(project string) []string {
	return []string{"-projectPath", project}
}
