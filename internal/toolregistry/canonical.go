package toolregistry

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
)

func computeCatalogHash(document map[string]json.RawMessage) (string, error) {
	schemaVersionRaw, ok := document["schema_version"]
	if !ok {
		return "", fmt.Errorf("catalog schema_version is required")
	}
	toolsRaw, ok := document["tools"]
	if !ok {
		return "", fmt.Errorf("catalog tools are required")
	}
	schemaVersion, err := decodeCanonicalValue(schemaVersionRaw)
	if err != nil {
		return "", fmt.Errorf("decode catalog schema_version: %w", err)
	}
	tools, err := decodeCanonicalValue(toolsRaw)
	if err != nil {
		return "", fmt.Errorf("decode catalog tools: %w", err)
	}
	material := map[string]any{
		"schema_version": schemaVersion,
		"tools":          tools,
	}

	var buffer bytes.Buffer
	encoder := json.NewEncoder(&buffer)
	encoder.SetEscapeHTML(false)
	if err := encoder.Encode(material); err != nil {
		return "", fmt.Errorf("encode catalog hash material: %w", err)
	}
	canonical := bytes.TrimSuffix(buffer.Bytes(), []byte{'\n'})
	digest := sha256.Sum256(canonical)
	return "sha256:" + hex.EncodeToString(digest[:]), nil
}

func decodeCanonicalValue(data json.RawMessage) (any, error) {
	var value any
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.UseNumber()
	if err := decoder.Decode(&value); err != nil {
		return nil, err
	}
	return value, nil
}
