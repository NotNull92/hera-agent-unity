package toolregistry

import (
	"context"
	"encoding/json"
	"fmt"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

const legacyCatalogSchema = "hera.tool-catalog/legacy"

type LegacyProvider struct {
	sender    Sender
	timeoutMs int
}

type legacyTool struct {
	Name         string          `json:"name"`
	Description  string          `json:"description"`
	Actions      []legacyAction  `json:"actions"`
	InputSchema  json.RawMessage `json:"schema"`
	OutputSchema json.RawMessage `json:"output_schema"`
}

type legacyAction struct {
	Name        string `json:"name"`
	Description string `json:"description"`
}

func NewLegacyProvider(sender Sender) *LegacyProvider {
	return &LegacyProvider{sender: sender, timeoutMs: defaultCatalogTimeoutMs}
}

func (provider *LegacyProvider) Load(
	ctx context.Context,
	instance *client.Instance,
) (*Snapshot, error) {
	names, err := provider.loadNames(ctx, instance)
	if err != nil {
		return nil, err
	}
	tools := make([]Tool, 0, len(names))
	for _, name := range names {
		tool, loadErr := provider.loadTool(ctx, instance, name)
		if loadErr != nil {
			return nil, loadErr
		}
		tools = append(tools, tool)
	}
	catalog, err := buildLegacyCatalog(instance, tools)
	if err != nil {
		return nil, err
	}
	return &Snapshot{
		Catalog:  catalog,
		Exposure: ExposureCompactOnly,
	}, nil
}

func (provider *LegacyProvider) loadNames(
	ctx context.Context,
	instance *client.Instance,
) ([]string, error) {
	response, err := provider.sender.Send(ctx, instance, "list", map[string]any{
		"names": true,
	}, provider.timeoutMs)
	if err != nil {
		return nil, fmt.Errorf("request legacy tool names: %w", err)
	}
	data, err := responseData(response, "legacy tool names")
	if err != nil {
		return nil, err
	}
	var names []string
	if err := decodeSingle(data, &names); err != nil {
		return nil, fmt.Errorf("decode legacy tool names: %w", err)
	}
	slices.Sort(names)
	names = slices.Compact(names)
	if len(names) == 0 {
		return nil, fmt.Errorf("legacy Connector returned no tools")
	}
	return names, nil
}

func (provider *LegacyProvider) loadTool(
	ctx context.Context,
	instance *client.Instance,
	name string,
) (Tool, error) {
	response, err := provider.sender.Send(ctx, instance, "list", map[string]any{
		"tool": name,
	}, provider.timeoutMs)
	if err != nil {
		return Tool{}, fmt.Errorf("request legacy tool %q: %w", name, err)
	}
	data, err := responseData(response, "legacy tool "+name)
	if err != nil {
		return Tool{}, err
	}
	var legacy legacyTool
	if err := decodeSingle(data, &legacy); err != nil {
		return Tool{}, fmt.Errorf("decode legacy tool %q: %w", name, err)
	}
	if legacy.Name != name || !jsonObject(legacy.InputSchema) || !jsonObject(legacy.OutputSchema) {
		return Tool{}, fmt.Errorf("legacy tool %q has an invalid contract", name)
	}
	actions := make([]Action, 0, len(legacy.Actions))
	for _, action := range legacy.Actions {
		actions = append(actions, Action{
			Name:         action.Name,
			Description:  action.Description,
			Aliases:      []string{},
			InputSchema:  json.RawMessage(`{"type":"object","additionalProperties":true}`),
			OutputSchema: json.RawMessage(`{"type":"object","additionalProperties":true}`),
			Safety:       conservativeLegacySafety(),
		})
	}
	slices.SortFunc(actions, func(left, right Action) int {
		return compare(left.Name, right.Name)
	})
	return Tool{
		Name:         legacy.Name,
		Title:        legacy.Name,
		Description:  legacy.Description,
		Source:       Source{Kind: "unknown", Assembly: "unknown", Type: "unknown"},
		ContractMode: ContractLegacy,
		Profiles:     []string{"compact"},
		Aliases:      []string{},
		Examples:     []Example{},
		InputSchema:  legacy.InputSchema,
		OutputSchema: legacy.OutputSchema,
		Actions:      actions,
		Safety:       conservativeLegacySafety(),
	}, nil
}

func conservativeLegacySafety() Safety {
	return Safety{
		RiskClass:            "unspecified",
		Destructive:          true,
		RequiresConfirmation: true,
		SideEffectScope:      "unknown",
		Rules:                []SafetyRule{},
	}
}

func buildLegacyCatalog(instance *client.Instance, tools []Tool) (*Catalog, error) {
	projectID, err := ProjectID(instance.ProjectPath)
	if err != nil {
		return nil, err
	}
	catalog := &Catalog{
		SchemaVersion: legacyCatalogSchema,
		DomainEpoch:   instance.DomainEpoch,
		ProjectID:     projectID,
		Tools:         tools,
	}
	if catalog.DomainEpoch == "" {
		catalog.DomainEpoch = "legacy"
	}
	document := map[string]json.RawMessage{}
	data, err := json.Marshal(catalog)
	if err != nil {
		return nil, fmt.Errorf("encode legacy catalog: %w", err)
	}
	if err := json.Unmarshal(data, &document); err != nil {
		return nil, fmt.Errorf("normalize legacy catalog: %w", err)
	}
	catalog.CatalogHash, err = computeCatalogHash(document)
	if err != nil {
		return nil, err
	}
	return catalog, nil
}
