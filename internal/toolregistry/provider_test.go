package toolregistry

import (
	"context"
	"encoding/json"
	"fmt"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

type fakeSender struct {
	responses map[string]*client.CommandResponse
	calls     []string
}

func (sender *fakeSender) Send(
	_ context.Context,
	_ *client.Instance,
	command string,
	params any,
	_ int,
) (*client.CommandResponse, error) {
	keyBytes, err := json.Marshal(params)
	if err != nil {
		return nil, fmt.Errorf("marshal fake key: %w", err)
	}
	key := command + ":" + string(keyBytes)
	sender.calls = append(sender.calls, key)
	response, ok := sender.responses[key]
	if !ok {
		return nil, fmt.Errorf("unexpected request %s", key)
	}
	return response, nil
}

func TestUnityProvider_Load_requests_catalog_v1(t *testing.T) {
	// Given
	raw := validFixture(t)
	sender := &fakeSender{responses: map[string]*client.CommandResponse{
		`list:{"catalog":true,"schema_version":"hera.tool-catalog/1"}`: {
			Success: true,
			Data:    raw,
		},
	}}
	provider := NewUnityProvider(sender)

	// When
	snapshot, err := provider.Load(context.Background(), &client.Instance{
		Port:        8090,
		ProjectPath: "/projects/current",
	})

	// Then
	if err != nil {
		t.Fatalf("load catalog: %v", err)
	}
	if snapshot.Catalog.SchemaVersion != CatalogSchemaV1 {
		t.Fatalf("schema = %q", snapshot.Catalog.SchemaVersion)
	}
	if len(sender.calls) != 1 {
		t.Fatalf("calls = %v", sender.calls)
	}
}

func TestLegacyProvider_Load_enters_compact_only_mode(t *testing.T) {
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
				"group":"scene",
				"groups":["scene"],
				"examples":[],
				"actions":[{"name":"info","description":"Inspect scene"}],
				"schema":{"type":"object","properties":{}},
				"output_schema":{"type":"object","properties":{}},
				"metadata":{}
			}`),
		},
	}}
	provider := NewLegacyProvider(sender)

	// When
	snapshot, err := provider.Load(context.Background(), &client.Instance{
		Port:        8090,
		ProjectPath: "/projects/current",
	})

	// Then
	if err != nil {
		t.Fatalf("load legacy catalog: %v", err)
	}
	if snapshot.Exposure != ExposureCompactOnly {
		t.Fatalf("exposure = %q, want %q", snapshot.Exposure, ExposureCompactOnly)
	}
	if len(snapshot.Catalog.Tools) != 1 {
		t.Fatalf("tools = %d, want 1", len(snapshot.Catalog.Tools))
	}
	tool := snapshot.Catalog.Tools[0]
	if tool.ContractMode != ContractLegacy || !tool.Safety.RequiresConfirmation || !tool.Safety.Destructive {
		t.Fatalf("legacy safety = %#v", tool.Safety)
	}
}
