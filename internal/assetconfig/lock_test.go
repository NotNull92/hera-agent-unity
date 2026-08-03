package assetconfig

import (
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
	"time"
)

func TestAcquireConfigLockRecoversStaleOwner(t *testing.T) {
	withTempHome(t)
	path := ConfigFilePath()
	lockPath := path + ".lock"
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		t.Fatal(err)
	}
	now := time.Date(2026, 8, 4, 0, 0, 0, 0, time.UTC)
	record, _ := json.Marshal(configLockRecord{
		Version: 1, PID: 999999, AcquiredAtMS: now.Add(-3 * time.Minute).UnixMilli(), Nonce: "stale-owner",
	})
	if err := os.WriteFile(lockPath, record, 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.Chtimes(lockPath, now.Add(-3*time.Minute), now.Add(-3*time.Minute)); err != nil {
		t.Fatal(err)
	}
	restore := configureConfigLockTest(now, func(int) bool { return true })
	defer restore()

	release, err := acquireConfigLock(path)
	if err != nil {
		t.Fatalf("recover stale lock: %v", err)
	}
	defer release()
	data, err := os.ReadFile(lockPath)
	if err != nil {
		t.Fatal(err)
	}
	var current configLockRecord
	if json.Unmarshal(data, &current) != nil || current.Nonce == "stale-owner" || current.PID != os.Getpid() {
		t.Fatalf("current lock = %s", data)
	}
}

func TestAcquireConfigLockDoesNotStealLiveOrRecentOwner(t *testing.T) {
	for _, test := range []struct {
		name      string
		age       time.Duration
		ownerGone bool
	}{
		{name: "live stale owner", age: 3 * time.Minute, ownerGone: false},
		{name: "recent exited owner", age: time.Minute, ownerGone: true},
	} {
		t.Run(test.name, func(t *testing.T) {
			withTempHome(t)
			path := ConfigFilePath()
			lockPath := path + ".lock"
			if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
				t.Fatal(err)
			}
			now := time.Date(2026, 8, 4, 0, 0, 0, 0, time.UTC)
			record, _ := json.Marshal(configLockRecord{
				Version: 1, PID: 4242, AcquiredAtMS: now.Add(-test.age).UnixMilli(), Nonce: "owned-lock",
			})
			if err := os.WriteFile(lockPath, record, 0o600); err != nil {
				t.Fatal(err)
			}
			if err := os.Chtimes(lockPath, now.Add(-test.age), now.Add(-test.age)); err != nil {
				t.Fatal(err)
			}
			restore := configureConfigLockTest(now, func(int) bool { return test.ownerGone })
			defer restore()

			if release, err := acquireConfigLock(path); err == nil {
				release()
				t.Fatal("lock was stolen")
			}
			if _, err := os.Stat(lockPath); err != nil {
				t.Fatalf("owner lock was removed: %v", err)
			}
		})
	}
}

func TestAcquireConfigLockRecoversMalformedStaleLock(t *testing.T) {
	withTempHome(t)
	path := ConfigFilePath()
	lockPath := path + ".lock"
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		t.Fatal(err)
	}
	now := time.Date(2026, 8, 4, 0, 0, 0, 0, time.UTC)
	if err := os.WriteFile(lockPath, []byte("{"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.Chtimes(lockPath, now.Add(-3*time.Minute), now.Add(-3*time.Minute)); err != nil {
		t.Fatal(err)
	}
	restore := configureConfigLockTest(now, func(int) bool { return false })
	defer restore()

	release, err := acquireConfigLock(path)
	if err != nil {
		t.Fatalf("recover malformed stale lock: %v", err)
	}
	release()
}

func TestReleaseConfigLockRequiresMatchingNonce(t *testing.T) {
	withTempHome(t)
	path := ConfigFilePath()
	release, err := acquireConfigLock(path)
	if err != nil {
		t.Fatal(err)
	}
	lockPath := path + ".lock"
	releaseOwnedConfigLock(lockPath, "not-the-owner")
	if _, err := os.Stat(lockPath); err != nil {
		t.Fatalf("mismatched release removed lock: %v", err)
	}
	release()
	if _, err := os.Stat(lockPath); !os.IsNotExist(err) {
		t.Fatalf("owner release did not remove lock: %v", err)
	}
}

func configureConfigLockTest(now time.Time, ownerGone func(int) bool) func() {
	oldTimeout := configLockTimeout
	oldNow := configLockNow
	oldSleep := configLockSleep
	oldProcessDead := configLockProcessDead
	configLockTimeout = -time.Nanosecond
	configLockNow = func() time.Time { return now }
	configLockSleep = func(time.Duration) {}
	configLockProcessDead = ownerGone
	return func() {
		configLockTimeout = oldTimeout
		configLockNow = oldNow
		configLockSleep = oldSleep
		configLockProcessDead = oldProcessDead
	}
}
