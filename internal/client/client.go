package client

import (
	"encoding/json"
	"fmt"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/paths"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

// sharedHTTPClient is the package-level singleton used by all Send() calls.
// Reusing it lets the connection pool keep idle keep-alive sockets open
// instead of allocating a fresh dialer + TCP handshake on every call.
// Per-request timeout comes from Request.Context so the singleton's own
// Timeout stays unset.
var sharedHTTPClient = &http.Client{
	Transport: &http.Transport{
		DialContext: (&net.Dialer{
			Timeout:   2 * time.Second,
			KeepAlive: 15 * time.Second,
		}).DialContext,
		MaxIdleConns:        8,
		MaxIdleConnsPerHost: 4,
		IdleConnTimeout:     30 * time.Second,
		DisableCompression:  true, // localhost, gzip cost > benefit
	},
}

// IsProcessDead is the public probe used by polling commands that want to
// detect a crashed Unity Editor without waiting for the heartbeat to stale.
// Returns true only when the OS confirms the process is gone.
func (c *Client) IsProcessDead(pid int) bool { return c.processDeadChecker(pid) }

// IsProcessDead delegates to DefaultClient.IsProcessDead.
func IsProcessDead(pid int) bool { return DefaultClient.IsProcessDead(pid) }

// ClearInstanceCache discards the cached scan result so the next call to
// ScanInstances reads from disk again. Useful in tests and after explicit
// install/uninstall flows where the cache could mask new state.
func (c *Client) ClearInstanceCache() { c.cache.Clear() }

// ClearInstanceCache delegates to DefaultClient.ClearInstanceCache.
func ClearInstanceCache() { DefaultClient.ClearInstanceCache() }

// ScanInstances reads all instance files from ~/.hera-agent-unity/instances/.
// Stale files whose PID is no longer running are automatically removed.
// Results are cached for instanceCacheTTL to keep multi-step workflows
// (batch, exec → console → exec, etc.) from re-stat'ing the dir on every hop.
func (c *Client) ScanInstances() ([]Instance, error) {
	return c.scanInstances(true)
}

// ScanInstancesFresh reads instance files directly instead of using the
// short-lived process cache. Transition polling and reload retry use this
// path so they can observe a new heartbeat or port binding immediately.
func (c *Client) ScanInstancesFresh() ([]Instance, error) {
	return c.scanInstances(false)
}

func (c *Client) scanInstances(useCache bool) ([]Instance, error) {
	if useCache {
		if cached, ok := c.cache.Get(); ok {
			return cached, nil
		}
	}

	dir := paths.InstancesDir()
	entries, err := os.ReadDir(dir)
	if err != nil {
		return nil, err
	}

	var instances []Instance
	for _, e := range entries {
		if e.IsDir() || !strings.HasSuffix(e.Name(), ".json") {
			continue
		}
		fp := filepath.Join(dir, e.Name())
		data, err := os.ReadFile(fp)
		if err != nil {
			continue
		}
		var inst Instance
		if err := json.Unmarshal(data, &inst); err != nil {
			continue
		}
		if inst.PID > 0 && c.processDeadChecker(inst.PID) {
			_ = os.Remove(fp)
			continue
		}
		instances = append(instances, inst)
	}

	if useCache {
		c.cache.Set(instances)
	}
	return instances, nil
}

// ScanInstances delegates to DefaultClient.ScanInstances.
func ScanInstances() ([]Instance, error) { return DefaultClient.ScanInstances() }

// ScanInstancesFresh delegates to DefaultClient.ScanInstancesFresh.
func ScanInstancesFresh() ([]Instance, error) { return DefaultClient.ScanInstancesFresh() }

// FindByPort scans instance files and returns the instance matching the given port.
// If multiple instances share the same port, the one with the most recent timestamp wins.
func (c *Client) FindByPort(port int) (*Instance, error) {
	return c.findByPort(port, false, false)
}

// FindByPortFresh finds an instance from the current heartbeat files.
// It is reserved for state-transition polling where a cached heartbeat may
// hide a new port or state.
func (c *Client) FindByPortFresh(port int) (*Instance, error) {
	return c.findByPort(port, false, true)
}

func (c *Client) findByPort(port int, active, fresh bool) (*Instance, error) {
	var (
		instances []Instance
		err       error
	)
	if fresh {
		instances, err = c.ScanInstancesFresh()
	} else {
		instances, err = c.ScanInstances()
	}
	if err != nil {
		return nil, err
	}
	var best *Instance
	for i, inst := range instances {
		if inst.Port != port || (active && !isActiveInstance(inst)) {
			continue
		}
		if best == nil || inst.Timestamp > best.Timestamp {
			best = &instances[i]
		}
	}
	if best == nil {
		return nil, fmt.Errorf("no instance on port %d", port)
	}
	return best, nil
}

// FindByPort delegates to DefaultClient.FindByPort.
func FindByPort(port int) (*Instance, error) { return DefaultClient.FindByPort(port) }

// FindByPortFresh delegates to DefaultClient.FindByPortFresh.
func FindByPortFresh(port int) (*Instance, error) { return DefaultClient.FindByPortFresh(port) }

func isActiveInstance(inst Instance) bool {
	return inst.State != unitystate.Stopped && inst.Timestamp > 0
}

// FindActiveByPort is like FindByPort but skips stopped or incomplete instances.
// Used by polling paths (waitForAlive, waitForReady) that only care about live instances.
func (c *Client) FindActiveByPort(port int) (*Instance, error) {
	return c.findByPort(port, true, false)
}

// FindActiveByPort delegates to DefaultClient.FindActiveByPort.
func FindActiveByPort(port int) (*Instance, error) { return DefaultClient.FindActiveByPort(port) }

func (c *Client) FindActiveByPortFresh(port int) (*Instance, error) {
	return c.findByPort(port, true, true)
}

func FindActiveByPortFresh(port int) (*Instance, error) {
	return DefaultClient.FindActiveByPortFresh(port)
}

// DiscoverInstance finds a running Unity instance from ~/.hera-agent-unity/instances/.
// If port > 0, matches an active instance by port.
// If project is set, matches by project path substring.
// Otherwise returns the most recently active instance.
func (c *Client) DiscoverInstance(project string, port int) (*Instance, error) {
	return c.discoverInstance(project, port, false)
}

// DiscoverInstanceFresh resolves an instance directly from heartbeat files.
// Normal command setup remains cached; this is for reload retry and polling.
func (c *Client) DiscoverInstanceFresh(project string, port int) (*Instance, error) {
	return c.discoverInstance(project, port, true)
}

func (c *Client) discoverInstance(project string, port int, fresh bool) (*Instance, error) {
	if port > 0 {
		if fresh {
			return c.FindActiveByPortFresh(port)
		}
		return c.FindActiveByPort(port)
	}

	var (
		instances []Instance
		err       error
	)
	if fresh {
		instances, err = c.ScanInstancesFresh()
	} else {
		instances, err = c.ScanInstances()
	}
	if err != nil {
		return nil, fmt.Errorf("no Unity instances found.\nIs Unity running with the Connector package?\nExpected: %s", paths.InstancesDir())
	}

	// Filter out stopped instances
	var alive []Instance
	for _, inst := range instances {
		if !isActiveInstance(inst) {
			continue
		}
		alive = append(alive, inst)
	}

	if len(alive) == 0 {
		return nil, fmt.Errorf("no Unity instances running")
	}

	if project != "" {
		for _, inst := range alive {
			if strings.Contains(filepath.ToSlash(inst.ProjectPath), filepath.ToSlash(project)) {
				return &inst, nil
			}
		}
		return nil, fmt.Errorf("no Unity instance found for project: %s", project)
	}

	// Try to match by current working directory before falling back to timestamp
	if cwd, err := os.Getwd(); err == nil {
		cwdNorm := filepath.ToSlash(cwd)
		for _, inst := range alive {
			projNorm := filepath.ToSlash(inst.ProjectPath)
			if cwdNorm == projNorm || strings.HasPrefix(cwdNorm, projNorm+"/") {
				return &inst, nil
			}
		}
	}

	// Return the most recently updated
	best := alive[0]
	for _, inst := range alive[1:] {
		if inst.Timestamp > best.Timestamp {
			best = inst
		}
	}
	return &best, nil
}

// DiscoverInstance delegates to DefaultClient.DiscoverInstance.
func DiscoverInstance(project string, port int) (*Instance, error) {
	return DefaultClient.DiscoverInstance(project, port)
}

// DiscoverInstanceFresh delegates to DefaultClient.DiscoverInstanceFresh.
func DiscoverInstanceFresh(project string, port int) (*Instance, error) {
	return DefaultClient.DiscoverInstanceFresh(project, port)
}
