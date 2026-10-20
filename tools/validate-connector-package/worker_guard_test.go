package main

import (
	"os"
	"path/filepath"
	"regexp"
	"testing"
)

// This source gate covers startup ordering; actual worker execution is a live gate.
func TestWorkerStartupGuards(t *testing.T) {
	for _, file := range []string{
		"HttpServer", "Heartbeat", "Core/PackageJobState",
		"TestRunner/TestRunnerState", "Core/InputQaRecording",
	} {
		t.Run(file, func(t *testing.T) {
			source, err := os.ReadFile(filepath.Join("..", "..", "AgentConnector", "Editor", file+".cs"))
			if err != nil {
				t.Fatal(err)
			}
			constructor := regexp.MustCompile(`static\s+` + filepath.Base(file) +
				`\(\)\s*\{\s*if\s*\(AssetDatabase\.IsAssetImportWorkerProcess\(\)\)\s*return;`)
			if !constructor.Match(source) {
				t.Fatal("worker must exit the static constructor before startup or event subscriptions")
			}
		})
	}
}
