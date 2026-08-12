# Bake Surface Design — lighting, navmesh, and occlusion in one tool

Status: LOCKED (user approval, 2026-08-12). Scope: wave 3 of the
editor-workflow surface queue.

## Problem

Hera cannot trigger, observe, cancel, or clear any bake. Lighting, NavMesh,
and occlusion-culling work — a routine part of finishing a scene — is only
reachable through hand-written `exec` code, with no typed status to poll, no
cancellation, and no guard against baking an unsaved scene. An agent asked
to "bake the lighting and tell me when it's done" has no verified loop to
run.

The Editor APIs make this cheap: bake state is fully derivable from live
editor state (confirmed on `6000.3.5f2`: `Lightmapping.isRunning` +
`buildProgress` + `giWorkflowMode`, `UnityEditor.AI.NavMeshBuilder.isRunning`,
`StaticOcclusionCulling.isRunning` + `umbraDataSize`), so no job ledger or
file bus is needed — unlike tests and package operations, a bake's status
survives reconnects for free.

## Decisions to lock

### D1. Tool shape — one tool, operation × area, not area × operation

A new top-level tool **`bake`** with four actions taking an `area` enum
(`lighting` | `navmesh` | `occlusion`):

| Action | Effect |
|---|---|
| `start` | Trigger the async bake for the area; returns immediately |
| `status` | `idle` \| `baking` \| area extras (lighting `progress`, occlusion `data_size_bytes`, navmesh baked-state) |
| `cancel` | Cancel an in-progress bake for the area |
| `clear` | Delete the area's baked data |

Unlike `manage_settings` (whose per-area *fields* differ, demanding typed
per-area actions), bake operations have an identical shape across areas —
the area-parameter design gives 4 actions instead of 12 with no loss of
schema strength (`area` is a strict enum). Absorbing into an existing tool
was rejected: no current tool owns scene-build artifacts, and `scene` is a
file-lifecycle tool.

### D2. Async model — stateless status, no job machinery

`start` returns immediately; the agent polls `bake status --area lighting`
until `idle`. Status is computed from the live APIs on every call — no
job_id, no file bus, no operation ledger entry beyond the normal dispatch.
Rationale: unlike test results, bake state needs no persistence — the Editor
itself is the source of truth across reconnects, and a domain reload aborts
a bake in a way `status` then reports honestly (`idle` again, with the
area's baked-data indicator showing whether output exists).

### D3. Guards

- `start` refuses when the active scene is untitled/unsaved-to-disk
  (`SCENE_NOT_SAVED`): baked data has nowhere durable to land.
- `start` while that area is already baking returns `ALREADY_BAKING` rather
  than silently queuing.
- `start` for `lighting` while the workflow mode is iterative/auto reports
  the mode in the response so the agent knows an explicit bake may be
  redundant.

### D4. NavMesh scope — built-in bake now, package surfaces later

`area: navmesh` drives the built-in scene NavMesh
(`UnityEditor.AI.NavMeshBuilder`, confirmed present on Unity 6). Baking AI
Navigation package `NavMeshSurface` components is a different authoring
model (per-component, runtime-capable) and is **deferred**; if the project
clearly uses only surfaces, `status` still answers honestly about the
built-in NavMesh, and the deferral is recorded here rather than half-shipped.

### D5. Safety and profiles

- `status` ReadOnly. `start` Write with `SupportsCancellation = true` and
  `MayReloadDomain = false`. `cancel` Write, idempotent (cancelling an idle
  area is a no-op success).
- `clear` **Destructive** → rides the existing approval-token flow (baked
  data deletion is not undoable).
- Tool profiles: `scene` + `full` (bakes are scene-finishing work).
  Connector `0.0.97`; CLI `v0.2.7` for the help topic.
- Lighting/navmesh *settings* areas (`manage_settings get/set_lighting`,
  `get/set_navmesh`) are **out of this wave** — the bake runs with the
  project's configured settings; the settings areas are a later
  `manage_settings` extension using its established pattern.

### D6. Validation plan

Bakes mutate project artifacts (GI cache, lightmaps, NavMesh, umbra data),
so live validation runs on a **disposable fixture**, not the connected user
project:

1. Fixture prep: saved scene with a static plane (lighting + navmesh input)
   and two static boxes (occluder/occludee).
2. Per area: `start` → poll `status` to `baking` → to `idle` → area's
   baked-data indicator present → `clear` (approval flow) → indicator gone.
3. `cancel` mid-bake for lighting (the slowest area) returns to `idle`.
4. Guards: `start` on an untitled scene → `SCENE_NOT_SAVED`; double `start`
   → `ALREADY_BAKING`; `clear` without a token → `APPROVAL_REQUIRED`.
5. Three-bucket gate before release, re-running one lighting
   start/status/clear cycle per bucket in the same fixtures.

## Implementation shape (informative, not gated)

`AgentConnector/Editor/Tools/Bake.cs` (tool name `bake`, filename
snake_case default), one handler switching on `action` + `area`; APIs:
`Lightmapping.BakeAsync/Cancel/Clear`, `NavMeshBuilder.
BuildNavMeshAsync/Cancel/ClearAllNavMeshes`, `StaticOcclusionCulling.
GenerateInBackground/Cancel/Clear`. No `Core/` additions. Help topic
`cmd/help/bake.txt` + general/README/COMMANDS rows.
