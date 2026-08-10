//go:build windows

package cmd

import (
	"os/exec"
	"slices"
	"testing"
)

func TestConfigureUnityEditorProcessRestoresAllUsersProfileFromProgramData(t *testing.T) {
	// Given
	t.Setenv("ALLUSERSPROFILE", "")
	t.Setenv("ProgramData", `C:\ProgramData`)
	command := exec.Command("Unity.exe")

	// When
	configureUnityEditorProcess(command)

	// Then
	if !slices.Contains(command.Env, `ALLUSERSPROFILE=C:\ProgramData`) {
		t.Fatalf("Unity child environment does not restore ALLUSERSPROFILE: %q", command.Env)
	}
}
