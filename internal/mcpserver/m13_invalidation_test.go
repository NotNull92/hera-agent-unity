package mcpserver

import (
	"context"
	"errors"
	"slices"
	"testing"
	"time"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestDomainEpochInvalidatesCatalog(t *testing.T) {
	oldSnapshot := m13Snapshot(t, "epoch-old", m13Hash('a'), "alpha")
	newSnapshot := m13Snapshot(t, "epoch-new", m13Hash('b'), "alpha")
	state, refresher := m13Refresher(t, oldSnapshot, newSnapshot)
	loader := refresher.loader.(*m13Loader)
	loaded := make(chan struct{})
	loader.onLoad = func() { close(loaded) }
	listEntered := make(chan struct{})
	releaseList := make(chan struct{})
	listDone := make(chan error, 1)
	middleware := catalogConsistencyMiddleware(state)(func(context.Context, string, mcp.Request) (mcp.Result, error) {
		close(listEntered)
		<-releaseList
		return &mcp.ListToolsResult{}, nil
	})
	go func() {
		_, err := middleware(context.Background(), "tools/list", nil)
		listDone <- err
	}()
	<-listEntered
	type refreshResult struct {
		changed bool
		err     error
	}
	refreshDone := make(chan refreshResult, 1)
	go func() {
		changed, err := refresher.refresh(context.Background())
		refreshDone <- refreshResult{changed: changed, err: err}
	}()
	<-loaded
	if _, err := state.acquire(); !errors.Is(err, errCatalogStale) {
		t.Fatalf("acquire during reload error = %v, want %v", err, errCatalogStale)
	}
	select {
	case result := <-refreshDone:
		t.Fatalf("refresh completed while tools/list held the old registry: %#v", result)
	default:
	}
	close(releaseList)
	if err := <-listDone; err != nil {
		t.Fatal(err)
	}
	result := <-refreshDone
	if result.err != nil {
		t.Fatal(result.err)
	}
	runtime, err := state.acquire()
	if err != nil {
		t.Fatal(err)
	}
	if got := runtime.snapshot.Catalog.DomainEpoch; got != "epoch-new" {
		t.Fatalf("domain epoch = %q, want epoch-new", got)
	}
}

func TestSameCatalogHashAvoidsSpuriousChange(t *testing.T) {
	hash := m13Hash('a')
	oldSnapshot := m13Snapshot(t, "epoch-old", hash, "alpha")
	newSnapshot := m13Snapshot(t, "epoch-new", hash, "alpha")
	state, refresher := m13Refresher(t, oldSnapshot, newSnapshot)
	refresher.loader.(*m13Loader).failOnce = true
	if _, err := refresher.refresh(context.Background()); err == nil {
		t.Fatal("failed catalog reload returned no error")
	}
	if _, err := state.acquire(); !errors.Is(err, errCatalogStale) {
		t.Fatalf("acquire after failed reload error = %v, want %v", err, errCatalogStale)
	}

	changed, err := refresher.refresh(context.Background())
	if err != nil {
		t.Fatal(err)
	}
	if changed {
		t.Fatal("same catalog hash produced a visible tool-list change")
	}
	runtime, err := state.acquire()
	if err != nil {
		t.Fatal(err)
	}
	if got := runtime.snapshot.Catalog.DomainEpoch; got != "epoch-new" {
		t.Fatalf("domain epoch = %q, want epoch-new", got)
	}
}

func TestListChangedOnCustomToolAdd(t *testing.T) {
	oldSnapshot := m13Snapshot(t, "epoch-old", m13Hash('a'), "alpha")
	newSnapshot := m13Snapshot(t, "epoch-new", m13Hash('b'), "alpha", "beta")
	_, refresher := m13Refresher(t, oldSnapshot, newSnapshot)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	serverTransport, clientTransport := mcp.NewInMemoryTransports()
	notified := make(chan struct{}, 1)
	done := make(chan error, 1)
	go func() { done <- runServer(ctx, refresher.server, serverTransport) }()
	clientSession := mcp.NewClient(&mcp.Implementation{Name: "m13-test", Version: "test"}, &mcp.ClientOptions{
		ToolListChangedHandler: func(context.Context, *mcp.ToolListChangedRequest) {
			select {
			case notified <- struct{}{}:
			default:
			}
		},
	})
	session, err := clientSession.Connect(ctx, clientTransport, nil)
	if err != nil {
		t.Fatal(err)
	}
	defer func() {
		_ = session.Close()
		cancel()
		<-done
	}()

	changed, err := refresher.refresh(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if !changed {
		t.Fatal("custom tool addition did not change the visible tool list")
	}
	select {
	case <-notified:
	case <-ctx.Done():
		t.Fatal("tools/list_changed notification was not delivered")
	}
	listed, err := session.ListTools(ctx, nil)
	if err != nil {
		t.Fatal(err)
	}
	if got := mcpToolNames(listed.Tools); !slices.Equal(got, []string{"alpha", "beta"}) {
		t.Fatalf("tools = %v, want [alpha beta]", got)
	}
}

func TestRemovedToolReturnsCatalogStaleOrUnknownTool(t *testing.T) {
	oldSnapshot := m13Snapshot(t, "epoch-old", m13Hash('a'), "alpha", "beta")
	newSnapshot := m13Snapshot(t, "epoch-new", m13Hash('b'), "alpha")
	sender := &recordingToolSender{response: successResponse()}
	state, refresher := m13RefresherWithSender(t, oldSnapshot, newSnapshot, sender)
	loader := refresher.loader.(*m13Loader)
	loader.entered = make(chan struct{})
	loader.release = make(chan struct{})
	handler := nativeToolHandler(nativeRuntime{catalogs: state}, "beta", "custom")
	done := make(chan error, 1)
	go func() {
		_, err := refresher.refresh(context.Background())
		done <- err
	}()
	<-loader.entered

	stale, err := handler(context.Background(), m13Call("beta"))
	if err != nil {
		t.Fatal(err)
	}
	assertStructuredCode(t, stale, "CATALOG_STALE")
	close(loader.release)
	if err := <-done; err != nil {
		t.Fatal(err)
	}
	unknown, err := handler(context.Background(), m13Call("beta"))
	if err != nil {
		t.Fatal(err)
	}
	assertStructuredCode(t, unknown, "TOOL_NOT_FOUND")
	if sender.calls != 0 {
		t.Fatalf("Unity sender calls = %d, want 0", sender.calls)
	}
}

func TestInFlightCallUsesOriginalSnapshot(t *testing.T) {
	oldSnapshot := m13Snapshot(t, "epoch-old", m13Hash('a'), "alpha")
	newSnapshot := m13Snapshot(t, "epoch-new", m13Hash('b'), "alpha")
	sender := newM13BlockingSender()
	state, refresher := m13RefresherWithSender(t, oldSnapshot, newSnapshot, sender)
	handler := nativeToolHandler(nativeRuntime{catalogs: state}, "alpha", "custom")
	callDone := make(chan error, 1)
	go func() {
		_, err := handler(context.Background(), m13Call("alpha"))
		callDone <- err
	}()
	<-sender.entered
	if _, err := refresher.refresh(context.Background()); err != nil {
		t.Fatal(err)
	}
	close(sender.release)
	if err := <-callDone; err != nil {
		t.Fatal(err)
	}
	if got := sender.catalogHash(); got != oldSnapshot.Catalog.CatalogHash {
		t.Fatalf("in-flight catalog hash = %q, want %q", got, oldSnapshot.Catalog.CatalogHash)
	}
	if _, err := handler(context.Background(), m13Call("alpha")); err != nil {
		t.Fatal(err)
	}
	if got := sender.catalogHashes(); !slices.Equal(got, []string{oldSnapshot.Catalog.CatalogHash, newSnapshot.Catalog.CatalogHash}) {
		t.Fatalf("catalog hashes = %v, want old then new", got)
	}
}

func TestCompactHandlersAcquireCurrentSnapshot(t *testing.T) {
	oldSnapshot := m13Snapshot(t, "epoch-old", m13Hash('a'), "alpha")
	newSnapshot := m13Snapshot(t, "epoch-new", m13Hash('b'), "alpha", "beta")
	state, refresher := m13Refresher(t, oldSnapshot, newSnapshot)
	refresher.config = compactTestConfig()
	handler := compactDescribeRuntimeHandler(nativeRuntime{catalogs: state}, false)
	request := &mcp.CallToolRequest{Params: &mcp.CallToolParamsRaw{Arguments: []byte(`{"name":"alpha"}`)}}

	before, err := handler(context.Background(), request)
	if err != nil {
		t.Fatal(err)
	}
	if got := m13DomainEpoch(before); got != "epoch-old" {
		t.Fatalf("before domain epoch = %q, want epoch-old", got)
	}
	changed, err := refresher.refresh(context.Background())
	if err != nil {
		t.Fatal(err)
	}
	if changed {
		t.Fatal("compact catalog refresh changed the fixed MCP tool surface")
	}
	after, err := handler(context.Background(), request)
	if err != nil {
		t.Fatal(err)
	}
	if got := m13DomainEpoch(after); got != "epoch-new" {
		t.Fatalf("after domain epoch = %q, want epoch-new", got)
	}
}

func m13DomainEpoch(result *mcp.CallToolResult) string {
	envelope := result.StructuredContent.(map[string]any)
	data := envelope["data"].(map[string]any)
	return data["domain_epoch"].(string)
}
