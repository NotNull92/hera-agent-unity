# NavMesh Surface Design — baking the component-based navigation model

Status: LOCKED (user approval, 2026-08-13). Scope: wave 10 of the
editor-workflow surface queue; resolves the deferral in
`docs/BAKE_SURFACE_DESIGN.md` D4.

## Problem

`bake --area navmesh` drives Unity's built-in scene NavMesh
(`UnityEditor.AI.NavMeshBuilder`). Projects that use the AI Navigation package
do not have one: their navigation lives in `NavMeshSurface` components, each
owning its own `NavMeshData` asset. For those projects `bake --area navmesh`
answers honestly and uselessly — the built-in mesh is empty because nothing
uses it — and the bake they actually need has no Hera surface at all.

Wave 3 recorded the deferral rather than half-shipping it. The package is a
registry package, not a built-in module, so the integration also has to work
when it is absent.

## Live API ground truth (`6000.3.5f2`, `com.unity.ai.navigation` 2.0.14)

Installed into a disposable fixture for this probe.

| Member | Shape |
|---|---|
| `Unity.AI.Navigation.NavMeshSurface` | assembly `Unity.AI.Navigation` |
| `NavMeshSurface.BuildNavMesh()` | public instance, synchronous |
| `NavMeshSurface.navMeshData` | public property; null until baked |
| `NavMeshSurface.RemoveData()` / `AddData()` | public instance |
| `Unity.AI.Navigation.Editor.NavMeshAssetManager` | `ScriptableSingleton<T>` with a public static `instance` |
| `instance.StartBakingSurfaces(Object[])` | **public**, asynchronous, creates the `NavMeshData` assets |
| `instance.IsSurfaceBaking(NavMeshSurface)` | **public** |
| `instance.ClearSurfaces(Object[])` | **public** |
| cancellation | **none** — the package exposes no cancel for surface bakes |

Everything Hera needs is public. This is not reaching into package internals.

## Decisions to lock

### D1. A new `area` value, never auto-detection

`bake --area navmesh` keeps meaning the built-in scene NavMesh. Surfaces get
their own value: **`bake --area navmesh_surfaces`**.

Switching on whether the scene happens to contain `NavMeshSurface` components
was rejected. The two produce different artifacts — one mutates the scene's
built-in NavMesh, the other writes `NavMeshData` assets — and `bake` is
approval-gated precisely because an agent should know what it is about to
change. Silently retargeting a destructive action on scene contents is the
ambiguity `deps --direction` and `unpack --mode` already refuse.

`+0 actions`; the `area` enum gains one value.

### D2. The package is optional and its absence fails closed

The integration is reflection-only against the two public types, so the
Connector keeps compiling and running without the package —
`InputQaInputSystem` is the precedent. When the types are missing, every
`navmesh_surfaces` call returns `PACKAGE_NOT_INSTALLED` naming the package and
the one command that fixes it:

```
PACKAGE_NOT_INSTALLED: this project has no com.unity.ai.navigation.
Install it with: manage_packages add com.unity.ai.navigation
```

No silent fallback to the built-in NavMesh. A caller that asked for surfaces
and got a built-in bake would be told a bake succeeded that did nothing they
wanted.

### D3. Scope defaults to every surface in the loaded scenes, with `--target` to narrow

`start` and `clear` operate on all `NavMeshSurface` components in the loaded
scenes by default, matching what "bake the navigation" means. Optional
`--target` (hierarchy path, instance_id, or durable handle, via the shared
`TargetResolver`) restricts the operation to the surfaces on one object and its
children, because baking every surface in a large scene is slow and writes
assets the caller may not have meant to touch.

`+1 parameter` on an existing action set.

### D4. Status is derived live, like the rest of `bake`

`status --area navmesh_surfaces` reports `{surfaces, baking, with_data,
state}`: how many surface components exist, how many are mid-bake
(`IsSurfaceBaking`), how many already hold a `NavMeshData`, and `baking` or
`idle` in aggregate. No job ledger — same reasoning as wave 3, the Editor is
the source of truth across reconnects.

### D5. `cancel` says the package cannot, rather than pretending

The AI Navigation package exposes no cancellation for surface bakes (measured:
no such member). `bake cancel --area navmesh_surfaces` returns
`CANCEL_UNSUPPORTED` with that reason. It does not silently succeed, and it
does not fall back to cancelling the built-in bake, which would cancel
something the caller never started.

### D6. Safety mirrors the existing areas

`start` Write, `status` ReadOnly, `clear` Destructive and approval-gated —
identical to the other areas, because the consequences are the same kind. The
scene-saved guard from wave 3 (`SCENE_NOT_SAVED`) applies unchanged: a surface
bake writes `NavMeshData` assets next to the scene, so an untitled scene has
nowhere to put them.

## Admission gate

1. **Failure prevented** — a project using the AI Navigation package cannot
   bake its navigation through Hera at all, and `bake --area navmesh` reports
   an honest but useless answer about a built-in mesh it does not use.
2. **Existing surface reuse** — no new tool and no new action; one new `area`
   enum value and one optional `--target` parameter on the existing `bake`
   actions.
3. **Contract and safety** — risk classes unchanged per action; the optional
   package fails closed (D2); cancellation is reported unsupported rather than
   faked (D5).
4. **Regression evidence** — live matrix on a disposable fixture with the
   package installed: bake surfaces, poll to idle, confirm `NavMeshData`
   assets exist, clear them, and confirm removal; plus a bucket without the
   package returning `PACKAGE_NOT_INSTALLED`.
5. **Surface cost** — +0 tools, +0 actions, +1 enum value, +1 optional
   parameter. Catalog growth measured and kept inside the wave-8/9 precedent
   or the slice is cut.
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review.

## Verification plan

- Fixture with the package installed and a floor plus a `NavMeshSurface`:
  `status` reports the surface with no data; `start` returns immediately;
  polling `status` reaches `idle` with `with_data` equal to the surface count;
  the `NavMeshData` asset exists on disk; `clear` removes it and `status`
  returns to `with_data: 0`.
- `--target` restricted to one object bakes only that object's surfaces while a
  second surface elsewhere stays unbaked.
- `cancel --area navmesh_surfaces` returns `CANCEL_UNSUPPORTED`.
- An untitled scene returns `SCENE_NOT_SAVED`.
- A bucket **without** the package returns `PACKAGE_NOT_INSTALLED` for
  `start`, `status`, and `clear`, and `--area navmesh` still works there.
- Three-bucket gate: zero-warning compile — including the buckets where the
  package is absent, which is what proves the reflection-only boundary — and
  the release-gate suite green in each.
