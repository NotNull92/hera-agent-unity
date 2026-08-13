# Prefab Override Design — closing the instance-to-asset loop

Status: LOCKED (user approval, 2026-08-13). Scope: wave 6 of the
editor-workflow surface queue.

## Problem

`manage_prefab` can create a prefab, instantiate one, and add or remove a
component on the asset root. It cannot do anything with the **difference**
between an instance and its asset — which is where prefab work actually
happens.

An agent asked to "tweak the player prefab" instantiates it, edits the
instance with `manage_components`, and then has nowhere to go: there is no
way to see what it changed, no way to push the change back to the asset, and
no way to throw it away. The edit stays stranded on one scene instance. The
same gap makes the reverse impossible — an agent cannot tell whether an
instance it is about to hand back is faithful to its prefab or silently
carrying overrides.

Two smaller holes sit next to it:

- **A component on a prefab child cannot be edited at all.** `add_component`
  and `remove_component` target the prefab root; `manage_components` only
  reaches scene objects. Editing a child today means instantiate → edit →
  apply → destroy, which needs the apply this document is adding *and*
  dirties the user's open scene along the way.
- **`create` silently produces a Variant.** Measured on `6000.3.5f2`:
  `SaveAsPrefabAsset` on a prefab instance returns a Variant, not a regular
  prefab. `manage_prefab create --source <an instance>` therefore already
  creates variants and never says so.

## Live API ground truth (`6000.3.5f2`)

| Fact | Measured |
|---|---|
| Override records | `GetObjectOverrides`, `GetAddedComponents`, `GetRemovedComponents`, `GetAddedGameObjects`, `GetRemovedGameObjects` — each element exposes the same `Apply()` / `Revert()` / `GetAssetObject()` trio |
| `GetObjectOverrides(root, includeDefaultOverrides: false)` | **omits the instance root's own Transform and name.** A run that changed root position, root name, a Rigidbody mass, and a child name reported only the Rigidbody and the child |
| `GetPropertyModifications` | reports all 13 raw modifications from that same run, including the omitted root Transform and both names |
| Raw C# setters | do **not** register an override. `rb.mass = 42f` left `HasPrefabInstanceAnyOverrides` false; the same edit through `SerializedObject` + `ApplyModifiedProperties` did register — which is the path `manage_components` already uses |
| Per-record `Revert()` | reverted only the Rigidbody and left the child rename in place |
| `ApplyPrefabInstance` | wrote mass, the added component, the added child, and the removed child to the asset in one call; the instance then reported no overrides |
| `UnpackPrefabInstance` modes | `OutermostRoot`, `Completely`; after unpacking, the object reports `NotAPrefab` |
| `SaveAsPrefabAsset(instance, path)` | produces `PrefabAssetType.Variant` |
| `HierarchyPath.Build` inside `LoadPrefabContents` | returns ordinary `/Root/Child` paths, so the existing path convention addresses prefab contents unchanged |

## Decisions to lock

### D1. `list_overrides` — see the difference before acting on it

New ReadOnly action taking `--target` (a scene prefab instance). Returns the
five record kinds, each entry carrying the hierarchy path and type of the
instance object it belongs to:

```
{instance_root, asset_path, asset_type, status, has_overrides,
 object_overrides:[{path, type}], added_components:[{path, type}],
 removed_components:[{path, type}], added_gameobjects:[{path, sibling_index}],
 removed_gameobjects:[{parent_path, name}]}
```

`--include_default` (default `false`) switches `GetObjectOverrides` to include
default overrides. The default matches what Unity's own Overrides dropdown
shows; the flag exists because "no overrides" from the default view does not
mean the instance is identical to its asset — measured above, a moved and
renamed root reports nothing without it.

This action is the precondition for the rest: `apply` rewrites an asset that
every other instance in the project inherits from, so the agent has to be able
to see what it is about to push.

### D2. `apply` and `revert` — whole instance, approval-gated

`apply` calls `ApplyPrefabInstance`; `revert` calls `RevertPrefabInstance`,
both with `InteractionMode.AutomatedAction`. Both are `Destructive`, so both
inherit `RequiresConfirmation` — `apply` rewrites a shared asset, `revert`
discards instance work that has no other copy.

Per-record apply/revert is **deferred**, not designed away. The record objects
support it, but addressing one record needs a stable identifier for "the
Rigidbody override on /Player/Arm", and no such identifier exists today that
survives a reload. Whole-instance apply and revert are what the Inspector's
Apply All / Revert All buttons do and cover the workflow that is currently
impossible. If per-record turns out to be needed, `list_overrides` already
returns the paths that would key it.

### D3. `unpack` — mode is explicit, never defaulted silently

`unpack --target <instance> --mode <outermost|completely>` maps to
`PrefabUnpackMode.OutermostRoot` / `Completely`. `mode` is **required**: the
two differ in whether nested prefab instances survive, the difference is
invisible in a flat project and destructive in a nested one, and there is no
correct default to guess. `Destructive`, approval-gated; the response reports
the roots that remain prefab instances afterwards.

### D4. Targets resolve to the outermost instance root, and say so

`ApplyPrefabInstance` and friends require an instance **root**; passing a
child throws. All four new actions resolve `--target` through the shared
`TargetResolver` (so instance ids, hierarchy paths, and durable handles all
work), then walk to `GetOutermostPrefabInstanceRoot` and act on that. Every
response reports the `instance_root` it used, and `list_overrides` reports the
same root, so the agent sees the retarget before the destructive call rather
than after. A target that is not part of any prefab instance fails with
`NOT_A_PREFAB_INSTANCE`.

### D5. `--child` on `add_component` / `remove_component`

Optional relative hierarchy path selecting a descendant inside the loaded
prefab contents; the root stays the default. This reuses the existing
`LoadPrefabContents` path — no prefab stage, no scene side effects — and
`HierarchyPath` addresses contents unchanged (measured). Without it, editing a
component on a prefab child requires instantiating into the user's open scene
and cleaning up afterwards.

### D6. `create` reports `asset_type`, and `--source` stops missing inactive objects

`create`'s result gains `asset_type` (`Regular` | `Variant` | `Model`), because
saving from an instance already produces a Variant and the response currently
claims nothing. No new action: variant creation is the existing action's
behavior, not a missing one.

Separately, `create --source` resolves through `GameObject.Find`, which skips
inactive objects, while the rest of Hera uses the inactive-aware
`HierarchyPath.Find`. `create --source /DisabledThing` fails today for no
reason a caller can see. Both local resolvers in `ManagePrefab` are replaced
with the shared ones.

## Admission gate

1. **Failure prevented** — an instance edit cannot be pushed to its asset,
   discarded, or even inspected; a component on a prefab child cannot be
   edited without polluting the open scene; `create` from an instance produces
   a Variant without saying so; `create --source` cannot see inactive objects.
2. **Existing surface reuse** — all four new actions land on `manage_prefab`;
   `--child` and `asset_type` extend existing actions rather than adding new
   ones. `create_variant` was rejected because `create` already does it.
   Per-record apply/revert was deferred for lack of a stable record id.
3. **Contract and safety** — `list_overrides` is `ReadOnly`; `apply`,
   `revert`, and `unpack` are `Destructive` and therefore approval-gated.
   `unpack` requires an explicit `mode`.
4. **Regression evidence** — connector contract and safety expectations for
   the new actions, plus a live matrix on a disposable fixture covering an
   override round trip (edit → list → apply → verify asset → edit → revert →
   verify instance), both unpack modes, a child component edit, and the
   inactive-source fix.
5. **Surface cost** — +0 tools, +4 actions, +4 parameters (`target`, `child`,
   `include_default`, `mode`), +1 response field on `create`.
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review.

## Verification plan

- Round trip on a disposable fixture: create a prefab with a child, instantiate,
  edit through `manage_components` (the `SerializedObject` path that actually
  records overrides), `list_overrides` shows exactly those, `apply` writes them
  to the asset, a fresh instantiate shows the change, a further edit followed by
  `revert` restores the instance.
- `list_overrides` with and without `--include_default` on an instance whose
  root has been moved and renamed — the flag must be the difference between an
  empty list and a populated one.
- `--target` given a child object returns the outermost root in
  `instance_root`; `--target` given a plain scene object returns
  `NOT_A_PREFAB_INSTANCE`.
- `unpack --mode outermost` on a prefab containing a nested prefab leaves the
  nested instance connected; `--mode completely` does not.
- `add_component --child /Root/Child` lands on the child in the asset and
  leaves the open scene untouched.
- `create --source` on an inactive GameObject succeeds; the result reports
  `asset_type: Variant` when the source is an instance.
- Three-bucket gate: `compile-exact-source.ps1` with zero warnings, then
  `HeraAgent.Editor.Tests` green in each bucket, then the live matrix.
