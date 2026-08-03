package main

import (
	"strings"
	"testing"
)

func TestGenerateIncludesAllSharedContracts(t *testing.T) {
	value := manifest{SchemaVersion: "hera.runtime-contracts/1"}
	value.ToolCatalog.SchemaVersion = "hera.tool-catalog/1"
	value.Execution.ProtocolVersion = "hera.execution/1"
	value.Features.Approval = "approval_v1"
	value.Features.DomainEpoch = "domain_epoch_v1"
	value.Features.ExecutionProtocol = "execution_protocol_v1"
	value.Features.OperationLedger = "operation_ledger_v1"
	value.Features.TaskBridge = "task_bridge_v1"
	value.Features.ToolCatalog = "tool_catalog_v1"
	value.AssetConfigLock.Version = 1
	value.AssetConfigLock.StaleAfterMS = 120000

	outputs, err := generate(value)
	if err != nil {
		t.Fatal(err)
	}
	for path, needles := range map[string][]string{
		"internal/protocol/contracts_gen.go":                        {"ExecutionProtocolVersion", "AssetConfigLockStaleAfterMilliseconds"},
		"AgentConnector/Editor/Core/ProtocolContracts.Generated.cs": {"FeatureExecutionProtocolV1", "AssetConfigLockVersion"},
	} {
		text := string(outputs[path])
		for _, needle := range needles {
			if !strings.Contains(text, needle) {
				t.Fatalf("%s does not contain %s", path, needle)
			}
		}
	}
}
