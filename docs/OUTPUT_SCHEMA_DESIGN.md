# Output Schema Design — declaring what an action returns

Status: LOCKED (user approval, 2026-08-13). Scope: wave 9 of the editor-workflow surface queue; the
surface-expansion review recorded in `docs/handoffs/ACTIVE.md`.

## Problem

An agent can read a tool's input schema and know exactly what to send. It
cannot read what it will get back. Measured on the live catalog:

| | count |
|---|---|
| actions with a declared `data` schema | 81 |
| actions with an empty `{}` `data` schema | 45 |

(The first census of this said 80 / 46 because it only looked for
`data.properties`. An action returning an array declares `data.items`
instead — `scene list` is one, returning `SceneListEntry[]` — so it was
miscounted as undeclared. The corrected rule treats a non-empty `properties`,
an `items`, or a non-`object` `type` as declared.)

An empty schema is not a contract breach — the object is open, so returning
fields is permitted — but it tells the caller nothing. Every one of these
forces the agent to call the action once to discover its shape, or to guess.

## What the 45 actually are

Two groups, and they need opposite treatment.

### Group A — message-only, already accurate (11)

`manage_editor play / stop / pause / set_active_tool / add_tag / remove_tag /
add_layer / remove_layer` and `profiler enable / disable / clear` return
`new SuccessResponse("…")` with **no `data` at all**. An empty `data` schema is
the honest description of that. Declaring a result type here would invent a
payload that does not exist.

These need no change. Recording that they were examined and deliberately left
alone is the point — otherwise the next audit re-opens them.

### Group B — real payloads, undeclared (34)

Measured returns, sampled live:

| Surface | Returned keys |
|---|---|
| `console` | `entries, total_in_console, matched, returned, since, last_cursor, truncated` |
| `describe_type` | 13 keys (`name, full_name, namespace, assembly, kind, is_static`, …) |
| `find_gameobjects` | `total, returned, offset, limit, has_more, results` |
| `unity_docs` | `title, signature, summary, unity_version, docs_version` |
| `describe_shader` | `total, truncated, shaders` |
| `scene hierarchy` | `scenes, node_count, truncated` |
| `build list_targets` | `targets` |
| `build status` | `state, last_report` |

plus `exec`, `screenshot`, `detect_assets`, `reserialize`, `refresh_unity`,
`list_assemblies`, `find_method`, `game_feel`, `ui_slop`,
`build set_settings`, `manage_animation get_clip` / `get_controller`,
`manage_ui create`, and the 13 `input` actions.

## Decisions to lock

### D1. Group A is closed by documentation, not by code

The eleven message-only actions keep their empty `data` schema. This document
is the record that they were checked; a future audit reads it instead of
re-deriving. `ACTIVE.md` stops listing them as a gap.

### D2. Group B is declared in ROI order, and this wave takes the first slice

Declaring 34 result types at once is a large, unreviewable diff whose payload
cost lands in one step. The wave takes the surfaces an agent reads most and
whose shapes are stable:

**Planned (12):** `console`, `find_gameobjects`, `describe_type`,
`find_method`, `list_assemblies`, `describe_shader`, `unity_docs`,
`scene hierarchy`, `build status`, `build list_targets`,
`build set_settings`, `manage_animation get_clip`.

**Shipped (10).** `describe_type` and `scene hierarchy` were cut when the
budget in D5 was applied — see the outcome below.

**Deferred with reasons:** `exec` (payload is user-code-shaped and genuinely
open — declaring it would be a lie), `screenshot` (several modes with
different shapes; needs its own pass), the 13 `input` actions (responses are
built in `InputQa*` helpers, so declaring them means touching the QA backends
in the same change), `game_feel` / `ui_slop` (bundle-driven records that move
with the data set), `detect_assets` / `reserialize` / `refresh_unity` (thin
and low-traffic), `manage_ui create`, `manage_animation get_controller`.

### D2b. Tool-level shapes use the existing nested-`Result` convention

A tool without actions takes its output schema from a nested type named
`Result` (`toolType.GetNestedType("Result")` in `ToolContractRegistry`). The
seven tool-level entries in this slice therefore need a nested class, not a new
attribute — the mechanism already exists and `profiler` already uses it.

### D3. Declared shapes are derived from live responses, not from reading code

Each result type is written against an actual captured response from the gate
fixtures and verified field-by-field by the conformance sweep from wave
`0.0.103`, which already compares every declared result type's serialization
against its schema. A shape guessed from the handler and never executed is how
`build status` ended up undeclared in the first place.

### D4. Optional fields are declared optional

Several payloads omit fields conditionally (`truncated` only when true,
`last_report` only when a build has run). Those properties carry
`NullValueHandling.Ignore` so the schema marks them optional rather than
promising a field that will not always arrive.

### D5. Payload budget is measured, not assumed

Existing declared output schemas run 312 bytes median, 395 mean, 1004 max.
Twelve new ones project to roughly 4–5 KB before profile duplication. The
real number is measured with `catalog-payload-report` and reviewed in this
change; if a profile grows more than the tool descriptions did in wave 8
(1930 bytes), the slice is cut rather than waved through.

### D5b. Outcome: the budget cut two of the twelve

Declaring all twelve grew `full` by 3996 bytes and `diagnostics` by 2082 —
both over the 1930-byte precedent. Per-schema sizes located the cause:

| Declaration | Bytes | Profiles |
|---|---:|---|
| `describe_type` | 1299 | diagnostics, full |
| `scene hierarchy` | 859 | core, full, scene |
| `manage_animation get_clip` | 533 | full, scene |
| everything else | 115–273 each | |

Cutting those two brought every profile inside the budget: `full` 1904,
`diagnostics` 816, `scene` 738, `core` 478, `testing` 240, `assets` 110.

They are the two highest-value declarations in the slice — the most complex
shapes are exactly the ones an agent most needs described — so they lead the
next slice, and that slice has to answer the budget question directly rather
than inherit this one's precedent.

`scene hierarchy` carries a second constraint found here: its node tree is
recursive, and `SchemaUtility` refuses recursive DTO graphs by design
("Recursive DTO graphs are unsupported"). Declaring it means either typing the
nested children as open `object[]` — the node's own fields declared, the
nesting not — or teaching the generator `$ref`. That is a decision for the next
slice, not a detail.

### D6. No behavior change

This wave adds `ResultType` declarations and, where needed, the result classes
themselves. No handler changes what it returns. The live matrix is a
before/after diff of actual payloads showing them byte-identical.

## Admission gate

1. **Failure prevented** — an agent cannot know an action's return shape
   without calling it; 45 of 126 actions publish nothing. The same gap let
   `build status` ship with an undeclared payload, found only by the wave
   `0.0.103` sweep.
2. **Existing surface reuse** — no new tool, action, or parameter. Only the
   declared output schema of existing actions changes.
3. **Contract and safety** — risk classes, approvals, and behavior unchanged
   (D6). Declaring a schema cannot loosen anything: the envelope stays open.
4. **Regression evidence** — the wave `0.0.103` conformance test covers every
   newly declared type automatically; plus a before/after payload diff proving
   no response changed.
5. **Surface cost** — measured in D5 and reviewed here; the slice is sized to
   stay under the wave-8 precedent.
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review, with the growth reported per profile.

## Verification plan

- Capture the live response of each in-scope action before the change; after
  the change, capture again and diff — byte-identical (D6).
- The result-type conformance test passes with the new types included, and its
  reported count rises from 60 by the number of types added.
- `catalog-payload-report --compare` reports the per-profile growth; the number
  is recorded in the changelog and the inventory rather than only in a hash.
- Three-bucket gate: zero-warning compile, release-gate suite green per bucket.
