package mcpserver

import (
	"context"
	"errors"
	"testing"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestFullExposureRegistersEveryStrictPolicyAllowedTool(t *testing.T) {
	// Given
	config := enabledTestConfig()
	config.Exposure = ExposureFull
	session, closeSession := startConfiguredTestSession(t, testServerSetup{config, snapshotWithDynamicTool(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	result, err := session.ListTools(context.Background(), nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	want := []string{"console", "dynamic_probe", "manage_assets", "manage_gameobject", "scene"}
	if got := mcpToolNames(result.Tools); !equalStrings(got, want) {
		t.Fatalf("tools=%v, want %v", got, want)
	}
}

func TestAdvancedProfileRequiresArbitraryCodePermission(t *testing.T) {
	// Given
	config := enabledTestConfig()
	config.Profile = "advanced"

	// When
	err := config.Validate()

	// Then
	if !errors.Is(err, ErrArbitraryCodePermissionRequired) {
		t.Fatalf("Validate() error=%v", err)
	}
}

func TestAdvancedProfileRegistersArbitraryCodeToolWhenExplicitlyAllowed(t *testing.T) {
	// Given
	config := enabledTestConfig()
	config.Profile = "advanced"
	config.AllowArbitraryCode = true
	session, closeSession := startConfiguredTestSession(t, testServerSetup{config, nativeTestSnapshot(t), &recordingToolSender{response: successResponse()}})
	defer closeSession()

	// When
	result, err := session.ListTools(context.Background(), nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if got := mcpToolNames(result.Tools); !equalStrings(got, []string{"exec"}) {
		t.Fatalf("tools=%v, want exec", got)
	}
}

func compactTestConfig() Config {
	config := enabledTestConfig()
	config.Exposure = ExposureCompact
	return config
}

func callToolData(t *testing.T, session *mcp.ClientSession, name string, arguments map[string]any) any {
	t.Helper()
	result, err := session.CallTool(context.Background(), &mcp.CallToolParams{Name: name, Arguments: arguments})
	if err != nil || result.IsError {
		t.Fatalf("CallTool(%s) result=%#v error=%v", name, result, err)
	}
	envelope := result.StructuredContent.(map[string]any)
	return envelope["data"]
}
