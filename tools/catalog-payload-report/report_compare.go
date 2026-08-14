package main

import (
	"fmt"
	"sort"
)

func compareReports(baseline, current report) reportComparison {
	comparison := reportComparison{
		BaselineCatalogHash:         baseline.CatalogHash,
		ContractChanged:             baseline.CatalogHash != current.CatalogHash,
		ToolCountDelta:              current.ToolCount - baseline.ToolCount,
		ActionCountDelta:            current.ActionCount - baseline.ActionCount,
		NormalizedCatalogBytesDelta: current.NormalizedCatalogBytes - baseline.NormalizedCatalogBytes,
		DescriptionCharactersDelta:  current.DescriptionCharacters - baseline.DescriptionCharacters,
		MCPCompactBytesDelta:        current.MCPCompact.ToolDefinitionBytes - baseline.MCPCompact.ToolDefinitionBytes,
		Profiles:                    compareProfiles(baseline.Profiles, current.Profiles),
	}

	if comparison.ContractChanged {
		comparison.Reasons = append(comparison.Reasons, "catalog contract hash changed")
	}
	if comparison.ToolCountDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("tool count increased by %d", comparison.ToolCountDelta))
	}
	if comparison.ActionCountDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("action count increased by %d", comparison.ActionCountDelta))
	}
	if comparison.DescriptionCharactersDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("tool descriptions grew by %d characters", comparison.DescriptionCharactersDelta))
	}
	if comparison.MCPCompactBytesDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("compact MCP tool definitions grew by %d byte(s)", comparison.MCPCompactBytesDelta))
	}
	for _, profile := range comparison.Profiles {
		switch {
		case profile.ToolCountDelta > 0 && profile.ContractBytesDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s grew by %d tool(s) and %d normalized byte(s)",
					profile.Name, profile.ToolCountDelta, profile.ContractBytesDelta))
		case profile.ToolCountDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s gained %d tool(s)", profile.Name, profile.ToolCountDelta))
		case profile.ContractBytesDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s grew by %d normalized byte(s)", profile.Name, profile.ContractBytesDelta))
		}
		if profile.MCPToolDefinitionBytesDelta > 0 {
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s MCP tool definitions grew by %d byte(s)", profile.Name, profile.MCPToolDefinitionBytesDelta))
		}
	}
	comparison.ReviewRequired = comparison.ContractChanged || comparison.Growth
	if len(comparison.Reasons) == 0 {
		comparison.Reasons = []string{}
	}
	return comparison
}

func compareProfiles(baseline, current []profileSize) []profileDelta {
	baselineByName := make(map[string]profileSize, len(baseline))
	currentByName := make(map[string]profileSize, len(current))
	names := make(map[string]struct{}, len(baseline)+len(current))
	for _, profile := range baseline {
		baselineByName[profile.Name] = profile
		names[profile.Name] = struct{}{}
	}
	for _, profile := range current {
		currentByName[profile.Name] = profile
		names[profile.Name] = struct{}{}
	}
	orderedNames := make([]string, 0, len(names))
	for name := range names {
		orderedNames = append(orderedNames, name)
	}
	sort.Strings(orderedNames)

	result := make([]profileDelta, 0, len(orderedNames))
	for _, name := range orderedNames {
		before := baselineByName[name]
		after := currentByName[name]
		result = append(result, profileDelta{
			Name:              name,
			BaselineToolCount: before.ToolCount, CurrentToolCount: after.ToolCount,
			ToolCountDelta:                 after.ToolCount - before.ToolCount,
			BaselineContractBytes:          before.NormalizedContractBytes,
			CurrentContractBytes:           after.NormalizedContractBytes,
			ContractBytesDelta:             after.NormalizedContractBytes - before.NormalizedContractBytes,
			BaselineMCPToolDefinitionBytes: before.MCPToolDefinitionBytes,
			CurrentMCPToolDefinitionBytes:  after.MCPToolDefinitionBytes,
			MCPToolDefinitionBytesDelta:    after.MCPToolDefinitionBytes - before.MCPToolDefinitionBytes,
		})
	}
	return result
}
