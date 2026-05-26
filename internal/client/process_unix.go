//go:build !windows

package client

import (
	"errors"
	"os"
	"syscall"
)

// checkProcessDead returns true only when the process is confirmed to not exist (ESRCH).
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
	// ESRCH means the process does not exist
	if errors.Is(err, syscall.ESRCH) {
		return true
	}
	// Unknown error — be conservative, assume alive
	return false
}
