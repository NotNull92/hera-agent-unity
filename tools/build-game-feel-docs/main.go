// build-game-feel-docs validates and compresses the curated Game Feel
// knowledge base (game_feel.jsonl in this directory — the checked-in source
// of truth, curated from the Game Feel & Juice Bible and the Ethical
// Engagement Game Feel Framework) into the bundle the connector ships.
// Run after editing game_feel.jsonl, commit both files.
//
//	go run ./tools/build-game-feel-docs
//
// Input/output line shape (see AgentConnector/Editor/Core/GameFeelStore.cs):
//
//	{"key":"screen_shake","category":"technique","title":"Screen Shake","body":"..."}
package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"log"

	"github.com/NotNull92/hera-agent-unity/internal/docbundle"
)

type Entry struct {
	Key      string `json:"key"`
	Category string `json:"category"`
	Title    string `json:"title"`
	Body     string `json:"body"`
}

var knownCategories = map[string]bool{
	"ethics":       true,
	"theory":       true,
	"technique":    true,
	"ui":           true,
	"workflow":     true,
	"anti_pattern": true,
	"checklist":    true,
}

func main() {
	in := flag.String("in", "tools/build-game-feel-docs/game_feel.jsonl",
		"Path to the source JSONL.")
	out := flag.String("out", "AgentConnector/Editor/Data/game_feel_1.0.jsonl.gz.bytes",
		"Output gzipped JSONL path.")
	flag.Parse()

	seen := map[string]bool{}
	entries, size, err := docbundle.Build(*in, *out, func(line []byte, _ int) error {
		var e Entry
		if err := json.Unmarshal(line, &e); err != nil {
			return fmt.Errorf("invalid JSON: %w", err)
		}
		switch {
		case e.Key == "":
			return fmt.Errorf("missing key")
		case e.Title == "":
			return fmt.Errorf("(%s): missing title", e.Key)
		case e.Body == "":
			return fmt.Errorf("(%s): missing body", e.Key)
		case seen[e.Key]:
			return fmt.Errorf("duplicate key %q", e.Key)
		case !knownCategories[e.Category]:
			return fmt.Errorf("(%s): unknown category %q", e.Key, e.Category)
		}
		seen[e.Key] = true
		return nil
	})
	if err != nil {
		log.Fatal(err)
	}
	fmt.Printf("wrote %s: %d entries, %d bytes gzipped\n", *out, entries, size)
}
