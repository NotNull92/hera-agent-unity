package toolregistry

import (
	"encoding/json"

	"github.com/NotNull92/hera-agent-unity/internal/protocol"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

const (
	CatalogSchemaV1      = protocol.ToolCatalogSchemaVersion
	FeatureDomainEpochV1 = protocol.FeatureDomainEpochV1
	FeatureToolCatalogV1 = protocol.FeatureToolCatalogV1
	ContractStrict       = "strict"
	ContractLegacy       = "legacy"
)

type Exposure string

const (
	ExposureProfile     Exposure = "profile"
	ExposureCompactOnly Exposure = "compact_only"
)

type Catalog struct {
	SchemaVersion string `json:"schema_version"`
	CatalogHash   string `json:"catalog_hash"`
	DomainEpoch   string `json:"domain_epoch"`
	ProjectID     string `json:"project_id"`
	Tools         []Tool `json:"tools"`
}

type Tool struct {
	Name         string          `json:"name"`
	Title        string          `json:"title"`
	Description  string          `json:"description"`
	Source       Source          `json:"source"`
	ContractMode string          `json:"contract_mode"`
	Profiles     []string        `json:"profiles"`
	Aliases      []string        `json:"aliases"`
	Examples     []Example       `json:"examples"`
	InputSchema  json.RawMessage `json:"input_schema"`
	OutputSchema json.RawMessage `json:"output_schema"`
	Actions      []Action        `json:"actions"`
	Safety       Safety          `json:"safety"`
}

type Source struct {
	Kind     string `json:"kind"`
	Assembly string `json:"assembly"`
	Type     string `json:"type"`
}

type Example struct {
	Call        string `json:"call"`
	Description string `json:"description"`
}

type Action struct {
	Name         string          `json:"name"`
	Description  string          `json:"description"`
	Aliases      []string        `json:"aliases"`
	InputSchema  json.RawMessage `json:"input_schema"`
	OutputSchema json.RawMessage `json:"output_schema"`
	Safety       Safety          `json:"safety"`
}

type Safety struct {
	RiskClass            string       `json:"risk_class"`
	ReadOnly             bool         `json:"read_only"`
	Destructive          bool         `json:"destructive"`
	Idempotent           bool         `json:"idempotent"`
	MayReloadDomain      bool         `json:"may_reload_domain"`
	RequiresPlayMode     bool         `json:"requires_play_mode"`
	RequiresConfirmation bool         `json:"requires_confirmation"`
	Reversible           bool         `json:"reversible"`
	SupportsCancellation bool         `json:"supports_cancellation"`
	SideEffectScope      string       `json:"side_effect_scope"`
	Rules                []SafetyRule `json:"rules"`
}

type SafetyRule struct {
	Operation string          `json:"operation"`
	When      json.RawMessage `json:"when"`
	Safety
}

type Snapshot struct {
	Catalog   *Catalog
	Schemas   *schema.CompiledCatalog
	Exposure  Exposure
	FromCache bool
}

func (catalog *Catalog) SchemaDefinitions() []schema.Definition {
	definitions := make([]schema.Definition, 0, len(catalog.Tools)*4)
	for _, tool := range catalog.Tools {
		if tool.ContractMode != ContractStrict {
			continue
		}
		definitions = append(definitions,
			schema.Definition{Key: tool.Name + "/input", Schema: tool.InputSchema},
			schema.Definition{Key: tool.Name + "/output", Schema: tool.OutputSchema},
		)
		for _, action := range tool.Actions {
			prefix := tool.Name + "/" + action.Name
			definitions = append(definitions,
				schema.Definition{Key: prefix + "/input", Schema: action.InputSchema},
				schema.Definition{Key: prefix + "/output", Schema: action.OutputSchema},
			)
		}
	}
	return definitions
}
