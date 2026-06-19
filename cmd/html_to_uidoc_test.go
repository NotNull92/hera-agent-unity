package cmd

import (
	"os"
	"testing"
)

func TestConvertHTMLToUIDoc(t *testing.T) {
	resetIDCounter()
	html := `<!DOCTYPE html>
<html>
<body style="width:1080px; height:1920px; background-color:#1A1A2E;">
  <div style="position:absolute; left:40px; top:200px; width:1000px; height:320px; background-color:#0F3460; border-radius:24px;"></div>
  <div style="position:absolute; left:340px; top:1640px; width:400px; height:120px; background-color:#E94560; border-radius:60px;">Play</div>
</body>
</html>`

	doc, err := convertHTMLToUIDoc(html, 1080, 1920)
	if err != nil {
		t.Fatalf("convert: %v", err)
	}

	if doc.Schema != "ui_doc/2" {
		t.Errorf("schema = %q, want ui_doc/2", doc.Schema)
	}
	if doc.Backend != "ugui" {
		t.Errorf("backend = %q, want ugui", doc.Backend)
	}
	if doc.Canvas == nil {
		t.Fatal("canvas config missing")
	}
	if doc.Canvas.ScaleMode != "scale_with_screen_size" {
		t.Errorf("scale_mode = %q", doc.Canvas.ScaleMode)
	}
	if len(doc.Canvas.ReferenceResolution) != 2 || doc.Canvas.ReferenceResolution[0] != 1080 || doc.Canvas.ReferenceResolution[1] != 1920 {
		t.Errorf("reference_resolution = %v", doc.Canvas.ReferenceResolution)
	}

	root, ok := doc.Root["element"].(string)
	if !ok || root != "canvas" {
		t.Errorf("root element = %v, want canvas", doc.Root["element"])
	}

	children, ok := doc.Root["children"].([]interface{})
	if !ok || len(children) != 2 {
		t.Fatalf("children count = %d, want 2", len(children))
	}

	first := children[0].(map[string]interface{})
	rect := first["rect"].(map[string]interface{})
	pos := rect["pos"].([]float64)
	if pos[0] != 40 || pos[1] != -200 {
		t.Errorf("first pos = %v, want [40 -200]", pos)
	}
	size := rect["size"].([]float64)
	if size[0] != 1000 || size[1] != 320 {
		t.Errorf("first size = %v, want [1000 320]", size)
	}

	second := children[1].(map[string]interface{})
	rect2 := second["rect"].(map[string]interface{})
	pos2 := rect2["pos"].([]float64)
	if pos2[0] != 340 || pos2[1] != -1640 {
		t.Errorf("second pos = %v, want [340 -1640]", pos2)
	}
}

func TestConvertHTMLToUIDocLandscape(t *testing.T) {
	resetIDCounter()
	html := `<body style="width:1920px; height:1080px;">
  <div style="position:absolute; left:100px; top:50px; width:400px; height:200px; background-color:red;"></div>
</body>`

	doc, err := convertHTMLToUIDoc(html, 1920, 1080)
	if err != nil {
		t.Fatalf("convert: %v", err)
	}
	if doc.Canvas.ReferenceResolution[0] != 1920 || doc.Canvas.ReferenceResolution[1] != 1080 {
		t.Errorf("reference_resolution = %v", doc.Canvas.ReferenceResolution)
	}
}

func TestParseColor(t *testing.T) {
	cases := []struct{ in, want string }{
		{"#fff", "#FFFFFFFF"},
		{"#FF0000", "#FF0000FF"},
		{"#FF000080", "#FF000080"},
		{"rgb(255, 128, 0)", "#FF8000FF"},
		{"red", "#FF0000FF"},
		{"", ""},
	}
	for _, c := range cases {
		got := parseColor(c.in)
		if got != c.want {
			t.Errorf("parseColor(%q) = %q, want %q", c.in, got, c.want)
		}
	}
}

func TestParsePX(t *testing.T) {
	cases := []struct {
		in   string
		want float64
	}{
		{"40px", 40},
		{"100", 100},
		{"12.5px", 12.5},
	}
	for _, c := range cases {
		got := parsePX(c.in)
		if got == nil || *got != c.want {
			var gotv float64
			if got != nil {
				gotv = *got
			}
			t.Errorf("parsePX(%q) = %v, want %v", c.in, gotv, c.want)
		}
	}
}

func TestHTMLToUIDocCmdStdout(t *testing.T) {
	resetIDCounter()
	// Write a temp HTML file.
	tmpDir := t.TempDir()
	htmlFile := tmpDir + "/sample.html"
	if err := os.WriteFile(htmlFile, []byte(`<body style="width:1080px; height:1920px;"><div style="position:absolute; left:10px; top:20px; width:100px; height:50px; background-color:#123456;"></div></body>`), 0644); err != nil {
		t.Fatal(err)
	}

	err := htmlToUIDocCmd([]string{"--file", htmlFile, "--width", "1080", "--height", "1920"})
	if err != nil {
		t.Fatalf("cmd: %v", err)
	}
	// Stdout output is hard to capture here; convertHTMLToUIDoc covers the logic.
}
