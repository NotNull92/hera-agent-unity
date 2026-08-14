//go:build windows

package cmd

import (
	"os"
	"os/exec"
	"syscall"

	"github.com/NotNull92/hera-agent-unity/internal/unityprocess"
)

func configureUnityEditorProcess(command *exec.Cmd) {
	command.SysProcAttr = &syscall.SysProcAttr{CreationFlags: 0x00000200}
	unityprocess.ConfigureEnvironment(command)
}

func stopUnityEditor(pid int) error {
	process, err := os.FindProcess(pid)
	if err != nil {
		return err
	}
	return process.Kill()
}
