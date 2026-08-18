package toolregistry

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

func TestRegistry_Load_reuses_valid_disk_cache_in_second_instance(t *testing.T) {
	// Given
	projectPath := "/projects/current"
	raw := validFixtureForProject(t, projectPath)
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"catalog":true,"schema_version":"hera.tool-catalog/1"}`: {
			Success: true,
			Data:    raw,
		},
	}}
	cacheRoot := t.TempDir()
	instance := &client.Instance{
		ProjectPath: projectPath,
		Port:        8090,
		DomainEpoch: "domain-1",
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
	}
	first := NewRegistry(RegistryOptions{
		Sender:  sender,
		Cache:   NewCatalogCache(CacheOptions{Root: cacheRoot}),
		Schemas: schema.NewCompilerCache(),
	})
	if _, err := first.Load(context.Background(), instance); err != nil {
		t.Fatalf("prime registry: %v", err)
	}
	second := NewRegistry(RegistryOptions{
		Sender:  failingSender{},
		Cache:   NewCatalogCache(CacheOptions{Root: cacheRoot}),
		Schemas: schema.NewCompilerCache(),
	})

	// When
	snapshot, err := second.Load(context.Background(), instance)

	// Then
	if err != nil {
		t.Fatalf("load cached registry: %v", err)
	}
	if !snapshot.FromCache {
		t.Fatal("FromCache = false, want true")
	}
	if snapshot.Schemas == nil {
		t.Fatal("compiled schemas are nil")
	}
}

func TestRegistry_Load_uses_legacy_provider_without_catalog_capability(t *testing.T) {
	// Given
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"names":true}`: {
			Success: true,
			Data:    json.RawMessage(`["scene"]`),
		},
		`list:{"tool":"scene"}`: {
			Success: true,
			Data: json.RawMessage(`{
				"name":"scene",
				"description":"Scene tools",
				"actions":[],
				"schema":{"type":"object","properties":{}},
				"output_schema":{"type":"object","properties":{}}
			}`),
		},
	}}
	registry := NewRegistry(RegistryOptions{
		Sender:  sender,
		Cache:   NewCatalogCache(CacheOptions{Root: t.TempDir()}),
		Schemas: schema.NewCompilerCache(),
	})

	// When
	snapshot, err := registry.Load(context.Background(), &client.Instance{
		ProjectPath: "/projects/current",
		Port:        8090,
	})

	// Then
	if err != nil {
		t.Fatalf("load legacy registry: %v", err)
	}
	if snapshot.Exposure != ExposureCompactOnly || snapshot.Schemas != nil {
		t.Fatalf("snapshot = %#v", snapshot)
	}
}

func TestRegistry_Load_uses_live_catalog_when_disk_cache_is_unavailable(t *testing.T) {
	// Given
	projectPath := "/projects/current"
	raw := validFixtureForProject(t, projectPath)
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"catalog":true,"schema_version":"hera.tool-catalog/1"}`: {
			Success: true,
			Data:    raw,
		},
	}}
	cacheRoot := filepath.Join(t.TempDir(), "not-a-directory")
	if err := os.WriteFile(cacheRoot, []byte("blocked"), 0o600); err != nil {
		t.Fatalf("create cache blocker: %v", err)
	}
	registry := NewRegistry(RegistryOptions{
		Sender:  sender,
		Cache:   NewCatalogCache(CacheOptions{Root: cacheRoot}),
		Schemas: schema.NewCompilerCache(),
	})
	instance := &client.Instance{
		ProjectPath: projectPath,
		Port:        8090,
		DomainEpoch: "domain-1",
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
	}

	// When
	snapshot, err := registry.Load(context.Background(), instance)

	// Then
	if err != nil {
		t.Fatalf("load live registry with unavailable cache: %v", err)
	}
	if snapshot.FromCache {
		t.Fatal("FromCache = true, want false")
	}
	if snapshot.Schemas == nil {
		t.Fatal("compiled schemas are nil")
	}
}

type failingSender struct{}

func (failingSender) Send(
	context.Context,
	*client.Instance,
	string,
	any,
	int,
) (*client.CommandResponse, error) {
	return nil, fmt.Errorf("sender must not be called")
}

func validFixtureForProject(t *testing.T, projectPath string) []byte {
	t.Helper()
	raw := validFixture(t)
	var document map[string]json.RawMessage
	if err := json.Unmarshal(raw, &document); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
	projectID, err := ProjectID(projectPath)
	if err != nil {
		t.Fatalf("project id: %v", err)
	}
	document["project_id"], err = json.Marshal(projectID)
	if err != nil {
		t.Fatalf("encode project id: %v", err)
	}
	hash, err := computeCatalogHash(document)
	if err != nil {
		t.Fatalf("compute catalog hash: %v", err)
	}
	document["catalog_hash"], err = json.Marshal(hash)
	if err != nil {
		t.Fatalf("encode catalog hash: %v", err)
	}
	raw, err = json.Marshal(document)
	if err != nil {
		t.Fatalf("encode fixture: %v", err)
	}
	return raw
}

func TestRegistry_Load_settles_a_heartbeat_epoch_left_behind_by_a_reload(t *testing.T) {
	// Given: the heartbeat was read before a compile and the live catalog now
	// answers with the epoch of the domain that replaced it.
	projectPath := "/projects/current"
	raw := validFixtureForProject(t, projectPath)
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"catalog":true,"schema_version":"hera.tool-catalog/1"}`: {
			Success: true,
			Data:    raw,
		},
	}}
	stale := &client.Instance{
		ProjectPath: projectPath,
		Port:        8090,
		DomainEpoch: "domain-before-reload",
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
	}
	refreshes := 0
	registry := NewRegistry(RegistryOptions{
		Sender:  sender,
		Cache:   NewCatalogCache(CacheOptions{Root: t.TempDir()}),
		Schemas: schema.NewCompilerCache(),
		Refresh: func(instance *client.Instance) (*client.Instance, error) {
			refreshes++
			settled := *instance
			// The Editor writes its post-reload heartbeat on the second read.
			if refreshes > 1 {
				settled.DomainEpoch = "domain-1"
			}
			return &settled, nil
		},
	})

	// When
	snapshot, err := registry.Load(context.Background(), stale)

	// Then: the stale view resolves instead of failing the first call after a compile.
	if err != nil {
		t.Fatalf("load after reload: %v", err)
	}
	if snapshot == nil || snapshot.Catalog.DomainEpoch != "domain-1" {
		t.Fatalf("snapshot = %#v, want catalog epoch domain-1", snapshot)
	}
	if refreshes < 2 {
		t.Fatalf("refreshes = %d, want the heartbeat re-read until it caught up", refreshes)
	}
}

func TestRegistry_Load_still_rejects_an_epoch_that_never_catches_up(t *testing.T) {
	// Given: the heartbeat keeps reporting a different domain than the catalog.
	projectPath := "/projects/current"
	raw := validFixtureForProject(t, projectPath)
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"catalog":true,"schema_version":"hera.tool-catalog/1"}`: {
			Success: true,
			Data:    raw,
		},
	}}
	restore := epochSettleTimeout
	epochSettleTimeout = 0
	t.Cleanup(func() { epochSettleTimeout = restore })
	registry := NewRegistry(RegistryOptions{
		Sender:  sender,
		Cache:   NewCatalogCache(CacheOptions{Root: t.TempDir()}),
		Schemas: schema.NewCompilerCache(),
		Refresh: func(instance *client.Instance) (*client.Instance, error) {
			settled := *instance
			return &settled, nil
		},
	})
	instance := &client.Instance{
		ProjectPath: projectPath,
		Port:        8090,
		DomainEpoch: "domain-other",
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
	}

	// When
	_, err := registry.Load(context.Background(), instance)

	// Then
	if err == nil || !strings.Contains(err.Error(), "does not match heartbeat epoch") {
		t.Fatalf("err = %v, want the epoch mismatch to survive", err)
	}
}
