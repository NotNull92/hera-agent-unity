package cmd

import (
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// editorCmd controls Unity play mode and asset database.
// resolve is needed for waitForReady so compile polling can follow the current project instance.
func editorCmd(args []string, send SendFunc, resolve instanceResolver, category string) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: hera-agent-unity editor <play|stop|pause|refresh>")
	}

	action := args[0]
	flags := parseSubFlags(args[1:])

	switch action {
	case "play":
		_, wait := flags["wait"]
		resp, err := send("manage_editor", map[string]interface{}{"action": "play"})
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
		if waitErr := waitForState(resolve, 60000, category, "playing", "paused"); waitErr != nil {
			return nil, waitErr
		}
		resp.Message = "Entered play mode (confirmed)."
		return resp, nil

	case "stop":
		return send("manage_editor", map[string]interface{}{"action": "stop"})

	case "pause":
		return send("manage_editor", map[string]interface{}{"action": "pause"})

	case "refresh":
		_, compile := flags["compile"]
		_, force := flags["force"]
		params := map[string]interface{}{}
		if force {
			params["force"] = true
			params["mode"] = "force"
		}
		if compile {
			params["compile"] = "request"
			resp, err := send("refresh_unity", params)
			if err != nil {
				return nil, err
			}
			if !resp.Success {
				return resp, nil
			}
			hasErrors := waitForReady(resolve, category)
			if hasErrors {
				return nil, fmt.Errorf("compilation finished with errors (check hera-agent-unity console)")
			}
			resp.Message = "Refresh and compilation completed."
			return resp, nil
		}
		return send("refresh_unity", params)

	default:
		return nil, fmt.Errorf("unknown editor action: %s\nAvailable: play, stop, pause, refresh", action)
	}
}
