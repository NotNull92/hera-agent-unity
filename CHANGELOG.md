# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`unity_docs` tool** (`AgentConnector/Editor/Tools/UnityDocs.cs`) —
  offline Unity ScriptReference lookup. Reads the local
  `Documentation/en/ScriptReference/*.html` tree the user has on disk
  and returns a slim shape `{ title, signature, summary, manual_url,
  scriptreference_url, unity_version }` — typically 250-400 bytes per
  call — so an AI agent can verify an API exists at this Unity version
  before piping it through `exec`. Query → filename mapping covers
  classes (`Rigidbody`), methods (`Rigidbody.AddForce`), properties
  (`Rigidbody.mass` → `-mass.html`), and qualified names
  (`UnityEditor.AssetDatabase.Refresh` strips the `UnityEditor.`
  prefix). Misses return `DOC_NOT_FOUND` with `did_you_mean[]` from a
  Levenshtein scan of the 31k-file index.
  - **No network dependency.** The original §5-5 spec assumed
    `docs.unity3d.com` fetch + 30-day disk TTL; the user pointed at the
    offline `Documentation/en` directory they already had, which
    collapses caching to a single in-memory LRU (32 entries) over
    parsed shapes. The HTML files themselves are the canonical store.
  - **No HtmlAgilityPack dependency.** Unity docs HTML uses stable
    structural attributes (`signature-CS sig-block`, `h3 Description`,
    `switch-link` anchor, `h1.heading inherit`) that hold up under
    plain compiled regex.
  - **CLI-side docs-root resolution.** `cmd/unity_docs.go` picks the
    docs root via `--docs-path` flag → `HERA_AGENT_UNITY_DOCS` env →
    `asset-config unity_docs_path` → `assetconfig.DetectUnityDocsPath`
    probe, then forwards the absolute path. The connector stays
    filesystem-environment-agnostic.
  - Fifth and final entry of the post-v0.0.6 capability queue
    (vault `capability-gaps-priorities-final.md` §5-5). The 5-item
    queue established at 2026-05-28 is now complete.
  - Connector bumps to **v0.0.9**.

- **`Core/UnityDocsParser.cs`** — compiled-regex HTML parser +
  32-entry in-memory LRU keyed by relative filename.
  `Core/UnityDocsIndex.cs` — lazy index of ScriptReference filenames
  for Levenshtein "did you mean" suggestions, built once per
  `docs_root` per domain.

- **`Core/Levenshtein.cs`** — extracted the edit-distance helper that
  was duplicated three times (`ToolDiscovery` typo hints,
  `ComponentTypeResolver` suggestions, the new `UnityDocsIndex`).
  All three call sites now delegate.

- **`asset-config unity-docs` sub-command** — persist / show /
  autodetect the offline-docs directory. Backed by a new top-level
  `unity_docs_path` field on `AssetConfig` so the value survives
  across CLI sessions without an env var.

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
