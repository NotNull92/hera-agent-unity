package cmd

import (
	"errors"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func TestRunLegacyToolCommandPreservesDynamicPassthrough(t *testing.T) {
	var command string
	var params map[string]interface{}
	response, err := runLegacyToolCommand("custom_probe", []string{"--count", "3", "target"}, func(gotCommand string, raw interface{}) (*client.CommandResponse, error) {
		command = gotCommand
		params = raw.(map[string]interface{})
		return &client.CommandResponse{Success: true}, nil
	})
	if err != nil || response == nil || command != "custom_probe" {
		t.Fatalf("response=%#v command=%q error=%v", response, command, err)
	}
	if params["count"] != 3 {
		t.Fatalf("count=%#v", params["count"])
	}
	args, ok := params["args"].([]string)
	if !ok || len(args) != 1 || args[0] != "target" {
		t.Fatalf("args=%#v", params["args"])
	}
}

func TestRunLegacyExecMapsCheckToCompileOnly(t *testing.T) {
	var params map[string]interface{}
	_, err := runLegacyToolCommand("exec", []string{"return 1;", "--check"}, func(_ string, raw interface{}) (*client.CommandResponse, error) {
		params = raw.(map[string]interface{})
		return &client.CommandResponse{Success: true}, nil
	})
	if err != nil {
		t.Fatal(err)
	}
	if params["compile_only"] != true {
		t.Fatalf("compile_only=%#v", params["compile_only"])
	}
	if _, exists := params["check"]; exists {
		t.Fatalf("legacy check leaked into Unity params: %#v", params)
	}
}

func TestRunLegacyToolCommandWrapsTransportError(t *testing.T) {
	_, err := runLegacyToolCommand("custom_probe", nil, func(string, interface{}) (*client.CommandResponse, error) {
		return nil, errors.New("offline")
	})
	if err == nil || err.Error() != `invoke legacy tool "custom_probe": offline` {
		t.Fatalf("error=%v", err)
	}
}
