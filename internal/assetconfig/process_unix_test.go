//go:build !windows

package assetconfig

import (
	"os"
	"testing"
)

func TestCheckConfigProcessDeadUnix(t *testing.T) {
	if checkConfigProcessDead(os.Getpid()) {
		t.Fatal("current process was classified as dead")
	}
	if !checkConfigProcessDead(int(^uint32(0) >> 1)) {
		t.Fatal("impossible process id was not classified as dead")
	}
}
