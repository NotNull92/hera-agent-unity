package cmd

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestCallValidatesAgainstResolvedActionSchema(t *testing.T) {
	// Given
	snapshot := testSnapshot(t)
	for toolIndex := range snapshot.Catalog.Tools {
		tool := &snapshot.Catalog.Tools[toolIndex]
		if tool.Name != "scene" {
			continue
		}
		tool.InputSchema = json.RawMessage(`{"type":"object","required":["action","doc"],"properties":{"action":{"type":"string"},"doc":{"type":"string"}},"additionalProperties":false}`)
		for actionIndex := range tool.Actions {
			action := &tool.Actions[actionIndex]
			if action.Name == "info" {
				action.InputSchema = json.RawMessage(`{"type":"object","required":["doc"],"properties":{"doc":{"type":"object","additionalProperties":true}},"additionalProperties":false}`)
			}
		}
	}
	compiled, err := schema.NewCompilerCache().Compile(
		"sha256:action-schema-regression",
		snapshot.Catalog.SchemaDefinitions(),
	)
	if err != nil {
		t.Fatal(err)
	}
	snapshot.Schemas = compiled
	command, sent := newTestCallCommand(t, callInput{})
	command.load = func(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
		return snapshot, nil
	}

	// When
	response, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info","doc":{"root":{"name":"Canvas"}}}`, "--validate-only",
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !response.Success || sent.calls != 0 {
		t.Fatalf("response=%#v HTTP tool calls=%d", response, sent.calls)
	}
}

func TestCallRetainsSelectorWhenActionSchemaDeclaresIt(t *testing.T) {
	// Given
	snapshot := testSnapshot(t)
	for toolIndex := range snapshot.Catalog.Tools {
		tool := &snapshot.Catalog.Tools[toolIndex]
		if tool.Name != "scene" {
			continue
		}
		for actionIndex := range tool.Actions {
			action := &tool.Actions[actionIndex]
			if action.Name == "info" {
				action.InputSchema = json.RawMessage(`{"type":"object","required":["action"],"properties":{"action":{"const":"info"}},"additionalProperties":false}`)
			}
		}
	}
	compiled, err := schema.NewCompilerCache().Compile(
		"sha256:action-selector-regression",
		snapshot.Catalog.SchemaDefinitions(),
	)
	if err != nil {
		t.Fatal(err)
	}
	snapshot.Schemas = compiled
	command, sent := newTestCallCommand(t, callInput{})
	command.load = func(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
		return snapshot, nil
	}

	// When
	response, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info"}`, "--validate-only",
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !response.Success || sent.calls != 0 {
		t.Fatalf("response=%#v HTTP tool calls=%d", response, sent.calls)
	}
}
