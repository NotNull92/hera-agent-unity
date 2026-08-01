package mcpserver

import (
	"context"
	"encoding/json"
	"errors"
	"slices"
	"sync"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type m13Loader struct {
	snapshot *toolregistry.Snapshot
	onLoad   func()
	entered  chan struct{}
	release  chan struct{}
	failOnce bool
}

func (loader *m13Loader) Load(context.Context, *client.Instance) (*toolregistry.Snapshot, error) {
	if loader.onLoad != nil {
		loader.onLoad()
	}
	if loader.entered != nil {
		close(loader.entered)
		<-loader.release
	}
	if loader.failOnce {
		loader.failOnce = false
		return nil, errors.New("catalog load failed")
	}
	return loader.snapshot, nil
}

type m13BlockingSender struct {
	entered chan struct{}
	release chan struct{}
	mu      sync.Mutex
	hashes  []string
	once    sync.Once
}

func newM13BlockingSender() *m13BlockingSender {
	return &m13BlockingSender{entered: make(chan struct{}), release: make(chan struct{})}
}

func (sender *m13BlockingSender) PreflightApproval(context.Context, *client.Instance, client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error) {
	return nil, nil
}

func (sender *m13BlockingSender) SendWithOptions(_ context.Context, _ *client.Instance, _ string, _ any, _ int, options client.SendOptions) (*client.CommandResponse, error) {
	sender.mu.Lock()
	sender.hashes = append(sender.hashes, options.CatalogHash)
	sender.mu.Unlock()
	sender.once.Do(func() {
		close(sender.entered)
		<-sender.release
	})
	return successResponse(), nil
}

func (sender *m13BlockingSender) catalogHash() string {
	sender.mu.Lock()
	defer sender.mu.Unlock()
	return sender.hashes[0]
}

func (sender *m13BlockingSender) catalogHashes() []string {
	sender.mu.Lock()
	defer sender.mu.Unlock()
	return slices.Clone(sender.hashes)
}

func m13Refresher(t *testing.T, oldSnapshot, newSnapshot *toolregistry.Snapshot) (*catalogState, *catalogRefresher) {
	return m13RefresherWithSender(t, oldSnapshot, newSnapshot, &recordingToolSender{response: successResponse()})
}

func m13RefresherWithSender(t *testing.T, oldSnapshot, newSnapshot *toolregistry.Snapshot, sender toolSender) (*catalogState, *catalogRefresher) {
	t.Helper()
	config := enabledTestConfig()
	config.Profile = "custom"
	instance := &client.Instance{Port: 1234, DomainEpoch: oldSnapshot.Catalog.DomainEpoch, Features: []string{client.FeatureApprovalV1, client.FeatureOperationLedgerV1}}
	runtime := nativeRuntime{instance: instance, snapshot: oldSnapshot, sender: sender, approver: sender.(approvalSender), timeout: 1_000}
	state := newCatalogState(runtime)
	runtime.catalogs = state
	server := newServer(config)
	if err := registerTools(server, config, runtime); err != nil {
		t.Fatal(err)
	}
	refresher := &catalogRefresher{
		server: server,
		config: config,
		state:  state,
		loader: &m13Loader{snapshot: newSnapshot},
		discover: func(string, int) (*client.Instance, error) {
			return &client.Instance{Port: 1234, DomainEpoch: newSnapshot.Catalog.DomainEpoch, Features: instance.Features}, nil
		},
	}
	return state, refresher
}

func m13Snapshot(t *testing.T, epoch, hash string, names ...string) *toolregistry.Snapshot {
	t.Helper()
	input := json.RawMessage(`{"type":"object","additionalProperties":false,"properties":{"action":{"type":"string","const":"inspect"}},"required":["action"]}`)
	output := json.RawMessage(`{"type":"object","additionalProperties":true}`)
	tools := make([]toolregistry.Tool, 0, len(names))
	for _, name := range names {
		tools = append(tools, toolregistry.Tool{
			Name: name, Title: name, Description: "M13 custom tool", Source: toolregistry.Source{Kind: "custom", Assembly: "M13.Tests", Type: name},
			ContractMode: toolregistry.ContractStrict, Profiles: []string{"custom", "full"}, InputSchema: input, OutputSchema: output,
			Safety: toolregistry.Safety{RiskClass: "read_only", ReadOnly: true, Idempotent: true, Reversible: true},
		})
	}
	compiled, err := schema.NewCompilerCache().Compile(hash, (&toolregistry.Catalog{Tools: tools}).SchemaDefinitions())
	if err != nil {
		t.Fatal(err)
	}
	return &toolregistry.Snapshot{Catalog: &toolregistry.Catalog{CatalogHash: hash, DomainEpoch: epoch, Tools: tools}, Schemas: compiled, Exposure: toolregistry.ExposureProfile}
}

func m13Hash(char byte) string { return "sha256:" + string(slices.Repeat([]byte{char}, 64)) }

func m13Call(name string) *mcp.CallToolRequest {
	return &mcp.CallToolRequest{Params: &mcp.CallToolParamsRaw{Name: name, Arguments: json.RawMessage(`{"action":"inspect"}`)}}
}
