package cmd

import (
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent/internal/client"
)

// doctorCmd prints a self-diagnostic report covering install path, PATH
// resolution, duplicate copies, shell gotchas, and Unity instance state.
// Intended as the first thing users (and AI agents) try when hera-agent
// "isn't working" — it answers "where is the binary, what does PATH see,
// and is Unity reachable?" without requiring a Unity connection.
func doctorCmd(args []string) error {
	jsonMode := false
	for _, a := range args {
		if a == "--json" {
			jsonMode = true
		}
	}
	if jsonMode {
		return doctorJSON()
	}
	return doctorText()
}

func doctorText() error {
	fmt.Println("hera-agent doctor")
	fmt.Println("=================")
	fmt.Printf("Version:   %s\n", Version)
	fmt.Printf("OS/Arch:   %s/%s\n", runtime.GOOS, runtime.GOARCH)
	fmt.Println()

	fmt.Println("[Binary]")
	exe, exeErr := os.Executable()
	if exeErr != nil {
		fmt.Printf("  X cannot determine running binary: %v\n", exeErr)
	} else {
		fmt.Printf("  running:  %s\n", exe)
	}

	resolved, lookErr := exec.LookPath("hera-agent")
	if lookErr != nil {
		fmt.Printf("  X 'hera-agent' not on PATH (%v)\n", lookErr)
		fmt.Println("    Reopen the terminal after install, or add the install dir to PATH.")
	} else {
		fmt.Printf("  on PATH:  %s\n", resolved)
		if exeErr == nil && !sameFile(resolveSymlink(exe), resolveSymlink(resolved)) {
			fmt.Println("  ! running binary differs from PATH lookup — possible duplicate install.")
		} else if exeErr == nil {
			fmt.Println("  OK binary matches PATH lookup")
		}
	}

	if dupes := findDuplicates("hera-agent"); len(dupes) > 1 {
		fmt.Println("  ! multiple hera-agent binaries on PATH:")
		for _, d := range dupes {
			fmt.Printf("      %s\n", d)
		}
		fmt.Println("    Remove older copies to avoid version drift.")
	}

	_, installBin := getInstallPaths()
	fmt.Printf("  canonical: %s\n", installBin)
	if _, err := os.Stat(installBin); err != nil {
		fmt.Printf("  ! canonical binary missing — reinstall via %s\n", canonicalInstaller())
	}
	fmt.Println()

	fmt.Println("[Shell]")
	printShellHints()
	fmt.Println()

	fmt.Println("[Unity]")
	instances, err := client.ScanInstances()
	if err != nil {
		fmt.Printf("  ! cannot scan instances dir: %v\n", err)
	} else if len(instances) == 0 {
		fmt.Println("  ! no Unity instances detected (~/.hera-agent/instances is empty)")
		fmt.Println("    Open Unity with the Connector package installed:")
		fmt.Println("      https://github.com/NotNull92/hera-agent.git?path=AgentConnector")
	} else {
		fmt.Printf("  OK %d Unity instance(s) registered\n", len(instances))
		for _, inst := range instances {
			age := time.Since(time.UnixMilli(inst.Timestamp)).Truncate(time.Second)
			stale := ""
			if age > 3*time.Second {
				stale = " (stale)"
			}
			fmt.Printf("    port=%d  pid=%d  state=%s  age=%s%s\n",
				inst.Port, inst.PID, inst.State, age, stale)
			fmt.Printf("    project: %s\n", inst.ProjectPath)
		}
	}

	fmt.Println()
	fmt.Println("See docs/troubleshooting.md for known issues.")
	return nil
}

// doctorJSON emits the same diagnostic data as the text view but as a
// structured envelope agents can parse and act on (e.g. self-repair when
// PATH is wrong, or instruct the user to reinstall when canonical binary
// is missing).
func doctorJSON() error {
	report := map[string]interface{}{
		"version":   Version,
		"os":        runtime.GOOS,
		"arch":      runtime.GOARCH,
		"binary":    collectBinaryInfo(),
		"shell":     map[string]string{"os": runtime.GOOS},
		"unity":     collectUnityInstances(),
		"timestamp": time.Now().UTC().Format(time.RFC3339),
	}
	out, err := json.MarshalIndent(report, "", "  ")
	if err != nil {
		return err
	}
	fmt.Println(string(out))
	return nil
}

func collectBinaryInfo() map[string]interface{} {
	info := map[string]interface{}{}
	exe, exeErr := os.Executable()
	if exeErr != nil {
		info["running_error"] = exeErr.Error()
	} else {
		info["running"] = exe
	}
	resolved, lookErr := exec.LookPath("hera-agent")
	if lookErr != nil {
		info["path_lookup_error"] = lookErr.Error()
	} else {
		info["on_path"] = resolved
		if exeErr == nil {
			info["path_matches_running"] = sameFile(resolveSymlink(exe), resolveSymlink(resolved))
		}
	}
	if dupes := findDuplicates("hera-agent"); len(dupes) > 1 {
		info["duplicates"] = dupes
	}
	_, installBin := getInstallPaths()
	info["canonical"] = installBin
	if _, err := os.Stat(installBin); err != nil {
		info["canonical_missing"] = true
	}
	return info
}

func collectUnityInstances() map[string]interface{} {
	out := map[string]interface{}{}
	instances, err := client.ScanInstances()
	if err != nil {
		out["scan_error"] = err.Error()
		return out
	}
	out["count"] = len(instances)
	list := make([]map[string]interface{}, 0, len(instances))
	for _, inst := range instances {
		age := time.Since(time.UnixMilli(inst.Timestamp))
		list = append(list, map[string]interface{}{
			"port":         inst.Port,
			"pid":          inst.PID,
			"state":        inst.State,
			"project":      inst.ProjectPath,
			"unityVersion": inst.UnityVersion,
			"age_ms":       age.Milliseconds(),
			"stale":        age > 3*time.Second,
		})
	}
	out["instances"] = list
	return out
}

// findDuplicates walks PATH and returns every directory that contains a
// matching executable. Used to surface stale parallel installs that cause
// "I updated but the old version is still running" confusion.
func findDuplicates(name string) []string {
	pathEnv := os.Getenv("PATH")
	sep := string(os.PathListSeparator)
	exts := []string{""}
	if runtime.GOOS == "windows" {
		exts = []string{".exe", ".cmd", ".bat", ""}
	}
	seen := map[string]bool{}
	var out []string
	for _, dir := range strings.Split(pathEnv, sep) {
		if dir == "" {
			continue
		}
		for _, ext := range exts {
			candidate := filepath.Join(dir, name+ext)
			info, err := os.Stat(candidate)
			if err != nil || info.IsDir() {
				continue
			}
			key := resolveSymlink(candidate)
			if runtime.GOOS == "windows" {
				key = strings.ToLower(key)
			}
			if !seen[key] {
				seen[key] = true
				out = append(out, candidate)
			}
			break
		}
	}
	return out
}

func canonicalInstaller() string {
	if runtime.GOOS == "windows" {
		return "install.ps1"
	}
	return "install.sh"
}

func printShellHints() {
	switch runtime.GOOS {
	case "windows":
		fmt.Println("  PowerShell: 'where' is aliased to 'Where-Object'. To locate the binary use")
		fmt.Println("              'where.exe hera-agent' or 'Get-Command hera-agent'.")
		fmt.Println("  PATH refresh in current session:")
		fmt.Println("    $env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' +")
		fmt.Println("                [Environment]::GetEnvironmentVariable('Path','Machine')")
	default:
		fmt.Println("  Verify resolution: 'command -v hera-agent' or 'which hera-agent'.")
		fmt.Println("  If PATH was just modified, restart your shell or source the rc file.")
	}
}
