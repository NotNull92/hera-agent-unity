//go:build !windows

package client

import (
	"os"
	"os/exec"
	"testing"
)

func TestCheckProcessDeadUnix(t *testing.T) {
	if checkProcessDead(os.Getpid()) {
		t.Fatal("current process must be alive")
	}

	child := exec.Command(os.Args[0], "-test.run=^$")
	if err := child.Start(); err != nil {
		t.Fatal(err)
	}
	if err := child.Wait(); err != nil {
		t.Fatal(err)
	}
	if !checkProcessDead(child.Process.Pid) {
		t.Fatal("waited child process must be dead")
	}
}
