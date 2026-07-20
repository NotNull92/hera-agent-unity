package cmd

import (
	"encoding/json"
	"fmt"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
	"github.com/charmbracelet/bubbletea"
)

func assetConfigCmd(args []string) error {
	// Check for --json flag early
	for _, arg := range args {
		if arg == "--json" {
			data, err := jsonOutputForAI()
			if err != nil {
				return fmt.Errorf("error: %w", err)
			}
			fmt.Println(string(data))
			return nil
		}
	}

	if len(args) == 0 {
		// No subcommand — launch interactive TUI
		return runAssetConfigTUI()
	}

	sub := args[0]
	subArgs := args[1:]

	switch sub {
	case "list", "ls":
		return assetConfigList()
	case "enable":
		if len(subArgs) == 0 {
			return fmt.Errorf("usage: asset-config enable <id>")
		}
		return assetConfigToggle(subArgs[0], true)
	case "disable":
		if len(subArgs) == 0 {
			return fmt.Errorf("usage: asset-config disable <id>")
		}
		return assetConfigToggle(subArgs[0], false)
	case "toggle":
		if len(subArgs) == 0 {
			return fmt.Errorf("usage: asset-config toggle <id>")
		}
		return assetConfigToggleAction(subArgs[0])
	case "gamefeel":
		return assetConfigGameFeel(subArgs)
	case "gamefeel-ui", "juicy": // "juicy" kept as a backward-compat alias (UI mode)
		return assetConfigGameFeelUI(subArgs)
	case "uislop":
		return assetConfigUISlop(subArgs)
	case "ui-system":
		return assetConfigUISystem(subArgs)
	case "get":
		if len(subArgs) == 0 {
			return fmt.Errorf("usage: asset-config get <id>")
		}
		return assetConfigGet(subArgs[0])
	case "path":
		fmt.Println(assetconfig.ConfigFilePath())
		return nil
	case "--help", "-h":
		printAssetConfigHelp()
		return nil
	default:
		return fmt.Errorf("unknown subcommand: %s\n\nUse \"asset-config --help\" for available commands", sub)
	}
}

func runAssetConfigTUI() error {
	p := tea.NewProgram(tui.NewAssetConfigModel(), tea.WithAltScreen())
	_, err := p.Run()
	return err
}

func assetConfigList() error {
	cfg, err := assetconfig.Load()
	if err != nil {
		return err
	}

	categorized := make(map[string][]assetconfig.AssetEntry)
	for _, a := range cfg.Assets {
		categorized[a.Category] = append(categorized[a.Category], a)
	}

	gameFeel := "off"
	if cfg.GameFeelMode {
		gameFeel = "on"
	}
	gameFeelUI := "off"
	if cfg.GameFeelUIMode {
		gameFeelUI = "on"
	}
	uiSlop := "off"
	if cfg.UISlopMode {
		uiSlop = "on"
	}
	loopMode := string(cfg.LoopEngineeringMode)
	uiSystem := string(cfg.UISystem)

	type listRow struct {
		Enabled   bool
		Installed bool
		ID        string
		Name      string
	}
	type listSection struct {
		Title string
		Rows  []listRow
	}
	var sections []listSection
	for _, cat := range assetconfig.CategoryOrder {
		items, ok := categorized[cat]
		if !ok {
			continue
		}
		var rows []listRow
		for _, a := range items {
			rows = append(rows, listRow{Enabled: a.Enabled, Installed: a.Installed, ID: a.ID, Name: a.Name})
		}
		sections = append(sections, listSection{Title: assetconfig.CategoryNames[cat], Rows: rows})
	}

	if tui.ColorEnabled() {
		fmt.Println(tui.TitleStyle.Render(fmt.Sprintf("Asset Config v%s", cfg.Version)))
		fmt.Println(tui.PathStyle.Render(assetconfig.ConfigFilePath()))
		fmt.Printf("%s %s\n", tui.LabelStyle.Render("Ultra Hera:"), tui.StatusBadge(loopMode))
		fmt.Printf("%s %s\n", tui.LabelStyle.Render("UI System:"), tui.StatusBadge(uiSystem))
		fmt.Printf("%s %s\n", tui.LabelStyle.Render("Game Feel Mode (Beta):"), tui.StatusBadge(map[bool]string{true: "enabled", false: "disabled"}[cfg.GameFeelMode]))
		fmt.Printf("%s %s\n", tui.LabelStyle.Render("Game Feel UI Mode (Beta):"), tui.StatusBadge(map[bool]string{true: "enabled", false: "disabled"}[cfg.GameFeelUIMode]))
		fmt.Printf("%s %s\n", tui.LabelStyle.Render("Unity De-slop Mode (Beta):"), tui.StatusBadge(map[bool]string{true: "enabled", false: "disabled"}[cfg.UISlopMode]))
		fmt.Println()
		for _, sec := range sections {
			fmt.Println("  " + tui.HelpSectionStyle.Render(sec.Title))
			for _, r := range sec.Rows {
				badge := tui.StatusBadge("disabled")
				if r.Enabled {
					badge = tui.StatusBadge("enabled")
				}
				installed := tui.MutedStyle.Render("·")
				if r.Installed {
					installed = tui.CheckStyle.Render("✓")
				}
				fmt.Printf("    %s %s  %s %s\n",
					badge,
					tui.PathStyle.Render(r.ID),
					installed,
					r.Name)
			}
			fmt.Println()
		}
		return nil
	}

	// Plain output — kept stable for script/AI parsing.
	fmt.Printf("Asset Config v%s — %s\n", cfg.Version, assetconfig.ConfigFilePath())
	fmt.Printf("Ultra Hera: %s\n", loopMode)
	fmt.Printf("UI System: %s\n", uiSystem)
	fmt.Printf("Game Feel Mode (Beta): %s\n", gameFeel)
	fmt.Printf("Game Feel UI Mode (Beta): %s\n", gameFeelUI)
	fmt.Printf("Unity De-slop Mode (Beta): %s\n\n", uiSlop)
	for _, sec := range sections {
		fmt.Printf("  %s\n", sec.Title)
		for _, r := range sec.Rows {
			status := "OFF"
			if r.Enabled {
				status = "ON "
			}
			installed := "  "
			if r.Installed {
				installed = "✓"
			}
			fmt.Printf("    [%s] %s  %s %s\n", status, r.ID, installed, r.Name)
		}
		fmt.Println()
	}

	return nil
}

func assetConfigToggle(id string, enabled bool) error {
	cfg, err := assetconfig.SetAssetEnabled(id, enabled)
	if err != nil {
		return err
	}
	if cfg == nil {
		return fmt.Errorf("asset not found: %s", id)
	}

	state := "disabled"
	if enabled {
		state = "enabled"
	}
	printToggleResult(id, state)
	return nil
}

func assetConfigToggleAction(id string) error {
	cfg, err := assetconfig.ToggleAsset(id)
	if err != nil {
		return err
	}
	if cfg == nil {
		return fmt.Errorf("asset not found: %s", id)
	}

	for _, a := range cfg.Assets {
		if a.ID == id {
			state := "disabled"
			if a.Enabled {
				state = "enabled"
			}
			printToggleResult(id, state)
			return nil
		}
	}
	return nil
}

func printToggleResult(id, state string) {
	if tui.ColorEnabled() {
		fmt.Printf("%s %s %s\n",
			tui.CheckStyle.Render("✓"),
			tui.PathStyle.Render(id),
			tui.StatusBadge(state))
		return
	}
	fmt.Printf("✓ %s %s\n", id, state)
}

func assetConfigGameFeel(args []string) error {
	return assetConfigBoolFlag(args, "gamefeel", "game_feel_mode",
		func(cfg *assetconfig.AssetConfig) bool { return cfg.GameFeelMode },
		assetconfig.SetGameFeelMode)
}

func assetConfigGameFeelUI(args []string) error {
	return assetConfigBoolFlag(args, "gamefeel-ui", "game_feel_ui_mode",
		func(cfg *assetconfig.AssetConfig) bool { return cfg.GameFeelUIMode },
		assetconfig.SetGameFeelUIMode)
}

func assetConfigUISlop(args []string) error {
	return assetConfigBoolFlag(args, "uislop", "ui_slop_mode",
		func(cfg *assetconfig.AssetConfig) bool { return cfg.UISlopMode },
		assetconfig.SetUISlopMode)
}

func assetConfigUISystem(args []string) error {
	if len(args) == 0 {
		cfg, err := assetconfig.Load()
		if err != nil {
			return err
		}
		fmt.Printf("ui_system: %s\n", cfg.UISystem)
		return nil
	}
	if len(args) != 1 {
		return fmt.Errorf("usage: asset-config ui-system [ugui|uitk]")
	}

	system, err := assetconfig.ParseUISystem(args[0])
	if err != nil {
		return fmt.Errorf("usage: asset-config ui-system [ugui|uitk]: %w", err)
	}
	if _, err := assetconfig.SetUISystem(system); err != nil {
		return err
	}
	if tui.ColorEnabled() {
		fmt.Printf("%s %s %s\n", tui.CheckStyle.Render("✓"), tui.PathStyle.Render("ui_system"), tui.StatusBadge(string(system)))
		return nil
	}
	fmt.Printf("✓ ui_system %s\n", system)
	return nil
}

// assetConfigBoolFlag shows or sets a boolean asset-config flag — shared by
// the gamefeel / gamefeel-ui subcommands, which differ only in key and setter.
func assetConfigBoolFlag(args []string, subcommand, key string, get func(*assetconfig.AssetConfig) bool, set func(bool) (*assetconfig.AssetConfig, error)) error {
	// No arg → report current state.
	if len(args) == 0 {
		cfg, err := assetconfig.Load()
		if err != nil {
			return err
		}
		state := "off"
		if get(cfg) {
			state = "on"
		}
		fmt.Printf("%s: %s\n", key, state)
		return nil
	}

	var enabled bool
	switch strings.ToLower(args[0]) {
	case "on", "enable", "true":
		enabled = true
	case "off", "disable", "false":
		enabled = false
	default:
		return fmt.Errorf("usage: asset-config %s [on|off]", subcommand)
	}

	if _, err := set(enabled); err != nil {
		return err
	}

	state := "off"
	if enabled {
		state = "on"
	}
	if tui.ColorEnabled() {
		fmt.Printf("%s %s %s\n",
			tui.CheckStyle.Render("✓"),
			tui.PathStyle.Render(key),
			tui.StatusBadge(map[bool]string{true: "enabled", false: "disabled"}[enabled]))
		return nil
	}
	fmt.Printf("✓ %s %s\n", key, state)
	return nil
}

func assetConfigGet(id string) error {
	enabled, err := assetconfig.IsEnabled(id)
	if err != nil {
		return err
	}

	fmt.Printf("%s: %v\n", id, enabled)
	return nil
}

func printAssetConfigHelp() {
	fmt.Print(`Usage: hera-agent-unity asset-config [subcommand]

Interactive TUI:
  asset-config                  Launch interactive checkbox UI (Space to toggle)

Subcommands:
  list, ls                      List all assets and their state
  enable <id>                   Enable an asset
  disable <id>                  Disable an asset
  toggle <id>                   Toggle an asset (flip ON/OFF)
  gamefeel [on|off]             Show or set Game Feel Mode (Beta) (gameplay game-feel guidance)
  gamefeel-ui [on|off]          Show or set Game Feel UI Mode (Beta) (manage_ui juice guidance)
  uislop [on|off]               Show or set Unity De-slop Mode (Beta) (static UI slop cleanup guidance)
  ui-system [ugui|uitk]         Show or set the UI authoring system
  detect                        Auto-detect installed assets (requires Unity)
  get <id>                      Show a single asset's state
  path                          Print the config file path

Available Assets:
  odin_inspector                Odin Inspector
  odin_validator                Odin Validator
  odin_serializer               Odin Serializer
  dotween                       DOTween
  dotween_pro                   DOTween Pro

Examples:
  hera-agent-unity asset-config
  hera-agent-unity asset-config enable dotween
  hera-agent-unity asset-config list
  hera-agent-unity asset-config toggle odin_inspector
  hera-agent-unity asset-config gamefeel on

TUI Controls:
  ↑/k          Move up
  ↓/j          Move down
  Space/Enter  Toggle (ON/OFF)
  q/Esc        Quit (changes auto-saved)
`)
}

// jsonOutputForAI returns the enabled assets as JSON for AI agent consumption.
// This is used when the AI needs to know which assets to prioritize.
func jsonOutputForAI() ([]byte, error) {
	cfg, err := assetconfig.Load()
	if err != nil {
		return nil, err
	}

	type aiAsset struct {
		ID       string `json:"id"`
		Name     string `json:"name"`
		Category string `json:"category"`
	}

	var assets []aiAsset
	dotweenPreferred := false
	for _, a := range cfg.Assets {
		if !a.Enabled {
			continue
		}
		assets = append(assets, aiAsset{
			ID:       a.ID,
			Name:     a.Name,
			Category: a.Category,
		})
		if a.ID == "dotween" || a.ID == "dotween_pro" {
			dotweenPreferred = true
		}
	}

	return json.MarshalIndent(map[string]interface{}{
		"enabled_assets":        assets,
		"total":                 len(assets),
		"loop_engineering_mode": cfg.LoopEngineeringMode,
		"game_feel_mode":        cfg.GameFeelMode,
		"game_feel_ui_mode":     cfg.GameFeelUIMode,
		"ui_slop_mode":          cfg.UISlopMode,
		"ui_system":             cfg.UISystem,
		"dotween_preferred":     dotweenPreferred,
	}, "", "  ")
}
