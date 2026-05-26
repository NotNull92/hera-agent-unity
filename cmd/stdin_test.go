package cmd

import (
	"os"
	"path/filepath"
	"testing"
	"time"
)

// withStdin temporarily replaces os.Stdin with the given file for the duration
// of fn. Restores the original on return regardless of how fn exits.
func withStdin(t *testing.T, replacement *os.File, fn func()) {
	t.Helper()
	orig := os.Stdin
	os.Stdin = replacement
	t.Cleanup(func() { os.Stdin = orig })
	fn()
}

// nullStdin opens a fresh, closed-empty file as a stand-in for "no real stdin"
// (Cursor shell, bash $(...) with no piped input, CI runner with detached stdin).
// io.ReadAll on this returns EOF immediately rather than blocking — but the
// Stat() mode is *not* NamedPipe and *not* CharDevice, mirroring real-world
// detached stdin so the function's mode guard is what we're exercising.
func nullStdin(t *testing.T) *os.File {
	t.Helper()
	// On Unix this would be /dev/null; on Windows we use NUL.
	path := os.DevNull
	f, err := os.Open(path)
	if err != nil {
		t.Fatalf("open %s: %v", path, err)
	}
	t.Cleanup(func() { _ = f.Close() })
	return f
}

func TestReadStdinIfPiped_PositionalSkipsStdin(t *testing.T) {
	// With a positional arg present, stdin must be ignored entirely — even if
	// it happens to be a real pipe with data. This is the contract that lets
	// Cursor / bash $(...) callers pass code positionally without `$null |`.
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("pipe: %v", err)
	}
	defer r.Close()
	// Write garbage to the pipe and close — readStdinIfPiped must NOT pick it up.
	go func() {
		_, _ = w.Write([]byte("STDIN_SHOULD_BE_IGNORED"))
		_ = w.Close()
	}()

	withStdin(t, r, func() {
		got := readStdinIfPiped([]string{"return 1+1;"})
		if len(got) != 1 || got[0] != "return 1+1;" {
			t.Fatalf("positional arg should be preserved unchanged, got %v", got)
		}
	})
}

func TestReadStdinIfPiped_PositionalAfterFlagsSkipsStdin(t *testing.T) {
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("pipe: %v", err)
	}
	defer r.Close()
	go func() {
		_, _ = w.Write([]byte("WRONG"))
		_ = w.Close()
	}()

	args := []string{"--usings", "Unity.Mathematics", "return 1+1;"}
	withStdin(t, r, func() {
		got := readStdinIfPiped(args)
		// Must be exactly what was passed in — stdin not consumed.
		if len(got) != len(args) {
			t.Fatalf("expected args unchanged, got %v", got)
		}
		for i := range args {
			if got[i] != args[i] {
				t.Fatalf("arg[%d] = %q, want %q", i, got[i], args[i])
			}
		}
	})
}

func TestReadStdinIfPiped_RealPipeReadsCode(t *testing.T) {
	// Real piped input ("echo 'code' | hera-agent-unity exec") must still be
	// consumed and prepended as the positional arg.
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("pipe: %v", err)
	}
	defer r.Close()
	go func() {
		_, _ = w.Write([]byte("return 42;\n"))
		_ = w.Close()
	}()

	withStdin(t, r, func() {
		got := readStdinIfPiped(nil)
		if len(got) != 1 || got[0] != "return 42;" {
			t.Fatalf("expected piped code prepended, got %v", got)
		}
	})
}

func TestReadStdinIfPiped_FileRedirectReadsCode(t *testing.T) {
	// `hera-agent-unity exec < code.cs` — stdin is a regular file. Must read it.
	tmp := filepath.Join(t.TempDir(), "code.cs")
	if err := os.WriteFile(tmp, []byte("return \"file\";\n"), 0644); err != nil {
		t.Fatalf("write tmp: %v", err)
	}
	f, err := os.Open(tmp)
	if err != nil {
		t.Fatalf("open tmp: %v", err)
	}
	defer f.Close()

	withStdin(t, f, func() {
		got := readStdinIfPiped(nil)
		if len(got) != 1 || got[0] != "return \"file\";" {
			t.Fatalf("expected file content prepended, got %v", got)
		}
	})
}

func TestReadStdinIfPiped_DetachedStdinSkipsRead(t *testing.T) {
	// /dev/null (or NUL) stands in for the Cursor / bash $(...) case: stdin is
	// open but isn't a TTY, isn't a real pipe with data, and isn't a regular
	// data file. Pre-fix this would block on io.ReadAll. After the fix it must
	// return promptly without hanging.
	null := nullStdin(t)

	done := make(chan []string, 1)
	go func() {
		withStdin(t, null, func() {
			done <- readStdinIfPiped(nil)
		})
	}()

	select {
	case got := <-done:
		// args were nil, so result should also be nil/empty — no code injected.
		if len(got) != 0 {
			t.Fatalf("expected no code injected from null stdin, got %v", got)
		}
	case <-time.After(2 * time.Second):
		t.Fatal("readStdinIfPiped blocked on detached stdin — fix not applied")
	}
}

func TestReadStdinIfPiped_DetachedStdinWithPositional(t *testing.T) {
	// The Cursor / Claude Code compound case: positional arg + detached stdin.
	// Must not hang, must preserve positional unchanged.
	null := nullStdin(t)

	done := make(chan []string, 1)
	go func() {
		withStdin(t, null, func() {
			done <- readStdinIfPiped([]string{"return Application.productName;"})
		})
	}()

	select {
	case got := <-done:
		if len(got) != 1 || got[0] != "return Application.productName;" {
			t.Fatalf("expected positional arg preserved, got %v", got)
		}
	case <-time.After(2 * time.Second):
		t.Fatal("readStdinIfPiped blocked despite positional arg present")
	}
}
