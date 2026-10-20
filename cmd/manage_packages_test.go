package cmd

import (
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func TestPackageJobWaitsForPositiveFallbackPort(t *testing.T) {
	fixtureHome := t.TempDir()
	t.Setenv("USERPROFILE", fixtureHome)
	t.Setenv("HOME", fixtureHome)
	resultPath := paths.PackageResultPath(8091, "pkg-port-zero")
	if err := os.MkdirAll(filepath.Dir(resultPath), 0755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(resultPath, []byte(`{"success":true,"message":"installed"}`), 0600); err != nil {
		t.Fatal(err)
	}
	resolveCalls, sendCalls := 0, 0
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	resp, err := managePackagesCmd(ctx, []string{"add", "--identifier", "fixture"}, packageRuntime{
		Config: GlobalConfig{Timeout: time.Second},
		Send: func(string, any) (*client.CommandResponse, error) {
			sendCalls++
			return &client.CommandResponse{Success: true, Message: "running", Data: json.RawMessage(`{"job_id":"pkg-port-zero","port":0}`)}, nil
		},
		Resolve: func() (*client.Instance, error) {
			resolveCalls++
			port := 0
			if resolveCalls > 1 {
				port = 8091
			}
			return &client.Instance{Port: port, State: "reloading", Timestamp: time.Now().UnixMilli()}, nil
		},
	})
	if err != nil || resp == nil || !resp.Success || resp.Message != "installed" {
		t.Fatalf("response=%+v err=%v resolves=%d sends=%d", resp, err, resolveCalls, sendCalls)
	}
	if sendCalls != 1 || resolveCalls < 2 {
		t.Fatalf("resolves=%d sends=%d; must wait without resending mutation", resolveCalls, sendCalls)
	}
}

func TestPackageJobPortWaitIsBoundedAndDoesNotResend(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	sends := 0
	_, err := managePackagesCmd(ctx, []string{"add", "--identifier", "fixture"}, packageRuntime{
		Config: GlobalConfig{Timeout: time.Second},
		Send: func(string, any) (*client.CommandResponse, error) {
			sends++
			return &client.CommandResponse{Success: true, Message: "running", Data: json.RawMessage(`{"job_id":"pkg-cancel","port":0}`)}, nil
		},
		Resolve: func() (*client.Instance, error) {
			cancel()
			return &client.Instance{Port: 0, State: "reloading", Timestamp: time.Now().UnixMilli()}, nil
		},
	})
	defer cancel()
	if !errors.Is(err, context.Canceled) || sends != 1 {
		t.Fatalf("err=%v sends=%d", err, sends)
	}
}

func TestPackageJobExplicitPortDoesNotResolve(t *testing.T) {
	fixtureHome := t.TempDir()
	t.Setenv("USERPROFILE", fixtureHome)
	t.Setenv("HOME", fixtureHome)
	resultPath := paths.PackageResultPath(8092, "pkg-explicit")
	if err := os.MkdirAll(filepath.Dir(resultPath), 0755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(resultPath, []byte(`{"success":true}`), 0600); err != nil {
		t.Fatal(err)
	}
	resp, err := managePackagesCmd(context.Background(), []string{"add"}, packageRuntime{
		Send: func(string, any) (*client.CommandResponse, error) {
			return &client.CommandResponse{Success: true, Message: "running", Data: json.RawMessage(`{"job_id":"pkg-explicit","port":8092}`)}, nil
		},
		Resolve: func() (*client.Instance, error) {
			t.Fatal("explicit result key must not follow a new port")
			return nil, nil
		},
	})
	if err != nil || !resp.Success {
		t.Fatalf("response=%+v err=%v", resp, err)
	}
}

func TestPackageJobMissingPortTimesOutWithoutResending(t *testing.T) {
	sends := 0
	_, err := managePackagesCmd(context.Background(), []string{"add"}, packageRuntime{
		Config: GlobalConfig{Timeout: 20 * time.Millisecond},
		Send: func(string, any) (*client.CommandResponse, error) {
			sends++
			return &client.CommandResponse{Success: true, Message: "running", Data: json.RawMessage(`{"job_id":"pkg-timeout","port":0}`)}, nil
		},
		Resolve: func() (*client.Instance, error) {
			return &client.Instance{Port: 0, Timestamp: time.Now().UnixMilli()}, nil
		},
	})
	if err == nil || sends != 1 {
		t.Fatalf("err=%v sends=%d", err, sends)
	}
}
