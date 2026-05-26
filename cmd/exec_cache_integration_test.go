//go:build integration

package cmd

import (
	"encoding/json"
	"fmt"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// These tests require a running Unity Editor with the Hera Agent connector.
// Run: go test -tags integration ./...

const integrationTimeoutMs = 60000

func discover(t *testing.T) *client.Instance {
	t.Helper()
	// Heartbeat rewrites the instance JSON every 500ms. Even with an atomic
	// rename, a scan that races the swap may transiently see no instances.
	// Retry briefly before declaring Unity unavailable.
	var lastErr error
	for attempt := 0; attempt < 5; attempt++ {
		inst, err := client.DiscoverInstance("", 0)
		if err == nil {
			return inst
		}
		lastErr = err
		time.Sleep(150 * time.Millisecond)
	}
	t.Skipf("no Unity instance available: %v", lastErr)
	return nil
}

func sendExec(t *testing.T, code string, noCache bool) *client.CommandResponse {
	t.Helper()
	inst := discover(t)
	params := map[string]interface{}{"code": code}
	if noCache {
		params["no_cache"] = true
	}
	resp, err := client.Send(inst, "exec", params, integrationTimeoutMs)
	if err != nil {
		t.Fatalf("send exec: %v", err)
	}
	if !resp.Success {
		t.Fatalf("exec failed: %s", resp.Message)
	}
	return resp
}

func uniqueCode(seed int64, i int) string {
	return fmt.Sprintf("return %d + %d;", seed, i)
}

// TestExecCacheColdWarm verifies that an identical second call skips compile
// and load (in-memory Assembly cache hit).
func TestExecCacheColdWarm(t *testing.T) {
	code := uniqueCode(time.Now().UnixNano(), 0)

	cold := sendExec(t, code, false)
	t.Logf("cold timings: %+v", cold.Timings)
	if got := cold.Timings["cache"]; got != 0 {
		t.Errorf("cold cache = %d, want 0 (compile)", got)
	}

	warm := sendExec(t, code, false)
	t.Logf("warm timings: %+v", warm.Timings)

	if got := warm.Timings["cache"]; got != 2 {
		t.Errorf("warm cache = %d, want 2 (memory hit)", got)
	}
	if got := warm.Timings["compile_ms"]; got != 0 {
		t.Errorf("warm compile_ms = %d, want 0", got)
	}
}

// TestExecCacheNoCacheFlag verifies --no-cache forces a compile even after a
// prior warm call.
func TestExecCacheNoCacheFlag(t *testing.T) {
	code := uniqueCode(time.Now().UnixNano(), 0)

	_ = sendExec(t, code, false) // prime cache
	_ = sendExec(t, code, false) // confirm warm

	nc := sendExec(t, code, true)
	t.Logf("no-cache timings: %+v", nc.Timings)
	if got := nc.Timings["cache"]; got != 0 {
		t.Errorf("no-cache cache = %d, want 0 (forced compile)", got)
	}
}

// TestExecCacheLRUEviction generates more distinct sources than the in-memory
// cache capacity (32) so the oldest entry is evicted. The re-issue of the
// oldest source must miss memory but should still skip compile via the
// on-disk DLL cache.
func TestExecCacheLRUEviction(t *testing.T) {
	const inMemoryCap = 32
	const overflow = 8
	seed := time.Now().UnixNano()

	first := uniqueCode(seed, 0)
	_ = sendExec(t, first, false)

	for i := 1; i < inMemoryCap+overflow; i++ {
		_ = sendExec(t, uniqueCode(seed, i), false)
	}

	re := sendExec(t, first, false)
	t.Logf("re-issue of evicted entry: %+v", re.Timings)

	if got := re.Timings["cache"]; got != 1 {
		t.Errorf("evicted re-issue cache = %d, want 1 (disk hit) — eviction or disk lookup failed", got)
	}
}

// TestExecCacheALCStable checks that bursts of --no-cache calls do not leak
// AssemblyLoadContexts. Each transient ALC must be unloaded after Invoke.
// Skips on Mono — collectible AssemblyLoadContext.Unload() is a no-op on
// Unity's Mono runtime, so growth is expected and unrelated to our code.
func TestExecCacheALCStable(t *testing.T) {
	runtimeResp := sendExec(t, "return System.Type.GetType(\"Mono.Runtime\") != null ? \"Mono\" : \"CoreCLR\";", false)
	var runtime string
	if err := json.Unmarshal(runtimeResp.Data, &runtime); err != nil {
		t.Fatalf("decode runtime: %v", err)
	}
	if runtime == "Mono" {
		t.Skip("Mono runtime: collectible AssemblyLoadContext.Unload is unsupported; skipping leak check")
	}

	const burst = 30
	const allowedGrowth = 8

	probe := "GC.Collect(); GC.WaitForPendingFinalizers(); return AppDomain.CurrentDomain.GetAssemblies().Length;"

	before := readAsmCount(t, sendExec(t, probe, true))
	t.Logf("assembly count before burst: %d", before)

	seed := time.Now().UnixNano()
	for i := 0; i < burst; i++ {
		_ = sendExec(t, uniqueCode(seed, i), true)
	}

	after := readAsmCount(t, sendExec(t, probe, true))
	t.Logf("assembly count after burst: %d (delta=%d)", after, after-before)

	if after-before > allowedGrowth {
		t.Errorf("assembly count grew by %d after %d --no-cache calls — ALC unload may not be working", after-before, burst)
	}
}

func readAsmCount(t *testing.T, resp *client.CommandResponse) int {
	t.Helper()
	var n float64
	if err := json.Unmarshal(resp.Data, &n); err != nil {
		t.Fatalf("decode assembly count: %v (data=%s)", err, string(resp.Data))
	}
	return int(n)
}
