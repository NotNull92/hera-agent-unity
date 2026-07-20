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
	if assetconfig.LoadGameFeelModeNoCreate() {
		out.WriteString(gameFeelAgentRules)
		out.WriteString("\n")
	}
	if assetconfig.LoadUiSlopModeNoCreate() {
		out.WriteString(uiSlopAgentRules)
		out.WriteString("\n")
	}
	out.WriteString(extractMdSection(agentGuide, "## 0. Bootstrap"))
	out.WriteString("\n")
	out.WriteString(extractMdSection(agentGuide, "## 1. Quick Rules"))
	out.WriteString("\n")
	out.WriteString(extractMdSection(agentGuide, "## 4. Pitfalls"))
	out.WriteString("\n")
	return out.String()
}

// gameFeelAgentRules is emitted only while Game Feel Mode (Beta) is ON in Hera
// Settings. Independent of Ultra Hera — it guides *what to build* (game-feel
// parameters with the ethics built in), not how strictly to verify.
const gameFeelAgentRules = "## Game Feel Mode (Beta)\n\n" +
	"Game Feel Mode is ON. When you build or modify anything the player feels — combat feedback, movement, camera, audio, rewards, presentation — do not guess parameters. Use the bundled Game Feel knowledge base (Game Feel & Juice Bible + Ethical Engagement Game Feel Framework).\n\n" +
	"Core principles:\n\n" +
	"1. Maximum output for minimum input — every player action gets multi-channel feedback (visual, audio, haptic, temporal).\n" +
	"2. Juice is an amplifier, not the source of fun — it amplifies real achievement, it does not replace it.\n" +
	"3. Honest Juice first: presentation intensity must match the actual value of the accomplishment. Never glorify a poor result; keep probabilities and pity counters transparent.\n" +
	"4. Golden rule: juice intensity is proportional to the screen's purpose — reward/celebration screens earn exaggeration, precision/input-heavy screens stay calm.\n" +
	"5. Accessibility is non-negotiable: screen-shake/flash/haptic intensity or off options, reduce-motion support, mobile shake at 70-80%.\n\n" +
	"Workflow:\n\n" +
	"1. Start from the topic index: `hera-agent-unity game_feel` (ethics topics listed first — apply them while building, not after).\n" +
	"2. Query concrete parameters before implementing: `hera-agent-unity game_feel screen_shake`, `game_feel hit_stop`, `game_feel tweening_easing`, `game_feel control_feel`; for UI work the `ui` category has per-element specs and theory: `game_feel ui_button`, `game_feel ui_bar`, `game_feel ecn_dmn_framework`, `game_feel ui_choice_symmetry`, ...\n" +
	"3. Before reporting done, validate the result against `game_feel ethics_checklist` and `game_feel checklist_all`.\n\n" +
	"Final quality questions (Ethical Engagement Framework):\n\n" +
	"- Does this design help the player actively enjoy the game because they genuinely want to, in every moment?\n" +
	"- Does the Juice on this screen sensorially amplify the joy of the pure achievement the player has earned?\n"

// uiSlopAgentRules is emitted only while Unity De-slop Mode (Beta) is ON in Hera
// Settings. Independent of Ultra Hera and Game Feel Mode — it guides *static
// visual discipline* (layout, spacing, typography, color), the complement to
// Game Feel Mode's motion/feel.
const uiSlopAgentRules = "## Unity De-slop Mode (Beta)\n\n" +
	"Unity De-slop Mode is ON. When you build or edit uGUI/UI Toolkit screens, do not leave statistical AI-slop (reflexive decoration, undisciplined layout, unscaled spacing, decorative italics, rainbow palettes). Clean it with the bundled `ui_slop` taxonomy (49 tells across 5 areas), every tell measured against the live scene.\n\n" +
	"Core discipline:\n\n" +
	"1. Checklist = evaluation function. A finding is not a status string — it is a predicate you re-measure from the live scene every time (e.g. `manage_components get --type TMP_Text` -> `m_fontStyle & Italic == 0`). Never trust a \"done\" note; recompute.\n" +
	"2. Values are derived, not guessed. No magic px — spacing = base x fixed multiplier; width from a single measure token; palette snaps to the bundled reference. The project's own tokens win; the corpus is the fallback.\n" +
	"3. Inspect in parallel, execute in fixed order A -> B -> C -> D -> E (upstream commits dissolve downstream conflicts). A = decorative sweep, B = layout/RectTransform/containers, C = spacing, D = typography, E = color.\n" +
	"4. Meaning is untouchable: copy, information, and order are never edited (removing decoration is not removing content).\n" +
	"5. Nested surfaces are usually functional in game UI — inventory slots, hotbars, HUD panels — so never flatten repeated interactive cells; each tell's `exception` field spells out what to leave alone. Korean typesetting (font fallback, word-wrap) has its own tells.\n\n" +
	"Workflow:\n\n" +
	"1. Start from the taxonomy index: `hera-agent-unity ui_slop` (areas A-E, ethics-free but decoration-first).\n" +
	"2. Query a concrete tell before fixing: `hera-agent-unity ui_slop box-in-box`, `ui_slop unscaled-spacing-ladder`, `ui_slop low-contrast-text`, `ui_slop tmp-italic`. Each returns the uGUI *and* UI Toolkit check predicate plus the mechanical fix; use the one matching your `ui_system`.\n" +
	"3. Measure the check against the live scene, fix only the tells that fail, then re-measure with a fresh pass.\n"

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
