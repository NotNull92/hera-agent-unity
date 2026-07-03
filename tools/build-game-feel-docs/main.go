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
	"bufio"
	"compress/gzip"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"os"
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
		case e.Key == "":
			log.Fatalf("%s:%d: missing key", *in, lineNo)
		case e.Title == "":
			log.Fatalf("%s:%d (%s): missing title", *in, lineNo, e.Key)
		case e.Body == "":
			log.Fatalf("%s:%d (%s): missing body", *in, lineNo, e.Key)
		case seen[e.Key]:
			log.Fatalf("%s:%d: duplicate key %q", *in, lineNo, e.Key)
		case !knownCategories[e.Category]:
			log.Fatalf("%s:%d (%s): unknown category %q", *in, lineNo, e.Key, e.Category)
		}
		seen[e.Key] = true
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
