package client

import "time"

type editorWindow struct {
	PID          int
	Class, Title string
	Visible      bool
}

// BlockingDialog only probes a live process whose heartbeat has stopped advancing.
func (c *Client) BlockingDialog(inst *Instance) (string, bool) {
	if inst == nil || inst.PID <= 0 || time.Since(time.UnixMilli(inst.Timestamp)) <= 3*time.Second || c.IsProcessDead(inst.PID) {
		return "", false
	}
	for _, window := range c.enumerateWindows(inst.PID) {
		if window.PID == inst.PID && window.Visible && window.Class == "#32770" {
			return window.Title, true
		}
	}
	return "", false
}
