package toolregistry

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"sync"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
)

const defaultMaxEntriesPerProject = 8

type CacheOptions struct {
	Root                 string
	MaxEntriesPerProject int
	Schemas              *schema.CompilerCache
}

type CacheKey struct {
	ProjectID   string   `json:"project_id"`
	Features    []string `json:"features"`
	DomainEpoch string   `json:"domain_epoch"`
	CatalogHash string   `json:"catalog_hash"`
}

type CatalogCache struct {
	mu         sync.Mutex
	memory     map[string]memoryEntry
	root       string
	maxEntries int
	sequence   uint64
	schemas    *schema.CompilerCache
}

type memoryEntry struct {
	key     CacheKey
	catalog *Catalog
	touched uint64
}

type cacheRecord struct {
	Key     CacheKey        `json:"key"`
	Catalog json.RawMessage `json:"catalog"`
}

func NewCatalogCache(options CacheOptions) *CatalogCache {
	root := options.Root
	if root == "" {
		root = filepath.Join(filepath.Dir(paths.InstancesDir()), "cache", "catalog")
	}
	maxEntries := options.MaxEntriesPerProject
	if maxEntries <= 0 {
		maxEntries = defaultMaxEntriesPerProject
	}
	schemas := options.Schemas
	if schemas == nil {
		schemas = schema.NewCompilerCache()
	}
	return &CatalogCache{
		memory:     make(map[string]memoryEntry),
		root:       root,
		maxEntries: maxEntries,
		schemas:    schemas,
	}
}

func (cache *CatalogCache) Store(key CacheKey, catalog *Catalog) error {
	key = normalizeCacheKey(key)
	if err := validateCacheKey(key, catalog); err != nil {
		return err
	}
	cloned, err := cloneCatalog(catalog)
	if err != nil {
		return err
	}
	if _, err := cache.schemas.Compile(cloned.CatalogHash, cloned.SchemaDefinitions()); err != nil {
		return fmt.Errorf("%w: compile catalog schemas: %w", ErrCacheInvalid, err)
	}
	catalogData, err := json.Marshal(catalog)
	if err != nil {
		return fmt.Errorf("marshal catalog cache: %w", err)
	}
	recordData, err := json.Marshal(cacheRecord{Key: key, Catalog: catalogData})
	if err != nil {
		return fmt.Errorf("marshal catalog cache record: %w", err)
	}
	cache.mu.Lock()
	defer cache.mu.Unlock()
	if err := writeAtomic(cache.entryPath(key), recordData); err != nil {
		return err
	}
	if err := cache.prune(key.ProjectID); err != nil {
		return err
	}
	cache.remember(key, cloned)
	return nil
}

func (cache *CatalogCache) Load(key CacheKey) (*Catalog, error) {
	key = normalizeCacheKey(key)
	cache.mu.Lock()
	defer cache.mu.Unlock()
	cached, ok := cache.memory[key.String()]
	if ok {
		cache.remember(cached.key, cached.catalog)
		return cloneCatalog(cached.catalog)
	}

	data, err := os.ReadFile(cache.entryPath(key))
	if errors.Is(err, os.ErrNotExist) {
		return nil, ErrCacheMiss
	}
	if err != nil {
		return nil, fmt.Errorf("read catalog cache: %w", err)
	}
	catalog, err := cache.parseCacheRecord(data, key)
	if err != nil {
		return nil, err
	}
	cache.remember(key, catalog)
	return cloneCatalog(catalog)
}

func (cache *CatalogCache) LoadMatching(lookup CacheKey) (*Catalog, CacheKey, error) {
	lookup = normalizeCacheKey(lookup)
	cache.mu.Lock()
	defer cache.mu.Unlock()
	matches := make([]memoryEntry, 0, 1)
	for _, entry := range cache.memory {
		if sameLookup(entry.key, lookup) {
			matches = append(matches, entry)
		}
	}
	slices.SortFunc(matches, func(left, right memoryEntry) int {
		return strings.Compare(left.key.CatalogHash, right.key.CatalogHash)
	})
	if len(matches) > 0 {
		cache.remember(matches[0].key, matches[0].catalog)
		cloned, err := cloneCatalog(matches[0].catalog)
		if err != nil {
			return nil, CacheKey{}, err
		}
		return cloned, matches[0].key, nil
	}

	projectDir := cache.projectDir(lookup.ProjectID)
	entries, err := os.ReadDir(projectDir)
	if errors.Is(err, os.ErrNotExist) {
		return nil, CacheKey{}, ErrCacheMiss
	}
	if err != nil {
		return nil, CacheKey{}, fmt.Errorf("read catalog cache directory: %w", err)
	}
	for _, entry := range entries {
		if entry.IsDir() || filepath.Ext(entry.Name()) != ".json" {
			continue
		}
		data, readErr := os.ReadFile(filepath.Join(projectDir, entry.Name()))
		if readErr != nil {
			continue
		}
		var record cacheRecord
		if json.Unmarshal(data, &record) != nil {
			continue
		}
		record.Key = normalizeCacheKey(record.Key)
		if !sameLookup(record.Key, lookup) {
			continue
		}
		expectedName := strings.TrimPrefix(record.Key.CatalogHash, "sha256:") + ".json"
		if entry.Name() != expectedName {
			continue
		}
		catalog, parseErr := cache.parseCacheRecord(data, record.Key)
		if parseErr != nil {
			continue
		}
		cache.remember(record.Key, catalog)
		cloned, cloneErr := cloneCatalog(catalog)
		if cloneErr != nil {
			return nil, CacheKey{}, cloneErr
		}
		return cloned, record.Key, nil
	}
	return nil, CacheKey{}, ErrCacheMiss
}

func (cache *CatalogCache) remember(key CacheKey, catalog *Catalog) {
	cache.sequence++
	cache.memory[key.String()] = memoryEntry{
		key:     key,
		catalog: catalog,
		touched: cache.sequence,
	}
	entries := make([]memoryEntry, 0, cache.maxEntries+1)
	for _, entry := range cache.memory {
		if entry.key.ProjectID == key.ProjectID {
			entries = append(entries, entry)
		}
	}
	slices.SortFunc(entries, func(left, right memoryEntry) int {
		switch {
		case left.touched > right.touched:
			return -1
		case left.touched < right.touched:
			return 1
		default:
			return strings.Compare(left.key.String(), right.key.String())
		}
	})
	if len(entries) <= cache.maxEntries {
		return
	}
	for _, entry := range entries[cache.maxEntries:] {
		delete(cache.memory, entry.key.String())
	}
}

func (key CacheKey) String() string {
	return strings.Join([]string{
		key.ProjectID,
		strings.Join(key.Features, ","),
		key.DomainEpoch,
		key.CatalogHash,
	}, "|")
}

func normalizeCacheKey(key CacheKey) CacheKey {
	key.Features = append([]string(nil), key.Features...)
	slices.Sort(key.Features)
	key.Features = slices.Compact(key.Features)
	return key
}

func validateCacheKey(key CacheKey, catalog *Catalog) error {
	if catalog == nil || key.ProjectID != catalog.ProjectID ||
		key.DomainEpoch != catalog.DomainEpoch || key.CatalogHash != catalog.CatalogHash {
		return fmt.Errorf("%w: key does not match catalog", ErrCacheInvalid)
	}
	if !sha256Pattern.MatchString(key.ProjectID) || !sha256Pattern.MatchString(key.CatalogHash) ||
		key.DomainEpoch == "" || len(key.Features) == 0 {
		return fmt.Errorf("%w: incomplete key", ErrCacheInvalid)
	}
	if slices.Contains(key.Features, "") {
		return fmt.Errorf("%w: empty feature", ErrCacheInvalid)
	}
	return nil
}

func sameLookup(key, lookup CacheKey) bool {
	return key.ProjectID == lookup.ProjectID &&
		key.DomainEpoch == lookup.DomainEpoch &&
		slices.Equal(key.Features, lookup.Features)
}

func (cache *CatalogCache) parseCacheRecord(data []byte, key CacheKey) (*Catalog, error) {
	var record cacheRecord
	if err := decodeSingleStrict(data, &record); err != nil {
		return nil, fmt.Errorf("%w: decode record: %v", ErrCacheInvalid, err)
	}
	record.Key = normalizeCacheKey(record.Key)
	if record.Key.ProjectID == key.ProjectID &&
		record.Key.CatalogHash == key.CatalogHash &&
		!sameLookup(record.Key, key) {
		return nil, ErrCacheMiss
	}
	if record.Key.String() != key.String() {
		return nil, fmt.Errorf("%w: cache key mismatch", ErrCacheInvalid)
	}
	catalog, err := ParseCatalog(record.Catalog)
	if err != nil {
		return nil, fmt.Errorf("%w: %v", ErrCacheInvalid, err)
	}
	if err := validateCacheKey(record.Key, catalog); err != nil {
		return nil, err
	}
	if _, err := cache.schemas.Compile(catalog.CatalogHash, catalog.SchemaDefinitions()); err != nil {
		return nil, fmt.Errorf("%w: compile catalog schemas: %w", ErrCacheInvalid, err)
	}
	return catalog, nil
}

func cloneCatalog(catalog *Catalog) (*Catalog, error) {
	data, err := json.Marshal(catalog)
	if err != nil {
		return nil, fmt.Errorf("clone catalog: %w", err)
	}
	cloned, err := ParseCatalog(data)
	if err != nil {
		return nil, fmt.Errorf("clone catalog: %w", err)
	}
	return cloned, nil
}
