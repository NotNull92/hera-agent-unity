package mcpserver

import (
	"encoding/json"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type compactToolIdentity struct {
	Name         string              `json:"name"`
	Title        string              `json:"title"`
	Description  string              `json:"description"`
	Source       toolregistry.Source `json:"source"`
	ContractMode string              `json:"contract_mode"`
	Profiles     []string            `json:"profiles"`
	Aliases      []string            `json:"aliases"`
}

type compactSafety struct {
	RiskClass            string `json:"risk_class"`
	ReadOnly             bool   `json:"read_only"`
	Destructive          bool   `json:"destructive"`
	Idempotent           bool   `json:"idempotent"`
	RequiresConfirmation bool   `json:"requires_confirmation"`
}

type compactActionOverview struct {
	Name        string        `json:"name"`
	Description string        `json:"description"`
	Aliases     []string      `json:"aliases"`
	Safety      compactSafety `json:"safety"`
}

type compactToolOverview struct {
	Tool         compactToolIdentity     `json:"tool"`
	Actions      []compactActionOverview `json:"actions,omitempty"`
	InputSchema  json.RawMessage         `json:"input_schema,omitempty"`
	OutputSchema json.RawMessage         `json:"output_schema,omitempty"`
	ToolSafety   compactSafety           `json:"tool_safety"`
	CatalogHash  string                  `json:"catalog_hash"`
	DomainEpoch  string                  `json:"domain_epoch"`
}

type compactActionDescription struct {
	Tool        compactToolIdentity `json:"tool"`
	Action      toolregistry.Action `json:"action"`
	ToolSafety  toolregistry.Safety `json:"tool_safety"`
	CatalogHash string              `json:"catalog_hash"`
	DomainEpoch string              `json:"domain_epoch"`
}

func describeToolOverview(catalog *toolregistry.Catalog, tool toolregistry.Tool) compactToolOverview {
	overview := compactToolOverview{
		Tool: compactIdentity(tool), ToolSafety: summarizeSafety(tool.Safety),
		CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch,
	}
	for _, action := range tool.Actions {
		overview.Actions = append(overview.Actions, compactActionOverview{
			Name: action.Name, Description: action.Description, Aliases: action.Aliases,
			Safety: summarizeSafety(action.Safety),
		})
	}
	if len(tool.Actions) == 0 {
		overview.InputSchema = tool.InputSchema
		overview.OutputSchema = tool.OutputSchema
	}
	return overview
}

func compactIdentity(tool toolregistry.Tool) compactToolIdentity {
	return compactToolIdentity{
		Name: tool.Name, Title: tool.Title, Description: tool.Description,
		Source: tool.Source, ContractMode: tool.ContractMode,
		Profiles: tool.Profiles, Aliases: tool.Aliases,
	}
}

func summarizeSafety(safety toolregistry.Safety) compactSafety {
	summary := compactSafety{
		RiskClass: safety.RiskClass, ReadOnly: safety.ReadOnly,
		Destructive: safety.Destructive, Idempotent: safety.Idempotent,
		RequiresConfirmation: safety.RequiresConfirmation,
	}
	flattened := flattenSafety(safety)
	for _, candidate := range flattened[1:] {
		summary.ReadOnly = summary.ReadOnly && candidate.ReadOnly
		summary.Destructive = summary.Destructive || candidate.Destructive
		summary.Idempotent = summary.Idempotent && candidate.Idempotent
		summary.RequiresConfirmation = summary.RequiresConfirmation || candidate.RequiresConfirmation
	}
	if len(flattened) > 1 {
		summary.RiskClass = "conditional"
	}
	return summary
}
