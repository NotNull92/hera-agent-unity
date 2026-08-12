# Target Resolution Design — durable identities and reported strategies

Status: LOCKED (user approval, 2026-08-12). Scope: wave 1b of the
editor-workflow surface queue.

## Problem

Hera addresses Unity objects by two forms today: an `instance_id` int and a
path (scene hierarchy path, or an `Assets/` path in ObjectReference values).
Live evidence of the gaps, measured on `6000.3.5f2` during wave 1a:

1. **Instance ids die on domain reload.** A `find_gameobjects` id became
   unresolvable minutes later because a package re-resolve reloaded the
   domain (`no object for instance_id=65781870`). Every id Hera hands out is
   a time bomb the agent cannot see; after any compile, play-mode switch, or
   package operation the agent must re-query and re-correlate by name.
2. **Sub-assets are unaddressable.** `ResolveReference` loads the *main*
   asset at a path. A sprite inside a sliced spritesheet, a material inside
   an FBX, or a clip inside a controller cannot be assigned to an
   ObjectReference field at all; the workaround is `exec`.
3. **Failures hide the strategies tried.** A bare-string target that fails
   reports only the last interpretation ("no GameObject at path"), so an
   agent that passed a guid-looking or stale-id string gets a misleading
   error and retries blind.
4. **Responses emit only the volatile identity.** Nothing Hera returns can
   be stored across a domain reload; there is no durable handle an agent can
   keep in its plan.

Unity's own durable identities already exist and round-trip live (verified on
`6000.3.5f2`): asset GUIDs + local file ids, and `GlobalObjectId`
(`GlobalObjectId_V1-2-<sceneGuid>-<objectId>-<prefabId>`), whose
`TryParse`/`GlobalObjectIdentifierToObjectSlow` pair resolved a scene object
back by value.

## Decisions to lock

### D1. Input grammar — which new target forms are accepted

Accepted string forms after this change (existing forms unchanged):

| Form | Example | Resolves to |
|---|---|---|
| digits (existing) | `104194` | loaded object by instance id |
| hierarchy path (existing) | `/Canvas/Panel` | scene GameObject |
| `Assets/` path (existing, reference values) | `Assets/Mat/Floor.mat` | main asset |
| **`guid:<32hex>`** (new) | `guid:8c9cfa26abfee488c85f1582747f6a02` | main asset by GUID |
| **`guid:<32hex>:<fileId>`** (new) | `guid:8c9c…:21300000` | **sub-asset** by GUID + local file id |
| **`GlobalObjectId_V1-…`** (new) | `GlobalObjectId_V1-2-…-1542870718-0` | asset or scene object, durable across domain reloads |

- All new forms are prefixed/self-describing, so the existing bare-string
  interpretation order (digits → id; `Assets/` → asset; else hierarchy path)
  is untouched. No ambiguity is introduced and nothing breaks.
- The GlobalObjectId form uses Unity's own `ToString`/`TryParse` format
  verbatim — no invented syntax to maintain.

**Recommendation: accept all three new forms.**

### D2. Application surface — where the grammar applies in wave 1b

| Surface | Gains | Rationale |
|---|---|---|
| `SerializedPropertyValue.ResolveReference` (ObjectReference values in `manage_components set/add`, prefab/UI property paths) | `guid:`, `guid::fileId`, GlobalObjectId | Highest ROI: unlocks sub-asset assignment (problem 2) |
| `TargetResolver` (`manage_gameobject`, `manage_components`, `manage_ui`, `scene hierarchy --root`, `screenshot --target`, input QA) | GlobalObjectId | Durable scene-object targeting (problem 1); `guid:` deliberately excluded — these tools operate on scene objects, and a main-asset handle is not a valid target for them |
| `manage_editor set_selection` targets | all three | Selection accepts both assets and scene objects by design |
| Path parameters of asset tools (`manage_material`, `manage_prefab`, `manage_asset_import`, `manage_assets`) | none in wave 1b | Additive follow-up once the grammar is proven; avoids touching `AssetPathGuard` semantics now |

**Recommendation: the three listed surfaces now, asset-tool paths deferred.**

### D3. Output — how durable identities are emitted

Emitting a GlobalObjectId on every node would bloat the payloads Hera keeps
deliberately small. Opt-in only:

- `find_gameobjects --fields` gains a selectable `global_id` field (the
  fields mechanism already exists; no default change).
- `manage_editor get_selection` entries gain `global_id` only when a new
  `durable=true` parameter is passed.
- `scene hierarchy` stays as shipped (id/name/active); agents that need a
  durable handle for one node fetch it via `find_gameobjects --fields`.

**Recommendation: as above — zero default-payload growth.**

### D4. Failure reporting — strategies tried

When every interpretation of a target fails, the error keeps its existing
code (`TARGET_NOT_FOUND` / `OBJECT_NOT_FOUND`) and message, and gains a
`data.tried` array naming each strategy and its individual failure:

```json
{ "code": "TARGET_NOT_FOUND",
  "message": "…",
  "data": { "tried": [
    { "form": "instance_id", "error": "no object for instance_id=65781870" },
    { "form": "hierarchy_path", "error": "no GameObject at path '65781870'" }
  ] } }
```

Single-form inputs (an int `instance_id` parameter, a prefixed `guid:` form)
keep today's single-error shape — `tried` appears only where more than one
interpretation was attempted. Error codes and messages stay byte-compatible
for existing flows.

### D5. Compatibility guarantees

- The int `instance_id` contract is untouched (locked design).
- Existing bare-string interpretation order is unchanged; new forms are
  prefix-gated. No existing call changes behavior.
- Strict schemas stay `string`/`integer`; only parameter descriptions and
  the shared resolution helpers change. Catalog baseline is regenerated for
  the description growth and reviewed.
- `GlobalObjectId` APIs exist across all three supported buckets (Unity 6+);
  the `*Slow` resolution cost is irrelevant at tool-call frequency.

### D6. Validation plan

EditMode contract additions plus a live matrix on `6000.3.5f2`:

1. `guid:` main-asset assignment to an ObjectReference field, and the same
   asset addressed by `Assets/` path — identical result.
2. **Sub-asset**: assign a sprite from a sliced sheet via `guid:<g>:<fileId>`
   (fileId harvested via `exec` ground truth), then read the property back.
3. **The original failure, reversed**: capture a scene object's
   GlobalObjectId, force a domain reload (`editor refresh --compile` after a
   script touch, or package re-resolve), then resolve the same string —
   must succeed where the instance id fails.
4. `tried[]` shape on a bare string that matches nothing.
5. `set_selection` with a mixed id + path + guid + GlobalObjectId list.
6. Three-bucket gate before release (grammar is version-agnostic; the gate
   re-runs the GlobalObjectId round trip per bucket).

## Implementation shape (informative, not gated)

One new shared helper in `Core/` (working name `ObjectIdentity`):
`TryParseDurable(string, out Object, out string err)` recognizing the two
prefixed forms + GlobalObjectId, and `DurableIdOf(Object)` producing the
GlobalObjectId string. `TargetResolver`, `SerializedPropertyValue`, and
`ManageEditor.SetSelection` call it first for prefixed forms; `tried[]`
assembly lives where multi-form interpretation happens. No new tool, no new
action; `find_gameobjects` grows one selectable field and `get_selection`
one optional parameter. Connector-only (`0.0.95`), CLI untouched unless help
text changes.
