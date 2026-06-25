# uGUI Version Rules for Hera UI Fixer

This document is the source of truth for `ui_doc` / `manage_ui` diagnostics
and automatic correction. It is intentionally based on Unity's official uGUI
manual pages, not on ad hoc behavior observed in one project.

Last audited: 2026-06-25.

## Supported Docs Buckets

Hera maps Unity Editor versions to uGUI documentation buckets through
`UnityVersionCompat`.

| Unity editor version | Hera docs bucket | Official uGUI manual |
|---|---|---|
| 2022.x | `2022.3` | `com.unity.ugui@1.0` |
| 2023.x | `2023.2` | `com.unity.ugui@2.0` |
| 6000.0 - 6000.2 | `6000.0` | `com.unity.ugui@2.0` |
| 6000.3 - 6000.4 | `6000.3` | `com.unity.ugui@2.0` |
| 6000.5+ | `6000.5` | `com.unity.ugui@2.5` |

User-provided official entry points:

- 2022.3: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html>
- 2023.2: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html>
- 6000.0: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html>
- 6000.3: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html>
- 6000.5: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/index.html>

## Fixer Contract

`UiDocFixer` should implement this policy before or during `ui_doc apply`:

- It must report `docs_version` and `ugui_package` in the apply response.
- It must separate `fixes` from `diagnostics`.
- It may auto-fix deterministic data-shape problems where official uGUI semantics
  are unambiguous.
- It must not rewrite creative layout intent when the official docs describe a
  tradeoff rather than a single correct value.
- It must not add runtime gameplay or animation components. UI Juicy Mode remains
  advisory through `agent_hint`.

Recommended response additions:

```jsonc
{
  "docs_version": "6000.5",
  "ugui_package": "com.unity.ugui@2.5",
  "fixes": [
    {
      "rule": "rect.stretch_offsets",
      "path": "/Canvas/HUD/Health",
      "message": "Converted stretched rect size/pos to offset_min/offset_max."
    }
  ],
  "diagnostics": [
    {
      "rule": "scrollrect.viewport_mask",
      "severity": "warning",
      "path": "/Canvas/ScrollView",
      "message": "ScrollRect viewport should reference a masked viewport RectTransform."
    }
  ]
}
```

Severity meanings:

| Severity | Meaning |
|---|---|
| `info` | Useful version-specific guidance, no broken structure. |
| `warning` | Likely wrong or fragile UI structure, but intent is ambiguous. |
| `error` | The UI element cannot behave as the requested uGUI component without a missing reference/component. |

## Official Source Inventory

### Package 1.0 (`2022.3`)

- Index: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html>
- Canvas: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UICanvas.html>
- Canvas Scaler: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-CanvasScaler.html>
- Rect Transform: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/class-RectTransform.html>
- Basic Layout: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIBasicLayout.html>
- Visual Components: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIVisualComponents.html>
- Image: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Image.html>
- Interaction Components: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIInteractionComponents.html>
- Dropdown: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-Dropdown.html>
- Auto Layout: <https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIAutoLayout.html>

### Package 2.0 (`2023.2`, `6000.0`, `6000.3`)

- Index: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html>
- Canvas: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UICanvas.html>
- Canvas Scaler: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html>
- Basic Layout: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UIBasicLayout.html>
- Image: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Image.html>
- Dropdown: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Dropdown.html>
- Designing UI for Multiple Resolutions: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html>
- Fitting UI to Content: <https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIFitContentSize.html>

### Package 2.5 (`6000.5`)

- Index: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/index.html>
- Canvas: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/UICanvas.html>
- Canvas Scaler: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-CanvasScaler.html>
- Canvas Group: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/class-CanvasGroup.html>
- Canvas Renderer: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/class-CanvasRenderer.html>
- Basic Layout: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/UIBasicLayout.html>
- Visual Components: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/UIVisualComponents.html>
- Text: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Text.html>
- Image: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Image.html>
- Raw Image: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-RawImage.html>
- Mask: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Mask.html>
- Interaction Components: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/UIInteractionComponents.html>
- Selectable: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Selectable.html>
- Button: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Button.html>
- Toggle: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Toggle.html>
- Toggle Group: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-ToggleGroup.html>
- Slider: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Slider.html>
- Scrollbar: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Scrollbar.html>
- Scroll Rect: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-ScrollRect.html>
- Dropdown: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-Dropdown.html>
- Input Field: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-InputField.html>
- Auto Layout: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/UIAutoLayout.html>
- Layout Element: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-LayoutElement.html>
- Content Size Fitter: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-ContentSizeFitter.html>
- Aspect Ratio Fitter: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-AspectRatioFitter.html>
- Horizontal Layout Group: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-HorizontalLayoutGroup.html>
- Vertical Layout Group: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-VerticalLayoutGroup.html>
- Grid Layout Group: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-GridLayoutGroup.html>
- Event System: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/EventSystem.html>
- Graphic Raycaster: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/script-GraphicRaycaster.html>
- Designing UI for Multiple Resolutions: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/HOWTO-UIMultiResolution.html>
- Fitting UI to Content: <https://docs.unity3d.com/Packages/com.unity.ugui@2.5/manual/HOWTO-UIFitContentSize.html>

## Version Differences That Matter

| Area | `com.unity.ugui@1.0` | `com.unity.ugui@2.0` | `com.unity.ugui@2.5` | Fixer impact |
|---|---|---|---|---|
| Package label | `Unity UI` | `uGUI` | `uGUI` | Report exact package label/version in diagnostics. |
| Canvas menu naming | Uses `GameObject > UI > ...` wording | Uses `GameObject > UI (Canvas) > ...` wording | Same as 2.0 | No behavior change; docs-only wording. |
| Canvas shader channels | Basic render modes only in manual page | Adds Additional Shader Channels, Reflection Probes, Vertex Color Always Gamma | Same documented surface | 2.0+ diagnostic for custom UI shaders/dark gradients in Linear color space. |
| Image properties | Source Image, Color, Material, Raycast Target, Preserve Aspect, Set Native Size | Similar page; broader visual overview covers Simple/Sliced/Tiled/Filled | Adds explicit `Raycast Padding`, `Maskable`, `Image Type` fields on Image page | 2.5 diagnostic can mention Raycast Padding / Maskable explicitly. |
| Raycast Receiver | Not documented in 1.0 visual overview | Not found in reviewed 2.0 pages | Documented in 2.5 visual overview | 2.5-only category. Prefer this over transparent Image for invisible hit zones. |
| Auto Layout | Same core layout model | Same reviewed model | Same reviewed model | Common rules across all buckets. |
| Dropdown | Present in 1.0 | Present in 2.0 | Present in 2.5 | Common rules across all buckets. |

## Rule Categories

### Canvas

Official semantics:

- Every UI element must be inside a Canvas.
- Creating a UI element from Unity's UI menu auto-creates a Canvas if none exists.
- Draw order follows hierarchy order: later siblings render above earlier siblings.
- Canvas render modes are Screen Space Overlay, Screen Space Camera, and World Space.
- Overlay and Camera canvases resize with the screen/resolution; World Space canvas
  size is manually controlled through RectTransform.
- Canvas uses EventSystem for messaging.
- In 2.0+ / 2.5, the Canvas manual documents Additional Shader Channels,
  Reflection Probe behavior, and Vertex Color Always in Gamma Color Space.

Common failure modes:

- UI node created outside any Canvas.
- Multiple overlay canvases used without a clear batching/draw-order reason.
- Screen Space Camera canvas without a valid camera reference.
- World Space canvas treated like an overlay canvas and left with nonsensical size.
- Custom UI shader in Linear color space with 2.0+ gamma-color setting but no
  matching gamma-to-linear handling.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `canvas.ensure_parent` | all | `ui_doc apply` root is not `canvas` and no parent Canvas exists | Create Canvas + CanvasScaler + GraphicRaycaster, matching current behavior. |
| `canvas.root_parent` | all | IR root `element:"canvas"` with no explicit parent | Create it at scene root, not under another Canvas. |
| `canvas.graphic_raycaster` | all | Canvas intended for input lacks GraphicRaycaster | Add GraphicRaycaster when building a new Canvas. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `canvas.camera_missing` | all | Screen Space Camera canvas has no camera | `error` | Camera choice is scene-specific. |
| `canvas.worldspace_size` | all | World Space canvas has zero or tiny RectTransform size | `warning` | Correct physical size depends on the game world. |
| `canvas.gamma_vertex_color` | 2023.2+ / 6000.x | Linear color space + custom UI material/shader + gamma vertex color setting detected | `warning` | Shader implementation cannot be inferred safely. |

Implementation notes:

- `ui_doc` currently creates Screen Space Overlay canvases. The fixer should not
  switch render mode unless the IR explicitly asks for it.
- The `ui_doc` schema should eventually accept canvas fields for render mode,
  camera, plane distance, sorting, additional shader channels, and gamma vertex
  color. Until then, diagnose only.

### Canvas Scaler

Official semantics:

- Canvas Scaler controls scale and pixel density for all UI elements under a Canvas,
  including fonts and image borders.
- Screen Space Overlay / Camera canvases support Constant Pixel Size, Scale With
  Screen Size, and Constant Physical Size.
- Scale With Screen Size uses a reference resolution. It scales up or down against
  the current screen and handles aspect mismatch through Screen Match Mode.
- World Space canvases use pixel-density settings rather than screen-size scaling.
- Unity's multiple-resolution guide recommends using anchors and Canvas Scaler
  together: anchors handle aspect changes; scaler handles proportional pixel size.

Common failure modes:

- Pixel-authored UI with no CanvasScaler or Constant Pixel Size only.
- Reference resolution missing while applying HTML/mockup pixel coordinates.
- Match value left at default width-biased value for a portrait design that must
  survive landscape.
- World Space canvas using overlay-oriented scaler assumptions.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `canvas_scaler.add` | all | New root canvas lacks CanvasScaler | Add CanvasScaler. |
| `canvas_scaler.reference_resolution` | all | IR top-level `canvas.reference_resolution` exists | Apply it to CanvasScaler. |
| `canvas_scaler.scale_mode` | all | IR top-level `canvas.scale_mode` exists | Apply the matching enum through reflection. |
| `canvas_scaler.match` | all | IR top-level `canvas.match` exists | Apply `matchWidthOrHeight`. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `canvas_scaler.no_reference_resolution` | all | `ui_doc` uses pixel-like fixed rects but no reference resolution is known | `warning` | Design size must come from user/IR/reference image. |
| `canvas_scaler.match_unspecified` | all | Scale With Screen Size + no match value | `info` | Correct value depends on portrait/landscape target. |
| `canvas_scaler.worldspace_density` | all | World Space canvas with overlay-style reference resolution | `warning` | World-space pixel density is scene scale dependent. |

### RectTransform and Basic Layout

Official semantics:

- RectTransform is the 2D layout counterpart to Transform.
- If the parent is also a RectTransform, child anchors specify how the child is
  positioned and sized relative to the parent rectangle.
- Anchors are parent-relative fractions: 0 is left/bottom, 0.5 is middle, 1 is
  right/top.
- When anchors are together on an axis, Inspector fields are Pos and Width/Height.
- When anchors are separated on an axis, Inspector fields become Left/Right or
  Top/Bottom. Those values are padding/offsets inside the anchor rectangle.
- Resizing a RectTransform changes width/height, not scale; this preserves font
  sizes and sliced-image borders.
- Pivot controls the origin for rotation, scale, and resize.
- RectTransform calculations can be deferred until the end of the frame; explicit
  `Canvas.ForceUpdateCanvases()` may be needed when reading immediately.

Common failure modes:

- Using `sizeDelta`/`size` as real width or height on a stretched axis.
- Setting `pos` and `size` together with `anchor:"stretch"` and expecting fixed
  pixel size.
- Scaling UI elements instead of resizing RectTransform.
- Changing anchors without preserving visual corners, accidentally moving the UI.
- Reading layout-dependent sizes before layout has rebuilt.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `rect.stretch_offsets` | all | Rect has stretched axis and `size`/`pos` but lacks offsets | Convert to `offset_min`/`offset_max` when parent size is known. |
| `rect.full_stretch_zero` | all | Rect has `anchor:"stretch"` and no rect sizing fields | Add `offset_min:[0,0]`, `offset_max:[0,0]`. |
| `rect.named_anchor` | all | Rect has parseable `anchor` preset | Apply anchorMin/anchorMax/pivot from preset. |
| `rect.offsets_win` | all | Both offset and size/pos supplied on stretched axis | Keep offsets authoritative; record diagnostic. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `rect.scale_used_for_layout` | all | UI RectTransform scale differs from `[1,1,1]` on a layout element | `warning` | Some animation systems intentionally scale. |
| `rect.custom_anchor` | all | Raw anchors are not a named preset | `info` | Custom anchors are valid. |
| `rect.defer_layout_read` | all | Caller asks for immediate measured rect after layout edits | `info` | Layout can update at end of frame. |

Implementation notes:

- Per-axis conversion matters. A rect can be stretched horizontally but fixed
  vertically. The fixer should normalize each axis independently.
- When parent size is zero or unknown, do not invent offsets. Emit a warning and
  preserve the IR.

### Text

Official semantics:

- Text displays non-interactive text used for captions, labels, instructions, and
  related UI copy.
- Text supports font, style, size, rich text, alignment, overflow handling, Best Fit,
  color, and material.
- Some controls, such as Button and Toggle, include textual descriptions in their
  standard hierarchy.
- Input Field editing uses Text but the value should be read from the InputField
  component, not from the display Text.

Common failure modes:

- Button/InputField visual label created with a rect that does not stretch to its
  container, causing clipping or off-center text.
- Text component has empty value but is expected to be visible.
- Input Field script points at no Text component.
- Reading masked/password display Text instead of InputField text.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `text.button_label_stretch` | all | `ui_doc` button text creates/reuses child label | Stretch label to full button rect and center by default. |
| `text.default_engine` | all | Text engine omitted | Use TMP if present, otherwise legacy Text. |
| `text.align_apply` | all | IR text alignment is known | Map to TMP alignment or legacy TextAnchor. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `text.empty_visible` | all | Visible Text element has no value | `info` | Empty labels can be placeholders. |
| `text.best_fit` | all | Best Fit requested for dense UI | `warning` | Good sizing range is design-specific. |

### Image, Raw Image, Sprite, and 9-Slice

Official semantics:

- Image displays a Sprite. Raw Image displays a Texture and should be used only
  when a Sprite is not appropriate.
- Image supports Source Image, Color, Material, Raycast Target, and visual display
  type.
- Visual overview defines Image Type values: Simple, Sliced, Tiled, and Filled.
- Sliced uses a 3x3 sprite division so corners are not distorted when resized.
- Filled displays a portion of the sprite from an origin, direction, method, and
  amount; this is the correct uGUI primitive for progress and health bars.
- 2.5 Image page explicitly documents Raycast Padding and Maskable.

Common failure modes:

- Using Simple Image with a sprite that has 9-slice border, distorting corners.
- Building progress bars by resizing a child sprite instead of using Image Filled.
- Using Raw Image for a normal imported UI sprite.
- Invisible click target implemented as fully transparent Image instead of a
  2.5 Raycast Receiver where available.
- Maskable disabled under a Mask when clipping is expected.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `image.sliced_border` | all | Sprite has non-zero border and Image type omitted | Set Image type to Sliced. |
| `image.fill_type` | all | IR has `image.fill` but no type | Set Image type to Filled. |
| `image.sprite_required` | all | IR references an asset path | Load Sprite and attach if found; otherwise diagnostic. |
| `image.progress_fill` | all | IR marks a progress/health bar | Prefer `type:"filled"` + `fill.amount`. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `image.raw_unneeded` | all | RawImage requested for an imported sprite asset | `warning` | Might be intentional for runtime textures. |
| `image.transparent_hit_zone` | 6000.5 | Fully transparent Image with Raycast Target enabled | `info` | Raycast Receiver may be a better 2.5 option. |
| `image.maskable_disabled` | all | Image under Mask has Maskable false | `warning` | Some elements intentionally escape masks. |

### Raycast Receiver

Official semantics:

- Documented in `com.unity.ugui@2.5` Visual Components.
- It is a non-visual component that intercepts Graphic Raycaster hits.
- It is intended for interactive zones without visible geometry.
- It exposes Raycast Target and Raycast Padding.

Version coverage:

| Bucket | Coverage |
|---|---|
| `2022.3` / package 1.0 | Not documented in reviewed official manual. |
| `2023.2`, `6000.0`, `6000.3` / package 2.0 | Not documented in reviewed official manual. |
| `6000.5` / package 2.5 | Documented. |

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `raycast_receiver.prefer_for_empty_hit_zone` | 6000.5 | New invisible clickable zone requested and type exists | Use Raycast Receiver rather than transparent Image. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `raycast_receiver.unavailable` | before 6000.5 | IR requests Raycast Receiver | `warning` | Fallback to transparent Image or CanvasGroup/Graphic settings may be needed. |

### Mask and Clipping

Official semantics:

- Mask is not visible UI by itself; it restricts child element appearance to the
  shape of the parent.
- It is commonly used for a scroll view viewport.
- Masking is implemented through the stencil buffer.

Common failure modes:

- ScrollRect viewport has no Mask/RectMask2D, so content draws outside viewport.
- Deeply nested Masks increase stencil complexity.
- Mask has Show Graphic enabled when the viewport frame should be invisible.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `mask.scrollrect_viewport` | all | Creating a ScrollRect viewport with no mask and requested clipping | Add Mask or RectMask2D if IR explicitly requests clipping. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `mask.missing_viewport_clip` | all | Existing ScrollRect viewport lacks Mask/RectMask2D | `error` | Correct component choice depends on shape/stencil needs. |
| `mask.nested_stencil` | all | Multiple nested Mask components | `warning` | Might be needed, but has rendering cost. |

### Canvas Group

Official semantics:

- Canvas Group controls alpha, interactability, and raycast blocking for a group.
- Alpha is multiplied with child element alpha.
- `Interactable=false` disables interaction for the group.
- `Block Raycasts=false` allows pointer events to pass through.
- `Ignore Parent Groups` lets a group ignore parent CanvasGroup settings.

Common failure modes:

- Hiding a whole panel by disabling the root object when fade/click-through is
  intended.
- Alpha 0 panel still blocking raycasts because `Block Raycasts` is true.
- Parent CanvasGroup unexpectedly disables child controls.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `canvas_group.fade_panel` | all | IR explicitly asks for hidden/nonblocking group | Add/adjust CanvasGroup alpha + blocksRaycasts. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `canvas_group.alpha_blocks` | all | CanvasGroup alpha 0 and blocks raycasts true | `warning` | Could be intentional modal blocker. |
| `canvas_group.parent_override` | all | Child interactable state conflicts with parent group | `warning` | Intent depends on modal design. |

### Canvas Renderer

Official semantics:

- Canvas Renderer renders a graphical UI object contained in a Canvas.
- Standard UI objects include Canvas Renderer wherever needed.
- The inspector exposes no properties.

Fixer policy:

- Do not expose Canvas Renderer in `ui_doc` IR unless a custom Graphic use case is
  added.
- If a custom visual component is detected without CanvasRenderer, emit a warning;
  do not add it blindly unless the component is known to require it.

### Auto Layout Core

Official semantics:

- Auto Layout is built on top of RectTransform.
- Layout Elements provide minimum, preferred, and flexible sizes.
- Layout Controllers consume layout element information to size/place elements.
- Allocation order is minimum, then preferred, then flexible.
- Image and Text provide layout element properties by default.
- Layout controllers should not be manually edited on driven axes; driven values
  are recalculated and are not saved as ordinary scene edits.
- Layout calculation order: horizontal inputs bottom-up, horizontal allocation
  top-down, then vertical inputs bottom-up, vertical allocation top-down.
- Layout rebuilds are normally deferred until the end of the frame.

Common failure modes:

- Manually setting RectTransform width/height on a child driven by a parent layout
  group.
- Expecting GridLayoutGroup children to use their own preferred sizes.
- Reading layout output immediately after edits without forcing rebuild.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `layout.element_apply` | all | IR has `layout_element` | Add/update LayoutElement. |
| `layout.group_apply` | all | IR has `layout` | Add/update matching Horizontal/Vertical/Grid Layout Group. |
| `layout.fit_apply` | all | IR has `fit` | Add/update ContentSizeFitter. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `layout.driven_rect_manual` | all | Child has manual rect fields on an axis controlled by parent layout group | `warning` | The layout group will override it. |
| `layout.rebuild_deferred` | all | Caller expects immediate measured layout | `info` | End-of-frame rebuild is normal. |

### Layout Element

Official semantics:

- Layout Element overrides minimum, preferred, flexible sizes, ignoreLayout, and
  layout priority.
- If multiple components provide layout values, the highest priority wins. If
  equal priority, highest value wins per property.
- Flexible sizes are relative units, commonly 0 or 1.

Common failure modes:

- Child of LayoutGroup uses manual size instead of LayoutElement preferred size.
- Multiple layout providers with unexpected priority.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `layout_element.preferred_from_size` | all | Parent layout controls child size and IR child has `rect.size` but no `layout_element` | Convert size to preferred values when safe. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `layout_element.priority_conflict` | all | Multiple layout providers with different priorities | `warning` | Priority intent is project-specific. |

### Content Size Fitter

Official semantics:

- Content Size Fitter controls the size of its own layout element.
- Its size comes from layout information on the same GameObject: Text, Image,
  layout groups, or LayoutElement.
- Resizing happens around the pivot, so pivot controls expansion direction.
- Official fitting guide warns that placing ContentSizeFitter on each child of a
  LayoutGroup creates a conflict because both parent and child fitter want to
  control the child RectTransform.

Common failure modes:

- ContentSizeFitter on a child whose size is controlled by parent LayoutGroup.
- Pivot left at center when content should grow down/right from a corner.
- Stretched anchors combined with fitting behavior where fixed expansion direction
  is expected.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `fit.apply_modes` | all | IR has `fit.h` / `fit.v` | Apply Horizontal/Vertical Fit modes. |
| `fit.text_pivot_hint` | all | New text fit uses top-left alignment | Set pivot top-left when IR explicitly aligns top-left. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `fit.child_layout_conflict` | all | ContentSizeFitter on child of active LayoutGroup | `warning` | Correct fix is design-specific: remove fitter or let parent own child size. |
| `fit.stretched_anchor` | all | Fitted element uses stretched anchor on fitted axis | `warning` | Expansion direction is ambiguous. |

### Aspect Ratio Fitter

Official semantics:

- Aspect Ratio Fitter controls its own RectTransform.
- Modes include Width Controls Height, Height Controls Width, Fit In Parent, and
  Envelope Parent.
- It does not consider layout information such as min/preferred size.
- Pivot controls alignment during resize.

Common failure modes:

- Combining AspectRatioFitter with layout groups and expecting min/preferred sizes
  to participate.
- Fit In Parent / Envelope Parent used with anchors/pivot that conflict with
  intended alignment.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `aspect_ratio.apply` | all | Future IR explicitly includes aspect ratio fitter | Add/update AspectRatioFitter fields. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `aspect_ratio.layout_ignored` | all | AspectRatioFitter on object also controlled by layout group | `warning` | Official docs state it ignores layout info. |

### Horizontal and Vertical Layout Groups

Official semantics:

- Horizontal Layout Group places children side by side; Vertical Layout Group stacks
  children.
- Group min/preferred size is derived from child min/preferred sizes plus spacing.
- If group has extra space, it distributes according to flexible sizes.
- Properties include Padding, Spacing, Child Alignment, Control Child Size, Use
  Child Scale, and Child Force Expand.
- Docs warn that child scale use means width/height correspond to child scale
  values, and scale values cannot be animated with Animator Controller in that
  layout mode.

Common failure modes:

- Child Force Expand left on when content should hug preferred size.
- Manual child rect sizes under a group that controls child size.
- Animated child scale in a group using child scale.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `layout_group.control_size` | all | IR specifies control_size | Apply childControlWidth/Height. |
| `layout_group.force_expand` | all | IR specifies force_expand | Apply childForceExpandWidth/Height. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `layout_group.force_expand_default` | all | Hug-content UI with force expand true | `info` | Could be desired full-width behavior. |
| `layout_group.child_manual_size` | all | Manual child rect on controlled axis | `warning` | Layout group will override it. |

### Grid Layout Group

Official semantics:

- Grid Layout Group places children in a grid.
- It assigns fixed cell size to every child and ignores child min/preferred/flexible
  size properties.
- In auto layout, horizontal and vertical sizes are calculated independently; grid
  row/column counts depend on each other, so constraints are important.
- For flexible width fixed height, docs suggest Fixed Row Count.
- For fixed width flexible height, docs suggest Fixed Column Count.
- Fully flexible grids can use Flexible constraint but lose control over exact row
  and column counts.

Common failure modes:

- Expecting child LayoutElement preferred size to affect GridLayoutGroup cell size.
- Grid with ContentSizeFitter but no row/column constraint for the intended axis.
- Missing cell size.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `grid.cell_required` | all | IR grid layout lacks `cell` | Use existing component cell size if upserting; otherwise diagnostic. |
| `grid.constraint_apply` | all | IR specifies constraint/count | Apply constraint and constraintCount. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `grid.child_layout_ignored` | all | Child LayoutElement expected to size cells | `warning` | Official docs state Grid ignores child layout sizes. |
| `grid.fit_no_constraint` | all | Grid + ContentSizeFitter + Flexible constraint | `warning` | Flexible is valid but row/column count is uncontrolled. |

### Selectable and Interaction Controls

Official semantics:

- Interaction components are not visible on their own; they must be combined with
  visual components.
- Selectable is the base class for interaction controls.
- Selectable properties include Interactable, Transition, and Navigation.
- Interaction components expose UnityEvents; the UI system catches/logs exceptions
  propagating from attached UnityEvent code.

Common failure modes:

- Button/Toggle/Slider/InputField added without any visual target graphic.
- Navigation left Automatic in dense or non-linear UI where keyboard/gamepad focus
  should be constrained.
- Disabled Selectable still has unrelated hover scripts firing.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `selectable.target_graphic` | all | New Button with Image created by `ui_doc` | Assign Button targetGraphic to Image. |
| `selectable.visual_required` | all | Creating an interaction element without visual component | Add the default visual component for known Hera elements. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `selectable.navigation_auto` | all | Many selectables in complex layout use Automatic navigation | `info` | Correct navigation graph is design-specific. |
| `selectable.no_target_graphic` | all | Existing Selectable has no target graphic | `warning` | Could be custom transition logic. |

### Button

Official semantics:

- Button responds when the user clicks and releases on the control.
- If the pointer moves off before release, the click action does not occur.
- Button uses OnClick UnityEvent.

Fixer policy:

- Auto-create Button with Image and stretched child Text when using `ui_doc` button.
- Do not invent OnClick listeners.
- Diagnose Button without visible/clickable bounds.

### Toggle and Toggle Group

Official semantics:

- Toggle switches an option on/off and has a checkmark Graphic.
- Toggle is a parent clickable area; if it has no children or children are disabled,
  it is not clickable.
- Toggle Group constrains a set of Toggles to one active choice when a new toggle
  is switched on.
- Toggle Group does not immediately enforce uniqueness when multiple toggles are
  already on at scene load or instantiation.
- Toggle Group does not need to be under a Canvas, but the Toggles themselves do.

Fixer policy:

- Diagnose Toggle with no enabled child graphics.
- Diagnose ToggleGroup initial state with multiple toggles on.
- Do not auto-change Toggle values unless IR explicitly declares the group default.

### Slider and Scrollbar

Official semantics:

- Slider selects a numeric value in a min/max range.
- Scrollbar scrolls content and uses value 0..1 plus handle `Size`.
- Slider and Scrollbar support four directions.
- Scrollbar handle size represents visible fraction when used with a ScrollRect.

Fixer policy:

- Diagnose Slider with missing fill/handle rects.
- Diagnose Scrollbar linked to ScrollRect with wrong direction: horizontal should
  be Left To Right, vertical should be Bottom To Top.
- Do not auto-assign value/range unless IR specifies them.

### Scroll Rect

Official semantics:

- ScrollRect displays content larger than a small area.
- Important parts are root ScrollRect, viewport, content, and optional scrollbars.
- Viewport usually has a Mask and must be referenced in the ScrollRect.
- Content must be a single child GameObject under the viewport and must be
  referenced by the ScrollRect.
- Input must be received from inside the ScrollRect bounds, not on the content.
- Unrestricted movement can lose content; Elastic or Clamped keeps content in bounds.
- Auto Hide And Expand View drives viewport and scrollbar size/position and requires
  viewport and scrollbars as children of the ScrollRect root.
- Content pivot/anchors determine alignment when content grows/shrinks.

Common failure modes:

- Missing viewport reference.
- Missing content reference.
- Content not child of viewport.
- Viewport lacks Mask/RectMask2D.
- Content anchors/pivot center when top-aligned scrolling content is intended.
- Auto-hide expand mode used with scrollbars outside the root hierarchy.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `scrollrect.content_reference` | all | IR declares scroll content child | Assign ScrollRect.content. |
| `scrollrect.viewport_reference` | all | IR declares viewport child | Assign ScrollRect.viewport. |
| `scrollrect.top_content_pivot` | all | IR declares vertical top-aligned list content | Set content anchors/pivot to top. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `scrollrect.missing_content` | all | Existing ScrollRect content null | `error` | Cannot scroll without target content. |
| `scrollrect.missing_viewport` | all | Existing ScrollRect viewport null | `warning` | Some setups use root as viewport, but explicit is safer. |
| `scrollrect.unrestricted` | all | Movement Type Unrestricted | `warning` | Might be intended for free panning. |
| `scrollrect.autohide_hierarchy` | all | Auto Hide And Expand View with viewport/scrollbars outside root children | `error` | Official setup requires root children. |

### Dropdown

Official semantics:

- Dropdown lets the user choose one option from a list.
- Template is an inactive child GameObject referenced by the Dropdown.
- Template must have a single item with a Toggle component. Runtime duplicates that
  item for options.
- Caption Text/Item Text enable text support; Caption Image/Item Image enable image
  support.
- Template anchoring and pivot control list placement. Default setup anchors the
  template to the bottom of the control and uses a top pivot so the list expands
  downward.
- Dropdown has simple bounds flipping logic. Template should be no larger than half
  the Canvas size minus the control size, otherwise there may be no valid placement.

Common failure modes:

- Missing Template reference.
- Template active in the scene by default.
- Template missing item Toggle.
- Caption/Item Text references incomplete.
- Oversized template that cannot fit above or below.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `dropdown.template_inactive` | all | IR-created Dropdown template exists | Set template inactive by default. |
| `dropdown.template_anchor` | all | IR-created default dropdown template | Anchor bottom with top pivot for downward expansion. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `dropdown.missing_template` | all | Existing Dropdown template null | `error` | Cannot open list. |
| `dropdown.item_toggle_missing` | all | Template has no item Toggle | `error` | Runtime option duplication depends on it. |
| `dropdown.template_too_large` | all | Template size exceeds documented simple-flip limits | `warning` | May still work with custom placement. |

### Input Field

Official semantics:

- Input Field makes a Text control editable.
- It is not visible by itself and needs visual UI.
- It references a Text component for content display.
- Placeholder is an optional Graphic shown when empty.
- Rich Text is intentionally unsupported for editable text in the way static Text
  uses rich text.
- To obtain input value, read InputField.text, not the display Text component.

Common failure modes:

- Missing TextComponent reference.
- Input Field with no visual background/clickable area.
- Placeholder blocks or confuses focus behavior.
- Agent reads display Text instead of InputField.text.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `inputfield.text_component` | all | IR-created InputField includes child Text | Assign TextComponent. |
| `inputfield.placeholder` | all | IR-created placeholder Graphic exists | Assign placeholder reference. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `inputfield.no_text_component` | all | Existing InputField TextComponent null | `error` | Editable text cannot display correctly. |
| `inputfield.rich_text` | all | Editable field configured as if rich text is expected | `warning` | Official docs describe rich text as unsupported for editing semantics. |

### Event System, Input Modules, and Raycasters

Official semantics:

- Event System sends events based on keyboard, mouse, touch, or custom input.
- It manages selected GameObject, active Input Module, raycasting, and module updates.
- Only one Input Module can be active at a time and modules must be on the same
  GameObject as EventSystem.
- Graphic Raycaster raycasts against Graphics on a Canvas and can ignore reversed
  graphics or be blocked by 2D/3D objects.

Common failure modes:

- No EventSystem in a scene with interactive UI.
- Multiple EventSystems in additively loaded scenes.
- Input module does not match active project input handling.
- Canvas with interactive Graphics but no GraphicRaycaster.
- Non-UI Raycasters unexpectedly block or receive UI-related events.

Auto-fix rules:

| Rule ID | Versions | Condition | Fix |
|---|---|---|---|
| `eventsystem.ensure` | all | Creating Button/interactive UI and no EventSystem exists | Create EventSystem and matching input module. |
| `graphic_raycaster.ensure` | all | Creating interactive Canvas and no GraphicRaycaster exists | Add GraphicRaycaster. |

Diagnostic-only rules:

| Rule ID | Versions | Condition | Severity | Reason |
|---|---|---|---|---|
| `eventsystem.multiple` | all | Multiple EventSystems in loaded scenes | `warning` | Additive scene policy is project-specific. |
| `inputmodule.mismatch` | all | Legacy/new input module mismatch suspected | `warning` | Must be checked against Player Settings symbols. |
| `graphic_raycaster.blocking` | all | Blocking Objects/Mask set on GraphicRaycaster | `info` | May be intentional world-object click blocking. |

## Initial UiDocFixer Implementation Scope

Phase 1 should be deliberately narrow and safe:

1. Report version profile:
   - `docs_version`
   - `ugui_package`
   - official manual index URL
2. Normalize RectTransform IR:
   - stretched-axis `size`/`pos` to offsets when parent size is known
   - `anchor:"stretch"` with no offsets to zero offsets
3. Normalize CanvasScaler:
   - apply explicit `canvas` block
   - warn when pixel layout has no reference resolution
4. Normalize Image:
   - filled image from `image.fill`
   - sliced image from sprite border
5. Diagnose obvious structure:
   - missing Canvas / GraphicRaycaster / EventSystem
   - ScrollRect missing content/viewport/mask
   - ContentSizeFitter under parent LayoutGroup
   - GridLayoutGroup with child LayoutElement expectations
6. Add response reporting:
   - `fixes`
   - `diagnostics`
   - per-rule IDs from this document

Phase 1 must not:

- Auto-create complex controls not represented in `ui_doc/2`.
- Guess visual design intent.
- Rewrite navigation graphs.
- Attach animation/game-feel runtime components.
- Change render modes or cameras without explicit IR support.

## Open Schema Gaps

The current `ui_doc/2` schema can express core CanvasScaler, RectTransform, Image,
Text, LayoutGroup, LayoutElement, and ContentSizeFitter. These official components
are not fully representable yet and need schema additions before auto-fix can be
more than diagnostics:

- Canvas render mode, world camera, sorting, additional shader channels, gamma vertex color.
- CanvasGroup.
- RawImage.
- Raycast Receiver.
- Mask / RectMask2D explicit selection.
- AspectRatioFitter.
- Selectable transition/navigation.
- Toggle / ToggleGroup.
- Slider / Scrollbar.
- ScrollRect structure.
- Dropdown template.
- InputField.
- EventSystem/InputModule policy.
- GraphicRaycaster blocking settings.
