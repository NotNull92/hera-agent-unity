package cmd

import (
	"encoding/json"
	"fmt"
	"image"
	"image/color"
	"image/gif"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"

	// Register decoders so image.Decode handles the common reference formats.
	// (gif is imported by name above — DecodeAll counts animation frames; its
	// init still registers the decoder for image.Decode.)
	_ "image/jpeg"
	_ "image/png"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// uiDocCmd handles the ui_doc tool. export / gen_sprite / capture / import are
// simple passthroughs; apply and import read their document from --file so the
// (potentially large) doc never rides inline in the agent's context — it is
// parsed here and injected as the `doc` param. sample and catalog are handled
// entirely CLI-side: sample reads measured colors off a reference image, and
// catalog scans a UI-asset folder into a manifest — both need no Unity state
// (the scene-touching actions, including the project import, go to the connector).
func uiDocCmd(args []string, send SendFunc) (*client.CommandResponse, error) {
	if len(args) > 0 && args[0] == "sample" {
		return uiDocSample(args[1:])
	}
	if len(args) > 0 && args[0] == "catalog" {
		return uiDocCatalog(args[1:])
	}

	args, doc, err := extractDocFile(args)
	if err != nil {
		return nil, err
	}

	params, _, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}
	if doc != nil {
		params["doc"] = doc
	}

	return send("ui_doc", params)
}

// extractDocFile strips `--file <path>` and returns the parsed JSON document.
// Returns a nil doc when --file is absent (export / gen_sprite / inline --doc).
func extractDocFile(args []string) ([]string, interface{}, error) {
	var out []string
	var filePath string
	for i := 0; i < len(args); i++ {
		if args[i] == "--file" {
			if i+1 >= len(args) {
				return nil, nil, fmt.Errorf("--file requires a path argument")
			}
			filePath = args[i+1]
			i++
			continue
		}
		out = append(out, args[i])
	}

	if filePath == "" {
		return out, nil, nil
	}

	data, err := os.ReadFile(filePath)
	if err != nil {
		return nil, nil, fmt.Errorf("read --file %s: %w", filePath, err)
	}
	var doc interface{}
	if err := json.Unmarshal(data, &doc); err != nil {
		return nil, nil, fmt.Errorf("parse --file %s as JSON: %w", filePath, err)
	}
	return out, doc, nil
}

// ---- ui_doc catalog: scan a UI-asset folder into a manifest -----------------

// catalogColor is one dominant color with its approximate share of the image.
type catalogColor struct {
	Hex string `json:"hex"`
	Pct int    `json:"pct"`
}

// catalogEntry is one image's metadata plus cheap heuristic hints. The
// vision-capable agent reads the actual pixels (via path) for the final "what UI
// is this" call; these signals only ground that read. Defaults are omitted to
// stay token-lean.
type catalogEntry struct {
	Path          string         `json:"path"`
	Format        string         `json:"format"`
	Decoded       bool           `json:"decoded"`
	W             int            `json:"w,omitempty"`
	H             int            `json:"h,omitempty"`
	Aspect        float64        `json:"aspect,omitempty"`
	HasAlpha      bool           `json:"has_alpha,omitempty"`
	OpaqueBounds  []int          `json:"opaque_bounds,omitempty"`   // [x,y,w,h] of non-transparent content (top-left origin)
	Palette       []catalogColor `json:"palette,omitempty"`         // dominant colors, most-common first
	NineSlice     []int          `json:"nine_slice_hint,omitempty"` // [left,bottom,right,top] border, ready for import --border
	NameHint      string         `json:"name_hint,omitempty"`       // element guess from the filename
	Animated      bool           `json:"animated,omitempty"`
	Frames        int            `json:"frames,omitempty"`
	ReferenceOnly bool           `json:"reference_only,omitempty"` // GIFs: catalogued for analysis, not importable as a Sprite
	Note          string         `json:"note,omitempty"`
}

type catalogResult struct {
	Dir       string         `json:"dir"`
	Count     int            `json:"count"`
	Truncated bool           `json:"truncated,omitempty"`
	Images    []catalogEntry `json:"images"`
}

// uiDocCatalog scans a folder of UI assets (recursively) and returns a manifest
// the agent uses to decide what each sprite is. CLI-side, like sample: it decodes
// images off disk with no Unity round-trip. Classification stays with the
// vision-capable agent (which reads the listed paths); the metadata + filename
// hint + conservative 9-slice border are only there to ground that read. GIFs
// are catalogued as reference-only — Unity doesn't import them as Sprites.
func uiDocCatalog(args []string) (*client.CommandResponse, error) {
	var dir string
	maxImages := 300
	for i := 0; i < len(args); i++ {
		switch args[i] {
		case "--dir", "--path":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("%s requires a folder path", args[i])
			}
			dir = args[i+1]
			i++
		case "--max":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("--max requires a number")
			}
			n, err := strconv.Atoi(args[i+1])
			if err != nil || n <= 0 {
				return nil, fmt.Errorf("--max must be a positive integer, got %q", args[i+1])
			}
			maxImages = n
			i++
		}
	}
	if dir == "" {
		return nil, fmt.Errorf("ui_doc catalog needs --dir <folder> (absolute path to your UI assets)")
	}
	abs, err := filepath.Abs(dir)
	if err != nil {
		return nil, fmt.Errorf("resolve --dir %s: %w", dir, err)
	}
	info, err := os.Stat(abs)
	if err != nil {
		return nil, fmt.Errorf("stat --dir %s: %w", abs, err)
	}
	if !info.IsDir() {
		return nil, fmt.Errorf("--dir %s is not a folder", abs)
	}

	var files []string
	_ = filepath.WalkDir(abs, func(path string, d os.DirEntry, werr error) error {
		if werr != nil || d.IsDir() {
			return nil // skip unreadable entries, keep scanning
		}
		if catalogKind(path) != "" {
			files = append(files, path)
		}
		return nil
	})
	sort.Strings(files)

	truncated := false
	if len(files) > maxImages {
		files = files[:maxImages]
		truncated = true
	}

	res := catalogResult{Dir: abs, Images: make([]catalogEntry, 0, len(files))}
	for _, f := range files {
		res.Images = append(res.Images, catalogImage(f))
	}
	res.Count = len(res.Images)
	res.Truncated = truncated

	data, err := json.Marshal(res)
	if err != nil {
		return nil, fmt.Errorf("marshal catalog: %w", err)
	}
	msg := fmt.Sprintf("Cataloged %d image(s) under %s", res.Count, abs)
	if truncated {
		msg += fmt.Sprintf(" (truncated to --max %d)", maxImages)
	}
	return &client.CommandResponse{
		Success: true,
		Message: msg,
		Data:    json.RawMessage(data),
	}, nil
}

// catalogKind classifies a file by extension: "raster" (Go-decodable still
// image), "gif" (decodable but reference-only), "other" (a Unity-importable
// image Go can't decode), or "" (not an image — skipped).
func catalogKind(path string) string {
	switch strings.ToLower(filepath.Ext(path)) {
	case ".png", ".jpg", ".jpeg":
		return "raster"
	case ".gif":
		return "gif"
	case ".tga", ".psd", ".exr", ".bmp", ".webp", ".tif", ".tiff":
		return "other"
	}
	return ""
}

func catalogImage(path string) catalogEntry {
	e := catalogEntry{
		Path:     path,
		Format:   strings.TrimPrefix(strings.ToLower(filepath.Ext(path)), "."),
		NameHint: nameHint(path),
	}

	switch catalogKind(path) {
	case "gif":
		e.ReferenceOnly = true
		f, err := os.Open(path)
		if err != nil {
			e.Note = "could not open"
			return e
		}
		defer f.Close()
		g, derr := gif.DecodeAll(f)
		if derr != nil {
			e.Note = "could not decode gif"
			return e
		}
		e.Decoded = true
		e.Frames = len(g.Image)
		e.Animated = len(g.Image) > 1
		if g.Config.Width > 0 {
			e.W, e.H = g.Config.Width, g.Config.Height
			e.Aspect = ratio(g.Config.Width, g.Config.Height)
		}
		e.Note = "gif is reference-only (not imported as a Sprite)"
		return e
	case "other":
		// Go can't decode these, but Unity can import them — list existence so
		// the agent knows they're available to import by path.
		e.Note = "not decoded here (Unity-only format); importable by path"
		return e
	}

	f, err := os.Open(path)
	if err != nil {
		e.Note = "could not open"
		return e
	}
	defer f.Close()
	img, _, derr := image.Decode(f)
	if derr != nil {
		e.Note = "could not decode"
		return e
	}
	e.Decoded = true
	b := img.Bounds()
	w, h := b.Dx(), b.Dy()
	e.W, e.H = w, h
	e.Aspect = ratio(w, h)

	step := w / 256
	if h/256 > step {
		step = h / 256
	}
	if step < 1 {
		step = 1
	}

	e.HasAlpha, e.OpaqueBounds = scanAlpha(img, step)
	e.Palette = dominantColors(img, step)
	if ns := suggestNineSlice(img); ns != nil {
		e.NineSlice = ns
	}
	return e
}

func ratio(w, h int) float64 {
	if h == 0 {
		return 0
	}
	return math.Round(float64(w)/float64(h)*1000) / 1000
}

// nameHint guesses the UI element from the filename — a weak prior the agent
// confirms by sight. Returns "" when nothing matches.
func nameHint(path string) string {
	n := strings.ToLower(filepath.Base(path))
	switch {
	case containsAny(n, "btn", "button"):
		return "button"
	case containsAny(n, "panel", "window", "dialog", "popup", "frame", "card"):
		return "panel"
	case containsAny(n, "bar", "progress", "health", "gauge", "slider", "fill"):
		return "bar"
	case containsAny(n, "icon", "ico"):
		return "image"
	case containsAny(n, "bg", "background"):
		return "image"
	case containsAny(n, "text", "label", "title"):
		return "text"
	}
	return ""
}

func containsAny(s string, subs ...string) bool {
	for _, sub := range subs {
		if strings.Contains(s, sub) {
			return true
		}
	}
	return false
}

// scanAlpha samples the image (stride `step`) for transparency and the bounding
// box of opaque content. Returns has-alpha and an [x,y,w,h] trim box (nil when
// the content already fills the image). Sampled, so it's a hint, not exact.
func scanAlpha(img image.Image, step int) (bool, []int) {
	b := img.Bounds()
	hasAlpha := false
	minX, minY := b.Max.X, b.Max.Y
	maxX, maxY := b.Min.X-1, b.Min.Y-1
	for y := b.Min.Y; y < b.Max.Y; y += step {
		for x := b.Min.X; x < b.Max.X; x += step {
			c := color.NRGBAModel.Convert(img.At(x, y)).(color.NRGBA)
			if c.A < 250 {
				hasAlpha = true
			}
			if c.A >= 8 {
				if x < minX {
					minX = x
				}
				if y < minY {
					minY = y
				}
				if x > maxX {
					maxX = x
				}
				if y > maxY {
					maxY = y
				}
			}
		}
	}
	if !hasAlpha || maxX < minX {
		return hasAlpha, nil
	}
	x0 := minX - b.Min.X
	y0 := minY - b.Min.Y
	bw := maxX - minX + 1
	bh := maxY - minY + 1
	// Omit when the opaque content basically spans the whole image.
	if x0 <= step && y0 <= step && bw >= b.Dx()-2*step && bh >= b.Dy()-2*step {
		return hasAlpha, nil
	}
	return hasAlpha, []int{x0, y0, bw, bh}
}

// dominantColors quantizes sampled pixels into coarse buckets and returns the
// top few as hex + percent share (near-transparent pixels excluded).
func dominantColors(img image.Image, step int) []catalogColor {
	b := img.Bounds()
	counts := map[uint32]int{}
	total := 0
	for y := b.Min.Y; y < b.Max.Y; y += step {
		for x := b.Min.X; x < b.Max.X; x += step {
			c := color.NRGBAModel.Convert(img.At(x, y)).(color.NRGBA)
			if c.A < 32 {
				continue
			}
			key := uint32(c.R/32*32)<<16 | uint32(c.G/32*32)<<8 | uint32(c.B/32*32)
			counts[key]++
			total++
		}
	}
	if total == 0 {
		return nil
	}
	type bucket struct {
		key uint32
		n   int
	}
	arr := make([]bucket, 0, len(counts))
	for k, n := range counts {
		arr = append(arr, bucket{k, n})
	}
	sort.Slice(arr, func(i, j int) bool {
		if arr[i].n != arr[j].n {
			return arr[i].n > arr[j].n
		}
		return arr[i].key < arr[j].key
	})
	var out []catalogColor
	for i := 0; i < len(arr) && len(out) < 4; i++ {
		pct := arr[i].n * 100 / total
		if pct == 0 {
			continue
		}
		out = append(out, catalogColor{
			Hex: fmt.Sprintf("#%02X%02X%02X", (arr[i].key>>16)&0xFF, (arr[i].key>>8)&0xFF, arr[i].key&0xFF),
			Pct: pct,
		})
	}
	return out
}

// suggestNineSlice proposes a [left,bottom,right,top] sprite border (Unity's
// spriteBorder order) when the image looks 9-sliceable — a detailed frame around
// a uniform, stretchable center. Conservative: nil unless it finds clear borders
// that still leave a real center. A hint only; the agent confirms by sight and
// can override at import with --border.
func suggestNineSlice(img image.Image) []int {
	b := img.Bounds()
	w, h := b.Dx(), b.Dy()
	if w < 12 || h < 12 {
		return nil
	}
	left, right := uniformRunInsets(img, true)
	top, bottom := uniformRunInsets(img, false)
	// Need a border somewhere and a real stretchable center on both axes.
	if left+right == 0 && top+bottom == 0 {
		return nil
	}
	if left+right >= w*9/10 || top+bottom >= h*9/10 {
		return nil
	}
	// Unity order: (left, bottom, right, top). Image y grows downward, so the
	// "lo" inset on the vertical axis is the sprite's top, "hi" is the bottom.
	return []int{left, bottom, right, top}
}

// uniformRunInsets finds the maximal run of near-equal adjacent lines (columns
// when horizontal, rows when vertical) that includes the center, and returns the
// insets from each edge to that run — i.e. the 9-slice border thickness on that
// axis. Compares lines over the central cross-band so corner art is ignored.
func uniformRunInsets(img image.Image, horizontal bool) (int, int) {
	b := img.Bounds()
	w, h := b.Dx(), b.Dy()
	n, cross := w, h
	if !horizontal {
		n, cross = h, w
	}
	cl, ch := cross/4, cross-cross/4
	cstep := (ch - cl) / 32
	if cstep < 1 {
		cstep = 1
	}
	at := func(i, j int) color.NRGBA {
		x, y := i, j
		if !horizontal {
			x, y = j, i
		}
		return color.NRGBAModel.Convert(img.At(b.Min.X+x, b.Min.Y+y)).(color.NRGBA)
	}
	lineEqual := func(i1, i2 int) bool {
		for j := cl; j < ch; j += cstep {
			c1, c2 := at(i1, j), at(i2, j)
			if absDiff(c1.R, c2.R) > 10 || absDiff(c1.G, c2.G) > 10 || absDiff(c1.B, c2.B) > 10 || absDiff(c1.A, c2.A) > 10 {
				return false
			}
		}
		return true
	}
	center := n / 2
	lo := center
	for lo > 0 && lineEqual(lo-1, lo) {
		lo--
	}
	hi := center
	for hi < n-1 && lineEqual(hi, hi+1) {
		hi++
	}
	return lo, n - 1 - hi
}

func absDiff(a, b uint8) int {
	if a > b {
		return int(a - b)
	}
	return int(b - a)
}

// samplePoint is one measured color result.
type samplePoint struct {
	At   []float64 `json:"at,omitempty"`
	Px   []int     `json:"px"`
	Hex  string    `json:"hex"`
	RGBA []int     `json:"rgba"`
}

type sampleRegion struct {
	Region []float64 `json:"region"`
	Px     []int     `json:"px"`
	Hex    string    `json:"hex"`
	RGBA   []int     `json:"rgba"`
}

type sampleResult struct {
	Image   string         `json:"image"`
	Width   int            `json:"width"`
	Height  int            `json:"height"`
	Points  []samplePoint  `json:"points,omitempty"`
	Regions []sampleRegion `json:"regions,omitempty"`
}

// uiDocSample reads colors from a reference image so the agent measures hex
// values instead of guessing them. Coordinates are NORMALIZED [0,1] with a
// top-left origin (matching how the image reads on screen). Points are averaged
// over a small kernel to shrug off antialiasing/JPEG noise.
func uiDocSample(args []string) (*client.CommandResponse, error) {
	var imgPath string
	var atSpecs []string
	var regionSpecs []string
	kernel := 2

	for i := 0; i < len(args); i++ {
		switch args[i] {
		case "--image", "--ref":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("%s requires a path argument", args[i])
			}
			imgPath = args[i+1]
			i++
		case "--at":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("--at requires \"x,y\" (normalized, ';'-separated for many)")
			}
			atSpecs = append(atSpecs, splitList(args[i+1])...)
			i++
		case "--region":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("--region requires \"x,y,w,h\" (normalized, ';'-separated for many)")
			}
			regionSpecs = append(regionSpecs, splitList(args[i+1])...)
			i++
		case "--kernel":
			if i+1 >= len(args) {
				return nil, fmt.Errorf("--kernel requires a pixel radius")
			}
			k, err := strconv.Atoi(args[i+1])
			if err != nil || k < 0 {
				return nil, fmt.Errorf("--kernel must be a non-negative integer, got %q", args[i+1])
			}
			kernel = k
			i++
		}
	}

	if imgPath == "" {
		return nil, fmt.Errorf("ui_doc sample needs --image <reference.png>")
	}
	if len(atSpecs) == 0 && len(regionSpecs) == 0 {
		return nil, fmt.Errorf("ui_doc sample needs at least one --at \"x,y\" or --region \"x,y,w,h\"")
	}

	f, err := os.Open(imgPath)
	if err != nil {
		return nil, fmt.Errorf("open --image %s: %w", imgPath, err)
	}
	defer f.Close()

	img, _, err := image.Decode(f)
	if err != nil {
		return nil, fmt.Errorf("decode --image %s: %w", imgPath, err)
	}
	b := img.Bounds()
	w, h := b.Dx(), b.Dy()
	if w == 0 || h == 0 {
		return nil, fmt.Errorf("--image %s has zero size", imgPath)
	}

	res := sampleResult{Image: imgPath, Width: w, Height: h}

	for _, spec := range atSpecs {
		nx, ny, perr := parse2(spec)
		if perr != nil {
			return nil, fmt.Errorf("--at %q: %w", spec, perr)
		}
		px := b.Min.X + clampInt(int(math.Round(nx*float64(w-1))), 0, w-1)
		py := b.Min.Y + clampInt(int(math.Round(ny*float64(h-1))), 0, h-1)
		r, g, bl, a := avgRect(img, px-kernel, py-kernel, px+kernel, py+kernel)
		res.Points = append(res.Points, samplePoint{
			At:   []float64{nx, ny},
			Px:   []int{px - b.Min.X, py - b.Min.Y},
			Hex:  hex8(r, g, bl, a),
			RGBA: []int{r, g, bl, a},
		})
	}

	for _, spec := range regionSpecs {
		nx, ny, nw, nh, perr := parse4(spec)
		if perr != nil {
			return nil, fmt.Errorf("--region %q: %w", spec, perr)
		}
		x0 := clampInt(int(math.Round(nx*float64(w))), 0, w-1)
		y0 := clampInt(int(math.Round(ny*float64(h))), 0, h-1)
		x1 := clampInt(int(math.Round((nx+nw)*float64(w)))-1, x0, w-1)
		y1 := clampInt(int(math.Round((ny+nh)*float64(h)))-1, y0, h-1)
		r, g, bl, a := avgRect(img, b.Min.X+x0, b.Min.Y+y0, b.Min.X+x1, b.Min.Y+y1)
		res.Regions = append(res.Regions, sampleRegion{
			Region: []float64{nx, ny, nw, nh},
			Px:     []int{x0, y0, x1 - x0 + 1, y1 - y0 + 1},
			Hex:    hex8(r, g, bl, a),
			RGBA:   []int{r, g, bl, a},
		})
	}

	data, err := json.Marshal(res)
	if err != nil {
		return nil, fmt.Errorf("marshal sample result: %w", err)
	}
	n := len(res.Points) + len(res.Regions)
	return &client.CommandResponse{
		Success: true,
		Message: fmt.Sprintf("Sampled %d color(s) from %s (%dx%d)", n, imgPath, w, h),
		Data:    json.RawMessage(data),
	}, nil
}

// avgRect averages the straight (non-premultiplied) sRGB color over an inclusive
// pixel rect, clamped to the image bounds.
func avgRect(img image.Image, x0, y0, x1, y1 int) (int, int, int, int) {
	b := img.Bounds()
	x0 = clampInt(x0, b.Min.X, b.Max.X-1)
	y0 = clampInt(y0, b.Min.Y, b.Max.Y-1)
	x1 = clampInt(x1, b.Min.X, b.Max.X-1)
	y1 = clampInt(y1, b.Min.Y, b.Max.Y-1)
	var rs, gs, bs, as, count uint64
	for y := y0; y <= y1; y++ {
		for x := x0; x <= x1; x++ {
			c := color.NRGBAModel.Convert(img.At(x, y)).(color.NRGBA)
			rs += uint64(c.R)
			gs += uint64(c.G)
			bs += uint64(c.B)
			as += uint64(c.A)
			count++
		}
	}
	if count == 0 {
		return 0, 0, 0, 0
	}
	div := func(s uint64) int { return int((s + count/2) / count) }
	return div(rs), div(gs), div(bs), div(as)
}

func hex8(r, g, b, a int) string {
	return fmt.Sprintf("#%02X%02X%02X%02X", r, g, b, a)
}

func clampInt(v, lo, hi int) int {
	if v < lo {
		return lo
	}
	if v > hi {
		return hi
	}
	return v
}

// splitList splits a ';'-separated list of coordinate specs, trimming blanks.
func splitList(s string) []string {
	var out []string
	for _, part := range strings.Split(s, ";") {
		if p := strings.TrimSpace(part); p != "" {
			out = append(out, p)
		}
	}
	return out
}

func parse2(s string) (float64, float64, error) {
	v, err := parseFloats(s, 2)
	if err != nil {
		return 0, 0, err
	}
	return v[0], v[1], nil
}

func parse4(s string) (float64, float64, float64, float64, error) {
	v, err := parseFloats(s, 4)
	if err != nil {
		return 0, 0, 0, 0, err
	}
	return v[0], v[1], v[2], v[3], nil
}

func parseFloats(s string, n int) ([]float64, error) {
	parts := strings.Split(s, ",")
	if len(parts) != n {
		return nil, fmt.Errorf("expected %d comma-separated numbers, got %d", n, len(parts))
	}
	out := make([]float64, n)
	for i, p := range parts {
		f, err := strconv.ParseFloat(strings.TrimSpace(p), 64)
		if err != nil {
			return nil, fmt.Errorf("value %d %q is not a number", i, p)
		}
		out[i] = f
	}
	return out, nil
}
