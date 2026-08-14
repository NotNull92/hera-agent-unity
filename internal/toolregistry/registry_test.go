package toolregistry

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
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
