package cmd

import (
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"regexp"
	"strconv"
	"strings"

	"golang.org/x/net/html"
)

var (
	htuWidth  float64
	htuHeight float64
)

type uidocDoc struct {
	Schema  string                 `json:"schema"`
	Backend string                 `json:"backend"`
	Canvas  *uidocCanvasConfig     `json:"canvas,omitempty"`
	Root    map[string]interface{} `json:"root"`
}

type uidocCanvasConfig struct {
	ScaleMode              string    `json:"scale_mode"`
	ReferenceResolution    []float64 `json:"reference_resolution"`
	Match                  float64   `json:"match,omitempty"`
	ScaleFactor            float64   `json:"scale_factor,omitempty"`
	ReferencePixelsPerUnit float64   `json:"reference_pixels_per_unit,omitempty"`
}

func htmlToUIDocCmd(args []string) error {
	fs := flag.NewFlagSet("html-to-uidoc", flag.ContinueOnError)
	var file, out string
	fs.StringVar(&file, "file", "", "Input HTML file")
	fs.StringVar(&out, "out", "", "Output JSON file (default stdout)")
	fs.Float64Var(&htuWidth, "width", 1080, "Design canvas width in pixels")
	fs.Float64Var(&htuHeight, "height", 1920, "Design canvas height in pixels")
	if err := fs.Parse(args); err != nil {
		return err
	}

	if file == "" {
		return fmt.Errorf("--file required")
	}

	input, err := os.ReadFile(file)
	if err != nil {
		return fmt.Errorf("read html: %w", err)
	}

	doc, err := convertHTMLToUIDoc(string(input), htuWidth, htuHeight)
	if err != nil {
		return err
	}

	output, err := json.MarshalIndent(doc, "", "  ")
	if err != nil {
		return fmt.Errorf("marshal json: %w", err)
	}

	if out != "" {
		if err := os.WriteFile(out, output, 0644); err != nil {
			return fmt.Errorf("write output: %w", err)
		}
		fmt.Printf("Wrote %s\n", out)
		return nil
	}
	fmt.Println(string(output))
	return nil
}

func convertHTMLToUIDoc(htmlText string, designW, designH float64) (*uidocDoc, error) {
	root, err := parseHTML(htmlText)
	if err != nil {
		return nil, err
	}
	if root == nil {
		return nil, fmt.Errorf("no convertible root element found")
	}

	rootNode := buildUINode(root, designW, designH)
	// Root becomes a standalone canvas at scene root.
	rootNode["element"] = "canvas"
	rootNode["name"] = "Canvas"
	rootNode["rect"] = map[string]interface{}{"anchor": "stretch", "size": []float64{0, 0}}
	delete(rootNode, "image")
	delete(rootNode, "text")

	return &uidocDoc{
		Schema:  "ui_doc/2",
		Backend: "ugui",
		Canvas: &uidocCanvasConfig{
			ScaleMode:           "scale_with_screen_size",
			ReferenceResolution: []float64{designW, designH},
			Match:               0.5,
		},
		Root: rootNode,
	}, nil
}

type htmlNode struct {
	tag      string
	styles   map[string]string
	children []*htmlNode
	text     string
}

func parseHTML(htmlText string) (*htmlNode, error) {
	doc, err := html.Parse(strings.NewReader(htmlText))
	if err != nil {
		return nil, fmt.Errorf("parse html: %w", err)
	}

	var allowedTags = map[string]bool{"body": true, "div": true, "span": true, "button": true, "img": true}

	var findRoot func(*html.Node) *htmlNode
	findRoot = func(n *html.Node) *htmlNode {
		if n.Type == html.ElementNode && allowedTags[n.Data] {
			node := &htmlNode{
				tag:      n.Data,
				styles:   inlineStyles(n),
				children: nil,
			}
			for c := n.FirstChild; c != nil; c = c.NextSibling {
				if child := findRoot(c); child != nil {
					node.children = append(node.children, child)
				} else if c.Type == html.TextNode {
					t := strings.TrimSpace(c.Data)
					if t != "" {
						node.text = t
					}
				}
			}
			return node
		}
		for c := n.FirstChild; c != nil; c = c.NextSibling {
			if r := findRoot(c); r != nil {
				return r
			}
		}
		return nil
	}

	return findRoot(doc), nil
}

func inlineStyles(n *html.Node) map[string]string {
	styles := make(map[string]string)
	for _, attr := range n.Attr {
		if attr.Key == "style" {
			for _, decl := range strings.Split(attr.Val, ";") {
				if strings.Contains(decl, ":") {
					parts := strings.SplitN(decl, ":", 2)
					styles[strings.TrimSpace(strings.ToLower(parts[0]))] = strings.TrimSpace(parts[1])
				}
			}
		}
	}
	return styles
}

func buildUINode(n *htmlNode, designW, designH float64) map[string]interface{} {
	styles := n.styles
	w := parsePX(styles["width"])
	h := parsePX(styles["height"])
	left := parsePX(styles["left"])
	top := parsePX(styles["top"])
	bg := parseColor(styles["background-color"])
	radius := parsePX(styles["border-radius"])

	element := "panel"
	switch n.tag {
	case "button":
		element = "button"
	case "img":
		element = "image"
	case "span":
		element = "text"
	}

	node := map[string]interface{}{
		"name":    fmt.Sprintf("%s_%d", capitalize(element), idCounter()),
		"element": element,
	}

	rect := map[string]interface{}{
		"anchor": "top-left",
		"pivot":  []float64{0, 1},
		"pos":    []float64{coalesce(left, 0), 0},
	}
	if top != nil {
		// uGUI anchoredPosition.y is positive upward; HTML top is downward positive.
		rect["pos"] = []float64{coalesce(left, 0), -(*top)}
	}
	if w != nil && h != nil {
		rect["size"] = []float64{*w, *h}
	}
	node["rect"] = rect

	if bg != "" && w != nil && h != nil {
		gen := map[string]interface{}{
			"kind":  "solid",
			"size":  []float64{*w, *h},
			"color": bg,
		}
		if radius != nil {
			gen["kind"] = "rounded_rect"
			gen["radius"] = *radius
		}
		node["image"] = map[string]interface{}{"sprite": map[string]interface{}{"gen": gen}}
	}

	if n.text != "" {
		node["text"] = map[string]interface{}{
			"value":  n.text,
			"engine": "auto",
			"align":  "center",
			"color":  "#FFFFFFFF",
		}
	}

	if len(n.children) > 0 {
		children := make([]interface{}, 0, len(n.children))
		for _, c := range n.children {
			children = append(children, buildUINode(c, designW, designH))
		}
		node["children"] = children
	}

	return node
}

func coalesce(a *float64, fallback float64) float64 {
	if a == nil {
		return fallback
	}
	return *a
}

var pxRe = regexp.MustCompile(`^(-?\d+(?:\.\d+)?)(?:px)?$`)

func parsePX(s string) *float64 {
	if s == "" {
		return nil
	}
	m := pxRe.FindStringSubmatch(strings.ToLower(strings.TrimSpace(s)))
	if m == nil {
		return nil
	}
	v, err := strconv.ParseFloat(m[1], 64)
	if err != nil {
		return nil
	}
	return &v
}

var hexRe = regexp.MustCompile(`^#([0-9a-fA-F]{3,8})$`)

func parseColor(s string) string {
	s = strings.TrimSpace(strings.ToLower(s))
	if s == "" {
		return ""
	}

	if m := hexRe.FindStringSubmatch(s); m != nil {
		hex := m[1]
		if len(hex) == 3 {
			hex = fmt.Sprintf("%c%c%c%c%c%c", hex[0], hex[0], hex[1], hex[1], hex[2], hex[2])
		}
		if len(hex) == 6 {
			return "#" + strings.ToUpper(hex) + "FF"
		}
		if len(hex) == 8 {
			return "#" + strings.ToUpper(hex)
		}
		return ""
	}

	if strings.HasPrefix(s, "rgb") {
		nums := regexp.MustCompile(`\d+`).FindAllString(s, 3)
		if len(nums) >= 3 {
			r, _ := strconv.Atoi(nums[0])
			g, _ := strconv.Atoi(nums[1])
			b, _ := strconv.Atoi(nums[2])
			return fmt.Sprintf("#%02X%02X%02XFF", r, g, b)
		}
	}

	named := map[string]string{
		"white":       "#FFFFFFFF",
		"black":       "#000000FF",
		"red":         "#FF0000FF",
		"green":       "#008000FF",
		"blue":        "#0000FFFF",
		"yellow":      "#FFFF00FF",
		"cyan":        "#00FFFFFF",
		"magenta":     "#FF00FFFF",
		"gray":        "#808080FF",
		"lightgray":   "#D3D3D3FF",
		"darkgray":    "#A9A9A9FF",
		"transparent": "#00000000",
	}
	return named[s]
}

var counter int

func idCounter() int {
	counter++
	return counter
}

func capitalize(s string) string {
	if s == "" {
		return s
	}
	return strings.ToUpper(s[:1]) + s[1:]
}

// Reset counter for deterministic tests.
func resetIDCounter() {
	counter = 0
}
