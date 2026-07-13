# Codex Handoff — UI Toolkit (UXML/USS) Support

> Handoff from Claude to Codex to continue **developing** UI Toolkit support in
> hera-agent-unity. Codex reads project state from git history, so this doc plus
> the commits on branch `feat/ui-toolkit-support` are the source of truth. Read
> `docs/UITK_VERSION_RULES.md` (the verified spec) alongside this.

## Goal

Add **UI Toolkit** (UXML + USS, edited in UI Builder) generation to hera, next to
the existing **uGUI** support. Prompted by a game developer who uses UI Toolkit +
MVVM and wants prompt→scaffolded runtime UI (e.g. a genre-appropriate settings
window) to start from. uGUI and UI Toolkit are entirely different systems
(GameObject+RectTransform vs VisualElement+`.uxml`/`.uss`).

## What is DONE (on this branch — read the commits)

1. **Reflection dump tool** — `tools/build-uitk-schema/`
   - `dump_uitk_schema.cs`: in-editor reflection, run by a maintainer via
     `hera-agent-unity exec --file tools/build-uitk-schema/dump_uitk_schema.cs`
     in each installed Editor. Writes `uitk_schema_<bucket>.jsonl` to the Editor
     temp cache. **Use the path it RETURNS** for step 2 (`Application.temporaryCachePath`
     sanitizes the project name, e.g. `Test6.0.35f1` → `Test6_0_35f1`).
   - `main.go`: `go run ./tools/build-uitk-schema --in <that jsonl>` validates and
     gzips it into `AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes`.
   - **Two-path architecture** (branched on `VisualElementFactoryRegistry.RegisterEngineFactories`):
     - `2022.3 .. 6000.3`: factory registry is fully populated; each factory
       carries its attributes.
     - `6000.5+`: `RegisterEngineFactories` was removed and the registry is only
       lazily/partially populated; enumerate module `VisualElement` types with a
       nested `UxmlFactory` (deterministic), read attributes from
       `UxmlDescriptionRegistry.GetDescription(<Elem>+UxmlSerializedData)`, and
       read attribute defaults from a constructed element instance via the
       attribute's C# member name.
   - USS on every version: `StylePropertyUtil.s_IdToName` (id → kebab name) +
     `IsAnimatable(id)`.

2. **Per-version schema bundles** — `AgentConnector/Editor/Data/uitk_schema_*.jsonl.gz.bytes` (+ `.meta`)

   | bucket | elements | uss | uxml_traits | source-gen |
   |---|---|---|---|---|
   | 2022.3 | 48 | 92 | present | no |
   | 2023.2 | 51 | 92 | present | yes |
   | 6000.0 | 51 | 94 | obsolete | yes |
   | 6000.3 | 51 | 99 | obsolete | yes |
   | 6000.5 | 51 | 99 | obsolete | yes |

   All **runtime elements only** (`UnityEngine.UIElementsModule`) — editor controls
   and package elements (Shader Graph / Tilemap / GraphView) are excluded so the
   bundle is deterministic across projects. Line shapes: `{"kind":"meta"|"element"|"structural"|"uss", ...}`.

3. **Loader** — `AgentConnector/Editor/Core/UiToolkitStore.cs` (+ `.meta`)
   - `public static class`, `namespace HeraAgent`. Mirrors `UnityDocsStore`
     (version-bucketed load of `uitk_schema_<CurrentDocsVersion>...`, mtime cache,
     fallback bucket `6000.0`). Parses JSONL by `kind`.
   - Query API: `IsElement` / `GetElement` / `IsStructural` / `GetUss` /
     `IsUssProperty` / `IsAnimatable` / `ElementCount` / `UssCount` /
     `LoadedBucket` / `UnityVersion` / `UxmlTraits` / `SupportsUxmlElementAttribute`
     / `ElementNames` / `UssNames` / `SuggestElements` / `SuggestUss`.
   - Live-verified in 6000.5: 51 elements / 99 uss, `Button` valid, `ObjectField`
     invalid (editor-only, excluded), `scale` animatable, Slider `high-value`
     default `10`, `SuggestElements("Buton")` → `[Button, Box]`.

## LOCKED design decisions — do NOT re-litigate

- **`ui_system` is the TOP-LEVEL UI axis**, above Game Feel. Stored in
  `asset-config.json` as `ui_system` (`ugui` | `uitk`); default `ugui` keeps all
  current behavior. Hera Settings shows it above the Game Feel section. `ui_doc` /
  `manage_ui` **completely separate** their emitters by `ui_system`; a build is
  all-uGUI or all-UITK, never mixed.
- **Runtime elements only** (v1 scope = runtime game UI). Editor-only controls are
  excluded by construction (not in the bundle → the emitter can't emit them).
- **Accuracy, no inference.** Per-version facts come from binary reflection (the
  bundles) or verified crawls — never from model/training guesses. `docs/UITK_VERSION_RULES.md`
  tags every fact `[verified]` / `[reflection]` / `[manual-todo]`.
- **World-space UITK rendering is gated on the RUNTIME Unity version `>= 6000.2`**
  (Unity 6.2, where PanelSettings gained World Space render mode). Do NOT gate it
  on the docs bucket — the `6000.0` bucket spans 6000.0-6000.2 and straddles the
  6.2 boundary. Below 6.2: do not offer world-space at all. Default is
  screen-space, which works on every version. Determine support at emit time from
  the live version (or live reflection of the PanelSettings render-mode API), not
  from the per-bucket bundle.
- **Game Feel applies underneath, system-aware.** uGUI juice = DOTween /
  RectTransform / CanvasGroup; UITK juice = USS `transition` + `:hover`/`:active`
  pseudo-classes + `experimental.animation`. `Core/UIJuiceGuide` gains a UITK branch.
- **0 new tools** (Hera philosophy) — absorb UITK into existing `ui_doc`/`manage_ui`
  actions/flags via the `ui_system` branch; keep `list --compact` token cost flat.
  Element *property* editing stays delegated to `manage_components`.
- **v1 = layout scaffolding** (`.uxml` + shared `.uss` classes). MVVM data binding
  (`data-source`, SerializedObject binding) is OUT of v1.

## Completed by Codex (2026-07-13, Connector 0.0.60)

1. **`ui_system` toggle** mirrors the Game Feel plumbing: Go config round-trips
   `ui_system` with default `ugui`; `HeraSettings` mtime-caches it; CLI exposes
   `asset-config ui-system [ugui|uitk]` and `--json`; Hera Settings places the
   selector above Game Feel.
2. **Emitter** — `ui_doc apply` and `manage_ui create` branch on `ui_system`.
   UITK emits `.uxml`, shared `.hera-*` `.uss`, `PanelSettings`, and a wired
   `UIDocument` in `Assets/HeraGenerated/UI`; types resolve through reflection,
   leaving asmdef `references: []` unchanged. Exact runtime elements/UXML
   attributes/USS properties are validated via `UiToolkitStore`; bad attributes
   reject, bad USS properties warn and are omitted.
3. **`UiToolkitFixer`** reports `uitk_version`, `uxml_traits`, and `uxml_api`,
   separates fixes from diagnostics, rejects data binding, and supplies the
   system-aware Game Feel advisory hint. World-space gates on the live runtime
   parser (`>=6000.2`), never the docs bucket.
4. **Verification completed** — Go asset-config tests passed; live Unity 6000.5
   compile had zero console errors; a real UXML/USS/PanelSettings/UIDocument
   scaffold, `manage_ui` bridge, validation rejection/downgrade paths, and
   `HeraAgent/Tests/UiToolkitFixer` all passed. Test artifacts were removed and
   the shared config was restored to `ugui`.

## How to work (project rules)

- Follow `AGENTS.md` + `CLAUDE.md`, and respect the "이미 처리된 항목" table and 🔒
  locked designs. Use the `hyper-mode` workflow for connector/CLI code.
- **Do not guess Unity APIs.** Use the running Editor via `hera-agent-unity`
  (`exec`, `describe_type`, `find_method`). New Unity versions differ — the whole
  reason this feature is per-version. Test world-space in a `>= 6000.2` editor.
- New `.cs`/`.bytes` under `AgentConnector/` needs a sibling `.meta` (fresh GUID);
  watch CS0104 (`Object`/`PackageInfo`/`Random`/`Debug`).
- Verify before claiming done: Go gauntlet (`gofmt`/`go vet`/`go build`/`go test`)
  for CLI, and live `editor refresh --compile` + `console --type error` (0 errors)
  for connector. Bump `AgentConnector/package.json` when the emitter makes this
  user-visible (the loader alone has no consumer yet, so it is not bumped).
- Sync docs on every change: help text, `README(.ko).md`, `docs/`, `CLAUDE.md`.
