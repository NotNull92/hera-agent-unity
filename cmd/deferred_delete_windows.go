//go:build windows

package cmd

import (
	"os"
	"os/exec"
	"strings"
)

// scheduleDelete tries os.Remove first; on failure (typically a Windows file
// lock because the file is the running .exe or its .bak still memory-mapped),
// it schedules a delayed deletion via a hidden PowerShell process that
// outlives the current CLI so the file disappears once the lock is released.
//
// Returns:
//   - deferred=false, err=nil  → file was deleted immediately (or didn't exist)
//   - deferred=true,  err=nil  → deletion was scheduled and will run shortly
//   - deferred=*,     err!=nil → scheduling itself failed; file remains
//
// Implementation note: an earlier version used `cmd /c timeout /t 1 && del`,
// but `timeout` aborts immediately with "Input redirection is not supported"
// whenever it is launched without a TTY (i.e. always, from our perspective).
// The non-zero exit short-circuited `&&`, so `del` never ran and the binary
// survived the uninstall while the success message still printed. PowerShell
// `Start-Sleep` has no such requirement.
func scheduleDelete(path string) (deferred bool, err error) {
	if _, statErr := os.Stat(path); os.IsNotExist(statErr) {
		return false, nil
	}
	if rmErr := os.Remove(path); rmErr == nil {
		return false, nil
	}
	psPath := "'" + strings.ReplaceAll(path, "'", "''") + "'"
	script := "Start-Sleep -Seconds 2; Remove-Item -LiteralPath " + psPath +
		" -Force -ErrorAction SilentlyContinue"
	cmd := exec.Command("powershell.exe", "-NoProfile", "-WindowStyle", "Hidden",
		"-Command", script)
	if startErr := cmd.Start(); startErr != nil {
		return false, startErr
	}
	return true, nil
}
