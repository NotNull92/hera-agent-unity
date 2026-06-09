package poll

import (
	"fmt"
	"time"
)

// ExponentialBackoffLoop sleeps in exponentially-backed-off intervals,
// calling condition each tick. It returns nil as soon as condition reports
// true, or an error once timeout expires. The first sleep happens before
// the first condition check, matching the behaviour of the original status
// polling loops.
func ExponentialBackoffLoop(timeout, baseInterval, maxInterval time.Duration, condition func() bool) error {
	deadline := time.Now().Add(timeout)
	interval := baseInterval

	for time.Now().Before(deadline) {
		time.Sleep(interval)
		if condition() {
			return nil
		}
		if interval < maxInterval {
			interval *= 2
			if interval > maxInterval {
				interval = maxInterval
			}
		}
	}
	return fmt.Errorf("timed out")
}
