package cmd

import (
	"context"
	"os"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func withTempAssetConfigHome(t *testing.T) {
	t.Helper()
	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)
}

func TestAssetConfigDetect_fallsThroughStandaloneRouting(t *testing.T) {
	// Given
	handled, err := runStandaloneCommand("asset-config", []string{"detect"})

	// Then
	if err != nil {
		t.Fatalf("runStandaloneCommand() error = %v", err)
	}
	if handled {
		t.Fatal("asset-config detect must be routed to the Unity connector")
	}
}

func TestAssetConfigDetect_dispatchesDetectAssetsAfterCreatingConfig(t *testing.T) {
	// Given
	withTempAssetConfigHome(t)
	var gotCommand string
	var gotParams interface{}
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		gotCommand = command
		gotParams = params
		return &client.CommandResponse{Success: true}, nil
	}

	// When
	_, err := runUnityCommand(context.Background(), "asset-config", []string{"detect", "--project_path", "C:/Project"}, send, nil, nil)

	// Then
	if err != nil {
		t.Fatalf("runUnityCommand() error = %v", err)
	}
	if gotCommand != "detect_assets" {
		t.Fatalf("sent command = %q, want detect_assets", gotCommand)
	}
	params, ok := gotParams.(map[string]interface{})
	if !ok {
		t.Fatalf("sent params type = %T, want map[string]interface{}", gotParams)
	}
	if got := params["project_path"]; got != "C:/Project" {
		t.Fatalf("project_path = %#v, want C:/Project", got)
	}
	if _, err := os.Stat(assetconfig.ConfigFilePath()); err != nil {
		t.Fatalf("config file was not created before detection: %v", err)
	}
}
