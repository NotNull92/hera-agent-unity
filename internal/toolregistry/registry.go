package toolregistry

import (
	"context"
	"fmt"
	"slices"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

type RegistryOptions struct {
	Sender  Sender
	Cache   *CatalogCache
	Schemas *schema.CompilerCache
	Refresh InstanceRefresher
}

// InstanceRefresher re-reads the heartbeat of the Editor an instance describes.
type InstanceRefresher func(*client.Instance) (*client.Instance, error)

// The heartbeat is written about once a second and not at all while the domain
// reloads, so the settle window has to span more than one interval.
var (
	epochSettleTimeout  = 2500 * time.Millisecond
	epochSettleInterval = 250 * time.Millisecond
)

type Registry struct {
	native  Provider
	legacy  Provider
	cache   *CatalogCache
	schemas *schema.CompilerCache
	refresh InstanceRefresher
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
	refresh := options.Refresh
	if refresh == nil {
		refresh = discoverFresh
	}
	return &Registry{
		native:  NewUnityProvider(sender),
		legacy:  NewLegacyProvider(sender),
		cache:   cache,
		schemas: schemas,
		refresh: refresh,
	}
}

func discoverFresh(instance *client.Instance) (*client.Instance, error) {
	if instance.ProjectPath != "" {
		return client.DiscoverInstanceFresh(instance.ProjectPath, 0)
	}
	return client.DiscoverInstanceFresh("", instance.Port)
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
		settled, ok := registry.awaitEpoch(ctx, instance, snapshot.Catalog.DomainEpoch)
		if !ok {
			return nil, fmt.Errorf(
				"catalog domain epoch %q does not match heartbeat epoch %q",
				snapshot.Catalog.DomainEpoch,
				instance.DomainEpoch,
			)
		}
		instance = settled
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

// awaitEpoch re-reads the heartbeat until it agrees with the epoch of the
// catalog that was just fetched live. The heartbeat is read before the catalog
// request and is not written at all while the domain reloads, so a compile
// between the two leaves the caller holding the older epoch. That is a stale
// view of a healthy Editor, not a wrong catalog, and it resolves as soon as the
// Editor writes its next heartbeat.
func (registry *Registry) awaitEpoch(
	ctx context.Context,
	instance *client.Instance,
	epoch string,
) (*client.Instance, bool) {
	if registry.refresh == nil {
		return nil, false
	}
	deadline := time.Now().Add(epochSettleTimeout)
	for {
		refreshed, err := registry.refresh(instance)
		if err != nil {
			return nil, false
		}
		if refreshed != nil && refreshed.DomainEpoch == epoch {
			return refreshed, true
		}
		if time.Now().After(deadline) {
			return nil, false
		}
		select {
		case <-ctx.Done():
			return nil, false
		case <-time.After(epochSettleInterval):
		}
	}
}
