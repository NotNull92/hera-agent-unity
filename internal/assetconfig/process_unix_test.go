//go:build !windows

package assetconfig

import (
	"os"
	"os/exec"
	"testing"
	"time"
)

func TestCheckConfigProcessDeadUnix(t *testing.T) {
	if checkConfigProcessDead(os.Getpid()) {
		t.Fatal("current process was classified as dead")
	}

	command := exec.Command("sh", "-c", "exit 0")
	if err := command.Start(); err != nil {
		t.Fatalf("start helper process: %v", err)
	}
	pid := command.Process.Pid
	if err := command.Wait(); err != nil {
		t.Fatalf("wait for helper process: %v", err)
	}

	deadline := time.Now().Add(time.Second)
	for !checkConfigProcessDead(pid) && time.Now().Before(deadline) {
		time.Sleep(10 * time.Millisecond)
	}
	if !checkConfigProcessDead(pid) {
		t.Fatalf("exited helper process %d was not classified as dead", pid)
	}
}
