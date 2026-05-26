package cmd

import (
	"encoding/json"
	"fmt"
	"os"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
	"github.com/charmbracelet/bubbletea"
)

func assetConfigCmd(args []string) error {
	// Load enabled assets into env for AI agent consumption
	loadEnabledAssetsEnv()

	// Check for --json flag early
	for _, arg := range args {
		if arg == "--json" {
			data, err := jsonOutputForAI()
			if err != nil {
				return fmt.Errorf("error: %v", err)
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
	case "detect":
		return assetConfigDetect()
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

	categoryNames := map[string]string{
		"inspector":     "Inspector",
		"validation":    "Validation",
		"serialization": "Serialization",
		"animation":     "Animation",
	}

	categorized := make(map[string][]assetconfig.AssetEntry)
	for _, a := range cfg.Assets {
		categorized[a.Category] = append(categorized[a.Category], a)
	}
	catOrder := []string{"inspector", "validation", "serialization", "animation"}

	if tui.ColorEnabled() {
		fmt.Println(tui.TitleStyle.Render(fmt.Sprintf("Asset Config v%s", cfg.Version)))
		fmt.Println(tui.PathStyle.Render(assetconfig.ConfigFilePath()))
		fmt.Println()
		for _, cat := range catOrder {
			items, ok := categorized[cat]
			if !ok {
				continue
			}
			fmt.Println("  " + tui.HelpSectionStyle.Render(categoryNames[cat]))
			for _, a := range items {
				badge := tui.StatusBadge("disabled")
				if a.Enabled {
					badge = tui.StatusBadge("enabled")
				}
				installed := tui.MutedStyle.Render("·")
				if a.Installed {
					installed = tui.CheckStyle.Render("✓")
				}
				fmt.Printf("    %s %s  %s %s\n",
					badge,
					tui.PathStyle.Render(a.ID),
					installed,
					a.Name)
			}
			fmt.Println()
		}
		return nil
	}

	// Plain output — kept stable for script/AI parsing.
	fmt.Printf("Asset Config v%s — %s\n\n", cfg.Version, assetconfig.ConfigFilePath())
	for _, cat := range catOrder {
		items, ok := categorized[cat]
		if !ok {
			continue
		}
		fmt.Printf("  %s\n", categoryNames[cat])
		for _, a := range items {
			status := "OFF"
			if a.Enabled {
				status = "ON "
			}
			installed := "  "
			if a.Installed {
				installed = "✓"
			}
			fmt.Printf("    [%s] %s  %s %s\n", status, a.ID, installed, a.Name)
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

func assetConfigDetect() error {
	// This command shows instructions for asset detection.
	// The actual detection requires Unity running with the Connector package.
	if tui.ColorEnabled() {
		fmt.Println(tui.InfoStyle.Render("Asset detection requires Unity to be running."))
		fmt.Println(tui.InfoStyle.Render("With Unity open, run:"))
		fmt.Println("  " + tui.PathStyle.Render("hera-agent-unity asset-config detect"))
		fmt.Println()
		fmt.Printf("%s %s\n",
			tui.LabelStyle.Render("Config path:"),
			tui.PathStyle.Render(assetconfig.ConfigFilePath()))
		return nil
	}

	fmt.Println("Asset detection requires Unity to be running.")
	fmt.Println("With Unity open, run:")
	fmt.Println("  hera-agent-unity asset-config detect")
	fmt.Println()
	fmt.Printf("Config path: %s\n", assetconfig.ConfigFilePath())
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
	enabled, err := assetconfig.GetEnabledAssets()
	if err != nil {
		return nil, err
	}

	type aiAsset struct {
		ID       string `json:"id"`
		Name     string `json:"name"`
		Category string `json:"category"`
	}

	var assets []aiAsset
	for _, a := range enabled {
		assets = append(assets, aiAsset{
			ID:       a.ID,
			Name:     a.Name,
			Category: a.Category,
		})
	}

	return json.MarshalIndent(map[string]interface{}{
		"enabled_assets": assets,
		"total":          len(assets),
	}, "", "  ")
}

// loadEnabledAssetsEnv loads enabled asset IDs into UNITY_AGENT_ENABLED_ASSETS env var.
// Only called when needed, not via init().
func loadEnabledAssetsEnv() {
	cfg, err := assetconfig.Load()
	if err != nil {
		return
	}
	var enabled []string
	for _, a := range cfg.Assets {
		if a.Enabled {
			enabled = append(enabled, a.ID)
		}
	}
	if len(enabled) > 0 {
		_ = os.Setenv("UNITY_AGENT_ENABLED_ASSETS", strings.Join(enabled, ","))
	}
}
