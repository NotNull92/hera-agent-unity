//go:build windows

package cmd

import (
	"os"
	"os/exec"
	"strings"
	"syscall"
)

func configureUnityEditorProcess(command *exec.Cmd) {
	command.SysProcAttr = &syscall.SysProcAttr{CreationFlags: 0x00000200}
	if strings.TrimSpace(os.Getenv("ALLUSERSPROFILE")) == "" {
		if programData := strings.TrimSpace(os.Getenv("ProgramData")); programData != "" {
			command.Env = append(command.Environ(), "ALLUSERSPROFILE="+programData)
		}
	}
}

func stopUnityEditor(pid int) error {
	process, err := os.FindProcess(pid)
	if err != nil {
		return err
	}
	return process.Kill()
}
