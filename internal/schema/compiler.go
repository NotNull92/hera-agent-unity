package schema

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"net/url"
	"strings"
	"sync"

	jsonschema "github.com/santhosh-tekuri/jsonschema/v6"
	"github.com/santhosh-tekuri/jsonschema/v6/kind"
)

const defaultMaxCompiledCatalogs = 8

// Definition is one named JSON Schema in a catalog.
type Definition struct {
	Key    string
	Schema json.RawMessage
}

// CompilerCache compiles and reuses immutable schema sets by catalog hash.
type CompilerCache struct {
	mu          sync.Mutex
	catalogs    map[string]compiledEntry
	maxCatalogs int
	sequence    uint64
}

type compiledEntry struct {
	catalog *CompiledCatalog
	touched uint64
}

// CompiledCatalog contains all schemas compiled from one catalog hash.
type CompiledCatalog struct {
	hash    string
	schemas map[string]*jsonschema.Schema
}

func NewCompilerCache() *CompilerCache {
	return newCompilerCache(defaultMaxCompiledCatalogs)
}

func newCompilerCache(maxCatalogs int) *CompilerCache {
	return &CompilerCache{
		catalogs:    make(map[string]compiledEntry),
		maxCatalogs: maxCatalogs,
	}
}

func (cache *CompilerCache) Compile(hash string, definitions []Definition) (*CompiledCatalog, error) {
	cache.mu.Lock()
	if cached, ok := cache.catalogs[hash]; ok {
		cache.remember(hash, cached.catalog)
		cache.mu.Unlock()
		return cached.catalog, nil
	}
	cache.mu.Unlock()

	compiled, err := compileCatalog(hash, definitions)
	if err != nil {
		return nil, err
	}

	cache.mu.Lock()
	defer cache.mu.Unlock()
	if cached, ok := cache.catalogs[hash]; ok {
		cache.remember(hash, cached.catalog)
		return cached.catalog, nil
	}
	cache.remember(hash, compiled)
	return compiled, nil
}

func (cache *CompilerCache) remember(hash string, catalog *CompiledCatalog) {
	cache.sequence++
	cache.catalogs[hash] = compiledEntry{catalog: catalog, touched: cache.sequence}
	if len(cache.catalogs) <= cache.maxCatalogs {
		return
	}
	var victimHash string
	var victimTouched uint64
	for candidateHash, candidate := range cache.catalogs {
		if victimHash == "" || candidate.touched < victimTouched ||
			(candidate.touched == victimTouched && candidateHash < victimHash) {
			victimHash = candidateHash
			victimTouched = candidate.touched
		}
	}
	delete(cache.catalogs, victimHash)
}

func compileCatalog(hash string, definitions []Definition) (*CompiledCatalog, error) {
	compiled := &CompiledCatalog{
		hash:    hash,
		schemas: make(map[string]*jsonschema.Schema, len(definitions)),
	}
	for _, definition := range definitions {
		if definition.Key == "" {
			return nil, fmt.Errorf("compile catalog %q: schema key is required", hash)
		}
		if _, exists := compiled.schemas[definition.Key]; exists {
			return nil, fmt.Errorf("compile catalog %q: duplicate schema key %q", hash, definition.Key)
		}

		document, err := jsonschema.UnmarshalJSON(bytes.NewReader(definition.Schema))
		if err != nil {
			return nil, fmt.Errorf("decode schema %q: %w", definition.Key, err)
		}
		compiler := jsonschema.NewCompiler()
		compiler.DefaultDraft(jsonschema.Draft2020)
		compiler.AssertFormat()
		location := "urn:hera:schema:" + url.PathEscape(definition.Key)
		if err := compiler.AddResource(location, document); err != nil {
			return nil, fmt.Errorf("add schema %q: %w", definition.Key, err)
		}
		schema, err := compiler.Compile(location)
		if err != nil {
			return nil, fmt.Errorf("compile schema %q: %w", definition.Key, err)
		}
		compiled.schemas[definition.Key] = schema
	}
	return compiled, nil
}

func (catalog *CompiledCatalog) Validate(key string, value any) error {
	compiled, ok := catalog.schemas[key]
	if !ok {
		return fmt.Errorf("schema %q not found in catalog %q", key, catalog.hash)
	}
	if err := compiled.Validate(value); err != nil {
		var validation *jsonschema.ValidationError
		if !errors.As(err, &validation) {
			return fmt.Errorf("validate schema %q: %w", key, err)
		}
		return &ValidationError{
			Key:     key,
			Pointer: instancePointer(validation),
			Cause:   err,
		}
	}
	return nil
}

func instancePointer(validation *jsonschema.ValidationError) string {
	leaf := validation
	for len(leaf.Causes) > 0 {
		leaf = leaf.Causes[0]
	}
	segments := append([]string(nil), leaf.InstanceLocation...)
	if additional, ok := leaf.ErrorKind.(*kind.AdditionalProperties); ok && len(additional.Properties) == 1 {
		segments = append(segments, additional.Properties[0])
	}
	if len(segments) == 0 {
		return ""
	}
	for index, segment := range segments {
		segments[index] = strings.ReplaceAll(strings.ReplaceAll(segment, "~", "~0"), "/", "~1")
	}
	return "/" + strings.Join(segments, "/")
}
