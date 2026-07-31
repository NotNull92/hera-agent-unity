package cmd

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestCallJSON(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{})

	// When
	response, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info"}`,
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if !response.Success || sent.command != "scene" || sent.params["action"] != "info" {
		t.Fatalf("response=%#v sent=%#v", response, sent)
	}
}

func TestCallStdin(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{
		Reader: strings.NewReader(`{"action":"info"}`),
		Piped:  true,
	})

	// When
	_, err := command.Run(context.Background(), testInstance(), []string{"scene"})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if sent.params["action"] != "info" {
		t.Fatalf("params=%#v", sent.params)
	}
}

func TestCallFile(t *testing.T) {
	// Given
	path := filepath.Join(t.TempDir(), "request.json")
	if err := os.WriteFile(path, []byte(`{"action":"info"}`), 0o600); err != nil {
		t.Fatal(err)
	}
	command, sent := newTestCallCommand(t, callInput{})

	// When
	_, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--file", path,
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if sent.params["action"] != "info" {
		t.Fatalf("params=%#v", sent.params)
	}
}

func TestCallRejectsMultipleSources(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{
		Reader: strings.NewReader(`{"action":"info"}`),
		Piped:  true,
	})

	// When
	_, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info"}`,
	})

	// Then
	if err == nil || !strings.Contains(err.Error(), "multiple input sources") {
		t.Fatalf("err=%v", err)
	}
	if sent.calls != 0 {
		t.Fatalf("HTTP tool calls=%d, want 0", sent.calls)
	}
}

func TestCallRejectsUnknownArgumentBeforeHTTP(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{})

	// When
	_, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info","unknown":true}`,
	})

	// Then
	if err == nil || !strings.Contains(err.Error(), "/unknown") {
		t.Fatalf("err=%v", err)
	}
	if sent.calls != 0 {
		t.Fatalf("HTTP tool calls=%d, want 0", sent.calls)
	}
}

func TestCallValidateOnlySkipsHTTP(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{})

	// When
	response, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info"}`, "--validate-only",
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if sent.calls != 0 {
		t.Fatalf("HTTP tool calls=%d, want 0", sent.calls)
	}
	var result callValidationResult
	if err := json.Unmarshal(response.Data, &result); err != nil {
		t.Fatal(err)
	}
	if !result.Valid || result.Tool != "scene" || result.Action != "info" {
		t.Fatalf("result=%#v", result)
	}
}

func TestCallExplainReportsResolvedSafety(t *testing.T) {
	// Given
	command, sent := newTestCallCommand(t, callInput{})

	// When
	response, err := command.Run(context.Background(), testInstance(), []string{
		"scene", "--json", `{"action":"info"}`, "--profile", "core", "--explain",
	})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if sent.calls != 0 {
		t.Fatalf("HTTP tool calls=%d, want 0", sent.calls)
	}
	var result callExplanation
	if err := json.Unmarshal(response.Data, &result); err != nil {
		t.Fatal(err)
	}
	if result.Tool != "scene" || result.Action != "info" ||
		result.Profile != "core" || result.Safety.RiskClass != "read_only" ||
		result.Policy.RequiresApproval || result.Policy.Enforced {
		t.Fatalf("result=%#v", result)
	}
}

func TestTypedAndLegacyProduceEquivalentRequest(t *testing.T) {
	// Given
	typed, err := decodeCallObject([]byte(`{"type":"error","lines":5}`))
	if err != nil {
		t.Fatal(err)
	}
	legacy, _, err := buildParams([]string{"--type", "error", "--lines", "5"}, nil)
	if err != nil {
		t.Fatal(err)
	}

	// When
	typedRequest := newToolRequest("console", typed)
	legacyRequest := newToolRequest("console", legacy)
	typedJSON, err := json.Marshal(typedRequest)
	if err != nil {
		t.Fatal(err)
	}
	legacyJSON, err := json.Marshal(legacyRequest)
	if err != nil {
		t.Fatal(err)
	}

	// Then
	if string(typedJSON) != string(legacyJSON) {
		t.Fatalf("typed=%#v legacy=%#v", typedRequest, legacyRequest)
	}
}

type sentCall struct {
	command string
	params  map[string]any
	calls   int
}

func newTestCallCommand(t *testing.T, input callInput) (*callCommand, *sentCall) {
	t.Helper()
	snapshot := testSnapshot(t)
	sent := &sentCall{}
	command := &callCommand{
		load: func(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
			return snapshot, nil
		},
		send: func(command string, params interface{}) (*client.CommandResponse, error) {
			sent.command = command
			sent.params = params.(map[string]any)
			sent.calls++
			return &client.CommandResponse{Success: true, Data: json.RawMessage(`{"ok":true}`)}, nil
		},
		input: input,
	}
	return command, sent
}

func testSnapshot(t *testing.T) *toolregistry.Snapshot {
	t.Helper()
	data, err := os.ReadFile("../internal/toolregistry/testdata/catalog-v1.json")
	if err != nil {
		t.Fatal(err)
	}
	catalog, err := toolregistry.ParseCatalog(data)
	if err != nil {
		t.Fatal(err)
	}
	compiled, err := schema.NewCompilerCache().Compile(
		catalog.CatalogHash,
		catalog.SchemaDefinitions(),
	)
	if err != nil {
		t.Fatal(err)
	}
	return &toolregistry.Snapshot{
		Catalog:  catalog,
		Schemas:  compiled,
		Exposure: toolregistry.ExposureProfile,
	}
}

func testInstance() *client.Instance {
	return &client.Instance{
		Port:        8090,
		ProjectPath: "C:/project",
		DomainEpoch: "epoch",
		Features:    []string{toolregistry.FeatureDomainEpochV1, toolregistry.FeatureToolCatalogV1},
	}
}
