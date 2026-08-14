//go:build windows

package unityprocess

import (
	"os"
	"os/exec"
	"strings"
)

func ConfigureEnvironment(command *exec.Cmd) {
	if strings.TrimSpace(os.Getenv("ALLUSERSPROFILE")) == "" {
		if programData := strings.TrimSpace(os.Getenv("ProgramData")); programData != "" {
			command.Env = append(command.Environ(), "ALLUSERSPROFILE="+programData)
		}
	}
}
