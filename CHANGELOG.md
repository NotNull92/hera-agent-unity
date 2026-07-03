# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed (Connector 0.0.51 / CLI 0.0.34 — ScriptReference member lookup)

- Rebuilt the `2022.3` bundled ScriptReference index with linked member
  entries from class pages, so `unity_docs` can resolve member queries such as
  `Rigidbody.mass`, `GameObject.AddComponent`, and
  `UnityEditor.AssetDatabase.Refresh` instead of returning `DOC_NOT_FOUND`.
- Updated the Unity docs bundle builder to emit member-link fallback entries
  from official class-page tables while preserving full member pages when they
  are available.

### Added (Connector 0.0.50 / CLI 0.0.33 — exact Unity version guidance)

- Added exact bundled ScriptReference indexes for the supported docs buckets:
  `2022.3`, `2023.2`, `6000.0`, `6000.3`, and `6000.5`. `unity_docs`
  now selects the bundle that matches the connected Editor's docs bucket before
  falling back to `6000.0` / legacy `6.0`.
- Added Unity Editor inventory tooling and documentation for the checked
  editor layouts, package versions, bundled compiler/runtime locations, and
  official ScriptReference bundle counts.
- `status` and `doctor --json` now report the Editor docs bucket and compiler
  summary from heartbeat data, so agents can see which version guidance and
  compiler path class Hera is using.
- Compiler discovery now prefers the running Unity Editor's bundled
  `DotNetSdkRoslyn`, `NetCoreRuntime`, or versioned `DotNetSdk` layout and
  ignores stale saved Unity-local paths from a different editor install.
- `ui_doc apply` diagnostics now select the uGUI manual profile from the
  version bucket: `2022.3 -> com.unity.ugui@1.0`,
  `2023.2` / `6000.0` / `6000.3 -> com.unity.ugui@2.0`, and
  `6000.5 -> com.unity.ugui@2.5`.
- `manage_gameobject duplicate` now routes source/clone ID comparison through
  `EntityIdCompat`, keeping the int `instance_id` contract compatible with
  Unity 6000.5's `EntityId` rename.

### Fixed (Connector 0.0.49 — Unity 6 URP screenshot capture)

- `screenshot --view scene` and `screenshot --view game` now capture the
  focused Unity Editor window before falling back to direct camera rendering.
  This avoids forcing `Camera.Render()` through Unity 6 URP 2D RenderGraph,
  which could log `DrawRenderer2DPass.SetGlobalLightTextures` /
  `Blitter.BlitTexture` null-reference errors even while the visible Game View
  rendered correctly.
- Direct `Camera.Render()` fallback is disabled when the active render pipeline
  is URP and editor-window capture is unavailable, returning a structured
  `SCREENSHOT_FAILED` error instead of polluting the Unity Console with
  RenderGraph exceptions.

### Changed (Connector 0.0.47 — action-level safety metadata)

- Added `metadata.action_safety` to `list --tool <name>` for multi-action
  tools, starting with `manage_assets`, so agents can identify read-only
  `find` separately from destructive `move` and `delete` without inflating
  `list --compact`.

### Added (Connector 0.0.46 / CLI 0.0.31 — CLI-native asset ops and isolated screenshots)

- Added `manage_assets` for compact `AssetDatabase` operations: `find`,
  `mkdir`, `copy`, `move`, and `delete`, with every path constrained to
  `Assets/`.
- Added `screenshot --isolated` for one-GameObject captures by hierarchy path
  or InstanceID, including comma-separated angle contact sheets.
- Added optional `[HeraTool]` safety metadata surfaced only through
  `list --tool <name>`, preserving the cheap `list --compact` discovery path.

### Added (Connector 0.0.45 — official uGUI docs fixer for `ui_doc apply`)

- Added `UiDocFixer`, a version-aware uGUI fixer that selects the official
  documentation bucket from the connected Editor version: `2022.3`
  (`com.unity.ugui@1.0`), `2023.2` / `6000.0` / `6000.3`
  (`com.unity.ugui@2.0`), or `6000.5+` (`com.unity.ugui@2.5`).
- `ui_doc apply` now reports `docs_version`, `ugui_package`, `manual_url`,
  `fixes`, and `diagnostics` so agents can see which official uGUI rules were
  selected and which deterministic corrections were applied.
- The first fixer pass corrects unambiguous IR shape issues such as full-stretch
  RectTransforms missing zero offsets and filled Images missing `type:"filled"`,
  while reporting ambiguous uGUI structure as diagnostics.
- Added `docs/UGUI_VERSION_RULES.md` as the official-manual-backed rule
  catalogue for future `ui_doc` / `manage_ui` fixer expansion.

### Added (Connector 0.0.44 / CLI 0.0.30 — Ultra Hera verification modes)

- Added `Ultra Hera` to Hera Settings with mutually exclusive `Off` / `Light` /
  `Ultra` modes, saved as `loopEngineeringMode` in shared `asset-config.json`
  with `light` as the default.
- `doctor --agent-rules` now reads the saved mode and emits mode-specific
  Light/Ultra verification loops for Codex, Claude, Cursor, Copilot, and other
  agents. `Ultra` mode applies Light checks to every task and upgrades strict
  requests to compile, console, state, test, PlayMode, screenshot, or `ui_doc`
  evidence.
- `asset-config --json` now includes `loop_engineering_mode`, and
  `asset-config list` displays the current Ultra Hera mode.
- Documented the Light and Ultra loops in `AGENTS.md`, `AGENT.md`,
  `cmd/AGENT.md`, `CLAUDE.md`, and command/internal docs.

### Changed (Connector 0.0.43 — token-saving discovery benchmarks)

- Documented the v0.0.43 token-reduction release surface in the README and
  Korean README: `list --compact` is now shown as the 93-token bootstrap path,
  and `find_gameobjects --ids` is shown as the 49-55-token object handoff path
  across Unity 2022.3.62f2, 2023.2.22f1, 6000.3.5f2, and 6000.5.0f1.
- Highlighted Unity 2022.3 LTS / 2023.2 compatibility and the measured token-saving
  release path as paired headlines instead of presenting Hera as a Unity 6-only
  connector.
- Added links from the README release narrative to the version-split benchmark
  reports under `docs/benchmarks/token-reduction/`, preserving separate results
  per Unity editor version.

### Changed (Connector 0.0.42 / CLI 0.0.29 — refactor safety pass)

- **Async file-bus result writes are now atomic.** Heartbeat, PlayMode test
  pending/results, and Package Manager job pending/results now write to a temp
  file and replace the final JSON only after the payload is complete. The Go
  poller now removes a result file only after JSON parse succeeds, so a corrupt
  or partial file is preserved for diagnosis instead of being deleted first.
- **`ui_doc` asset destinations are containment-checked.** `gen_sprite --out`
  and `import --into` now share an `Assets/` path guard that rejects traversal
  such as `Assets/../...` while preserving normalized `Assets/...` asset paths.
- **CLI dispatch moved out of `root.go`.** Standalone and Unity-backed command
  routing now lives in `cmd/dispatch.go`; `root.go` keeps global flag parsing,
  response printing, and parameter parsing. Behavior is unchanged.
- **Help drift is covered.** Added missing `ui_doc` and `uninstall` help topics,
  included `ui_doc` in general help, and added tests that fail when routed help
  topics are missing from the embedded `cmd/help` tree.
- **`--params` collision semantics now match the agent guide.** Explicit
  `--key value` flags override the same key supplied through `--params`.

### Added (Connector 0.0.38 + CLI — `ui_doc` mock up from your own UI kit: `catalog` + `import`)

Two new `ui_doc` actions let the agent build UI from *your* sprite art instead of
only procedural placeholders, while keeping the "what UI is this" judgment with the
vision-capable agent (the CLI/connector can't see pixels):

- **`ui_doc catalog --dir <abs>`** (CLI-side, no Unity) recursively scans a folder of
  UI sprites into a compact manifest — per image: size, aspect, `has_alpha`,
  `opaque_bounds` (trim box), dominant `palette`, a conservative `nine_slice_hint`
  (`[left,bottom,right,top]`, ready for `import --border`), and a filename-derived
  `name_hint`. The agent then reads the listed PNGs to classify them. GIFs are
  catalogued `reference_only` (animated, frame count); Unity-only formats Go can't
  decode (tga/psd/exr…) are listed with `decoded:false`. `--max` caps the count (300).
- **`ui_doc import`** (Connector) copies external sprite files (absolute paths) into
  the project as `Sprite` assets so `apply` can reference them by `Assets/` path.
  Single sprite via `--src` + shared flags, or many with per-sprite settings via
  `--file` `{into?, items:[{src, name?, border?, ppu?, filter?, pivot?}]}`. A `border`
  sets `Image.type = Sliced` (FullRect mesh), the same fixed-corner scaling as a
  `nine_slice` gen sprite but on your own art. Default dest `Assets/HeraImported/`;
  GIFs are skipped (Unity has no GIF→Sprite import). Returns
  `{into, imported:[{src,asset,instance_id,sliced}], skipped, errors, count}`.

Flow: `catalog` (scan) → agent reads PNGs to classify → `import` (bring chosen
sprites into the project) → `apply` (IR references them by `Assets/` path).

### Changed (Connector 0.0.36 — UI Juicy Mode: deeper Game-Feel coverage)

Audited `Core/UIJuiceGuide` against the "Secrets of Game Feel and Juice" playbook
and closed four gaps, plus added a dedicated bar recipe:

- **Audio depth** — `button` now advises randomizing the click SFX pitch (±5–8 %)
  so repeats don't fatigue, and `text` count-up advises a **rising pitch** per
  consecutive step (Peggle / Mario-coin) to sell a combo's build-up.
- **Hit-pause / freeze-frame** — `canvas` setup now teaches the ~30–80 ms freeze on
  high-impact moments (it was only hinted at via `.SetUpdate(true)`).
- **Flash-on-hit** — `image` now advises a 1–2 frame white flash + tween interrupt +
  knockback + bassy impact SFX when an element takes damage.
- **The golden rule** — the shared footer now states it explicitly: *double down on
  the screen's purpose* (reward UI earns big juice; precision / input-heavy UI stays
  calm and readable).
- **New `bar` recipe** — the signature health / progress-bar juice (instant fill drop
  with a delayed "chip" bar catching up, low-value danger pulse, segment ticks). It
  fires through `ui_doc apply`: a node whose `image` has a `fill` (or `type: filled`)
  is now classified as `bar` for the juice hint instead of the generic `image`.

### Fixed (CLI 0.0.25 — `editor refresh --compile` no longer blocks for minutes)

- **`editor refresh --compile` could hang for up to 5 minutes**, which made
  wrapping agents (Claude Code's 120s bash timeout) background the process — the
  recompile "kept running" in the background very frequently. `waitForReady` had a
  hard-coded `5*time.Minute` cap and ignored `--timeout`. It now honors the caller's
  `--timeout` (default 60s, under the 120s agent budget; raise it for big projects).
- **Timeout is no longer misreported as a compile error.** `waitForReady` returned
  `hasErrors=true` on timeout, so `editor refresh --compile` printed "compilation
  finished with errors" when it had merely timed out. It now returns a distinct
  `ready=false`, and the command reports "compilation still running after Ns — raise
  --timeout, or poll status / console" instead. (`refresh_unity --compile request`
  is unchanged — it was already fire-and-forget.)

### Changed (Connector 0.0.35 / CLI 0.0.24 — token & robustness follow-ups)

Three follow-ups from the discovery-token audit:

- **`find_method` / `describe_type` drop the redundant `is_static` field.** The
  method signature already encodes the `static` modifier (and the name), so the
  separate boolean was pure overhead. `find_method`'s grouped results now return
  bare signature strings instead of `{signature, is_static}` objects. (Property /
  field / event `is_static` stays — those carry it nowhere else.)
- **Tool-command errors no longer print the message twice.** A failed tool command
  emitted both the compact JSON error envelope (for the agent to parse) *and* a
  human `Error: command failed: <message>` line — doubling the error text in the
  agent's captured output. The CLI now exits non-zero on an `ErrCommandFailed`
  sentinel without the duplicate line for AI-target commands; human commands keep
  the readable line.
- **Hera Settings compiler auto-detect prefers `csc.dll`.** `FindUnityBuiltInCsc`
  hard-coded the 6.0–6.4 `DotNetSdkRoslyn/csc.dll` path and otherwise fell to Mono
  `csc.exe`; it now recursively prefers any `csc.dll` (finding the 6.5 SDK path
  too) so the persisted `defaultCscPath` can't point at the CodePages-breaking Mono
  compiler. Defense-in-depth on top of the v0.0.34 `ResolveCsc` guard.

### Fixed (Connector 0.0.34 — exec on non-English Windows + Unity 6.5)

- **`exec` failed to compile *anything* on Korean/Japanese/Chinese Windows under
  Unity 6.5+** with `EXEC_COMPILE_ERROR` and a `System.Text.Encoding.CodePages`
  assembly-load error — every snippet, even `return 1+1;`. **Three root causes**, all
  fixed version-agnostically (no per-version branching, so 6.0–6.4 can't regress):
  - **Stale/auto-detected `defaultCscPath` override.** The Hera Settings window
    auto-detects a compiler and persists it to `asset-config.json` `defaultCscPath`,
    which `ResolveCsc` honored *before* `FindCsc`. On Unity versions where its
    detector can't find the SDK Roslyn it saves the bundled Mono `csc.exe` — so even
    a corrected `FindCsc` was bypassed. `ResolveCsc` now ignores a `defaultCscPath`
    that points at the Mono `csc.exe` (`MonoBleedingEdge/…/csc.exe`) and falls through
    to `FindCsc`, so a stale or mis-detected path can't break exec. A real csc.dll or
    VS/MSBuild csc.exe override is still honored.
  - **Wrong compiler selected.** Unity moved the .NET SDK Roslyn between versions —
    6.0–6.4: `…/DotNetSdkRoslyn/csc.dll`; 6.5+: `…/DotNetSdk/sdk/<version>/Roslyn/
    bincore/csc.dll` (version-numbered). `FindCsc` had a hard-coded
    `MonoBleedingEdge/…/csc.exe` fast-path candidate that, on Windows 6.5 (where the
    6.3 `csc.dll` candidate path no longer exists), short-circuited to the **Mono
    `csc.exe`** before the recursive `csc.dll` search ever ran. Mono `csc.exe` run on
    a non-Latin (CP949) Windows console fails to load `System.Text.Encoding.CodePages`
    and crashes at startup. Fix: dropped the `csc.exe` fast-path candidate so the
    recursive `csc.dll` search always wins (it finds the version-numbered 6.5 SDK
    path too); Mono `csc.exe` is now a true last resort, used only when no .NET
    Roslyn ships.
  - **CP949 output encoding.** Even on the correct `dotnet exec csc.dll` path, csc on
    a non-Latin console encodes redirected output via `Encoding.GetEncoding(<oem-cp>)`,
    which needs the same missing assembly. Fix: add `-utf8output` to force UTF-8
    compiler output, and write the snippet with a UTF-8 BOM so csc never falls back
    to the system code page to read the source.
- Verified on Unity 6.3 (macOS, live): exec + `--usings` + `--compile_only` + `--file`
  all still pass, and `FindCsc` resolves the same `csc.dll` it did before. Verified on
  Unity 6.5 (macOS): the new resolution picks `DotNetSdk/sdk/8.0.318/Roslyn/bincore/
  csc.dll` and `dotnet exec csc.dll -utf8output` compiles with clean English
  diagnostics.

### Changed (Connector 0.0.32 — discovery token cost)

Measured the AI-agent discovery surface and cut the three biggest payloads. No
behavior change beyond response shape; full detail stays one flag away.

- **`list` (default) no longer dumps every tool's full JSON Schema** — it returns
  `{name, description}` per tool (~70% fewer tokens). The per-tool schema, which
  was the bulk of the bytes, is still available on demand via `list --tool <name>`.
- **`list --names` now returns a flat array of names** (`["exec", …]`) instead of
  `{name, description}` objects — the cheapest discovery surface, and the one the
  AGENTS.md bootstrap runs every session.
- **`list_assemblies` returns bare name strings by default** instead of
  `{name, version}` objects — most assemblies report `0.0.0.0`, so the version was
  noise (~50% fewer tokens). Opt back in with `--include_version`; `--include_location`
  still implies version.

## [CLI 0.0.23 / Connector 0.0.30] - 2026-06-15

### Fixed

- **PreWarmCompiler noise**: `ExecuteCsharp.PreWarmCompiler()` now skips when
  `EditorApplication.isCompiling` or `isUpdating`, and failures are silently
  ignored. Pre-warming is purely an optimization and should not spam the console.

## [CLI 0.0.22 / Connector 0.0.29] - 2026-06-15

### Fixed

- **Connector compile error**: `ExecuteCsharp.PreWarmCompiler()` now uses
  `UnityEngine.Debug.LogWarning` to avoid ambiguous reference with
  `System.Diagnostics.Debug`.

## [CLI 0.0.21 / Connector 0.0.28] - 2026-06-15

### Added (Connector 0.0.27 + CLI — ui_doc verify loop: capture + sample)

Two endpoints that turn "eyeball and rationalize" into "measure and correct" when
reproducing a reference image — the measure-don't-guess loop is now first-class.

- **`ui_doc capture`** (Connector) renders the live UI to a PNG. ScreenSpaceOverlay
  canvases are composited after the camera, so a normal `screenshot` misses them;
  `capture` temporarily routes every root non-world canvas through a throwaway
  camera + RenderTexture, `ReadPixels` → PNG, then restores each canvas (in a
  `finally`, so a throw can't leave the scene mangled). Flags: `--out`,
  `--width`/`--height` (default = canvas pixel size), `--bg #RRGGBBAA` (alpha 0 =
  transparent), `--canvas` (restrict to one). Replaces the hand-rolled temp-camera
  `exec` the HUD work kept rewriting.
- **`ui_doc sample`** (CLI-side) reads measured hex colors from a reference image —
  `--at "x,y"` points and/or `--region "x,y,w,h"`, normalized [0,1] top-left,
  `;`-separated for many, `±--kernel` px averaging (default 2). Returns
  `{at/region, px, hex, rgba}`. Runs in the CLI (pure stdlib image decode) since it
  only reads a static file — no Unity round-trip, available before `apply`.
- Loop: `sample` colors → author IR → `apply` → `capture` → compare → fix → repeat.
  Documented in COMMANDS.md / AGENTS.md / `docs/UI_DOC_IR.md`.

### Changed (Performance & reliability refactor)

A conservative pass focused on faster `exec` warm-up, lower token/byte overhead,
and surviving domain reloads more gracefully. No user-facing defaults were changed.

- **Fixed `batch` nil-pointer panic**: a successful `batch` invocation no longer
  crashes the CLI when `Execute()` dereferences a nil response.
- **`batch` compact/quiet output**: when `--compact-json` or `--quiet` is passed,
  each step is emitted as compact JSON or plain text instead of styled TUI output.
- **Faster Go response formatting**: the success path no longer round-trips
  `resp.Data` through `interface{}`; compact mode prints raw bytes and pretty mode
  uses `json.Indent` directly. Plain string responses are still unquoted.
- **`SendBatch` reliability**: batch requests now reuse the same domain-reload
  retry loop as single commands and respect an explicit `--timeout` when provided.
- **HTTP response size guard**: responses larger than 50 MB are rejected with a
  clear error instead of being buffered indefinitely.
- **Connector compiler pre-warm**: after the HTTP server starts, a trivial
  `return null;` compile is scheduled on `EditorApplication.delayCall` to keep the
  VBCSCompiler server warm across domain reloads.
- **`exec` in-memory cache expanded**: `MaxInMemoryAssemblies` raised from 32 to
  128, reducing disk loads during long agent sessions.
- **`asset-config.json` compiler paths honored**: `ExecCompileCache` now prefers
  `defaultCscPath`/`defaultDotnetPath` from Hera Settings before falling back to
  auto-discovery.
- **Faster compiler discovery**: known Unity installation paths are probed before
  falling back to recursive directory scans.
- **Cached assembly reference enumeration**: `EnsureRefs` skips full AppDomain
  scanning when a validated `refs-meta.json` + `refs-<hash>.rsp` already exists.
- **`Serialize` member caching**: per-type field/property reflection results are
  cached within a domain, reducing repeated reflection cost for complex return
  values.

### Added (Connector 0.0.26 — ui_doc IR v2: layout system, Image fill, stretch offsets)

Audited the ui_doc IR against the official uGUI manual (Unity 6000.0 /
com.unity.ugui@2.0) and closed the accuracy/coverage gaps. Schema is now
`ui_doc/2`; full reference in `docs/UI_DOC_IR.md`. All new types resolve at
runtime (still no compile-time com.unity.ugui dependency).

- **Layout system** (the big one — relative layout, no fragile absolute coords):
  `node.layout` adds a `HorizontalLayoutGroup` / `VerticalLayoutGroup` /
  `GridLayoutGroup` (padding, spacing, child alignment, control/force-expand size,
  reverse; grid cell/spacing/start corner/axis/constraint/count). `node.layout_element`
  → `LayoutElement` (min/preferred/flexible/ignore). `node.fit` → `ContentSizeFitter`
  (h/v: unconstrained|min|preferred).
- **Image `type` + Filled fill**: `image.type` (simple/sliced/tiled/filled) and
  `image.fill {amount, method, origin, clockwise}` — the idiomatic progress / HP /
  damage bar (`fill.amount`) instead of resizing a child. Plus `fill_center`,
  `preserve_aspect`, `ppu_multiplier`, `raycast_target`.
- **Rect stretch offsets**: `rect.offset_min`/`offset_max` map to
  `RectTransform.offsetMin/offsetMax`. Fixes the bug where `size` (sizeDelta) was
  used as the size on a stretched axis (where it actually means edge padding).
  export now emits offsets for stretched rects, pos/size for non-stretched.

### Added (Connector 0.0.25 — ui_doc text font in the IR)

- **`text` nodes now accept `font`** — an asset path to a TMP_FontAsset (for TMP
  text) or a Font (legacy). `apply` loads it and assigns it via reflection; a type
  mismatch (TMP path on a legacy Text, or vice versa) is a safe no-op. Lets a doc
  pick a project/icon font in one step instead of a follow-up `manage_components`
  call. (`export` intentionally omits `font` to avoid stamping every text node with
  the default font path.) This is also the in-model path for icon-font glyphs.

### Added (Connector 0.0.24 — ui_doc text color + alignment in the IR)

- **`text` nodes now accept `color` (#hex or r,g,b[,a]) and `align`**
  (`center` / `left` / `right` / `top-left` / `top-center`). `apply` sets them on
  the text component via reflection; `align` maps to TMP `TextAlignmentOptions`
  or legacy `TextAnchor` automatically. Removes the manual `manage_components` /
  `exec` pass previously needed to make agent-built text readable and centered.
  `export` now also emits `text.color` when non-white. (Found by dogfooding —
  every HUD apply needed a follow-up to recolor/centre text.)

### Fixed (Connector 0.0.23 — ui_doc apply auto-Sliced for bordered sprites)

- **`apply` now sets `Image.type = Sliced` when the node's sprite has a 9-slice
  border** (any `nine_slice` gen sprite, or a referenced sprite with a border).
  Previously the Image kept the default `Simple` type, which stretches a bordered
  sprite's corners into an oval — making `nine_slice` useless without a manual
  follow-up. Set via reflection, so the connector keeps no compile-time
  com.unity.ugui dependency. (Found by dogfooding ui_doc to rebuild a game HUD
  from a screenshot.)

### Added (Connector 0.0.22 — `ui_doc` phase 2: nine_slice + upsert)

- **`gen_sprite` nine_slice kind** — bakes a rounded-rect texture and sets the
  sprite's 9-slice border (default = corner radius; override with `border [l,b,r,t]`)
  plus FullRect mesh, so a single generated sprite scales without distorting corners.
- **`apply --mode upsert`** — matches existing children by name and updates their
  rect / graphic / text **in place** (same `root_id`, no duplicate objects; button
  labels are reused) instead of always creating. Completes the
  export → edit → apply round-trip. Default stays `create`. Response now reports
  `updated` alongside `created`.
- SVG was evaluated and **deferred**: the rasterization shader (`Unlit/VectorGradient`)
  ships with the full `com.unity.vectorgraphics` package, not the built-in
  `com.unity.modules.vectorgraphics` module, so it can't be delivered as a
  zero-dependency, verified feature yet.

### Added (Connector 0.0.21 + CLI — `ui_doc`: HTML→Unity UI pipeline)

- **New `ui_doc` tool (uGUI)** — closes the agent's weakest Unity area (UI) by
  routing design through HTML, which LLMs are fluent in. Three deterministic
  endpoints around a compact JSON IR (`ui_doc/1`):
  - **`export`** serializes a live UI subtree to the IR (defaults omitted) so the
    agent grounds an HTML design on the project's real structure instead of guessing.
  - **`apply`** builds an IR document (always-create) under a parent and returns a
    compact summary (`created` / `sprites` / `errors` / `root_id`). The doc is passed
    via `--file` so it never rides inline in the agent's context.
  - **`gen_sprite`** bakes a Tier-1 procedural sprite (`solid` / `rounded_rect` /
    `gradient`) and imports it as a Sprite — **no external dependency** (hera's
    zero-runtime-dep principle). Richer bitmap art is intentionally out of scope.
- **UI Juicy Mode integration** — when on, `apply` adds the Game UI/UX Bible juice
  recipes for each *distinct* element type in the doc as an `agent_hint` (deduped
  once, not per element — strong signature, lean tokens). Guidance only; no runtime
  components attached (the locked Juicy Mode boundary holds). Added
  `UIJuiceGuide.ForElements`.
- Reuses `SerializedPropertyValue`, `ComponentTypeResolver`, `TargetResolver`,
  `HierarchyPath`, `ProceduralSprite`; UI/TMP types resolve via `TypeCache`, so the
  connector still compiles without `com.unity.ugui`. MVP scope: uGUI only,
  always-create, `solid`/`rounded_rect`/`gradient`. Phase 2: upsert, `nine_slice`/`svg`,
  UI Toolkit.

### Fixed (Connector 0.0.21 — multi-word action dispatch)

- **Multi-word actions returned `UNKNOWN_COMMAND`** on action-method tools that
  lack a `HandleCommand` fallback: `manage_ui get_rect` / `set_anchor` / `set_rect`
  and `manage_gameobject set_parent` / `set_active` / `set_name` / `get_transform`.
  `ToolDiscovery` registered action handlers under `method.Name.ToLowerInvariant()`
  (`SetRect` → `setrect`), but the CLI sends snake_case (`set_rect`) to match the
  tool/parameter naming convention, so the lookup missed. Now registered under
  `StringCaseUtility.ToSnakeCase(method.Name)` (`SetRect` → `set_rect`). Single-word
  actions are unchanged (snake_case == lower); only HandleCommand-free multi-word
  actions are affected, and no tool exposes a non-action public-static `(JObject)`
  method, so there is no misrouting.

### Fixed (Connector 0.0.20 — Unity 6000.5 compatibility)

- **Unity 6000.5 (e.g. 6000.5.0b11) failed to compile the connector**, which
  silently stripped every `[MenuItem]`/`[InitializeOnLoad]` — the **HeraAgent menu
  disappeared** and no HttpServer booted. Root cause: 6000.5 promoted
  `EditorUtility.InstanceIDToObject(int)` and `Object.GetInstanceID()` from a
  deprecation *warning* (as in 6000.3) to an *obsolete-as-error* (CS0619), replacing
  them with `EditorUtility.EntityIdToObject(EntityId)` / `Object.GetEntityId()`.
- Added `Core/EntityIdCompat` — a version-gated (`UNITY_6000_5_OR_NEWER`) shim that
  routes the 27 call sites across 8 files through the new API on 6000.5+ and the
  legacy API on 6000.0–6000.4. The `int` id is read via `EntityId.GetHashCode()`
  (bit-identical to the forbidden `EntityId → int` cast), preserving the existing
  integer `instance_id` contract with no round-trip change. Verified clean compile
  on both 6000.5.0b11 and 6000.3.5f2.

## [0.0.16] - 2026-06-09

### Fixed

- `ToolMetadata.cs`: qualified `SchemaUtility.GetTypeName` call missing after
  PR #4 extraction. Restored compilation in UPM package context.
- `HeraAgentAssetConfigWindow.Model.cs` / `.View.cs`: missing `{` after
  `partial class` declarations (syntax errors surfaced in UPM immutable folder).
- Missing `.meta` files for `SchemaUtility.cs`, `TargetResolver.cs`,
  `HeraAgentAssetConfigWindow.Model.cs`, `HeraAgentAssetConfigWindow.View.cs`.

## [0.0.15] - 2026-06-09

### Added

- Go test coverage expanded across `cmd` (`doctor`, `install`), `internal/poll`,
  and `internal/assetconfig`.
- C# editor tests for `HierarchyPath.Build` / `HierarchyPath.Find`.

### Changed

- C# `TargetResolver` extracted from `ManageComponents`, `ManageGameObject`,
  and `ManageUI` to eliminate duplicated GameObject/Component resolution logic.

### Fixed

- `staticcheck`/`errcheck` warnings in newly added test files.

## [0.0.15] - 2026-06-09

### Fixed (CLI — domain-reload window resilience)

- **Commands issued while Unity is mid-domain-reload now ride the reload out
  instead of failing.** The connection-refused retry in `internal/client` had two
  gaps that surfaced when chaining a command right after `editor refresh
  --compile`: (1) it retried a **fixed 10×500ms (~5s)** budget, so a reload
  longer than ~5s exhausted it (`cannot connect ... after 10 retries`), and
  (2) it re-dialed the **same port**, so when Unity rebinds to a new port during
  the reload (e.g. 8090 → 8092) every retry hit the dead listener. The retry now
  re-reads the heartbeat each attempt to **follow the port rebind** and keeps
  trying until the editor answers, reports `stopped`, disappears, the caller's
  timeout fires, or a 60s fallback elapses — bounded, but no longer cut short
  while a reload is genuinely still in progress. The
  connection-established-then-closed path is unchanged (still not retried, to
  preserve the no-double-dispatch guarantee for mutating commands).

### Fixed (manage_ui review follow-ups — Connector v0.0.16)

- **EventSystem now gets the input module matching the project's active input
  handling.** Previously `manage_ui create` always tried `StandaloneInputModule`
  first and only fell back to `InputSystemUIInputModule` if the type was
  unloadable — but `StandaloneInputModule` always loads (it ships with
  com.unity.ugui), so on **new-Input-System-only** projects the auto-created
  EventSystem got a module that throws every frame and routes no input. The gate
  is now Unity's own `ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER`
  compile defines, so it picks `InputSystemUIInputModule` exactly when Unity's
  GameObject ▸ UI ▸ EventSystem menu would.

### Changed (shared hierarchy-path resolver extracted to Core — v0.0.16)

- **`HierarchyPath.Find(path)`** added to `Core/HierarchyPath.cs` (the reverse of
  the existing `Build`). The inactive-subtree-aware `"/Root/Child"` → GameObject
  walk was verbatim-duplicated in `manage_gameobject`, `manage_components`, and
  `manage_ui`; with the third consumer it crossed the repo's extraction
  threshold. All three now call the shared resolver and dropped their private
  `FindByPath`/`ResolveByPath` + `WalkPath` copies (and the now-unused
  `UnityEngine.SceneManagement` usings).

### Added (uGUI authoring tool — Connector v0.0.15)

- **`manage_ui` — a new `[HeraTool]` for uGUI authoring.** Verified end-to-end
  against a live Unity 6 editor. Actions:
  - **`create`** — spins up a UI element (`canvas`, `panel`, `image`, `button`,
    `text`, `empty`) with automatic Canvas + EventSystem scaffolding when one is
    missing. Non-canvas elements default to the existing/auto Canvas as parent.
  - **`set_anchor`** — exposes Unity's named anchor-preset grid
    (`top-center`, `middle-left`, `stretch`, …) plus raw `anchor_min` /
    `anchor_max`. By default the element's rect stays **visually fixed** (offsets
    recomputed, the painful part to do by hand); `--snap` zeroes offsets / fills
    and moves the pivot to match (Unity's Alt+Shift click).
  - **`get_rect` / `set_rect`** — read the full RectTransform (anchors, pivot,
    offsets, size, detected preset) and set any subset of fields directly.
- **Zero compile-time dependency on com.unity.ugui / TextMeshPro.** UI and TMP
  component types resolve through `TypeCache` (`ComponentTypeResolver`) and are
  added via `AddComponent(type)`, so the connector still compiles in a project
  without those packages. The text engine auto-selects TextMeshPro when present,
  else legacy `UnityEngine.UI.Text`; force either with `--text tmp` / `--text
  legacy`.
- **Boundary kept clean:** `manage_ui` owns RectTransform anchor/pivot math and
  UI-aware creation only; element *property* edits (Image color, Button colors,
  Text font) stay in `manage_components` — no reimplementation.

### Added (asset-editing tools — Connector v0.0.14)

- **Four new `[HeraTool]`s that fill the prefab / material / shader gap.** All
  are stateless one-shot HTTP calls and were verified end-to-end against a live
  Unity 6 (URP) editor:
  - **`describe_shader`** — inspect a shader's properties (name, type, display
    label, range) or search shader names (`--list`). Read-only; pairs with
    `manage_material`. Missing-shader lookups suggest similar names via
    `Core/Levenshtein`.
  - **`manage_material`** — material asset CRUD: `create` (with a shader),
    `get`, `set` (one shader property), `set_shader`. Values reuse the
    `manage_components` forms (`1,0,0,1`/`#hex` colors, numbers, `x,y,z,w`
    vectors, asset-path/InstanceID textures) via `Core/SerializedPropertyValue`.
  - **`manage_prefab`** — `create` (scene GameObject → prefab asset),
    `instantiate`, and headless `add_component` / `remove_component` via
    `PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset` →
    `UnloadPrefabContents` (no PrefabStage, no scene side effects).
  - **`manage_asset_import`** — `get` / `set` an asset's import settings through
    its `AssetImporter` (`TextureImporter`, `ModelImporter`, …) by raw
    SerializedProperty path, then `SaveAndReimport`. Same SerializedObject
    pattern as `manage_components`, applied to the importer.
- `Core/SerializedPropertyValue.TryParseColor` / `TryParseFloats` are now
  `public` so value-typed tools (manage_material) can reuse the exact parse
  forms instead of re-implementing them.

### Changed (optimisation)

- **`unity_docs` response shrunk + miss-path scan made ~30× cheaper —
  Connector bumps to v0.0.12.** Two independent wins on top of the
  v0.0.10/0.0.11 RAG refactor:
  - **Minimal response shape.** `query`, `query_normalized`,
    `manual_url`, `scriptreference_url`, and `unity_version` are
    dropped from the happy-path reply; what remains is
    `{ title, signature, summary }`. ~360 B → ~150 B over the wire,
    ~90 → ~30 AI input tokens (66% reduction). The full row is still
    in the in-memory dict if a follow-up tool ever needs it.
  - **`SuggestSimilar` near-O(n/26) on typical misses.** The
    `DOC_NOT_FOUND` Levenshtein path now layers three cheap
    pre-filters: a lazy prefix bucket (keys grouped by lowercase
    first letter — typo misses scan ~1/26 of the corpus), a
    length-difference filter (lower bound on edit distance), and the
    new `Levenshtein.DistanceBounded` (bails out the moment a DP row
    min exceeds the budget). First-character-typo queries fall back
    to a full scan so obvious suggestions don't vanish. Smoke-measured
    miss latency: ~290 ms → ~10 ms (single-digit ms in cache-warm
    cases).

### Changed

- **`unity_docs` reworked to ship pre-parsed data inside the UPM
  package — Connector bumps to v0.0.10.** Replaces the per-call HTML
  parser introduced in the previous Unreleased entry. The 31,581
  ScriptReference pages are now converted once by
  `tools/build-unity-docs/main.go` into a single gzipped JSONL
  artefact (`AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes`,
  ~1.2 MiB on disk after gzip) that lives inside the UPM connector
  package itself. Installing the connector is the only prerequisite —
  the user no longer has to host a `Documentation/en/` folder, set an
  env var, or run `asset-config unity-docs`. First call per domain
  triggers a one-time GZip decompress + JSONL parse into an in-memory
  Dictionary<string, Entry>; every subsequent lookup is an O(1) dict
  hit.

- **Replaces** the previous Unreleased work that read HTML per call:
  - `AgentConnector/Editor/Core/UnityDocsParser.cs` (regex + LRU) and
    `UnityDocsIndex.cs` (filesystem-scan index) are **removed**. Their
    behaviour is now split across `tools/build-unity-docs/main.go`
    (regex extraction, build-time only) and the new
    `Core/UnityDocsStore.cs` (gzipped JSONL load + dict lookup +
    Levenshtein suggestions over the in-memory key set).
  - The CLI-side docs-root resolution stack in `cmd/unity_docs.go`
    collapses to a one-line passthrough — the `--docs-path` /
    `HERA_AGENT_UNITY_DOCS` / `asset-config unity_docs_path` /
    `DetectUnityDocsPath` chain is **gone**. The `unity_docs_path`
    field on `AssetConfig` and the `asset-config unity-docs`
    sub-command are removed; `internal/assetconfig` reverts to its
    pre-v0.0.9 surface.
  - `UnityDocs.cs` (the `[HeraTool]` itself) loses its `docs_root`
    parameter and gains a `DOCS_BUNDLE_UNAVAILABLE` error for the
    case where the bundled data file is missing on a broken install.

- **`tools/build-unity-docs/main.go`** — Go script that scans
  `Documentation/en/ScriptReference/*.html`, applies the same regex
  set the connector used to evaluate per call (h1 / signature-CS /
  Description / switch-link / Unity version), and emits sorted JSONL.
  Detects `.gz` in the output path and wraps the writer in
  `gzip.NewWriter` automatically. One-shot maintainer tool; the
  resulting artefact is committed.

- **`Core/Levenshtein.cs`** stays — `UnityDocsStore.SuggestSimilar`
  is the third consumer of the shared helper.

### Changed (docs)

### Changed (docs)

- **AGENT.md §4.15 — PowerShell `--params` JSON quoting trap.** §4.13
  already covers PowerShell `exec` snippet quoting, but the same
  failure mode also catches `--params '{...}'` payloads — bash-style
  `\"` escapes survive into JSON as literal backslashes and Go's
  `json.Unmarshal` errors out with `invalid character '\\'`. Document
  the single-quoted outer / raw-`"` inside pattern as the safe form,
  plus the alternative of letting the scalar flags
  (`--property` + `--value 0,1,0`) carry simple Vector / Color
  values without `--params` at all. Bumps the AGENT.md surface that
  `doctor --agent-rules` embeds — pick this CLI release up via
  `hera-agent-unity update` to get the new section into your
  AGENTS.md / CLAUDE.md / cursor rules.

### Added

- **`manage_components` tool**
  (`AgentConnector/Editor/Tools/ManageComponents.cs`) — Component CRUD
  on a target GameObject with five sub-actions: `add`, `remove`, `list`,
  `get`, `set`. Targets a component by `--component_id` (preferred,
  survives renames + multi-instance disambiguation) or by GameObject
  (`--instance_id` / `--path`) plus `--type` and optional `--index`.
  Property paths are raw `SerializedProperty` paths (`m_Name`,
  `m_LocalScale.x`, `m_Materials.Array.data[0]`) — no friendly-name
  mapping; the user writes what Unity serialises. `get` omits
  `--property` to dump every visible top-level property of the
  component; `set` re-reads after `ApplyModifiedProperties` so the
  returned value reflects whatever Unity actually accepted (clamps,
  normalisation, enum-bit canonicalisation). `PROPERTY_NOT_FOUND`
  errors include the list of top-level property names that *do* exist
  on the target — pipe that into the next `set` call.
  - Fourth entry of the post-v0.0.6 capability queue
    (vault `capability-gaps-priorities-final.md` §5-2). Establishes
    the property-set pattern reused by every future `manage_*`
    (material / animation / vfx / scriptable objects / prefab
    properties).
  - Connector bumps to **v0.0.8**.

- **`Core/SerializedPropertyValue.cs`** — JSON ↔ `SerializedProperty`
  bridge. `Read` returns a JSON-friendly shape per
  `SerializedPropertyType`; `Apply` coerces a `JToken` back into the
  matching typed setter. Supported types: Integer / ArraySize /
  LayerMask / Boolean / Float / String / Character / Color / Vector2 /
  Vector3 / Vector4 / Vector2Int / Vector3Int / Quaternion / Rect /
  Bounds / Enum / ObjectReference. `ResolveReference` decodes
  ObjectReference targets from an InstanceID integer, an asset path
  string, or a `{instance_id | asset_path}` envelope — the same
  resolution path future `manage_material` and friends will reuse.

- **`Core/ComponentTypeResolver.cs`** — extracted from
  `find_gameobjects.ResolveComponentType` so `manage_components` and
  any future tool that needs a component type-name lookup share the
  `TypeCache.GetTypesDerivedFrom<Component>()` scan. `SuggestSimilar`
  adds a Levenshtein "did you mean" surface for `UNKNOWN_COMPONENT_TYPE`
  errors. `find_gameobjects` now calls the helper and emits the
  `did_you_mean` hint on its own type lookups too.

### Changed (docs)

- **README × 2 sync with current code.** Reorganised the Commands table
  from a 24-row flat list into seven categories (Editor & runtime /
  Scene & GameObjects / Packages / Console-tests-capture / Introspection
  / Workflow / Status & maintenance) so the three newer tools land in an
  obvious slot. Replaced batch examples whose `manage_editor` `wait` /
  `refresh+compile` actions and `read_console` command name never
  existed in shipped code (same pattern already corrected in the
  v0.0.6 `batch --help`). Generalised the "PlayMode test polling"
  bullet to cover the `manage_packages` async-job + `[InitializeOnLoad]`
  resume path on the same plumbing. Dropped the "What's New in v2 —
  Unified" section, the matching `hera-agent` / `hera-agent-pro` FAQ
  migration note, and the corresponding hero subtitle — the merge is
  ~a year old and the migration audience is effectively gone (vault
  §8-2 "Free/Pro 잔존 흔적 일소").

- **AGENT.md `--depth` default corrected to match code.** Rule 2 and
  §5.5 both said the default was `1`; `ExecuteCsharp.cs` ships
  `DefaultSerializeDepth = 3`. Updated both passages so agents reading
  the rules get the actual default, and reframed the "lean down" advice
  from "raise to 3 only when..." to "drop to 1 or 2 when you want the
  shallowest payload."

### Added

- **`find_gameobjects` tool** (`AgentConnector/Editor/Tools/FindGameObjects.cs`)
  — search loaded-scene GameObjects with filters that combine via AND
  (`name` substring case-insensitive, exact `tag`, `layer` by name or
  index, `component` short or fully-qualified type name resolved through
  `TypeCache`, and `path_glob` with `*`/`**`/`?` glob semantics) plus
  built-in pagination (`limit` defaults to 50, `offset` defaults to 0,
  `has_more` echoed back so callers know when to stop). Results are
  sorted by hierarchy path so pagination is stable across calls.
  Strips prefab assets and `HideFlags.HideInHierarchy` objects so only
  what a user would see in the Hierarchy window is returned. Shallow
  return per entry: `{ instance_id, name, path, scene, active }` —
  same `instance_id` shape `manage_gameobject` accepts as input, so
  filter-then-edit workflows feed straight through.
  - Third entry of the post-v0.0.6 capability queue (vault
    `capability-gaps-priorities-final.md` §5-3).
  - Connector bumps to **v0.0.7**.

- **`Core/HierarchyPath.Build(Transform)` helper** — extracted from
  `ManageGameObject.GetHierarchyPath` now that `FindGameObjects` is a
  second consumer. Keeps the `/Root/Child/Name` path format consistent
  across every tool that returns a GameObject shallow shape.

- **`manage_packages` tool** (`AgentConnector/Editor/Tools/ManagePackages.cs`
  + `AgentConnector/Editor/Core/PackageJobState.cs`) — drives
  `UnityEditor.PackageManager.Client` so AI agents can install / remove /
  embed packages without hand-editing `Packages/manifest.json` (which
  races the resolver and skips git-URL validation). Four sub-actions:
  `list` (synchronous, returns the full resolved package set), `add`,
  `remove`, `embed` (each async — returns `{ job_id, port, action,
  identifier }` immediately and writes the final result to
  `~/.hera-agent-unity/status/package-result-<port>-<job_id>.json` up
  to 10 minutes later). `add` accepts every `Client.Add` identifier
  form: `com.x.y`, `com.x.y@1.2.3`, git URLs (with optional `?path=`
  subdir), and `file:..` local paths.
  - **Domain-reload safe.** Package installs almost always trigger a
    resolver-driven domain reload that destroys the in-flight `Request`
    handle. `PackageJobState` registers an `[InitializeOnLoad]` hook
    that, after the reload settles, scans pending-job files and runs a
    fresh `Client.List` to infer success (identifier present, or absent
    for `remove`) before writing the result file the CLI is polling.
  - **CLI poller** (`cmd/manage_packages.go`) mirrors
    `cmd/test.go`'s PlayMode pattern: extract `job_id` from the start
    envelope, poll the result file every 500ms, check Unity PID
    liveness every 5s, fail after 10 minutes.
  - Second entry of the post-v0.0.6 capability queue
    (vault `capability-gaps-priorities-final.md` §5-4). OpenUPM scoped-
    registry handling is intentionally out of scope for this entry —
    document the manual scoped-registry registration as a precondition.
  - Connector bumps to **v0.0.6**.

- **`manage_gameobject` tool** (`AgentConnector/Editor/Tools/ManageGameObject.cs`)
  — GameObject CRUD with seven sub-actions: `create`, `destroy`, `move`,
  `set_parent`, `set_active`, `set_name`, `get_transform`. Target by
  `instance_id` (preferred — survives renames and duplicates) or hierarchy
  `path` (with a fallback walk that reaches inactive subtrees
  `GameObject.Find` skips). `create` supports an optional `--primitive`
  (cube / sphere / capsule / cylinder / plane / quad) and optional initial
  `--parent` / `--position`. Every action registers an `Undo` entry and
  marks the scene dirty, and every action returns the same depth-1 shape
  `{ instance_id, name, path, scene, scene_path, active, transform:{position,
  rotation, scale} }`. First entry of the post-v0.0.6 capability queue
  (vault `capability-gaps-priorities-final.md` §5-1).

### Changed

- **`editor play --wait` confirmation moved from C# to Go.** Play-mode
  entry triggers a domain reload that stops the HTTP listener
  mid-response, so the previous `ManageEditor.HandleCommand`'s
  `await WaitForPlayModeStateAsync(EnteredPlayMode)` path could never
  write a reply. The handler now returns synchronously the moment
  `EditorApplication.isPlaying = true` is set, and `cmd/editor.go`
  polls the heartbeat file via the new `waitForState(resolve,
  timeoutMs, "playing", "paused")` helper in `cmd/status.go` for the
  60-second confirmation window. Same pattern as PlayMode test result
  polling — file-bus uncouples confirmation from HTTP liveness.
  - **Wire change**: `wait_for_completion` is no longer sent over
    HTTP. Old CLI ↔ new Connector silently no-ops (`--wait` doesn't
    block); new CLI ↔ old Connector also no-ops (old C# wouldn't have
    written the response anyway). Bump both sides together.
  - `stop --wait` is intentionally not supported — `editor stop` is
    fire-and-forget.

### Added

- **`waitForState(resolve, timeoutMs, targets...)`** in `cmd/status.go`
  — generic heartbeat-state poller used by `editor play --wait`. Uses
  `statusPollInterval`, narration-aware.

### Removed

- **`ManageEditor.WaitForPlayModeStateAsync` + `PlayModeTimeoutSeconds`
  constant + `WaitForCompletion` parameter.** Dead after confirmation
  moved Go-side. `HandleCommand` reverted to synchronous `object`
  signature (no `async Task`).

### Added (templates)

- **`.github/PULL_REQUEST_TEMPLATE.md` and Korean companion.**
  Pre-merge regression checklist covering scope, version bump policy,
  automated verification (`go build` / `vet` / `test` /
  `golangci-lint` / `gofmt`), manual Unity-Editor integration checks,
  and CLAUDE.md "Hard Constraints" review. English template is
  GitHub's default auto-fill; `PULL_REQUEST_TEMPLATE.ko.md` sits
  alongside as a copy-paste reference for Korean PRs.

> Both the CLI binary and the UPM connector change in this entry.
> Connector bumps to **v0.0.4** (ManageEditor.cs). CLI tag will follow.

## [0.0.6] - 2026-05-27

### Fixed

- **`batch` help example used a non-existent action.** The
  `hera-agent-unity batch --help` text showed
  `{"command":"manage_editor","params":{"action":"refresh"}}` — but
  `manage_editor` only accepts play/stop/pause/set_active_tool/
  add_tag/remove_tag/add_layer/remove_layer, so users who copy-pasted
  the example hit `UNKNOWN_ACTION`. Swapped to the working
  `refresh_unity` / `compile:"request"` form.

- **`cmd/test.go` branched on a message string for the
  Test-Framework-missing case.** Now branches on `resp.Code ==
  "UNKNOWN_COMMAND"` to honor AGENT.md Rule 3 (code is stable; message
  is not). Drops the unused `strings` import. `CommandRouter`
  already emits `UNKNOWN_COMMAND` for this path, so it is a strict
  upgrade with no behaviour change.

### Changed

- **`humanCategories` AGENT.md doc no longer lists `upgrade`.** The
  word was never in `cmd/root.go`'s actual whitelist — invoking
  `hera-agent-unity upgrade` already returns `UNKNOWN_COMMAND` with
  `did_you_mean=["update"]`. Both `AGENT.md` and the embedded
  `cmd/AGENT.md` are aligned with the real surface.

### Removed

- **Dead `SetAssetInstalled` doc comment** in
  `internal/assetconfig/config.go` (function was removed in an
  earlier refactor; its orphan comment was sitting above
  `GetEnabledAssets` and misreading as its second doc line).
- **Dead `keyMap.FullHelp` / `ShortHelp` methods** in
  `internal/tui/assetconfig.go` — they satisfied
  `bubbles/help.KeyMap` but the asset-config TUI never imports
  `bubbles/help`, never constructs a `help.Model`, and never
  renders help in `View()`. Zero callers across the repo.

> UPM connector stays at v0.0.3 for this release — no C# changes.

## [0.0.5] - 2026-05-27

### Fixed

- **Go error wrapping switched from `%v` to `%w`.** Eight `fmt.Errorf`
  calls across `internal/client` and `cmd` now use `%w` so callers can
  unwrap with `errors.Is` / `errors.As`. This enables programmatic
  detection of `context.DeadlineExceeded`, `net.ErrClosed`, and other
  wrapped errors without string matching.

- **C# `ExecCompileCache` now disposes `SHA256` instances.** Both
  `ComputeKey` and `HashStrings` previously abandoned `SHA256.Create()`
  without disposal, creating finalizer pressure during repeated `exec`
  invocations. Each call site now wraps the instance in `using var`.

> UPM connector stays at v0.0.3 for this release. The C# fix above is
> committed to `main` but the package version will be bumped separately.

## [0.0.4] - 2026-05-27

### Added

- **AGENT.md Pitfall §4.13: PowerShell `exec` quoting.** Adds the
  missing rule that bit a Cursor / Claude Code session on Windows —
  PowerShell single quotes don't interpret backslash escapes, double
  quotes interpret `$` / backtick / `;`, and agents that spawn a fresh
  process per command lose `$code = @'...'@` between calls. The section
  documents three patterns that always work (stdin pipe + here-string,
  single-quoted strings without `\"` escapes, `exec --file` from disk)
  and the matching anti-patterns. `cmd/AGENT.md` is re-synced so
  `doctor --agent-rules` emits the new pitfall alongside the existing
  twelve.

> UPM connector unchanged in this release — still v0.0.3. Only the CLI
> binary is rebuilt so the embedded AGENT.md picks up the new pitfall.

## [0.0.3] - 2026-05-27

### Fixed

- **Console `--stacktrace user` filter widened.** Previously only five
  frame patterns were dropped (`UnityEngine.Debug:`, `EditorGUIUtility:`,
  `Unity.Entities.SystemState:`, `(at Library/`, `(at ./Library/`).
  Real-world exception traces leaked the synthetic exec wrapper
  (`__CliDynamic:Execute`), the hera-agent dispatcher itself
  (`HeraAgent.CommandRouter:*`, `HeraAgent.HttpServer:*`), reflection
  machinery (`System.Reflection.MethodBase:Invoke`,
  `System.Runtime.CompilerServices.AsyncTaskMethodBuilder…`), and the
  editor's update pump (`EditorApplication:Internal_CallUpdateFunctions`).
  All seven families now drop in `user` mode; `full` still returns
  everything verbatim.

### Added

- **`list --tool <name>` now includes an `examples` field.** The
  `HeraToolAttribute.Examples` / `ExampleDescriptions` properties
  restored in v0.0.2 were stored on the attribute but never surfaced
  in the schema response. `ToolDiscovery.GetToolSchema()` now zips
  the two arrays index-wise and emits a `[{call, description}, ...]`
  list. The slim `list` (no `--tool`) payload is intentionally
  unchanged — examples are deep-dive material.
- **`exec` schema now advertises `compile_only`, `stacktrace`, and
  `strict`.** These three flags were already wired end-to-end (v0.0.1
  ExecuteCsharp.HandleCommand reads them via `p.GetBool` / `p.Get`),
  but the `Parameters` nested class only declared the v0.0.1 base
  set. Schema-driven consumers (`list --tool exec`) couldn't see
  them. Three `[ToolParameter]` declarations added so the schema
  matches the actual surface.

## [0.0.2] - 2026-05-27

### Fixed

- **UPM package failed to compile** because the merged `HeraToolAttribute`
  was missing `Examples` and `ExampleDescriptions` properties that
  `DescribeType`, `FindMethod`, and `ListAssemblies` reference on their
  `[HeraTool(... Examples = new[] { ... })]` declarations. Adding the two
  properties back to the attribute (carried over from Pro) restores a
  clean Unity import. `ToolDiscovery` does not yet surface the examples
  in tool schemas — that is a future enhancement; v0.0.2 only restores
  the compile path so the CLI side of v0.0.1 becomes actually usable.

## [0.0.1] - 2026-05-27

Initial release of the unified `hera-agent-unity` — successor to
`hera-agent` (free lite) and `hera-agent-pro` (commercial). All
former Pro features ship free under MIT.

### Added

- Single Go CLI + C# UPM connector bridging Unity Editor over localhost HTTP.
- Built-in tools: `editor`, `exec`, `log`, `scene`, `console`, `test`,
  `menu`, `screenshot`, `profiler`, `reserialize`, `describe_type`,
  `find_method`, `list_assemblies`.
- Auto-discovery via heartbeat files under `~/.hera-agent-unity/instances/`.
- Batch execution (`batch`) for atomic multi-step workflows.
- `[HeraTool]` attribute-based custom tool registration with reflection scan.
- Unity pitfalls catalog surfaced through `describe_type`.
- Asset Config (`asset-config`) TUI + UPM editor window, sharing
  `~/.hera-agent-unity/asset-config.json`.
- Self-install (`install`), self-update (`update`), self-uninstall (`uninstall`),
  and self-diagnostic (`doctor`) commands.
- Cross-platform binaries (Linux, macOS, Windows × amd64/arm64).
