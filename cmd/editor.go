package cmd

import (
	"context"
	"fmt"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/unitystate"
)

// editorCmd controls Unity play mode and asset database.
// resolve is needed for waitForReady so compile polling can follow the current project instance.
type editorRuntime struct {
	Config  GlobalConfig
	Send    SendFunc
	Resolve instanceResolver
}

func runEditorCmd(
	ctx context.Context,
	args []string,
	runtime editorRuntime,
) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: hera-agent-unity editor <play|stop|pause|refresh>")
	}

	action := args[0]
	parsedParams, _, err := buildParams(args[1:], nil)
	if err != nil {
		return nil, err
	}

	switch action {
	case "play":
		_, wait := parsedParams["wait"]
		resp, err := runtime.Send("manage_editor", map[string]interface{}{"action": "play"})
		if err != nil {
			return nil, err
		}
		if !resp.Success || !wait {
			return resp, nil
		}
		// Confirmation must come from the heartbeat file: play-mode entry
		// triggers a domain reload that stops the HTTP listener, so any
		// C#-side `await EnteredPlayMode` would never get to write a response.
		// `playing` or `paused` both indicate isPlaying == true.
		waitConfig := WaitConfig{Timeout: 60 * time.Second, Narrate: runtime.Config.Wait("editor").Narrate}
		if waitErr := waitConfig.WaitForState(ctx, runtime.Resolve, unitystate.Playing, unitystate.Paused); waitErr != nil {
			return nil, waitErr
		}
		resp.Message = "Entered play mode (confirmed)."
		return resp, nil

	case "stop":
		_, wait := parsedParams["wait"]
		resp, err := runtime.Send("manage_editor", map[string]interface{}{"action": "stop"})
		if err != nil {
			return nil, err
		}
		if !resp.Success || !wait {
			return resp, nil
		}
		// Leaving play mode triggers a domain reload too, so the heartbeat
		// still reads `playing` for a moment after the connector has answered.
		// Confirm from the file, the same way `play --wait` does.
		waitConfig := WaitConfig{Timeout: 60 * time.Second, Narrate: runtime.Config.Wait("editor").Narrate}
		if waitErr := waitConfig.WaitForState(ctx, runtime.Resolve, unitystate.Ready); waitErr != nil {
			return nil, waitErr
		}
		resp.Message = "Exited play mode (confirmed)."
		return resp, nil

	case "pause":
		return runtime.Send("manage_editor", map[string]interface{}{"action": "pause"})

	case "refresh":
		_, compile := parsedParams["compile"]
		_, force := parsedParams["force"]
		params := map[string]interface{}{}
		if force {
			params["force"] = true
			params["mode"] = "force"
		}
		if compile {
			params["compile"] = "request"
			resp, err := runtime.Send("refresh_unity", params)
			if err != nil {
				return nil, err
			}
			if !resp.Success {
				return resp, nil
			}
			client.ClearInstanceCache()
			ready, hasErrors, waitErr := runtime.Config.Wait("editor").WaitForReady(
				ctx,
				runtime.Resolve,
			)
			if waitErr != nil {
				return nil, waitErr
			}
			if !ready {
				return nil, fmt.Errorf(
					"compilation still running after %ds — raise --timeout, or poll `status` / `console` for completion",
					runtime.Config.TimeoutMillis()/1000,
				)
			}
			if hasErrors {
				return nil, fmt.Errorf("compilation finished with errors (check hera-agent-unity console)")
			}
			resp.Message = "Refresh and compilation completed."
			return resp, nil
		}
		return runtime.Send("refresh_unity", params)

	default:
		return nil, fmt.Errorf("unknown editor action: %s\nAvailable: play, stop, pause, refresh", action)
	}
}

func editorCmd(
	ctx context.Context,
	args []string,
	send SendFunc,
	resolve instanceResolver,
	_ string,
) (*client.CommandResponse, error) {
	return runEditorCmd(ctx, args, editorRuntime{
		Config:  GlobalConfig{Timeout: 60 * time.Second},
		Send:    send,
		Resolve: resolve,
	})
}
