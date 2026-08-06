package main

import (
	"bytes"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

func TestBuildReportIsDeterministicAndSeparatesRawBytesFromEstimates(t *testing.T) {
	catalog := &toolregistry.Catalog{
		CatalogHash: "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
		Tools: []toolregistry.Tool{{
			Name: "alpha", Title: "Alpha", Description: "Inspect alpha", ContractMode: toolregistry.ContractStrict,
			Profiles:     []string{"core", "full"},
			InputSchema:  json.RawMessage(`{"type":"object"}`),
			OutputSchema: json.RawMessage(`{"type":"object"}`),
			Actions: []toolregistry.Action{{
				Name: "inspect", Description: "Inspect",
				InputSchema:  json.RawMessage(`{"type":"object"}`),
				OutputSchema: json.RawMessage(`{"type":"object"}`),
			}},
		}},
	}
	options := reportOptions{Largest: 10, WarnProfileBytes: 1, WarnToolBytes: 1}
	first, err := buildReport(catalog, 123, options)
	if err != nil {
		t.Fatal(err)
	}
	second, err := buildReport(catalog, 123, options)
	if err != nil {
		t.Fatal(err)
	}
	firstJSON, _ := json.Marshal(first)
	secondJSON, _ := json.Marshal(second)
	if string(firstJSON) != string(secondJSON) {
		t.Fatalf("reports differ:\n%s\n%s", firstJSON, secondJSON)
	}
	if first.InputBytes != 123 || first.NormalizedCatalogBytes == 0 || first.RoughTokens.Central == 0 {
		t.Fatalf("size fields=%#v", first)
	}
	if len(first.Profiles) != 2 || first.Profiles[0].Name != "core" || first.Profiles[1].Name != "full" {
		t.Fatalf("profiles=%#v", first.Profiles)
	}
	if len(first.Warnings) == 0 {
		t.Fatal("warning budgets produced no warnings")
	}
	if len(first.ActionDescribeSavings) != 1 || first.ActionDescribeSavings[0].ActionBytes == 0 {
		t.Fatalf("action describe savings=%#v", first.ActionDescribeSavings)
	}
}

func TestUnwrapCatalogAcceptsHeraEnvelope(t *testing.T) {
	raw := []byte(`{"success":true,"data":{"schema_version":"hera.tool-catalog/1"}}`)
	got, err := unwrapCatalog(raw)
	if err != nil || string(got) != `{"schema_version":"hera.tool-catalog/1"}` {
		t.Fatalf("got=%s error=%v", got, err)
	}
}

func TestCompareReportsRequiresReviewForContractAndPayloadGrowth(t *testing.T) {
	baseline := report{
		Schema:                reportSchema,
		CatalogHash:           "sha256:baseline",
		ToolCount:             1,
		ActionCount:           1,
		DescriptionCharacters: 10,
		Profiles: []profileSize{{
			Name: "core", ToolCount: 1, NormalizedContractBytes: 100,
		}},
	}
	current := report{
		Schema:                reportSchema,
		CatalogHash:           "sha256:current",
		ToolCount:             2,
		ActionCount:           3,
		DescriptionCharacters: 14,
		Profiles: []profileSize{{
			Name: "core", ToolCount: 2, NormalizedContractBytes: 140,
		}},
	}

	comparison := compareReports(baseline, current)
	if !comparison.ContractChanged || !comparison.Growth || !comparison.ReviewRequired {
		t.Fatalf("comparison=%#v", comparison)
	}
	if comparison.ToolCountDelta != 1 || comparison.ActionCountDelta != 2 ||
		comparison.DescriptionCharactersDelta != 4 {
		t.Fatalf("comparison deltas=%#v", comparison)
	}
	if len(comparison.Profiles) != 1 || comparison.Profiles[0].ContractBytesDelta != 40 {
		t.Fatalf("profile deltas=%#v", comparison.Profiles)
	}
}

func TestCompareReportsAcceptsReviewedEqualBaseline(t *testing.T) {
	baseline := report{
		Schema:                reportSchema,
		CatalogHash:           "sha256:same",
		ToolCount:             2,
		ActionCount:           3,
		DescriptionCharacters: 14,
		Profiles: []profileSize{{
			Name: "core", ToolCount: 2, NormalizedContractBytes: 140,
		}},
	}
	comparison := compareReports(baseline, baseline)
	if comparison.ContractChanged || comparison.Growth || comparison.ReviewRequired || len(comparison.Reasons) != 0 {
		t.Fatalf("comparison=%#v", comparison)
	}
}

func TestRunComparisonGateUsesCheckedInReport(t *testing.T) {
	fixture := filepath.Join("..", "..", "internal", "toolregistry", "testdata", "catalog-v1.json")
	raw, err := os.ReadFile(fixture)
	if err != nil {
		t.Fatal(err)
	}
	catalog, err := toolregistry.ParseCatalog(raw)
	if err != nil {
		t.Fatal(err)
	}
	baseline, err := buildReport(catalog, len(bytes.TrimSpace(raw)), reportOptions{
		WarnProfileBytes: 24_000,
		WarnToolBytes:    20_000,
		Largest:          10,
	})
	if err != nil {
		t.Fatal(err)
	}
	baselinePath := filepath.Join(t.TempDir(), "baseline.json")
	writeReportFixture(t, baselinePath, baseline)

	var output bytes.Buffer
	exitCode, err := run([]string{
		"--catalog", fixture,
		"--compare", baselinePath,
		"--fail-on-change",
	}, bytes.NewReader(nil), &output)
	if err != nil || exitCode != 0 {
		t.Fatalf("matching baseline exit=%d error=%v output=%s", exitCode, err, output.String())
	}

	baseline.CatalogHash = "sha256:outdated"
	writeReportFixture(t, baselinePath, baseline)
	output.Reset()
	reportPath := filepath.Join(t.TempDir(), "comparison.json")
	exitCode, err = run([]string{
		"--catalog", fixture,
		"--compare", baselinePath,
		"--fail-on-change",
		"--output", reportPath,
	}, bytes.NewReader(nil), &output)
	if err != nil || exitCode != reviewRequiredExitCode {
		t.Fatalf("changed baseline exit=%d error=%v output=%s", exitCode, err, output.String())
	}
	if output.Len() != 0 {
		t.Fatalf("stdout = %q, want report written only to file", output.String())
	}
	reportData, err := os.ReadFile(reportPath)
	if err != nil {
		t.Fatalf("read comparison report: %v", err)
	}
	var result report
	if err := json.Unmarshal(reportData, &result); err != nil {
		t.Fatalf("decode comparison output: %v", err)
	}
	if result.Comparison == nil || !result.Comparison.ContractChanged || !result.Comparison.ReviewRequired {
		t.Fatalf("comparison output=%#v", result.Comparison)
	}
}

func TestRunFailOnGrowthAllowsSameSizeContractChange(t *testing.T) {
	fixture := filepath.Join("..", "..", "internal", "toolregistry", "testdata", "catalog-v1.json")
	raw, err := os.ReadFile(fixture)
	if err != nil {
		t.Fatal(err)
	}
	catalog, err := toolregistry.ParseCatalog(raw)
	if err != nil {
		t.Fatal(err)
	}
	baseline, err := buildReport(catalog, len(bytes.TrimSpace(raw)), reportOptions{
		WarnProfileBytes: 24_000,
		WarnToolBytes:    20_000,
		Largest:          10,
	})
	if err != nil {
		t.Fatal(err)
	}
	baseline.CatalogHash = "sha256:different-contract"
	baselinePath := filepath.Join(t.TempDir(), "baseline.json")
	writeReportFixture(t, baselinePath, baseline)

	var output bytes.Buffer
	exitCode, err := run([]string{
		"--catalog", fixture,
		"--compare", baselinePath,
		"--fail-on-growth",
	}, bytes.NewReader(nil), &output)
	if err != nil || exitCode != 0 {
		t.Fatalf("same-size contract change exit=%d error=%v output=%s", exitCode, err, output.String())
	}
	var result report
	if err := json.Unmarshal(output.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	if result.Comparison == nil || !result.Comparison.ContractChanged || result.Comparison.Growth {
		t.Fatalf("comparison=%#v", result.Comparison)
	}
}

func TestRunRejectsFailureGateWithoutComparison(t *testing.T) {
	_, err := run([]string{"--fail-on-change"}, bytes.NewReader([]byte(`{}`)), &bytes.Buffer{})
	if err == nil {
		t.Fatal("expected --fail-on-change to require --compare")
	}
}

func writeReportFixture(t *testing.T, path string, value report) {
	t.Helper()
	data, err := json.MarshalIndent(value, "", "  ")
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, append(data, '\n'), 0o644); err != nil {
		t.Fatal(err)
	}
}
