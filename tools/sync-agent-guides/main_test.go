package main

import (
	"bytes"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestGenerateGuides_DerivesMirrorsFromCanonicalSource(t *testing.T) {
	// Given
	source := []byte("# Guide\r\n\r\nUsage.\r\n\r\n" + developmentHeading + "\r\n\r\nInternal.\r\n")

	// When
	guides, err := generateGuides(source)

	// Then
	if err != nil {
		t.Fatalf("generateGuides() error = %v", err)
	}
	wantMirror := []byte("# Guide\n\nUsage.\n")
	if !bytes.Equal(guides["AGENT.md"], wantMirror) {
		t.Errorf("AGENT.md = %q, want %q", guides["AGENT.md"], wantMirror)
	}
	if !bytes.Equal(guides["cmd/AGENT.md"], wantMirror) {
		t.Errorf("cmd/AGENT.md = %q, want %q", guides["cmd/AGENT.md"], wantMirror)
	}
	if !bytes.HasPrefix(guides[".cursor/rules/hera-agent-unity.mdc"], []byte("---\n")) {
		t.Error("Cursor guide is missing YAML frontmatter")
	}
	if !bytes.Contains(guides[".agents/skills/hera-agent-unity/SKILL.md"], []byte("\nname: hera-agent-unity\n")) {
		t.Error("AntiGravity skill is missing name frontmatter")
	}
	for path, content := range guides {
		if bytes.Contains(content, []byte("\r")) {
			t.Errorf("%s contains a non-LF line ending", path)
		}
		if bytes.Contains(content, []byte(developmentHeading)) {
			t.Errorf("%s includes repository-development-only rules", path)
		}
	}
}

func TestGenerateGuides_RebasesRepositoryLinksForNestedTargets(t *testing.T) {
	// Given
	source := []byte("# Guide\n\n[Commands](docs/COMMANDS.md) [README](README.md) [Examples](examples/rules/) " +
		"[License](LICENSE) [Section](#section) [Web](https://example.com)\n\n" +
		developmentHeading + "\n\nInternal.\n")

	// When
	guides, err := generateGuides(source)

	// Then
	if err != nil {
		t.Fatalf("generateGuides() error = %v", err)
	}
	for path, prefix := range map[string]string{
		".cursor/rules/hera-agent-unity.mdc":       "../../",
		".agents/skills/hera-agent-unity/SKILL.md": "../../../",
	} {
		for _, target := range []string{"docs/COMMANDS.md", "README.md", "examples/rules/", "LICENSE"} {
			want := []byte("](" + prefix + target + ")")
			if !bytes.Contains(guides[path], want) {
				t.Errorf("%s does not contain rebased link %q", path, want)
			}
		}
		for _, want := range []string{"](#section)", "](https://example.com)"} {
			if !bytes.Contains(guides[path], []byte(want)) {
				t.Errorf("%s changed non-repository link %q", path, want)
			}
		}
	}
}

func TestGenerateGuides_RejectsInvalidUTF8(t *testing.T) {
	// Given
	source := []byte{0xff, '\n'}

	// When
	_, err := generateGuides(source)

	// Then
	if err == nil || !strings.Contains(err.Error(), "UTF-8") {
		t.Fatalf("generateGuides() error = %v, want invalid UTF-8 error", err)
	}
}

func TestSyncGuides_CheckDetectsDriftWithoutWriting(t *testing.T) {
	// Given
	root := t.TempDir()
	source := "# Guide\n\nUsage.\n\n" + developmentHeading + "\n\nInternal.\n"
	if err := os.WriteFile(filepath.Join(root, "AGENTS.md"), []byte(source), 0o644); err != nil {
		t.Fatal(err)
	}
	if err := syncGuides(root, false); err != nil {
		t.Fatalf("syncGuides(write) error = %v", err)
	}
	driftedPath := filepath.Join(root, "AGENT.md")
	drifted := []byte("drifted\n")
	if err := os.WriteFile(driftedPath, drifted, 0o644); err != nil {
		t.Fatal(err)
	}

	// When
	err := syncGuides(root, true)

	// Then
	if err == nil || !strings.Contains(err.Error(), "AGENT.md") {
		t.Fatalf("syncGuides(check) error = %v, want AGENT.md drift", err)
	}
	after, readErr := os.ReadFile(driftedPath)
	if readErr != nil {
		t.Fatal(readErr)
	}
	if !bytes.Equal(after, drifted) {
		t.Errorf("check mode changed AGENT.md to %q", after)
	}
}

func TestGeneratedGuidesMatchCanonicalSource(t *testing.T) {
	// Given
	root := filepath.Clean(filepath.Join("..", ".."))

	// When
	err := syncGuides(root, true)

	// Then
	if err != nil {
		t.Fatal(err)
	}
}
