package cmd

import (
	"context"
	"fmt"
	"os"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

type SendFunc func(command string, params interface{}) (*client.CommandResponse, error)

type SendBatchFunc func(ctx context.Context, inst *client.Instance, req client.BatchCommandRequest, timeoutMs int) (*client.BatchCommandResponse, error)

func makeFreshResolver(inst *client.Instance, project string, port int) instanceResolver {
	targetProject := project
	if port == 0 && targetProject == "" {
		targetProject = inst.ProjectPath
	}
	return func() (*client.Instance, error) {
		if port > 0 {
			return client.DiscoverInstanceFresh("", port)
		}
		return client.DiscoverInstanceFresh(targetProject, 0)
	}
}

func prepareSend(ctx context.Context, inst *client.Instance, category string, timeoutMs int, verbose bool) SendFunc {
	return func(command string, params interface{}) (*client.CommandResponse, error) {
		if command == "exec" && (isHumanCommand(category) || verbose) {
			fmt.Fprintln(os.Stderr, "[hera-agent-unity] compiling...")
		}
		return sendWithProgress(ctx, inst, command, params, timeoutMs, verbose)
	}
}

func withProgress(ctx context.Context, command string, verbose bool, fn func()) {
	if !verbose {
		fn()
		return
	}
	done := make(chan struct{})
	start := time.Now()
	go func() {
		ticker := time.NewTicker(time.Second)
		defer ticker.Stop()
		for {
			select {
			case <-done:
				return
			case <-ctx.Done():
				return
			case tick := <-ticker.C:
				elapsed := int(tick.Sub(start).Seconds())
				fmt.Fprintf(os.Stderr, "[hera-agent-unity] %s in progress... (%ds)\n", command, elapsed)
			}
		}
	}()
	fn()
	close(done)
}

func sendWithProgress(ctx context.Context, inst *client.Instance, command string, params interface{}, timeoutMs int, verbose bool) (*client.CommandResponse, error) {
	var resp *client.CommandResponse
	var err error
	withProgress(ctx, command, verbose, func() {
		resp, err = client.Send(ctx, inst, command, params, timeoutMs)
	})
	return resp, err
}
