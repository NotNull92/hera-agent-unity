package toolregistry

import (
	"encoding/json"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"slices"
	"sync"
	"testing"
)

func TestParseCatalog_accepts_valid_catalog_v1(t *testing.T) {
	// Given
	raw := validFixture(t)

	// When
	catalog, err := ParseCatalog(raw)

	// Then
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	if catalog.SchemaVersion != CatalogSchemaV1 {
		t.Fatalf("schema = %q", catalog.SchemaVersion)
	}
	if len(catalog.Tools) != 1 || catalog.Tools[0].Name != "scene" {
		t.Fatalf("tools = %#v", catalog.Tools)
	}
}

func TestParseCatalog_rejects_hash_mismatch(t *testing.T) {
	// Given
	raw := validFixture(t)
	var document map[string]json.RawMessage
	if err := json.Unmarshal(raw, &document); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
	document["catalog_hash"] = json.RawMessage(
		`"sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"`,
	)
	raw, err := json.Marshal(document)
	if err != nil {
		t.Fatalf("encode fixture: %v", err)
	}

	// When
	_, err = ParseCatalog(raw)

	// Then
	if !errors.Is(err, ErrCatalogHashMismatch) {
		t.Fatalf("error = %v, want ErrCatalogHashMismatch", err)
	}
}

func TestCatalog_ToolsForProfile_is_deterministic(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}

	// When
	tools, err := catalog.ToolsForProfile("core")

	// Then
	if err != nil {
		t.Fatalf("select profile: %v", err)
	}
	if !slices.Equal(toolNames(tools), []string{"scene"}) {
		t.Fatalf("tools = %v", toolNames(tools))
	}
}

func TestCatalogCache_survives_second_process(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	options := CacheOptions{Root: t.TempDir(), MaxEntriesPerProject: 2}
	first := NewCatalogCache(options)
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	if err := first.Store(key, catalog); err != nil {
		t.Fatalf("store cache: %v", err)
	}
	keyData, err := json.Marshal(key)
	if err != nil {
		t.Fatalf("encode cache key: %v", err)
	}

	// When
	command := exec.Command(os.Args[0], "-test.run=^TestCatalogCache_process_reader$")
	command.Env = append(os.Environ(),
		"HERA_CACHE_PROCESS_HELPER=1",
		"HERA_CACHE_ROOT="+options.Root,
		"HERA_CACHE_KEY="+string(keyData),
	)
	output, err := command.CombinedOutput()

	// Then
	if err != nil {
		t.Fatalf("load cache in second process: %v\n%s", err, output)
	}
}

func TestCatalogCache_LoadMatching_uses_memory_cache(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir()})
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	if err := cache.Store(key, catalog); err != nil {
		t.Fatalf("store cache: %v", err)
	}
	if err := os.Remove(cache.entryPath(key)); err != nil {
		t.Fatalf("remove disk entry: %v", err)
	}

	// When
	got, gotKey, err := cache.LoadMatching(CacheKey{
		ProjectID:   key.ProjectID,
		Features:    key.Features,
		DomainEpoch: key.DomainEpoch,
	})

	// Then
	if err != nil {
		t.Fatalf("load matching cache: %v", err)
	}
	if got.CatalogHash != catalog.CatalogHash || gotKey.String() != key.String() {
		t.Fatalf("got key/catalog = %#v / %q", gotKey, got.CatalogHash)
	}
}

func TestCatalogCache_process_reader(t *testing.T) {
	if os.Getenv("HERA_CACHE_PROCESS_HELPER") != "1" {
		t.Skip("subprocess helper")
	}
	var key CacheKey
	if err := json.Unmarshal([]byte(os.Getenv("HERA_CACHE_KEY")), &key); err != nil {
		t.Fatalf("decode cache key: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: os.Getenv("HERA_CACHE_ROOT")})
	catalog, err := cache.Load(key)
	if err != nil {
		t.Fatalf("load cache: %v", err)
	}
	if catalog.CatalogHash != key.CatalogHash {
		t.Fatalf("hash = %q, want %q", catalog.CatalogHash, key.CatalogHash)
	}
}

func TestCatalogCache_rejects_stale_domain_epoch(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir(), MaxEntriesPerProject: 2})
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	if err := cache.Store(key, catalog); err != nil {
		t.Fatalf("store cache: %v", err)
	}
	key.DomainEpoch = "domain-2"

	// When
	_, err = cache.Load(key)

	// Then
	if !errors.Is(err, ErrCacheMiss) {
		t.Fatalf("error = %v, want ErrCacheMiss", err)
	}
}

func TestCatalogCache_rejects_corrupt_entry(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir(), MaxEntriesPerProject: 2})
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	if err := cache.Store(key, catalog); err != nil {
		t.Fatalf("store cache: %v", err)
	}
	if err := os.WriteFile(cache.entryPath(key), []byte("{"), 0o600); err != nil {
		t.Fatalf("corrupt cache fixture: %v", err)
	}
	cache = NewCatalogCache(CacheOptions{
		Root:                 cache.root,
		MaxEntriesPerProject: cache.maxEntries,
	})

	// When
	_, err = cache.Load(key)

	// Then
	if !errors.Is(err, ErrCacheInvalid) {
		t.Fatalf("error = %v, want ErrCacheInvalid", err)
	}
}

func TestCatalogCache_rejects_invalid_schema_on_store_and_load(t *testing.T) {
	// Given
	catalog := catalogWithInputSchema(t, json.RawMessage(`{"type":7}`))
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir()})

	// When
	storeErr := cache.Store(key, catalog)
	recordData, err := json.Marshal(cacheRecord{Key: key, Catalog: mustMarshal(t, catalog)})
	if err != nil {
		t.Fatalf("encode invalid cache record: %v", err)
	}
	if err := os.MkdirAll(cache.projectDir(key.ProjectID), 0o700); err != nil {
		t.Fatalf("create cache directory: %v", err)
	}
	if err := os.WriteFile(cache.entryPath(key), recordData, 0o600); err != nil {
		t.Fatalf("write invalid cache record: %v", err)
	}
	_, loadErr := NewCatalogCache(CacheOptions{Root: cache.root}).Load(key)

	// Then
	if !errors.Is(storeErr, ErrCacheInvalid) {
		t.Fatalf("store error = %v, want ErrCacheInvalid", storeErr)
	}
	if !errors.Is(loadErr, ErrCacheInvalid) {
		t.Fatalf("load error = %v, want ErrCacheInvalid", loadErr)
	}
}

func TestCatalogCache_LoadMatching_rejects_misnamed_entry(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir()})
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}
	if err := cache.Store(key, catalog); err != nil {
		t.Fatalf("store cache: %v", err)
	}
	if err := os.Rename(
		cache.entryPath(key),
		filepath.Join(cache.projectDir(key.ProjectID), "misnamed.json"),
	); err != nil {
		t.Fatalf("rename cache entry: %v", err)
	}
	cache = NewCatalogCache(CacheOptions{Root: cache.root})

	// When
	_, _, err = cache.LoadMatching(CacheKey{
		ProjectID:   key.ProjectID,
		Features:    key.Features,
		DomainEpoch: key.DomainEpoch,
	})

	// Then
	if !errors.Is(err, ErrCacheMiss) {
		t.Fatalf("error = %v, want ErrCacheMiss", err)
	}
}

func TestCatalogCache_rejects_empty_feature(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir()})

	// When
	err = cache.Store(CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{""},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}, catalog)

	// Then
	if !errors.Is(err, ErrCacheInvalid) {
		t.Fatalf("error = %v, want ErrCacheInvalid", err)
	}
}

func TestCatalogCache_bounds_entries_per_project(t *testing.T) {
	// Given
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir(), MaxEntriesPerProject: 2})
	var latest CacheKey
	for _, description := range []string{"one", "two", "three"} {
		catalog := catalogVariant(t, description)
		latest = CacheKey{
			ProjectID:   catalog.ProjectID,
			Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
			DomainEpoch: catalog.DomainEpoch,
			CatalogHash: catalog.CatalogHash,
		}
		if err := cache.Store(latest, catalog); err != nil {
			t.Fatalf("store %s: %v", description, err)
		}
	}

	// When
	entries, err := os.ReadDir(cache.projectDir(latest.ProjectID))

	// Then
	if err != nil {
		t.Fatalf("read cache directory: %v", err)
	}
	if len(entries) != 2 {
		t.Fatalf("entries = %d, want 2", len(entries))
	}
	if len(cache.memory) != 2 {
		t.Fatalf("memory entries = %d, want 2", len(cache.memory))
	}
}

func TestCatalogCache_concurrent_store_and_load_is_safe(t *testing.T) {
	// Given
	catalog, err := ParseCatalog(validFixture(t))
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	cache := NewCatalogCache(CacheOptions{Root: t.TempDir(), MaxEntriesPerProject: 2})
	key := CacheKey{
		ProjectID:   catalog.ProjectID,
		Features:    []string{FeatureDomainEpochV1, FeatureToolCatalogV1},
		DomainEpoch: catalog.DomainEpoch,
		CatalogHash: catalog.CatalogHash,
	}

	// When
	var wait sync.WaitGroup
	errs := make(chan error, 16)
	for range 8 {
		wait.Add(2)
		go func() {
			defer wait.Done()
			errs <- cache.Store(key, catalog)
		}()
		go func() {
			defer wait.Done()
			_, loadErr := cache.Load(key)
			if errors.Is(loadErr, ErrCacheMiss) {
				loadErr = nil
			}
			errs <- loadErr
		}()
	}
	wait.Wait()
	close(errs)

	// Then
	for err := range errs {
		if err != nil {
			t.Fatalf("concurrent cache operation: %v", err)
		}
	}
}

func validFixture(t *testing.T) []byte {
	t.Helper()
	raw, err := os.ReadFile(filepath.Join("testdata", "catalog-v1.json"))
	if err != nil {
		t.Fatalf("read fixture: %v", err)
	}
	var document map[string]json.RawMessage
	if err := json.Unmarshal(raw, &document); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
	hash, err := computeCatalogHash(document)
	if err != nil {
		t.Fatalf("compute fixture hash: %v", err)
	}
	document["catalog_hash"], err = json.Marshal(hash)
	if err != nil {
		t.Fatalf("encode fixture hash: %v", err)
	}
	raw, err = json.Marshal(document)
	if err != nil {
		t.Fatalf("encode fixture: %v", err)
	}
	return raw
}

func toolNames(tools []Tool) []string {
	names := make([]string, len(tools))
	for index, tool := range tools {
		names[index] = tool.Name
	}
	return names
}

func catalogVariant(t *testing.T, description string) *Catalog {
	t.Helper()
	raw := validFixture(t)
	var document map[string]json.RawMessage
	if err := json.Unmarshal(raw, &document); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
	var tools []map[string]json.RawMessage
	if err := json.Unmarshal(document["tools"], &tools); err != nil {
		t.Fatalf("decode tools: %v", err)
	}
	tools[0]["description"], _ = json.Marshal(description)
	document["tools"], _ = json.Marshal(tools)
	hash, err := computeCatalogHash(document)
	if err != nil {
		t.Fatalf("compute variant hash: %v", err)
	}
	document["catalog_hash"], _ = json.Marshal(hash)
	data, _ := json.Marshal(document)
	catalog, err := ParseCatalog(data)
	if err != nil {
		t.Fatalf("parse variant: %v", err)
	}
	return catalog
}

func catalogWithInputSchema(t *testing.T, inputSchema json.RawMessage) *Catalog {
	t.Helper()
	raw := validFixture(t)
	var document map[string]json.RawMessage
	if err := json.Unmarshal(raw, &document); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
	var tools []map[string]json.RawMessage
	if err := json.Unmarshal(document["tools"], &tools); err != nil {
		t.Fatalf("decode tools: %v", err)
	}
	tools[0]["input_schema"] = inputSchema
	document["tools"] = mustMarshal(t, tools)
	hash, err := computeCatalogHash(document)
	if err != nil {
		t.Fatalf("compute catalog hash: %v", err)
	}
	document["catalog_hash"] = mustMarshal(t, hash)
	data := mustMarshal(t, document)
	catalog, err := ParseCatalog(data)
	if err != nil {
		t.Fatalf("parse catalog: %v", err)
	}
	return catalog
}

func mustMarshal(t *testing.T, value any) json.RawMessage {
	t.Helper()
	data, err := json.Marshal(value)
	if err != nil {
		t.Fatalf("marshal fixture: %v", err)
	}
	return data
}
