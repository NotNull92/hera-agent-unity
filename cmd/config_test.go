package cmd

import (
	"testing"
	"time"
)

func TestGlobalConfigIsIsolatedBetweenParses(t *testing.T) {
	// Given
	for _, name := range []string{
		"HERA_AGENT_PORT",
		"HERA_AGENT_PROJECT",
		"HERA_AGENT_TIMEOUT_MS",
		"HERA_AGENT_VERBOSE",
		"HERA_AGENT_QUIET",
		"HERA_AGENT_DEBUG",
		"HERA_AGENT_COMPACT_JSON",
		"HERA_AGENT_NARRATE",
	} {
		t.Setenv(name, "")
	}
	firstArgs := []string{"--port", "8123", "--timeout", "120000", "--quiet", "scene", "info"}
	secondArgs := []string{"scene", "info"}

	// When
	first, firstCommand, err := parseGlobalConfig(firstArgs)
	if err != nil {
		t.Fatal(err)
	}
	second, secondCommand, err := parseGlobalConfig(secondArgs)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if first.Port != 8123 || first.Timeout != 120*time.Second || !first.Quiet {
		t.Fatalf("first=%#v", first)
	}
	if second.Port != 0 || second.Timeout != 60*time.Second || second.Quiet {
		t.Fatalf("second=%#v", second)
	}
	if firstCommand[0] != "scene" || secondCommand[0] != "scene" {
		t.Fatalf("commands=%#v %#v", firstCommand, secondCommand)
	}
}
