package cmd

import (
	"bufio"
	"bytes"
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"testing"
	"time"
)

func TestVerifyChecksum(t *testing.T) {
	path := filepath.Join(t.TempDir(), "binary")
	if err := os.WriteFile(path, []byte("binary"), 0600); err != nil {
		t.Fatal(err)
	}
	hash := fmt.Sprintf("%x", sha256.Sum256([]byte("binary")))
	for _, tc := range []struct {
		name, sums string
		valid      bool
	}{
		{"match", hash + "  asset.exe\n", true},
		{"binary marker", hash + " *asset.exe\r\n", true},
		{"mismatch", strings.Repeat("0", 64) + "  asset.exe\n", false},
		{"missing filename", hash + "  other.exe\n", false},
		{"empty", "", false},
		{"invalid hash", "oops  asset.exe\n", false},
		{"duplicate", hash + "  asset.exe\n" + hash + "  asset.exe\n", false},
	} {
		t.Run(tc.name, func(t *testing.T) {
			if err := verifyChecksum(path, "asset.exe", strings.NewReader(tc.sums)); (err == nil) != tc.valid {
				t.Fatalf("err=%v", err)
			}
		})
	}
	if findAsset([]githubAsset{{Name: "SHA256SUMS"}}) != nil {
		t.Fatal("selected checksum as binary")
	}
}

func TestFindAsset(t *testing.T) {
	assets := []githubAsset{
		{Name: "hera-agent-unity-linux-amd64"},
		{Name: "hera-agent-unity-darwin-arm64"},
		{Name: "hera-agent-unity-windows-amd64.exe"},
	}

	// findAsset uses runtime.GOOS/GOARCH, so we just verify it returns something on the current platform
	got := findAsset(assets)
	if got == nil {
		t.Error("findAsset: should find asset for current platform")
	}

	empty := findAsset(nil)
	if empty != nil {
		t.Error("findAsset: should return nil for empty list")
	}

	noMatch := []githubAsset{{Name: "hera-agent-unity-plan9-mips"}}
	got = findAsset(noMatch)
	if got != nil {
		t.Error("findAsset: should return nil when no platform match")
	}
}

func TestUpdateReleaseEndToEnd(t *testing.T) {
	payload, err := os.ReadFile("testdata/update/payload.txt")
	if err != nil {
		t.Fatal(err)
	}
	golden, err := os.ReadFile("testdata/update/SHA256SUMS.golden")
	if err != nil {
		t.Fatal(err)
	}
	name := "hera-agent-unity-" + runtime.GOOS + "-" + runtime.GOARCH
	if runtime.GOOS == "windows" {
		name += ".exe"
	}
	oldVersion := Version
	Version = "v0.0.1"
	t.Cleanup(func() { Version = oldVersion })
	for _, tc := range []struct {
		name, errorText string
		missing         bool
		running         bool
		sums            []byte
	}{
		{name: "match GNU golden", sums: golden},
		{name: "running Windows executable", sums: golden, running: true},
		{name: "mismatch", sums: []byte(strings.Repeat("0", 64) + "  " + name + "\n"), errorText: "checksum mismatch"},
		{name: "missing asset", missing: true, errorText: "missing SHA256SUMS"},
		{name: "missing filename", sums: []byte(strings.Repeat("0", 64) + "  other-binary\n"), errorText: "has no checksum"},
	} {
		t.Run(tc.name, func(t *testing.T) {
			if tc.running && runtime.GOOS != "windows" {
				t.Skip("Windows memory-mapped executable deletion")
			}
			dir := t.TempDir()
			target := filepath.Join(dir, "hera-agent-unity.exe")
			original := []byte("original executable\x00bytes")
			if tc.running {
				executable, err := os.Executable()
				if err != nil {
					t.Fatal(err)
				}
				original, err = os.ReadFile(executable)
				if err != nil {
					t.Fatal(err)
				}
			}
			for path, data := range map[string][]byte{target: original, target + ".bak": []byte("stale backup"), filepath.Join(dir, "hera-agent-unity-update-stale.tmp"): []byte("stale download")} {
				if err := os.WriteFile(path, data, 0600); err != nil {
					t.Fatal(err)
				}
			}
			var child *exec.Cmd
			var childInput io.WriteCloser
			if tc.running {
				child = exec.Command(target, "-test.run=^TestUpdateBinaryProcess$")
				child.Env = append(os.Environ(), "HERA_UPDATE_TEST_CHILD=1")
				childInput, err = child.StdinPipe()
				if err != nil {
					t.Fatal(err)
				}
				output, err := child.StdoutPipe()
				if err != nil {
					t.Fatal(err)
				}
				if err := child.Start(); err != nil {
					t.Fatal(err)
				}
				t.Cleanup(func() {
					_ = childInput.Close()
					if child.ProcessState == nil {
						_ = child.Process.Kill()
						_ = child.Wait()
					}
				})
				ready := make(chan string, 1)
				go func() { line, _ := bufio.NewReader(output).ReadString('\n'); ready <- line }()
				select {
				case line := <-ready:
					if line != "ready\n" {
						t.Fatalf("helper did not start: %q", line)
					}
				case <-time.After(10 * time.Second):
					t.Fatal("helper startup timeout")
				}
			}
			var mu sync.Mutex
			var requests []string
			var server *httptest.Server
			server = httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				mu.Lock()
				requests = append(requests, r.URL.Path)
				mu.Unlock()
				switch r.URL.Path {
				case "/release":
					assets := []githubAsset{{Name: name, BrowserDownloadURL: server.URL + "/binary"}}
					if !tc.missing {
						assets = append(assets, githubAsset{Name: "SHA256SUMS", BrowserDownloadURL: server.URL + "/sums"})
					}
					if err := json.NewEncoder(w).Encode(githubRelease{TagName: "v99.0.0", Assets: assets}); err != nil {
						t.Error(err)
					}
				case "/binary":
					_, _ = w.Write(payload)
				case "/sums":
					_, _ = w.Write(tc.sums)
				default:
					http.NotFound(w, r)
				}
			}))
			defer server.Close()
			err := updateFromRelease(nil, server.URL+"/release", target)
			if tc.errorText == "" {
				if err != nil {
					t.Fatal(err)
				}
			} else if err == nil || !strings.Contains(err.Error(), tc.errorText) {
				t.Fatalf("error=%v, want %q", err, tc.errorText)
			}
			actual, readErr := os.ReadFile(target)
			if readErr != nil {
				t.Fatal(readErr)
			}
			expected := original
			if tc.errorText == "" {
				expected = payload
			}
			if !bytes.Equal(actual, expected) {
				t.Fatalf("target changed incorrectly: %q", actual)
			}
			if tc.running {
				if _, err := os.Stat(target + ".bak"); err != nil {
					t.Fatalf("running backup missing: %v", err)
				}
				_ = childInput.Close()
				if err := child.Wait(); err != nil {
					t.Fatal(err)
				}
				deadline := time.Now().Add(10 * time.Second)
				for time.Now().Before(deadline) {
					if _, err := os.Stat(target + ".bak"); os.IsNotExist(err) {
						break
					}
					time.Sleep(100 * time.Millisecond)
				}
			}
			entries, err := os.ReadDir(dir)
			if err != nil {
				t.Fatal(err)
			}
			if len(entries) != 1 || entries[0].Name() != filepath.Base(target) {
				t.Fatalf("backup/temp leftovers: %v", entries)
			}
			mu.Lock()
			gotRequests := strings.Join(requests, ",")
			mu.Unlock()
			wantRequests := "/release,/binary,/sums"
			if tc.missing {
				wantRequests = "/release,/binary"
			}
			if gotRequests != wantRequests {
				t.Fatalf("requests=%s, want %s", gotRequests, wantRequests)
			}
		})
	}
}

func TestUpdateBinaryProcess(t *testing.T) {
	if os.Getenv("HERA_UPDATE_TEST_CHILD") != "1" {
		return
	}
	fmt.Print("ready\n")
	_, _ = io.Copy(io.Discard, os.Stdin)
	os.Exit(0)
}
