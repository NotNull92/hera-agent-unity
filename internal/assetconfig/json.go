package assetconfig

import "encoding/json"

func (entry *AssetEntry) UnmarshalJSON(data []byte) error {
	var fields struct {
		ID            string `json:"id"`
		Name          string `json:"name"`
		Enabled       bool   `json:"enabled"`
		Installed     bool   `json:"installed"`
		Category      string `json:"category"`
		Description   string `json:"description"`
		DocURL        string `json:"doc_url"`
		ReferencePath string `json:"reference_path"`
	}
	if err := json.Unmarshal(data, &fields); err != nil {
		return err
	}

	var extra map[string]json.RawMessage
	if err := json.Unmarshal(data, &extra); err != nil {
		return err
	}
	deleteKnownAssetEntryFields(extra)

	*entry = AssetEntry{
		ID:            fields.ID,
		Name:          fields.Name,
		Enabled:       fields.Enabled,
		Installed:     fields.Installed,
		Category:      fields.Category,
		Description:   fields.Description,
		DocURL:        fields.DocURL,
		ReferencePath: fields.ReferencePath,
		extra:         extra,
	}
	return nil
}

func (entry AssetEntry) MarshalJSON() ([]byte, error) {
	root := cloneRawMessages(entry.extra)
	if err := addRawMessage(root, "id", entry.ID); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "name", entry.Name); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "enabled", entry.Enabled); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "installed", entry.Installed); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "category", entry.Category); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "description", entry.Description); err != nil {
		return nil, err
	}
	if err := addOptionalRawMessage(root, "doc_url", entry.DocURL); err != nil {
		return nil, err
	}
	if err := addOptionalRawMessage(root, "reference_path", entry.ReferencePath); err != nil {
		return nil, err
	}
	return json.Marshal(root)
}

func (cfg *AssetConfig) UnmarshalJSON(data []byte) error {
	var fields struct {
		Version             string              `json:"version"`
		Assets              []AssetEntry        `json:"assets"`
		LoopEngineeringMode LoopEngineeringMode `json:"loopEngineeringMode"`
		UISystem            UISystem            `json:"ui_system"`
		GameFeelUIMode      bool                `json:"game_feel_ui_mode"`
		GameFeelMode        bool                `json:"game_feel_mode"`
		UISlopMode          bool                `json:"ui_slop_mode"`
		DefaultCscPath      string              `json:"defaultCscPath"`
		DefaultDotnetPath   string              `json:"defaultDotnetPath"`
	}
	if err := json.Unmarshal(data, &fields); err != nil {
		return err
	}

	var extra map[string]json.RawMessage
	if err := json.Unmarshal(data, &extra); err != nil {
		return err
	}
	deleteKnownAssetConfigFields(extra)

	*cfg = AssetConfig{
		Version:             fields.Version,
		Assets:              fields.Assets,
		LoopEngineeringMode: fields.LoopEngineeringMode,
		UISystem:            fields.UISystem,
		GameFeelUIMode:      fields.GameFeelUIMode,
		GameFeelMode:        fields.GameFeelMode,
		UISlopMode:          fields.UISlopMode,
		DefaultCscPath:      fields.DefaultCscPath,
		DefaultDotnetPath:   fields.DefaultDotnetPath,
		extra:               extra,
	}
	return nil
}

func (cfg AssetConfig) MarshalJSON() ([]byte, error) {
	root := cloneRawMessages(cfg.extra)
	if err := addRawMessage(root, "version", cfg.Version); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "assets", cfg.Assets); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "loopEngineeringMode", cfg.LoopEngineeringMode); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "ui_system", cfg.UISystem); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "game_feel_ui_mode", cfg.GameFeelUIMode); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "game_feel_mode", cfg.GameFeelMode); err != nil {
		return nil, err
	}
	if err := addRawMessage(root, "ui_slop_mode", cfg.UISlopMode); err != nil {
		return nil, err
	}
	if err := addOptionalRawMessage(root, "defaultCscPath", cfg.DefaultCscPath); err != nil {
		return nil, err
	}
	if err := addOptionalRawMessage(root, "defaultDotnetPath", cfg.DefaultDotnetPath); err != nil {
		return nil, err
	}
	return json.Marshal(root)
}

func deleteKnownAssetEntryFields(root map[string]json.RawMessage) {
	for _, key := range []string{"id", "name", "enabled", "installed", "category", "description", "doc_url", "reference_path"} {
		delete(root, key)
	}
}

func deleteKnownAssetConfigFields(root map[string]json.RawMessage) {
	for _, key := range []string{"version", "assets", "loopEngineeringMode", "ui_system", "game_feel_ui_mode", "game_feel_mode", "ui_slop_mode", "defaultCscPath", "defaultDotnetPath", "ui_juicy_mode"} {
		delete(root, key)
	}
}

func cloneRawMessages(source map[string]json.RawMessage) map[string]json.RawMessage {
	cloned := make(map[string]json.RawMessage, len(source))
	for key, value := range source {
		cloned[key] = append(json.RawMessage(nil), value...)
	}
	return cloned
}

func mergeRawMessages(base, latest map[string]json.RawMessage) map[string]json.RawMessage {
	merged := cloneRawMessages(base)
	for key, value := range latest {
		merged[key] = append(json.RawMessage(nil), value...)
	}
	return merged
}

func addRawMessage(root map[string]json.RawMessage, key string, value any) error {
	encoded, err := json.Marshal(value)
	if err != nil {
		return err
	}
	root[key] = encoded
	return nil
}

func addOptionalRawMessage(root map[string]json.RawMessage, key, value string) error {
	if value == "" {
		delete(root, key)
		return nil
	}
	return addRawMessage(root, key, value)
}
