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
