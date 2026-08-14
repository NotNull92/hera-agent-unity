package docbundle

import (
	"compress/gzip"
	"errors"
	"io"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
)

func TestBuild_WhenJSONLIsValid_PreservesRawLines(t *testing.T) {
	// Given: validated JSONL contains an intentional blank line.
	dir := t.TempDir()
	input := filepath.Join(dir, "source.jsonl")
	output := filepath.Join(dir, "bundle.jsonl.gz")
	raw := "{\"id\":\"one\"}\n\n{\"id\":\"two\",\"value\":\"kept\"}\n"
	if err := os.WriteFile(input, []byte(raw), 0o600); err != nil {
		t.Fatal(err)
	}
	var lineNumbers []int

	// When: the shared bundle pipeline validates and compresses the source.
	entries, size, err := Build(input, output, func(_ []byte, lineNo int) error {
		lineNumbers = append(lineNumbers, lineNo)
		return nil
	})

	// Then: non-empty source lines survive byte-for-byte and in order.
	if err != nil {
		t.Fatal(err)
	}
	if entries != 2 || size <= 0 {
		t.Fatalf("Build() = entries %d, size %d", entries, size)
	}
	if !reflect.DeepEqual(lineNumbers, []int{1, 3}) {
		t.Fatalf("validator line numbers = %v, want [1 3]", lineNumbers)
	}
	file, err := os.Open(output)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = file.Close() })
	reader, err := gzip.NewReader(file)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = reader.Close() })
	decoded, err := io.ReadAll(reader)
	if err != nil {
		t.Fatal(err)
	}
	want := "{\"id\":\"one\"}\n{\"id\":\"two\",\"value\":\"kept\"}\n"
	if string(decoded) != want {
		t.Fatalf("decoded bundle = %q, want %q", decoded, want)
	}
}

func TestBuild_WhenValidationFails_DoesNotCreateOutput(t *testing.T) {
	// Given: the first non-empty source line violates the domain taxonomy.
	dir := t.TempDir()
	input := filepath.Join(dir, "source.jsonl")
	output := filepath.Join(dir, "bundle.jsonl.gz")
	if err := os.WriteFile(input, []byte("{}\n"), 0o600); err != nil {
		t.Fatal(err)
	}

	// When: domain validation rejects the line.
	_, _, err := Build(input, output, func(_ []byte, _ int) error {
		return errors.New("invalid entry")
	})

	// Then: the source location is preserved and no output is truncated into existence.
	if err == nil || !strings.Contains(err.Error(), input+":1: invalid entry") {
		t.Fatalf("Build() error = %v", err)
	}
	if _, statErr := os.Stat(output); !os.IsNotExist(statErr) {
		t.Fatalf("output exists after validation failure: %v", statErr)
	}
}
