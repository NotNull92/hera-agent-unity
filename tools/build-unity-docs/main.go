// build-unity-docs converts the offline Unity ScriptReference HTML tree into
// a single JSONL file that ships with the connector. Run once per Unity
// version, commit the result. Mirrors the C# UnityDocsParser regex set so
// the Connector can drop its HTML-parsing path entirely.
//
//	go run ./tools/build-unity-docs \
//	    --in  C:\Users\PC\Downloads\UnityDocumentation\Documentation\en \
//	    --out AgentConnector/Editor/Data/unity_docs_6.0.jsonl
//
// Output line shape:
//
//	{"name":"Rigidbody-mass","title":"Rigidbody.mass","signature":"public float mass;",
//	 "summary":"The mass of the rigidbody.","manual_url":"Manual/class-Rigidbody.html",
//	 "scriptreference_url":"ScriptReference/Rigidbody-mass.html","unity_version":"6.0"}
//
// `name` is the filename without `.html` so the Connector's existing
// query→candidate-filename mapping doubles as the dict lookup key.
package main

import (
	"bufio"
	"compress/gzip"
	"encoding/json"
	"flag"
	"fmt"
	"html"
	"io"
	"log"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
	"time"
)

type Entry struct {
	Name               string `json:"name"`
	Title              string `json:"title,omitempty"`
	Signature          string `json:"signature,omitempty"`
	Summary            string `json:"summary,omitempty"`
	ManualURL          string `json:"manual_url,omitempty"`
	ScriptReferenceURL string `json:"scriptreference_url"`
	UnityVersion       string `json:"unity_version,omitempty"`
}

var (
	reH1 = regexp.MustCompile(
		`<h1\s+class="heading\s+inherit">([\s\S]*?)</h1>`)
	reSig = regexp.MustCompile(
		`<div\s+class="signature-CS\s+sig-block">([\s\S]*?)</div>`)
	reSummary = regexp.MustCompile(
		`<h3>\s*Description\s*</h3>\s*<p>([\s\S]*?)</p>`)
	reManualClassFirst = regexp.MustCompile(
		`<a[^>]*class\s*=\s*['"][^'"]*switch-link[^'"]*['"][^>]*href\s*=\s*['"]([^'"]+)['"]`)
	reManualHrefFirst = regexp.MustCompile(
		`<a[^>]*href\s*=\s*['"]([^'"]+)['"][^>]*class\s*=\s*['"][^'"]*switch-link`)
	reVersion = regexp.MustCompile(
		`Version:\s*<b>Unity\s+(\d+\.\d+)</b>`)
	reHTMLTag    = regexp.MustCompile(`<[^>]+>`)
	reWhitespace = regexp.MustCompile(`\s+`)
)

func main() {
	in := flag.String("in", "",
		"Path to the Documentation/en directory (containing ScriptReference/).")
	out := flag.String("out", "",
		"Output JSONL path (e.g. AgentConnector/Editor/Data/unity_docs_6.0.jsonl).")
	flag.Parse()

	if *in == "" || *out == "" {
		fmt.Fprintln(os.Stderr, "usage: build-unity-docs --in <docs_root> --out <jsonl_path>")
		os.Exit(2)
	}

	srDir := filepath.Join(*in, "ScriptReference")
	srInfo, err := os.Stat(srDir)
	if err != nil || !srInfo.IsDir() {
		log.Fatalf("ScriptReference not found at %s", srDir)
	}

	start := time.Now()
	entries, skipped, err := scanDir(srDir)
	if err != nil {
		log.Fatalf("scan failed: %v", err)
	}

	// Stable order so the committed file diffs cleanly between regenerations.
	sort.Slice(entries, func(i, j int) bool { return entries[i].Name < entries[j].Name })

	if err := writeJSONL(*out, entries); err != nil {
		log.Fatalf("write failed: %v", err)
	}

	info, _ := os.Stat(*out)
	fmt.Printf("wrote %d entries (%s) in %s; skipped %d non-script-reference files\n",
		len(entries), humanSize(info.Size()), time.Since(start).Round(time.Millisecond), skipped)
}

func scanDir(srDir string) ([]Entry, int, error) {
	var entries []Entry
	skipped := 0

	err := filepath.Walk(srDir, func(path string, info os.FileInfo, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if info.IsDir() {
			// Stay top-level. ScriptReference has a docdata/ subfolder with
			// generated JS — nothing parseable for us.
			if path != srDir {
				return filepath.SkipDir
			}
			return nil
		}
		if filepath.Ext(info.Name()) != ".html" {
			return nil
		}
		// Skip the auto-generated index and search pages.
		base := strings.TrimSuffix(info.Name(), ".html")
		if base == "index" || strings.HasPrefix(base, "30_") || strings.HasPrefix(base, "40_") {
			skipped++
			return nil
		}

		data, err := os.ReadFile(path)
		if err != nil {
			return fmt.Errorf("read %s: %w", path, err)
		}
		entry, ok := parse(base, string(data))
		if !ok {
			skipped++
			return nil
		}
		entries = append(entries, entry)
		return nil
	})
	if err != nil {
		return nil, 0, err
	}
	return entries, skipped, nil
}

func parse(name, body string) (Entry, bool) {
	e := Entry{
		Name:               name,
		ScriptReferenceURL: "ScriptReference/" + name + ".html",
	}

	if m := reH1.FindStringSubmatch(body); m != nil {
		e.Title = cleanText(m[1])
	}
	if m := reSig.FindStringSubmatch(body); m != nil {
		e.Signature = cleanText(m[1])
	}
	if m := reSummary.FindStringSubmatch(body); m != nil {
		e.Summary = cleanText(m[1])
	}
	if m := reManualClassFirst.FindStringSubmatch(body); m != nil {
		e.ManualURL = normalizeRelativeURL(m[1])
	} else if m := reManualHrefFirst.FindStringSubmatch(body); m != nil {
		e.ManualURL = normalizeRelativeURL(m[1])
	}
	if m := reVersion.FindStringSubmatch(body); m != nil {
		e.UnityVersion = m[1]
	}

	// A real ScriptReference page has at least a title; pages without one
	// (redirects, partials, deprecated stubs) get dropped to keep the data
	// file lean.
	if e.Title == "" {
		return e, false
	}
	return e, true
}

func cleanText(s string) string {
	if s == "" {
		return s
	}
	s = reHTMLTag.ReplaceAllString(s, " ")
	s = html.UnescapeString(s)
	s = reWhitespace.ReplaceAllString(s, " ")
	return strings.TrimSpace(s)
}

func normalizeRelativeURL(raw string) string {
	if raw == "" {
		return raw
	}
	s := strings.ReplaceAll(raw, "\\", "/")
	for strings.HasPrefix(s, "../") {
		s = strings.TrimPrefix(s, "../")
	}
	for strings.HasPrefix(s, "./") {
		s = strings.TrimPrefix(s, "./")
	}
	return s
}

func writeJSONL(path string, entries []Entry) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()

	// Two suffix cues trigger gzip wrapping: ".gz" anywhere in the path
	// (e.g. unity_docs_6.0.jsonl.gz.bytes) and an explicit ".bytes" final
	// extension layered on top so Unity imports the artefact as a TextAsset.
	wantGzip := strings.Contains(path, ".gz")

	var inner io.Writer = f
	var gz *gzip.Writer
	if wantGzip {
		gz = gzip.NewWriter(f)
		defer func() { _ = gz.Close() }()
		inner = gz
	}

	w := bufio.NewWriter(inner)
	enc := json.NewEncoder(w)
	enc.SetEscapeHTML(false)
	for _, e := range entries {
		if err := enc.Encode(&e); err != nil {
			return err
		}
	}
	return w.Flush()
}

func humanSize(b int64) string {
	switch {
	case b > 1<<20:
		return fmt.Sprintf("%.1f MiB", float64(b)/(1<<20))
	case b > 1<<10:
		return fmt.Sprintf("%.1f KiB", float64(b)/(1<<10))
	default:
		return fmt.Sprintf("%d B", b)
	}
}

// Reserved for an alternate stream-based parser that does not load the full
// HTML into memory; current pages are small (~10-25KB) so io.ReadAll is fine.
var _ = io.Discard
