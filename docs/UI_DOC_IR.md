# ui_doc IR reference (`ui_doc/2`)

The contract between the agent and Unity for the **HTML→Unity UI** pipeline. The
agent is fluent in HTML/CSS but weak at uGUI; it designs in HTML, then translates
to this IR, which `ui_doc apply` realizes deterministically as uGUI.

This document is the **static schema** — it mirrors uGUI's serialized model
(Unity 6000.0 / `com.unity.ugui@2.0`) so the agent never needs to read the raw
component JSON at runtime. Field → uGUI mappings and enum values are given
exactly.

## Pipeline

- **New UI**: image/text → HTML mockup → IR (this schema) → `ui_doc apply`.
- **Edit existing UI**: `ui_doc export` (current state → IR) first, then apply with
  `--mode upsert`.

Prefer **relative layout** (`layout` groups, `fill`, anchors) over absolute
coordinates — it survives different inputs/resolutions. Reserve absolute
`pos`/`size` for elements the reference genuinely places by pixel.

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

When `element:"canvas"` (or auto-created), configure the scaler for a target
resolution so absolute coordinates are reproducible:

```jsonc
"canvas": {
  "scale_mode": "scale_with_screen_size|constant_pixel_size",
  "reference_resolution": [1080, 1920],
  "match": 0.5                              // matchWidthOrHeight 0=width…1=height
}
```

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

## Apply semantics

- `apply --file <doc.json>` (doc passed by file, never inline in context).
- `--mode create` (default): always new objects.
- `--mode upsert`: match existing children by name; update rect/image/text/layout
  in place (no duplicates, no deletes). Button labels are reused.
- Response: compact summary `{created, updated, sprites, errors, root_id}`.
- With UI Juicy Mode on, `apply` adds per-distinct-element-type juice recipes as an
  `agent_hint` (guidance only).
