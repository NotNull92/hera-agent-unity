//go:build !windows

package unityprocess

import "os/exec"

func ConfigureEnvironment(_ *exec.Cmd) {}
