package cmd

import (
	"context"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func TestLegacySceneCommandStillWorks(t *testing.T) {
	// Given
	var sent toolRequest
	runner := unityCommandRunner{
		config: GlobalConfig{Timeout: 60 * time.Second},
		send: func(command string, params interface{}) (*client.CommandResponse, error) {
			sent = newToolRequest(command, params.(map[string]any))
			return &client.CommandResponse{Success: true}, nil
		},
	}

	// When
	response, err := runner.Run(context.Background(), "scene", []string{"info"})

	// Then
	if err != nil {
		t.Fatal(err)
	}
	args, ok := sent.Params["args"].([]string)
	if !response.Success || sent.Command != "scene" || !ok || len(args) != 1 || args[0] != "info" {
		t.Fatalf("response=%#v sent=%#v", response, sent)
	}
}

func TestLegacyParamsPrecedence(t *testing.T) {
	// Given
	args := []string{
		"--params", `{"type":"log","lines":1}`,
		"--type", "error",
		"--lines", "5",
	}

	// When
	params, _, err := buildParams(args, nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if params["type"] != "error" || params["lines"] != 5 {
		t.Fatalf("params=%#v", params)
	}
}
