package cmd

import (
	"context"
	"os"
	"strings"
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

func TestAssetConfigCompilerPath_RequiresExactlyOnePath(t *testing.T) {
	for _, args := range [][]string{nil, {}, {"one", "two"}, {"   "}} {
		err := assetConfigCompilerPath(args, "set-csc", "defaultCscPath", func(string) (*assetconfig.AssetConfig, error) {
			t.Fatal("setter must not run for invalid input")
			return nil, nil
		})
		if err == nil || !strings.Contains(err.Error(), "usage: asset-config set-csc <path>") {
			t.Fatalf("args %q error = %v", args, err)
		}
	}
}

func TestAssetConfigCompilerPath_PassesTrimmedPathToSetter(t *testing.T) {
	var got string
	err := assetConfigCompilerPath([]string{"  C:/tools/csc.dll  "}, "set-csc", "defaultCscPath", func(path string) (*assetconfig.AssetConfig, error) {
		got = path
		return &assetconfig.AssetConfig{}, nil
	})
	if err != nil {
		t.Fatalf("assetConfigCompilerPath() error = %v", err)
	}
	if got != "C:/tools/csc.dll" {
		t.Fatalf("setter path = %q", got)
	}
}

func TestAssetConfigCmd_CompilerPathSubcommandsUseSpecificUsage(t *testing.T) {
	for _, subcommand := range []string{"set-csc", "set-dotnet"} {
		err := assetConfigCmd([]string{subcommand})
		if err == nil || !strings.Contains(err.Error(), "usage: asset-config "+subcommand+" <path>") {
			t.Fatalf("%s error = %v", subcommand, err)
		}
	}
}

func TestAssetConfigDetect_fallsThroughStandaloneRouting(t *testing.T) {
	// Given
	handled, err := (standaloneRunner{}).Run(
		context.Background(),
		"asset-config",
		[]string{"detect"},
	)

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
	_, err := (unityCommandRunner{send: send}).Run(
		context.Background(),
		"asset-config",
		[]string{"detect", "--project_path", "C:/Project"},
	)

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
