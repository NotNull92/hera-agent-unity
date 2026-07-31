package toolregistry

import "errors"

var (
	ErrCatalogHashMismatch = errors.New("tool catalog hash mismatch")
	ErrCacheInvalid        = errors.New("tool catalog cache is invalid")
	ErrCacheMiss           = errors.New("tool catalog cache miss")
	ErrUnsupportedProfile  = errors.New("tool profile is unsupported")
)
