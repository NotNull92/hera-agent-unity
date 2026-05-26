//go:build !windows

package cmd

import "os"

// scheduleDelete removes the file immediately. Unix does not lock running
// executables, so deferred deletion is never required and the deferred
// return value is always false.
func scheduleDelete(path string) (deferred bool, err error) {
	if _, statErr := os.Stat(path); os.IsNotExist(statErr) {
		return false, nil
	}
	return false, os.Remove(path)
}
