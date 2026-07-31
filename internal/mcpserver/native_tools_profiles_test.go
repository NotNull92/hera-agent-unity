package mcpserver

import (
	"context"
	"slices"
	"testing"
)

func TestProfileRegistersExpectedTools(t *testing.T) {
	expected := map[string][]string{
		"assets":      {"manage_assets"},
		"core":        {"manage_gameobject", "scene"},
		"diagnostics": {"console"},
		"scene":       {"manage_gameobject", "scene"},
		"testing":     {"console"},
		"ui":          {"manage_gameobject"},
	}
	for profile, want := range expected {
		t.Run(profile, func(t *testing.T) {
			// Given
			session, closeSession := startNativeTestSession(t, profile, &recordingToolSender{
				response: successResponse(),
			})
			defer closeSession()

			// When
			result, err := session.ListTools(context.Background(), nil)

			// Then
			if err != nil {
				t.Fatal(err)
			}
			if got := mcpToolNames(result.Tools); !slices.Equal(got, want) {
				t.Fatalf("tools = %v, want %v", got, want)
			}
		})
	}
}

func TestProfileOrderingStable(t *testing.T) {
	// Given
	session, closeSession := startNativeTestSession(t, "core", &recordingToolSender{
		response: successResponse(),
	})
	defer closeSession()

	// When
	first, err := session.ListTools(context.Background(), nil)
	if err != nil {
		t.Fatal(err)
	}
	second, err := session.ListTools(context.Background(), nil)
	if err != nil {
		t.Fatal(err)
	}

	// Then
	want := []string{"manage_gameobject", "scene"}
	if !slices.Equal(mcpToolNames(first.Tools), want) || !slices.Equal(mcpToolNames(second.Tools), want) {
		t.Fatalf("tool order changed: first=%v second=%v", mcpToolNames(first.Tools), mcpToolNames(second.Tools))
	}
}

func TestExecAbsentFromNormalProfiles(t *testing.T) {
	for _, profile := range []string{"core", "scene", "assets", "ui", "diagnostics", "testing"} {
		t.Run(profile, func(t *testing.T) {
			session, closeSession := startNativeTestSession(t, profile, &recordingToolSender{
				response: successResponse(),
			})
			defer closeSession()
			result, err := session.ListTools(context.Background(), nil)
			if err != nil {
				t.Fatal(err)
			}
			if slices.Contains(mcpToolNames(result.Tools), "exec") {
				t.Fatalf("profile %q exposed exec", profile)
			}
		})
	}
}
