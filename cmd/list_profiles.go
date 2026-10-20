package cmd

import (
	"context"
	"slices"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func hasListProfileSelector(args []string) bool {
	for _, arg := range args {
		if arg == "--profile" || arg == "--profiles" || strings.HasPrefix(arg, "--profile=") || strings.HasPrefix(arg, "--profiles=") {
			return true
		}
	}
	return false
}

func listProfilesCmd(ctx context.Context, inst *client.Instance, args []string, load callLoadFunc) (*client.CommandResponse, error) {
	profile := ""
	countsOnly := len(args) == 1 && args[0] == "--profiles"
	if len(args) == 2 && args[0] == "--profile" {
		profile = args[1]
	} else if len(args) == 1 && strings.HasPrefix(args[0], "--profile=") {
		profile = strings.TrimPrefix(args[0], "--profile=")
	}
	if !countsOnly && (profile == "" || strings.HasPrefix(profile, "--")) {
		return callPolicyResponse("INVALID_ARGUMENT", "Use either --profiles or --profile <name>, without other list selectors.", nil)
	}
	snapshot, err := load(ctx, inst)
	if err != nil {
		return nil, err
	}
	counts := map[string]int{}
	type slimTool struct {
		Name        string `json:"name"`
		Description string `json:"description"`
	}
	selected := []slimTool{}
	for _, tool := range snapshot.Catalog.Tools {
		for _, name := range tool.Profiles {
			counts[name]++
		}
		if slices.Contains(tool.Profiles, profile) {
			selected = append(selected, slimTool{tool.Name, tool.Description})
		}
	}
	if countsOnly {
		return callDataResponse("", counts)
	}
	if counts[profile] == 0 {
		valid := make([]string, 0, len(counts))
		for name := range counts {
			valid = append(valid, name)
		}
		slices.Sort(valid)
		return callPolicyResponse("INVALID_ARGUMENT", "Unknown tool profile.", map[string]any{"profile": profile, "valid_profiles": valid})
	}
	slices.SortFunc(selected, func(a, b slimTool) int { return strings.Compare(a.Name, b.Name) })
	return callDataResponse("", selected)
}
