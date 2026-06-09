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
func (s *SuppressWriter) Write(p []byte) (int, error) {
	if bytes.Contains(p, []byte(s.Suppress)) {
		return len(p), nil
	}
	return s.W.Write(p)
}

// NewSuppressWriter returns an io.Writer that discards writes containing the
// given substring.
func NewSuppressWriter(w io.Writer, suppress string) io.Writer {
	return &SuppressWriter{W: w, Suppress: suppress}
}
