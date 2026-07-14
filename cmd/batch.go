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

// SendBatchFunc is injected for testing.

type batchStdinReader interface {
	io.Reader
	Stat() (os.FileInfo, error)
}

var batchStdin batchStdinReader = os.Stdin

func batchCmd(ctx context.Context, args []string, sendBatch SendBatchFunc, inst *client.Instance, timeoutMs int) error {
	params, _, err := buildParams(args, nil)
	if err != nil {
		return err
	}
	filePath, hasFile := params["file"].(string)
	_, dryRun := params["dry-run"]

	var data []byte

	if hasFile && filePath != "" {
		data, err = os.ReadFile(filePath)
		if err != nil {
			return fmt.Errorf("cannot read batch file: %w", err)
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
		return fmt.Errorf("invalid JSON in batch data: %w", err)
	}

	// --atomic forces atomic mode regardless of the JSON options block, so
	// callers don't have to hand-write "options": {"atomic": true}.
	if atomic, ok := params["atomic"].(bool); ok && atomic {
		req.Options.Atomic = true
	}

	if dryRun {
		header := fmt.Sprintf("Would execute %d command(s)", len(req.Commands))
		if req.Options.Atomic {
			header += " (atomic: revert all on any failure)"
		}
		fmt.Println(tui.InfoPanel("War Council", header))
		for i, cmd := range req.Commands {
			fmt.Printf("  %s %s\n", tui.MutedStyle.Render(fmt.Sprintf("[%d]", i+1)), cmd.Command)
		}
		return nil
	}

	var resp *client.BatchCommandResponse
	withProgress(ctx, "batch", flagVerbose, func() {
		resp, err = sendBatch(ctx, inst, req, timeoutMs)
	})
	if err != nil {
		return err
	}

	compact := flagCompactJSON || !isHumanCommand("batch")
	quiet := flagQuiet
	if resp.Code != "" {
		encoded, marshalErr := json.Marshal(resp)
		if marshalErr != nil {
			return fmt.Errorf("marshal batch rejection: %w", marshalErr)
		}
		fmt.Fprintln(os.Stderr, string(encoded))
		return ErrCommandFailed
	}

	for i, result := range resp.Results {
		if compact {
			b, _ := json.Marshal(result)
			fmt.Println(string(b))
			continue
		}
		status := "OK"
		if !result.Success {
			status = "FAIL"
		}
		if quiet {
			fmt.Printf("[%d/%d] %s: %s\n", i+1, len(resp.Results), status, result.Message)
		} else {
			fmt.Printf("%s %s %s\n", tui.Progress(i+1, len(resp.Results)), tui.StatusBadge(status), result.Message)
		}
	}

	if resp.Failed > 0 {
		return fmt.Errorf("batch completed with %d failure(s)", resp.Failed)
	}

	return nil
}
