package cmd

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/NotNull92/hera-agent-unity-unity-unity/internal/tui"
)

func uninstallCmd() error {
	printUninstallHeader()

	// Step 1: Confirm
	prompt := fmt.Sprintf("  %s ",
		tui.LabelStyle.Render("This will dissolve hera-agent-unity-unity and all its records. Proceed? (y/N):"))
	confirmed, err := promptConfirm(prompt)
	if err != nil {
		return err
	}
	if !confirmed {
		fmt.Println("  " + tui.InfoStyle.Render("Decommissioning cancelled."))
		return nil
	}
	fmt.Println()

	// Step 2: Detect current binary / install dir
	exe, err := os.Executable()
	if err != nil {
		exe = ""
	} else {
		exe, _ = filepath.EvalSymlinks(exe)
	}
	installDir, _ := getInstallPaths()

	// Step 3: Remove config directory
	if cfgErr := removeConfigDir(); cfgErr != nil {
		printUninstallWarn("Config directory", cfgErr)
	} else {
		printUninstallDone("Removed config directory")
	}

	// Step 4: Remove from PATH
	if pathErr := removeFromPATH(installDir); pathErr != nil {
		printUninstallWarn("PATH cleanup", pathErr)
	} else {
		printUninstallDone("Removed from PATH")
	}

	// Step 5: Remove binary and install directory (OS-specific)
	deferred, delErr := removeBinaryAndDir(exe, installDir)
	if delErr != nil {
		printUninstallWarn("Binary removal", delErr)
	} else if deferred {
		printUninstallDone("Scheduled removal of binary (completes in ~2s)")
	} else {
		printUninstallDone("Removed binary and install directory")
	}

	// Step 6: Success
	printUninstallSuccess(deferred)
	return nil
}

func removeConfigDir() error {
	home, err := os.UserHomeDir()
	if err != nil {
		return err
	}
	var cfgDir string
	if runtime.GOOS == "windows" {
		cfgDir = filepath.Join(home, "AppData", "Roaming", "hera-agent-unity-unity")
	} else {
		cfgDir = filepath.Join(home, ".config", "hera-agent-unity-unity")
	}
	if _, err := os.Stat(cfgDir); os.IsNotExist(err) {
		return nil
	}
	return os.RemoveAll(cfgDir)
}

func removePathEntry(pathList, entry string) string {
	parts := strings.Split(pathList, string(os.PathListSeparator))
	var out []string
	for _, p := range parts {
		if strings.TrimSpace(p) == entry {
			continue
		}
		out = append(out, p)
	}
	return strings.Join(out, string(os.PathListSeparator))
}

func promptConfirm(msg string) (bool, error) {
	fmt.Print(msg)
	reader := bufio.NewReader(os.Stdin)
	input, err := reader.ReadString('\n')
	if err != nil {
		return false, err
	}
	input = strings.TrimSpace(strings.ToLower(input))
	return input == "y" || input == "yes", nil
}

func printUninstallHeader() {
	fmt.Println()
	fmt.Println(tui.BrandBanner())
	fmt.Println()
}

func printUninstallWarn(context string, err error) {
	fmt.Printf("  %s %s: %v\n",
		tui.WarningStyle.Render("!"),
		tui.LabelStyle.Render(context),
		err)
}

func printUninstallDone(msg string) {
	fmt.Printf("  %s %s\n", tui.CheckStyle.Render("✓"), msg)
}

func printUninstallSuccess(deferred bool) {
	fmt.Println()
	msg := "Your instrument has been released.\n\nNo trace remains upon the estate."
	if runtime.GOOS == "windows" {
		if deferred {
			msg += "\n\nThe binary is locked by this process and will be removed by a"
			msg += "\nbackground helper within ~2 seconds. Close this terminal afterwards."
		}
		msg += "\n\nFully close and reopen your IDE or terminal application"
		msg += "\n(not merely the terminal tab) for PATH changes to take full effect."
	} else {
		msg += "\n\nRestart your terminal, or run 'source ~/.bashrc' (or ~/.zshrc)."
	}
	fmt.Println(tui.BoxAccent.Render(tui.SuccessStyle.Render("✓ Decommissioned") + "\n\n" + msg))
	fmt.Println()
}
