package main

import (
	"html"
	"regexp"
	"strings"
)

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
	reMemberRow = regexp.MustCompile(
		`<tr><td\s+class=["']lbl["']><a\s+href=["']([^"']+\.html)["']>([\s\S]*?)</a></td><td\s+class=["']desc["']>([\s\S]*?)</td></tr>`)
	reHTMLTag    = regexp.MustCompile(`<[^>]+>`)
	reWhitespace = regexp.MustCompile(`\s+`)
)

func parse(name, body string, unityVersion string) (Entry, bool) {
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
	if unityVersion != "" {
		e.UnityVersion = unityVersion
	} else if m := reVersion.FindStringSubmatch(body); m != nil {
		e.UnityVersion = m[1]
	}

	if e.Title == "" {
		return e, false
	}
	return e, true
}

func parseLinkedMemberEntries(parent Entry, body string, unityVersion string) []Entry {
	matches := reMemberRow.FindAllStringSubmatch(body, -1)
	if len(matches) == 0 {
		return nil
	}

	entries := make([]Entry, 0, len(matches))
	seen := make(map[string]struct{})
	for _, m := range matches {
		name, ok := scriptReferenceName(m[1])
		if !ok || name == parent.Name {
			continue
		}
		if _, exists := seen[name]; exists {
			continue
		}
		seen[name] = struct{}{}

		version := unityVersion
		if version == "" {
			version = parent.UnityVersion
		}
		entries = append(entries, Entry{
			Name:               name,
			Title:              linkedMemberTitle(name, m[2]),
			Summary:            cleanText(m[3]),
			ManualURL:          parent.ManualURL,
			ScriptReferenceURL: "ScriptReference/" + name + ".html",
			UnityVersion:       version,
		})
	}
	return entries
}

func scriptReferenceName(raw string) (string, bool) {
	if raw == "" {
		return "", false
	}
	s := strings.ReplaceAll(raw, "\\", "/")
	if strings.Contains(s, "/") || strings.HasPrefix(s, "#") {
		return "", false
	}
	if !strings.HasSuffix(s, ".html") {
		return "", false
	}
	name := strings.TrimSuffix(s, ".html")
	if name == "" || name == "index" || strings.HasPrefix(name, "30_") || strings.HasPrefix(name, "40_") {
		return "", false
	}
	return name, true
}

func linkedMemberTitle(name string, label string) string {
	if strings.Contains(name, ".") {
		return name
	}
	cleanLabel := cleanText(label)
	if idx := strings.Index(name, "-"); idx > 0 && cleanLabel != "" {
		return name[:idx] + "." + cleanLabel
	}
	if cleanLabel != "" {
		return cleanLabel
	}
	return name
}

func cleanText(s string) string {
	if s == "" {
		return s
	}
	s = reHTMLTag.ReplaceAllString(s, " ")
	s = html.UnescapeString(s)
	s = reWhitespace.ReplaceAllString(s, " ")
	s = strings.NewReplacer(" . ", ".", " .", ".").Replace(s)
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
