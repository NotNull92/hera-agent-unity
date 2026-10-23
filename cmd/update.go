package cmd

import (
	"bufio"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

const repoAPI = "https://api.github.com/repos/NotNull92/hera-agent-unity/releases/latest"

// githubAPIClient bounds the release-check request. fetchLatestRelease is
// reached synchronously from printUpdateNotice after every human command once
// the 12h cache expires, so a black-holed connection must not hang an
// otherwise-instant status/doctor.
var githubAPIClient = &http.Client{Timeout: 15 * time.Second}

// githubDownloadClient bounds connect / TLS-handshake / response-header stalls
// without capping the body transfer itself, so a slow-but-progressing binary
// download on a poor link is not killed mid-stream.
var githubDownloadClient = &http.Client{
	Transport: &http.Transport{
		DialContext:           (&net.Dialer{Timeout: 10 * time.Second}).DialContext,
		TLSHandshakeTimeout:   10 * time.Second,
		ResponseHeaderTimeout: 30 * time.Second,
	},
}

type githubRelease struct {
	TagName string        `json:"tag_name"`
	Assets  []githubAsset `json:"assets"`
}

type githubAsset struct {
	Name               string `json:"name"`
	BrowserDownloadURL string `json:"browser_download_url"`
}

func updateCmd(args []string) error {
	parsedParams, _, err := buildParams(args, nil)
	if err != nil {
		return err
	}
	_, checkOnly := parsedParams["check"]

	fmt.Println("Checking for updates...")

	release, err := fetchLatestRelease()
	if err != nil {
		return fmt.Errorf("failed to check for updates: %w", err)
	}

	latest := release.TagName
	current := Version

	if !isNewerRelease(current, latest) {
		fmt.Printf("Already up to date (%s)\n", current)
		return nil
	}

	fmt.Printf("Update available: %s → %s\n", current, latest)

	if checkOnly {
		return nil
	}

	asset := findAsset(release.Assets)
	if asset == nil {
		return fmt.Errorf("no binary found for %s/%s", runtime.GOOS, runtime.GOARCH)
	}

	exe, err := os.Executable()
	if err != nil {
		return fmt.Errorf("cannot locate current binary: %w", err)
	}
	exe, err = filepath.EvalSymlinks(exe)
	if err != nil {
		return fmt.Errorf("cannot resolve binary path: %w", err)
	}

	// Sweep stale *.bak files left by prior failed/interrupted updates.
	// Without this, a subsequent update's `Rename(exe, exe+".bak")` fails
	// with "Access is denied" on Windows because the target already exists.
	sweepBackups(filepath.Dir(exe))

	fmt.Printf("Downloading %s...\n", asset.Name)

	tmpFile, err := download(asset.BrowserDownloadURL, filepath.Dir(exe))
	if err != nil {
		return fmt.Errorf("download failed: %w", err)
	}
	defer func() { _ = os.Remove(tmpFile) }()

	var checksumURL string
	for _, candidate := range release.Assets {
		if candidate.Name == "SHA256SUMS" {
			checksumURL = candidate.BrowserDownloadURL
			break
		}
	}
	if checksumURL == "" {
		return fmt.Errorf("release is missing SHA256SUMS")
	}
	sums, err := githubAPIClient.Get(checksumURL)
	if err != nil {
		return fmt.Errorf("download checksums: %w", err)
	}
	defer sums.Body.Close()
	if sums.StatusCode != http.StatusOK {
		return fmt.Errorf("checksum download returned %d", sums.StatusCode)
	}
	if err := verifyChecksum(tmpFile, asset.Name, sums.Body); err != nil {
		return err
	}

	if err := os.Chmod(tmpFile, 0755); err != nil {
		return fmt.Errorf("chmod failed: %w", err)
	}

	// Rename dance: backup → replace → cleanup, with restore on failure.
	backup := exe + ".bak"
	if err := os.Rename(exe, backup); err != nil {
		return fmt.Errorf("backup failed: %w", err)
	}

	if err := os.Rename(tmpFile, exe); err != nil {
		if restoreErr := os.Rename(backup, exe); restoreErr != nil {
			return fmt.Errorf("replace failed: %w (restore also failed: %v)", err, restoreErr)
		}
		return fmt.Errorf("replace failed: %w", err)
	}

	// On Windows the just-renamed .bak can still be memory-mapped by the
	// outgoing process, so direct removal fails with "Access is denied".
	// scheduleDelete falls back to a deferred PowerShell delete in that
	// case; otherwise .bak files accumulate across updates and clutter
	// installDir.
	if _, err := scheduleDelete(backup); err != nil {
		fmt.Fprintf(os.Stderr, "warning: could not remove backup %s: %v\n", backup, err)
	}

	fmt.Printf("Updated to %s\n", latest)
	return nil
}

func fetchLatestRelease() (*githubRelease, error) {
	resp, err := githubAPIClient.Get(repoAPI)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("GitHub API returned %d", resp.StatusCode)
	}

	var release githubRelease
	if err := json.NewDecoder(resp.Body).Decode(&release); err != nil {
		return nil, err
	}
	return &release, nil
}

// findAsset finds the release asset matching the current OS and architecture.
func findAsset(assets []githubAsset) *githubAsset {
	suffix := fmt.Sprintf("%s-%s", runtime.GOOS, runtime.GOARCH)
	for i, a := range assets {
		if strings.Contains(a.Name, suffix) {
			return &assets[i]
		}
	}
	return nil
}

// sweepBackups removes lingering hera-agent-unity*.bak files in the install dir.
// On Windows the in-use binary can hold a lock on the .bak rename target,
// so prior runs may have failed to delete it. Best-effort: log warnings,
// never fail the update.
func sweepBackups(dir string) {
	entries, err := os.ReadDir(dir)
	if err != nil {
		return
	}
	for _, e := range entries {
		name := e.Name()
		if !strings.HasPrefix(name, "hera-agent-unity") || (!strings.HasSuffix(name, ".bak") && !strings.HasSuffix(name, ".tmp")) {
			continue
		}
		path := filepath.Join(dir, name)
		if err := os.Remove(path); err != nil {
			fmt.Fprintf(os.Stderr, "warning: stale backup left in place: %s (%v)\n", path, err)
		}
	}
}

func download(url string, targetDir string) (string, error) {
	resp, err := githubDownloadClient.Get(url)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("download returned %d", resp.StatusCode)
	}

	tmp, err := os.CreateTemp(targetDir, "hera-agent-unity-update-*.tmp")
	if err != nil {
		return "", err
	}
	if _, err := io.Copy(tmp, resp.Body); err != nil {
		_ = tmp.Close()
		_ = os.Remove(tmp.Name())
		return "", err
	}
	if err := tmp.Close(); err != nil {
		_ = os.Remove(tmp.Name())
		return "", err
	}

	return tmp.Name(), nil
}

func verifyChecksum(path, name string, sums io.Reader) error {
	scanner := bufio.NewScanner(sums)
	var expected []byte
	for scanner.Scan() {
		fields := strings.Fields(scanner.Text())
		if len(fields) != 2 || strings.TrimPrefix(fields[1], "*") != name {
			continue
		}
		if expected != nil {
			return fmt.Errorf("duplicate checksum for %s", name)
		}
		digest, err := hex.DecodeString(fields[0])
		if err != nil || len(digest) != sha256.Size {
			return fmt.Errorf("invalid checksum for %s", name)
		}
		expected = digest
	}
	if err := scanner.Err(); err != nil {
		return fmt.Errorf("read checksums: %w", err)
	}
	if expected == nil {
		return fmt.Errorf("SHA256SUMS has no checksum for %s", name)
	}
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	defer file.Close()
	hash := sha256.New()
	if _, err := io.Copy(hash, file); err != nil {
		return err
	}
	if !strings.EqualFold(hex.EncodeToString(hash.Sum(nil)), hex.EncodeToString(expected)) {
		return fmt.Errorf("checksum mismatch for %s", name)
	}
	return nil
}
