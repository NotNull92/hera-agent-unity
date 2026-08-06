package cmd

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"net"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

func writeInstanceFile(t *testing.T, inst client.Instance) string {
	t.Helper()
	client.ClearInstanceCache()
	t.Cleanup(client.ClearInstanceCache)

	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)
	dir := paths.InstancesDir()
	if err := os.MkdirAll(dir, 0755); err != nil {
		t.Fatalf("failed to create instances dir: %v", err)
	}
	data, err := json.Marshal(inst)
	if err != nil {
		t.Fatalf("failed to marshal instance: %v", err)
	}
	// Use a fixed filename for testing
	path := filepath.Join(dir, "test.json")
	if err := os.WriteFile(path, data, 0644); err != nil {
		t.Fatalf("failed to write instance file: %v", err)
	}
	return home
}

func TestWaitForAlive_FollowsResolverPortChange(t *testing.T) {
	origPollInterval := statusPollBaseInterval
	statusPollBaseInterval = 5 * time.Millisecond
	t.Cleanup(func() { statusPollBaseInterval = origPollInterval })

	project := "C:/WorkSpace/ProjectMaid"
	callCount := 0
	resolve := func() (*client.Instance, error) {
		callCount++
		if callCount == 1 {
			return &client.Instance{
				State:       unitystate.Reloading,
				ProjectPath: project,
				Port:        8090,
				Timestamp:   time.Now().Add(-5 * time.Second).UnixMilli(),
			}, nil
		}
		return &client.Instance{
			State:       unitystate.Ready,
			ProjectPath: project,
			Port:        8091,
			Timestamp:   time.Now().UnixMilli(),
		}, nil
	}

	inst, err := waitForAlive(context.Background(), resolve, 100, "status")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if inst.Port != 8091 {
		t.Fatalf("expected updated port 8091, got %d", inst.Port)
	}
	if callCount < 2 {
		t.Fatalf("expected resolver to be called multiple times, got %d", callCount)
	}
}

func TestWaitForAlive_WhenRootContextCancelled_ReturnsCancellation(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	resolve := func() (*client.Instance, error) {
		return &client.Instance{Timestamp: time.Now().Add(-time.Second).UnixMilli()}, nil
	}

	_, err := waitForAlive(ctx, resolve, 60_000, "status")
	if !errors.Is(err, context.Canceled) {
		t.Fatalf("waitForAlive error = %v, want context cancellation", err)
	}
}

func TestWaitForInstance_recovers_when_resolver_is_temporarily_empty(t *testing.T) {
	// Given
	originalInterval := statusPollBaseInterval
	statusPollBaseInterval = time.Millisecond
	t.Cleanup(func() { statusPollBaseInterval = originalInterval })
	attempts := 0
	resolve := func() (*client.Instance, error) {
		attempts++
		if attempts == 1 {
			return nil, errors.New("no Unity instances running")
		}
		return &client.Instance{State: unitystate.Ready, Port: 8090}, nil
	}

	// When
	instance, err := waitForInstance(context.Background(), resolve, 100)

	// Then
	if err != nil {
		t.Fatalf("waitForInstance error = %v, want retry success", err)
	}
	if instance.Port != 8090 {
		t.Fatalf("instance.Port = %d, want 8090", instance.Port)
	}
	if attempts != 2 {
		t.Fatalf("attempts = %d, want 2", attempts)
	}
}

func TestWaitForInstance_fails_immediately_for_target_mismatch(t *testing.T) {
	start := time.Now()
	resolve := func() (*client.Instance, error) {
		return nil, &client.TargetMismatchError{
			Port:            8091,
			ExpectedProject: "/projects/requested",
			ActualProject:   "/projects/other",
		}
	}

	_, err := waitForInstance(context.Background(), resolve, 200)
	var mismatch *client.TargetMismatchError
	if !errors.As(err, &mismatch) {
		t.Fatalf("error = %v, want TargetMismatchError", err)
	}
	if elapsed := time.Since(start); elapsed >= 100*time.Millisecond {
		t.Fatalf("terminal mismatch took %s, want immediate failure", elapsed)
	}
}

func TestInitialDiscoveryTimeout_preserves_unbounded_request_timeout(t *testing.T) {
	if got := initialDiscoveryTimeoutMs(0); got != instanceDiscoveryTimeoutMs {
		t.Fatalf("initialDiscoveryTimeoutMs(0) = %d, want %d", got, instanceDiscoveryTimeoutMs)
	}
	if got := initialDiscoveryTimeoutMs(120); got != 120 {
		t.Fatalf("initialDiscoveryTimeoutMs(120) = %d, want 120", got)
	}
	if got := initialDiscoveryTimeoutMs(60_000); got != instanceDiscoveryTimeoutMs {
		t.Fatalf("initialDiscoveryTimeoutMs(60000) = %d, want %d", got, instanceDiscoveryTimeoutMs)
	}
}

func TestWaitForAlive_returns_stale_instance_when_state_is_stable(t *testing.T) {
	// Given
	resolve := func() (*client.Instance, error) {
		return &client.Instance{
			State:     unitystate.Ready,
			Port:      8090,
			Timestamp: time.Now().Add(-10 * time.Second).UnixMilli(),
		}, nil
	}

	// When
	instance, err := waitForAlive(context.Background(), resolve, 1, "exec")

	// Then
	if err != nil {
		t.Fatalf("waitForAlive error = %v, want stable instance", err)
	}
	if instance.Port != 8090 {
		t.Fatalf("instance.Port = %d, want 8090", instance.Port)
	}
}

func TestDiscoverStatusInstance_PortAllowsStoppedInstance(t *testing.T) {
	want := client.Instance{
		State:       unitystate.Stopped,
		ProjectPath: "/home/user/MyProject",
		Port:        8090,
		PID:         os.Getpid(),
		Timestamp:   1000000,
	}

	writeInstanceFile(t, want)

	got, err := discoverStatusInstance("", 8090)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if got.State != unitystate.Stopped {
		t.Errorf("State: got %q, want stopped", got.State)
	}
	if got.Port != 8090 {
		t.Errorf("Port: got %d, want 8090", got.Port)
	}
}

func TestDiscoverStatusInstance_ProjectAndPortRejectDifferentProject(t *testing.T) {
	want := client.Instance{
		State:       unitystate.Ready,
		ProjectPath: "/projects/other",
		Port:        8090,
		PID:         os.Getpid(),
		Timestamp:   time.Now().UnixMilli(),
	}

	writeInstanceFile(t, want)

	_, err := discoverStatusInstance("/projects/requested", 8090)
	var mismatch *client.TargetMismatchError
	if !errors.As(err, &mismatch) {
		t.Fatalf("error = %v, want TargetMismatchError", err)
	}
}

func TestMakeFreshResolver_FollowsSelectedProjectInsteadOfOriginalPort(t *testing.T) {
	want := client.Instance{
		State:       unitystate.Ready,
		ProjectPath: "/projects/selected",
		Port:        8091,
		PID:         os.Getpid(),
		Timestamp:   time.Now().UnixMilli(),
	}

	writeInstanceFile(t, want)
	competing := client.Instance{
		State:       unitystate.Ready,
		ProjectPath: "/projects/newer-but-not-selected",
		Port:        8092,
		PID:         os.Getpid(),
		Timestamp:   want.Timestamp + 1000,
	}
	data, err := json.Marshal(competing)
	if err != nil {
		t.Fatalf("marshal competing instance: %v", err)
	}
	if err := os.WriteFile(filepath.Join(paths.InstancesDir(), "competing.json"), data, 0o644); err != nil {
		t.Fatalf("write competing instance: %v", err)
	}

	resolve := makeFreshResolver(&client.Instance{
		ProjectPath: want.ProjectPath,
		Port:        8090,
	}, "", 8090)

	got, err := resolve()
	if err != nil {
		t.Fatalf("resolve error = %v", err)
	}
	if got.Port != want.Port || got.ProjectPath != want.ProjectPath {
		t.Fatalf("resolved target = %q on %d, want %q on %d",
			got.ProjectPath, got.Port, want.ProjectPath, want.Port)
	}
}

func TestStatusCmd_PrintsDocsAndCompiler(t *testing.T) {
	inst := client.Instance{
		State:        unitystate.Ready,
		ProjectPath:  "/home/user/MyProject",
		Port:         8090,
		PID:          os.Getpid(),
		UnityVersion: "6000.0.35f1",
		DocsVersion:  "6000.0",
		Compiler: &client.CompilerInfo{
			CscKind:    "unity_dotnet_sdk_roslyn",
			DotnetKind: "unity_netcore_runtime",
		},
		Timestamp: time.Now().UnixMilli(),
	}
	writeInstanceFile(t, inst)

	var out bytes.Buffer
	oldStdout := os.Stdout
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatal(err)
	}
	os.Stdout = w
	err = statusCmd(context.Background(), &inst)
	_ = w.Close()
	os.Stdout = oldStdout
	if err != nil {
		t.Fatalf("statusCmd returned error: %v", err)
	}
	if _, err := out.ReadFrom(r); err != nil {
		t.Fatal(err)
	}
	got := out.String()
	for _, want := range []string{
		"Version: 6000.0.35f1",
		"Docs:    6000.0",
		"Compiler: csc=unity_dotnet_sdk_roslyn dotnet=unity_netcore_runtime",
	} {
		if !strings.Contains(got, want) {
			t.Fatalf("status output missing %q in %q", want, got)
		}
	}
}

func TestStatusCmd_reports_reachable_when_heartbeat_is_stale_but_port_accepts_connections(t *testing.T) {
	// Given
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	t.Cleanup(func() { _ = listener.Close() })
	port := listener.Addr().(*net.TCPAddr).Port
	inst := client.Instance{
		State:        unitystate.Ready,
		ProjectPath:  "C:/Project",
		Port:         port,
		PID:          os.Getpid(),
		UnityVersion: "6000.0.35f1",
		Timestamp:    time.Now().Add(-10 * time.Second).UnixMilli(),
	}
	writeInstanceFile(t, inst)

	oldStderr := os.Stderr
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("stderr pipe: %v", err)
	}
	os.Stderr = w

	// When
	statusErr := statusCmd(context.Background(), &inst)
	_ = w.Close()
	os.Stderr = oldStderr

	// Then
	if statusErr != nil {
		t.Fatalf("statusCmd error = %v, want reachable status", statusErr)
	}
	var stderr bytes.Buffer
	if _, err := stderr.ReadFrom(r); err != nil {
		t.Fatalf("read stderr: %v", err)
	}
	got := stderr.String()
	if !strings.Contains(got, "reachable (heartbeat stale") {
		t.Fatalf("status output = %q, want reachable stale-heartbeat distinction", got)
	}
	if strings.Contains(got, "not responding") {
		t.Fatalf("status output = %q, must not report reachable port as not responding", got)
	}
}
