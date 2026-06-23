package cmd

import (
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
