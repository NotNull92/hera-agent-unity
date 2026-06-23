package cmd

import (
	_ "embed"
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

// agentGuide is the repo-root AGENT.md, embedded into the binary so
// `doctor --agent-rules` can extract the Quick Rules + Pitfalls subset
// without requiring repo access. The canonical file lives at the repo
// root; a copy at cmd/AGENT.md exists because //go:embed cannot escape
// the package directory.
//
//go:embed AGENT.md
var agentGuide string

// doctorCmd prints a self-diagnostic report covering install path, PATH
// resolution, duplicate copies, shell gotchas, and Unity instance state.
// Intended as the first thing users (and AI agents) try when hera-agent-unity
// "isn't working" — it answers "where is the binary, what does PATH see,
// and is Unity reachable?" without requiring a Unity connection.
func doctorCmd(args []string) error {
	jsonMode := false
	agentRulesMode := false
	format := "markdown"
	for i := 0; i < len(args); i++ {
		switch args[i] {
		case "--json":
			jsonMode = true
		case "--agent-rules", "--print-agent-rules":
			agentRulesMode = true
		case "--format":
			if i+1 < len(args) {
				format = args[i+1]
				i++
			}
		}
	}
	if agentRulesMode {
		fmt.Print(extractAgentRules(format))
		return nil
	}
	if jsonMode {
		return doctorJSON()
	}
	return doctorText()
}

// extractAgentRules pulls the "Quick Rules" and "Pitfalls" sections out of
// the embedded AGENT.md, with a header preamble pointing back to the full
// guide. Designed to be appended to a project's AI rules file (CLAUDE.md /
// AGENTS.md / .cursor/rules / .github/copilot-instructions.md / etc.) via
// `hera-agent-unity doctor --agent-rules >> <your-rules-file>`.
//
// format=="cursor" emits a valid `.mdc` rule file with YAML frontmatter so
// Cursor's rule system actually loads and applies it. Without frontmatter
// Cursor parses the file but never activates the rule.
//
// format=="antigravity" (alias "skill") emits SKILL.md-style frontmatter
// (name + description) for AntiGravity's `.agents/skills/` on-demand skills
// (the current default path; `.agent/skills/` is the legacy backward-compat
// location).
// format=="gemini" is AntiGravity's root GEMINI.md, which is plain markdown
// (handled by the default branch, same as Claude Code / Codex / Copilot).
//
// Any other format value (or "markdown") emits plain markdown that Claude
// Code, Codex, Copilot, Continue.dev, and AntiGravity's GEMINI.md all accept
// verbatim.
func extractAgentRules(format string) string {
	var out strings.Builder
	switch format {
	case "cursor":
		out.WriteString("---\n")
		out.WriteString("description: Use hera-agent-unity CLI for any Unity Editor task — measure, do not guess\n")
		out.WriteString("globs: **/*.cs,**/*.unity,**/*.prefab,**/*.asmdef,**/*.mat,**/*.asset,**/Assets/**\n")
		out.WriteString("alwaysApply: true\n")
		out.WriteString("---\n\n")
	case "antigravity", "skill":
		out.WriteString("---\n")
		out.WriteString("name: hera-agent-unity\n")
		out.WriteString("description: Control the running Unity Editor via the hera-agent-unity CLI — execute C#, read the console, drive Play Mode, run tests, inspect live types\n")
		out.WriteString("---\n\n")
	}
	out.WriteString("# hera-agent-unity — Bootstrap + Quick Rules + Pitfalls\n\n")
	out.WriteString("> Emitted by `hera-agent-unity doctor --agent-rules`. ")
	out.WriteString("Works with any AI coding agent (Claude Code, Codex, Cursor, Copilot, ...). ")
	out.WriteString("Full guide: https://github.com/NotNull92/hera-agent-unity/blob/main/AGENT.md\n\n")
	out.WriteString(buildUltraHeraAgentRules(assetconfig.LoadLoopEngineeringModeNoCreate()))
	out.WriteString("\n")
	out.WriteString(extractMdSection(agentGuide, "## 0. Bootstrap"))
	out.WriteString("\n")
	out.WriteString(extractMdSection(agentGuide, "## 1. Quick Rules"))
	out.WriteString("\n")
	out.WriteString(extractMdSection(agentGuide, "## 4. Pitfalls"))
	out.WriteString("\n")
	return out.String()
}

func buildUltraHeraAgentRules(mode assetconfig.LoopEngineeringMode) string {
	const intro = "## Ultra Hera\n\nHera does not do the AI work by itself. This setting only tells AI agents how carefully to check Unity work.\n\n"
	const lightLoop = "### Light Mode\n\nCurrent setting target: `light`.\n\nLight Mode is the default. Use it for every Unity coding, Editor, and Inspector task without heavy token cost.\n\nLight loop:\n\n1. Confirm the goal in one sentence.\n2. Observe only the needed current state in a compact way.\n3. Change code, scene, or Inspector state.\n4. Verify compile or state.\n5. Check console errors.\n6. Re-read only the changed target.\n7. If it failed, fix and repeat up to 1-2 times.\n8. Report short final evidence.\n\nRepresentative commands:\n\n```bash\nhera-agent-unity status\nhera-agent-unity console --type error --lines 20\nhera-agent-unity editor refresh --compile\nhera-agent-unity find_gameobjects --ids\nhera-agent-unity manage_components get ...\nhera-agent-unity exec --depth 1 ...\n```\n\nLight Mode's goal is: do not finish in a wrong state. PlayMode, screenshots, and full tests are not required by default.\n"
	const ultraLoop = "### Ultra Mode\n\nCurrent setting target: `ultra`.\n\nUse Ultra Mode when the user asks for strict verification, for example: `정확히 검증해줘`, `플레이해서 확인해줘`, `UI 맞춰줘`, or `인스펙터까지 확실히 봐줘`.\n\nUltra loop:\n\n1. Split the goal into success criteria.\n2. Take a before-change state snapshot.\n3. Apply the change.\n4. Compile.\n5. Confirm console errors are 0.\n6. Re-read Inspector, GameObject, or asset state.\n7. Run PlayMode or Unity tests.\n8. If needed, capture a screenshot or `ui_doc` capture.\n9. Classify the failure cause and repeat.\n10. Report final evidence and remaining risk.\n\nRepresentative commands:\n\n```bash\nhera-agent-unity editor refresh --compile\nhera-agent-unity console --type error --lines 50\nhera-agent-unity test --mode EditMode\nhera-agent-unity test --mode PlayMode\nhera-agent-unity editor play --wait\nhera-agent-unity screenshot --view game\nhera-agent-unity ui_doc capture --out ...\n```\n\nWhen the saved mode is `ultra`, apply the Light loop to every task, then automatically upgrade to the Ultra loop for strict keywords or important Unity work.\n"

	switch assetconfig.NormalizeLoopEngineeringMode(string(mode)) {
	case assetconfig.LoopEngineeringOff:
		return intro + "Current setting: `off`.\n\nOff: AI does not have to check again after using Hera.\n"
	case assetconfig.LoopEngineeringUltra:
		return intro + "Current setting: `ultra`.\n\n" + lightLoop + "\n" + ultraLoop
	case assetconfig.LoopEngineeringLight:
		fallthrough
	default:
		return intro + "Current setting: `light`.\n\n" + lightLoop
	}
}

// extractMdSection returns the lines of doc starting with the given heading
// and continuing until the next top-level "## " heading or a standalone "---"
// line. The heading itself is included; trailing blank lines are trimmed.
func extractMdSection(doc, heading string) string {
	lines := strings.Split(doc, "\n")
	var out []string
	in := false
	for _, l := range lines {
		if !in {
			if strings.HasPrefix(l, heading) {
				in = true
				out = append(out, l)
			}
			continue
		}
		if strings.TrimSpace(l) == "---" {
			break
		}
		if strings.HasPrefix(l, "## ") && !strings.HasPrefix(l, heading) {
			break
		}
		out = append(out, l)
	}
	// Trim trailing blank lines for a clean append target.
	for len(out) > 0 && strings.TrimSpace(out[len(out)-1]) == "" {
		out = out[:len(out)-1]
	}
	return strings.Join(out, "\n")
}

func doctorText() error {
	fmt.Println("hera-agent-unity doctor")
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

	resolved, lookErr := exec.LookPath("hera-agent-unity")
	if lookErr != nil {
		fmt.Printf("  X 'hera-agent-unity' not on PATH (%v)\n", lookErr)
		fmt.Println("    Reopen the terminal after install, or add the install dir to PATH.")
	} else {
		fmt.Printf("  on PATH:  %s\n", resolved)
		if exeErr == nil && !sameFile(resolveSymlink(exe), resolveSymlink(resolved)) {
			fmt.Println("  ! running binary differs from PATH lookup — possible duplicate install.")
		} else if exeErr == nil {
			fmt.Println("  OK binary matches PATH lookup")
		}
	}

	if dupes := findDuplicates("hera-agent-unity"); len(dupes) > 1 {
		fmt.Println("  ! multiple hera-agent-unity binaries on PATH:")
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
		fmt.Printf("  ! no Unity instances detected (%s is empty)\n", paths.InstancesDir())
		fmt.Println("      https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector")
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
	resolved, lookErr := exec.LookPath("hera-agent-unity")
	if lookErr != nil {
		info["path_lookup_error"] = lookErr.Error()
	} else {
		info["on_path"] = resolved
		if exeErr == nil {
			info["path_matches_running"] = sameFile(resolveSymlink(exe), resolveSymlink(resolved))
		}
	}
	if dupes := findDuplicates("hera-agent-unity"); len(dupes) > 1 {
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
		fmt.Println("              'where.exe hera-agent-unity' or 'Get-Command hera-agent-unity'.")
		fmt.Println("  PATH refresh in current session:")
		fmt.Println("    $env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' +")
		fmt.Println("                [Environment]::GetEnvironmentVariable('Path','Machine')")
	default:
		fmt.Println("  Verify resolution: 'command -v hera-agent-unity' or 'which hera-agent-unity'.")
		fmt.Println("  If PATH was just modified, restart your shell or source the rc file.")
	}
}
