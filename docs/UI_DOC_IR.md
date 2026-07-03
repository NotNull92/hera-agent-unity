# ui_doc IR reference (`ui_doc/2`)

The contract between the agent and Unity for the **HTML→Unity UI** pipeline. The
agent is fluent in HTML/CSS but weak at uGUI; it designs in HTML, then translates
to this IR, which `ui_doc apply` realizes deterministically as uGUI.

This document is the **static schema**. It mirrors uGUI's serialized model and
is paired with version-aware fixer rules from [`UGUI_VERSION_RULES.md`](UGUI_VERSION_RULES.md):
Unity 2022 uses `com.unity.ugui@1.0`, Unity 2023 / 6000.0 / 6000.3 use
`com.unity.ugui@2.0`, and Unity 6000.5+ uses `com.unity.ugui@2.5`.
Field → uGUI mappings and enum values are given exactly.

## Pipeline

- **New UI**: image/text → HTML mockup → IR (this schema) → `ui_doc apply`.
- **Edit existing UI**: `ui_doc export` (current state → IR) first, then apply with
  `--mode upsert`.

Prefer **relative layout** (`layout` groups, `fill`, anchors) over absolute
coordinates — it survives different inputs/resolutions. Reserve absolute
`pos`/`size` for elements the reference genuinely places by pixel.

### Verify loop (measure, don't guess)

When reproducing a reference image, close the loop instead of eyeballing:

1. `ui_doc sample --image ref.png --at "x,y" [--region "x,y,w,h"]` — read **measured**
   hex colors (normalized [0,1], top-left; `;`-separate many; CLI-side, no Unity).
2. Author the IR with those colors / measured positions.
3. `ui_doc apply --file design.json`.
4. `ui_doc capture --out /tmp/built.png` — render the live overlay UI to a PNG
   (a normal `screenshot` misses ScreenSpaceOverlay canvases).
5. Read the PNG, compare to the reference, fix the largest discrepancy, repeat.

## Document shape

```jsonc
{ "schema": "ui_doc/2", "backend": "ugui", "root": <node> }
```

## Node

```jsonc
{
  "name": "string",          // GameObject name; used to match on upsert
  "element": "canvas|panel|image|button|text|empty",
  "rect":  { ... },          // RectTransform (see Rect)
  "image": { ... },          // Image (image/panel/button)
  "text":  { ... },          // TMP/Text (text; button label)
  "layout": { ... },         // a LayoutGroup ON THIS node (arranges its children)
  "layout_element": { ... }, // this node's size hints for a parent layout group
  "fit": { ... },            // ContentSizeFitter on this node
  "children": [ <node>, ... ]
}
```

`element` only decides which components are added on create. Property objects
(`rect`/`image`/`text`/`layout`/…) are each optional and applied if present.

## Rect → `RectTransform`

uGUI has **two anchor modes**, and the meaning of the size fields differs:

| anchors | size meaning | use these fields |
|---|---|---|
| **non-stretched** (`anchor_min == anchor_max` on an axis) | `sizeDelta` = real size | `pos`, `size` |
| **stretched** (`anchor_min != anchor_max`) | rect = distances from parent edges; `sizeDelta` = padding | `offset_min`, `offset_max` |

```jsonc
"rect": {
  "anchor": "top-center",         // named preset → sets anchor_min/max (+pivot)
  "anchor_min": [0,1], "anchor_max":[1,1],  // raw, alternative to preset
  "pivot": [0.5, 1],
  "pos":  [0, -40],               // anchoredPosition  (non-stretched axis)
  "size": [240, 64],              // sizeDelta         (non-stretched axis)
  "offset_min": [16, 16],         // (left, bottom)    (stretched axis)
  "offset_max": [-16, -16]        // (right, top)      (stretched axis)
}
```

- Mapping: `anchorMin`, `anchorMax`, `pivot`, `anchoredPosition`, `sizeDelta`,
  `offsetMin`, `offsetMax`. Relations: `sizeDelta = offsetMax - offsetMin`,
  `anchoredPosition = offsetMin + sizeDelta*pivot`.
- **On a stretched axis, `size` is ignored — use `offset_min`/`offset_max`.**
  Full-stretch + zero margins = `{"anchor":"stretch","offset_min":[0,0],"offset_max":[0,0]}`.
- Anchor presets: `<vertical>-<horizontal>` with vertical ∈ {top,middle,bottom,stretch},
  horizontal ∈ {left,center,right,stretch}; plus `stretch` (full). Preset also sets pivot.

## Image → `Image`

```jsonc
"image": {
  "color": "#1A1A2EFF",                         // r,g,b[,a] or #hex
  "sprite": { "asset": "Assets/..." } | { "gen": <sprite-spec> },
  "type": "simple|sliced|tiled|filled",         // Image.Type
  "fill_center": true,                          // sliced/tiled
  "preserve_aspect": false,
  "ppu_multiplier": 1.0,                        // pixelsPerUnitMultiplier (sliced/tiled)
  "raycast_target": true,
  "fill": {                                     // type=filled only
    "amount": 0.7,                              // fillAmount 0..1  (progress/HP bars)
    "method": "horizontal|vertical|radial90|radial180|radial360",  // Image.FillMethod
    "origin": 0,                                // fillOrigin (int, per method)
    "clockwise": true                           // fillClockwise (radial)
  }
}
```

- Mapping: `color`, `sprite`, `type`, `fillCenter`, `preserveAspect`,
  `pixelsPerUnitMultiplier`, `raycastTarget`, `fillMethod`, `fillAmount`,
  `fillOrigin`, `fillClockwise`.
- **Progress / health / damage bars: use `type:"filled"` + `fill.amount`** — do
  NOT resize a child sprite. e.g. HP 70% = `{"type":"filled","fill":{"amount":0.7,"method":"horizontal","origin":0}}`.
- `Image.FillMethod`: `Horizontal`=0, `Vertical`=1, `Radial90`=2, `Radial180`=3, `Radial360`=4.
- If `type` is omitted and the sprite has a 9-slice border, apply defaults to `Sliced`
  (so `nine_slice` gen sprites scale without distorting corners).

## Text → `TextMeshProUGUI` / `Text`

```jsonc
"text": {
  "value": "Play",
  "engine": "auto|tmp|legacy",   // auto = TMP if present, else legacy
  "color": "#FFFFFFFF",
  "align": "center|left|right|top-left|top-center",  // → TMP TextAlignmentOptions / legacy TextAnchor
  "font": "Assets/.../Font SDF.asset",   // TMP_FontAsset or legacy Font (icon-font glyph path too)
  "size": 36,                    // fontSize (auto-size off)
  "auto_size": true,             // TMP enableAutoSizing / legacy bestFit
  "wrap": true                   // word wrapping
}
```

## Layout → `HorizontalLayoutGroup` / `VerticalLayoutGroup` / `GridLayoutGroup`

Put a `layout` on a **container** node to arrange its children automatically — no
absolute coords. This is the primary tool for faithful, resolution-robust layouts
(rows of stats, lists, grids).

```jsonc
"layout": {
  "type": "horizontal|vertical|grid",
  "padding": [left, right, top, bottom],   // RectOffset
  "spacing": 8,                            // float (h/v); [x,y] for grid
  "align": "middle-center",                // childAlignment → TextAnchor
  "control_size": [true, true],            // childControlWidth, childControlHeight (h/v)
  "force_expand": [false, false],          // childForceExpandWidth/Height (h/v)
  "reverse": false,                        // reverseArrangement (h/v)

  // grid only:
  "cell": [100, 100],                      // cellSize
  "start_corner": "upper-left|upper-right|lower-left|lower-right",
  "start_axis": "horizontal|vertical",
  "constraint": "flexible|fixed-columns|fixed-rows",
  "count": 3                               // constraintCount
}
```

- `childAlignment` / grid corner/axis / constraint are uGUI enums; the string
  values above map to: `TextAnchor` (UpperLeft…LowerRight), `GridLayoutGroup.Corner`
  (UpperLeft=0,UpperRight=1,LowerLeft=2,LowerRight=3), `GridLayoutGroup.Axis`
  (Horizontal=0,Vertical=1), `GridLayoutGroup.Constraint` (Flexible=0,
  FixedColumnCount=1,FixedRowCount=2).

### Layout element (child hints) → `LayoutElement`

```jsonc
"layout_element": { "min": [w,h], "preferred": [w,h], "flexible": [w,h], "ignore": false }
```
`min*` allocated first, then `preferred*`, then `flexible*` distributes leftover
space proportionally among siblings. → `minWidth/Height`, `preferredWidth/Height`,
`flexibleWidth/Height`, `ignoreLayout`.

### Content size fitter → `ContentSizeFitter`

```jsonc
"fit": { "h": "unconstrained|min|preferred", "v": "unconstrained|min|preferred" }
```
→ `horizontalFit`, `verticalFit` with `FitMode` (Unconstrained=0, MinSize=1,
PreferredSize=2).

## Canvas (root) → `Canvas` + `CanvasScaler`

When the root node has `element: "canvas"`, `ui_doc apply` creates a standalone
Canvas at the scene root and applies the top-level `canvas` block to its
`CanvasScaler`. Set `reference_resolution` to the HTML design size so that
HTML pixel values map 1:1 to uGUI canvas units.

```jsonc
{
  "schema": "ui_doc/2",
  "backend": "ugui",
  "canvas": {
    "scale_mode": "scale_with_screen_size",
    "reference_resolution": [1080, 1920],  // same as the HTML design canvas
    "match": 0.5                           // matchWidthOrHeight 0=width…1=height
  },
  "root": {
    "name": "Canvas",
    "element": "canvas",
    "rect": { "anchor": "stretch", "size": [0, 0] },
    "children": [ ... ]
  }
}
```

Supported `canvas` fields:
- `scale_mode`: `"scale_with_screen_size"` | `"constant_pixel_size"`
- `reference_resolution`: `[width, height]` (used by Scale With Screen Size)
- `match`: `0..1` (`matchWidthOrHeight`)
- `scale_factor`: float (`scaleFactor` for Constant Pixel Size)
- `reference_pixels_per_unit`: float (`referencePixelsPerUnit`, default 100)

If the root element is **not** `canvas` and no `--parent` is supplied, `ui_doc apply`
still auto-parents under an existing Canvas (the previous behavior); the top-level
`canvas` config is ignored in that case.

## gen sprite spec (`image.sprite.gen` / `ui_doc gen_sprite`)

```jsonc
{ "kind": "solid|rounded_rect|gradient|nine_slice",
  "size": [w,h], "color": "#hex",
  "radius": 12,                 // rounded_rect / nine_slice
  "border": [l,b,r,t],          // nine_slice (default = radius)
  "from": "#hex", "to": "#hex", "direction": "vertical|horizontal" }  // gradient
```
Tier-1 only (zero external dependency). Bespoke art (icons/illustrations) must be
real sprite assets (`image.sprite.asset`) or icon-font glyphs (`text.font`).

## Bring your own art (`catalog` → `import`)

When you have a sprite kit instead of procedural shapes, two actions feed real
`image.sprite.asset` references into the IR:

- **`catalog --dir <abs>`** (CLI-side, no Unity) recursively scans a folder and
  returns a manifest — one entry per image. The vision-capable agent **reads the
  listed PNGs** to classify them; the metadata only grounds that read.

  ```jsonc
  { "dir": "...", "count": 12, "images": [
    { "path": "/abs/btn_blue.png", "format": "png", "decoded": true,
      "w": 240, "h": 64, "aspect": 3.75, "has_alpha": true,
      "opaque_bounds": [4,4,232,56],         // [x,y,w,h] trimmed content (top-left origin)
      "palette": [ {"hex":"#1A1A2E","pct":62}, {"hex":"#4ED0FF","pct":18} ],
      "nine_slice_hint": [16,16,16,16],      // [left,bottom,right,top] — pass straight to import --border
      "name_hint": "button" },               // element guess from the filename
    { "path": "/abs/spinner.gif", "format": "gif", "decoded": true,
      "animated": true, "frames": 24, "reference_only": true } ] }
  ```
  GIFs are catalogued `reference_only` (Unity has no GIF→Sprite import).
  Unity-only formats Go can't decode (tga/psd/exr…) appear with `decoded:false`.

- **`import`** (Connector) copies the chosen files into the project as `Sprite`
  assets, then `apply` references them by their new `Assets/` path. Single sprite
  via `--src` + shared flags, or many via `--file`:

  ```jsonc
  { "into": "Assets/UI/Imported",          // default Assets/HeraImported
    "items": [
      { "src": "/abs/btn_blue.png", "name": "btn_blue", "border": [16,16,16,16] },
      { "src": "/abs/panel.png", "border": [24,24,24,24], "ppu": 100, "filter": "point", "pivot": [0.5,0.5] }
    ] }
  ```
  A `border` sets the sprite to `Sliced` (FullRect mesh) so corners stay fixed —
  the same effect as a `nine_slice` gen sprite, but on your own art. Returns
  `{into, imported:[{src,asset,instance_id,sliced}], skipped, errors, count}`.

## Apply semantics

- `apply --file <doc.json>` (doc passed by file, never inline in context).
- `--mode create` (default): always new objects.
- `--mode upsert`: match existing children by name; update rect/image/text/layout
  in place (no duplicates, no deletes). Button labels are reused.
- Before realization, `apply` runs the official-manual-backed uGUI fixer. It
  auto-corrects deterministic IR issues such as stretched RectTransforms using
  `size`/`pos` instead of offsets and filled Images missing `type:"filled"`.
- Response: compact summary
  `{created, updated, sprites, docs_version, ugui_package, manual_url, fixes, diagnostics, errors, root_id}`.
  `fixes` are deterministic mutations applied to the input IR before objects are
  created/updated. `diagnostics` are version-specific warnings or errors where
  the official docs show likely broken uGUI structure but the agent must decide
  the intended fix.
- With Game Feel UI Mode (Beta) on, `apply` adds per-distinct-element-type juice recipes as an
  `agent_hint` (guidance only).
