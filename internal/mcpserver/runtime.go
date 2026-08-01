package mcpserver

import (
	"context"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/taskbridge"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type toolSender interface {
	SendWithOptions(context.Context, *client.Instance, string, any, int, client.SendOptions) (*client.CommandResponse, error)
}

type approvalSender interface {
	PreflightApproval(context.Context, *client.Instance, client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error)
}

type nativeRuntime struct {
	instance *client.Instance
	snapshot *toolregistry.Snapshot
	catalogs *catalogState
	loader   catalogLoader
	discover instanceDiscoverer
	sender   toolSender
	approver approvalSender
	timeout  int
	mrtr     bool
	tasks    *taskbridge.Store
	taskMode bool
}

func prepareNativeRuntime(ctx context.Context, config Config) (nativeRuntime, error) {
	instance, err := client.DiscoverInstanceFresh(config.Project, config.Port)
	if err != nil {
		return nativeRuntime{}, fmt.Errorf("discover Unity for MCP startup: %w", err)
	}
	registry := toolregistry.NewRegistry(toolregistry.RegistryOptions{})
	snapshot, err := registry.Load(ctx, instance)
	if err != nil {
		return nativeRuntime{}, fmt.Errorf("load native tool catalog for MCP startup: %w", err)
	}
	runtime := nativeRuntime{
		instance: instance,
		snapshot: snapshot,
		loader:   registry,
		discover: client.DiscoverInstanceFresh,
		sender:   client.DefaultClient,
		approver: client.DefaultClient,
		timeout:  config.TimeoutMS,
		mrtr:     config.MRTR,
		tasks:    taskbridge.New(paths.StatusDir()),
		taskMode: instanceHasFeature(instance, client.FeatureTaskBridgeV1),
	}
	if err := validateRuntime(config, runtime); err != nil {
		return nativeRuntime{}, err
	}
	return runtime, nil
}

func validateRuntime(config Config, runtime nativeRuntime) error {
	if runtime.instance == nil || runtime.snapshot == nil || runtime.snapshot.Catalog == nil || runtime.sender == nil {
		return fmt.Errorf("MCP runtime is incomplete")
	}
	if config.exposure() != ExposureCompact && (runtime.snapshot.Exposure != toolregistry.ExposureProfile || runtime.snapshot.Schemas == nil) {
		return fmt.Errorf("native strict tool catalog is required for MCP profile exposure")
	}
	if config.exposure() != ExposureCompact {
		tools, err := runtime.snapshot.Catalog.ToolsForProfile(config.effectiveProfile())
		if err != nil {
			return err
		}
		if profileMayMutate(tools) && !instanceHasFeature(runtime.instance, client.FeatureOperationLedgerV1) {
			return fmt.Errorf("profile %q contains mutations but Unity does not advertise %s", config.effectiveProfile(), client.FeatureOperationLedgerV1)
		}
	}
	return nil
}

func (runtime nativeRuntime) acquire() (nativeRuntime, error) {
	if runtime.catalogs == nil {
		return runtime, nil
	}
	return runtime.catalogs.acquire()
}
