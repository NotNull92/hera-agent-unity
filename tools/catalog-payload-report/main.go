package main

import (
	"bytes"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"slices"
	"sort"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

const (
	reportSchema = "hera.catalog-payload-report/1"
	// reviewRequiredExitCode is preserved by a built binary. `go run` wraps
	// child failures as its own non-zero exit and prints `exit status 3`.
	reviewRequiredExitCode = 3
)

type reportOptions struct {
	WarnProfileBytes int
	WarnToolBytes    int
	Largest          int
}

type report struct {
	Schema                 string            `json:"schema"`
	CatalogHash            string            `json:"catalog_hash"`
	InputBytes             int               `json:"input_bytes"`
	NormalizedCatalogBytes int               `json:"normalized_catalog_bytes"`
	ToolCount              int               `json:"tool_count"`
	ActionCount            int               `json:"action_count"`
	DescriptionCharacters  int               `json:"description_characters"`
	Profiles               []profileSize     `json:"profiles"`
	LargestTools           []toolSize        `json:"largest_tools"`
	LargestActions         []actionSize      `json:"largest_actions"`
	ActionDescribeSavings  []describeSaving  `json:"action_describe_savings"`
	RoughTokens            tokenEstimates    `json:"rough_token_estimates"`
	Warnings               []string          `json:"warnings"`
	Comparison             *reportComparison `json:"comparison,omitempty"`
}

type profileSize struct {
	Name                    string `json:"name"`
	ToolCount               int    `json:"tool_count"`
	NormalizedContractBytes int    `json:"normalized_contract_bytes"`
}

type reportComparison struct {
	BaselineCatalogHash         string         `json:"baseline_catalog_hash"`
	ContractChanged             bool           `json:"contract_changed"`
	ToolCountDelta              int            `json:"tool_count_delta"`
	ActionCountDelta            int            `json:"action_count_delta"`
	NormalizedCatalogBytesDelta int            `json:"normalized_catalog_bytes_delta"`
	DescriptionCharactersDelta  int            `json:"description_characters_delta"`
	Profiles                    []profileDelta `json:"profiles"`
	Growth                      bool           `json:"growth"`
	ReviewRequired              bool           `json:"review_required"`
	Reasons                     []string       `json:"reasons"`
}

type profileDelta struct {
	Name                  string `json:"name"`
	BaselineToolCount     int    `json:"baseline_tool_count"`
	CurrentToolCount      int    `json:"current_tool_count"`
	ToolCountDelta        int    `json:"tool_count_delta"`
	BaselineContractBytes int    `json:"baseline_contract_bytes"`
	CurrentContractBytes  int    `json:"current_contract_bytes"`
	ContractBytesDelta    int    `json:"contract_bytes_delta"`
}

type toolSize struct {
	Name              string `json:"name"`
	Bytes             int    `json:"bytes"`
	ActionCount       int    `json:"action_count"`
	DescriptionLength int    `json:"description_characters"`
}

type actionSize struct {
	Tool              string `json:"tool"`
	Action            string `json:"action"`
	Bytes             int    `json:"bytes"`
	DescriptionLength int    `json:"description_characters"`
}

type describeSaving struct {
	Tool        string  `json:"tool"`
	Action      string  `json:"action"`
	FullBytes   int     `json:"full_tool_describe_bytes"`
	ActionBytes int     `json:"action_describe_bytes"`
	SavedBytes  int     `json:"saved_bytes"`
	SavedRatio  float64 `json:"saved_ratio"`
}

type compactToolIdentity struct {
	Name         string              `json:"name"`
	Title        string              `json:"title"`
	Description  string              `json:"description"`
	Source       toolregistry.Source `json:"source"`
	ContractMode string              `json:"contract_mode"`
	Profiles     []string            `json:"profiles"`
	Aliases      []string            `json:"aliases"`
}

type tokenEstimates struct {
	Assumption string `json:"assumption"`
	Low        int    `json:"low_4_bytes_per_token"`
	Central    int    `json:"central_3_2_bytes_per_token"`
	High       int    `json:"high_2_5_bytes_per_token"`
}

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

func buildReport(catalog *toolregistry.Catalog, inputBytes int, options reportOptions) (report, error) {
	if options.Largest < 1 {
		return report{}, fmt.Errorf("largest must be positive")
	}
	normalized, err := json.Marshal(catalog)
	if err != nil {
		return report{}, fmt.Errorf("encode normalized catalog: %w", err)
	}
	result := report{
		Schema:                 reportSchema,
		CatalogHash:            catalog.CatalogHash,
		InputBytes:             inputBytes,
		NormalizedCatalogBytes: len(normalized),
		ToolCount:              len(catalog.Tools),
		Warnings:               []string{},
	}
	profiles := make(map[string]struct{})
	for _, tool := range catalog.Tools {
		result.ActionCount += len(tool.Actions)
		result.DescriptionCharacters += len([]rune(tool.Description))
		encoded, marshalErr := json.Marshal(tool)
		if marshalErr != nil {
			return report{}, fmt.Errorf("encode tool %s: %w", tool.Name, marshalErr)
		}
		result.LargestTools = append(result.LargestTools, toolSize{
			Name: tool.Name, Bytes: len(encoded), ActionCount: len(tool.Actions),
			DescriptionLength: len([]rune(tool.Description)),
		})
		if options.WarnToolBytes > 0 && len(encoded) > options.WarnToolBytes {
			result.Warnings = append(result.Warnings,
				fmt.Sprintf("tool %s uses %d normalized bytes (warning budget %d)", tool.Name, len(encoded), options.WarnToolBytes))
		}
		for _, profile := range tool.Profiles {
			profiles[profile] = struct{}{}
		}
		fullDescribe, describeErr := json.Marshal(struct {
			Tool        toolregistry.Tool `json:"tool"`
			CatalogHash string            `json:"catalog_hash"`
			DomainEpoch string            `json:"domain_epoch"`
		}{Tool: tool, CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch})
		if describeErr != nil {
			return report{}, fmt.Errorf("encode full description %s: %w", tool.Name, describeErr)
		}
		for _, action := range tool.Actions {
			actionEncoded, actionErr := json.Marshal(action)
			if actionErr != nil {
				return report{}, fmt.Errorf("encode action %s/%s: %w", tool.Name, action.Name, actionErr)
			}
			result.LargestActions = append(result.LargestActions, actionSize{
				Tool: tool.Name, Action: action.Name, Bytes: len(actionEncoded),
				DescriptionLength: len([]rune(action.Description)),
			})
			selectedDescribe, selectedErr := json.Marshal(struct {
				Tool        compactToolIdentity `json:"tool"`
				Action      toolregistry.Action `json:"action"`
				ToolSafety  toolregistry.Safety `json:"tool_safety"`
				CatalogHash string              `json:"catalog_hash"`
				DomainEpoch string              `json:"domain_epoch"`
			}{
				Tool: compactToolIdentity{
					Name: tool.Name, Title: tool.Title, Description: tool.Description,
					Source: tool.Source, ContractMode: tool.ContractMode,
					Profiles: tool.Profiles, Aliases: tool.Aliases,
				},
				Action: action, ToolSafety: tool.Safety,
				CatalogHash: catalog.CatalogHash, DomainEpoch: catalog.DomainEpoch,
			})
			if selectedErr != nil {
				return report{}, fmt.Errorf("encode selected description %s/%s: %w", tool.Name, action.Name, selectedErr)
			}
			saved := len(fullDescribe) - len(selectedDescribe)
			ratio := 0.0
			if len(fullDescribe) > 0 {
				ratio = float64(saved) / float64(len(fullDescribe))
			}
			result.ActionDescribeSavings = append(result.ActionDescribeSavings, describeSaving{
				Tool: tool.Name, Action: action.Name, FullBytes: len(fullDescribe),
				ActionBytes: len(selectedDescribe), SavedBytes: saved, SavedRatio: ratio,
			})
		}
	}
	profileNames := make([]string, 0, len(profiles))
	for name := range profiles {
		profileNames = append(profileNames, name)
	}
	sort.Strings(profileNames)
	for _, name := range profileNames {
		tools, profileErr := catalog.ToolsForProfile(name)
		if profileErr != nil {
			return report{}, fmt.Errorf("resolve profile %s: %w", name, profileErr)
		}
		encoded, marshalErr := json.Marshal(tools)
		if marshalErr != nil {
			return report{}, fmt.Errorf("encode profile %s: %w", name, marshalErr)
		}
		size := profileSize{Name: name, ToolCount: len(tools), NormalizedContractBytes: len(encoded)}
		result.Profiles = append(result.Profiles, size)
		if options.WarnProfileBytes > 0 && size.NormalizedContractBytes > options.WarnProfileBytes {
			result.Warnings = append(result.Warnings,
				fmt.Sprintf("profile %s uses %d normalized bytes (warning budget %d)", name, size.NormalizedContractBytes, options.WarnProfileBytes))
		}
	}
	sort.Slice(result.LargestTools, func(i, j int) bool {
		if result.LargestTools[i].Bytes != result.LargestTools[j].Bytes {
			return result.LargestTools[i].Bytes > result.LargestTools[j].Bytes
		}
		return result.LargestTools[i].Name < result.LargestTools[j].Name
	})
	sort.Slice(result.LargestActions, func(i, j int) bool {
		if result.LargestActions[i].Bytes != result.LargestActions[j].Bytes {
			return result.LargestActions[i].Bytes > result.LargestActions[j].Bytes
		}
		if result.LargestActions[i].Tool != result.LargestActions[j].Tool {
			return result.LargestActions[i].Tool < result.LargestActions[j].Tool
		}
		return result.LargestActions[i].Action < result.LargestActions[j].Action
	})
	sort.Slice(result.ActionDescribeSavings, func(i, j int) bool {
		if result.ActionDescribeSavings[i].SavedBytes != result.ActionDescribeSavings[j].SavedBytes {
			return result.ActionDescribeSavings[i].SavedBytes > result.ActionDescribeSavings[j].SavedBytes
		}
		if result.ActionDescribeSavings[i].Tool != result.ActionDescribeSavings[j].Tool {
			return result.ActionDescribeSavings[i].Tool < result.ActionDescribeSavings[j].Tool
		}
		return result.ActionDescribeSavings[i].Action < result.ActionDescribeSavings[j].Action
	})
	result.LargestTools = result.LargestTools[:min(options.Largest, len(result.LargestTools))]
	result.LargestActions = result.LargestActions[:min(options.Largest, len(result.LargestActions))]
	result.ActionDescribeSavings = result.ActionDescribeSavings[:min(options.Largest, len(result.ActionDescribeSavings))]
	result.RoughTokens = tokenRange(result.NormalizedCatalogBytes)
	slices.Sort(result.Warnings)
	return result, nil
}

func compareReports(baseline, current report) reportComparison {
	comparison := reportComparison{
		BaselineCatalogHash:         baseline.CatalogHash,
		ContractChanged:             baseline.CatalogHash != current.CatalogHash,
		ToolCountDelta:              current.ToolCount - baseline.ToolCount,
		ActionCountDelta:            current.ActionCount - baseline.ActionCount,
		NormalizedCatalogBytesDelta: current.NormalizedCatalogBytes - baseline.NormalizedCatalogBytes,
		DescriptionCharactersDelta:  current.DescriptionCharacters - baseline.DescriptionCharacters,
		Profiles:                    compareProfiles(baseline.Profiles, current.Profiles),
	}

	if comparison.ContractChanged {
		comparison.Reasons = append(comparison.Reasons, "catalog contract hash changed")
	}
	if comparison.ToolCountDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("tool count increased by %d", comparison.ToolCountDelta))
	}
	if comparison.ActionCountDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("action count increased by %d", comparison.ActionCountDelta))
	}
	if comparison.DescriptionCharactersDelta > 0 {
		comparison.Growth = true
		comparison.Reasons = append(comparison.Reasons,
			fmt.Sprintf("tool descriptions grew by %d characters", comparison.DescriptionCharactersDelta))
	}
	for _, profile := range comparison.Profiles {
		switch {
		case profile.ToolCountDelta > 0 && profile.ContractBytesDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s grew by %d tool(s) and %d byte(s)",
					profile.Name, profile.ToolCountDelta, profile.ContractBytesDelta))
		case profile.ToolCountDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s gained %d tool(s)", profile.Name, profile.ToolCountDelta))
		case profile.ContractBytesDelta > 0:
			comparison.Growth = true
			comparison.Reasons = append(comparison.Reasons,
				fmt.Sprintf("profile %s grew by %d byte(s)", profile.Name, profile.ContractBytesDelta))
		}
	}
	comparison.ReviewRequired = comparison.ContractChanged || comparison.Growth
	if len(comparison.Reasons) == 0 {
		comparison.Reasons = []string{}
	}
	return comparison
}

func compareProfiles(baseline, current []profileSize) []profileDelta {
	baselineByName := make(map[string]profileSize, len(baseline))
	currentByName := make(map[string]profileSize, len(current))
	names := make(map[string]struct{}, len(baseline)+len(current))
	for _, profile := range baseline {
		baselineByName[profile.Name] = profile
		names[profile.Name] = struct{}{}
	}
	for _, profile := range current {
		currentByName[profile.Name] = profile
		names[profile.Name] = struct{}{}
	}
	orderedNames := make([]string, 0, len(names))
	for name := range names {
		orderedNames = append(orderedNames, name)
	}
	sort.Strings(orderedNames)

	result := make([]profileDelta, 0, len(orderedNames))
	for _, name := range orderedNames {
		before := baselineByName[name]
		after := currentByName[name]
		result = append(result, profileDelta{
			Name:                  name,
			BaselineToolCount:     before.ToolCount,
			CurrentToolCount:      after.ToolCount,
			ToolCountDelta:        after.ToolCount - before.ToolCount,
			BaselineContractBytes: before.NormalizedContractBytes,
			CurrentContractBytes:  after.NormalizedContractBytes,
			ContractBytesDelta:    after.NormalizedContractBytes - before.NormalizedContractBytes,
		})
	}
	return result
}

func tokenRange(size int) tokenEstimates {
	return tokenEstimates{
		Assumption: "rough JSON estimate only; use provider usage/tokenizer for billing decisions",
		Low:        divideRoundUp(size*10, 40),
		Central:    divideRoundUp(size*10, 32),
		High:       divideRoundUp(size*10, 25),
	}
}

func divideRoundUp(numerator, denominator int) int {
	return (numerator + denominator - 1) / denominator
}

func min(left, right int) int {
	if left < right {
		return left
	}
	return right
}
