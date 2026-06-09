package client

import (
	"sync"
	"time"
)

// InstanceCache stores the last ScanInstances result for process-level reuse.
// A 5-second TTL is short enough that a stopped editor disappears quickly,
// but long enough that batch / multi-step workflows don't re-stat the dir
// on every hop.
type InstanceCache struct {
	mu   sync.RWMutex
	data []Instance
	time time.Time
	ttl  time.Duration
}

// NewInstanceCache creates a cache with the default 5-second TTL.
func NewInstanceCache() *InstanceCache {
	return &InstanceCache{ttl: 5 * time.Second}
}

// Get returns a shallow copy of the cached instances if they are still valid.
// The second return value reports whether the cache hit.
func (c *InstanceCache) Get() ([]Instance, bool) {
	c.mu.RLock()
	defer c.mu.RUnlock()
	if c.data != nil && time.Since(c.time) < c.ttl {
		out := make([]Instance, len(c.data))
		copy(out, c.data)
		return out, true
	}
	return nil, false
}

// Set stores a copy of the given instances and refreshes the timestamp.
func (c *InstanceCache) Set(instances []Instance) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.data = make([]Instance, len(instances))
	copy(c.data, instances)
	c.time = time.Now()
}

// Clear discards the cached data so the next call reads from disk again.
func (c *InstanceCache) Clear() {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.data = nil
	c.time = time.Time{}
}
