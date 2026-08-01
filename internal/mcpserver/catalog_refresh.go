package mcpserver

import (
	"context"
	"errors"
	"fmt"
	"reflect"
	"sync"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

const catalogRefreshInterval = time.Second

var errCatalogStale = errors.New("unity tool catalog is being refreshed")

type catalogLoader interface {
	Load(context.Context, *client.Instance) (*toolregistry.Snapshot, error)
}

type instanceDiscoverer func(string, int) (*client.Instance, error)

type catalogState struct {
	mu         sync.RWMutex
	registryMu sync.RWMutex
	runtime    nativeRuntime
	stale      bool
	ready      chan struct{}
}

func newCatalogState(runtime nativeRuntime) *catalogState {
	runtime.catalogs = nil
	ready := make(chan struct{})
	close(ready)
	return &catalogState{runtime: runtime, ready: ready}
}

func (state *catalogState) acquire() (nativeRuntime, error) {
	state.mu.RLock()
	defer state.mu.RUnlock()
	if state.stale {
		return nativeRuntime{}, errCatalogStale
	}
	return state.runtime, nil
}

func (state *catalogState) current() nativeRuntime {
	state.mu.RLock()
	defer state.mu.RUnlock()
	return state.runtime
}

func (state *catalogState) markStale() nativeRuntime {
	state.mu.Lock()
	defer state.mu.Unlock()
	if !state.stale {
		state.ready = make(chan struct{})
	}
	state.stale = true
	return state.runtime
}

func (state *catalogState) replace(runtime nativeRuntime) {
	runtime.catalogs = nil
	state.mu.Lock()
	state.runtime = runtime
	wasStale := state.stale
	state.stale = false
	if wasStale {
		close(state.ready)
	}
	state.mu.Unlock()
}

func (state *catalogState) wait(ctx context.Context) error {
	state.mu.RLock()
	ready := state.ready
	state.mu.RUnlock()
	select {
	case <-ready:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

func (state *catalogState) withRegistryRead(ctx context.Context, read func() (mcp.Result, error)) (mcp.Result, error) {
	for {
		if err := state.wait(ctx); err != nil {
			return nil, err
		}
		state.registryMu.RLock()
		state.mu.RLock()
		stale := state.stale
		state.mu.RUnlock()
		if !stale {
			defer state.registryMu.RUnlock()
			return read()
		}
		state.registryMu.RUnlock()
	}
}

type catalogRefresher struct {
	mu       sync.Mutex
	server   *mcp.Server
	config   Config
	state    *catalogState
	loader   catalogLoader
	discover instanceDiscoverer
}

func (refresher *catalogRefresher) refresh(ctx context.Context) (bool, error) {
	refresher.mu.Lock()
	defer refresher.mu.Unlock()

	current := refresher.state.current()
	instance, err := refresher.discover(refresher.config.Project, refresher.config.Port)
	if err != nil {
		refresher.state.markStale()
		return false, fmt.Errorf("discover Unity catalog epoch: %w", err)
	}
	if instance.DomainEpoch == current.instance.DomainEpoch {
		refresher.state.replace(current)
		return false, nil
	}

	current = refresher.state.markStale()
	snapshot, err := refresher.loader.Load(ctx, instance)
	if err != nil {
		return false, fmt.Errorf("reload Unity tool catalog: %w", err)
	}
	next := current
	next.instance = instance
	next.snapshot = snapshot
	if err := validateRuntime(refresher.config, next); err != nil {
		return false, err
	}
	refresher.state.registryMu.Lock()
	defer refresher.state.registryMu.Unlock()
	changed, err := refresher.reconcile(current, next)
	if err != nil {
		return false, err
	}
	refresher.state.replace(next)
	return changed, nil
}

func catalogConsistencyMiddleware(state *catalogState) mcp.Middleware {
	return func(next mcp.MethodHandler) mcp.MethodHandler {
		return func(ctx context.Context, method string, request mcp.Request) (mcp.Result, error) {
			if method == "tools/list" {
				return state.withRegistryRead(ctx, func() (mcp.Result, error) {
					return next(ctx, method, request)
				})
			}
			return next(ctx, method, request)
		}
	}
}

func (refresher *catalogRefresher) reconcile(current, next nativeRuntime) (bool, error) {
	if refresher.config.exposure() == ExposureCompact || current.snapshot.Catalog.CatalogHash == next.snapshot.Catalog.CatalogHash {
		return false, nil
	}
	oldTools, err := visibleTools(refresher.config, current)
	if err != nil {
		return false, err
	}
	newTools, err := visibleTools(refresher.config, next)
	if err != nil {
		return false, err
	}
	oldByName := make(map[string]toolregistry.Tool, len(oldTools))
	newByName := make(map[string]toolregistry.Tool, len(newTools))
	for _, tool := range oldTools {
		oldByName[tool.Name] = tool
	}
	for _, tool := range newTools {
		newByName[tool.Name] = tool
	}
	removed := make([]string, 0)
	for name := range oldByName {
		if _, ok := newByName[name]; !ok {
			removed = append(removed, name)
		}
	}
	if len(removed) > 0 {
		refresher.server.RemoveTools(removed...)
	}
	root := nativeRuntime{catalogs: refresher.state}
	for name, tool := range newByName {
		if old, ok := oldByName[name]; ok && reflect.DeepEqual(nativeMCPTool(old), nativeMCPTool(tool)) {
			continue
		}
		refresher.server.AddTool(nativeMCPTool(tool), nativeToolHandler(root, tool.Name, refresher.config.effectiveProfile()))
	}
	return len(removed) > 0 || !sameToolContracts(oldByName, newByName), nil
}

func sameToolContracts(left, right map[string]toolregistry.Tool) bool {
	if len(left) != len(right) {
		return false
	}
	for name, tool := range left {
		other, ok := right[name]
		if !ok || !reflect.DeepEqual(nativeMCPTool(tool), nativeMCPTool(other)) {
			return false
		}
	}
	return true
}

func visibleTools(config Config, runtime nativeRuntime) ([]toolregistry.Tool, error) {
	return runtime.snapshot.Catalog.ToolsForProfile(config.effectiveProfile())
}

func (refresher *catalogRefresher) observe(ctx context.Context, diagnostics func(string, ...any)) {
	ticker := time.NewTicker(catalogRefreshInterval)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if _, err := refresher.refresh(ctx); err != nil && !errors.Is(err, context.Canceled) {
				diagnostics("MCP catalog refresh: %v\n", err)
			}
		}
	}
}
