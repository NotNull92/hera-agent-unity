package mcpserver

import (
	"context"
	"encoding/json"
	"slices"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type testServerSetup struct {
	config   Config
	snapshot *toolregistry.Snapshot
	sender   toolSender
}

func startConfiguredTestSession(t *testing.T, setup testServerSetup) (*mcp.ClientSession, func()) {
	return startConfiguredTestSessionWithClient(t, setup, nil)
}

func startConfiguredTestSessionWithClient(
	t *testing.T,
	setup testServerSetup,
	options *mcp.ClientOptions,
) (*mcp.ClientSession, func()) {
	t.Helper()
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	done := make(chan error, 1)
	runtime := nativeRuntime{
		instance: &client.Instance{Port: 1234, Features: []string{client.FeatureApprovalV1, client.FeatureOperationLedgerV1}},
		snapshot: setup.snapshot,
		sender:   setup.sender,
		approver: setup.sender.(approvalSender),
		timeout:  1_000,
		mrtr:     setup.config.MRTR,
	}
	go func() {
		done <- runPrepared(ctx, setup.config, serverTransport, runtime)
	}()
	clientSession := mcp.NewClient(&mcp.Implementation{Name: "m10-test", Version: "test"}, options)
	session, err := clientSession.Connect(ctx, clientTransport, nil)
	if err != nil {
		cancel()
		t.Fatal(err)
	}
	return session, func() {
		_ = session.Close()
		cancel()
		select {
		case err := <-done:
			if err != nil {
				t.Errorf("server error: %v", err)
			}
		case <-time.After(time.Second):
			t.Error("server did not stop")
		}
	}
}

func snapshotWithDynamicTool(t *testing.T) *toolregistry.Snapshot {
	t.Helper()
	snapshot := nativeTestSnapshot(t)
	snapshot.Catalog.DomainEpoch = "m10-test-epoch"
	for index := range snapshot.Catalog.Tools {
		tool := &snapshot.Catalog.Tools[index]
		if tool.ContractMode == toolregistry.ContractStrict && tool.Name != "exec" {
			tool.Profiles = append(tool.Profiles, "full")
			slices.Sort(tool.Profiles)
		}
	}
	objectSchema := json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"action":{"type":"string","const":"inspect"}},"required":["action"]}`)
	snapshot.Catalog.Tools = append(snapshot.Catalog.Tools, toolregistry.Tool{
		Name: "dynamic_probe", Title: "Dynamic Probe", Description: "Inspect a dynamic custom probe",
		Source:       toolregistry.Source{Kind: "custom", Assembly: "M10.Tests", Type: "DynamicProbe"},
		ContractMode: toolregistry.ContractStrict, Profiles: []string{"custom", "full"},
		InputSchema: objectSchema, OutputSchema: json.RawMessage(`{"type":"object","additionalProperties":true}`),
		Safety: toolregistry.Safety{RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true},
	})
	slices.SortFunc(snapshot.Catalog.Tools, func(left, right toolregistry.Tool) int {
		return strings.Compare(left.Name, right.Name)
	})
	compiled, err := schema.NewCompilerCache().Compile(snapshot.Catalog.CatalogHash, snapshot.Catalog.SchemaDefinitions())
	if err != nil {
		t.Fatal(err)
	}
	snapshot.Schemas = compiled
	return snapshot
}

func equalStrings(left, right []string) bool { return slices.Equal(left, right) }

func mustJSON(t *testing.T, value any) string {
	t.Helper()
	encoded, err := json.Marshal(value)
	if err != nil {
		t.Fatal(err)
	}
	return string(encoded)
}
