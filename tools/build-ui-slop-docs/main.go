// build-ui-slop-docs validates and compresses the curated Unity UI-slop
// taxonomy (ui_slop.jsonl in this directory — the checked-in source of truth,
// ported from the slopslap methodology and grounded in live hera measurement /
// per-version editor-binary reflection) into the bundle the connector ships.
// Run after editing ui_slop.jsonl, commit both files.
//
//	go run ./tools/build-ui-slop-docs
//
// Line shape (see AgentConnector/Editor/Core/UiSlopStore.cs):
//
//	{"id":"box-in-box","area":"B","severity":"strong","tell":"...","check_ugui":"...","check_uitk":"...","exception":"...|null","fix":"...","borrow":{...}|null,"deep_topic":"layout"}
package main

import (
	"bufio"
	"compress/gzip"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"os"
)

type Entry struct {
	ID        string          `json:"id"`
	Area      string          `json:"area"`
	Severity  string          `json:"severity"`
	Tell      string          `json:"tell"`
	CheckUGUI string          `json:"check_ugui"`
	CheckUITK string          `json:"check_uitk"`
	Exception json.RawMessage `json:"exception"`
	Fix       string          `json:"fix"`
	Borrow    json.RawMessage `json:"borrow"`
	DeepTopic string          `json:"deep_topic"`
}

// Areas mirror the pipeline's fixed execution order A -> B -> C -> D -> E
// (see .claude/skills/unity-deslop/references/ui-slop-taxonomy.md).
var knownAreas = map[string]bool{"A": true, "B": true, "C": true, "D": true, "E": true}
var knownSeverity = map[string]bool{"strong": true, "weak": true}

func main() {
	in := flag.String("in", "tools/build-ui-slop-docs/ui_slop.jsonl",
		"Path to the source JSONL.")
	out := flag.String("out", "AgentConnector/Editor/Data/ui_slop_1.0.jsonl.gz.bytes",
		"Output gzipped JSONL path.")
	flag.Parse()

	src, err := os.Open(*in)
	if err != nil {
		log.Fatalf("open %s: %v", *in, err)
	}
	defer src.Close()

	seen := map[string]bool{}
	var lines []string
	scanner := bufio.NewScanner(src)
	scanner.Buffer(make([]byte, 0, 1024*1024), 1024*1024)
	lineNo := 0
	for scanner.Scan() {
		lineNo++
		line := scanner.Text()
		if len(line) == 0 {
			continue
		}
		var e Entry
		if err := json.Unmarshal([]byte(line), &e); err != nil {
			log.Fatalf("%s:%d: invalid JSON: %v", *in, lineNo, err)
		}
		switch {
		case e.ID == "":
			log.Fatalf("%s:%d: missing id", *in, lineNo)
		case !knownAreas[e.Area]:
			log.Fatalf("%s:%d (%s): invalid area %q (use A-E)", *in, lineNo, e.ID, e.Area)
		case !knownSeverity[e.Severity]:
			log.Fatalf("%s:%d (%s): invalid severity %q (use strong|weak)", *in, lineNo, e.ID, e.Severity)
		case e.Tell == "":
			log.Fatalf("%s:%d (%s): missing tell", *in, lineNo, e.ID)
		case e.CheckUGUI == "":
			log.Fatalf("%s:%d (%s): missing check_ugui", *in, lineNo, e.ID)
		case e.CheckUITK == "":
			log.Fatalf("%s:%d (%s): missing check_uitk", *in, lineNo, e.ID)
		case e.Fix == "":
			log.Fatalf("%s:%d (%s): missing fix", *in, lineNo, e.ID)
		case e.DeepTopic == "":
			log.Fatalf("%s:%d (%s): missing deep_topic", *in, lineNo, e.ID)
		case len(e.Exception) == 0:
			log.Fatalf("%s:%d (%s): missing exception key (use null when none)", *in, lineNo, e.ID)
		case len(e.Borrow) == 0:
			log.Fatalf("%s:%d (%s): missing borrow key (use null for deletion-type)", *in, lineNo, e.ID)
		case seen[e.ID]:
			log.Fatalf("%s:%d: duplicate id %q", *in, lineNo, e.ID)
		}
		seen[e.ID] = true
		lines = append(lines, line)
	}
	if err := scanner.Err(); err != nil {
		log.Fatalf("read %s: %v", *in, err)
	}
	if len(lines) == 0 {
		log.Fatalf("%s: no entries", *in)
	}

	dst, err := os.Create(*out)
	if err != nil {
		log.Fatalf("create %s: %v", *out, err)
	}
	defer dst.Close()

	gz, err := gzip.NewWriterLevel(dst, gzip.BestCompression)
	if err != nil {
		log.Fatalf("gzip: %v", err)
	}
	for _, line := range lines {
		if _, err := gz.Write([]byte(line + "\n")); err != nil {
			log.Fatalf("write: %v", err)
		}
	}
	if err := gz.Close(); err != nil {
		log.Fatalf("close gzip: %v", err)
	}

	info, err := os.Stat(*out)
	if err != nil {
		log.Fatalf("stat %s: %v", *out, err)
	}
	fmt.Printf("wrote %s: %d entries, %d bytes gzipped\n", *out, len(lines), info.Size())
}
