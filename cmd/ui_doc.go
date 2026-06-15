package cmd

import (
	"encoding/json"
	"fmt"
	"image"
	"image/color"
	"math"
	"os"
	"strconv"
	"strings"

	// Register decoders so image.Decode handles the common reference formats.
	_ "image/gif"
	_ "image/jpeg"
	_ "image/png"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// uiDocCmd handles the ui_doc tool. export / gen_sprite / capture are simple
// passthroughs; apply reads the IR document from --file so the (potentially
// large) doc never rides inline in the agent's context — it is parsed here and
// injected as the `doc` param. sample is handled entirely CLI-side: it reads a
// reference image off disk and returns measured colors, which needs no Unity
// state (the scene-touching actions go to the connector).
func uiDocCmd(args []string, send SendFunc) (*client.CommandResponse, error) {
	if len(args) > 0 && args[0] == "sample" {
		return uiDocSample(args[1:])
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
