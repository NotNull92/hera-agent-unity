# Unity CLI and Pipeline Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` to implement this plan task-by-task. Subagents
> are intentionally not used in this workspace. Steps use checkbox syntax for
> tracking.

**Goal:** Close the verified, architecture-compatible Unity Pipeline gaps,
repair confirmed Hera defects, and finish a zero-unread full-product audit.

**Architecture:** Preserve the Go CLI -> loopback HTTP -> serialized Unity main
thread -> reflection-discovered C# tool architecture. Extend existing tools
where the concept already exists; add a new tool only for Timeline or Project
Auditor when no existing owner is truthful. Optional Unity packages are
reflection-only and fail closed.

**Tech Stack:** Go 1.25+, C# Unity Editor package, Newtonsoft JSON, NUnit Unity
Test Framework, PowerShell compatibility-matrix scripts.

## Global constraints

- Work only in `codex/unity-pipeline-parity`; do not commit unless requested.
- Use the official Pipeline source only as an independently re-derived behavior
  inventory. Do not copy source, prose, or implementation structure.
- No new package dependencies.
- Tests precede production changes and must fail for the intended reason.
- Every new `.cs` file has a unique `.meta` sibling.
- Keep `Object`, `PackageInfo`, `Random`, and `Debug` unambiguous.
- Preserve stable error codes, bounded output, approval policy, Undo/dirty/save
  semantics, operation-ledger retry safety, and domain-reload behavior.
- Project Auditor is omitted unless a positive rules-enabled fixture is
  available during Task 9.

---

### Task 1: Windows-clean generated guides

**Files:**

- Modify: `.gitattributes`
- Test: `tools/sync-agent-guides/main_test.go`

**Produces:** clean-checkout LF semantics for generated `.mdc` mirrors.

- [x] Run `go test ./tools/sync-agent-guides -run TestGeneratedGuidesMatchCanonicalSource -count=1` and observe `.cursor/rules/hera-agent-unity.mdc` drift.
- [x] Add `.gitattributes text eol=lf` and `*.mdc text eol=lf`.
- [x] Re-run the focused test and `go test ./...`; both pass.

### Task 2: Full inventory, coverage ledger, and parity matrix

**Files:**

- Create: `docs/UNITY_PIPELINE_PARITY_AUDIT.md`
- Create: `docs/UNITY_PIPELINE_PARITY_MATRIX.md`
- Read: every in-scope file identified by `review-hera-agent-unity`

**Produces:** zero-unread ledger plus a 153-row decision record.

- [ ] Enumerate current Go, C#, test, metadata, installer, workflow, help, and
  contract files from Git into the audit document; record the checkout SHA.
- [ ] Read every production and test file completely, adding purpose, risk
  lane, callers/consumers, tests/docs, and open questions to the ledger.
- [ ] Enumerate every Go command, `[HeraTool]`, `[HeraAction]`, Core utility,
  persistent file format, and response producer/consumer; require zero
  untraced entries.
- [ ] Apply structural, adversarial, and omission passes and record only
  evidence-backed findings with tight paths and concrete triggers.
- [ ] Add one row per official public Pipeline command with classification,
  current Hera equivalent, decision, and validation evidence.
- [ ] Run `git diff --check` and manually verify both documents contain no
  placeholder or unresolved ledger row.

### Task 3: Build options reach BuildPipeline

**Files:**

- Modify: `AgentConnector/Editor/Tools/Build.cs`
- Create: `AgentConnector/Editor/Tests/BuildToolTests.cs`
- Create: `AgentConnector/Editor/Tests/BuildToolTests.cs.meta`

**Produces:** `BuildOptions` derived from the three stored build settings.

- [ ] Write an EditMode test that sets distinct `EditorUserBuildSettings`
  values and asserts the option builder returns exactly `Development`,
  `AllowDebugging`, and `BuildScriptsOnly` when enabled.
- [ ] Run the focused package test and confirm it fails because no option
  builder/behavior exists.
- [ ] Add the smallest internal option builder and pass its result to
  `BuildPlayerOptions.options`; do not change target selection.
- [ ] Run the focused test and the complete `HeraAgent.Editor.Tests` suite.
- [ ] Live-run a disposable development build preflight/status path without
  publishing the build and verify reported settings match the actual options.

### Task 4: MCP test-task cancellation

**Files:**

- Modify: `internal/mcpserver/tasks.go`
- Modify: `internal/mcpserver/runtime.go` only if the existing sender is not
  already reachable from the task handler
- Modify: `internal/mcpserver/tasks_test.go`

**Consumes:** the existing `run_tests` action `cancel` and native runtime
sender. **Produces:** MCP test cancellation with package cancellation unchanged.

- [ ] Add a Go test whose fake connector sender records one `run_tests`
  cancellation for a decoded test task ID and asserts the MCP result is
  cancelled.
- [ ] Add a separate test proving a package task still returns
  `supported:false` without sending a Unity command.
- [ ] Run the focused tests and confirm the test-task case fails at the stale
  unsupported branch.
- [ ] Route only test tasks to `run_tests cancel` with the original port and
  run ID; preserve task ID validation and connector error propagation.
- [ ] Run `go test ./internal/mcpserver ./internal/taskbridge` and an actual
  MCP Tasks cancellation scenario against a disposable long-running test.

### Task 5: Console default window is newest-first bounded data

**Files:**

- Modify: `AgentConnector/Editor/Tools/ReadConsole.cs`
- Modify: `AgentConnector/Editor/Tests/ReadConsoleTests.cs`

**Produces:** newest default window while preserving `since` forward paging.

- [ ] Add a test with more entries than `lines` that invokes console without
  `since` and expects only the newest bounded entries in chronological order.
- [ ] Run it and confirm the current forward scan returns the oldest entries.
- [ ] Calculate the initial console index as `max(0, count-lines)` only when
  `since` is absent; retain cursor paging and all caps.
- [ ] Run focused tests, then live-create bounded diagnostic logs and call the
  installed `console --lines 2` surface to prove the final two are returned.

### Task 6: Scene and GameObject parity

**Files:**

- Modify: `AgentConnector/Editor/Tools/ManageScene.cs`
- Modify: `AgentConnector/Editor/Tools/ManageGameObject.cs`
- Create: `AgentConnector/Editor/Tests/SceneGameObjectToolTests.cs`
- Create: `AgentConnector/Editor/Tests/SceneGameObjectToolTests.cs.meta`
- Modify: `cmd/help/scene.txt`
- Modify: `cmd/help/manage_gameobject.txt`

**Produces:** scene `create`, `set_active`, `save_all`; GameObject
`set_transform`, `set_tag`, and `set_layer`.

- [ ] Add contract and behavior tests for additive/single scene creation,
  loaded-scene-only activation, save-all dirty scenes, world/local transform
  fields, invalid tag/layer, inactive durable targets, Undo, and scene dirtying.
- [ ] Run focused tests and observe missing-action failures.
- [ ] Implement scene actions with `EditorSceneManager` and stable
  `SCENE_*` errors; never overwrite an existing path without approval.
- [ ] Implement one transform action with optional position, Euler rotation,
  scale, and local/world space; reject an empty mutation.
- [ ] Implement tag and layer using Unity's existing registries and stable
  invalid-value errors; do not auto-create registry entries.
- [ ] Run package tests and use the installed CLI on a disposable unsaved
  scene, then clean it without touching user scenes.

### Task 7: Animation and Timeline parity

**Files:**

- Modify: `AgentConnector/Editor/Tools/ManageAnimation.cs`
- Create: `AgentConnector/Editor/Tools/ManageTimeline.cs`
- Create: `AgentConnector/Editor/Tools/ManageTimeline.cs.meta`
- Create: `AgentConnector/Editor/Tests/AnimationTimelineToolTests.cs`
- Create: `AgentConnector/Editor/Tests/AnimationTimelineToolTests.cs.meta`
- Modify: `cmd/help/general.txt`
- Modify: `docs/COMMANDS.md`

**Produces:** Animator layer addition, curve removal, and reflection-only
Timeline create/get/add-track/add-clip.

- [ ] Add Animator tests for duplicate layer names and deleting one exact
  curve binding; add Timeline tests that skip only when Timeline is absent and
  otherwise exercise real assets.
- [ ] Run focused tests and observe missing-action/tool failures.
- [ ] Add layer and remove-curve actions using the existing controller/clip
  target resolution and asset-save conventions.
- [ ] Add a focused Timeline tool that resolves public Timeline types by name,
  validates track/clip types, and returns `PACKAGE_NOT_INSTALLED` when absent.
- [ ] Keep results bounded to asset path, duration, and track/clip identity;
  do not serialize Timeline objects.
- [ ] Run package tests on all three Unity buckets and live-clean all temporary
  Timeline assets.

### Task 8: Settings, shader inventory, and Editor focus

**Files:**

- Modify: `AgentConnector/Editor/Tools/ManageSettings.cs` or split focused
  partials if its measured pure LOC would otherwise grow further
- Modify: `AgentConnector/Editor/Tools/DescribeShader.cs`
- Modify: `AgentConnector/Editor/Tools/ManageEditor.cs`
- Create: `AgentConnector/Editor/Tests/PipelineSettingsToolTests.cs`
- Create: `AgentConnector/Editor/Tests/PipelineSettingsToolTests.cs.meta`
- Modify: `cmd/help/manage_settings.txt`
- Modify: `cmd/help/editor.txt`
- Modify: `docs/COMMANDS.md`

**Produces:** graphics/input/lighting/navigation settings, bounded shader
listing, and Editor-window focus.

- [ ] Characterize the exact serialized project settings paths and public
  APIs on all three Unity buckets before naming request fields.
- [ ] Add failing tests for get/set round trips, dry-run no-mutation behavior,
  approval metadata, invalid axes/settings, bounded shader filtering, and
  missing Editor window type.
- [ ] Implement each settings area through existing `SerializedObject` and
  settings response conventions; report recompile/reload truthfully.
- [ ] Add `describe_shader list` with case-insensitive filter and hard limit;
  return names only by default.
- [ ] Add `manage_editor focus` for an exact loaded `EditorWindow` type or
  title, without claiming physical OS focus/click verification.
- [ ] Run focused tests and live round trips that restore every original
  project setting in `finally`.

### Task 9: Editor UI capture and conditional Project Auditor

**Files:**

- Modify or create a focused partial beside the existing screenshot tool after
  measuring its current responsibilities
- Create matching `.meta` and EditMode tests for any new C# files
- Conditionally create: `AgentConnector/Editor/Tools/ProjectAudit.cs` and
  `.meta`
- Modify: `cmd/help/screenshot.txt`
- Modify: `docs/COMMANDS.md`

**Produces:** bounded metadata capture for Editor UI; Project Auditor only
with positive fixture evidence.

- [ ] Inspect supported UI Toolkit trees in all three fixtures and define a
  bounded response containing window, type/name, hierarchy path, visibility,
  enabled state, and layout rectangle.
- [ ] Add a failing test over a real test EditorWindow and implement the
  smallest metadata-only capture action; no runtime/player server.
- [ ] Probe a rules-enabled Project Auditor fixture. If none exists, record the
  conditional exclusion in the parity matrix and do not add production code.
- [ ] If a positive fixture exists, add failing success/status tests and a
  reflection-only, bounded `project_audit` tool; otherwise mark this step
  complete with runtime evidence of the missing prerequisite.

### Task 10: Proven orphan and duplicate cleanup

**Files:**

- Modify only files named by evidence in `docs/UNITY_PIPELINE_PARITY_AUDIT.md`
- Modify directly related tests

**Produces:** removal of proven dead or duplicate code with no surface change.

- [ ] Search definitions/references with `rg`, `sg`, Go compiler/vet/lint, and
  reflection registration rules after the feature work lands.
- [ ] For every candidate, name the behavior-preserving test that fails if the
  wrong implementation is removed; add the red characterization first where
  needed.
- [ ] Delete only zero-consumer helpers, unreachable branches, stale contract
  copies, or duplicate parsers whose shared replacement is already present.
- [ ] Re-run the narrow test after each deletion and restore immediately on a
  behavioral difference.
- [ ] Repeat the structural, adversarial, and omission passes; record why
  intentional boundary duplication remains.

### Task 11: Contract, docs, version, and final gates

**Files:**

- Modify: `AgentConnector/package.json`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Modify: `README.ko.md`
- Modify: `docs/COMMANDS.md`
- Modify: `docs/handoffs/ACTIVE.md`
- Modify: `docs/metrics/catalog-payload-baseline.json`
- Modify generated agent guides through `go run ./tools/sync-agent-guides`
- Modify other help files identified by the final catalog diff

**Produces:** synchronized public contract and verified connector `0.0.109`.

- [ ] Capture the final live `list` catalog and validate it before updating the
  payload baseline; account for every new action/tool.
- [ ] Update command/help/README/Korean README/design/handoff claims from the
  actual final schemas and responses, not from this plan.
- [ ] Bump only the connector package to `0.0.109`; do not invent a CLI tag.
- [ ] Run `go run ./tools/sync-agent-guides` and verify check mode is clean.
- [ ] Run CS0104 scans and pure-LOC measurement on every changed C# and Go
  source; split touched responsibilities that exceed the accepted ceiling.
- [ ] Run the Go gauntlet, connector validator, exact-source compile, package
  tests, and live matrix defined in the design.
- [ ] Re-read this plan and the user's eight requirements, confirm the audit
  ledger has zero unread/untraced rows, and report any conditional exclusion
  without calling it implemented.
