package main

import (
	"context"
	"errors"
	"fmt"
	"os"
	"os/signal"

	"github.com/NotNull92/hera-agent-unity/cmd"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
)

var Version = "dev"

func init() {
	cmd.Version = Version
}

func main() {
	os.Exit(run())
}

// run is split out from main so that defer stop() actually runs before
// the process exits. main()'s os.Exit would otherwise skip deferred
// cleanup of the signal context.
func run() int {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()

	if err := cmd.Execute(ctx); err != nil {
		// Tool-command failures already printed a JSON error envelope to stderr;
		// don't print a second, duplicate "Error: …" line for them.
		if !errors.Is(err, cmd.ErrCommandFailed) {
			fmt.Fprintln(os.Stderr, tui.ErrorStyle.Render(fmt.Sprintf("Error: %v", err)))
		}
		return 1
	}
	return 0
}
