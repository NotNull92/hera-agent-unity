package cmd

import (
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
)

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
	for len(out) > 0 && strings.TrimSpace(out[len(out)-1]) == "" {
		out = out[:len(out)-1]
	}
	return strings.Join(out, "\n")
}
