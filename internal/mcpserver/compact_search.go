package mcpserver

import (
	"encoding/json"
	"slices"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type compactSearchResult struct {
	Name         string              `json:"name"`
	Description  string              `json:"description"`
	ContractMode string              `json:"contract_mode"`
	Safety       toolregistry.Safety `json:"safety"`
	InputSchema  json.RawMessage     `json:"input_schema,omitempty"`
	score        int
}

type catalogSearch struct {
	query              string
	profile            string
	limit              int
	includeSchema      bool
	allowArbitraryCode bool
}

func searchCatalog(catalog *toolregistry.Catalog, search catalogSearch) []compactSearchResult {
	results := make([]compactSearchResult, 0, len(catalog.Tools))
	for _, tool := range catalog.Tools {
		if toolregistry.ToolHasArbitraryCode(tool) && !search.allowArbitraryCode {
			continue
		}
		if !matchesSearchProfile(tool, search.profile) {
			continue
		}
		score := lexicalScore(tool, search.query)
		if score == 0 {
			continue
		}
		result := compactSearchResult{
			Name: tool.Name, Description: tool.Description, ContractMode: tool.ContractMode,
			Safety: tool.Safety, score: score,
		}
		if search.includeSchema {
			result.InputSchema = tool.InputSchema
		}
		results = append(results, result)
	}
	slices.SortFunc(results, func(left, right compactSearchResult) int {
		if left.score != right.score {
			return right.score - left.score
		}
		return strings.Compare(left.Name, right.Name)
	})
	if len(results) > search.limit {
		results = results[:search.limit]
	}
	return results
}

func matchesSearchProfile(tool toolregistry.Tool, profile string) bool {
	if profile == "" || profile == "compact" {
		return true
	}
	return slices.Contains(tool.Profiles, profile)
}

func lexicalScore(tool toolregistry.Tool, query string) int {
	normalizedQuery := strings.ToLower(strings.TrimSpace(query))
	name := strings.ToLower(tool.Name)
	title := strings.ToLower(tool.Title)
	description := strings.ToLower(tool.Description)
	score := 0
	if name == normalizedQuery {
		score += 1000
	}
	for _, token := range strings.Fields(normalizedQuery) {
		if name == token {
			score += 200
		} else if strings.Contains(name, token) {
			score += 100
		}
		if strings.Contains(title, token) {
			score += 40
		}
		if strings.Contains(description, token) {
			score += 20
		}
		for _, action := range tool.Actions {
			if strings.Contains(strings.ToLower(action.Name+" "+action.Description), token) {
				score += 30
			}
		}
	}
	return score
}
