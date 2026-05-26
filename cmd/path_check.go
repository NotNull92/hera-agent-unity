package cmd

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
)

// checkBinaryPath emits a one-line stderr warning when the running binary
// disagrees with what 'hera-agent-unity-unity' resolves to on PATH. Catches the common
// "I updated but PATH still points at the old copy" failure mode without
// requiring the user to remember 'hera-agent-unity-unity doctor'.
//
// Silent on agreement. Silent on commands where the warning would be noise
// (help, version, doctor itself). Opt-out via HERA_AGENT_NO_PATH_CHECK=1.
func checkBinaryPath() {
	if os.Getenv("HERA_AGENT_NO_PATH_CHECK") == "1" {
		return
	}
	if len(os.Args) >= 2 {
		switch os.Args[1] {
		case "doctor", "help", "--help", "-h", "version", "--version", "-v":
			return
		}
	}

	exe, err := os.Executable()
	if err != nil {
		return
	}
	resolved, err := exec.LookPath("hera-agent-unity-unity")
	if err != nil {
		// Not on PATH — might be intentional (go run, manual invocation).
		// 'hera-agent-unity-unity doctor' surfaces this explicitly when the user asks.
		return
	}
	if sameFile(resolveSymlink(exe), resolveSymlink(resolved)) {
		return
	}
	fmt.Fprintln(os.Stderr,
		"[hera-agent-unity-unity] warning: running binary differs from 'hera-agent-unity-unity' on PATH.\n"+
			"  running: "+exe+"\n"+
			"  on PATH: "+resolved+"\n"+
			"  Run 'hera-agent-unity-unity doctor' for details. Silence with HERA_AGENT_NO_PATH_CHECK=1.")
}

func resolveSymlink(p string) string {
	abs, err := filepath.Abs(p)
	if err != nil {
		return p
	}
	if real, err := filepath.EvalSymlinks(abs); err == nil {
		return real
	}
	return abs
}

func sameFile(a, b string) bool {
	if runtime.GOOS == "windows" {
		return strings.EqualFold(a, b)
	}
	return a == b
}
