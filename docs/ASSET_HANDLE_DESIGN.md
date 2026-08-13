# Asset Handle Design — accepting the identities Hera already emits

Status: LOCKED (user approval, 2026-08-13). Scope: wave 8 of the
editor-workflow surface queue; the follow-up `docs/TARGET_RESOLUTION_DESIGN.md`
D2 deferred.

## Problem

Hera hands out GUIDs that Hera itself refuses. Measured on `6000.3.5f2`:

```
> manage_assets find --type Material --limit 1
{"path":"Packages/…/root-blue.mat","guid":"28638862ea1084726a3511cd325fc53d", …}

> manage_assets deps --path guid:28638862…      ASSET_NOT_FOUND
> manage_material  get  --path guid:28638862…   INVALID_PATH: path must be under Assets/
> manage_asset_import get --path guid:28638862… INVALID_PATH: path must be under Assets/
```

`manage_assets find` and `manage_assets deps` both report a `guid` for every
result, and no asset tool accepts one back. The agent has to convert to a path
it was also given — until the asset moves, at which point the path it recorded
is wrong and the GUID it was also given, and which never changes, is still not
usable.

Wave 1b shipped this grammar for scene targets, ObjectReference values, and
selection. Asset-tool path parameters were the one surface deliberately left
out, "additive follow-up once the grammar is proven". It is proven.

### Second, narrower gain: sub-assets that paths cannot name

A container holding **one** asset of a type is already reachable by path —
measured, `LoadAssetAtPath<Material>` on a controller with a single embedded
material returns it. The gap opens at two or more:

```
container with MatA, MatB, MatC embedded
  LoadAssetAtPath<Material>(path)  => MatA        (the other two: unreachable)
  guid:<guid>:5202938269566848524  => MatA
  guid:<guid>:-3333106086538760990 => MatB
  guid:<guid>:6105397947402632264  => MatC
```

That is the FBX-with-several-materials case. It is a real capability gain, but
a narrower one than "sub-assets are unreachable", and this document does not
claim the broader version.

## Decisions to lock

### D1. Where handles are accepted: existing-asset parameters only

A `guid:` or `GlobalObjectId_V1-…` handle names an asset that **already
exists**. It therefore cannot name a destination that does not exist yet.

| Parameter kind | Handles | Examples |
|---|---|---|
| Names an existing asset | **accepted** | `manage_material get/set/set_shader --path`, `manage_prefab --path`, `manage_asset_import get/set --path`, `manage_assets deps/delete --path`, `manage_assets copy/move --path` (source), `manage_animation --path` |
| Names a new file | rejected, unchanged | `manage_assets create --path`, `copy/move --new_path`, `manage_material create --path`, `manage_prefab create --path`, `manage_assets mkdir --path` |

Rejection stays the current `INVALID_PATH`, with a message naming the reason
rather than the generic containment text.

### D2. Resolution happens first, then the action's existing rule applies unchanged

A handle resolves to an asset path, and that path then goes through exactly the
guard the action already used. Nothing about containment, extension checks, or
existence checks changes.

This matters because a handle can address anything in the project, including
`Packages/` — measured, `guid:28638862…` resolves to
`Packages/com.unity.2d.psdimporter/Editor/Assets/root-blue.mat`. A mutating
action that only ever accepted `Assets/` keeps refusing it, now with the
resolved path quoted so the refusal is intelligible:

```
INVALID_PATH: guid:28638862… resolves to
'Packages/com.unity.2d.psdimporter/Editor/Assets/root-blue.mat',
which is outside Assets/.
```

Read-only actions that already accept any asset path (`manage_assets deps`)
accept handles to `Packages/` for the same reason they accept those paths.

**No action gains reach it did not have.** The handle is an addressing form,
never a permission.

### D3. Sub-asset handles resolve to the sub-asset, and the action decides

`guid:<guid>:<fileId>` resolves to the specific object. Actions that operate on
a typed object (`manage_material`) use it directly and fail with the existing
type error when it is the wrong type. Actions that operate on the asset **file**
(`manage_asset_import`, `manage_assets delete`) use the containing file's path,
because an importer and a file deletion are properties of the file, not of one
object inside it — and silently importing "the sub-asset" would be a lie.

Where a sub-asset handle is used for a file-level action, the response reports
the resolved `path` so the widening is visible.

### D4. One shared entry point, not four

`AssetPathGuard` gains a resolution step used by every asset tool, so the
grammar cannot drift per tool the way the output casing did. The four tools
call the same helper they call today; only its front door changes.

### D5. Responses report the resolved path

Any action given a handle echoes the concrete `path` it acted on. An agent that
passed `guid:…` and got back a bare success has no way to confirm it hit the
asset it meant.

### D6. Not in scope

- Handles in `--new_path` or any create destination (D1).
- Accepting a bare 32-hex GUID without the `guid:` prefix: the prefix is what
  makes the grammar unambiguous, and a bare hex string is a plausible file name.
- Emitting `global_id` from asset tools by default. `find` already returns
  `guid`, which is the durable form for assets; adding GlobalObjectId to every
  row would grow payloads Hera keeps deliberately small (the wave-1b opt-in
  rule stands).

## Admission gate

1. **Failure prevented** — reproduced above: Hera emits `guid` from
   `manage_assets find` / `deps` and every asset tool rejects it, so an agent
   cannot round-trip its own output, and a recorded path silently rots when the
   asset moves. Secondarily, one of several same-typed sub-assets in a container
   cannot be addressed at all.
2. **Existing surface reuse** — no new tool, no new action, no new parameter.
   Existing `--path` parameters accept an additional, already-shipped string
   grammar (`Core/ObjectIdentity`, wave 1b).
3. **Contract and safety** — risk classes unchanged; containment and existence
   rules unchanged and re-applied after resolution (D2); handles rejected on
   create destinations (D1).
4. **Regression evidence** — connector tests for the resolution helper, plus a
   live matrix: round-trip `find` → `guid` → each accepting action; a
   `Packages/` handle refused by a mutating action and accepted by `deps`; a
   multi-sub-asset container addressed by fileId; a create destination
   rejecting a handle; an asset moved between resolution attempts still
   reachable by GUID.
5. **Surface cost** — +0 tools, +0 actions, +0 parameters. Parameter
   descriptions and the input schemas' prose change; the catalog contract hash
   will move for the touched actions and the baseline is regenerated in the
   same review.
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated and reviewed with this change.

## Verification plan

- `manage_assets find --type Material` → feed the returned `guid` straight back
  into `manage_assets deps`, `manage_material get`, `manage_asset_import get`;
  all three succeed and report the resolved path.
- Move an asset with `manage_assets move`, then resolve the same GUID again —
  still correct, while the original path now fails.
- `manage_material set --path guid:<packages asset>` refuses with the resolved
  path named; `manage_assets deps --path guid:<packages asset>` succeeds.
- A container with three embedded materials: each `guid:<guid>:<fileId>` reaches
  its own material, and the path form reaches only the first.
- `manage_assets create --path guid:…` and `copy --new_path guid:…` are refused
  with a reason that says a handle cannot name a new file.
- `manage_asset_import get --path guid:<sub-asset>` reports the containing
  file's path (D3).
- Three-bucket gate: `compile-exact-source.ps1` with zero warnings, the
  release-gate suite green per bucket, then the live matrix.
