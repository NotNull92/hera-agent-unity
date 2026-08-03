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
	return configProcessSignalConfirmsDead(process.Signal(syscall.Signal(0)))
}

func configProcessSignalConfirmsDead(err error) bool {
	if err == nil || errors.Is(err, syscall.EPERM) {
		return false
	}
	return errors.Is(err, syscall.ESRCH) || errors.Is(err, os.ErrProcessDone)
}
