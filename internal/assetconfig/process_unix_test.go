//go:build !windows

package assetconfig

import (
	"errors"
	"os"
	"syscall"
	"testing"
)

func TestCheckConfigProcessDeadUnixCurrentProcessIsAlive(t *testing.T) {
	if checkConfigProcessDead(os.Getpid()) {
		t.Fatal("current process was classified as dead")
	}
}

func TestConfigProcessSignalConfirmsDeadUnix(t *testing.T) {
	tests := []struct {
		name string
		err  error
		dead bool
	}{
		{name: "process exists", err: nil, dead: false},
		{name: "permission denied means alive", err: syscall.EPERM, dead: false},
		{name: "process missing", err: syscall.ESRCH, dead: true},
		{name: "Go process already finished", err: os.ErrProcessDone, dead: true},
		{name: "indeterminate error means alive", err: errors.New("indeterminate"), dead: false},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			if got := configProcessSignalConfirmsDead(test.err); got != test.dead {
				t.Fatalf("dead=%v, want %v for %v", got, test.dead, test.err)
			}
		})
	}
}
