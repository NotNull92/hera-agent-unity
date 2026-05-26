package cmd

import (
	"os"
	"path/filepath"
	"runtime"
)

// getInstallPaths returns the canonical install directory and binary path
// used by the install.ps1 / install.sh bootstrap scripts. uninstall reads
// these to know where to delete from.
func getInstallPaths() (dir, bin string) {
	home, _ := os.UserHomeDir()
	switch runtime.GOOS {
	case "windows":
		// WindowsApps is on the default user PATH in Windows 10+,
		// so install.ps1 places the binary here.
		dir = filepath.Join(home, "AppData", "Local", "Microsoft", "WindowsApps")
		bin = filepath.Join(dir, "hera-agent-unity.exe")
	default:
		dir = filepath.Join(home, ".local", "bin")
		bin = filepath.Join(dir, "hera-agent-unity")
	}
	return
}
