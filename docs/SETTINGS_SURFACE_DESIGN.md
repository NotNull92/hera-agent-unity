# Project Settings Surface Design — read/write pairs with previews

Status: LOCKED (user approval, 2026-08-12). Scope: wave 2 of the
editor-workflow surface queue.

## Problem

Hera has no project-settings surface. Reading gravity, the fixed timestep,
the active quality level, or the product version requires `exec`; changing
them requires `exec` with no preview, no typed validation, and no approval
gate. The one exception is tag/layer *writes* (`manage_editor
add_tag/remove_tag/add_layer/remove_layer`) — which ship without a read, so
an agent cannot even list existing tags before creating one, and
`manage_gameobject set_tag` fails on unknown tags with no way to check
first.

Settings changes are also the risk profile Hera's safety model was built
for: project-wide, not undoable via Ctrl+Z, occasionally domain-reload
triggering — yet today they run as arbitrary code with no gate at all.

## Decisions to lock

### D1. Tool shape — one new tool, per-area typed actions

A new top-level tool **`manage_settings`** with one get/set action pair per
area (`get_physics` / `set_physics`, …).

- Absorbing into `manage_editor` was rejected: it would grow that tool from
  10 to 24+ actions and mix live-editor state (play mode, selection) with
  persisted project configuration — different risk profiles and different
  audiences.
- A 2-action shape (`get`/`set` + an `area` enum) was rejected: the set
  payload would be an untyped object, discarding the strict per-field
  schemas that are the point of Hera's typed contracts.
- Admission-gate cost: +1 tool, ~11 actions (five areas × 2 + one read
  below). Profiles: `core` is wrong (not an everyday surface) —
  `diagnostics` + `full`, revisitable.

**Exception:** the tag/layer *read* joins its existing writes as
`manage_editor get_tags_layers` (ReadOnly) — reads belong next to the writes
they precede.

### D2. Areas in wave 2

| Area | get/set | Notes |
|---|---|---|
| `physics` | both | gravity, solver iterations, bounce threshold, contact offset |
| `time` | both | fixedDeltaTime, maximumDeltaTime, timeScale |
| `quality` | both | active level (by index or name), per-level names list, vSync, anti-aliasing |
| `player` | both | company/product/version identity; **scripting backend and API level are read-only in wave 2** (their writes trigger a full domain reload and deserve their own gate) |
| `audio` | both | global volume, doppler, rolloff via the audio configuration |
| tags/layers | read (in `manage_editor`) | tags list + named layers with indices |
| graphics (render pipeline asset), input axes | **deferred** | pipeline-asset swaps interact with render setup too broadly; legacy input axes are a shrinking audience |

Exact field vocabulary is re-derived against the live Unity APIs at
implementation time and validated per bucket — the lists above are
candidates, not contract.

### D3. Write semantics — omitted fields unchanged, previews first-class

- Every `set_*` input model uses nullable fields; omitted fields are left
  untouched.
- Every `set_*` accepts `dry_run: true`, which validates and reports what
  would change without touching anything.
- Responses report `{applied: {field: new_value}, skipped: {field: reason},
  dry_run: bool}` — a partially-valid request applies the valid fields and
  names why the rest were skipped, so the agent never has to diff the whole
  area to learn what happened.

### D4. Safety — Hera's approval flow, not a confirm flag

No `confirm` parameter. Hera already has the right machinery: `set_*`
actions are declared `RiskClass.Destructive` (project-wide, not undoable),
which routes them through the existing preflight/approval-token flow the
same way `manage_assets delete` works today. A per-action safety rule
downgrades `dry_run == true` to read-only, so previews run without an
approval round trip (the conditional-rule mechanism `console clear` already
uses). `get_*` and `get_tags_layers` are ReadOnly.

### D5. Compatibility

- No existing action changes. `manage_editor` grows one ReadOnly action.
- New tool ships in `diagnostics` and `full` profiles only; `core` and
  compact baselines grow by the discovery row, reviewed via the catalog
  baseline regeneration.
- Connector `0.0.96`; CLI release only for help text (`manage_settings`
  passthrough needs no Go handler).

### D6. Validation plan

Per area, live on `6000.3.5f2`:

1. `get_*` snapshot → `set_*` one field → `get_*` shows the change and
   *only* that change → restore the original value (fixture-safe: every
   mutation is restored in the same session).
2. `dry_run: true` → response lists the would-be change → follow-up `get_*`
   proves nothing moved.
3. A `set_*` without an approval token is refused with `APPROVAL_REQUIRED`;
   the token flow completes it.
4. A mixed valid+unknown-field set applies the valid part and reports the
   rest in `skipped`.
5. `manage_editor get_tags_layers` lists a tag created by `add_tag`, then
   the tag is removed.
6. Three-bucket gate before release (settings APIs are long-stable; the
   gate re-runs one get/set/restore round trip per bucket).

## Implementation shape (informative, not gated)

`AgentConnector/Editor/Tools/ManageSettings.cs`, one `[HeraActionContract]`
per action with typed nullable input models and typed result DTOs; no shared
`Core/` additions expected. `manage_editor get_tags_layers` reuses the
existing TagManager access. Help: new `cmd/help/manage_settings.txt` topic
plus a row in the command tables. Connector `0.0.96` + CLI `v0.2.6`.
