package cmd

import (
	"context"
	"errors"
	"fmt"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/poll"
)

const instanceDiscoveryTimeoutMs = 5_000

func initialDiscoveryTimeoutMs(requestTimeoutMs int) int {
	if requestTimeoutMs <= 0 || requestTimeoutMs > instanceDiscoveryTimeoutMs {
		return instanceDiscoveryTimeoutMs
	}
	return requestTimeoutMs
}

func waitForInstance(ctx context.Context, resolve instanceResolver, timeoutMs int) (*client.Instance, error) {
	var (
		instance    *client.Instance
		lastErr     error
		terminalErr error
	)
	isTerminal := func(err error) bool {
		var ambiguous *client.AmbiguousProjectError
		var mismatch *client.TargetMismatchError
		return errors.As(err, &ambiguous) || errors.As(err, &mismatch)
	}
	instance, lastErr = resolve()
	if lastErr == nil {
		return instance, nil
	}
	if isTerminal(lastErr) {
		return nil, lastErr
	}
	err := poll.ExponentialBackoffLoop(
		ctx,
		time.Duration(timeoutMs)*time.Millisecond,
		statusPollBaseInterval,
		1500*time.Millisecond,
		func() bool {
			instance, lastErr = resolve()
			if isTerminal(lastErr) {
				terminalErr = lastErr
				return true
			}
			return lastErr == nil
		},
	)
	if err == nil {
		if terminalErr != nil {
			return nil, terminalErr
		}
		return instance, nil
	}
	if errors.Is(err, context.Canceled) || errors.Is(err, context.DeadlineExceeded) {
		return nil, err
	}
	return nil, fmt.Errorf("no Unity instance became available within %s: %w",
		time.Duration(timeoutMs)*time.Millisecond, lastErr)
}
