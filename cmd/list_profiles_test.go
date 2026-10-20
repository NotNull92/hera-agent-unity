package cmd

import (
	"context"
	"encoding/json"
	"fmt"
	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"os"
	"testing"
)

func TestListProfiles(t *testing.T) {
	catalog := &toolregistry.Catalog{Tools: []toolregistry.Tool{
		{Name: "z", Description: "Z", Profiles: []string{"core", "full"}},
		{Name: "a", Description: "A", Profiles: []string{"core"}},
		{Name: "exec", Description: "Code", Profiles: []string{"advanced"}},
		{Name: "legacy", Description: "Old", ContractMode: "legacy", Profiles: []string{"compact"}},
	}}
	load := func(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
		return &toolregistry.Snapshot{Catalog: catalog}, nil
	}
	for _, tc := range []struct {
		args    []string
		want    string
		success bool
	}{
		{[]string{"--profiles"}, `{"advanced":1,"compact":1,"core":2,"full":1}`, true},
		{[]string{"--profile", "core"}, `[{"name":"a","description":"A"},{"name":"z","description":"Z"}]`, true},
		{[]string{"--profile=advanced"}, `[{"name":"exec","description":"Code"}]`, true},
		{[]string{"--profile", "compact"}, `[{"name":"legacy","description":"Old"}]`, true},
		{[]string{"--profile", "absent"}, `{"profile":"absent","valid_profiles":["advanced","compact","core","full"]}`, false},
	} {
		r, e := listProfilesCmd(context.Background(), nil, tc.args, load)
		if e != nil || r.Success != tc.success || string(r.Data) != tc.want {
			t.Fatalf("%v: %#v %v", tc.args, r, e)
		}
		if !tc.success && r.Code != "INVALID_ARGUMENT" {
			t.Fatal(r.Code)
		}
	}
	for _, args := range [][]string{{"--profile"}, {"--profile", ""}, {"--profiles", "--names"}, {"--profile", "core", "--profiles"}, {"--profiles=false"}} {
		r, e := listProfilesCmd(context.Background(), nil, args, func(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
			t.Fatal("invalid arguments loaded catalog")
			return nil, nil
		})
		if e != nil || r.Code != "INVALID_ARGUMENT" {
			t.Fatalf("%v %#v %v", args, r, e)
		}
	}
}

type noProfileFetch struct{ t *testing.T }

func (s noProfileFetch) Send(context.Context, *client.Instance, string, any, int) (*client.CommandResponse, error) {
	s.t.Error("cache hit fetched Unity")
	return nil, fmt.Errorf("unexpected fetch")
}
func TestListProfileUsesRegistryDiskCache(t *testing.T) {
	raw, err := os.ReadFile("../internal/toolregistry/testdata/catalog-v1.json")
	if err != nil {
		t.Fatal(err)
	}
	catalog, err := toolregistry.ParseCatalog(raw)
	if err != nil {
		t.Fatal(err)
	}
	project := t.TempDir()
	catalog.ProjectID, err = toolregistry.ProjectID(project)
	if err != nil {
		t.Fatal(err)
	}
	features := []string{toolregistry.FeatureToolCatalogV1, toolregistry.FeatureDomainEpochV1}
	root := t.TempDir()
	cache := toolregistry.NewCatalogCache(toolregistry.CacheOptions{Root: root})
	key := toolregistry.CacheKey{ProjectID: catalog.ProjectID, DomainEpoch: catalog.DomainEpoch, CatalogHash: catalog.CatalogHash, Features: features}
	if err = cache.Store(key, catalog); err != nil {
		t.Fatal(err)
	}
	registry := toolregistry.NewRegistry(toolregistry.RegistryOptions{Sender: noProfileFetch{t}, Cache: toolregistry.NewCatalogCache(toolregistry.CacheOptions{Root: root})})
	inst := &client.Instance{ProjectPath: project, DomainEpoch: catalog.DomainEpoch, CatalogSchemaHash: catalog.CatalogHash, Features: features}
	r, err := listProfilesCmd(context.Background(), inst, []string{"--profile", "core"}, registry.Load)
	if err != nil || !r.Success {
		t.Fatalf("%#v %v", r, err)
	}
	var rows []map[string]any
	if err = json.Unmarshal(r.Data, &rows); err != nil {
		t.Fatal(err)
	}
	if len(rows) != 1 || rows[0]["name"] != "scene" || len(rows[0]) != 2 {
		t.Fatal(rows)
	}
}
