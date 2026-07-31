package client

import (
	"encoding/json"
	"slices"
	"testing"
)

func TestInstance_UnmarshalHeartbeatCapabilities_whenCatalogFeaturePresent(t *testing.T) {
	// Given
	raw := []byte(`{
		"state":"ready",
		"projectPath":"/projects/current",
		"port":8090,
		"pid":100,
		"domainEpoch":"domain-1",
		"features":["domain_epoch_v1","tool_catalog_v1"]
	}`)

	// When
	var instance Instance
	err := json.Unmarshal(raw, &instance)

	// Then
	if err != nil {
		t.Fatalf("unmarshal heartbeat: %v", err)
	}
	if instance.DomainEpoch != "domain-1" {
		t.Fatalf("DomainEpoch = %q, want %q", instance.DomainEpoch, "domain-1")
	}
	if !slices.Equal(instance.Features, []string{"domain_epoch_v1", "tool_catalog_v1"}) {
		t.Fatalf("Features = %v", instance.Features)
	}
}
