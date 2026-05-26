package cmd

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/tui"
)

// sendBatchFn is injected for testing.

type batchStdinReader interface {
	io.Reader
	Stat() (os.FileInfo, error)
}

var batchStdin batchStdinReader = os.Stdin

func batchCmd(ctx context.Context, args []string, sendBatch sendBatchFn, resolve instanceResolver) error {
	params, err := buildParams(args, nil)
	if err != nil {
		return err
	}
	filePath, hasFile := params["file"].(string)
	_, dryRun := params["dry-run"]

	var data []byte

	if hasFile && filePath != "" {
		data, err = os.ReadFile(filePath)
		if err != nil {
			return fmt.Errorf("cannot read batch file: %v", err)
		}
	} else {
		// Read from stdin if piped (no --file required)
		info, statErr := batchStdin.Stat()
		if statErr != nil || info.Mode()&os.ModeCharDevice != 0 {
			return fmt.Errorf("usage: hera-agent-unity batch --file <path.json>  (or pipe JSON via stdin)")
		}
		data, err = io.ReadAll(batchStdin)
		if err != nil || len(data) == 0 {
			return fmt.Errorf("no batch data provided via stdin")
		}
	}

	var req client.BatchCommandRequest
	if err := json.Unmarshal(data, &req); err != nil {
		return fmt.Errorf("invalid JSON in batch data: %v", err)
	}

	if dryRun {
		fmt.Println(tui.InfoPanel("War Council", fmt.Sprintf("Would execute %d command(s)", len(req.Commands))))
		for i, cmd := range req.Commands {
			fmt.Printf("  %s %s\n", tui.MutedStyle.Render(fmt.Sprintf("[%d]", i+1)), cmd.Command)
		}
		return nil
	}

	inst, err := resolve()
	if err != nil {
		return err
	}

	resp, err := sendBatch(ctx, inst, req)
	if err != nil {
		return err
	}

	for i, result := range resp.Results {
		status := "OK"
		if !result.Success {
			status = "FAIL"
		}
		fmt.Printf("%s %s %s\n", tui.Progress(i+1, len(resp.Results)), tui.StatusBadge(status), result.Message)
	}

	if resp.Failed > 0 {
		return fmt.Errorf("batch completed with %d failure(s)", resp.Failed)
	}

	return nil
}
