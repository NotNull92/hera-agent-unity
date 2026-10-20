package client

import "time"

type editorWindow struct {
	PID          int
	Class, Title string
	Visible      bool
	HWND, Owner  uintptr
	Enabled      bool
}

// BlockingDialog only probes a live process whose heartbeat has stopped advancing.
func (c *Client) BlockingDialog(inst *Instance) (string, bool) {
	if inst == nil || inst.PID <= 0 || time.Since(time.UnixMilli(inst.Timestamp)) <= 3*time.Second || c.IsProcessDead(inst.PID) {
		return "", false
	}
	disabled := make(map[uintptr]bool)
	var enabled []editorWindow
	for _, window := range c.enumerateWindows(inst.PID) {
		if window.PID != inst.PID || !window.Visible {
			continue
		}
		if window.Enabled {
			enabled = append(enabled, window)
		} else {
			disabled[window.HWND] = true
		}
	}
	if len(disabled) == 0 || len(enabled) == 0 {
		return "", false
	}
	for _, window := range enabled {
		if window.Owner != 0 && disabled[window.Owner] {
			return window.Title, true
		}
	}
	for _, window := range enabled {
		if window.Owner != 0 {
			return window.Title, true
		}
	}
	return enabled[0].Title, true
}
