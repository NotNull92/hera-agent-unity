//go:build !windows

package client

import (
	"errors"
	"os"
	"syscall"
)

// checkProcessDead recognizes both Go's ErrProcessDone and the OS ESRCH result.
// Returns false for permission errors (EPERM) or any other transient failure,
// meaning the instance file will be preserved.
func checkProcessDead(pid int) bool {
	proc, err := os.FindProcess(pid)
	if err != nil {
		return true
	}
	err = proc.Signal(syscall.Signal(0))
	if err == nil {
		return false // process exists and we have permission
	}
	// EPERM means the process exists but we lack permission to signal it
	if errors.Is(err, syscall.EPERM) {
		return false
	}
	// Go converts ESRCH to ErrProcessDone on modern Unix process paths.
	if errors.Is(err, os.ErrProcessDone) || errors.Is(err, syscall.ESRCH) {
		return true
	}
	// Unknown error — be conservative, assume alive
	return false
}
