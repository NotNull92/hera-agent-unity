package cmd

import (
	"encoding/json"
	"encoding/xml"
	"fmt"
	"io"
	"os"
	"sort"
	"strconv"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

type testOptions struct {
	format, output, name string
}

func parseTestOptions(args []string) (testOptions, error) {
	o := testOptions{format: "json", name: "EditMode"}
	p, positional, err := buildParams(args, nil)
	if err != nil {
		return o, err
	}
	// Paths are strings even when they look like numbers or booleans.
	for i, arg := range args {
		if arg != "--format" && arg != "--output" {
			continue
		}
		if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
			return o, fmt.Errorf("%s requires a value", arg)
		}
		p[strings.TrimPrefix(arg, "--")] = args[i+1]
	}
	for _, key := range []string{"format", "output"} {
		if value, exists := p[key]; exists {
			s, ok := value.(string)
			if !ok || s == "" {
				return o, fmt.Errorf("--%s requires a value", key)
			}
			if key == "format" {
				o.format = s
			} else {
				o.output = s
			}
		}
	}
	if o.format != "json" && o.format != "junit" && o.format != "nunit" {
		return o, fmt.Errorf("--format must be json, junit, or nunit")
	}
	_, hasIndex := p["shard-index"]
	_, hasCount := p["shard-count"]
	if hasIndex != hasCount {
		return o, fmt.Errorf("--shard-index and --shard-count must be supplied together")
	}
	if hasIndex {
		i, ie := strconv.ParseInt(fmt.Sprint(p["shard-index"]), 10, 32)
		c, ce := strconv.ParseInt(fmt.Sprint(p["shard-count"]), 10, 32)
		if ie != nil || ce != nil || i < 1 || c < 1 || i > c {
			return o, fmt.Errorf("shards require 1 <= index <= count <= 2147483647")
		}
	}
	if len(positional) > 0 && (o.format != "json" || o.output != "" || hasIndex) {
		return o, fmt.Errorf("format, output, and shard options apply only to test runs")
	}
	if _, resume := p["resume"]; resume {
		o.name = "Unity tests"
		if hasIndex {
			return o, fmt.Errorf("--resume cannot change the run's shard")
		}
	}
	if mode, ok := p["mode"].(string); ok {
		o.name = mode
	}
	if assembly, ok := p["assembly"].(string); ok {
		o.name += " " + assembly
	}
	return o, nil
}

type testCaseResult struct {
	FullName   string   `json:"full_name"`
	Status     string   `json:"status"`
	DurationMS *float64 `json:"duration_ms"`
	Message    string   `json:"message"`
	StackTrace string   `json:"stack_trace"`
}

type testSummary struct {
	Total    int              `json:"total"`
	Passed   int              `json:"passed"`
	Failed   int              `json:"failed"`
	Skipped  int              `json:"skipped"`
	Cases    []testCaseResult `json:"cases"`
	Passes   []string         `json:"passes"`
	Failures []string         `json:"failures"`
}

type junitFailure struct {
	Message string `xml:"message,attr"`
	Stack   string `xml:",chardata"`
}
type junitCase struct {
	Class   string        `xml:"classname,attr"`
	Name    string        `xml:"name,attr"`
	Time    string        `xml:"time,attr,omitempty"`
	Failure *junitFailure `xml:"failure,omitempty"`
	Skipped *struct{}     `xml:"skipped,omitempty"`
}
type junitSuite struct {
	Name     string      `xml:"name,attr"`
	Tests    int         `xml:"tests,attr"`
	Failures int         `xml:"failures,attr"`
	Skipped  int         `xml:"skipped,attr"`
	Time     string      `xml:"time,attr,omitempty"`
	Cases    []junitCase `xml:"testcase"`
}
type junitDocument struct {
	XMLName xml.Name   `xml:"testsuites"`
	Suite   junitSuite `xml:"testsuite"`
}
type nunitFailure struct {
	Message string `xml:"message"`
	Stack   string `xml:"stack-trace,omitempty"`
}
type nunitCase struct {
	ID       string        `xml:"id,attr"`
	Name     string        `xml:"name,attr"`
	FullName string        `xml:"fullname,attr"`
	Result   string        `xml:"result,attr"`
	Duration string        `xml:"duration,attr,omitempty"`
	Failure  *nunitFailure `xml:"failure,omitempty"`
}
type nunitSuite struct {
	ID            string      `xml:"id,attr"`
	FullName      string      `xml:"fullname,attr"`
	TestCaseCount int         `xml:"testcasecount,attr"`
	Type          string      `xml:"type,attr"`
	Name          string      `xml:"name,attr"`
	Result        string      `xml:"result,attr"`
	Total         int         `xml:"total,attr"`
	Passed        int         `xml:"passed,attr"`
	Failed        int         `xml:"failed,attr"`
	Skipped       int         `xml:"skipped,attr"`
	Duration      string      `xml:"duration,attr,omitempty"`
	Cases         []nunitCase `xml:"test-case"`
}
type nunitDocument struct {
	XMLName       xml.Name   `xml:"test-run"`
	ID            string     `xml:"id,attr"`
	TestCaseCount int        `xml:"testcasecount,attr"`
	Result        string     `xml:"result,attr"`
	Total         int        `xml:"total,attr"`
	Passed        int        `xml:"passed,attr"`
	Failed        int        `xml:"failed,attr"`
	Skipped       int        `xml:"skipped,attr"`
	Duration      string     `xml:"duration,attr,omitempty"`
	Suite         nunitSuite `xml:"test-suite"`
}

func renderTestXML(data []byte, options testOptions) ([]byte, bool, error) {
	var s testSummary
	if err := json.Unmarshal(data, &s); err != nil {
		return nil, false, err
	}
	legacy := s.Cases == nil
	if legacy {
		for _, name := range s.Passes {
			s.Cases = append(s.Cases, testCaseResult{FullName: name, Status: "passed"})
		}
		for _, failure := range s.Failures {
			name, message, _ := strings.Cut(failure, ": ")
			s.Cases = append(s.Cases, testCaseResult{FullName: name, Status: "failed", Message: message})
		}
		// Old results never supplied skipped names; preserve counts with explicit placeholders.
		for i := 0; i < s.Skipped; i++ {
			s.Cases = append(s.Cases, testCaseResult{FullName: fmt.Sprintf("[unreported skipped test %d]", i+1), Status: "skipped"})
		}
	}
	sort.SliceStable(s.Cases, func(i, j int) bool { return s.Cases[i].FullName < s.Cases[j].FullName })
	j := junitSuite{Name: options.name, Tests: s.Total, Failures: s.Failed, Skipped: s.Skipped}
	result := "Passed"
	if s.Failed > 0 {
		result = "Failed"
	} else if s.Passed == 0 && s.Skipped > 0 {
		result = "Skipped"
	}
	n := nunitSuite{ID: "0-0", FullName: options.name, TestCaseCount: s.Total, Type: "TestSuite", Name: options.name, Result: result, Total: s.Total, Passed: s.Passed, Failed: s.Failed, Skipped: s.Skipped}
	var duration float64
	for i, c := range s.Cases {
		// Dots in parameter values are part of the method name, not its namespace.
		prefix := c.FullName
		if at := strings.Index(prefix, "("); at >= 0 {
			prefix = prefix[:at]
		}
		class, name := "", c.FullName
		if at := strings.LastIndex(prefix, "."); at >= 0 {
			class, name = c.FullName[:at], c.FullName[at+1:]
		}
		timeText := ""
		if c.DurationMS != nil {
			duration += *c.DurationMS
			timeText = strconv.FormatFloat(*c.DurationMS/1000, 'f', -1, 64)
		}
		jc := junitCase{Class: class, Name: name, Time: timeText}
		nc := nunitCase{ID: "0-" + strconv.Itoa(i+1), Name: name, FullName: c.FullName, Duration: timeText}
		switch c.Status {
		case "passed":
			nc.Result = "Passed"
		case "failed":
			nc.Result = "Failed"
			jc.Failure = &junitFailure{c.Message, c.StackTrace}
			nc.Failure = &nunitFailure{c.Message, c.StackTrace}
		case "skipped":
			nc.Result = "Skipped"
			jc.Skipped = &struct{}{}
		default:
			return nil, legacy, fmt.Errorf("unknown test status %q", c.Status)
		}
		j.Cases = append(j.Cases, jc)
		n.Cases = append(n.Cases, nc)
	}
	if !legacy {
		j.Time = strconv.FormatFloat(duration/1000, 'f', -1, 64)
		n.Duration = j.Time
	}
	var document any = junitDocument{Suite: j}
	if options.format == "nunit" {
		document = nunitDocument{ID: "0", TestCaseCount: s.Total, Result: result, Total: s.Total, Passed: s.Passed, Failed: s.Failed, Skipped: s.Skipped, Duration: n.Duration, Suite: n}
	}
	content, err := xml.MarshalIndent(document, "", "  ")
	return append([]byte(xml.Header), append(content, '\n')...), legacy, err
}

// Only completed runs produce artifacts; pending/transport/selector errors keep their JSON envelope.
func writeTestOutput(resp *client.CommandResponse, options testOptions, stdout, stderr io.Writer) (bool, error) {
	if options.format == "json" && options.output == "" {
		return false, nil
	}
	if !resp.Success && resp.Code != "TESTS_FAILED" {
		return false, nil
	}
	if resp.Message == "running" {
		return false, nil
	}
	var content []byte
	var err error
	if options.format == "json" {
		content = resp.Data
		if !resp.Success {
			content, err = json.Marshal(resp)
		}
		content = append(append([]byte(nil), content...), '\n')
	} else {
		var legacy bool
		content, legacy, err = renderTestXML(resp.Data, options)
		if legacy {
			if _, warningErr := fmt.Fprintln(stderr, "warning: connector returned summary-only results; XML omits durations, cannot recover exact failure names, and uses placeholders for skipped tests."); warningErr != nil {
				return true, warningErr
			}
		}
	}
	if err != nil {
		return true, err
	}
	if options.output != "" {
		return true, os.WriteFile(options.output, content, 0o600)
	}
	_, err = stdout.Write(content)
	return true, err
}
