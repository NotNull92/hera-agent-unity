package cmd

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func mcpHelperCommand(ctx context.Context, args string) *exec.Cmd {
	var command *exec.Cmd
	if binary := os.Getenv("HERA_MCP_TEST_BINARY"); binary != "" {
		command = exec.CommandContext(ctx, binary, strings.Fields(args)...)
	} else {
		command = exec.CommandContext(ctx, os.Args[0], "-test.run=^TestMCPProcessHelper$")
	}
	command.Env = append(os.Environ(),
		"HERA_MCP_TEST_HELPER=1",
		"HERA_MCP_TEST_ARGS="+args,
		"HERA_MCP_ENABLED=1",
		"HERA_AGENT_NO_PATH_CHECK=1",
	)
	return command
}

func assertProtocolOnly(t *testing.T, stdout []byte) {
	t.Helper()
	lines := bytes.Split(bytes.TrimSpace(stdout), []byte{'\n'})
	if len(lines) == 0 || len(lines[0]) == 0 {
		t.Fatal("stdout contained no MCP protocol frames")
	}
	for index, line := range lines {
		var frame struct {
			JSONRPC string `json:"jsonrpc"`
		}
		if err := json.Unmarshal(bytes.TrimSpace(line), &frame); err != nil {
			t.Fatalf("stdout line %d is not JSON: %q: %v", index+1, line, err)
		}
		if frame.JSONRPC != "2.0" {
			t.Fatalf("stdout line %d is not an MCP frame: %q", index+1, line)
		}
	}
}

func startMCPUnityFixture(t *testing.T) (int, string) {
	t.Helper()
	project := filepath.Join(t.TempDir(), "Project")
	if err := os.MkdirAll(project, 0o755); err != nil {
		t.Fatal(err)
	}
	projectID, err := toolregistry.ProjectID(project)
	if err != nil {
		t.Fatal(err)
	}
	catalog := mcpCatalogFixture(t, projectID)
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		defer request.Body.Close()
		var command client.CommandRequest
		if err := json.NewDecoder(request.Body).Decode(&command); err != nil {
			http.Error(writer, err.Error(), http.StatusBadRequest)
			return
		}
		switch command.Command {
		case "list":
			_ = json.NewEncoder(writer).Encode(&client.CommandResponse{Success: true, Message: "OK", Data: catalog})
		case "scene":
			_ = json.NewEncoder(writer).Encode(&client.CommandResponse{
				Success: true,
				Message: "Scene inspected",
				Data:    json.RawMessage(`{"name":"M9Fixture"}`),
			})
		case "dynamic_probe":
			_ = json.NewEncoder(writer).Encode(&client.CommandResponse{Success: true, Message: "Dynamic probe inspected", Data: json.RawMessage(`{"dynamic":true}`)})
		default:
			http.Error(writer, "unexpected command", http.StatusBadRequest)
		}
	}))
	t.Cleanup(server.Close)
	port, err := strconv.Atoi(server.URL[strings.LastIndex(server.URL, ":")+1:])
	if err != nil {
		t.Fatal(err)
	}
	home := t.TempDir()
	instances := filepath.Join(home, ".hera-agent-unity", "instances")
	if err := os.MkdirAll(instances, 0o755); err != nil {
		t.Fatal(err)
	}
	heartbeat, err := json.Marshal(client.Instance{
		State:       "ready",
		ProjectPath: project,
		Port:        port,
		PID:         os.Getpid(),
		DomainEpoch: "m9-test-epoch",
		Features: []string{
			toolregistry.FeatureDomainEpochV1,
			toolregistry.FeatureToolCatalogV1,
			client.FeatureOperationLedgerV1,
		},
		Timestamp: time.Now().UnixMilli(),
	})
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(instances, fmt.Sprintf("%d.json", port)), heartbeat, 0o600); err != nil {
		t.Fatal(err)
	}
	return port, home
}

func mcpCatalogFixture(t *testing.T, projectID string) json.RawMessage {
	t.Helper()
	tool := map[string]any{
		"name": "scene", "title": "Scene", "description": "Inspect the scene",
		"source":        map[string]any{"kind": "builtin", "assembly": "HeraAgent.Editor", "type": "HeraAgent.Tools.Scene"},
		"contract_mode": "strict", "profiles": []string{"core", "full", "scene"},
		"aliases": []any{}, "examples": []any{}, "actions": []any{},
		"input_schema": map[string]any{
			"type": "object", "additionalProperties": false,
			"properties": map[string]any{"action": map[string]any{"type": "string", "enum": []string{"info"}}},
			"required":   []string{"action"},
		},
		"output_schema": map[string]any{"type": "object", "additionalProperties": true},
		"safety": map[string]any{
			"risk_class": "read_only", "read_only": true, "destructive": false,
			"idempotent": true, "may_reload_domain": false, "requires_play_mode": false,
			"requires_confirmation": false, "reversible": true, "supports_cancellation": false,
			"side_effect_scope": "none", "rules": []any{},
		},
	}
	dynamicTool := map[string]any{
		"name": "dynamic_probe", "title": "Dynamic Probe", "description": "Inspect a dynamic custom probe",
		"source":        map[string]any{"kind": "custom", "assembly": "M10.Fixture", "type": "DynamicProbe"},
		"contract_mode": "strict", "profiles": []string{"custom", "full"},
		"aliases": []any{}, "examples": []any{}, "actions": []any{},
		"input_schema": map[string]any{
			"type": "object", "additionalProperties": false,
			"properties": map[string]any{"action": map[string]any{"type": "string", "const": "inspect"}},
			"required":   []string{"action"},
		},
		"output_schema": map[string]any{"type": "object", "additionalProperties": true},
		"safety": map[string]any{
			"risk_class": "read_only", "read_only": true, "destructive": false,
			"idempotent": true, "may_reload_domain": false, "requires_play_mode": false,
			"requires_confirmation": false, "reversible": true, "supports_cancellation": false,
			"side_effect_scope": "none", "rules": []any{},
		},
	}
	tools := []any{dynamicTool, tool}
	material := map[string]any{"schema_version": toolregistry.CatalogSchemaV1, "tools": tools}
	canonical, err := json.Marshal(material)
	if err != nil {
		t.Fatal(err)
	}
	hash := sha256.Sum256(canonical)
	document := map[string]any{
		"schema_version": toolregistry.CatalogSchemaV1,
		"catalog_hash":   fmt.Sprintf("sha256:%x", hash),
		"domain_epoch":   "m9-test-epoch",
		"project_id":     projectID,
		"tools":          tools,
	}
	encoded, err := json.Marshal(document)
	if err != nil {
		t.Fatal(err)
	}
	return encoded
}
