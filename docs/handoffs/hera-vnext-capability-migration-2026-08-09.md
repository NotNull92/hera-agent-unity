# Hera vNext Capability Migration Handoff

Status: **READY FOR M2**

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

## 8. User-decision gates

These are intentionally blocked. Do not silently resolve them.

### BLOCKED A: Unity launch/restart before Connector discovery

Problem:

Unity cannot execute C# Connector logic while the Editor is not running. True `editor launch` / `editor restart` therefore requires process orchestration in Go before normal Unity discovery.

This is a narrow exception to the general rule that Unity business logic belongs in the C# Connector.

Recommended implementation if user approves:

- expose as existing `editor launch` / `editor restart`, not a new top-level tool,
- handle only process/bootstrap concerns in Go,
- derive project Unity version from project metadata / installed Editors,
- wait for the selected project's heartbeat,
- hand control back to the normal Connector path immediately after startup,
- do not add Node or `launch-unity` dependency.

Status:

```text
BLOCKED: USER DECISION
```

When M7 is reached, stop and ask the user whether this narrow Go-side exception is approved unless the user has already answered it in a later commit/handoff update.

### BLOCKED B: make `exec` Restricted by default

Current public Hera contract:

```text
exec = approved arbitrary C# with full Unity / loaded-assembly access
```

Candidate vNext contract:

```text
exec default = Restricted dynamic code
  source validation
  pre-load metadata validation
  post-load IL validation

explicit Full Access
  existing arbitrary-code permission
  existing approval
  existing operation ledger
```

This would be a breaking behavior change even though it improves defense in depth.

Status:

```text
BLOCKED: USER DECISION
```

When M8 is reached, stop and ask whether to:

1. make Restricted the new default in a breaking release, or
2. preserve Full Access default and add opt-in Restricted mode.

Do not choose on the user's behalf.

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

Status: **PENDING M1**

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

Status: **PENDING M2**

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

Status: **PENDING M3**

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

Status: **PENDING M4**

### M6: 3D physics/raycast evidence

Goal:

Map visible gameplay-space candidates to collider identity and input coordinates.

Requirements:

- use live camera and culling/layer constraints,
- bound grid density and result count,
- cluster/compact results where useful,
- integrate with screenshot evidence contract,
- do not add an unbounded scene scan.

Status: **PENDING M5**

### M7: Unity launch/restart

Status: **BLOCKED: USER DECISION A**

Do not implement until approved.

### M8: Restricted dynamic-code security

Status: **BLOCKED: USER DECISION B**

Do not change `exec` default semantics until approved.

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

## 18. First prompt for a new Codex session

Use this from the repository root:

```text
Read docs/handoffs/ACTIVE.md and the handoff it points to. Follow AGENTS.md and CLAUDE.md. Verify git status and actual current code first, then continue from the first incomplete milestone. Preserve existing user commits and changes. Do not implement any BLOCKED: USER DECISION item unless the handoff shows that the user explicitly approved it. Update the handoff with evidence before ending the session.
```
