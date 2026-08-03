//go:build windows

package assetconfig

import (
	"syscall"
	"unsafe"
)

var (
	assetConfigKernel32           = syscall.NewLazyDLL("kernel32.dll")
	assetConfigOpenProcess        = assetConfigKernel32.NewProc("OpenProcess")
	assetConfigGetExitCodeProcess = assetConfigKernel32.NewProc("GetExitCodeProcess")
)

const (
	assetConfigProcessQueryLimitedInfo = 0x1000
	assetConfigStillActive             = 259
	assetConfigErrorInvalidParameter   = syscall.Errno(87)
)

// checkConfigProcessDead returns true only when the process is confirmed dead.
// Access-denied and indeterminate states are treated as alive so a live lock is
// never stolen.
func checkConfigProcessDead(pid int) bool {
	handle, _, err := assetConfigOpenProcess.Call(
		uintptr(assetConfigProcessQueryLimitedInfo),
		0,
		uintptr(pid),
	)
	if handle == 0 {
		if errno, ok := err.(syscall.Errno); ok {
			switch errno {
			case assetConfigErrorInvalidParameter:
				return true
			case syscall.ERROR_ACCESS_DENIED:
				return false
			}
		}
		return false
	}
	defer func() {
		_ = syscall.CloseHandle(syscall.Handle(handle))
	}()

	var exitCode uint32
	result, _, _ := assetConfigGetExitCodeProcess.Call(
		handle,
		uintptr(unsafe.Pointer(&exitCode)),
	)
	if result == 0 {
		return false
	}
	return exitCode != assetConfigStillActive
}
