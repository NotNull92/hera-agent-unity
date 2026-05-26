package tui

import (
	"os"

	"github.com/mattn/go-isatty"
)

// ColorEnabled reports whether stdout supports styled output.
// Returns false when stdout is redirected/piped, NO_COLOR is set, or the
// user explicitly opted out via HERA_PLAIN. Callers should emit plain
// text in that case so downstream tooling (scripts, AI agents) keeps
// parsing the existing format.
func ColorEnabled() bool {
	if os.Getenv("NO_COLOR") != "" {
		return false
	}
	if os.Getenv("HERA_PLAIN") != "" {
		return false
	}
	fd := os.Stdout.Fd()
	return isatty.IsTerminal(fd) || isatty.IsCygwinTerminal(fd)
}
