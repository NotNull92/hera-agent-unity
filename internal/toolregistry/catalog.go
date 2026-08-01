package toolregistry

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"regexp"
	"slices"
)

var sha256Pattern = regexp.MustCompile(`^sha256:[0-9a-f]{64}$`)

func ParseCatalog(data []byte) (*Catalog, error) {
	var document map[string]json.RawMessage
	if err := decodeSingle(data, &document); err != nil {
		return nil, fmt.Errorf("decode tool catalog document: %w", err)
	}

	var catalog Catalog
	if err := decodeSingleStrict(data, &catalog); err != nil {
		return nil, fmt.Errorf("decode tool catalog: %w", err)
	}
	if err := validateCatalog(&catalog); err != nil {
		return nil, err
	}
	actualHash, err := computeCatalogHash(document)
	if err != nil {
		return nil, err
	}
	if catalog.CatalogHash != actualHash {
		return nil, fmt.Errorf("%w: got %s, computed %s",
			ErrCatalogHashMismatch, catalog.CatalogHash, actualHash)
	}
	return &catalog, nil
}

func decodeSingle(data []byte, target any) error {
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.UseNumber()
	if err := decoder.Decode(target); err != nil {
		return err
	}
	if err := decoder.Decode(&struct{}{}); err != io.EOF {
		return fmt.Errorf("unexpected trailing JSON")
	}
	return nil
}

func decodeSingleStrict(data []byte, target any) error {
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(target); err != nil {
		return err
	}
	if err := decoder.Decode(&struct{}{}); err != io.EOF {
		return fmt.Errorf("unexpected trailing JSON")
	}
	return nil
}

func validateCatalog(catalog *Catalog) error {
	if catalog.SchemaVersion != CatalogSchemaV1 {
		return fmt.Errorf("unsupported catalog schema %q", catalog.SchemaVersion)
	}
	if !sha256Pattern.MatchString(catalog.CatalogHash) {
		return fmt.Errorf("catalog hash %q is not lowercase SHA-256", catalog.CatalogHash)
	}
	if !sha256Pattern.MatchString(catalog.ProjectID) {
		return fmt.Errorf("project id %q is not lowercase SHA-256", catalog.ProjectID)
	}
	if catalog.DomainEpoch == "" {
		return fmt.Errorf("catalog domain epoch is required")
	}
	if len(catalog.Tools) == 0 {
		return fmt.Errorf("catalog tools are required")
	}
	if !slices.IsSortedFunc(catalog.Tools, func(left, right Tool) int {
		return compare(left.Name, right.Name)
	}) {
		return fmt.Errorf("catalog tools are not ordinal sorted")
	}
	for index := range catalog.Tools {
		if err := validateTool(&catalog.Tools[index]); err != nil {
			return err
		}
		if index > 0 && catalog.Tools[index-1].Name == catalog.Tools[index].Name {
			return fmt.Errorf("duplicate catalog tool %q", catalog.Tools[index].Name)
		}
	}
	return nil
}

func validateTool(tool *Tool) error {
	if tool.Name == "" || tool.Title == "" || tool.Source.Kind == "" ||
		tool.Source.Assembly == "" || tool.Source.Type == "" {
		return fmt.Errorf("catalog tool has incomplete identity: %q", tool.Name)
	}
	if tool.ContractMode != ContractStrict && tool.ContractMode != ContractLegacy {
		return fmt.Errorf("tool %q has unsupported contract mode %q", tool.Name, tool.ContractMode)
	}
	if !slices.IsSorted(tool.Profiles) || hasDuplicate(tool.Profiles) {
		return fmt.Errorf("tool %q profiles are not unique and ordinal sorted", tool.Name)
	}
	visibleInNormalProfile := false
	for _, profile := range tool.Profiles {
		visibleInNormalProfile = visibleInNormalProfile || IsNormalProfile(profile)
	}
	if visibleInNormalProfile && tool.ContractMode != ContractStrict {
		return fmt.Errorf("legacy tool %q is visible in a normal profile", tool.Name)
	}
	if visibleInNormalProfile && ToolHasArbitraryCode(*tool) {
		return fmt.Errorf("arbitrary-code tool %q is visible in a normal profile", tool.Name)
	}
	if !jsonObject(tool.InputSchema) || !jsonObject(tool.OutputSchema) {
		return fmt.Errorf("tool %q has invalid schemas", tool.Name)
	}
	if !slices.IsSortedFunc(tool.Actions, func(left, right Action) int {
		return compare(left.Name, right.Name)
	}) {
		return fmt.Errorf("tool %q actions are not ordinal sorted", tool.Name)
	}
	for index := range tool.Actions {
		action := &tool.Actions[index]
		if action.Name == "" || !jsonObject(action.InputSchema) || !jsonObject(action.OutputSchema) {
			return fmt.Errorf("tool %q has invalid action %q", tool.Name, action.Name)
		}
		if index > 0 && tool.Actions[index-1].Name == action.Name {
			return fmt.Errorf("tool %q has duplicate action %q", tool.Name, action.Name)
		}
	}
	return nil
}

func ToolHasArbitraryCode(tool Tool) bool {
	if safetyHasRisk(tool.Safety, "arbitrary_code") {
		return true
	}
	for _, action := range tool.Actions {
		if safetyHasRisk(action.Safety, "arbitrary_code") {
			return true
		}
	}
	return false
}

func safetyHasRisk(safety Safety, risk string) bool {
	if safety.RiskClass == risk {
		return true
	}
	for _, rule := range safety.Rules {
		if safetyHasRisk(rule.Safety, risk) {
			return true
		}
	}
	return false
}

func jsonObject(data json.RawMessage) bool {
	var value map[string]json.RawMessage
	return json.Unmarshal(data, &value) == nil && value != nil
}

func hasDuplicate(values []string) bool {
	for index := 1; index < len(values); index++ {
		if values[index-1] == values[index] {
			return true
		}
	}
	return false
}

func compare(left, right string) int {
	switch {
	case left < right:
		return -1
	case left > right:
		return 1
	default:
		return 0
	}
}
