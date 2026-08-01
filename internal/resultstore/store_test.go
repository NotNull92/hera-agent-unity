package resultstore

import (
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

const testProjectID = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

func TestStoreSpoolsAndReadsByIntegrityCheckedHandle(t *testing.T) {
	store, err := New(testProjectID, Options{Root: t.TempDir(), MaxBytes: 1024, Retention: time.Hour})
	if err != nil {
		t.Fatal(err)
	}
	payload := []byte(`{"success":true,"data":{"items":[1,2,3]}}`)
	handle, err := store.Spool("op_result_test", payload)
	if err != nil {
		t.Fatal(err)
	}
	if handle.Bytes != int64(len(payload)) || !strings.HasPrefix(handle.Hash, "sha256:") {
		t.Fatalf("handle = %#v", handle)
	}
	if strings.Contains(handle.URI, store.Root()) || strings.Contains(handle.URI, `\\`) {
		t.Fatalf("handle leaked a filesystem path: %q", handle.URI)
	}
	got, err := store.Read(handle.URI)
	if err != nil {
		t.Fatal(err)
	}
	if string(got) != string(payload) {
		t.Fatalf("read = %s, want %s", got, payload)
	}
}

func TestStoreRejectsTraversalAndTampering(t *testing.T) {
	store, err := New(testProjectID, Options{Root: t.TempDir(), MaxBytes: 1024, Retention: time.Hour})
	if err != nil {
		t.Fatal(err)
	}
	handle, err := store.Spool("op_result_test", []byte(`{"ok":true}`))
	if err != nil {
		t.Fatal(err)
	}
	if _, err := store.Read("hera-result://cache/../outside/op_result_test/deadbeef"); !errors.Is(err, ErrInvalidHandle) {
		t.Fatalf("traversal error = %v", err)
	}
	path, err := store.pathForURI(handle.URI)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, []byte(`{"changed":true}`), 0o600); err != nil {
		t.Fatal(err)
	}
	if _, err := store.Read(handle.URI); !errors.Is(err, ErrIntegrity) {
		t.Fatalf("tamper error = %v", err)
	}
	if _, err := store.Spool("op_result_test", []byte(`{"ok":true}`)); !errors.Is(err, ErrIntegrity) {
		t.Fatalf("tampered destination was overwritten: %v", err)
	}
}

func TestStorePrunesExpiredAndOverBudgetResults(t *testing.T) {
	root := t.TempDir()
	now := time.Date(2026, 8, 1, 12, 0, 0, 0, time.UTC)
	store, err := New(testProjectID, Options{
		Root: root, MaxBytes: 24, Retention: time.Hour, Now: func() time.Time { return now },
	})
	if err != nil {
		t.Fatal(err)
	}
	old, err := store.Spool("op_result_old", []byte(`{"value":"old"}`))
	if err != nil {
		t.Fatal(err)
	}
	oldPath, err := store.pathForURI(old.URI)
	if err != nil {
		t.Fatal(err)
	}
	stale := now.Add(-2 * time.Hour)
	if err := os.Chtimes(oldPath, stale, stale); err != nil {
		t.Fatal(err)
	}
	newer, err := store.Spool("op_result_new", []byte(`{"value":"new"}`))
	if err != nil {
		t.Fatal(err)
	}
	if _, err := store.Read(old.URI); !errors.Is(err, ErrNotFound) {
		t.Fatalf("expired result error = %v", err)
	}
	if _, err := store.Read(newer.URI); err != nil {
		t.Fatalf("new result was pruned: %v", err)
	}
	now = now.Add(time.Minute)
	latest, err := store.Spool("op_result_latest", []byte(`{"value":"latest"}`))
	if err != nil {
		t.Fatal(err)
	}
	if _, err := store.Read(newer.URI); !errors.Is(err, ErrNotFound) {
		t.Fatalf("over-budget result error = %v", err)
	}
	if _, err := store.Read(latest.URI); err != nil {
		t.Fatalf("latest result was pruned: %v", err)
	}
	if err := filepath.WalkDir(root, func(path string, entry os.DirEntry, err error) error {
		if err == nil && !entry.IsDir() && strings.HasSuffix(entry.Name(), ".tmp") {
			t.Fatalf("temporary file remains: %s", path)
		}
		return err
	}); err != nil {
		t.Fatal(err)
	}
}

func TestStoreReadExpiresIdleResult(t *testing.T) {
	now := time.Date(2026, 8, 1, 12, 0, 0, 0, time.UTC)
	store, err := New(testProjectID, Options{
		Root: t.TempDir(), MaxBytes: 1024, Retention: time.Hour, Now: func() time.Time { return now },
	})
	if err != nil {
		t.Fatal(err)
	}
	handle, err := store.Spool("op_idle_expiry", []byte(`{"value":"expires"}`))
	if err != nil {
		t.Fatal(err)
	}
	now = now.Add(time.Hour + time.Second)
	if _, err := store.Read(handle.URI); !errors.Is(err, ErrNotFound) {
		t.Fatalf("expired idle resource error = %v", err)
	}
}

func TestStoreDoesNotPublishWhenTimestampingFails(t *testing.T) {
	store, err := New(testProjectID, Options{Root: t.TempDir(), MaxBytes: 1024, Retention: time.Hour})
	if err != nil {
		t.Fatal(err)
	}
	store.dateFile = func(string, time.Time, time.Time) error { return errors.New("date denied") }
	if _, err := store.Spool("op_date_failure", []byte(`{"value":true}`)); err == nil {
		t.Fatal("timestamp failure unexpectedly published a result")
	}
	var published []string
	_ = filepath.WalkDir(store.Root(), func(path string, entry os.DirEntry, err error) error {
		if err == nil && !entry.IsDir() && strings.HasSuffix(entry.Name(), ".json") {
			published = append(published, path)
		}
		return err
	})
	if len(published) != 0 {
		t.Fatalf("timestamp failure published files: %v", published)
	}
}
