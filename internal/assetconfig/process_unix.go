//go:build !windows

package assetconfig

import (
	"errors"
	"os"
	"syscall"
)

// checkConfigProcessDead returns true only when the process is confirmed absent.
// Permission and indeterminate errors are treated as alive.
func checkConfigProcessDead(pid int) bool {
	process, err := os.FindProcess(pid)
	if err != nil {
		return true
	}
	err = process.Signal(syscall.Signal(0))
	if err == nil || errors.Is(err, syscall.EPERM) {
		return false
	}
	if errors.Is(err, syscall.ESRCH) {
		return true
	}
	return false
}
