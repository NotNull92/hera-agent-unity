//go:build integration

package cmd

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func sendScene(t *testing.T, args []string, extra map[string]interface{}) *client.CommandResponse {
	t.Helper()
	inst := discover(t)
	params := map[string]interface{}{"args": args}
	for k, v := range extra {
		params[k] = v
	}
	resp, err := client.Send(context.Background(), inst, "scene", params, integrationTimeoutMs)
	if err != nil {
		t.Fatalf("send scene %v: %v", args, err)
	}
	return resp
}

type sceneInfoActive struct {
	Name    string `json:"name"`
	Path    string `json:"path"`
	IsDirty bool   `json:"isDirty"`
}

type sceneInfoEntry struct {
	Name     string `json:"name"`
	Path     string `json:"path"`
	IsLoaded bool   `json:"isLoaded"`
	IsDirty  bool   `json:"isDirty"`
}

type sceneInfoData struct {
	Active sceneInfoActive  `json:"active"`
	Loaded []sceneInfoEntry `json:"loaded"`
}

func TestSceneInfo(t *testing.T) {
	resp := sendScene(t, []string{"info"}, nil)
	if !resp.Success {
		t.Fatalf("scene info failed: %s", resp.Message)
	}
	var info sceneInfoData
	if err := json.Unmarshal(resp.Data, &info); err != nil {
		t.Fatalf("decode info: %v (data=%s)", err, string(resp.Data))
	}
	if info.Active.Name == "" {
		t.Errorf("active scene name is empty: %+v", info)
	}
	if len(info.Loaded) == 0 {
		t.Errorf("no loaded scenes reported")
	}
}

func TestSceneList(t *testing.T) {
	resp := sendScene(t, []string{"list"}, nil)
	if !resp.Success {
		t.Fatalf("scene list failed: %s", resp.Message)
	}
	var entries []map[string]interface{}
	if err := json.Unmarshal(resp.Data, &entries); err != nil {
		t.Fatalf("decode list: %v", err)
	}
	t.Logf("Build Settings scenes: %d", len(entries))
}

// TestSceneAdditiveLifecycle loads an additional copy of the active scene
// additively, then closes it. Uses the currently-active scene as the target
// so the test is project-agnostic.
func TestSceneAdditiveLifecycle(t *testing.T) {
	info := sendScene(t, []string{"info"}, nil)
	if !info.Success {
		t.Fatalf("precondition scene info failed: %s", info.Message)
	}
	var data sceneInfoData
	if err := json.Unmarshal(info.Data, &data); err != nil {
		t.Fatalf("decode info: %v", err)
	}
	if data.Active.Path == "" {
		t.Skip("no active scene path available")
	}

	// Active scene already loaded — additive load of the same path is a no-op
	// in newer Unity versions; skip when only one scene is registered and
	// already loaded.
	list := sendScene(t, []string{"list"}, nil)
	var entries []struct {
		Path    string `json:"path"`
		Enabled bool   `json:"enabled"`
	}
	if err := json.Unmarshal(list.Data, &entries); err != nil {
		t.Fatalf("decode list: %v", err)
	}
	var other string
	for _, e := range entries {
		if e.Path != data.Active.Path && e.Path != "" {
			other = e.Path
			break
		}
	}
	if other == "" {
		t.Skip("no second scene registered in Build Settings to exercise additive load")
	}

	loadResp := sendScene(t, []string{"load", other}, map[string]interface{}{"mode": "additive"})
	if !loadResp.Success {
		t.Fatalf("additive load failed: %s", loadResp.Message)
	}

	// Verify both scenes loaded.
	mid := sendScene(t, []string{"info"}, nil)
	var midData sceneInfoData
	if err := json.Unmarshal(mid.Data, &midData); err != nil {
		t.Fatalf("decode mid info: %v", err)
	}
	found := false
	for _, e := range midData.Loaded {
		if e.Path == other {
			found = true
			break
		}
	}
	if !found {
		t.Errorf("additively loaded scene %q not present in loaded list: %+v", other, midData.Loaded)
	}

	closeResp := sendScene(t, []string{"close", other}, nil)
	if !closeResp.Success {
		t.Errorf("close failed: %s", closeResp.Message)
	}
	var closeData struct {
		Name string `json:"name"`
		Path string `json:"path"`
	}
	if err := json.Unmarshal(closeResp.Data, &closeData); err != nil {
		t.Fatalf("decode close data: %v", err)
	}
	if closeData.Path != other {
		t.Errorf("close response path = %q, want %q (post-close fields must be captured before invalidation)", closeData.Path, other)
	}
}

func TestSceneCloseSoleSceneRefused(t *testing.T) {
	info := sendScene(t, []string{"info"}, nil)
	var data sceneInfoData
	if err := json.Unmarshal(info.Data, &data); err != nil {
		t.Fatalf("decode info: %v", err)
	}
	if len(data.Loaded) != 1 {
		t.Skipf("test requires exactly 1 loaded scene; got %d", len(data.Loaded))
	}

	resp := sendScene(t, []string{"close", data.Active.Path}, nil)
	if resp.Success {
		t.Errorf("close on sole scene unexpectedly succeeded: %+v", resp)
	}
}
