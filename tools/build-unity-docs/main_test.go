package main

import "testing"

func TestParseUsesExplicitUnityVersionWhenProvided(t *testing.T) {
	body := `<h1 class="heading inherit">Rigidbody</h1>` +
		`<div class="signature-CS sig-block">public class Rigidbody;</div>` +
		`<h3>Description</h3><p>Control physics.</p>` +
		`Version: <b>Unity 6.0</b>`

	entry, ok := parse("Rigidbody", body, "2022.3")
	if !ok {
		t.Fatal("parse returned false")
	}
	if entry.UnityVersion != "2022.3" {
		t.Fatalf("expected explicit version 2022.3, got %q", entry.UnityVersion)
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
		UnityVersion: "2022.3",
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
