package poll

import (
	"context"
	"fmt"
	"time"
)

// ExponentialBackoffLoop sleeps in exponentially-backed-off intervals,
// calling condition each tick. It returns nil as soon as condition reports
// true, or an error once timeout expires. The first sleep happens before
// the first condition check, matching the behaviour of the original status
// polling loops.
func ExponentialBackoffLoop(ctx context.Context, timeout, baseInterval, maxInterval time.Duration, condition func() bool) error {
	deadline := time.Now().Add(timeout)
	interval := baseInterval

	for time.Now().Before(deadline) {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(interval):
		}
		if condition() {
			return nil
		}
		if interval < maxInterval {
			interval = min(interval*2, maxInterval)
		}
	}
	return fmt.Errorf("timed out")
}
