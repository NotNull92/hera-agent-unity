// build-ui-slop-docs validates and compresses the curated Unity UI-slop
// taxonomy (ui_slop.jsonl in this directory, the checked-in source of truth)
// into the bundle the connector ships.
// Run after editing ui_slop.jsonl, commit both files.
//
//	go run ./tools/build-ui-slop-docs
//
// Line shape (see AgentConnector/Editor/Core/UiSlopStore.cs):
//
//	{"id":"box-in-box","area":"B","severity":"strong","tell":"...","check_ugui":"...","exception":"...|null","fix":"...","borrow":{...}|null,"deep_topic":"layout"}
package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"log"

	"github.com/NotNull92/hera-agent-unity/internal/docbundle"
)

type Entry struct {
	ID        string          `json:"id"`
	Area      string          `json:"area"`
	Severity  string          `json:"severity"`
	Tell      string          `json:"tell"`
	CheckUGUI string          `json:"check_ugui"`
	Exception json.RawMessage `json:"exception"`
	Fix       string          `json:"fix"`
	Borrow    json.RawMessage `json:"borrow"`
	DeepTopic string          `json:"deep_topic"`
}

// Areas double as the fixed execution order: A decorative sweep, B layout /
// RectTransform / containers, C spacing, D typography, E color. Inspection runs
// in parallel but fixes land in this order, so an upstream fix dissolves the
// conflicts a downstream one would otherwise hit.
var knownAreas = map[string]bool{"A": true, "B": true, "C": true, "D": true, "E": true}
var knownSeverity = map[string]bool{"strong": true, "weak": true}

// deep_topic is a closed set, and UiSlop surfaces it verbatim to agents — a
// typo would ship a pointer that resolves to nothing.
var knownDeepTopics = map[string]bool{
	"decoration": true, "layout": true, "spacing": true, "typography": true, "color": true,
}

func main() {
	in := flag.String("in", "tools/build-ui-slop-docs/ui_slop.jsonl",
		"Path to the source JSONL.")
	out := flag.String("out", "AgentConnector/Editor/Data/ui_slop_1.0.jsonl.gz.bytes",
		"Output gzipped JSONL path.")
	flag.Parse()

	seen := map[string]bool{}
	entries, size, err := docbundle.Build(*in, *out, func(line []byte, _ int) error {
		var e Entry
		if err := json.Unmarshal(line, &e); err != nil {
			return fmt.Errorf("invalid JSON: %w", err)
		}
		switch {
		case e.ID == "":
			return fmt.Errorf("missing id")
		case !knownAreas[e.Area]:
			return fmt.Errorf("(%s): invalid area %q (use A-E)", e.ID, e.Area)
		case !knownSeverity[e.Severity]:
			return fmt.Errorf("(%s): invalid severity %q (use strong|weak)", e.ID, e.Severity)
		case e.Tell == "":
			return fmt.Errorf("(%s): missing tell", e.ID)
		case e.CheckUGUI == "":
			return fmt.Errorf("(%s): missing check_ugui", e.ID)
		case e.Fix == "":
			return fmt.Errorf("(%s): missing fix", e.ID)
		case !knownDeepTopics[e.DeepTopic]:
			return fmt.Errorf("(%s): invalid deep_topic %q (use decoration|layout|spacing|typography|color)", e.ID, e.DeepTopic)
		case len(e.Exception) == 0:
			return fmt.Errorf("(%s): missing exception key (use null when none)", e.ID)
		case len(e.Borrow) == 0:
			return fmt.Errorf("(%s): missing borrow key (use null for deletion-type)", e.ID)
		case seen[e.ID]:
			return fmt.Errorf("duplicate id %q", e.ID)
		}
		seen[e.ID] = true
		return nil
	})
	if err != nil {
		log.Fatal(err)
	}
	fmt.Printf("wrote %s: %d entries, %d bytes gzipped\n", *out, entries, size)
}
