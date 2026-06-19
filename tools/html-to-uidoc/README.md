# html-to-uidoc

HTML mobile layout → `ui_doc/2` JSON converter for Unity uGUI.

This is a standalone prototype/example area. The production converter is built
into the main CLI as `hera-agent-unity html-to-uidoc`.

## Quick start

```bash
hera-agent-unity html-to-uidoc --file tools/html-to-uidoc/sample.html --out ui_doc.json
hera-agent-unity ui_doc apply --file ui_doc.json
```

## How it works

- Parses **inline styles only** (`style="..."`). CSS classes / `<style>` blocks
  are not parsed.
- Converts absolute pixel positions (`left`, `top`, `width`, `height`) to
  `RectTransform` values.
- HTML `top` (downward positive) becomes uGUI `anchoredPosition.y` negative,
  because uGUI anchored positions are positive upward when anchored to the
  top-left.
- Sets the output `canvas.reference_resolution` to the HTML design size so that
  **1 HTML pixel = 1 uGUI canvas unit** when the CanvasScaler uses
  Scale With Screen Size.

## Supported tags

- `<div>` → panel
- `<button>` → button
- `<img>` → image
- `<span>` → text

## Options

```
--file <path>    Input HTML file (required)
--out <path>     Output JSON file (default: stdout)
--width <N>      Design canvas width  (default 1080)
--height <N>     Design canvas height (default 1920)
```

## Example

```html
<body style="width:1080px; height:1920px; background-color:#1A1A2E;">
  <div style="position:absolute; left:40px; top:200px; width:1000px; height:320px;
              background-color:#0F3460; border-radius:24px;"></div>
</body>
```

Produces a `ui_doc/2` document with a CanvasScaler reference resolution of
`[1080, 1920]` and a panel at `pos: [40, -200]` with `size: [1000, 320]`.

## Limitations

- No responsive CSS (`%`, `vw/vh`, flex, grid).
- No `@media` queries.
- Text rendering depends on available TMP/legacy font assets.
