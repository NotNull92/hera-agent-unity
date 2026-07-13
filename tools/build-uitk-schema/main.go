// build-uitk-schema validates and compresses a UI Toolkit schema JSONL produced
// by dump_uitk_schema.cs (run in-editor via `hera-agent-unity exec --file`) into
// the per-version bundle the connector ships. Run once per Unity version, commit
// the resulting bundle + its .meta.
//
//  1. hera-agent-unity exec --file tools/build-uitk-schema/dump_uitk_schema.cs
//     -> writes <editor temp>/uitk_schema_<bucket>.jsonl, returns its path
//  2. go run ./tools/build-uitk-schema --in <that path>
//     -> AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes
//
// The output path defaults from the meta line's bucket, so one command handles
// any version. Line shapes (see AgentConnector/Editor/Core/UiToolkitStore.cs):
//
//	{"kind":"meta","unity_version":"2022.3.62f2","bucket":"2022.3","uxml_element_attribute":false,"uxml_traits":"present","elements":48,"structural":4,"uss_properties":92}
//	{"kind":"element"|"structural","element":"Button","full_type":"UnityEngine.UIElements.Button","surface":"runtime","attributes":[{"name":"text","type":"string","default":""}]}
//	{"kind":"uss","name":"flex-direction","animatable":true}
package main

import (
	"bufio"
	"compress/gzip"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"os"
	"path/filepath"
)

type entry struct {
	Kind         string `json:"kind"`
	Element      string `json:"element"`
	FullType     string `json:"full_type"`
	Surface      string `json:"surface"`
	Name         string `json:"name"`
	Bucket       string `json:"bucket"`
	UnityVersion string `json:"unity_version"`
}

func main() {
	in := flag.String("in", "",
		"Path to the JSONL produced by dump_uitk_schema.cs.")
	out := flag.String("out", "",
		"Output bundle path. Defaults to AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes from the meta line.")
	flag.Parse()

	if *in == "" {
		fmt.Fprintln(os.Stderr, "usage: build-uitk-schema --in <uitk_schema_<bucket>.jsonl> [--out <bundle>]")
		os.Exit(2)
	}

	src, err := os.Open(*in)
	if err != nil {
		log.Fatalf("open %s: %v", *in, err)
	}
	defer src.Close()

	validSurface := map[string]bool{"runtime": true, "editor": true, "other": true}
	// Dedup by full type, not short name: distinct types can share a UXML tag
	// name across the ui: (runtime) and uie: (editor) namespaces.
	seenTypes := map[string]bool{}
	ussCount := 0
	structuralCount := 0
	var meta *entry
	var lines []string

	sc := bufio.NewScanner(src)
	sc.Buffer(make([]byte, 0, 1024*1024), 1024*1024)
	n := 0
	for sc.Scan() {
		n++
		line := sc.Text()
		if len(line) == 0 {
			continue
		}
		var e entry
		if err := json.Unmarshal([]byte(line), &e); err != nil {
			log.Fatalf("%s:%d: invalid JSON: %v", *in, n, err)
		}
		switch e.Kind {
		case "meta":
			if meta != nil {
				log.Fatalf("%s:%d: duplicate meta line", *in, n)
			}
			if e.Bucket == "" {
				log.Fatalf("%s:%d: meta missing bucket", *in, n)
			}
			m := e
			meta = &m
		case "element", "structural":
			if e.Element == "" {
				log.Fatalf("%s:%d: %s missing element", *in, n, e.Kind)
			}
			if !validSurface[e.Surface] {
				log.Fatalf("%s:%d (%s): invalid surface %q", *in, n, e.Element, e.Surface)
			}
			if e.Kind == "structural" {
				structuralCount++
				break
			}
			if e.FullType == "" {
				log.Fatalf("%s:%d (%s): element missing full_type", *in, n, e.Element)
			}
			if seenTypes[e.FullType] {
				log.Fatalf("%s:%d: duplicate element type %q", *in, n, e.FullType)
			}
			seenTypes[e.FullType] = true
		case "uss":
			if e.Name == "" {
				log.Fatalf("%s:%d: uss missing name", *in, n)
			}
			ussCount++
		default:
			log.Fatalf("%s:%d: unknown kind %q", *in, n, e.Kind)
		}
		lines = append(lines, line)
	}
	if err := sc.Err(); err != nil {
		log.Fatalf("read %s: %v", *in, err)
	}
	if meta == nil {
		log.Fatalf("%s: missing meta line", *in)
	}
	if len(seenTypes) == 0 {
		log.Fatalf("%s: no element entries", *in)
	}
	if ussCount == 0 {
		log.Fatalf("%s: no uss entries", *in)
	}

	outPath := *out
	if outPath == "" {
		outPath = filepath.Join("AgentConnector", "Editor", "Data", "uitk_schema_"+meta.Bucket+".jsonl.gz.bytes")
	}

	dst, err := os.Create(outPath)
	if err != nil {
		log.Fatalf("create %s: %v", outPath, err)
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

	info, err := os.Stat(outPath)
	if err != nil {
		log.Fatalf("stat %s: %v", outPath, err)
	}
	fmt.Printf("wrote %s: %d elements, %d structural, %d uss, %d bytes gzipped (bucket %s, unity %s)\n",
		outPath, len(seenTypes), structuralCount, ussCount, info.Size(), meta.Bucket, meta.UnityVersion)
}
