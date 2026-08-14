package main

import (
	"bytes"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"slices"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

const (
	reportSchema = "hera.catalog-payload-report/2"
	// reviewRequiredExitCode is preserved by a built binary. `go run` wraps
	// child failures as its own non-zero exit and prints `exit status 3`.
	reviewRequiredExitCode = 3
)

func main() {
	exitCode, err := run(os.Args[1:], os.Stdin, os.Stdout)
	if err != nil {
		fmt.Fprintln(os.Stderr, strings.TrimSpace(err.Error()))
		os.Exit(1)
	}
	if exitCode != 0 {
		os.Exit(exitCode)
	}
}

func run(args []string, stdin io.Reader, stdout io.Writer) (int, error) {
	flags := flag.NewFlagSet("catalog-payload-report", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	catalogPath := flags.String("catalog", "", "catalog JSON file; stdin when omitted")
	outputPath := flags.String("output", "", "write report to a file instead of stdout")
	comparePath := flags.String("compare", "", "compare against a checked-in payload report")
	failOnGrowth := flags.Bool("fail-on-growth", false, "exit 3 when tool, action, description, or profile payload grows")
	failOnChange := flags.Bool("fail-on-change", false, "exit 3 when the catalog contract differs from the baseline")
	options := reportOptions{}
	flags.IntVar(&options.WarnProfileBytes, "warn-profile-bytes", 24_000, "warning budget for one normalized profile")
	flags.IntVar(&options.WarnToolBytes, "warn-tool-bytes", 20_000, "warning budget for one normalized tool")
	flags.IntVar(&options.Largest, "largest", 10, "number of largest tools and actions to report")
	if err := flags.Parse(args); err != nil {
		return 0, fmt.Errorf("parse arguments: %w", err)
	}
	if flags.NArg() != 0 {
		return 0, fmt.Errorf("catalog-payload-report accepts flags only")
	}
	if *failOnGrowth && *comparePath == "" {
		return 0, fmt.Errorf("--fail-on-growth requires --compare")
	}
	if *failOnChange && *comparePath == "" {
		return 0, fmt.Errorf("--fail-on-change requires --compare")
	}

	raw, err := readInput(*catalogPath, stdin)
	if err != nil {
		return 0, err
	}
	catalogData, err := unwrapCatalog(raw)
	if err != nil {
		return 0, err
	}
	catalog, err := toolregistry.ParseCatalog(catalogData)
	if err != nil {
		return 0, fmt.Errorf("parse catalog: %w", err)
	}
	result, err := buildReport(catalog, len(bytes.TrimSpace(catalogData)), options)
	if err != nil {
		return 0, err
	}
	if *comparePath != "" {
		baseline, readErr := readReport(*comparePath)
		if readErr != nil {
			return 0, readErr
		}
		comparison := compareReports(baseline, result)
		result.Comparison = &comparison
		if comparison.ReviewRequired {
			result.Warnings = append(result.Warnings,
				"catalog payload differs from the checked-in baseline: "+strings.Join(comparison.Reasons, "; "))
			slices.Sort(result.Warnings)
		}
	}

	encoded, err := json.MarshalIndent(result, "", "  ")
	if err != nil {
		return 0, fmt.Errorf("encode report: %w", err)
	}
	encoded = append(encoded, '\n')
	if *outputPath == "" {
		_, err = stdout.Write(encoded)
	} else {
		err = os.WriteFile(*outputPath, encoded, 0o644)
	}
	if err != nil {
		return 0, fmt.Errorf("write report: %w", err)
	}
	if result.Comparison != nil {
		if *failOnChange && result.Comparison.ReviewRequired {
			return reviewRequiredExitCode, nil
		}
		if *failOnGrowth && result.Comparison.Growth {
			return reviewRequiredExitCode, nil
		}
	}
	return 0, nil
}

func readInput(path string, stdin io.Reader) ([]byte, error) {
	if path != "" {
		data, err := os.ReadFile(path)
		if err != nil {
			return nil, fmt.Errorf("read catalog: %w", err)
		}
		return data, nil
	}
	data, err := io.ReadAll(stdin)
	if err != nil {
		return nil, fmt.Errorf("read catalog stdin: %w", err)
	}
	if len(bytes.TrimSpace(data)) == 0 {
		return nil, fmt.Errorf("catalog input is empty")
	}
	return data, nil
}

func readReport(path string) (report, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return report{}, fmt.Errorf("read comparison report: %w", err)
	}
	var value report
	if err := json.Unmarshal(data, &value); err != nil {
		return report{}, fmt.Errorf("parse comparison report: %w", err)
	}
	if value.Schema != reportSchema {
		return report{}, fmt.Errorf("unsupported comparison report schema %q", value.Schema)
	}
	return value, nil
}

func unwrapCatalog(raw []byte) ([]byte, error) {
	trimmed := bytes.TrimSpace(raw)
	var object map[string]json.RawMessage
	if json.Unmarshal(trimmed, &object) != nil {
		return trimmed, nil
	}
	if _, isCatalog := object["schema_version"]; isCatalog {
		return trimmed, nil
	}
	data, ok := object["data"]
	if !ok || len(bytes.TrimSpace(data)) == 0 || bytes.Equal(bytes.TrimSpace(data), []byte("null")) {
		return nil, fmt.Errorf("JSON input is neither a catalog nor a Hera response containing data")
	}
	return data, nil
}
