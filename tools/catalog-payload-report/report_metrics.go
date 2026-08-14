package main

import (
	"encoding/json"
	"fmt"
	"slices"
	"sort"

	"github.com/NotNull92/hera-agent-unity/internal/mcpserver"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func buildReport(catalog *toolregistry.Catalog, inputBytes int, options reportOptions) (report, error) {
	if options.Largest < 1 {
		return report{}, fmt.Errorf("largest must be positive")
	}
	normalized, err := json.Marshal(catalog)
	if err != nil {
		return report{}, fmt.Errorf("encode normalized catalog: %w", err)
	}
	compactPayload, err := mcpserver.MeasureCompactToolDefinitions()
	if err != nil {
		return report{}, err
	}
	result := report{
		Schema:                 reportSchema,
		CatalogHash:            catalog.CatalogHash,
		InputBytes:             inputBytes,
		NormalizedCatalogBytes: len(normalized),
		ToolCount:              len(catalog.Tools),
		MCPCompact: mcpPayloadSize{
			ToolCount: compactPayload.ToolCount, ToolDefinitionBytes: compactPayload.Bytes,
			RoughTokens: tokenRange(compactPayload.Bytes),
		},
		Warnings: []string{},
	}
	profiles := make(map[string]struct{})
	for _, tool := range catalog.Tools {
		result.ActionCount += len(tool.Actions)
		result.DescriptionCharacters += len([]rune(tool.Description))
		encoded, marshalErr := json.Marshal(tool)
		if marshalErr != nil {
			return report{}, fmt.Errorf("encode tool %s: %w", tool.Name, marshalErr)
		}
		result.LargestTools = append(result.LargestTools, toolSize{
			Name: tool.Name, Bytes: len(encoded), ActionCount: len(tool.Actions),
			DescriptionLength: len([]rune(tool.Description)),
		})
		if options.WarnToolBytes > 0 && len(encoded) > options.WarnToolBytes {
			result.Warnings = append(result.Warnings,
				fmt.Sprintf("tool %s uses %d normalized bytes (warning budget %d)", tool.Name, len(encoded), options.WarnToolBytes))
		}
		for _, profile := range tool.Profiles {
			profiles[profile] = struct{}{}
		}
		fullDescribe, describeErr := json.Marshal(struct {
			Tool        toolregistry.Tool `json:"tool"`
			CatalogHash string            `json:"catalog_hash"`
			DomainEpoch string            `json:"domain_epoch"`
		}{Tool: tool, CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch})
		if describeErr != nil {
			return report{}, fmt.Errorf("encode full description %s: %w", tool.Name, describeErr)
		}
		for _, action := range tool.Actions {
			actionEncoded, actionErr := json.Marshal(action)
			if actionErr != nil {
				return report{}, fmt.Errorf("encode action %s/%s: %w", tool.Name, action.Name, actionErr)
			}
			result.LargestActions = append(result.LargestActions, actionSize{
				Tool: tool.Name, Action: action.Name, Bytes: len(actionEncoded),
				DescriptionLength: len([]rune(action.Description)),
			})
			selectedDescribe, selectedErr := json.Marshal(struct {
				Tool        compactToolIdentity `json:"tool"`
				Action      toolregistry.Action `json:"action"`
				ToolSafety  toolregistry.Safety `json:"tool_safety"`
				CatalogHash string              `json:"catalog_hash"`
				DomainEpoch string              `json:"domain_epoch"`
			}{
				Tool: compactToolIdentity{
					Name: tool.Name, Title: tool.Title, Description: tool.Description,
					Source: tool.Source, ContractMode: tool.ContractMode,
					Profiles: tool.Profiles, Aliases: tool.Aliases,
				},
				Action: action, ToolSafety: tool.Safety,
				CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch,
			})
			if selectedErr != nil {
				return report{}, fmt.Errorf("encode selected description %s/%s: %w", tool.Name, action.Name, selectedErr)
			}
			saved := len(fullDescribe) - len(selectedDescribe)
			ratio := 0.0
			if len(fullDescribe) > 0 {
				ratio = float64(saved) / float64(len(fullDescribe))
			}
			result.ActionDescribeSavings = append(result.ActionDescribeSavings, describeSaving{
				Tool: tool.Name, Action: action.Name, FullBytes: len(fullDescribe),
				ActionBytes: len(selectedDescribe), SavedBytes: saved, SavedRatio: ratio,
			})
		}
	}
	profileNames := make([]string, 0, len(profiles))
	for name := range profiles {
		profileNames = append(profileNames, name)
	}
	sort.Strings(profileNames)
	for _, name := range profileNames {
		tools, profileErr := catalog.ToolsForProfile(name)
		if profileErr != nil {
			return report{}, fmt.Errorf("resolve profile %s: %w", name, profileErr)
		}
		encoded, marshalErr := json.Marshal(tools)
		if marshalErr != nil {
			return report{}, fmt.Errorf("encode profile %s: %w", name, marshalErr)
		}
		mcpPayload, measureErr := mcpserver.MeasureProfileToolDefinitions(catalog, name)
		if measureErr != nil {
			return report{}, fmt.Errorf("measure MCP profile %s: %w", name, measureErr)
		}
		size := profileSize{
			Name: name, ToolCount: len(tools), NormalizedContractBytes: len(encoded),
			MCPToolDefinitionBytes: mcpPayload.Bytes, MCPRoughTokens: tokenRange(mcpPayload.Bytes),
		}
		result.Profiles = append(result.Profiles, size)
		if options.WarnProfileBytes > 0 && size.NormalizedContractBytes > options.WarnProfileBytes {
			result.Warnings = append(result.Warnings,
				fmt.Sprintf("profile %s uses %d normalized bytes (warning budget %d)", name, size.NormalizedContractBytes, options.WarnProfileBytes))
		}
	}
	sort.Slice(result.LargestTools, func(i, j int) bool {
		if result.LargestTools[i].Bytes != result.LargestTools[j].Bytes {
			return result.LargestTools[i].Bytes > result.LargestTools[j].Bytes
		}
		return result.LargestTools[i].Name < result.LargestTools[j].Name
	})
	sort.Slice(result.LargestActions, func(i, j int) bool {
		if result.LargestActions[i].Bytes != result.LargestActions[j].Bytes {
			return result.LargestActions[i].Bytes > result.LargestActions[j].Bytes
		}
		if result.LargestActions[i].Tool != result.LargestActions[j].Tool {
			return result.LargestActions[i].Tool < result.LargestActions[j].Tool
		}
		return result.LargestActions[i].Action < result.LargestActions[j].Action
	})
	sort.Slice(result.ActionDescribeSavings, func(i, j int) bool {
		if result.ActionDescribeSavings[i].SavedBytes != result.ActionDescribeSavings[j].SavedBytes {
			return result.ActionDescribeSavings[i].SavedBytes > result.ActionDescribeSavings[j].SavedBytes
		}
		if result.ActionDescribeSavings[i].Tool != result.ActionDescribeSavings[j].Tool {
			return result.ActionDescribeSavings[i].Tool < result.ActionDescribeSavings[j].Tool
		}
		return result.ActionDescribeSavings[i].Action < result.ActionDescribeSavings[j].Action
	})
	result.LargestTools = result.LargestTools[:min(options.Largest, len(result.LargestTools))]
	result.LargestActions = result.LargestActions[:min(options.Largest, len(result.LargestActions))]
	result.ActionDescribeSavings = result.ActionDescribeSavings[:min(options.Largest, len(result.ActionDescribeSavings))]
	result.RoughTokens = tokenRange(result.NormalizedCatalogBytes)
	slices.Sort(result.Warnings)
	return result, nil
}

func tokenRange(size int) tokenEstimates {
	return tokenEstimates{
		Assumption: "rough JSON estimate only; use provider usage/tokenizer for billing decisions",
		Low:        divideRoundUp(size*10, 40),
		Central:    divideRoundUp(size*10, 32),
		High:       divideRoundUp(size*10, 25),
	}
}

func divideRoundUp(numerator, denominator int) int {
	return (numerator + denominator - 1) / denominator
}

func min(left, right int) int {
	if left < right {
		return left
	}
	return right
}
