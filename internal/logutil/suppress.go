package logutil

import (
	"bytes"
	"io"
)

// SuppressWriter wraps an io.Writer and drops any Write that contains the
// suppression string. It is used to silence known-harmless log noise (e.g.
// Go's net/http "Unsolicited response received on idle HTTP channel" during
// Unity domain reloads).
type SuppressWriter struct {
	W        io.Writer
	Suppress string
}

// Write implements io.Writer.
// It drops the write only if the suppression string appears at the start of a
// line (or the very start of p). This is stricter than bytes.Contains, which
// would accidentally suppress legitimate log lines that merely include the
// phrase elsewhere.
func (s *SuppressWriter) Write(p []byte) (int, error) {
	prefix := []byte(s.Suppress)
	if bytes.HasPrefix(p, prefix) {
		return len(p), nil
	}
	if bytes.Contains(p, append([]byte("\n"), prefix...)) {
		return len(p), nil
	}
	return s.W.Write(p)
}

// NewSuppressWriter returns an io.Writer that discards writes whose lines
// start with the given string.
func NewSuppressWriter(w io.Writer, suppress string) io.Writer {
	return &SuppressWriter{W: w, Suppress: suppress}
}
