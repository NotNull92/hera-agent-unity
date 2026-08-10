//go:build !windows

package cmd

import (
	"os"
	"os/exec"
	"syscall"
)

func configureUnityEditorProcess(command *exec.Cmd) {
	command.SysProcAttr = &syscall.SysProcAttr{Setpgid: true}
}

func stopUnityEditor(pid int) error {
	process, err := os.FindProcess(pid)
	if err != nil {
		return err
	}
	return process.Signal(syscall.SIGTERM)
}
