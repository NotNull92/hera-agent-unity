package toolregistry

import (
	"context"
	"fmt"
	"slices"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

type RegistryOptions struct {
	Sender  Sender
	Cache   *CatalogCache
	Schemas *schema.CompilerCache
}

type Registry struct {
	native  Provider
	legacy  Provider
	cache   *CatalogCache
	schemas *schema.CompilerCache
}

func NewRegistry(options RegistryOptions) *Registry {
	sender := options.Sender
	if sender == nil {
		sender = client.DefaultClient
	}
	schemas := options.Schemas
	if schemas == nil {
		schemas = schema.NewCompilerCache()
	}
	cache := options.Cache
	if cache == nil {
		cache = NewCatalogCache(CacheOptions{Schemas: schemas})
	}
	return &Registry{
		native:  NewUnityProvider(sender),
		legacy:  NewLegacyProvider(sender),
		cache:   cache,
		schemas: schemas,
	}
}

func (registry *Registry) Load(
	ctx context.Context,
	instance *client.Instance,
) (*Snapshot, error) {
	if instance == nil {
		return nil, fmt.Errorf("unity instance is required")
	}
	if !slices.Contains(instance.Features, FeatureToolCatalogV1) ||
		!slices.Contains(instance.Features, FeatureDomainEpochV1) ||
		instance.DomainEpoch == "" {
		return registry.legacy.Load(ctx, instance)
	}

	projectID, err := ProjectID(instance.ProjectPath)
	if err != nil {
		return nil, err
	}
	lookup := CacheKey{
		ProjectID:   projectID,
		Features:    instance.Features,
		DomainEpoch: instance.DomainEpoch,
	}
	if cached, _, cacheErr := registry.cache.LoadMatching(lookup); cacheErr == nil {
		compiled, compileErr := registry.schemas.Compile(
			cached.CatalogHash,
			cached.SchemaDefinitions(),
		)
		if compileErr == nil {
			return &Snapshot{
				Catalog:   cached,
				Schemas:   compiled,
				Exposure:  ExposureProfile,
				FromCache: true,
			}, nil
		}
	}

	snapshot, err := registry.native.Load(ctx, instance)
	if err != nil {
		return nil, err
	}
	if snapshot.Catalog.ProjectID != projectID {
		return nil, fmt.Errorf(
			"catalog project id %q does not match heartbeat project %q",
			snapshot.Catalog.ProjectID,
			projectID,
		)
	}
	if snapshot.Catalog.DomainEpoch != instance.DomainEpoch {
		return nil, fmt.Errorf(
			"catalog domain epoch %q does not match heartbeat epoch %q",
			snapshot.Catalog.DomainEpoch,
			instance.DomainEpoch,
		)
	}
	compiled, err := registry.schemas.Compile(
		snapshot.Catalog.CatalogHash,
		snapshot.Catalog.SchemaDefinitions(),
	)
	if err != nil {
		return nil, fmt.Errorf("compile tool catalog schemas: %w", err)
	}
	key := CacheKey{
		ProjectID:   snapshot.Catalog.ProjectID,
		Features:    instance.Features,
		DomainEpoch: snapshot.Catalog.DomainEpoch,
		CatalogHash: snapshot.Catalog.CatalogHash,
	}
	_ = registry.cache.Store(key, snapshot.Catalog)
	snapshot.Schemas = compiled
	return snapshot, nil
}
