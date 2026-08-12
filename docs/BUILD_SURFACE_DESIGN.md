# Build Surface Design — trigger a Player build and read its report

Status: LOCKED (user approval, 2026-08-12). Scope: wave 4 of the
editor-workflow surface queue.

## Problem

Hera cannot build the project. "Build it and tell me what failed" — the
canonical end-of-task request — has no verified loop: no trigger, no way to
know whether the build succeeded, no error extraction, no size or output
facts. `exec` can call `BuildPipeline.BuildPlayer`, but the call is
**synchronous and blocks the Editor main thread**, so the HTTP dispatch that
launched it cannot even return — the one shape Hera's single-shot model
cannot express without the file bus.

Scene-list management has the same blind spot as tags had: `EditorBuildSettings`
scenes (confirmed live: 2 entries on the connected project) are invisible and
unwritable without `exec`.

## Decisions to lock

### D1. Tool shape — one new tool `build`, seven actions

| Action | Effect | Risk |
|---|---|---|
| `start` | Queue the Player build for the **active** target and return immediately; the result lands on the file bus | Write |
| `status` | `idle` \| `building` \| last persisted report summary | ReadOnly |
| `get_settings` | Active target/group, development flags, scene list with enabled state | ReadOnly |
| `set_settings` | `development`, `allow_debugging`, `build_scripts_only` — nullable fields, `manage_settings` write semantics (`{applied, skipped}`, `dry_run`) | Write |
| `add_scene` / `remove_scene` | Edit the Build Settings scene list (idempotent; `add_scene` accepts `enabled`) | Write |
| `list_targets` | BuildTarget values with group and whether build support is installed | ReadOnly |

Deferred, recorded here: **switching the active build target** (full
reimport + domain reload — its own destructive gate), **build profiles**
(Unity 6 asset-based profiles), and player-settings writes that imply a
target switch. `start` builds what the Editor is already configured for.

### D2. Async model — file bus, the test/packages precedent

`BuildPipeline.BuildPlayer` blocks the main thread, so while a build runs
the Connector cannot answer HTTP at all — stateless in-memory status (the
`bake` model) is impossible. This is exactly the shape the file bus exists
for:

- `start` validates, schedules the build via a delayed editor call, and
  returns `{queued: true, output_path}` immediately.
- The build runs; the Editor goes unresponsive; the heartbeat reports it.
- On completion the Connector writes a compact report to
  `~/.hera-agent-unity/status/build-result-<port>.json` via the atomic
  file-write path the test runner uses.
- The Go CLI gains a thin `build` handler: `build start --wait` polls the
  result file with the shared `internal/poll` backoff (exactly `test`'s
  loop), tolerating the unresponsive window instead of surfacing it as
  errors. Without `--wait`, the agent can poll `build status` after the
  heartbeat returns.

### D3. Report shape — compact essentials, errors first

The persisted report (and `status` echo) is a token-conscious summary, not
the raw BuildReport:

`{result, output_path, size_bytes, total_seconds, error_count,
warning_count, errors: [first N step messages], target, started_at}`

- `errors` carries at most 20 messages, deduplicated, each truncated to a
  sane length — enough to fix the build, not the whole log.
- No inline full report. If deeper analysis is ever needed, that is a
  follow-up surface, recorded here rather than smuggled in.

### D4. Output guard

- Default output path: `Builds/<target>/<productName>` under the project
  root (created if missing), overridable with `output_path`.
- The path must resolve **outside `Assets/`** (a build into Assets would
  recursively import itself) and inside the project root unless the caller
  passes an absolute path explicitly.
- `start` refuses while a build is already running (`ALREADY_BUILDING`) and
  while the Editor is in play mode (`IN_PLAY_MODE`).

### D5. Safety and profiles

- `start` is **Write with `RequiresConfirmation = true`** — not destructive
  to the project, but long-running, Editor-blocking, and disk-writing; the
  approval preflight is the honest gate. `--wait` composes with the
  approval flow (approve, then wait).
- `set_settings`, `add_scene`, `remove_scene`: Write, no approval
  (persisted but ordinary, reversible edits). Reads ReadOnly.
- Profiles: `full` + a new nothing — `scene` is wrong (builds are not scene
  edits); `testing` fits `status`/`get_settings` reads but the tool ships in
  `full` only to start, revisitable.
- Connector `0.0.98`; CLI `v0.2.8` (new Go handler + help).

### D6. Validation plan

On the disposable `6000.3.5f2` fixture (never the connected user project —
a build writes large artifacts and churns the import pipeline):

1. `get_settings` → scene list matches `EditorBuildSettings`; `add_scene`
   the fixture scene → listed enabled; `remove_scene` → gone; re-add for
   the build.
2. `set_settings {development: true, dry_run: true}` previews without
   mutating; approved-free write applies; restore.
3. `start` (approval flow) with default output → poll with `build start
   --wait`-equivalent until the result file lands → report shows
   `result: Succeeded`, non-zero `size_bytes`, output exists on disk.
4. Guards: second `start` mid-build → `ALREADY_BUILDING` (or the
   unresponsive window, whichever the timing yields — both acceptable,
   recorded); output path inside `Assets/` → typed refusal; `start` in play
   mode → `IN_PLAY_MODE`.
5. A deliberately broken build (missing scene path in the list) surfaces
   `result: Failed` with the error in `errors[]`.
6. Fixture cleanup: delete the build output directory; three-bucket gate
   re-runs one build cycle per bucket before release.

## Implementation shape (informative, not gated)

`AgentConnector/Editor/Tools/Build.cs` (tool name `build`) + a small
`BuildRunner` state holder ([InitializeOnLoad]-free; delayCall + static
flag, result via `AtomicFile` like `TestRunnerState`); Go: `cmd/build.go`
(start/--wait polling via `internal/poll`, other actions passthrough),
`cmd/help/build.txt`. Catalog: +1 tool, +7 actions in `full`.
