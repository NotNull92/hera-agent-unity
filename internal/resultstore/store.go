package resultstore

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"fmt"
	"net/url"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
	"time"
)

const Scheme = "hera-result"

var (
	ErrInvalidHandle = errors.New("invalid result resource handle")
	ErrNotFound      = errors.New("result resource not found")
	ErrIntegrity     = errors.New("result resource failed integrity check")
	hexDigest        = regexp.MustCompile(`^[a-f0-9]{64}$`)
	safeOperationID  = regexp.MustCompile(`^[A-Za-z0-9_-]{8,128}$`)
)

type Options struct {
	Root      string
	MaxBytes  int64
	Retention time.Duration
	Now       func() time.Time
}

type Handle struct {
	URI   string
	Hash  string
	Bytes int64
}

type Store struct {
	root       string
	projectKey string
	maxBytes   int64
	retention  time.Duration
	now        func() time.Time
	dateFile   func(string, time.Time, time.Time) error
	mu         sync.Mutex
}

func New(projectID string, options Options) (*Store, error) {
	projectKey := strings.TrimPrefix(projectID, "sha256:")
	if !hexDigest.MatchString(projectKey) {
		return nil, fmt.Errorf("project id must be a lowercase SHA-256 digest")
	}
	if options.Root == "" || options.MaxBytes <= 0 || options.Retention <= 0 {
		return nil, fmt.Errorf("result store root, byte cap, and retention are required")
	}
	now := options.Now
	if now == nil {
		now = time.Now
	}
	return &Store{
		root: options.Root, projectKey: projectKey,
		maxBytes: options.MaxBytes, retention: options.Retention, now: now, dateFile: os.Chtimes,
	}, nil
}

func (store *Store) Root() string { return store.root }

func (store *Store) Spool(operationID string, payload []byte) (Handle, error) {
	if !safeOperationID.MatchString(operationID) {
		return Handle{}, fmt.Errorf("%w: invalid operation id", ErrInvalidHandle)
	}
	if int64(len(payload)) > store.maxBytes {
		return Handle{}, fmt.Errorf("result is larger than the result cache byte cap")
	}
	digest := sha256.Sum256(payload)
	hashKey := hex.EncodeToString(digest[:])
	handle := Handle{
		URI:  fmt.Sprintf("%s://cache/%s/%s/%s", Scheme, store.projectKey, operationID, hashKey),
		Hash: "sha256:" + hashKey, Bytes: int64(len(payload)),
	}
	path := store.resultPath(operationID, hashKey)
	store.mu.Lock()
	defer store.mu.Unlock()
	if existing, err := os.ReadFile(path); err == nil {
		if bytes.Equal(existing, payload) {
			return handle, nil
		}
		return Handle{}, ErrIntegrity
	} else if !errors.Is(err, os.ErrNotExist) {
		return Handle{}, fmt.Errorf("inspect existing result resource: %w", err)
	}
	if err := writeAtomic(path, payload, store.now().UTC(), store.dateFile); err != nil {
		if existing, readErr := os.ReadFile(path); readErr == nil && bytes.Equal(existing, payload) {
			return handle, nil
		}
		return Handle{}, err
	}
	if err := store.pruneLocked(path); err != nil {
		return Handle{}, errors.Join(err, ignoreMissing(os.Remove(path)))
	}
	return handle, nil
}

func (store *Store) Read(uri string) ([]byte, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	path, err := store.pathForURI(uri)
	if err != nil {
		return nil, err
	}
	info, err := os.Lstat(path)
	if errors.Is(err, os.ErrNotExist) {
		return nil, ErrNotFound
	}
	if err != nil || !info.Mode().IsRegular() {
		return nil, ErrNotFound
	}
	if info.ModTime().Before(store.now().UTC().Add(-store.retention)) {
		if err := os.Remove(path); err != nil && !os.IsNotExist(err) {
			return nil, fmt.Errorf("expire result resource: %w", err)
		}
		return nil, ErrNotFound
	}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("read result resource: %w", err)
	}
	digest := sha256.Sum256(data)
	want := strings.TrimSuffix(filepath.Base(path), ".json")
	if hex.EncodeToString(digest[:]) != want {
		return nil, ErrIntegrity
	}
	return data, nil
}

func (store *Store) pathForURI(rawURI string) (string, error) {
	parsed, err := url.Parse(rawURI)
	if err != nil || parsed.Scheme != Scheme || parsed.Host != "cache" || parsed.RawQuery != "" || parsed.Fragment != "" {
		return "", ErrInvalidHandle
	}
	parts := strings.Split(strings.TrimPrefix(parsed.EscapedPath(), "/"), "/")
	if len(parts) != 3 {
		return "", ErrInvalidHandle
	}
	for index := range parts {
		decoded, decodeErr := url.PathUnescape(parts[index])
		if decodeErr != nil || decoded != parts[index] {
			return "", ErrInvalidHandle
		}
	}
	if parts[0] != store.projectKey || !safeOperationID.MatchString(parts[1]) || !hexDigest.MatchString(parts[2]) {
		return "", ErrInvalidHandle
	}
	return store.resultPath(parts[1], parts[2]), nil
}

func (store *Store) resultPath(operationID, hashKey string) string {
	return filepath.Join(store.root, store.projectKey, operationID, hashKey+".json")
}

func writeAtomic(path string, payload []byte, modified time.Time, dateFile func(string, time.Time, time.Time) error) (resultErr error) {
	directory := filepath.Dir(path)
	if err := os.MkdirAll(directory, 0o700); err != nil {
		return fmt.Errorf("create result cache directory: %w", err)
	}
	file, err := os.CreateTemp(directory, ".result-*.tmp")
	if err != nil {
		return fmt.Errorf("create result cache temp file: %w", err)
	}
	tempPath := file.Name()
	defer func() { resultErr = errors.Join(resultErr, ignoreMissing(os.Remove(tempPath))) }()
	if err := file.Chmod(0o600); err != nil {
		_ = file.Close()
		return err
	}
	if _, err := file.Write(payload); err != nil {
		_ = file.Close()
		return fmt.Errorf("write result cache temp file: %w", err)
	}
	if err := file.Sync(); err != nil {
		_ = file.Close()
		return fmt.Errorf("sync result cache temp file: %w", err)
	}
	if err := file.Close(); err != nil {
		return fmt.Errorf("close result cache temp file: %w", err)
	}
	if err := dateFile(tempPath, modified, modified); err != nil {
		return fmt.Errorf("date result cache entry: %w", err)
	}
	if err := os.Rename(tempPath, path); err != nil {
		return fmt.Errorf("commit result cache: %w", err)
	}
	return nil
}

func ignoreMissing(err error) error {
	if os.IsNotExist(err) {
		return nil
	}
	return err
}
