package assetconfig

import (
	"os"
	"path/filepath"
	"strings"
	"sync"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func resetConfigPath(t *testing.T) {
	t.Helper()
	configOnce = sync.Once{}
	configPath = ""
}

func withTempHome(t *testing.T) string {
	t.Helper()
	resetConfigPath(t)
	dir := t.TempDir()
	// On Windows os.UserHomeDir reads USERPROFILE first; on Unix it reads HOME.
	t.Setenv("HOME", dir)
	t.Setenv("USERPROFILE", dir)
	return dir
}

func TestConfigFilePath(t *testing.T) {
	withTempHome(t)

	got := ConfigFilePath()
	if got == "" {
		t.Fatal("ConfigFilePath returned empty string")
	}

	if !filepath.IsAbs(got) {
		t.Errorf("ConfigFilePath not absolute: %s", got)
	}
	if filepath.Base(got) != "asset-config.json" {
		t.Errorf("expected basename asset-config.json, got %s", filepath.Base(got))
	}
	if filepath.Base(filepath.Dir(got)) != ".hera-agent-unity" {
		t.Errorf("expected parent dir .hera-agent-unity, got %s", filepath.Base(filepath.Dir(got)))
	}

}

func TestDefaultAssets(t *testing.T) {
	assets := DefaultAssets()
	if len(assets) == 0 {
		t.Fatal("DefaultAssets returned empty slice")
	}

	ids := make(map[string]bool, len(assets))
	for _, a := range assets {
		if a.ID == "" {
			t.Error("asset missing ID")
		}
		if a.Name == "" {
			t.Errorf("asset %q missing Name", a.ID)
		}
		if a.Category == "" {
			t.Errorf("asset %q missing Category", a.ID)
		}
		if ids[a.ID] {
			t.Errorf("duplicate asset ID %q", a.ID)
		}
		ids[a.ID] = true
	}

	// Verify known entries exist.
	for _, want := range []string{"odin_inspector", "odin_validator", "odin_serializer", "dotween", "dotween_pro"} {
		if !ids[want] {
			t.Errorf("expected default asset %q not found", want)
		}
	}
}

func TestLoadConfig_NoFile_ReturnsDefaultsAndCreatesFile(t *testing.T) {
	withTempHome(t)

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if cfg == nil {
		t.Fatal("Load returned nil config")
	}
	if cfg.Version != "1.0.0" {
		t.Errorf("expected version 1.0.0, got %s", cfg.Version)
	}
	if len(cfg.Assets) != len(DefaultAssets()) {
		t.Errorf("expected %d assets, got %d", len(DefaultAssets()), len(cfg.Assets))
	}
	if cfg.LoopEngineeringMode != LoopEngineeringLight {
		t.Errorf("expected loop mode %q, got %q", LoopEngineeringLight, cfg.LoopEngineeringMode)
	}

	// File should have been created.
	if _, err := os.Stat(ConfigFilePath()); err != nil {
		t.Errorf("expected config file to be created: %v", err)
	}
}

func TestLoadConfig_ValidJSON(t *testing.T) {
	withTempHome(t)
	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	data := `{"version":"2.0.0","loopEngineeringMode":"ultra","assets":[{"id":"dotween","name":"DOTween","enabled":true,"installed":true,"category":"animation","description":"test"}]}`
	if err := os.WriteFile(path, []byte(data), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if cfg.Version != "2.0.0" {
		t.Errorf("expected version 2.0.0, got %s", cfg.Version)
	}
	if cfg.LoopEngineeringMode != LoopEngineeringUltra {
		t.Errorf("expected loop mode %q, got %q", LoopEngineeringUltra, cfg.LoopEngineeringMode)
	}

	// Merged with defaults: dotween should preserve enabled/installed, but metadata refreshed.
	found := false
	for _, a := range cfg.Assets {
		if a.ID == "dotween" {
			found = true
			if !a.Enabled {
				t.Error("expected dotween to remain enabled")
			}
			if !a.Installed {
				t.Error("expected dotween to remain installed")
			}
			if a.Name != "DOTween" {
				t.Errorf("expected DOTween, got %s", a.Name)
			}
		}
	}
	if !found {
		t.Error("dotween not found in merged config")
	}

	// Should also include other defaults.
	if len(cfg.Assets) < len(DefaultAssets()) {
		t.Errorf("expected at least %d assets after merge, got %d", len(DefaultAssets()), len(cfg.Assets))
	}
}

func TestLoadConfig_InvalidLoopEngineeringMode_DefaultsToLight(t *testing.T) {
	withTempHome(t)
	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	data := `{"version":"2.0.0","loopEngineeringMode":"careful","assets":[]}`
	if err := os.WriteFile(path, []byte(data), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if cfg.LoopEngineeringMode != LoopEngineeringLight {
		t.Errorf("expected loop mode %q, got %q", LoopEngineeringLight, cfg.LoopEngineeringMode)
	}
}

func TestLoadConfig_InvalidJSON(t *testing.T) {
	withTempHome(t)
	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	if err := os.WriteFile(path, []byte("not json"), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	_, err := Load()
	if err == nil {
		t.Fatal("expected error for invalid JSON, got nil")
	}
}

func TestSaveConfig_RoundTrip(t *testing.T) {
	withTempHome(t)

	// Load defaults, mutate one asset, save, then load again.
	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	cfg.Version = "1.2.3"
	cfg.LoopEngineeringMode = LoopEngineeringUltra
	for i := range cfg.Assets {
		if cfg.Assets[i].ID == "dotween" {
			cfg.Assets[i].Enabled = true
			cfg.Assets[i].Installed = true
		}
	}
	if err := Save(cfg); err != nil {
		t.Fatalf("Save error: %v", err)
	}

	loaded, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if loaded.Version != "1.2.3" {
		t.Errorf("version mismatch: want %s, got %s", cfg.Version, loaded.Version)
	}
	if loaded.LoopEngineeringMode != LoopEngineeringUltra {
		t.Errorf("expected loop mode %q after round-trip, got %q", LoopEngineeringUltra, loaded.LoopEngineeringMode)
	}

	var found bool
	for _, a := range loaded.Assets {
		if a.ID == "dotween" {
			found = true
			if !a.Enabled {
				t.Error("expected dotween to be enabled after round-trip")
			}
			if !a.Installed {
				t.Error("expected dotween to be installed after round-trip")
			}
		}
	}
	if !found {
		t.Error("dotween not found after round-trip")
	}
}

func TestSetGameFeelMode(t *testing.T) {
	withTempHome(t)

	cfg, err := SetGameFeelMode(true)
	if err != nil {
		t.Fatalf("SetGameFeelMode error: %v", err)
	}
	if !cfg.GameFeelMode {
		t.Error("expected GameFeelMode true after enable")
	}

	// Persisted under the current key and survives a reload; the UI flag is
	// independent and stays off.
	loaded, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if !loaded.GameFeelMode {
		t.Error("expected GameFeelMode true after reload")
	}
	if loaded.GameFeelUIMode {
		t.Error("expected GameFeelUIMode to stay off — flags are independent")
	}

	if _, err := SetGameFeelMode(false); err != nil {
		t.Fatalf("SetGameFeelMode error: %v", err)
	}
	loaded, _ = Load()
	if loaded.GameFeelMode {
		t.Error("expected GameFeelMode false after disable")
	}
}

func TestSetGameFeelUIMode(t *testing.T) {
	withTempHome(t)

	cfg, err := SetGameFeelUIMode(true)
	if err != nil {
		t.Fatalf("SetGameFeelUIMode error: %v", err)
	}
	if !cfg.GameFeelUIMode {
		t.Error("expected GameFeelUIMode true after enable")
	}
	if cfg.GameFeelMode {
		t.Error("expected GameFeelMode to stay off — flags are independent")
	}

	loaded, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if !loaded.GameFeelUIMode {
		t.Error("expected GameFeelUIMode true after reload")
	}
}

func TestLoadGameFeelModeNoCreate(t *testing.T) {
	home := withTempHome(t)

	if LoadGameFeelModeNoCreate() {
		t.Error("expected missing config to read as off")
	}
	if _, err := os.Stat(filepath.Join(home, ".hera-agent-unity", "asset-config.json")); !os.IsNotExist(err) {
		t.Fatalf("expected no config file to be created, stat err=%v", err)
	}

	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	if err := os.WriteFile(path, []byte(`{"game_feel_mode":true}`), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}
	if !LoadGameFeelModeNoCreate() {
		t.Error("expected game_feel_mode true to be read")
	}
}

// A config written before the rename stores the flag under ui_juicy_mode; Load
// must migrate it onto GameFeelUIMode, and Save must drop the legacy key.
func TestLoadConfig_MigratesLegacyJuicyKey(t *testing.T) {
	withTempHome(t)
	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	data := `{"version":"2.0.0","assets":[],"ui_juicy_mode":true}`
	if err := os.WriteFile(path, []byte(data), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if !cfg.GameFeelUIMode {
		t.Fatal("expected legacy ui_juicy_mode=true to migrate to GameFeelUIMode")
	}

	// After a save the legacy key is gone and only game_feel_ui_mode remains.
	if err := Save(cfg); err != nil {
		t.Fatalf("Save error: %v", err)
	}
	raw, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("read file: %v", err)
	}
	if strings.Contains(string(raw), "ui_juicy_mode") {
		t.Errorf("expected legacy ui_juicy_mode key to be dropped after save, got: %s", raw)
	}
	if !strings.Contains(string(raw), "game_feel_ui_mode") {
		t.Errorf("expected game_feel_ui_mode key after save, got: %s", raw)
	}
}

// When the new key is present it wins even if the legacy key disagrees.
func TestLoadConfig_NewKeyWinsOverLegacy(t *testing.T) {
	withTempHome(t)
	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	data := `{"version":"2.0.0","assets":[],"game_feel_ui_mode":false,"ui_juicy_mode":true}`
	if err := os.WriteFile(path, []byte(data), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load error: %v", err)
	}
	if cfg.GameFeelUIMode {
		t.Error("expected explicit game_feel_ui_mode=false to win over legacy ui_juicy_mode=true")
	}
}

func TestSetLoopEngineeringMode(t *testing.T) {
	withTempHome(t)

	cfg, err := SetLoopEngineeringMode(LoopEngineeringUltra)
	if err != nil {
		t.Fatalf("SetLoopEngineeringMode error: %v", err)
	}
	if cfg.LoopEngineeringMode != LoopEngineeringUltra {
		t.Errorf("expected loop mode %q, got %q", LoopEngineeringUltra, cfg.LoopEngineeringMode)
	}

	cfg, err = SetLoopEngineeringMode(LoopEngineeringMode("unknown"))
	if err != nil {
		t.Fatalf("SetLoopEngineeringMode error: %v", err)
	}
	if cfg.LoopEngineeringMode != LoopEngineeringLight {
		t.Errorf("expected invalid mode to normalize to %q, got %q", LoopEngineeringLight, cfg.LoopEngineeringMode)
	}
}

func TestToggleAsset(t *testing.T) {
	withTempHome(t)

	cfg, err := ToggleAsset("dotween")
	if err != nil {
		t.Fatalf("ToggleAsset error: %v", err)
	}
	for _, a := range cfg.Assets {
		if a.ID == "dotween" && !a.Enabled {
			t.Error("expected dotween to be enabled after toggle")
		}
	}

	cfg, err = ToggleAsset("dotween")
	if err != nil {
		t.Fatalf("ToggleAsset error: %v", err)
	}
	for _, a := range cfg.Assets {
		if a.ID == "dotween" && a.Enabled {
			t.Error("expected dotween to be disabled after second toggle")
		}
	}

	_, err = ToggleAsset("nonexistent")
	if err == nil {
		t.Error("expected error for nonexistent asset")
	}
}

func TestSetAssetEnabled(t *testing.T) {
	withTempHome(t)

	cfg, err := SetAssetEnabled("dotween", true)
	if err != nil {
		t.Fatalf("SetAssetEnabled error: %v", err)
	}
	for _, a := range cfg.Assets {
		if a.ID == "dotween" && !a.Enabled {
			t.Error("expected dotween enabled")
		}
	}

	cfg, err = SetAssetEnabled("dotween", false)
	if err != nil {
		t.Fatalf("SetAssetEnabled error: %v", err)
	}
	for _, a := range cfg.Assets {
		if a.ID == "dotween" && a.Enabled {
			t.Error("expected dotween disabled")
		}
	}

	_, err = SetAssetEnabled("nonexistent", true)
	if err == nil {
		t.Error("expected error for nonexistent asset")
	}
}

func TestLoadLoopEngineeringModeNoCreate(t *testing.T) {
	home := withTempHome(t)

	if got := LoadLoopEngineeringModeNoCreate(); got != LoopEngineeringLight {
		t.Errorf("expected missing config to return %q, got %q", LoopEngineeringLight, got)
	}
	if _, err := os.Stat(filepath.Join(home, ".hera-agent-unity", "asset-config.json")); !os.IsNotExist(err) {
		t.Fatalf("expected no config file to be created, stat err=%v", err)
	}

	path := paths.AssetConfigPath()
	_ = os.MkdirAll(filepath.Dir(path), 0755)
	if err := os.WriteFile(path, []byte(`{"loopEngineeringMode":" Ultra "}`), 0644); err != nil {
		t.Fatalf("write file: %v", err)
	}
	if got := LoadLoopEngineeringModeNoCreate(); got != LoopEngineeringUltra {
		t.Errorf("expected loop mode %q, got %q", LoopEngineeringUltra, got)
	}
}

func TestGetEnabledAssets(t *testing.T) {
	withTempHome(t)

	// Initially none enabled.
	enabled, err := GetEnabledAssets()
	if err != nil {
		t.Fatalf("GetEnabledAssets error: %v", err)
	}
	if len(enabled) != 0 {
		t.Errorf("expected 0 enabled, got %d", len(enabled))
	}

	_, _ = SetAssetEnabled("dotween", true)
	_, _ = SetAssetEnabled("odin_inspector", true)

	enabled, err = GetEnabledAssets()
	if err != nil {
		t.Fatalf("GetEnabledAssets error: %v", err)
	}
	if len(enabled) != 2 {
		t.Errorf("expected 2 enabled, got %d", len(enabled))
	}
	ids := make(map[string]bool)
	for _, a := range enabled {
		ids[a.ID] = true
	}
	if !ids["dotween"] || !ids["odin_inspector"] {
		t.Errorf("unexpected enabled set: %+v", ids)
	}
}

func TestIsEnabled(t *testing.T) {
	withTempHome(t)

	ok, err := IsEnabled("dotween")
	if err != nil {
		t.Fatalf("IsEnabled error: %v", err)
	}
	if ok {
		t.Error("expected dotween to be disabled by default")
	}

	_, _ = SetAssetEnabled("dotween", true)

	ok, err = IsEnabled("dotween")
	if err != nil {
		t.Fatalf("IsEnabled error: %v", err)
	}
	if !ok {
		t.Error("expected dotween to be enabled")
	}

	// Nonexistent asset returns false without error.
	ok, err = IsEnabled("nonexistent")
	if err != nil {
		t.Fatalf("IsEnabled error: %v", err)
	}
	if ok {
		t.Error("expected nonexistent asset to be disabled")
	}
}

func TestCategoryNamesAndOrder(t *testing.T) {
	if len(CategoryNames) != len(CategoryOrder) {
		t.Errorf("CategoryNames length %d != CategoryOrder length %d", len(CategoryNames), len(CategoryOrder))
	}

	seen := make(map[string]bool)
	for _, key := range CategoryOrder {
		if seen[key] {
			t.Errorf("duplicate key %q in CategoryOrder", key)
		}
		seen[key] = true
		if _, ok := CategoryNames[key]; !ok {
			t.Errorf("key %q in CategoryOrder missing from CategoryNames", key)
		}
	}

	for key := range CategoryNames {
		if !seen[key] {
			t.Errorf("key %q in CategoryNames missing from CategoryOrder", key)
		}
	}
}
