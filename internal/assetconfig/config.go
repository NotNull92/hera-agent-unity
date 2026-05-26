package assetconfig

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sync"
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
}

// AssetConfig holds the full configuration.
type AssetConfig struct {
	Version string       `json:"version"`
	Assets  []AssetEntry `json:"assets"`
}

var (
	configPath string
	configOnce sync.Once
)

// ConfigDir returns the directory where asset-config.json is stored.
func ConfigDir() string {
	configOnce.Do(func() {
		home, err := os.UserHomeDir()
		if err != nil {
			home = "."
		}
		configPath = filepath.Join(home, ".hera-agent-unity-unity", "asset-config.json")
	})
	return filepath.Dir(configPath)
}

// ConfigFilePath returns the full path to asset-config.json.
func ConfigFilePath() string {
	_ = ConfigDir() // ensure initialized
	return configPath
}

// SetConfigFilePath overrides the config file path (for testing).
func SetConfigFilePath(path string) {
	configPath = path
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
				Version: "1.0.0",
				Assets:  DefaultAssets(),
			}
			_ = Save(cfg)
			return cfg, nil
		}
		return nil, err
	}

	var cfg AssetConfig
	if err := json.Unmarshal(data, &cfg); err != nil {
		return nil, err
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
	dir := ConfigDir()
	if err := os.MkdirAll(dir, 0755); err != nil {
		return err
	}

	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return err
	}

	return os.WriteFile(ConfigFilePath(), data, 0644)
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

// SetAssetInstalled sets the installed state of an asset by ID.
func SetAssetInstalled(id string, installed bool) (*AssetConfig, error) {
	cfg, err := Load()
	if err != nil {
		return nil, err
	}

	for i := range cfg.Assets {
		if cfg.Assets[i].ID == id {
			cfg.Assets[i].Installed = installed
			if err := Save(cfg); err != nil {
				return nil, err
			}
			return cfg, nil
		}
	}

	return nil, fmt.Errorf("asset %q not found in config", id)
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
