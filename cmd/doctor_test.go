package cmd

import (
	"io"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
)

func TestExtractMdSection(t *testing.T) {
	tests := []struct {
		name    string
		doc     string
		heading string
		want    string
	}{
		{
			name: "exact heading match",
			doc: "## 1. Quick Rules\n" +
				"Rule 1.\n" +
				"\n" +
				"## 2. Other\n" +
				"Other content.\n",
			heading: "## 1. Quick Rules",
			want:    "## 1. Quick Rules\nRule 1.",
		},
		{
			name: "nested headings preserved",
			doc: "## 1. Quick Rules\n" +
				"### 1.1 Subsection\n" +
				"Sub content.\n" +
				"## 2. Other\n" +
				"Other content.\n",
			heading: "## 1. Quick Rules",
			want:    "## 1. Quick Rules\n### 1.1 Subsection\nSub content.",
		},
		{
			name: "standalone terminator",
			doc: "## 1. Quick Rules\n" +
				"Rule 1.\n" +
				"\n" +
				"---\n" +
				"## 2. Other\n" +
				"Other content.\n",
			heading: "## 1. Quick Rules",
			want:    "## 1. Quick Rules\nRule 1.",
		},
		{
			name: "trailing blank lines trimmed",
			doc: "## 1. Quick Rules\n" +
				"Rule 1.\n" +
				"\n" +
				"\n" +
				"## 2. Other\n" +
				"Other content.\n",
			heading: "## 1. Quick Rules",
			want:    "## 1. Quick Rules\nRule 1.",
		},
		{
			name: "heading not found",
			doc: "## 1. Quick Rules\n" +
				"Rule 1.\n",
			heading: "## 2. Missing",
			want:    "",
		},
		{
			name: "same heading does not terminate itself",
			doc: "## 1. Quick Rules\n" +
				"Rule 1.\n" +
				"## 1. Quick Rules\n" +
				"Duplicate.\n" +
				"## 2. Other\n",
			heading: "## 1. Quick Rules",
			want:    "## 1. Quick Rules\nRule 1.\n## 1. Quick Rules\nDuplicate.",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := extractMdSection(tt.doc, tt.heading)
			if got != tt.want {
				t.Errorf("extractMdSection(...) = %q, want %q", got, tt.want)
			}
		})
	}
}

func TestExtractAgentRules(t *testing.T) {
	t.Run("markdown format", func(t *testing.T) {
		out := extractAgentRules("markdown")
		if strings.HasPrefix(out, "---") {
			t.Error("markdown format should not start with YAML frontmatter")
		}
		if !strings.Contains(out, "## 0. Bootstrap") {
			t.Error("expected Bootstrap section")
		}
		if !strings.Contains(out, "## Ultra Hera") {
			t.Error("expected Ultra Hera section")
		}
		if !strings.Contains(out, "## 1. Quick Rules") {
			t.Error("expected Quick Rules section")
		}
		if !strings.Contains(out, "## 4. Pitfalls") {
			t.Error("expected Pitfalls section")
		}
	})

	t.Run("cursor format", func(t *testing.T) {
		out := extractAgentRules("cursor")
		if !strings.HasPrefix(out, "---\n") {
			t.Error("cursor format should start with YAML frontmatter")
		}
		if !strings.Contains(out, "alwaysApply: true") {
			t.Error("expected alwaysApply frontmatter field")
		}
		if !strings.Contains(out, "## Ultra Hera") {
			t.Error("expected Ultra Hera section")
		}
		if !strings.Contains(out, "## 0. Bootstrap") {
			t.Error("expected Bootstrap section")
		}
		if !strings.Contains(out, "## 1. Quick Rules") {
			t.Error("expected Quick Rules section")
		}
		if !strings.Contains(out, "## 4. Pitfalls") {
			t.Error("expected Pitfalls section")
		}
	})
}

func TestExtractCompactAgentRules(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)

	out := extractCompactAgentRules("markdown")
	for _, want := range []string{
		"# hera-agent-unity - Compact Project Rules",
		"hera-agent-unity list --compact",
		"current-working-directory match and then the most recent live heartbeat",
		"normalized project path remains the identity across port changes",
		"OPERATION_OUTCOME_UNKNOWN",
		"Current Ultra Hera setting: `light`",
	} {
		if !strings.Contains(out, want) {
			t.Errorf("compact rules missing %q", want)
		}
	}
	for _, unwanted := range []string{"## 4. Pitfalls", "## Game Feel Mode (Beta)", "## Unity De-slop Mode (Beta)"} {
		if strings.Contains(out, unwanted) {
			t.Errorf("compact rules unexpectedly include %q", unwanted)
		}
	}
	if strings.HasPrefix(out, "---") {
		t.Error("markdown compact rules should not start with frontmatter")
	}

	cursor := extractCompactAgentRules("cursor")
	if !strings.HasPrefix(cursor, "---\n") || !strings.Contains(cursor, "alwaysApply: true") {
		t.Error("cursor compact rules must include activation frontmatter")
	}

	const (
		compactAgentRulesBaselineUTF8Bytes = 2277
		compactAgentRulesBaselineNewlines  = 28
	)
	if got := len([]byte(out)); got != compactAgentRulesBaselineUTF8Bytes {
		t.Fatalf("compact rules UTF-8 bytes = %d, reviewed baseline = %d",
			got, compactAgentRulesBaselineUTF8Bytes)
	}
	if got := strings.Count(out, "\n"); got != compactAgentRulesBaselineNewlines {
		t.Fatalf("compact rules newlines = %d, reviewed baseline = %d",
			got, compactAgentRulesBaselineNewlines)
	}
}

func TestDoctorCmdCompactAgentRules(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	t.Setenv("USERPROFILE", home)

	out := captureDoctorStdout(t, func() error {
		return doctorCmd([]string{"--agent-rules", "--compact"})
	})
	if !strings.Contains(out, "# hera-agent-unity - Compact Project Rules") {
		t.Fatalf("doctor compact output = %q", out)
	}
	if strings.Contains(out, "## 4. Pitfalls") {
		t.Fatal("doctor compact output included full pitfalls")
	}
}

func TestDoctorCmdRejectsCompactWithoutAgentRules(t *testing.T) {
	if err := doctorCmd([]string{"--compact"}); err == nil || !strings.Contains(err.Error(), "requires --agent-rules") {
		t.Fatalf("doctor --compact error = %v", err)
	}
}

func captureDoctorStdout(t *testing.T, run func() error) string {
	t.Helper()
	oldStdout := os.Stdout
	reader, writer, err := os.Pipe()
	if err != nil {
		t.Fatal(err)
	}
	os.Stdout = writer
	t.Cleanup(func() { os.Stdout = oldStdout })

	runErr := run()
	_ = writer.Close()
	os.Stdout = oldStdout
	output, readErr := io.ReadAll(reader)
	_ = reader.Close()
	if runErr != nil {
		t.Fatalf("doctor command failed: %v", runErr)
	}
	if readErr != nil {
		t.Fatalf("read doctor output: %v", readErr)
	}
	return string(output)
}

func TestBuildCompactUltraHeraAgentRules(t *testing.T) {
	tests := []struct {
		mode assetconfig.LoopEngineeringMode
		want string
	}{
		{mode: assetconfig.LoopEngineeringOff, want: "Current Ultra Hera setting: `off`"},
		{mode: assetconfig.LoopEngineeringLight, want: "Current Ultra Hera setting: `light`"},
		{mode: assetconfig.LoopEngineeringUltra, want: "Current Ultra Hera setting: `ultra`"},
		{mode: assetconfig.LoopEngineeringMode("invalid"), want: "Current Ultra Hera setting: `light`"},
	}
	for _, test := range tests {
		if got := buildCompactUltraHeraAgentRules(test.mode); !strings.Contains(got, test.want) {
			t.Errorf("buildCompactUltraHeraAgentRules(%q) missing %q in %q", test.mode, test.want, got)
		}
	}
}

func TestBuildUltraHeraAgentRules(t *testing.T) {
	tests := []struct {
		name string
		mode assetconfig.LoopEngineeringMode
		want string
	}{
		{name: "off", mode: assetconfig.LoopEngineeringOff, want: "Current setting: `off`"},
		{name: "light", mode: assetconfig.LoopEngineeringLight, want: "Current setting: `light`"},
		{name: "ultra", mode: assetconfig.LoopEngineeringUltra, want: "Current setting: `ultra`"},
		{name: "invalid", mode: assetconfig.LoopEngineeringMode("invalid"), want: "Current setting: `light`"},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := buildUltraHeraAgentRules(tt.mode)
			if !strings.Contains(got, tt.want) {
				t.Errorf("buildUltraHeraAgentRules(%q) missing %q in %q", tt.mode, tt.want, got)
			}
			if !strings.Contains(got, "Hera does not do the AI work by itself") {
				t.Errorf("buildUltraHeraAgentRules(%q) missing boundary sentence", tt.mode)
			}
		})
	}

	t.Run("light details", func(t *testing.T) {
		got := buildUltraHeraAgentRules(assetconfig.LoopEngineeringLight)
		for _, want := range []string{
			"Light loop:",
			"hera-agent-unity console --type error --lines 20",
			"hera-agent-unity exec --depth 1 ...",
			"PlayMode, screenshots, and full tests are not required by default",
		} {
			if !strings.Contains(got, want) {
				t.Errorf("light rules missing %q", want)
			}
		}
	})

	t.Run("ultra details", func(t *testing.T) {
		got := buildUltraHeraAgentRules(assetconfig.LoopEngineeringUltra)
		for _, want := range []string{
			"Light loop:",
			"Ultra loop:",
			"hera-agent-unity test --mode EditMode",
			"hera-agent-unity test --mode PlayMode",
			"hera-agent-unity screenshot --view game",
			"hera-agent-unity ui_doc capture --out ...",
		} {
			if !strings.Contains(got, want) {
				t.Errorf("ultra rules missing %q", want)
			}
		}
	})
}

func TestSameFile(t *testing.T) {
	tests := []struct {
		name string
		a    string
		b    string
		want bool
	}{
		{"identical", "/foo/bar", "/foo/bar", true},
		{"different", "/foo/bar", "/foo/baz", false},
	}
	if runtime.GOOS == "windows" {
		tests = append(tests, []struct {
			name string
			a    string
			b    string
			want bool
		}{
			{"case insensitive", `C:\\Foo\\Bar`, `c:\\foo\\bar`, true},
			{"case insensitive diff", `C:\\Foo\\Bar`, `c:\\foo\\baz`, false},
		}...)
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := sameFile(tt.a, tt.b); got != tt.want {
				t.Errorf("sameFile(%q, %q) = %v, want %v", tt.a, tt.b, got, tt.want)
			}
		})
	}
}

func TestResolveSymlink(t *testing.T) {
	dir := t.TempDir()
	real := filepath.Join(dir, "real.txt")
	if err := os.WriteFile(real, []byte("hello"), 0o644); err != nil {
		t.Fatal(err)
	}

	t.Run("regular file", func(t *testing.T) {
		got := resolveSymlink(real)
		// Should return absolute path.
		if !filepath.IsAbs(got) {
			t.Errorf("expected absolute path, got %q", got)
		}
		if got != real {
			t.Errorf("resolveSymlink(%q) = %q, want %q", real, got, real)
		}
	})

	t.Run("symlink", func(t *testing.T) {
		link := filepath.Join(dir, "link.txt")
		if err := os.Symlink(real, link); err != nil {
			if runtime.GOOS == "windows" {
				t.Skip("symlinks require privileges on Windows:", err)
			}
			t.Fatal(err)
		}
		got := resolveSymlink(link)
		if got != real {
			t.Errorf("resolveSymlink(%q) = %q, want %q", link, got, real)
		}
	})
}
