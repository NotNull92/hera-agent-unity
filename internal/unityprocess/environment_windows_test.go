//go:build windows

package unityprocess

import (
	"os/exec"
	"slices"
	"testing"
)

func TestConfigureEnvironmentRestoresAllUsersProfileFromProgramData(t *testing.T) {
	t.Setenv("ALLUSERSPROFILE", "")
	t.Setenv("ProgramData", `C:\ProgramData`)
	command := exec.Command("Unity.exe")

	ConfigureEnvironment(command)

	if !slices.Contains(command.Env, `ALLUSERSPROFILE=C:\ProgramData`) {
		t.Fatalf("Unity child environment does not restore ALLUSERSPROFILE: %q", command.Env)
	}
}

func TestConfigureEnvironmentPreservesExistingAllUsersProfile(t *testing.T) {
	t.Setenv("ALLUSERSPROFILE", `D:\SharedProfile`)
	t.Setenv("ProgramData", `C:\ProgramData`)
	command := exec.Command("Unity.exe")

	ConfigureEnvironment(command)

	if command.Env != nil {
		t.Fatalf("Unity child environment was unnecessarily replaced: %q", command.Env)
	}
}
