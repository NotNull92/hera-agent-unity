package main

import "github.com/NotNull92/hera-agent-unity/internal/toolregistry"

type reportOptions struct {
	WarnProfileBytes int
	WarnToolBytes    int
	Largest          int
}

type report struct {
	Schema                 string            `json:"schema"`
	CatalogHash            string            `json:"catalog_hash"`
	InputBytes             int               `json:"input_bytes"`
	NormalizedCatalogBytes int               `json:"normalized_catalog_bytes"`
	ToolCount              int               `json:"tool_count"`
	ActionCount            int               `json:"action_count"`
	DescriptionCharacters  int               `json:"description_characters"`
	MCPCompact             mcpPayloadSize    `json:"mcp_compact"`
	Profiles               []profileSize     `json:"profiles"`
	LargestTools           []toolSize        `json:"largest_tools"`
	LargestActions         []actionSize      `json:"largest_actions"`
	ActionDescribeSavings  []describeSaving  `json:"action_describe_savings"`
	RoughTokens            tokenEstimates    `json:"normalized_catalog_rough_token_estimates"`
	Warnings               []string          `json:"warnings"`
	Comparison             *reportComparison `json:"comparison,omitempty"`
}

type mcpPayloadSize struct {
	ToolCount           int            `json:"tool_count"`
	ToolDefinitionBytes int            `json:"tool_definition_bytes"`
	RoughTokens         tokenEstimates `json:"rough_token_estimates"`
}

type profileSize struct {
	Name                    string         `json:"name"`
	ToolCount               int            `json:"tool_count"`
	NormalizedContractBytes int            `json:"normalized_contract_bytes"`
	MCPToolDefinitionBytes  int            `json:"mcp_tool_definition_bytes"`
	MCPRoughTokens          tokenEstimates `json:"mcp_rough_token_estimates"`
}

type reportComparison struct {
	BaselineCatalogHash         string         `json:"baseline_catalog_hash"`
	ContractChanged             bool           `json:"contract_changed"`
	ToolCountDelta              int            `json:"tool_count_delta"`
	ActionCountDelta            int            `json:"action_count_delta"`
	NormalizedCatalogBytesDelta int            `json:"normalized_catalog_bytes_delta"`
	DescriptionCharactersDelta  int            `json:"description_characters_delta"`
	MCPCompactBytesDelta        int            `json:"mcp_compact_bytes_delta"`
	Profiles                    []profileDelta `json:"profiles"`
	Growth                      bool           `json:"growth"`
	ReviewRequired              bool           `json:"review_required"`
	Reasons                     []string       `json:"reasons"`
}

type profileDelta struct {
	Name                           string `json:"name"`
	BaselineToolCount              int    `json:"baseline_tool_count"`
	CurrentToolCount               int    `json:"current_tool_count"`
	ToolCountDelta                 int    `json:"tool_count_delta"`
	BaselineContractBytes          int    `json:"baseline_contract_bytes"`
	CurrentContractBytes           int    `json:"current_contract_bytes"`
	ContractBytesDelta             int    `json:"contract_bytes_delta"`
	BaselineMCPToolDefinitionBytes int    `json:"baseline_mcp_tool_definition_bytes"`
	CurrentMCPToolDefinitionBytes  int    `json:"current_mcp_tool_definition_bytes"`
	MCPToolDefinitionBytesDelta    int    `json:"mcp_tool_definition_bytes_delta"`
}

type toolSize struct {
	Name              string `json:"name"`
	Bytes             int    `json:"bytes"`
	ActionCount       int    `json:"action_count"`
	DescriptionLength int    `json:"description_characters"`
}

type actionSize struct {
	Tool              string `json:"tool"`
	Action            string `json:"action"`
	Bytes             int    `json:"bytes"`
	DescriptionLength int    `json:"description_characters"`
}

type describeSaving struct {
	Tool        string  `json:"tool"`
	Action      string  `json:"action"`
	FullBytes   int     `json:"legacy_full_tool_bytes"`
	ActionBytes int     `json:"action_describe_bytes"`
	SavedBytes  int     `json:"saved_bytes"`
	SavedRatio  float64 `json:"saved_ratio"`
}

type compactToolIdentity struct {
	Name         string              `json:"name"`
	Title        string              `json:"title"`
	Description  string              `json:"description"`
	Source       toolregistry.Source `json:"source"`
	ContractMode string              `json:"contract_mode"`
	Profiles     []string            `json:"profiles"`
	Aliases      []string            `json:"aliases"`
}

type tokenEstimates struct {
	Assumption string `json:"assumption"`
	Low        int    `json:"low_4_bytes_per_token"`
	Central    int    `json:"central_3_2_bytes_per_token"`
	High       int    `json:"high_2_5_bytes_per_token"`
}
