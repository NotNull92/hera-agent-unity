package cmd

import (
	"os"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func mockSend(wantCmd string, t *testing.T) (sendFn, *map[string]interface{}) {
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

func TestParseSubFlags(t *testing.T) {
	tests := []struct {
		name string
		args []string
		want map[string]string
	}{
		{"empty", nil, map[string]string{}},
		{"key value pair", []string{"--port", "8080"}, map[string]string{"port": "8080"}},
		{"boolean flag", []string{"--wait"}, map[string]string{"wait": "true"}},
		{"mixed", []string{"--port", "8080", "--wait", "--filter", "error"}, map[string]string{"port": "8080", "wait": "true", "filter": "error"}},
		{"consecutive boolean flags", []string{"--wait", "--clear"}, map[string]string{"wait": "true", "clear": "true"}},
		{"non-flag args ignored", []string{"play", "--wait"}, map[string]string{"wait": "true"}},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := parseSubFlags(tt.args)
			if len(got) != len(tt.want) {
				t.Errorf("parseSubFlags(%v) = %v, want %v", tt.args, got, tt.want)
				return
			}
			for k, v := range tt.want {
				if got[k] != v {
					t.Errorf("parseSubFlags(%v)[%q] = %q, want %q", tt.args, k, got[k], v)
				}
			}
		})
	}
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
	p, err := buildParams([]string{"--lines", "50"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["lines"] != 50 {
		t.Errorf("expected lines=50, got %v", p["lines"])
	}
}

func TestBuildParams_BoolParsing(t *testing.T) {
	p, err := buildParams([]string{"--clear"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["clear"] != true {
		t.Errorf("expected clear=true, got %v", p["clear"])
	}
}

func TestBuildParams_StringParsing(t *testing.T) {
	p, err := buildParams([]string{"--filter", "error"}, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p["filter"] != "error" {
		t.Errorf("expected filter=error, got %v", p["filter"])
	}
}

func TestBuildParams_BaseParams(t *testing.T) {
	p, err := buildParams([]string{"--depth", "5"}, map[string]interface{}{"action": "hierarchy"})
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

func TestHasPositionalArg(t *testing.T) {
	cases := []struct {
		name string
		args []string
		want bool
	}{
		{"empty", nil, false},
		{"only code", []string{"return 1;"}, true},
		{"only valueless flags", []string{"--check", "--no-cache"}, false},
		{"flag with value, no code", []string{"--depth", "2"}, false},
		{"flag value then code", []string{"--depth", "2", "return 1;"}, true},
		{"code then flag value", []string{"return 1;", "--depth", "2"}, true},
		// Mirrors buildParams: with no registry of valueless flags, the token
		// after a --flag is consumed as its value. So a bare flag immediately
		// followed by code reads as "flag=code, no positional". Code-first
		// ordering (case above) is the unambiguous form.
		{"flag eats following token", []string{"--check", "return 1;"}, false},
	}
	for _, tc := range cases {
		if got := hasPositionalArg(tc.args); got != tc.want {
			t.Errorf("%s: hasPositionalArg(%v) = %v, want %v", tc.name, tc.args, got, tc.want)
		}
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
