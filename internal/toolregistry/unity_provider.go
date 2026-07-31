package toolregistry

import (
	"context"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

type UnityProvider struct {
	sender    Sender
	timeoutMs int
}

func NewUnityProvider(sender Sender) *UnityProvider {
	return &UnityProvider{sender: sender, timeoutMs: defaultCatalogTimeoutMs}
}

func (provider *UnityProvider) Load(
	ctx context.Context,
	instance *client.Instance,
) (*Snapshot, error) {
	response, err := provider.sender.Send(ctx, instance, "list", map[string]any{
		"catalog":        true,
		"schema_version": CatalogSchemaV1,
	}, provider.timeoutMs)
	if err != nil {
		return nil, fmt.Errorf("request Unity tool catalog: %w", err)
	}
	data, err := responseData(response, "Unity tool catalog")
	if err != nil {
		return nil, err
	}
	catalog, err := ParseCatalog(data)
	if err != nil {
		return nil, fmt.Errorf("parse Unity tool catalog: %w", err)
	}
	return &Snapshot{Catalog: catalog, Exposure: ExposureProfile}, nil
}
