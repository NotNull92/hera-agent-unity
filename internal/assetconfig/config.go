package assetconfig

import (
	"encoding/json"
	"fmt"
	"os"
	"strings"
	"sync"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

// AssetEntry represents a single asset plugin entry.
type AssetEntry struct {
	ID            string `json:"id"`
	Name          string `json:"name"`
	Enabled       bool   `json:"enabled"`
	Installed     bool   `json:"installed"`
	Category      string `json:"category"`
	Description   string `json:"description"`
	DocURL        string `json:"doc_url,omitempty"`
	ReferencePath string `json:"reference_path,omitempty"`

	extra map[string]json.RawMessage
}

// LoopEngineeringMode controls the Ultra Hera agent verification guidance.
type LoopEngineeringMode string

const (
	LoopEngineeringOff   LoopEngineeringMode = "off"
	LoopEngineeringLight LoopEngineeringMode = "light"
	LoopEngineeringUltra LoopEngineeringMode = "ultra"
)

// UISystem selects the UI authoring backend used by ui_doc and manage_ui.
// UGUI remains the default so existing UI workflows keep their behavior.
type UISystem string

const (
	UISystemUGUI UISystem = "ugui"
	UISystemUITK UISystem = "uitk"
)

// AssetConfig holds the full configuration.
type AssetConfig struct {
	Version             string              `json:"version"`
	Assets              []AssetEntry        `json:"assets"`
	LoopEngineeringMode LoopEngineeringMode `json:"loopEngineeringMode"`
	UISystem            UISystem            `json:"ui_system"`

	// GameFeelUIMode mirrors game_feel_ui_mode in the shared asset-config.json.
	// When on, the connector's manage_ui attaches Game Feel & Juice Bible + UI
	// Feedback Design Guide juice guidance to its create responses. The Hera
	// Settings window is the primary editor. (Persisted under the old
	// `ui_juicy_mode` key before the Game Feel UI Mode rename; Load migrates
	// that key transparently.)
	GameFeelUIMode bool `json:"game_feel_ui_mode"`

	// GameFeelMode mirrors game_feel_mode — the gameplay-wide Game Feel Mode
	// (Beta). When on, `doctor --agent-rules` and connector tool responses point
	// agents at the bundled game_feel knowledge base (Game Feel & Juice Bible +
	// Ethical Engagement Framework).
	GameFeelMode bool `json:"game_feel_mode"`

	// UiSlopMode mirrors ui_slop_mode — the Unity De-slop Mode (Beta). When on,
	// `doctor --agent-rules` injects the unity-deslop discipline and connector
	// tool responses point agents at the bundled ui_slop taxonomy (static visual
	// slop: layout, spacing, typography, color) via agent_hint.
	UiSlopMode bool `json:"ui_slop_mode"`

	// DefaultCscPath/DefaultDotnetPath are written by the Hera Settings window.
	// The CLI doesn't edit them, but it must round-trip them so a CLI-side Save
	// (e.g. toggling an asset or Game Feel UI Mode) doesn't drop a user's compiler paths.
	DefaultCscPath    string `json:"defaultCscPath,omitempty"`
	DefaultDotnetPath string `json:"defaultDotnetPath,omitempty"`

	extra map[string]json.RawMessage
}

var (
	configPath string
	configOnce sync.Once
)

// ConfigFilePath returns the full path to asset-config.json under the
// user's home directory. Initialised once on first call.
func ConfigFilePath() string {
	configOnce.Do(func() {
		configPath = paths.AssetConfigPath()
	})
	return configPath
}

// NormalizeLoopEngineeringMode accepts persisted/user-provided mode text and
// falls back to Light, the product default.
func NormalizeLoopEngineeringMode(mode string) LoopEngineeringMode {
	switch LoopEngineeringMode(strings.ToLower(strings.TrimSpace(mode))) {
	case LoopEngineeringOff:
		return LoopEngineeringOff
	case LoopEngineeringUltra:
		return LoopEngineeringUltra
	case LoopEngineeringLight:
		return LoopEngineeringLight
	default:
		return LoopEngineeringLight
	}
}

// ParseUISystem validates a user-provided UI backend value.
func ParseUISystem(value string) (UISystem, error) {
	switch UISystem(strings.ToLower(strings.TrimSpace(value))) {
	case UISystemUGUI:
		return UISystemUGUI, nil
	case UISystemUITK:
		return UISystemUITK, nil
	default:
		return "", fmt.Errorf("unknown UI system %q (use ugui or uitk)", value)
	}
}

// NormalizeUISystem accepts persisted UI-system text and falls back to uGUI,
// preserving all existing UI behavior when the key is missing or malformed.
func NormalizeUISystem(value string) UISystem {
	system, err := ParseUISystem(value)
	if err != nil {
		return UISystemUGUI
	}
	return system
}

// DefaultAssets returns the built-in list of known asset plugins.
func DefaultAssets() []AssetEntry {
	return []AssetEntry{
		{
			ID:            "odin_inspector",
			Name:          "Odin Inspector",
			Enabled:       false,
			Installed:     false,
			Category:      "inspector",
			Description:   "Odin Inspector — powerful inspector extension. Prefer the Odin API when building custom editors.",
			DocURL:        "https://odininspector.com/documentation",
			ReferencePath: "references/odin-inspector.md",
		},
		{
			ID:            "odin_validator",
			Name:          "Odin Validator",
			Enabled:       false,
			Installed:     false,
			Category:      "validation",
			Description:   "Odin Validator — data validation system. Use Odin Validator for data integrity checks.",
			DocURL:        "https://odininspector.com/tutorials/odin-validator/getting-started-with-odin-validator",
			ReferencePath: "references/odin-validator.md",
		},
		{
			ID:            "odin_serializer",
			Name:          "Odin Serializer",
			Enabled:       false,
			Installed:     false,
			Category:      "serialization",
			Description:   "Odin Serializer — high-performance serialization. Prefer Odin Serializer over Unity's default serializer.",
			DocURL:        "https://odininspector.com/tutorials/serialize-anything/odin-serializer-quick-start",
			ReferencePath: "references/odin-serializer.md",
		},
		{
			ID:            "dotween",
			Name:          "DOTween",
			Enabled:       false,
			Installed:     false,
			Category:      "animation",
			Description:   "DOTween — tweening/animation engine. Use the DOTween API as the default for Unity animation work.",
			DocURL:        "https://dotween.demigiant.com/documentation.php",
			ReferencePath: "references/dotween.md",
		},
		{
			ID:            "dotween_pro",
			Name:          "DOTween Pro",
			Enabled:       false,
			Installed:     false,
			Category:      "animation",
			Description:   "DOTween Pro — DOTween extensions (Visual Animation, Physics2D, Audio).",
			DocURL:        "https://dotween.demigiant.com/pro.php",
			ReferencePath: "references/dotween-pro.md",
		},
	}
}

// Load reads the asset config from disk. Returns defaults if file doesn't exist.
func Load() (*AssetConfig, error) {
	path := ConfigFilePath()

	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			// First run — create defaults and save
			cfg := &AssetConfig{
				Version:             "1.0.0",
				Assets:              DefaultAssets(),
				LoopEngineeringMode: LoopEngineeringLight,
				UISystem:            UISystemUGUI,
			}
			if err := Save(cfg); err != nil {
				return nil, err
			}
			return cfg, nil
		}
		return nil, err
	}

	var cfg AssetConfig
	if err := json.Unmarshal(data, &cfg); err != nil {
		return nil, err
	}
	cfg.LoopEngineeringMode = NormalizeLoopEngineeringMode(string(cfg.LoopEngineeringMode))
	cfg.UISystem = NormalizeUISystem(string(cfg.UISystem))

	// Migrate the pre-rename `ui_juicy_mode` key onto game_feel_ui_mode. Detect
	// key presence with pointers so an explicit `false` isn't confused with absent.
	// The legacy key is dropped on the next Save (the struct only writes the new key).
	var compat struct {
		GameFeel *bool `json:"game_feel_ui_mode"`
		Legacy   *bool `json:"ui_juicy_mode"`
	}
	if json.Unmarshal(data, &compat) == nil && compat.GameFeel == nil && compat.Legacy != nil {
		cfg.GameFeelUIMode = *compat.Legacy
	}

	// Merge with defaults. User state (Enabled, Installed) is preserved per ID.
	// Immutable metadata (Name, Description, Category, DocURL, ReferencePath)
	// is refreshed from defaults so existing configs pick up upstream changes
	// (e.g. translated copy) without needing the user to delete the file.
	defaults := DefaultAssets()
	byID := make(map[string]AssetEntry, len(cfg.Assets))
	for _, a := range cfg.Assets {
		byID[a.ID] = a
	}
	merged := make([]AssetEntry, 0, len(defaults))
	for _, def := range defaults {
		if prev, ok := byID[def.ID]; ok {
			def.Enabled = prev.Enabled
			def.Installed = prev.Installed
			def.extra = cloneRawMessages(prev.extra)
		}
		merged = append(merged, def)
	}
	// Preserve any user-added assets that aren't in defaults.
	seen := make(map[string]bool, len(defaults))
	for _, def := range defaults {
		seen[def.ID] = true
	}
	for _, a := range cfg.Assets {
		if !seen[a.ID] {
			merged = append(merged, a)
		}
	}
	cfg.Assets = merged

	return &cfg, nil
}

// Save writes the asset config to disk.
func Save(cfg *AssetConfig) error {
	cfg.LoopEngineeringMode = NormalizeLoopEngineeringMode(string(cfg.LoopEngineeringMode))
	cfg.UISystem = NormalizeUISystem(string(cfg.UISystem))
	path := ConfigFilePath()
	release, err := acquireConfigLock(path)
	if err != nil {
		return err
	}
	defer release()
	if err := preserveCurrentExtensions(path, cfg); err != nil {
		return err
	}

	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return err
	}

	return writeConfigAtomically(path, data)
}

// LoadLoopEngineeringModeNoCreate reads only Ultra Hera mode for agent-rules
// generation. Missing or unreadable config returns the default without writing.
func LoadLoopEngineeringModeNoCreate() LoopEngineeringMode {
	data, err := os.ReadFile(ConfigFilePath())
	if err != nil {
		return LoopEngineeringLight
	}
	var cfg struct {
		LoopEngineeringMode LoopEngineeringMode `json:"loopEngineeringMode"`
	}
	if err := json.Unmarshal(data, &cfg); err != nil {
		return LoopEngineeringLight
	}
	return NormalizeLoopEngineeringMode(string(cfg.LoopEngineeringMode))
}

// ToggleAsset flips the enabled state of an asset by ID.
func ToggleAsset(id string) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}

	for i := range cfg.Assets {
		if cfg.Assets[i].ID == id {
			cfg.Assets[i].Enabled = !cfg.Assets[i].Enabled
			if err := Save(cfg); err != nil {
				return nil, err
			}
			return cfg, nil
		}
	}

	return nil, fmt.Errorf("asset %q not found in config", id)
}

// SetAssetEnabled sets the enabled state of an asset by ID.
func SetAssetEnabled(id string, enabled bool) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}

	for i := range cfg.Assets {
		if cfg.Assets[i].ID == id {
			cfg.Assets[i].Enabled = enabled
			if err := Save(cfg); err != nil {
				return nil, err
			}
			return cfg, nil
		}
	}

	return nil, fmt.Errorf("asset %q not found in config", id)
}

// SetGameFeelUIMode sets the Game Feel UI Mode (Beta) flag and persists it.
func SetGameFeelUIMode(enabled bool) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}
	cfg.GameFeelUIMode = enabled
	if err := Save(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// SetUISystem sets the UI authoring backend and persists it.
func SetUISystem(system UISystem) (*AssetConfig, error) {
	validated, err := ParseUISystem(string(system))
	if err != nil {
		return nil, err
	}

	cfg, err := Load()
	if err != nil {
		return nil, err
	}
	cfg.UISystem = validated
	if err := Save(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// SetGameFeelMode sets the Game Feel Mode (Beta) flag and persists it.
func SetGameFeelMode(enabled bool) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}
	cfg.GameFeelMode = enabled
	if err := Save(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// LoadGameFeelModeNoCreate reads only the Game Feel Mode (Beta) flag for
// agent-rules generation. Missing or unreadable config reads as off without
// writing a config file.
func LoadGameFeelModeNoCreate() bool {
	data, err := os.ReadFile(ConfigFilePath())
	if err != nil {
		return false
	}
	var cfg struct {
		GameFeelMode bool `json:"game_feel_mode"`
	}
	if err := json.Unmarshal(data, &cfg); err != nil {
		return false
	}
	return cfg.GameFeelMode
}

// SetUiSlopMode sets the Unity De-slop Mode (Beta) flag and persists it.
func SetUiSlopMode(enabled bool) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}
	cfg.UiSlopMode = enabled
	if err := Save(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// LoadUiSlopModeNoCreate reads only the Unity De-slop Mode (Beta) flag for
// agent-rules generation. Missing or unreadable config reads as off without
// writing a config file.
func LoadUiSlopModeNoCreate() bool {
	data, err := os.ReadFile(ConfigFilePath())
	if err != nil {
		return false
	}
	var cfg struct {
		UiSlopMode bool `json:"ui_slop_mode"`
	}
	if err := json.Unmarshal(data, &cfg); err != nil {
		return false
	}
	return cfg.UiSlopMode
}

// SetLoopEngineeringMode sets the Ultra Hera verification mode and persists it.
func SetLoopEngineeringMode(mode LoopEngineeringMode) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}
	cfg.LoopEngineeringMode = NormalizeLoopEngineeringMode(string(mode))
	if err := Save(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

// GetEnabledAssets returns all enabled asset entries.
func GetEnabledAssets() ([]AssetEntry, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}

	var enabled []AssetEntry
	for _, a := range cfg.Assets {
		if a.Enabled {
			enabled = append(enabled, a)
		}
	}
	return enabled, nil
}

// IsEnabled checks if a specific asset is enabled.
func IsEnabled(id string) (bool, error) {
	cfg, err := Load()
	if err != nil {
		return false, err
	}

	for _, a := range cfg.Assets {
		if a.ID == id {
			return a.Enabled, nil
		}
	}
	return false, nil
}
