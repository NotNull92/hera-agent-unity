# Hera vNext Capability Migration Handoff

Status: **M7 IMPLEMENTED; LIVE SUCCESS BLOCKED BY EXTERNAL UPM FAILURE**

Date: 2026-08-09

This is the active implementation handoff for evolving Hera into a higher-capability Unity agent by combining Hera's existing safety/control architecture with selected PlayMode automation, evidence, and dynamic-code ideas studied from `hatayama/unity-cli-loop`.

This document is for development continuity. It is not a release note and it must not be copied into user-facing tool descriptions or generated agent rules.

## 0. Start here

Before editing code, read:

1. `AGENTS.md`
2. `CLAUDE.md`
3. `docs/ARCHITECTURE.md`
4. This file
5. Only the relevant rows of `docs/DECISION_LEDGER.md`

Then run:

```bash
git status --short --branch
git log --oneline --decorate -10
```

Do not reset, rewrite, squash, or discard user changes.

At handoff creation time the repository state was:

```text
branch: main
HEAD: f59d12b docs: make the Hera README beginner-first
origin/main: 6c6050c docs(distribution): record v0.1.4 publication
local relation: main ahead of origin/main by 1 commit
working tree: clean
CLI release: v0.1.4
Connector package: 0.0.80
```

The local README commit `f59d12b` belongs to the user and must be preserved.

## 1. Reference implementation

Reference repository:

```text
https://github.com/hatayama/unity-cli-loop
```

Analysis baseline commit:

```text
6e5e90097eb14df242055bd3f694603d70f26227
2026-08-07T17:32:51+09:00
```

Use this exact commit as the comparison baseline unless the user explicitly asks to refresh against newer upstream code. If a local comparison checkout is unavailable, clone/fetch it into a disposable directory outside the Hera repository.

Relevant upstream areas:

```text
Packages/src/Editor/Api/McpTools/RecordInput/
Packages/src/Editor/Api/McpTools/ReplayInput/
Packages/src/Editor/Api/McpTools/SimulateKeyboard/
Packages/src/Editor/Api/McpTools/SimulateMouseInput/
Packages/src/Editor/Api/McpTools/SimulateMouseUi/
Packages/src/Editor/Api/McpTools/Screenshot/
Packages/src/Editor/Api/McpTools/Raycast/
Packages/src/Editor/Compilation/
Packages/src/Cli~/src/launch-readiness.ts
Packages/src/Cli~/src/launch-restart-guard.ts
Packages/src/Cli~/src/unity-process.ts
```

### Licensing boundary

The reference repository is MIT licensed, copyright 2025 Masamichi Hatayama.

Preferred approach: learn behavior, edge cases, tests, and architecture, then reimplement natively in Hera.

Do not copy substantial source verbatim unless there is a deliberate reason. If substantial source is copied, preserve the required MIT copyright/license notice in Hera's distribution notices. Never remove attribution obligations by mechanically rewriting identifiers.

## 2. Product goal

Target concept:

```text
existing Hera
+ strong PlayMode automation/evidence capabilities
= Hera vNext
```

This is not a repository merge and not a protocol replacement.

The goal is to improve capability density:

- more autonomous PlayMode QA,
- reproducible input sequences,
- screenshot-to-action evidence loops,
- safer dynamic code,
- optional Unity process bootstrap,
- lower command round-trip overhead,
- minimal catalog growth,
- minimal production LOC growth,
- remove existing duplication when a new third consumer makes abstraction justified.

## 3. Architecture decision

### Keep the Hera control plane

The following remain authoritative unless a new measured requirement and explicit user decision supersede them:

```text
Go CLI
  -> localhost HTTP
  -> C# Connector
  -> CommandRouter
  -> strict tool contracts
  -> safety / approval
  -> operation ID / operation ledger
  -> main-thread serialized Unity execution
```

Do **not** replace Hera HTTP with the reference TCP/JSON-RPC transport.

Reason:

- Hera CLI normally performs one Unity request per process, so connection reuse is not the dominant cost.
- Hera already has ingress limits, project-safe discovery, approval, exactly-once ledger behavior, task recovery, MCP reuse, and stable error envelopes on the HTTP path.
- Replacing the transport would recreate a large amount of already-proven infrastructure for little measured latency benefit.

### Add a capability/evidence plane inside the existing Connector

Preferred conceptual shape:

```text
Go Control Plane
  target / contracts / approval / operation ledger / tasks
                  |
                  | localhost HTTP
                  v
C# Capability Plane
  typed Unity tools
  dynamic exec
  PlayMode automation
  screenshot/evidence
```

Internal type names are not locked. Choose the smallest design justified by actual consumers.

## 4. Current Hera baseline

Measured production source baseline at handoff creation:

```text
Go production files:        102
Go production LOC:        11,639
Go test LOC:               9,609
C# production files:         108
C# production LOC:        24,050
C# test LOC:               5,907
--------------------------------
Production Go+C#:          35,689 LOC
```

Current focused areas:

```text
input-related production:       ~943 LOC
screenshot production:          ~546 LOC
exec production:              ~1,487 LOC
```

Current catalog baseline before this migration:

```text
built-in tools: 31
actions:        75
normalized catalog bytes: 185,339
```

The checked-in source of truth for the catalog payload gate is:

```text
docs/metrics/catalog-payload-baseline.json
```

Do not update that baseline merely to make a test green. A changed catalog must be reviewed as part of the milestone that caused it.

## 5. Existing Hera capabilities to reuse

### `input`

Current actions:

```text
state
inspect
click
pointer_down
pointer_up
submit
scroll
drag
```

Existing services already provide:

- hierarchy path / instance ID targeting,
- EventSystem discovery,
- EventSystem raycast stack,
- blocker diagnostics,
- Selectable interactability checks,
- CanvasGroup checks,
- click / drag / scroll / submit synthesis,
- stable typed contracts,
- `ui` and `testing` profiles.

Do not add a separate `simulate-mouse-ui` top-level tool. Extend `input`.

### `screenshot`

Current implementation already provides:

- SceneView capture,
- GameView capture,
- safe URP fallback behavior,
- output path policy,
- isolated GameObject capture,
- multi-angle isolated contact sheets.

Do not replace it with another screenshot implementation. Add evidence/annotation capabilities to the existing tool.

### `exec`

Current implementation already provides:

- source wrapping and using hoisting,
- Unity compiler/runtime discovery,
- `/shared` compiler invocation,
- compiler prewarm,
- cached reference response files,
- versioned source/reference/compiler cache keys,
- disk DLL cache,
- in-memory Assembly cache,
- collectible `AssemblyLoadContext` where available,
- compile-only path,
- structured compiler diagnostics,
- bounded result serialization,
- strict captured Unity error detection.

Do not replace this with the reference repository's full dynamic compilation subsystem unless A/B measurements prove a meaningful advantage that justifies the code and maintenance cost.

### Safety / reliability

Do not regress:

- project absolute-path identity,
- exact-first multi-Editor selection,
- fresh heartbeat ownership validation,
- idempotent-or-ledger-backed retries only,
- operation IDs and argument hashes,
- Connector operation ledger,
- approval tokens bound to exact operations,
- fail-closed capability behavior,
- compact JSON agent output,
- optional/default-off MCP sharing the same execution path.

## 6. Migration rules

1. **Reuse existing top-level tools first.** Prefer new actions/flags under `input`, `screenshot`, `editor`, or `exec` over new tools.
2. **Do not import the reference repository's generic mutation retry behavior.** Hera's operation ledger remains authoritative.
3. **Do not add raw-port identity bypass behavior.** Port is an endpoint, not identity.
4. **Keep Input System optional.** Projects without `com.unity.inputsystem` must retain existing EventSystem functionality.
5. **Do not add Node.js to Hera.** Go remains the only CLI runtime.
6. **Do not move MCP into Unity.** MCP remains optional in the Go process.
7. **Path / instance identity beats coordinates.** Use coordinates only where game input semantics require them.
8. **No speculative abstractions.** Extract shared helpers when a real third production consumer appears or duplication is otherwise measurably harmful.
9. **No copy-paste feature inflation.** Port behavior and tests into Hera-native structures.
10. **Every new mutation goes through existing safety + ledger semantics.**
11. **Every catalog change goes through the admission gate.**
12. **Every performance claim needs before/after measurement.**
13. **Do not narrate upstream provenance in production tool descriptions, agent hints, code comments, CHANGELOG, or commit messages.** Describe the Hera behavior itself.

## 7. Deliberate design restrictions

### Input System dependency

The reference implementation compiles directly against Input System types. Hera currently keeps its main Editor assembly dependency-light.

Preferred approach:

```text
Input System installed
  -> optional compile capability / version define or a small optional assembly
  -> keyboard/mouse/record/replay available

Input System absent
  -> existing EventSystem backend remains fully usable
  -> capability reports unavailable instead of failing package compilation
```

Choose the implementation that adds the least permanent dependency surface while remaining testable across the supported Unity buckets.

### Dynamic compilation size

Reference dynamic compilation code is much larger than Hera's existing exec pipeline. Analysis baseline:

```text
reference Compilation directory: ~6,044 LOC
Hera complete exec implementation: ~1,487 LOC
```

The reference shared Roslyn worker is therefore **not** an automatic migration target.

Benchmark first.

### Screenshot coordinates

The reference project exposes screenshot simulation coordinates heavily. Hera should return path/instance identity where possible and include coordinate mappings as evidence for physical game input, not make coordinates the primary identity contract.

## 8. User decisions

Both decisions were explicitly resolved by the user on 2026-08-10. Preserve
the approved scope below when their milestones are reached.

### APPROVED A: Unity launch/restart before Connector discovery

Problem:

Unity cannot execute C# Connector logic while the Editor is not running. True `editor launch` / `editor restart` therefore requires process orchestration in Go before normal Unity discovery.

This is a narrow exception to the general rule that Unity business logic belongs in the C# Connector.

Approved implementation contract:

- expose as existing `editor launch` / `editor restart`, not a new top-level tool,
- handle only process/bootstrap concerns in Go,
- derive project Unity version from project metadata / installed Editors,
- wait for the selected project's heartbeat,
- hand control back to the normal Connector path immediately after startup,
- do not add Node or `launch-unity` dependency.

Status:

```text
APPROVED: USER DECISION (2026-08-10)
```

When M7 is reached, implement this narrow Go-side exception without expanding
it into Unity business logic or adding a Node / `launch-unity` dependency.

### DECIDED B: preserve Full Access default and add opt-in Restricted mode

Current public Hera contract:

```text
exec = approved arbitrary C# with full Unity / loaded-assembly access
```

Approved vNext contract:

```text
exec default = Full Access (unchanged)
  existing arbitrary-code permission
  existing approval
  existing operation ledger

explicit opt-in Restricted mode
  source validation
  pre-load metadata validation
  post-load IL validation
```

This preserves the current public default and adds Restricted mode as defense in
depth for callers that explicitly select it.

Status:

```text
DECIDED: OPTION 2 (2026-08-10)
```

When M8 is reached, implement opt-in Restricted mode without changing existing
`exec` default semantics.

## 9. Target action design

Keep top-level tool count at 31 if practical.

Candidate `input` additions:

```text
keyboard
mouse
sequence
record    mode=start|stop|status as appropriate
replay    mode=start|stop|status
```

Avoid splitting state transitions into many catalog actions when a small enum parameter gives a cleaner stable contract.

Candidate screenshot additions:

```text
annotate = ui | raycast | all
annotation_only / elements_only = true
optional physics layer filters
```

The exact contract must be derived from current Hera conventions and strict-schema tests, not copied mechanically from another CLI.

Expected first-order catalog target:

```text
tools:   31 -> 31
actions: 75 -> roughly low 80s
```

This is a target, not a quota. Prefer fewer actions if contracts remain clear.

## 10. Performance strategy

Optimize where work is actually expensive.

Priority:

1. **Input sequence/replay inside one Unity call**
   - remove one CLI/HTTP round trip per individual input event,
   - execute frame timing in the Editor.
2. **Annotation-only evidence path**
   - when image pixels are unnecessary, avoid PNG render/encode/write.
3. **Identity-first screenshot-to-input loop**
   - prefer direct path/instance execution for UI,
   - use coordinates for Input System / 3D gameplay only.
4. **Shared Editor-update scheduler only after justified**
   - `InputQaEventSystem` and `ManagePackages` already duplicate a next-update helper,
   - sequence/replay introduces the third production consumer that can justify a Core helper.
5. **Exec benchmark before compiler architecture changes**
   - current `/shared` + prewarm + DLL cache + Assembly cache may already cover the practical hot path.
6. **Persistent compiler worker only if measured win is material**
   - include LOC, complexity, domain-reload, and version-compat cost in the decision.

## 11. LOC strategy

Do not promise that total production LOC decreases while adding several major capabilities.

Use these success measures instead:

- production LOC delta per milestone,
- deleted/reused LOC,
- number of new abstractions,
- top-level tool delta,
- action delta,
- normalized catalog byte delta,
- command latency before/after,
- repeated-input round-trip reduction.

The migration should improve **capability per line** and remove existing duplication opportunistically.

For each milestone record:

```text
production Go LOC before/after/delta
production C# LOC before/after/delta
tool count before/after
action count before/after
catalog normalized bytes before/after
benchmark before/after where applicable
```

## 12. Milestones

### M1: common frame/update primitive and input cleanup

Goal:

- inspect existing duplicate next-Editor-update patterns,
- add a shared helper only if sequence/replay creates a real third production consumer,
- keep behavior unchanged,
- reduce duplication before feature growth.

Likely consumers:

```text
InputQaEventSystem
ManagePackages
new InputSystem automation engine
```

Do not over-generalize package watchers, test runner state, or heartbeat into one scheduler unless the API is genuinely identical.

Exit criteria:

- focused tests pass,
- all Go tests pass,
- Connector compile/test gate passes for relevant change,
- no tool/action/catalog change unless unavoidable,
- production LOC delta recorded.

Status: **PASS**

Decision:

- Keep the two await-based `NextEditorUpdate` implementations local for now.
- `InputQaEventSystem` and `ManagePackages` are still the only production
  consumers with the same task-returning, main-thread continuation contract.
- `PackageJobState` uses persistent callback watchers with domain-reload
  recovery, so it is not the same abstraction.
- Extract the shared Core primitive when the optional Input System automation
  engine or input sequence/replay becomes the third production consumer.

### M2: optional Input System keyboard/mouse backend

Goal:

Add game-input synthesis while keeping EventSystem behavior intact and Input System optional.

Expected capabilities:

```text
keyboard press/down/up
mouse position/click/hold/delta/scroll as justified
capability/state reporting
```

Prefer extending `input` with backend-aware actions rather than adding top-level tools.

Exit criteria:

- project without Input System compiles and existing EventSystem tests remain green,
- project with Input System compiles and live PlayMode keyboard/mouse smoke tests pass,
- no accidental package dependency added to all projects,
- operation ledger/safety metadata are correct,
- catalog gate reviewed.

Status: **PASS**

### M3: input sequence

Goal:

Execute multiple input steps in one Unity request to reduce round trips and preserve frame timing.

Requirements:

- bounded step count,
- bounded waits/durations,
- deterministic validation before execution where possible,
- cancellation/cleanup leaves held keys/buttons released,
- no blind retry after unknown mutation outcome outside Hera's existing ledger model.

Measure:

- N single-event invocations vs one sequence invocation,
- total wall time,
- HTTP call count,
- output bytes.

Status: **PASS**

### M4: versioned input record/replay

Goal:

Add reproducible PlayMode QA recordings.

Suggested format identity:

```text
hera.input-recording/1
```

Requirements:

- record and replay via existing `input` tool,
- project-local/temp output policy consistent with Hera path safety,
- bounded file size/event count,
- frame/update semantics explicit,
- replay cleanup always releases injected state,
- status/result shape compact,
- exact failure evidence for unsupported Input System configuration.

Exit criteria:

- record a real sequence,
- replay it successfully,
- verify repeated replay does not leave stuck input state,
- verify file compatibility after domain reload where relevant.

Status: **READY**

### M5: screenshot UI annotation and annotation-only mode

Goal:

Enrich existing `screenshot` rather than replacing it.

Prefer metadata:

```text
instance_id
hierarchy_path
interactable
blocked_by
image coordinates
input coordinates when needed
```

Requirements:

- identity first for UI,
- annotation-only mode can avoid PNG work when pixels are not requested,
- coordinate-space naming is explicit,
- no duplicate EventSystem inspection logic if existing `InputQaResolver` can be reused cleanly.

Status: **PASS**

### M6: 3D physics/raycast evidence

Goal:

Map visible gameplay-space candidates to collider identity and input coordinates.

Requirements:

- use live camera and culling/layer constraints,
- bound grid density and result count,
- cluster/compact results where useful,
- integrate with screenshot evidence contract,
- do not add an unbounded scene scan.

Status: **PASS**

### M7: Unity launch/restart

Status: **IMPLEMENTED; LIVE HEARTBEAT/RESTART ACCEPTANCE BLOCKED**

Implement the approved narrow Go-side bootstrap exception after the preceding
milestones.

### M8: Restricted dynamic-code security

Status: **OPTION 2 APPROVED; PENDING M7**

Preserve Full Access as the default and add Restricted as an explicit opt-in.

If Restricted mode is approved in any form, design it to complement, not replace:

- arbitrary-code permission,
- approval,
- operation ledger,
- strict tool contract.

Security should be defense in depth.

### M9: exec performance A/B benchmark

Goal:

Measure current Hera against any proposed compiler fast path before importing a persistent worker architecture.

At minimum measure:

```text
cold unique exec
warm unique exec
identical cache-hit exec
post-domain-reload first exec
multiple unique snippets in one Editor session
```

Record compile/load/execute/serialize timings and total CLI wall time.

Only proceed with a new compiler architecture if the gain is material and repeatable across supported Unity buckets.

Status: **PENDING PREVIOUS DECISIONS/FEATURES**

### M10: cleanup and admission review

Goal:

After functionality is proven:

- delete superseded helpers,
- collapse true duplication,
- remove dead compatibility branches only with evidence,
- update docs and tests,
- run catalog comparison,
- report final LOC/capability/latency deltas,
- scan for unintentional new dependencies,
- verify no temporary reference source was copied into the package.

Status: **PENDING**

## 13. Validation discipline

For each milestone, use this order:

1. inspect the exact current files and relevant Git history,
2. record before metrics,
3. implement the minimum coherent change,
4. run narrow unit tests,
5. run repository Go gates,
6. run Connector package/Unity gates required by `CLAUDE.md`,
7. perform live Unity validation for behavioral claims,
8. compare tool catalog payload if the surface changed,
9. record after metrics,
10. refactor unnecessary growth before moving on,
11. commit with a focused conventional commit,
12. update this handoff before ending the session.

Do not claim a Unity behavior passed based only on a C# compile or a Go payload test.

## 14. Required repository gates

At minimum, when applicable:

```bash
gofmt -w <changed-go-files>
go vet ./...
go test -count=1 ./...
golangci-lint run ./...
golangci-lint fmt --diff
go run ./tools/generate-runtime-contracts --check
go run ./tools/sync-agent-guides --check
go run ./tools/validate-connector-package
git diff --check
```

For C# Connector behavior, follow the exact Unity compatibility/test requirements in `CLAUDE.md`. Do not substitute static reasoning for required live Unity evidence.

For catalog changes, produce a live catalog and compare against:

```text
docs/metrics/catalog-payload-baseline.json
```

Use `tools/catalog-payload-report` and explicitly review any `review_required` result.

## 15. Working style for Codex

- Read code before proposing a new layer.
- Prefer deleting duplication over wrapping it with another facade.
- Avoid defensive branches with no proven failure mode.
- Avoid tiny one-use helper methods that make control flow harder to read.
- Keep Go thin.
- Keep C# Unity behavior close to Unity APIs.
- Keep public result envelopes compact.
- Use on-demand skills/docs instead of growing always-loaded instructions.
- Preserve user changes and unrelated local commits.
- Never push, tag, publish, or bump release versions unless the user explicitly asks.

## 16. Handoff update format

At the end of every Codex work session, update this file with a `Progress Log` entry containing:

```text
Date/time
HEAD before
HEAD after
Milestone status
Files changed
Behavior added/removed
Tests run + exact results
Live Unity evidence
Tool/action/catalog deltas
Production LOC deltas
Open risks
Next exact step
User decisions still blocked
```

Commit implementation and handoff updates according to repository collaboration rules.

## 17. Progress Log

### 2026-08-09 - migration design handoff created

```text
Milestone: pre-M1 architecture analysis
Implementation changes: none
Architecture decision: preserve Hera Go/HTTP/Connector control plane
Migration strategy: extend existing input/screenshot/editor/exec surfaces
Reference baseline: unity-cli-loop 6e5e90097eb14df242055bd3f694603d70f26227
Hera baseline HEAD: f59d12b
Tools/actions baseline: 31 / 75
Production LOC baseline: Go 11,639 / C# 24,050
Blocked decisions: A Unity launch/restart Go exception, B exec Restricted-default semantics
Next step: M1, re-read actual update/frame consumers and implement only justified common primitive/input cleanup
```

### 2026-08-09 19:38 +09:00 - M1 passed without speculative extraction

```text
HEAD before: d221c930af1b3e8d72ad43c46022357cecb3be84
HEAD after implementation: d221c930af1b3e8d72ad43c46022357cecb3be84 (no production change; this handoff update is the session-close commit)
Milestone: M1 PASS; M2 is next
Files changed: docs/handoffs/hera-vnext-capability-migration-2026-08-09.md only
Behavior added/removed: none

Decision evidence:
- Current production await-based next-update consumers are exactly
  AgentConnector/Editor/Core/InputQaEventSystem.cs and
  AgentConnector/Editor/Tools/ManagePackages.cs.
- docs/DECISION_LEDGER.md records the existing lock: keep the helper local at
  two consumers and extract it when a third production consumer exists.
- AgentConnector/Editor/Core/PackageJobState.cs uses callback watchers that
  persist/recover package jobs across domain reloads; its lifecycle and API are
  intentionally different.
- The planned Input System automation engine and sequence/replay code do not
  exist yet. Adding a Core helper in M1 would therefore be speculative.

Tests and repository gates:
- go clean -testcache: PASS
- gofmt -w .: PASS, zero diff
- golangci-lint run ./...: PASS, 0 issues
- golangci-lint fmt --diff: PASS, zero diff
- go vet ./...: PASS
- go test -count=1 ./...: PASS, all packages
- go run ./tools/generate-runtime-contracts --check: PASS
- go run ./tools/sync-agent-guides --check: PASS
- go run ./tools/validate-connector-package: PASS
- git diff --check: PASS before the handoff edit

Live Unity evidence:
- Bootstrap PASS: Inventoria, port 8090, Unity 6000.3.5f2, state ready,
  31 tools, CLI v0.1.4.
- Installed UPM connector package is 0.0.80 from git commit
  6c6050c81a91753ecb04733e54901dfc2e4f4dd6.
- manage_packages list: PASS; the package request completed and returned the
  resolved package set.
- input state: PASS; EventSystem /EventSystem and one active GraphicRaycaster
  at /GameCanvas were reported; Input System capability was available.
- editor refresh --compile --timeout 120000: PASS; Editor returned to ready.
- console --type error --lines 50: PASS, 0 matched errors.
- This is a 6000.3 smoke check only. The five-bucket Connector compatibility
  release gate was not triggered because M1 changed no Connector code, asmdef,
  package dependency, tests, or package version.

Surface and production metrics:
- Tools: 31 -> 31 (delta 0)
- Actions: 75 -> 75 (delta 0)
- Normalized catalog bytes: 185,339 -> 185,339 (delta 0)
- Production Go LOC: 11,639 -> 11,639 (delta 0)
- Production C# LOC: 24,050 -> 24,050 (delta 0)
- No catalog comparison/baseline regeneration was required because the only
  post-baseline repository changes are handoff documentation.

Open risks:
- M2 must choose the optional Input System compilation boundary before a third
  consumer can justify the shared update/frame primitive.
- M2 must preserve EventSystem compilation and behavior when
  com.unity.inputsystem is absent.

Next exact step:
- M2: inspect the supported-bucket compilation options for an optional Input
  System keyboard/mouse backend, select the least-permanent dependency surface,
  and only then extract the shared update/frame helper when that engine becomes
  its third production consumer.

User decisions still blocked:
- Decision A: Unity launch/restart Go-side bootstrap exception.
- Decision B: exec Restricted-default semantics.
```

### 2026-08-09 23:22 +09:00 - M2 optional Input System backend passed

```text
HEAD before: d4d84a683fa88119bdb9c8f3299468cfb9e60cbd
HEAD after implementation: d4d84a683fa88119bdb9c8f3299468cfb9e60cbd (implementation and this handoff update are included in the session-close commit)
Milestone: M2 PASS; M3 is next

Files changed:
- Added Core/EditorUpdate.cs and Core/InputQaInputSystem.cs with Unity meta files.
- Extended InputQaEventSystem, InputQaResolver, InputQaTypes, and Tools/Input.
- Reused EditorUpdate from ManagePackages as the third production consumer
  justified by the M1 decision.
- Extended InputQa, release-gate, catalog, contract, discovery, and safety tests.
- Updated input help, command/design docs, README files, AGENTS/CLAUDE guidance,
  generated agent guides, changelog, and reviewed catalog baseline.
- Connector package version remains 0.0.80; no release version was bumped.

Behavior added:
- Existing `input` now exposes strict `keyboard` and `mouse` actions through an
  optional reflection-only Unity Input System backend.
- Keyboard supports press/down/up. Mouse supports move/click/down/up/delta/scroll.
- Mutations require active, unpaused PlayMode and current devices; no synthetic
  device is created and no compile-time package/assembly dependency is added.
- Hera-owned held keys/buttons reject duplicate ownership and are released on
  explicit up, PlayMode exit, or assembly reload.
- Existing EventSystem state/inspect/click/submit/scroll/drag behavior remains on
  its original backend.

Tests and repository gates:
- go test ./...: PASS, all Go packages.
- go vet ./...: PASS.
- go run ./tools/sync-agent-guides --check: PASS.
- go run ./tools/validate-connector-package: PASS.
- Exact-source Connector compatibility matrix: PASS 5/5, compile_failed 0,
  blocked 0: Unity 2022.3.62f2, 2023.2.22f1, 6000.0.35f1, 6000.3.5f2,
  and 6000.5.6f1.
- Source-injected disposable Unity 6000.5.6f1 EditMode suite: PASS 22/22 for
  HeraAgent.Tests, including InputQa, ToolCatalog, ToolContract, ToolDiscovery,
  ToolSafety, ReleaseGate, and UiDocApply coverage.
- The normal blank-project UPM package-test path was independently attempted but
  the local Unity Package Manager failed before Hera import with `[Package
  Manager] The "path" argument must be of type string. Received undefined`.
  The exact-source five-bucket compile gate and source-injected suite were used
  instead; this environment failure was not treated as a Hera test failure.

Live Unity evidence:
- Disposable Unity 6000.5.6f1 with Input System 1.20.0 reported the optional
  backend available with current keyboard and mouse.
- EditMode mutation correctly returned INPUTSYSTEM_PLAY_MODE_REQUIRED.
- In PlayMode, Space down/up, A press, mouse move, left down/up, delta, scroll,
  right click, B down, and middle down all changed the live Input System state as
  requested. After leaving PlayMode, both held-control lists were empty.
- Mutation records were written to the operation ledger; no approval token was
  auto-approved. This proves Unity gameplay-state synthesis, not physical OS
  keyboard/mouse input.
- The disposable test6.5 Unity process was stopped. Its activeInputHandler was
  restored to Input System, injected test assets were moved to a recoverable
  system temporary directory, and only the user's Inventoria Editor remained.

Surface and production metrics:
- Tools: 31 -> 31 (delta 0)
- Actions: 75 -> 77 (delta +2, both on existing `input`)
- Normalized catalog bytes: 185,339 -> 188,751 (delta +3,412)
- Catalog hash: sha256:fbf56525f4a1d3fdeede7e009d7daddc7a53bb53a8b1ce65c289ef81c8f8b6d7
- Catalog baseline was regenerated after explicit review; recompare with
  --fail-on-change passed with contract_changed=false and review_required=false.
- Production Go LOC: 11,639 -> 11,639 (delta 0)
- Production C# files: 108 -> 110 (delta +2)
- Production C# nonblank LOC: 24,050 -> 24,938 (delta +888)

Open risks:
- Normal UPM package tests remain locally blocked by the external Unity Package
  Manager `path` exception; rerun that packaging surface when the local UPM
  installation is healthy.
- The backend intentionally depends on reflected internal Input System event
  layout. The five supported Unity buckets compile cleanly, and live behavior is
  proven on Input System 1.20.0; future Input System releases still require the
  same compatibility/live gate.

Next exact step:
- M3: extend the existing `input` surface with one bounded sequence action,
  validate the full sequence before mutation, execute frame-timed steps in one
  Unity request, and guarantee cleanup of every held key/button on completion,
  cancellation, or failure. Measure it against N single-event CLI invocations.

User decisions still blocked:
- Decision A: Unity launch/restart Go-side bootstrap exception.
- Decision B: exec Restricted-default semantics.
```

### 2026-08-10 - Editor restart / UPM path comparison (diagnosis only)

```text
Scope:
- Investigated the user's report that agent-driven Editor restarts correlate
  with Unity Package Manager `path` failures.
- No launcher or blocked milestone was implemented. Decision A remains blocked.

Current live evidence:
- The user-restarted Inventoria Editor is healthy on Unity 6000.3.5f2, PID
  56188, Hera port 8090, state ready, with 31 exposed tools.
- The current Editor log shows UPM IPC connected, 70 packages registered, and
  the Hera Git package resolved from `?path=AgentConnector` into PackageCache.
- The current Hera error-console read contains no matching errors, and retained
  current logs contain no `path argument` / `undefined` occurrence.

Reference implementation evidence:
- Audited hatayama/unity-cli-loop at pinned commit
  6e5e90097eb14df242055bd3f694603d70f26227 and its launch-unity dependency at
  LaunchUnityCommand commit 098caa583c03af9655b7fb92c132f98b81e817ec.
- `uloop launch -r` passes `restart: true` and `unityArgs: []`; it does not use
  `-noUpm`. LaunchUnityCommand resolves the exact project, stops its matching
  process, handles stale lock state, launches with separate argv and inherited
  environment, sets `MSYS_NO_PATHCONV=1`, and waits briefly for UnityLockfile.
  unity-cli-loop separately waits for Editor/dynamic-code readiness and prewarms.
- The reference is itself a normal UPM package (Git URL or OpenUPM). Its
  post-import resolver checks local `Packages/src`, fixed `Library/uLoopMCP`,
  `PackageInfo.resolvedPath`, then `Library/PackageCache`. Because that code runs
  only after package import, it cannot repair a UPM failure that occurs before
  the package loads.

Conclusion and limits:
- The reference does not avoid UPM or contain a workaround for the observed
  pre-import UPM `path` exception; it assumes an already prepared, UPM-healthy
  project and hardens process/argument/readiness orchestration around it.
- The retained failure still belongs to the earlier normal blank-project UPM
  package-test attempt, before Hera import. The original failing stack is not
  retained. The local `file:` package / Unity 6000.5 fixture state is only a
  correlated candidate condition, not a proven root cause.
- Hera's `-noUpm` path remains appropriate only for disposable, source-injected
  benchmark/test fixtures. It is not evidence for disabling UPM in a real user
  project restart.

Repository state:
- HEAD e725b66bddf68542b06aca48774f312d25168e86; main remains ahead of origin/main
  by four user commits. Only this handoff evidence was added in this diagnosis.
```

### 2026-08-10 - User decisions A and B resolved

```text
Explicit user decisions:
- Decision A APPROVED: Hera may own Unity Editor launch/restart through the
  narrow Go-side process/bootstrap exception documented in section 8.
- Decision B OPTION 2 SELECTED: preserve Full Access as the `exec` default and
  add Restricted mode as an explicit opt-in.

Milestone effect:
- M7 is no longer user-blocked; it remains pending behind M3-M6.
- M8 is no longer user-blocked; it remains pending behind M7.
- No M7/M8 implementation was started out of sequence in this decision-record
  update. The next exact implementation milestone remains M3.
```

### 2026-08-10 - M3 bounded input sequence complete

```text
Status: PASS

Implemented contract:
- Extended the existing strict `input` tool with `action: "sequence"`; no new
  top-level tool was added.
- Sequence execution is PlayMode-only and deliberately limited to Input System
  keyboard/mouse steps. EventSystem pointer actions remain excluded because
  their current standalone down/up calls do not share a transaction-local
  pointer lifecycle that can be rolled back safely.
- Accepts 1..32 strict nested steps, validates the complete JSON shape,
  action/mode fields, device/control availability, ownership transitions, and
  aggregate budgets before the first mutation.
- Caps aggregate press/click holds at 30,000 ms, aggregate awaited frames at
  600, and execution wall time at 45 seconds. Nested sequence/read actions and
  unknown fields fail closed.
- Rejects sequence start when any standalone Hera-held key/button exists, so a
  sequence cannot release state it does not own. Sequence-acquired controls are
  released in `finally`; cleanup success/failure and final held state are
  reported structurally.
- Uses one outer command/operation-ledger record, fail-fast step execution, and
  compact summaries. Existing outcome-unknown/no-blind-retry behavior remains
  unchanged.
- Input System changes now execute deterministically in the configured update
  phase, and Editor frame waits queue the player loop. This removed the observed
  background-Editor stall without adding an Input System package dependency.

Primary implementation:
- AgentConnector/Editor/Core/InputQaSequence.cs
- AgentConnector/Editor/Core/InputQaSequencePlan.cs
- AgentConnector/Editor/Core/InputQaInputSystem.cs
- AgentConnector/Editor/Core/EditorUpdate.cs
- AgentConnector/Editor/Tools/Input.cs
- AgentConnector/Editor/Tools/InputSequenceContract.cs
- AgentConnector/Editor/Tests/InputQaSequenceTests.cs
- Connector source version: 0.0.81

Contract and live evidence:
- RED against the previously installed Connector: `sequence` was outside the
  input action enum and nested `steps` collided with the drag integer contract.
- Strict validation against the source-injected Connector: PASS.
- InputQa release gate: PASS 1/1.
- Full source-injected Unity 6000.5.6f1 EditMode suite: 21/22. Every InputQa,
  ToolCatalog, ToolContract, ToolDiscovery, and ToolSafety test passed. The only
  failure was the pre-existing unrelated UiDocApply
  `TestRootCanvasCreatesEventSystem` fixture assertion.
- Live PlayMode pre-held test: standalone B remained held and sequence returned
  INPUT_SEQUENCE_PREEXISTING_HOLD; explicit standalone up then released B.
- Live unbalanced Space-down sequence: completed successfully and final cleanup
  released Space with an empty `held_after` state.
- Live eight-step keyboard/mouse sequence: 8/8 completed, total_hold_ms 20,
  total_awaited_frames 9, cleanup succeeded, and held_after was empty.
- Final post-review background-Editor smoke: a two-step sequence completed in
  17 ms, cleanup succeeded, held_after was empty, and Unity console errors were
  zero. The targeted InputQa release gate also passed 1/1 after the review fix.

Performance and catalog evidence:
- Eight equivalent single calls: 8 HTTP calls, 1,367 ms wall time, 1,500 output
  bytes.
- One eight-step sequence: 1 HTTP call, 191 ms wall time, 871 output bytes.
- Measured wall-time speedup: 7.16x.
- Tools 31 -> 31; actions 77 -> 78; normalized catalog bytes 188,751 ->
  191,043 (+2,292); sequence describe payload 2,838 bytes.
- Catalog hash:
  sha256:0b1e13bb595df72d57c3fc2939be3c13a789cbd4fce16f5ef177be97101626bc
- Reviewed baseline regeneration then passed `--fail-on-change` with
  contract_changed=false, review_required=false, and growth=false.

Final gates:
- Exact-source Connector compile matrix: PASS 5/5, failed 0, blocked 0 for
  Unity 2022.3, 2023.2, 6000.0-6000.2, 6000.3-6000.4, and 6000.5+.
- gofmt -l: clean.
- golangci-lint run ./...: 0 issues.
- golangci-lint fmt --diff: clean.
- go test ./...: PASS.
- go vet ./...: PASS.
- go run ./tools/sync-agent-guides --check: PASS.
- go run ./tools/validate-connector-package: PASS.
- git diff --check: PASS (line-ending conversion warnings only).
- Independent review initially found that timeout cancellation callbacks could
  unsubscribe Unity events from a ThreadPool thread. Cancellation callbacks now
  change only atomic completion/Task state; main-thread update/failure paths own
  Unity event removal. Re-review verdict: RESOLVED, no blocking findings.

UPM diagnosis update:
- Fresh normal-UPM disposable Editor launches reproduced the exact
  `The "path" argument must be of type string. Received undefined` package
  resolution popup across more than one installed Unity version. Removing the
  local Hera dependency/cache and adding Hub-style launch flags did not remove
  it, so the local `file:` dependency is not established as the cause.
- The retained artifacts still do not include the originating UPM/Node stack;
  the precise external/global UPM environment cause remains unknown.
- The reference unity-cli-loop/launch-unity flow neither disables UPM nor repairs
  this pre-import exception. It launches a prepared UPM project and waits for
  process/Editor readiness. Hera's `-noUpm` source-injected path remains limited
  to disposable verification fixtures.

Cleanup:
- The disposable test6.5 Editor was stopped after verifying its exact project
  command line. Injected M3 connector/dependency directories and Unity-created
  recovery directories were moved, not deleted, into unique system temporary
  recovery folders.
- The user's Inventoria Editor and its user commits were preserved.

Next exact step:
- M4: add versioned `hera.input-recording/1` record/replay through the existing
  input tool, with bounded project-local/temp storage, explicit timing, replay
  cleanup, and live repeated-replay evidence.
```

### 2026-08-10 - M4 versioned input recording and replay complete

```text
Status: PASS

Implemented contract:
- Extended the existing strict `input` tool with `record` and `replay`; tools
  remain 31 and no top-level surface was added.
- `record` uses modes start/stop/status. Capture requires active, unpaused Play
  Mode and samples real current Keyboard/Mouse state after the configured Input
  System update. Stop/status remain available after Play Mode exits.
- Added the explicit `hera.input-recording/1` JSON identity. The format records
  keyboard/button transitions, changed mouse positions, and non-zero
  delta/scroll with relative frame timing.
- Bounds are 256 events, 600 relative frames, 30 seconds, and 512 KiB. Replay
  requires monotonic frames starting at zero, strict event fields/actions,
  finite vectors, and full sequence ownership/preflight before mutation.
- Default output is a unique project-local
  Library/HeraAgent/Recordings/*.json file. Explicit read/write paths must stay
  under the project or system temp directory. Existing output is never
  overwritten and writes use create-new semantics.
- Replay reuses the M3 plan executor, one outer ledger record, fail-fast
  execution, the 45-second wall deadline, pre-held rejection, and finally-based
  held-control cleanup. Replay results remain compact and report cleanup plus
  held_after.
- Active capture stops on Play Mode exit and is saved before assembly reload.
  Saved files remain plain versioned JSON and load after domain reload.
- Connector source version advanced from 0.0.81 to 0.0.82.

Primary implementation:
- AgentConnector/Editor/Core/InputQaRecording.cs
- AgentConnector/Editor/Core/InputQaReplay.cs
- AgentConnector/Editor/Core/InputQaInputSystem.cs
- AgentConnector/Editor/Core/InputQaSequence.cs
- AgentConnector/Editor/Core/InputQaSequencePlan.cs
- AgentConnector/Editor/Tools/Input.cs
- AgentConnector/Editor/Tools/InputRecordingContract.cs
- AgentConnector/Editor/Tests/InputQaRecordingTests.cs

Live Unity evidence:
- Marked disposable source-injected Unity 6000.5.6f1 fixture, Input System
  1.19.0, current FastKeyboard/FastMouse, Play Mode: PASS.
- Recorded a real three-step synthesized sequence while the recorder observed
  configured dynamic updates. The saved recording contained five events over
  frames 0..30 (586 bytes, metadata duration 713 ms): initial/current mouse
  positions, Space down, mouse move, and Space up.
- Replacing Connector source caused a domain reload after the file was saved.
  The same file then replayed successfully twice: each replay completed 5/5,
  cleanup succeeded, and held_after contained no keys or mouse buttons.
- The first post-record replay exposed an M3 timing assumption: when the
  explicit InputSystem.Update call did not synchronously enter the configured
  phase, the callback failed before mutation. Evidence was completed_count=0,
  cleanup succeeded, and held_after empty. The executor now keeps the guarded
  callback, queues the player loop, and waits for the next matching update.
  Standalone mouse input and both repeated replays passed after the fix.
- A separate marked source-injected fixture without Input System reported
  available=false and record start failed exactly with INPUTSYSTEM_UNAVAILABLE
  plus a first-person [Hera] diagnostic. No compile-time Input System dependency
  was added.

Contract and regression evidence:
- Targeted source-injected InputQa release gate: PASS 1/1 after final bounds,
  finite-vector, path, contract, and player-loop changes.
- Full source-injected Unity 6000.5.6f1 EditMode suite: PASS 22/22 after the
  residual UiDocApply fixture expectation was corrected. The `-noUpm` fixture
  compiles copied Input System sources without the package-derived
  `UNITY_INPUT_SYSTEM_ENABLE_UI` symbol, so `InputSystemUIInputModule` is
  legitimately absent and production falls back to `StandaloneInputModule`.
  UiDocApply tests now mirror that existing fallback instead of requiring the
  optional type unconditionally.
- Strict catalog actions: 78 -> 80; tool count 31 unchanged. Normalized catalog
  grew by 1,608 bytes. `record` and `replay` action describes are 1,575 and
  1,493 bytes and save about 95.5%/95.8% versus full input describe.
- Reviewed catalog hash:
  sha256:1f68ec8dbb1af6b75590656c32c792a6ee9a72b119090f2b638a9ed2dcf6946c
  Regenerated baseline then passed --fail-on-change with
  contract_changed=false, review_required=false, growth=false.

Compatibility and repository gates:
- Current exact Connector/TestRunner sources passed representative Unity
  2022.3.62f2, 2023.2.22f1, 6000.0.35f1, 6000.3.5f2, and 6000.5.0f1 compile
  buckets with failed 0 and blocked 0 after the UiDocApply correction.
- go test ./...: PASS.
- go vet ./...: PASS.
- golangci-lint run ./...: 0 issues.
- golangci-lint fmt --diff: clean.
- go run ./tools/sync-agent-guides --check: PASS.
- go run ./tools/validate-connector-package: PASS.
- catalog baseline comparison --fail-on-change: PASS.
- git diff --check: PASS (line-ending conversion warnings only).
- C# LSP remained unavailable because installation was previously declined;
  exact-source compilation and live Unity compilation/test gates covered the
  changed C# files instead.

Residual UiDocApply diagnosis:
- RED was reproducible in the original source-injected fixture: targeted
  UiDocApply 0/1, with both root creation and shared EventSystem checks logging
  failure. The shared test stopped at its null expected-module guard.
- Cause toggle: temporarily defining `UNITY_INPUT_SYSTEM_ENABLE_UI` in only the
  disposable fixture compiled `InputSystemUIInputModule` and changed the
  unchanged target to PASS 1/1. Removing that toggle and applying only the test
  expectation correction also produced PASS 1/1, confirming the product
  fallback was already correct.
- Final source-injected full EditMode suite: PASS 22/22. No Connector runtime or
  public contract changed, so the Connector version remains 0.0.82.

UPM diagnosis update:
- A brand-new Unity 6000.5.6f1 project with no Hera file/local package
  dependency reproduced `The "path" argument must be of type string. Received
  undefined` in 0.04 seconds when its manifest requested only registry Input
  System. This disproves the earlier local Hera `file:` dependency as a required
  trigger; the precise external/global UPM environment cause remains unknown
  because Unity did not retain the originating Node stack.
- M4 live/test work therefore used the marked `-noUpm` source-injected fixture,
  with cached Input System/TestRunner sources or compiled assemblies confined
  to that disposable project. This remains a verification-only workaround, not
  a production Editor launch policy.
- Enabling the copied Input System required activeInputHandler=1 only in the
  disposable fixture. The TMP essential-resources warning in the no-Input-System
  fixture likewise came from copied built-in uGUI/TMP sources and did not touch
  the user's project.

Preservation:
- The user's Inventoria Editor (Unity 6000.3.5f2, PID 56188) and all pre-existing
  user commits/changes were left untouched.
- The marked M4 Unity 6000.5.6f1 fixture was stopped after its executable and
  exact temporary project command line were verified. The separate no-Input-
  System fixture had already been stopped; both fixture directories remain in
  system temp for recoverable evidence inspection.
- No M6-M8 work was started out of order.

Next exact step:
- M6: add bounded 3D physics/raycast evidence through the existing input/evidence
  architecture, using the live camera plus layer/culling constraints and stable
  collider identity.
```

### 2026-08-10 - M5 screenshot UI annotation and annotation-only mode complete

```text
Status: PASS

Implemented contract:
- Extended the existing strict `screenshot` tool; no new top-level tool or
  action was added. `annotate_ui` enriches Game View captures and
  `annotations_only` returns the same metadata without resolving an output
  path, rendering pixels, encoding PNG, or writing a file.
- Annotation candidates are active uGUI `Selectable` objects, ordered by stable
  hierarchy path then instance ID and bounded to 1..100 entries (default 32).
- Each entry is identity-first: instance_id, hierarchy_path, name, type,
  interactable state, non-interactable reason, blocked_by identity, raycast
  target state, and point/bounds coordinates.
- Input coordinates are named `unity_screen_bottom_left_pixels`; image
  coordinates are named `game_view_top_left_pixels`. The response reports both
  spaces and dimensions separately from captured PNG dimensions/editor chrome.
- Existing `InputQaEventSystem.BuildInspection` performs reachability/blocker
  inspection and `InputQaResolver` now owns the shared world/RectTransform to
  screen conversion. No second EventSystem raycast implementation was added.
- Annotation-only rejects PNG output/overwrite flags before any file policy or
  capture work. UI annotations reject Scene View and isolated-render modes.
- Connector source version advanced from 0.0.82 to 0.0.83.

Primary implementation:
- AgentConnector/Editor/Core/InputQaResolver.cs
- AgentConnector/Editor/Tools/EditorScreenshot.cs
- AgentConnector/Editor/Tools/EditorScreenshot.UiAnnotations.cs
- AgentConnector/Editor/Tests/ScreenshotAnnotationTests.cs
- AgentConnector/Editor/Tests/ReleaseGateTests.cs

Live Unity evidence:
- Marked disposable source-injected Unity 6000.5.6f1 fixture compiled the exact
  current Connector sources with `-noUpm`.
- Actual CLI `screenshot --annotations_only --max_annotations 10` returned the
  target instance ID/path, explicit 1080x1920 input/image spaces, bounds, and
  `pixels_requested=false` with no path or captured PNG.
- In Play Mode the same actual CLI call reported target_hit=true and identified
  `/HeraM5Canvas/BlockingGraphic` as blocked_by. The preceding Edit Mode call
  accurately retained identity/coordinates while its inactive raycast stack
  reported target_hit=false and no blocker.
- Actual CLI `screenshot --view game --annotate_ui --width 640 --height 360`
  wrote the requested disposable PNG and returned the same identity/blocker
  metadata while keeping 1080x1920 Game View coordinates distinct from the
  640x360 captured PNG/editor window.
- Fresh fixture console error reads after live annotation/capture returned zero
  matched errors. The user's Inventoria Editor was never selected for mutation.

Contract and regression evidence:
- Direct source-injected annotation suite executed through Unity
  `-executeMethod`: PASS. It covers metadata-only no-PNG behavior, identity,
  interactability, Edit Mode raycast shape, points/bounds, explicit coordinate
  names, output conflict, and strict 1..100 bounds.
- The no-UPM fixture does not expose package-gated `run_tests`; the reviewed M4
  catalog's unchanged run_tests entry was combined with the live M5 screenshot
  entry for a complete 31-tool baseline. Tool count remains 31, actions remain
  80, normalized catalog growth is 536 bytes, and the reviewed catalog hash is
  sha256:e1ecc397f0b7ed5a6249c4a50ad5851b309409a92bf4a96d4cc28c34ed5432cb.
  Regenerated baseline comparison passed with contract_changed=false,
  growth=false, and review_required=false.
- Exact current Connector/TestRunner sources passed all five representative
  compile buckets: Unity 2022.3.62f2, 2023.2.22f1, 6000.0.35f1, 6000.3.5f2,
  and 6000.5.0f1; failed 0, blocked 0.
- go test ./...: PASS.
- go vet ./...: PASS.
- go run ./tools/sync-agent-guides --check: PASS.
- go run ./tools/validate-connector-package: PASS.
- git diff --check: PASS (line-ending conversion warnings only).
- C# LSP remained unavailable because installation was previously declined;
  exact-source five-bucket compilation and live Unity execution covered the
  changed C# files instead.

Preservation:
- The user's Inventoria Editor (Unity 6000.3.5f2, PID 56188), all existing user
  commits, and the 120-second router lock invariant were left untouched.
- Every M5 fixture Editor process was stopped by exact PID. Recoverable M5 QA
  scene/helper/PNG/catalog/log artifacts remain only inside the already marked
  disposable system-temp fixture; cleanup commands were not escalated after the
  environment rejected removal.
- The approval-gated Editor menu test was not auto-approved. Equivalent direct
  Unity executeMethod coverage and actual screenshot CLI calls supplied the
  evidence without consuming an approval token.
- M6-M8 implementation was not started.

Next exact step:
- M6: enrich the existing evidence/input surface with bounded 3D physics raycast
  results using live camera/layer/culling state, stable collider identity, and
  explicit screen/input coordinates.
```

### 2026-08-10 - M6 bounded 3D physics/raycast evidence complete

```text
Status: PASS

Implemented contract:
- Extended the existing strict screenshot tool; no new top-level tool or action
  was added. annotate_physics enriches Game View captures and physics_only
  returns the same 3D evidence without resolving an output path, rendering,
  encoding, or writing PNG pixels.
- Physics evidence requires an active Camera tagged MainCamera. Every ray uses
  the intersection of Camera.main.cullingMask and the optional signed 32-bit
  physics_layer_mask, with bounded positive distance and explicit trigger
  handling (use_global, ignore, or collide).
- The square grid defaults to 9x9 and is strictly bounded to 1..16 per axis, so
  one request issues at most 256 nearest-hit Physics.Raycast queries. Results
  are clustered by 3D Collider, sorted by sample count then stable path/ID, and
  bounded to 1..100 entries (default 32) after clustering. No collider/scene
  object scan or Physics2D query was added.
- Each result identifies the GameObject and Collider separately, then reports
  layer, sample_count, representative hit distance/point/normal, Unity input
  point/bounds, and top-left Game View image point/bounds. The response also
  reports camera identity, requested/camera/effective masks, grid/ray counts,
  distance, trigger policy, truncation, and explicit coordinate spaces.
- UI and physics evidence share one coordinate-space response builder and can
  coexist on the same screenshot request. Scene View and isolated rendering
  remain rejected for either Game View evidence mode.
- Connector source version advanced from 0.0.83 to 0.0.84.

Primary implementation:
- AgentConnector/Editor/Tools/EditorScreenshot.cs
- AgentConnector/Editor/Tools/EditorScreenshot.PhysicsAnnotations.cs
- AgentConnector/Editor/Tools/EditorScreenshot.UiAnnotations.cs
- AgentConnector/Editor/Tests/ScreenshotPhysicsTests.cs
- AgentConnector/Editor/Tests/ReleaseGateTests.cs

Reference comparison:
- Compared against unity-cli-loop baseline
  6e5e90097eb14df242055bd3f694603d70f26227. Its raycast path also uses
  Camera.main, intersects the requested layer mask with camera culling, and
  clusters samples by collider. Hera retained those behavioral lessons but
  reimplemented them in the existing screenshot contract with configurable
  strict bounds instead of the reference's fixed dense 40x40 grid.

Live Unity evidence:
- Used only the marked disposable source-injected Unity 6000.5.6f1 fixture
  under system temp with -noUpm. The user's Inventoria Editor was never selected
  for mutation.
- Actual CLI screenshot --physics_only --physics_grid_size 3 returned 9/9 ray
  hits clustered into one BoxCollider with distinct GameObject/collider IDs,
  stable /HeraM6Target path, representative world hit data, explicit 640x480
  input/image coordinates, effective layer mask 1073741824, and no PNG path.
- Actual CLI screenshot --view game --annotate_physics --width 320 --height 240
  wrote a non-empty disposable PNG and returned the same physics identity while
  keeping the 640x480 Game View coordinate spaces distinct from the 320x240
  captured PNG dimensions.
- The fixture console error read after both live calls returned zero matched
  errors. The initial setup exec was correctly stopped by APPROVAL_REQUIRED and
  was not auto-approved; a disposable executeMethod helper created the QA scene
  instead.

Contract and regression evidence:
- Targeted source-injected ReleaseGateTests.ScreenshotPhysics: PASS 1/1. Its
  direct suite logged 10/10 checks PASS: no-PNG mode, 3x3/9-ray bound,
  clustering, GameObject/collider identity, camera/layer intersection,
  coordinates/world hit data, post-cluster truncation, empty culling
  intersection, output conflict, and strict schema bounds.
- A full no-UPM ReleaseGateTests wrapper run produced 12/17 PASS and five
  catalog/discovery/profile/safety failures because this fixture does not expose
  the package-gated run_tests tool. That known fixture limitation is the same
  reason M5 combined the unchanged reviewed run_tests entry with its live
  catalog; it is not a new M6 runtime failure.
- The reviewed M5 full catalog's unchanged run_tests entry was combined with
  the live M6 screenshot entry. Tools remain 31, actions remain 80, normalized
  catalog bytes are 194404 (+1217), and the reviewed catalog hash is
  sha256:d1216b934d5fc1783665904dd0128d0d840f558dab6600963c2f6292a175f269.
  Regenerated baseline comparison passed with contract_changed=false,
  growth=false, and review_required=false.
- Exact final Connector/TestRunner sources passed all five representative
  compile buckets: Unity 2022.3.62f2, 2023.2.22f1, 6000.0.35f1, 6000.3.5f2,
  and 6000.5.0f1; failed 0, blocked 0.
- go test ./...: PASS.
- go vet ./...: PASS.
- go run ./tools/sync-agent-guides --check: PASS.
- go run ./tools/validate-connector-package: PASS.
- git diff --check: PASS (line-ending conversion warnings only).
- C# LSP remained unavailable because installation was previously declined;
  exact-source five-bucket compilation plus targeted/live Unity execution
  covered the changed C# files.

Preservation:
- The user's Inventoria Editor remained running at its original PID 56188 and
  was not selected for mutation. Existing commits and the 120-second router lock
  invariant were preserved.
- Every M6 disposable fixture Editor was stopped by its exact verified PID.
  Recoverable logs, XML, scene/helper, PNG, and catalog artifacts remain only in
  the already marked system-temp fixture.
- No M7 or M8 implementation was started. M7's prior user approval and M8 option
  2 approval remain recorded above.

Next exact step:
- M7: implement the approved narrow Go-side Unity launch/restart bootstrap,
  preserving project-safe exact targeting and avoiding the external UPM path
  failure assumptions documented in this handoff.
```

### 2026-08-10 - M7 exact-project Unity launch/restart implemented

```text
Status: IMPLEMENTED; LIVE SUCCESS BLOCKED BY EXTERNAL UPM FAILURE

Implementation commit:
- 601f2a2 feat(editor): add exact-project launch and restart

Implemented contract:
- Added `editor launch` and `editor restart` to the existing editor command.
  The standalone dispatch path handles only these two actions before normal
  Connector discovery; play/stop/pause/refresh remain Connector-backed.
- Both actions require an exact `--project` path and reject `--port` before any
  process mutation. The project path is normalized from the filesystem and its
  exact Unity version is read from ProjectSettings/ProjectVersion.txt.
- The matching Unity executable is resolved from `--hub-root`, then
  UNITY_HUB_EDITOR, then the platform Unity Hub default. No Node or
  launch-unity dependency was added.
- Unity starts with exactly `-projectPath <exact-path>`. The production launch
  path does not pass `-noUpm`, batch mode, or hidden package flags.
- `launch` refuses an already running exact-project heartbeat. `restart`
  refuses a missing exact-project heartbeat, stops only its recorded PID,
  waits for OS-confirmed exit, attempts exact Temp/UnityLockfile cleanup, and
  starts the new process. A lock cleanup failure is reported without leaving
  the already-stopped project permanently down.
- Completion requires a fresh heartbeat whose normalized project path and PID
  both match the process just started. Timeout returns stable
  EDITOR_HEARTBEAT_TIMEOUT data with the started PID and explicitly forbids a
  blind second launch.
- Windows and Unix process launch/stop implementations are separated by Go
  build tags. The CLI process releases the started Editor handle so the Editor
  survives CLI exit; Unix uses a new process group and Windows uses a new
  process group without suppressing the Editor GUI.
- CLI/help, COMMANDS, English/Korean README, changelog, CLAUDE structure, and
  generated agent-guide command inventory are synchronized. This was a Go-only
  change, so Connector package 0.0.84 was not changed.

Automated evidence:
- Added boundary tests for exact new-PID heartbeat selection, restart stopping
  only the exact project's PID, exact stale-lock target, pre-mutation --port
  rejection, installed version resolution, and the exact normal-UPM Unity argv.
- go test ./...: PASS.
- go vet ./...: PASS.
- go run ./tools/validate-connector-package: PASS.
- go run ./tools/sync-agent-guides --check: PASS.
- git diff --check: PASS (line-ending conversion warnings only).
- CGO-disabled linux/amd64, darwin/amd64, and windows/amd64 cross-builds: PASS.
- The referenced scripts/go-gauntlet.sh path does not exist in the current
  repository; the explicit Go test/vet/build/tool gates above were run instead.

Live process and failure evidence:
- Inventoria was never selected for mutation and remained on its original Unity
  6000.3.5f2 PID 56188.
- `editor launch` started the existing marked M17 fixture with the exact
  6000.3.5f2 Unity executable and separate `-projectPath` argv. UPM connected,
  registered 67 packages including the Hera Git package, then failed before
  Connector import with `[Package Manager] The "path" argument must be of type
  string. Received undefined`; therefore no Hera heartbeat appeared.
- A second marked source-injected disposable fixture removed the Hera Git
  dependency candidate and supplied a valid empty Packages/manifest.json.
  Normal UPM launch still failed at the same pre-import undefined-path error and
  loaded no packages. This narrows the local failure away from the Hera Git
  package path and a missing manifest, but the retained Unity log still lacks
  the underlying Node stack, so the root cause remains unproven.
- The second launch returned the intended compact failure envelope with code
  EDITOR_HEARTBEAT_TIMEOUT, exact project/version/editor path, started PID
  51324, heartbeat_seen=false, and the no-blind-retry message.
- Because both normal-UPM disposable launches failed before the Connector could
  publish a heartbeat, a real successful heartbeat handoff and subsequent
  `editor restart` PID transition could not be honestly demonstrated. M7 stays
  live-acceptance blocked and M8 must not start yet.

Preservation and cleanup:
- All three failed disposable Unity processes were verified against their exact
  command-line project path and stopped by exact PID. The newly created marked
  QA fixtures and temporary binaries/logs were sent to the Windows Recycle Bin,
  so they remain recoverable; the existing M17 fixture itself was preserved.
- All existing user commits and changes were preserved. No release tag or push
  was performed.

Next exact step:
- Reproduce the normal-UPM undefined-path failure with a retained Package
  Manager/Node stack or obtain a known UPM-healthy disposable project, then run
  one successful `editor launch` heartbeat handoff and one exact-PID
  `editor restart` transition. Only after that live closure may M7 become PASS
  and M8 option 2 begin.
```

### 2026-08-10 - M7 live restart closure and remaining UPM console failure

```text
Status: M7 LIVE PROCESS ACCEPTANCE PASS; EXTERNAL UPM FAILURE REMAINS OPEN

Live evidence:
- After the user manually updated the Inventoria Connector package to 0.0.84,
  the implemented exact-project restart path completed two real heartbeat
  handoffs: PID 56188 -> 51580, then PID 51580 -> 47212. Each new process
  published the exact Inventoria project path on port 8090 and reached ready.
- The second restart was also repeated with the two inherited NODE_REPL_*
  variables removed for that launch only. The same UPM error remained, so
  those inherited variables are not the cause.
- The latest compact console read returned two errors and no compiler stack:
  `[Package Manager Window] The "path" argument must be of type string.
  Received undefined` and its offline package-list variant. Both terminate at
  `UnityEditor.EditorApplication:Internal_CallUpdateFunctions()`.
- Connector 0.0.84 being loaded and the M7 heartbeat/PID transition succeeding
  do not resolve this separate Package Manager Window failure. The current
  project does have Packages/manifest.json, and an earlier valid-empty-manifest
  disposable reproduction failed identically, so a missing manifest is not an
  established cause.

Preservation:
- No project assets, package manifest, package lock, or scene state were
  modified while reading the console. M8 option 2 has not started.

Next exact step:
- Diagnose the local Package Manager Window/List failure from Unity Hub launch
  context or Package Manager state without changing Inventoria's manifest.
  Once that environment issue is isolated, continue with approved M8 option 2.
```

## 18. First prompt for a new Codex session

Use this from the repository root:

```text
Read docs/handoffs/ACTIVE.md and the handoff it points to. Follow AGENTS.md and CLAUDE.md. Verify git status and actual current code first, then continue from the first incomplete milestone. Preserve existing user commits and changes. Do not implement any BLOCKED: USER DECISION item unless the handoff shows that the user explicitly approved it. Update the handoff with evidence before ending the session.
```
