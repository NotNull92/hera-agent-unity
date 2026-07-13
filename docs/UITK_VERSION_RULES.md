# UI Toolkit Version Rules for Hera UI

This document is the intended source of truth for `ui_doc` / `manage_ui`
diagnostics and generation **when `ui_system` is `uitk`** (UI Toolkit /
UXML+USS), mirroring the role `UGUI_VERSION_RULES.md` plays for uGUI.

**Accuracy policy (strict):** every factual claim below is tagged.

- `[verified]` — confirmed by crawling the official Unity manual page (source
  URL in the Source Inventory). Date of crawl noted per section.
- `[reflection]` — will be produced by the per-version reflection dump tool
  (see "Reflection Dump Spec"). NOT asserted here until extracted.
- `[manual-todo]` — needs a specific manual sub-page crawl not yet done.

Nothing in this document is filled from model/training inference. If a fact is
not tagged `[verified]`, treat it as not-yet-established.

Landing pages + element-reference pages crawled 2026-07-12 for all five buckets.

## Design context (locked)

- `ui_system` (`ugui` | `uitk`) is the **top-level** UI axis, above Game Feel.
  Hera Settings surfaces it above the Game Feel section; `asset-config.json`
  stores `ui_system`; default `ugui` keeps all current behavior unchanged.
- `ui_doc` / `manage_ui` **completely separate** emitters by `ui_system`. A
  single UI build is all-uGUI or all-UITK; never mixed.
- Game Feel applies underneath with system-aware juice: uGUI = DOTween/
  RectTransform/CanvasGroup; UITK = USS `transition` + `:hover`/`:active` +
  `experimental.animation`.
- v1 scope = layout scaffolding (`.uxml` + shared `.uss` classes). MVVM data
  binding is out of v1.

## Why binaries, not package.json

uGUI is the `com.unity.ugui` UPM package, so `UGUI_VERSION_RULES.md` buckets on
its `package.json` version. **UI Toolkit runtime is the built-in
`UnityEngine.UIElementsModule` engine module — there is no package version to
read.** The precise per-version surface (exact element set, exact USS property
support, which UXML authoring API is active, and each element's runtime-vs-editor
classification) lives only in the module assemblies. Hera therefore buckets UITK
on the **Editor version** and extracts the schema by **reflection inside each
running Editor**, once per version → commit a bundle (the `build-unity-docs`
model, sourced from live reflection instead of HTML).

## Supported Buckets `[verified]` (structure/URLs), `[reflection]` (API generation)

| Unity editor | Hera UITK bucket | Manual host crawled | Manual structure (verified) | UXML authoring API |
|---|---|---|---|---|
| 2022.x | `2022.3` | `docs.unity3d.com/kr/2022.3` | classic flat | traits primary; no source-gen `[verified]` |
| 2023.x | `2023.2` | `docs.unity3d.com/kr/2023.2` | **hybrid** (flat pages, but `UIE-ElementRef.html` + `UIE-data-binding.html`) | source-gen added; traits not yet obsolete `[verified]` |
| 6000.0 - 6000.2 | `6000.0` | `docs.unity3d.com/6000.1` | restructured | source-gen; traits obsolete `[verified]` |
| 6000.3 - 6000.4 | `6000.3` | `docs.unity3d.com/6000.3` | restructured + `accessibility/` | source-gen; traits obsolete `[verified]` |
| 6000.5+ | `6000.5` | `docs.unity3d.com/6000.5` | restructured | source-gen; traits obsolete; factory registry retired `[verified]` |

> The "UXML authoring API" column is established by the committed reflection
> bundles: 2022.3 has traits and no source-generated attribute; 2023.2 has both;
> 6000.0+ has the source-generated attribute and obsolete traits. Emitters do not
> generate custom `[UxmlElement]` controls in v1; these facts select diagnostics
> only.

Official landing pages (crawled, `[verified]`):

- 2022.3: <https://docs.unity3d.com/kr/2022.3/Manual/UIElements.html>
- 2023.2: <https://docs.unity3d.com/kr/2023.2/Manual/UIElements.html>
- 6000.0: <https://docs.unity3d.com/6000.1/Documentation/Manual/UIElements.html>
- 6000.3: <https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html>
- 6000.5: <https://docs.unity3d.com/6000.5/Documentation/Manual/UIElements.html>

## Official Source Inventory `[verified]`

Exact sub-page link list observed on each landing page (crawl 2026-07-12).

### 2022.3 (classic flat)

`UI-system-compare.html` · `UIE-simple-ui-toolkit-workflow.html` ·
`UIE-VisualTree.html` · `UIE-Controls.html` (C# structuring; points to
`UIE-ElementRef.html#built-in-controls` for the element list) · `UIE-UXML.html` ·
`UIE-USS.html` · `UIE-LayoutEngine.html` · `UIE-Binding.html` (data binding) ·
`UIE-Events.html` · `UIE-ui-renderer.html` · `UIE-support-for-editor-ui.html` ·
`UIE-support-for-runtime-ui.html` · `UIE-ui-debugger.html` · `UIBuilder.html`

### 2023.2 (hybrid)

`UI-system-compare.html` · `UIE-simple-ui-toolkit-workflow.html` ·
`UIE-VisualTree.html` · **`UIE-ElementRef.html`** (controls) · `UIE-UXML.html` ·
`UIE-USS.html` · `UIE-LayoutEngine.html` · **`UIE-data-binding.html`** ·
`UIE-Events.html` · `UIE-ui-renderer.html` · `UIE-support-for-editor-ui.html` ·
`UIE-support-for-runtime-ui.html` · `UIE-ui-debugger.html` · `UIBuilder.html`

### 6000.0 / 6000.1 (restructured)

`ui-systems/introduction-ui-toolkit.html` · `UIE-simple-ui-toolkit-workflow.html`
· `UIBuilder.html` · `UIE-structure-ui.html` · `UIE-USS.html` · `UIE-Events.html`
· `UIE-ui-renderer.html` · `UIE-data-binding.html` ·
`UIE-support-for-editor-ui.html` · `UIE-support-for-runtime-ui.html` ·
`UIE-work-with-text.html` · `UIE-test-ui.html` · `UIE-examples.html` ·
`UIE-migration-guides.html`

### 6000.3 (restructured + accessibility)

Same as 6000.0 plus `UI-system-compare.html` and **`accessibility/_index.html`**.

### 6000.5 (restructured)

Same top-level set as 6000.0. `UIE-structure-ui.html` links to the reference
pages that matter for generation:

- Structure UI with UXML: `UIE-UXML.html`
- Structure UI with C#: `UIE-Controls.html`
- Custom controls: `UIE-custom-controls.html`
- **UXML elements Reference: `UIE-ElementRef.html`** (per-element pages `UIE-uxml-element-<Name>.html`)

`UIE-USS.html` links to: `UIE-about-uss.html` · `UIE-USS-Selectors.html` ·
**`UIE-uss-properties.html`** (full USS property reference: common / transform /
transition sub-pages) · `UIE-USS-variables.html` ·
`UIE-apply-styles-with-csharp.html` · `UIE-tss.html`.

## Built-in Element Set `[verified]` (names) / `[reflection]` (attributes + surface)

Element **names** below are crawl-verified from each version's element-reference
page. Two caveats: (a) the manual page's small-model extraction may drop an
element, and (b) the manual does not reliably state each element's runtime-vs-
editor surface or attribute set — so the **authoritative allow-list is the
reflection dump**. Element *counts* are intentionally omitted (the crawl summary's
counts were unreliable).

### Crawl-verified version delta (element names)

| Element | 2022.3 | 2023.2 | 6000.0/.1 | 6000.5 |
|---|:--:|:--:|:--:|:--:|
| base + common controls (VisualElement, Label, Button, Toggle, TextField, Slider, SliderInt, MinMaxSlider, DropdownField, RadioButton(Group), ScrollView, ListView, TreeView, Foldout, GroupBox, Box, Image, ProgressBar, Scroller, RepeatButton, TemplateContainer, Vector/Rect/Bounds/Integer/Float/Double/Long/Unsigned fields, Enum(Flags)Field, Color/Curve/Gradient/Object fields, Layer/LayerMask/Mask/Tag fields, Hash128Field, HelpBox, IMGUIContainer, InspectorElement, PropertyField, PopupWindow, MultiColumn*, Toolbar*) | ✅ | ✅ | ✅ | ✅ |
| `ToggleButtonGroup` | ❌ | ✅ | ✅ | ✅ |
| `Tab`, `TabView` | ❌ | ✅ | ✅ | ✅ |
| `TwoPaneSplitView` | ✅ | ✅ | ✅ | ✅ |
| `GUIDField` | ❌ | ❌ | ✅ | ✅ |
| `Mask64Field` | ❌ | ❌ | ✅ | ✅ |
| `RenderingLayerMaskField` | ❌ | ❌ | ✅ | ✅ |
| `GenericDropdownMenu` (C#-only) | ❌ | ❌ | ✅ | ✅ |

> `[reflection]` The runtime-vs-editor surface of each element is NOT taken from
> the manual. It is determined by namespace at dump time:
> `UnityEngine.UIElements.*` = runtime-capable, `UnityEditor.UIElements.*` =
> Editor-only. The emitter must not place an Editor-only element into a runtime
> game UI. No hand-classified runtime/editor list appears in this document.

## Reflection Dump Spec

Implemented in `tools/build-uitk-schema/` (`dump_uitk_schema.cs` + `main.go`; see
its README). The maintainer runs `dump_uitk_schema.cs` via `hera-agent-unity exec
--file` inside each installed Editor (the connector already lives there); it reads
`VisualElementFactoryRegistry.factories` + `StylePropertyUtil` and writes a JSONL,
which `go run ./tools/build-uitk-schema` validates and gzips into
`AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes` (loaded by a
version-bucketed `UiToolkitStore` mirroring `UnityDocsStore`).

### Extraction status

| Bucket | Extracted | elements / structural / uss | uxml_traits | uxml_element_attr |
|---|---|---|---|---|
| **2022.3** | ✅ `[verified]` 2026-07-13 | 48 / 4 / 92 | present | false |
| **2023.2** | ✅ `[verified]` 2026-07-13 | 51 / 4 / 92 | present | true |
| **6000.0** | ✅ `[verified]` 2026-07-13 | 51 / 4 / 94 | obsolete | true |
| **6000.3** | ✅ `[verified]` 2026-07-13 | 51 / 4 / 99 | obsolete | true |
| **6000.5** | ✅ `[verified]` 2026-07-13 | 51 / **0** / 99 | obsolete | true |

> **6000.5 changed the element architecture.** `VisualElementFactoryRegistry.
> RegisterEngineFactories` is gone and the factory registry is only lazily/
> partially populated (returned 0-34 elements non-deterministically), and
> standalone factories report no attributes (traits are empty shells). The dump
> branches on `RegisterEngineFactories` presence: 6000.5 enumerates module
> `VisualElement` types that still carry a nested `UxmlFactory` (deterministic,
> same 51 as 6000.0/6000.3) and reads attributes from `UxmlDescriptionRegistry`
> keyed by each element's nested `UxmlSerializedData` type. Attribute defaults
> come from a constructed element instance (via the attribute's C# member name),
> so constructor-set defaults are correct (e.g. `Slider.high-value = 10`). The
> four UXML directives (`UXML`/`Template`/`Style`/`AttributeOverrides`) are not
> module element types, so 6000.5 has `structural: 0` — cosmetic, they are
> excluded from the allow-list on every version anyway.

> USS property count grows across versions: 92 (2022.3, 2023.2) → 94 (6000.0) →
> 99 (6000.3). Runtime element count is stable (48 → 51 from 2023.2's Tab/TabView/
> ToggleButtonGroup) because 6000.x's new controls (GUIDField, Mask64Field,
> RenderingLayerMaskField, GenericDropdownMenu) are editor/C#-only, excluded by
> the runtime-only filter.

**Runtime elements only.** The dump keeps only elements from
`UnityEngine.UIElementsModule` — the built-in runtime element library. This is
deterministic across projects and Unity versions and matches the emitter's
runtime-UI (v1) scope. Editor controls (`UnityEditor.CoreModule`, mixed with
version-variable editor internals + UI Builder — 2022.3 has ~57 there vs 6000.3's
22), package elements (Shader Graph / Tilemap / GraphView), and project custom
controls are excluded; assembly alone is too noisy to capture a clean editor set,
so editor UI generation (if ever added) needs a curated list. The four UXML
directives (`UXML`, `Template`, `Style`, `AttributeOverrides`) are recorded as
`kind:"structural"`, outside the element allow-list.

**USS** enumerates `StylePropertyUtil.s_IdToName` (id → canonical kebab name),
present in both 2022.3 and 6000.x; `animatable` via `IsAnimatable(id)`. The newer
`ussNameToCSharpName` property is 6000.x-only (using it returned 0 on 2022.3).
`inherited` was dropped from the schema — no reliable reflection source; not
inferred.

Per-line entry shape:

```jsonc
{
  "kind": "element",
  "element": "Slider",
  "full_type": "UnityEngine.UIElements.Slider",
  "surface": "runtime",              // runtime | editor  (from namespace)
  "attributes": [
    { "name": "low-value", "type": "float", "default": "0" },
    { "name": "high-value", "type": "float", "default": "10" },
    { "name": "direction", "type": "SliderDirection", "default": "Horizontal" }
  ],
  "unity_version": "6000.5"
}
```

Extract per version:

1. **Built-in UXML elements** — via `VisualElementFactoryRegistry` (classic) and/
   or `TypeCache.GetTypesWithAttribute<UxmlElementAttribute>()` (source-gen). For
   each: full type, `ui:`/`uie:` tag, and **surface** from namespace.
2. **UXML attributes per element** — name, type, default, from
   `UxmlTraits.uxmlAttributesDescription` (classic) or `[UxmlAttribute]` members
   (source-gen), including inherited.
3. **Supported USS properties** — enumerate the StyleSheet property table for
   property name, inherited flag, animatable flag. This is the emitter's USS
   allow-list.
4. **UXML authoring API generation** — presence of `UxmlElementAttribute`;
   whether `IUxmlFactory`/`UxmlTraits` are `[Obsolete]`. (Resolves the
   `[reflection]` column in Supported Buckets.)
5. **Default theme / built-in `unity-*` USS classes** — so generated USS does
   not collide.

Also extend `tools/unity-editor-inventory/inventory-unity-editors.ps1` with UITK
columns (module DLL presence, UI Builder availability, detected authoring API
generation), recorded with the `%UNITY_HUB_EDITOR%` token (no absolute paths).

## UiToolkitFixer Contract

`UiToolkitFixer` (analog of `UiDocFixer`) applies before/during `ui_doc apply`
when `ui_system` is `uitk`:

- Report `uitk_version` (bucket), `uxml_traits`, `uxml_api`, and the verified
  manual index URL in the apply response.
- Separate `fixes` from `diagnostics` (same `info`/`warning`/`error` model).
- **Validate against the reflection allow-list**: unknown element or UXML
  attribute → `error`; unsupported USS property for the bucket → `warning` and
  is omitted. The store contains runtime elements only, so an Editor-only
  control cannot be emitted through this path.
- Auto-fix only deterministic, unambiguous shape problems.
- Do not rewrite creative intent; do not add runtime gameplay/animation
  components. Game Feel stays advisory via `agent_hint`.

## Manual concepts `[verified]`

Only statements confirmed from the crawled pages:

- UI Toolkit is "a collection of features, resources, and tools for developing
  user interface (UI)," supporting both Editor and runtime UI. (landing pages)
- UI is structured with **UXML** or **C#**; the **visual tree** is "an object
  graph, made of lightweight nodes, that holds all the elements in a window or
  panel." (`UIE-structure-ui.html`, 6000.5)
- **USS** ≈ CSS with Unity overrides: "USS syntax is the same as CSS syntax, but
  USS includes overrides and customizations to work better with Unity." Uses
  selectors, properties, and custom properties (variables). (`UIE-USS.html`, 6000.5)
- The 6000.5 USS reference splits into common properties, **transform**, and
  **transition** sub-pages plus a full "all USS properties, inherited/animatable"
  reference. (`UIE-USS.html`, 6000.5)

`[manual-todo]` — not yet crawled, needed to fill rule categories precisely:
Flexbox layout model specifics (`UIE-LayoutEngine.html`), USS selector/pseudo-
class list (`UIE-USS-Selectors.html`), enumerated USS property list
(`UIE-uss-properties.html`), UXML namespaces and template syntax (`UIE-UXML.html`).
Exact USS property support per version comes from `[reflection]`, not these pages.

## Phase 1 behavior

1. `asset-config.json` stores the top-level `ui_system` (`ugui` default or
   `uitk`), above Game Feel in Hera Settings. A single build is always one
   backend, never a mixed uGUI/UITK tree.
2. `ui_doc apply` with `backend:"uitk"` and `manage_ui create` emit built-in
   **runtime** UXML elements, shared `.hera-*` USS classes, a `PanelSettings`
   asset, and a wired `UIDocument` into `Assets/HeraGenerated/UI`
   (AssetPathGuard containment).
3. Validate every user-supplied element, UXML attribute, and USS property
   against the reflection bundle; reject/downgrade unsupported input with
   diagnostics. Flexbox/USS replaces RectTransform concepts.
4. Screen-space is the default. World-space is allowed only when the **live
   runtime** Unity version is `6000.2+`, never based on the docs bucket.
5. v1 remains layout scaffolding: no custom controls, `[UxmlElement]` authoring,
   or MVVM/data-binding attributes. Game Feel is USS-first advisory `agent_hint`
   guidance, not runtime animation components.

Phase 1 must not: emit Editor-only elements into a runtime target; emit custom
controls or `[UxmlElement]` C# authoring; emit data-binding attributes; guess
visual design intent or invent USS beyond the allow-list.

## Open Items

- Crawl the `[manual-todo]` pages to fill Flexbox/selector/UXML rule categories.
