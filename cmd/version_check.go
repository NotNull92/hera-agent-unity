package cmd

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

const checkInterval = 12 * time.Hour

var fetchLatestReleaseFn = fetchLatestRelease

type versionCache struct {
	CheckedAt int64  `json:"checked_at"`
	Latest    string `json:"latest,omitempty"`
	Outdated  bool   `json:"outdated,omitempty"`
}

func cacheFilePath() string {
	return paths.VersionCheckPath()
}

func loadCache(path string) (*versionCache, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}
	var c versionCache
	if err := json.Unmarshal(data, &c); err != nil {
		return nil, err
	}
	return &c, nil
}

func saveCache(path string, c *versionCache) {
	dir := filepath.Dir(path)
	_ = os.MkdirAll(dir, 0755)
	data, err := json.Marshal(c)
	if err != nil {
		return
	}
	_ = os.WriteFile(path, data, 0644)
}

func isNewerRelease(current, latest string) bool {
	if latest == "" || latest == current {
		return false
	}

	currentVersion, currentOK := parseReleaseTag(current)
	latestVersion, latestOK := parseReleaseTag(latest)
	if !latestOK {
		return false
	}
	if !currentOK {
		return true
	}

	for i := range latestVersion {
		if latestVersion[i] > currentVersion[i] {
			return true
		}
		if latestVersion[i] < currentVersion[i] {
			return false
		}
	}
	return false
}

func parseReleaseTag(tag string) ([3]int, bool) {
	var version [3]int
	raw := strings.TrimPrefix(tag, "v")
	if core, _, ok := strings.Cut(raw, "-"); ok {
		raw = core
	}
	parts := strings.Split(raw, ".")
	if len(parts) != len(version) {
		return version, false
	}
	for i, part := range parts {
		n, err := strconv.Atoi(part)
		if err != nil || n < 0 {
			return version, false
		}
		version[i] = n
	}
	return version, true
}

// printUpdateNotice checks for a newer version and prints a notice if available.
// Silently does nothing on any error (no network, bad cache, etc.).
func printUpdateNotice(category string) {
	if Version == "dev" || flagQuiet || !isHumanCommand(category) {
		return
	}

	path := cacheFilePath()
	if path == "" {
		return
	}

	now := time.Now().Unix()
	cache, _ := loadCache(path)
	latestNotice := ""

	if cache != nil && cache.Outdated && isNewerRelease(Version, cache.Latest) {
		latestNotice = cache.Latest
	}

	if cache != nil && now-cache.CheckedAt < int64(checkInterval.Seconds()) {
		if latestNotice != "" {
			printNotice(Version, latestNotice)
		}
		return
	}

	// Fetch from GitHub
	release, err := fetchLatestReleaseFn()
	if err != nil {
		// Network error — save timestamp so we don't retry immediately
		if cache != nil {
			cache.CheckedAt = now
			saveCache(path, cache)
		} else {
			saveCache(path, &versionCache{CheckedAt: now})
		}
		if latestNotice != "" {
			printNotice(Version, latestNotice)
		}
		return
	}

	nextCache := &versionCache{
		CheckedAt: now,
		Latest:    release.TagName,
		Outdated:  isNewerRelease(Version, release.TagName),
	}
	saveCache(path, nextCache)

	if nextCache.Outdated {
		latestNotice = release.TagName
	} else {
		latestNotice = ""
	}

	if latestNotice != "" {
		printNotice(Version, latestNotice)
	}
}

func printNotice(current, latest string) {
	fmt.Fprintf(os.Stderr, "\nUpdate available: %s → %s\nRun \"hera-agent-unity update\" to upgrade.\n", current, latest)
}
