package cmd

import (
	"os"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func mockSend(wantCmd string, t *testing.T) (SendFunc, *map[string]interface{}) {
	t.Helper()
	captured := map[string]interface{}{}
	fn := func(cmd string, params interface{}) (*client.CommandResponse, error) {
		if cmd != wantCmd {
			t.Errorf("send called with command %q, want %q", cmd, wantCmd)
		}
		if p, ok := params.(map[string]interface{}); ok {
			for k, v := range p {
				captured[k] = v
			}
		}
		return &client.CommandResponse{Success: true}, nil
	}
	return fn, &captured
}

func TestSplitArgs(t *testing.T) {
	tests := []struct {
		name         string
		args         []string
		wantFlags    []string
		wantCommands []string
	}{
		{"empty", nil, nil, nil},
		{"commands only", []string{"editor", "play"}, nil, []string{"editor", "play"}},
		{"port flag", []string{"--port", "8080", "editor", "play"}, []string{"--port", "8080"}, []string{"editor", "play"}},
		{"project flag", []string{"--project", "myproj", "status"}, []string{"--project", "myproj"}, []string{"status"}},
		{"timeout flag", []string{"exec", "--timeout", "5000", "Time.time"}, []string{"--timeout", "5000"}, []string{"exec", "Time.time"}},
		{"multiple global flags", []string{"--port", "8080", "--timeout", "3000", "exec", "code"}, []string{"--port", "8080", "--timeout", "3000"}, []string{"exec", "code"}},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			flags, commands := splitArgs(tt.args)
			if !sliceEqual(flags, tt.wantFlags) {
				t.Errorf("splitArgs(%v) flags = %v, want %v", tt.args, flags, tt.wantFlags)
			}
			if !sliceEqual(commands, tt.wantCommands) {
				t.Errorf("splitArgs(%v) commands = %v, want %v", tt.args, commands, tt.wantCommands)
			}
		})
	}
}

func TestBuildParams_IntParsing(t *testing.T) {
	p, _, err := buildParams([]string{"--lines", "50"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["lines"] != 50 {
		t.Errorf("expected lines=50, got %v", p["lines"])
	}
}

func TestBuildParams_BoolParsing(t *testing.T) {
	p, _, err := buildParams([]string{"--clear"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["clear"] != true {
		t.Errorf("expected clear=true, got %v", p["clear"])
	}
}

func TestBuildParams_StringParsing(t *testing.T) {
	p, _, err := buildParams([]string{"--filter", "error"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["filter"] != "error" {
		t.Errorf("expected filter=error, got %v", p["filter"])
	}
}

func TestBuildParams_BaseParams(t *testing.T) {
	p, _, err := buildParams([]string{"--depth", "5"}, map[string]interface{}{"action": "hierarchy"})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["action"] != "hierarchy" {
		t.Errorf("expected action=hierarchy, got %v", p["action"])
	}
	if p["depth"] != 5 {
		t.Errorf("expected depth=5, got %v", p["depth"])
	}
}

func TestBuildParams_ExplicitFlagsOverrideParamsJSON(t *testing.T) {
	p, _, err := buildParams([]string{
		"--params", `{"lines":10,"clear":false,"filter":"warning"}`,
		"--lines", "20",
		"--clear",
		"--filter", "error",
	}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["lines"] != 20 {
		t.Errorf("expected lines=20, got %v", p["lines"])
	}
	if p["clear"] != true {
		t.Errorf("expected clear=true, got %v", p["clear"])
	}
	if p["filter"] != "error" {
		t.Errorf("expected filter=error, got %v", p["filter"])
	}
}

// Regression: `exec --depth 2 --file x.cs` must still read the file. The "2"
// (value of --depth) was previously mistaken for positional code, so --file was
// silently skipped and the caller hit MISSING_PARAM: 'code' required.
func TestReadExecFileIfPresent_FlagValueNotMistakenForCode(t *testing.T) {
	dir := t.TempDir()
	path := dir + "/probe.cs"
	if err := os.WriteFile(path, []byte("return 1;\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	out, err := readExecFileIfPresent([]string{"--depth", "2", "--file", path})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(out) == 0 || out[0] != "return 1;" {
		t.Fatalf("expected file contents as first positional arg, got %v", out)
	}
}

func sliceEqual(a, b []string) bool {
	if len(a) == 0 && len(b) == 0 {
		return true
	}
	if len(a) != len(b) {
		return false
	}
	for i := range a {
		if a[i] != b[i] {
			return false
		}
	}
	return true
}
