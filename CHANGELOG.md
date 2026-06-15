# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
