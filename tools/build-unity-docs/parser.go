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
	} else if declared := pageVersion(body); declared != "" {
		e.UnityVersion = declared
	}

	if e.Title == "" {
		// Unity ships member pages (enum members, fields, operators, some events) with an empty
		// <h1> even though they document a real API. The file name is the path the lookup key
		// uses, so it becomes the title; a page with no description either is a search or
		// feedback stub and stays skipped.
		if e.Summary == "" {
			return e, false
		}
		e.Title = name
	}
	return e, true
}

// pageVersion returns the per-page "Unity X.Y" marker, or "" when the page carries none.
func pageVersion(body string) string {
	if m := reVersion.FindStringSubmatch(body); m != nil {
		return m[1]
	}
	return ""
}

// normalizePageVersion maps the per-page marker onto the bucket label the entries carry:
// "6.6" becomes "6000.6", while an already-bucketed "6000.0" or a pre-Unity-6 "2022.3" stays
// as it is.
func normalizePageVersion(pageVersion string) string {
	parts := strings.SplitN(pageVersion, ".", 2)
	if len(parts) == 2 && len(parts[0]) == 1 {
		return "6000." + parts[1]
	}
	return pageVersion
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
