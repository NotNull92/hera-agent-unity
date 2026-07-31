package toolregistry

import (
	"fmt"
	"slices"
)

func IsSeedProfile(profile string) bool {
	switch profile {
	case "assets", "core", "diagnostics", "scene", "testing", "ui":
		return true
	default:
		return false
	}
}

func IsNormalProfile(profile string) bool {
	return IsSeedProfile(profile) || profile == "full"
}

func (catalog *Catalog) ToolsForProfile(profile string) ([]Tool, error) {
	if profile == "" {
		return nil, fmt.Errorf("%w: empty profile", ErrUnsupportedProfile)
	}
	tools := make([]Tool, 0, len(catalog.Tools))
	for _, tool := range catalog.Tools {
		if tool.ContractMode == ContractStrict && slices.Contains(tool.Profiles, profile) {
			tools = append(tools, tool)
		}
	}
	if len(tools) == 0 {
		return nil, fmt.Errorf("%w: %s", ErrUnsupportedProfile, profile)
	}
	slices.SortFunc(tools, func(left, right Tool) int {
		return compare(left.Name, right.Name)
	})
	return tools, nil
}
