package cmd

import (
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/tui"
)

func installCmd() error {
	printInstallHeader()

	// Step 1: Detect current binary
	exe, err := os.Executable()
	if err != nil {
		return printInstallError("Cannot locate current binary", err)
	}
	exe, err = filepath.EvalSymlinks(exe)
	if err != nil {
		return printInstallError("Cannot resolve binary path", err)
	}

	// Step 2: Determine install path
	installDir, installPath := getInstallPaths()
	printStep("Install directory", installDir)
	printStep("Binary path", installPath)

	// Step 3: Create install directory
	if err := os.MkdirAll(installDir, 0755); err != nil {
		return printInstallError("Failed to create install directory", err)
	}
	printDone("Created install directory")

	// Step 4: Copy self to install path
	if err := copyFile(exe, installPath); err != nil {
		return printInstallError("Failed to copy binary", err)
	}
	printDone("Copied binary to install path")

	// Step 5: Make executable (Unix)
	if runtime.GOOS != "windows" {
		if err := os.Chmod(installPath, 0755); err != nil {
			return printInstallError("Failed to set executable permission", err)
		}
		printDone("Set executable permission")
	}

	// Step 6: Cleanup legacy install (Windows only — pre-WindowsApps locations)
	if runtime.GOOS == "windows" {
		cleanupLegacyInstall(exe)
		printDone("Cleaned legacy install directories (if any)")
	}

	// Step 7: PATH handling
	//   Windows: WindowsApps is auto-PATH, so we only clean stale legacy entries.
	//   Unix:    append export line to shell rc files.
	if err := addToPATH(installDir); err != nil {
		return printInstallError("Failed to update PATH", err)
	}
	if runtime.GOOS == "windows" {
		printDone("Cleaned legacy PATH entries (if any)")
	} else {
		printDone("Added to PATH")
	}

	// Step 8: Print success
	printInstallSuccess(installPath, exe)
	return nil
}

// legacyInstallDirs returns pre-WindowsApps install locations that may still
// hold leftover binaries / PATH entries from older versions:
//   - %LOCALAPPDATA%\hera-agent-unity       (post-rebrand, pre-WindowsApps)
//   - %LOCALAPPDATA%\unity-agent-cli-pro  (pre-rebrand)
//
// Returns nil on non-Windows platforms.
func legacyInstallDirs() []string {
	if runtime.GOOS != "windows" {
		return nil
	}
	home, _ := os.UserHomeDir()
	return []string{
		filepath.Join(home, "AppData", "Local", "hera-agent-unity"),
		filepath.Join(home, "AppData", "Local", "unity-agent-cli-pro"),
	}
}

// cleanupLegacyInstall removes legacy binary files and empty legacy directories.
// Skips the running binary itself (can't delete an executable that's currently running).
func cleanupLegacyInstall(currentExe string) {
	for _, dir := range legacyInstallDirs() {
		// Try both possible binary names (post-rebrand and pre-rebrand)
		candidates := []string{
			filepath.Join(dir, "hera-agent-unity.exe"),
			filepath.Join(dir, "unity-agent-cli-pro.exe"),
		}
		for _, bin := range candidates {
			if _, err := os.Stat(bin); err != nil {
				continue
			}
			if strings.EqualFold(bin, currentExe) {
				continue // can't delete self
			}
			_ = os.Remove(bin)
		}
		// Remove dir if empty (os.Remove only removes empty dirs)
		_ = os.Remove(dir)
	}
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()

	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer out.Close()

	if _, err := io.Copy(out, in); err != nil {
		return err
	}
	return out.Close()
}

func addToPATH(installDir string) error {
	if runtime.GOOS == "windows" {
		return addToPATHWindows(installDir)
	}
	return addToPATHUnix(installDir)
}

// addToPATHWindows does NOT register installDir — WindowsApps is already on the
// default user PATH. Its only job is to scrub legacy entries (variants of
// %LOCALAPPDATA%\hera-agent-unity and %LOCALAPPDATA%\unity-agent-cli-pro,
// including \\-double-backslash variants and duplicates from earlier buggy
// installs). Matching is done by case-insensitive suffix on the basename so
// `System.IO.Path` normalisation failures cannot leave stale entries behind.
func addToPATHWindows(_ string) error {
	return scrubLegacyUserPATH()
}

// scrubLegacyUserPATH rewrites the current user's PATH, dropping any entry
// whose basename (after backslash normalisation) matches hera-agent-unity or
// unity-agent-cli-pro. Empty entries are also discarded. Idempotent.
func scrubLegacyUserPATH() error {
	script := `
$p = [Environment]::GetEnvironmentVariable("Path", "User")
if (-not $p) { return }
$legacy = @('hera-agent-unity','unity-agent-cli-pro')
$filtered = @()
foreach ($entry in ($p -split ';')) {
    $trimmed = $entry.Trim()
    if (-not $trimmed) { continue }
    $norm = $trimmed -replace '\\+','\' -replace '\\$',''
    $leaf = Split-Path -Leaf $norm
    $skip = $false
    foreach ($name in $legacy) {
        if ([string]::Equals($leaf, $name, [System.StringComparison]::OrdinalIgnoreCase)) {
            $skip = $true
            break
        }
    }
    if (-not $skip) { $filtered += $trimmed }
}
$new = $filtered -join ';'
if ($new -ne $p) { [Environment]::SetEnvironmentVariable("Path", $new, "User") }
`
	cmd := exec.Command("powershell.exe", "-NoProfile", "-Command", script)
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	return cmd.Run()
}

func addToPATHUnix(installDir string) error {
	home, err := os.UserHomeDir()
	if err != nil {
		return err
	}

	exportLine := fmt.Sprintf(`export PATH="%s:$PATH"`, installDir)

	// Update .bashrc
	bashrc := filepath.Join(home, ".bashrc")
	_ = appendLineIfNotExists(bashrc, exportLine)

	// Update .zshrc if exists
	zshrc := filepath.Join(home, ".zshrc")
	if _, err := os.Stat(zshrc); err == nil {
		_ = appendLineIfNotExists(zshrc, exportLine)
	}

	return nil
}

func appendLineIfNotExists(path, line string) error {
	data, _ := os.ReadFile(path)
	content := string(data)
	if strings.Contains(content, line) {
		return nil
	}
	f, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		return err
	}
	defer f.Close()
	_, err = f.WriteString(line + "\n")
	return err
}

func printInstallHeader() {
	fmt.Println()
	fmt.Println(tui.BrandBanner())
	fmt.Println()
}

func printStep(label, value string) {
	fmt.Printf("  %s %s\n", tui.LabelStyle.Render(label+":"), tui.PathStyle.Render(value))
}

func printDone(msg string) {
	fmt.Printf("  %s %s\n", tui.CheckStyle.Render("✓"), msg)
}

func printInstallError(context string, err error) error {
	fmt.Println()
	fmt.Printf("  %s %s: %v\n", tui.ErrorStyle.Render("✗"), tui.LabelStyle.Render(context), err)
	fmt.Println()
	return fmt.Errorf("%s: %w", context, err)
}

func printInstallSuccess(installedPath, originalPath string) {
	fmt.Println()
	msg := fmt.Sprintf("Your instrument has been commissioned.\n\nEstablished at:\n  %s\n", installedPath)

	if runtime.GOOS == "windows" {
		msg += fmt.Sprintf("\nYou can now delete the original file:\n  %s\n", originalPath)
		msg += "\nAny NEW terminal or IDE will recognize 'hera-agent-unity' immediately"
		msg += "\n(WindowsApps resides on the default user PATH)."
		msg += "\n\nShould an open terminal not yet recognize it, refresh with:\n"
		msg += `  $env:Path = [Environment]::GetEnvironmentVariable("Path","User") + ";" + [Environment]::GetEnvironmentVariable("Path","Machine")`
	} else {
		msg += "\nRun 'source ~/.bashrc' (or ~/.zshrc), or restart your terminal."
	}

	msg += "\n\nNext, instruct your agent to employ it:"
	msg += "\n  - Discover: inquire of Claude Code CLI or Codex in any terminal:"
	msg += "\n      \"Verify that hera-agent-unity is installed and survey its capabilities.\""
	msg += "\n  - Commission (recommended): add to your project's CLAUDE.md / AGENTS.md:"
	msg += "\n      \"For all Unity endeavours, employ hera-agent-unity.\""

	fmt.Println(tui.BoxAccent.Render(tui.SuccessStyle.Render(msg)))
	fmt.Println()
}
