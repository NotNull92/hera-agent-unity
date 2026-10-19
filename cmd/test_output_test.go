package cmd

import (
	"bytes"
	"context"
	"encoding/json"
	"encoding/xml"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/paths"
)

func TestTestXML(t *testing.T) {
	ms := 1250.0
	summary := testSummary{Total: 3, Passed: 1, Failed: 1, Skipped: 1, Cases: []testCaseResult{
		{FullName: "N.F.Z", Status: "skipped", DurationMS: &ms},
		{FullName: `N.F.A("a.b&,<>'")`, Status: "failed", DurationMS: &ms, Message: `bad <&> "'`, StackTrace: "line <1> &\nline 2"},
		{FullName: "N.F.B", Status: "passed", DurationMS: &ms},
	}}
	data, err := json.Marshal(summary)
	if err != nil {
		t.Fatal(err)
	}
	for _, format := range []string{"junit", "nunit"} {
		t.Run(format, func(t *testing.T) {
			content, legacy, err := renderTestXML(data, testOptions{format: format, name: "EditMode <&>"})
			if err != nil || legacy {
				t.Fatalf("render: %v legacy=%v", err, legacy)
			}
			if !bytes.HasPrefix(content, []byte(xml.Header)) {
				t.Fatal("missing XML declaration")
			}
			if format == "junit" {
				var doc junitDocument
				if err := xml.Unmarshal(content, &doc); err != nil {
					t.Fatal(err)
				}
				if doc.Suite.Tests != 3 || doc.Suite.Failures != 1 || doc.Suite.Skipped != 1 || doc.Suite.Time != "3.75" {
					t.Fatalf("counts: %+v", doc.Suite)
				}
				c := doc.Suite.Cases[0]
				if c.Class != "N.F" || c.Name != `A("a.b&,<>'")` || c.Time != "1.25" || c.Failure.Message != summary.Cases[1].Message || c.Failure.Stack != summary.Cases[1].StackTrace {
					t.Fatalf("case: %+v", c)
				}
				if doc.Suite.Cases[2].Skipped == nil {
					t.Fatal("skip missing")
				}
			} else {
				var doc nunitDocument
				if err := xml.Unmarshal(content, &doc); err != nil {
					t.Fatal(err)
				}
				if doc.Total != 3 || doc.Passed != 1 || doc.Failed != 1 || doc.Skipped != 1 || doc.Result != "Failed" {
					t.Fatalf("counts: %+v", doc)
				}
				c := doc.Suite.Cases[0]
				if c.FullName != summary.Cases[1].FullName || c.Result != "Failed" || c.Duration != "1.25" || c.Failure.Message != summary.Cases[1].Message || c.Failure.Stack != summary.Cases[1].StackTrace {
					t.Fatalf("case: %+v", c)
				}
				if doc.Suite.Cases[2].Result != "Skipped" {
					t.Fatal("skip missing")
				}
			}
			golden := filepath.Join("testdata", "test-"+format+".xml")
			want, err := os.ReadFile(golden)
			if err != nil {
				t.Fatal(err)
			}
			if !bytes.Equal(content, bytes.ReplaceAll(want, []byte("\r\n"), []byte("\n"))) {
				t.Fatalf("XML snapshot differs:\n%s", content)
			}
		})
	}
}

func TestTestOutputLegacyAndErrors(t *testing.T) {
	resp := &client.CommandResponse{Success: false, Code: "TESTS_FAILED", Data: json.RawMessage(`{"total":3,"passed":1,"failed":1,"skipped":1,"passes":["N.P"],"failures":["N.F: bad"]}`)}
	var out, warnings bytes.Buffer
	handled, err := writeTestOutput(resp, testOptions{format: "junit"}, &out, &warnings)
	if !handled || err != nil || strings.Count(warnings.String(), "\n") != 1 {
		t.Fatalf("output: %v %v %s", handled, err, warnings.String())
	}
	if resp.Success || !strings.Contains(out.String(), "unreported skipped") || strings.Contains(out.String(), "time=") {
		t.Fatal(out.String())
	}
	for _, code := range []string{"TEST_RUN_PENDING", "NO_TESTS_MATCHED", "TEST_RUN_INTERRUPTED"} {
		resp.Code = code
		out.Reset()
		handled, err = writeTestOutput(resp, testOptions{format: "junit"}, &out, &warnings)
		if handled || err != nil || out.Len() != 0 {
			t.Fatalf("error %s must keep JSON", code)
		}
	}
}

func TestTestArtifactOptions(t *testing.T) {
	options, err := parseTestOptions([]string{"--output", "001"})
	if err != nil || options.output != "001" {
		t.Fatalf("numeric filename was coerced: %+v %v", options, err)
	}
	for _, args := range [][]string{
		{"--format", "csv"}, {"--output"}, {"--format"}, {"--shard-count", "2"},
		{"--shard-count", "0", "--shard-index", "1"}, {"--shard-count", "2", "--shard-index", "3"},
		{"--shard-count", "2", "--shard-index", "1.0"}, {"--shard-count", "2147483648", "--shard-index", "1"},
		{"list", "--format", "junit"}, {"--resume", "run", "--shard-count", "1", "--shard-index", "1"},
	} {
		resp, err := testCmd(context.Background(), args, func(string, any) (*client.CommandResponse, error) {
			t.Fatal("invalid flags sent a request")
			return nil, nil
		}, nil, time.Second)
		if err != nil || resp.Code != "INVALID_ARGUMENT" {
			t.Fatalf("%v: %v %v", args, resp, err)
		}
	}
	_, err = testCmd(context.Background(), []string{"--shard-count", "2", "--shard-index", "1"}, func(_ string, p any) (*client.CommandResponse, error) {
		params := p.(map[string]any)
		if params["shard_count"] != int64(2) || params["shard_index"] != int64(1) {
			t.Fatal(params)
		}
		return &client.CommandResponse{Success: true}, nil
	}, nil, time.Second)
	if err != nil {
		t.Fatal(err)
	}
}

func TestTestResumeXMLFile(t *testing.T) {
	const port = 39128
	const run = "m4-resume"
	path := paths.TestResultPath(port, run)
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, []byte(`{"success":true,"data":{"total":1,"passed":1,"cases":[{"full_name":"N.F.P","status":"passed","duration_ms":20}]}}`), 0o600); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = os.Remove(path) })
	output := filepath.Join(t.TempDir(), "results.xml")
	args := []string{"--resume", run, "--format", "nunit", "--output", output}
	resp, err := testCmd(context.Background(), args, func(string, any) (*client.CommandResponse, error) { t.Fatal("resume sent a request"); return nil, nil }, func() (*client.Instance, error) { return &client.Instance{Port: port}, nil }, time.Second)
	if err != nil {
		t.Fatal(err)
	}
	opts, err := parseTestOptions(args)
	if err != nil {
		t.Fatal(err)
	}
	var out, warnings bytes.Buffer
	if handled, err := writeTestOutput(resp, opts, &out, &warnings); !handled || err != nil {
		t.Fatalf("write: %v %v", handled, err)
	}
	content, err := os.ReadFile(output)
	if err != nil {
		t.Fatal(err)
	}
	var doc nunitDocument
	if err := xml.Unmarshal(content, &doc); err != nil || doc.Total != 1 || out.Len() != 0 {
		t.Fatalf("artifact: %v %+v", err, doc)
	}
}
