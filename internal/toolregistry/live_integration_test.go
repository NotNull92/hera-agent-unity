//go:build integration

package toolregistry

import (
	"context"
	"os"
	"slices"
	"strconv"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

func TestRegistry_live_catalog_compiles_all_strict_schemas(t *testing.T) {
	// Given
	port := 0
	if rawPort := os.Getenv("HERA_AGENT_INTEGRATION_PORT"); rawPort != "" {
		parsed, err := strconv.Atoi(rawPort)
		if err != nil {
			t.Fatalf("parse HERA_AGENT_INTEGRATION_PORT: %v", err)
		}
		port = parsed
	}
	instance, err := client.DiscoverInstance("", port)
	if err != nil {
		t.Fatalf("discover Unity: %v", err)
	}
	registry := NewRegistry(RegistryOptions{
		Cache:   NewCatalogCache(CacheOptions{Root: t.TempDir()}),
		Schemas: schema.NewCompilerCache(),
	})

	// When
	snapshot, err := registry.Load(context.Background(), instance)

	// Then
	if err != nil {
		t.Fatalf("load live registry: %v", err)
	}
	hasNativeCatalog := slices.Contains(instance.Features, FeatureToolCatalogV1) &&
		slices.Contains(instance.Features, FeatureDomainEpochV1)
	if !hasNativeCatalog {
		if snapshot.Exposure != ExposureCompactOnly || snapshot.Schemas != nil {
			t.Fatalf("legacy snapshot = %#v", snapshot)
		}
		if len(snapshot.Catalog.Tools) == 0 {
			t.Fatal("legacy catalog has no tools")
		}
		return
	}
	if snapshot.Exposure != ExposureProfile || snapshot.Schemas == nil {
		t.Fatalf("snapshot = %#v", snapshot)
	}
	expectedTools := 31
	if rawExpected := os.Getenv("HERA_AGENT_EXPECTED_NATIVE_TOOLS"); rawExpected != "" {
		parsed, err := strconv.Atoi(rawExpected)
		if err != nil {
			t.Fatalf("parse HERA_AGENT_EXPECTED_NATIVE_TOOLS: %v", err)
		}
		expectedTools = parsed
	}
	if len(snapshot.Catalog.Tools) < expectedTools {
		t.Fatalf("tools = %d, want at least %d", len(snapshot.Catalog.Tools), expectedTools)
	}
	actions := 0
	strict := 0
	for _, tool := range snapshot.Catalog.Tools {
		actions += len(tool.Actions)
		if tool.ContractMode == ContractStrict {
			strict++
		}
	}
	if strict < expectedTools {
		t.Fatalf("strict tools = %d, want at least %d", strict, expectedTools)
	}
	if actions < 75 {
		t.Fatalf("actions = %d, want at least 75", actions)
	}
}
