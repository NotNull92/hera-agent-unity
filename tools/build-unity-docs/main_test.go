package main

import "testing"

func TestParseUsesExplicitUnityVersionWhenProvided(t *testing.T) {
	body := `<h1 class="heading inherit">Rigidbody</h1>` +
		`<div class="signature-CS sig-block">public class Rigidbody;</div>` +
		`<h3>Description</h3><p>Control physics.</p>` +
		`Version: <b>Unity 6.0</b>`

	entry, ok := parse("Rigidbody", body, "6000.0")
	if !ok {
		t.Fatal("parse returned false")
	}
	if entry.UnityVersion != "6000.0" {
		t.Fatalf("expected explicit version 6000.0, got %q", entry.UnityVersion)
	}
}

func TestParseFallsBackToPageVersion(t *testing.T) {
	body := `<h1 class="heading inherit">Rigidbody</h1>` +
		`Version: <b>Unity 6000.5</b>`

	entry, ok := parse("Rigidbody", body, "")
	if !ok {
		t.Fatal("parse returned false")
	}
	if entry.UnityVersion != "6000.5" {
		t.Fatalf("expected page version 6000.5, got %q", entry.UnityVersion)
	}
}

func TestParseDerivesTitleFromNameWhenHeadingIsEmpty(t *testing.T) {
	// Unity ships enum-member, field and operator pages with an empty <h1>; they still document a
	// real API and their file name is the lookup key, so they must not be dropped.
	body := `<h1 class="heading inherit">` + "\n" + `</h1>` +
		`<h3>Description</h3><p>Selects driven property combining PositionX, PositionY and PositionZ.</p>` +
		`Version: <b>Unity 6.6</b> (6000.6)`

	entry, ok := parse("DrivenTransformProperties.Position", body, "6000.6")
	if !ok {
		t.Fatal("parse dropped a documented page with an empty heading")
	}
	if entry.Title != "DrivenTransformProperties.Position" {
		t.Fatalf("expected the name as title, got %q", entry.Title)
	}
	if entry.Summary == "" {
		t.Fatal("expected the description to survive")
	}
}

func TestParseStillSkipsPagesWithoutTitleAndDescription(t *testing.T) {
	body := `<h1 class="heading inherit"></h1>` + `Version: <b>Unity 6.6</b> (6000.6)`

	if _, ok := parse("LeaveFeedback", body, "6000.6"); ok {
		t.Fatal("expected a page with neither title nor description to stay skipped")
	}
}

func TestNormalizePageVersion(t *testing.T) {
	cases := map[string]string{
		"6.6":    "6000.6",
		"6.0":    "6000.0",
		"6000.0": "6000.0",
		"2022.3": "2022.3",
		"":       "",
	}
	for input, want := range cases {
		if got := normalizePageVersion(input); got != want {
			t.Fatalf("normalizePageVersion(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestPageVersionReadsTheMarker(t *testing.T) {
	body := `<h1 class="heading inherit">Rigidbody</h1>Version: <b>Unity 6.6</b> (6000.6)`
	if got := pageVersion(body); got != "6.6" {
		t.Fatalf("pageVersion = %q, want 6.6", got)
	}
	if got := pageVersion(`<h1 class="heading inherit">Rigidbody</h1>`); got != "" {
		t.Fatalf("pageVersion on a page without a marker = %q, want empty", got)
	}
}

func TestCleanTextNormalizesMemberDotSpacing(t *testing.T) {
	text := cleanText(`GameObject <span>.</span> AddComponent`)
	if text != "GameObject.AddComponent" {
		t.Fatalf("expected compact member title, got %q", text)
	}
}

func TestParseLinkedMemberEntries(t *testing.T) {
	parent := Entry{
		Name:         "Rigidbody",
		Title:        "Rigidbody",
		ManualURL:    "Manual/class-Rigidbody.html",
		UnityVersion: "6000.0",
	}
	body := `<tr><td class="lbl"><a href="Rigidbody-mass.html">mass</a></td><td class="desc">The mass of the rigidbody.</td></tr>` +
		`<tr><td class="lbl"><a href="Rigidbody.AddForce.html">AddForce</a></td><td class="desc">Adds a force to the Rigidbody.</td></tr>` +
		`<tr><td class="lbl"><a href="../Manual/class-Rigidbody.html">Manual</a></td><td class="desc">Not a ScriptReference member.</td></tr>`

	entries := parseLinkedMemberEntries(parent, body, "")
	if len(entries) != 2 {
		t.Fatalf("expected 2 linked entries, got %d", len(entries))
	}
	if entries[0].Name != "Rigidbody-mass" {
		t.Fatalf("expected Rigidbody-mass, got %q", entries[0].Name)
	}
	if entries[0].Title != "Rigidbody.mass" {
		t.Fatalf("expected Rigidbody.mass title, got %q", entries[0].Title)
	}
	if entries[0].Summary != "The mass of the rigidbody." {
		t.Fatalf("expected mass summary, got %q", entries[0].Summary)
	}
	if entries[0].ManualURL != parent.ManualURL {
		t.Fatalf("expected inherited manual URL, got %q", entries[0].ManualURL)
	}
	if entries[0].UnityVersion != parent.UnityVersion {
		t.Fatalf("expected inherited Unity version, got %q", entries[0].UnityVersion)
	}
	if entries[1].Name != "Rigidbody.AddForce" {
		t.Fatalf("expected Rigidbody.AddForce, got %q", entries[1].Name)
	}
	if entries[1].Title != "Rigidbody.AddForce" {
		t.Fatalf("expected Rigidbody.AddForce title, got %q", entries[1].Title)
	}
}
