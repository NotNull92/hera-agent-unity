# Asset Dependency Design — what uses this, and what does it use

Status: LOCKED (user approval, 2026-08-13). Scope: wave 7 of the
editor-workflow surface queue, and the resolution of survey candidate Q9.

## Problem

Hera cannot answer either dependency question about an asset.

**"What does this use?"** has no tool. An agent asked why a prefab pulls in a
20 MB texture, or which material a mesh renderer resolves to, has to write
`exec` code.

**"What uses this?"** is worse, because it is the question that precedes a
destructive act. Before `manage_assets delete` or `manage_assets move`, the
right question is which scenes and prefabs will break — and Hera has no way to
ask it. The agent either deletes blind or refuses to act.

## Q9 resolution: Unity Search is not the answer

The survey proposed exposing Unity Search (`manage_assets search`). Measured
on `6000.3.5f2` against a project with a settled `Assets` search index, its
query space is a **subset** of what Hera already answers, not a superset:

| Query | Existing Hera | Unity Search |
|---|---|---|
| `t:Material` | `manage_assets find --type Material` → 73 hits including `Packages/` | 1 hit, `Assets/` only |
| name / path | `manage_assets find --filter` | equivalent |
| scene objects | `find_gameobjects` — type, tag, layer, component, path glob, pagination, projections | name match only |
| menu items | `menu list --filter` | equivalent |
| `dep:` forward deps | — | **0 hits, even with a settled index** |
| `#property` filters | — | **0 hits — the default index carries no properties** |
| **`ref:` reverse deps** | **none** | 2 ms, correct |

Only `ref:` is unique, and it comes with a hazard: **the index lags the asset
database**. Queried immediately after creating assets in the same call, asset
queries returned zero; the same queries returned correct results once the
index settled. An agent that writes an asset and immediately asks about it
gets a silently empty answer.

Worse, it is **intermittent**. The same create-then-query-in-one-call probe,
run during the release gate, reported for `ref:` against a material a prefab
had just been saved referencing:

| Bucket | Unity Search `ref:` | AssetDatabase scan | Truth |
|---|---|---|---|
| `6000.0.35f1` | 1 | 1 | 1 |
| `6000.3.5f2` | **0** | 1 | 1 |
| `6000.5.6f1` | **0** | 1 | 1 |

An intermittently empty answer is more dangerous than a consistently broken
one: it passes casual testing and fails the run that matters.

The official Unity CLI's `search` command is a thin passthrough
(`SearchService.Request(query, SearchFlags.Synchronous)`, capped at 200) with
no index handling, and its only test asserts the call succeeds and returns
valid JSON without checking the result count — so a query returning nothing
passes it. It is not a solution to copy, and that package ships no dependency
command at all across its 140 commands.

**Unity Search is therefore rejected**, and with it the index-freshness
hazard. Both directions are computed from `AssetDatabase`, which is
authoritative and never stale.

## Live cost ground truth (`6000.3.5f2`)

| Fact | Measured |
|---|---|
| `AssetDatabase.GetDependencies(path, recursive)` | exact, index-free, sub-millisecond for one asset |
| `GetDependencies(string[], bool)` batch overload | exists, but returns the **union** — a dependency cannot be attributed back to its owner, so it is useless for a reverse lookup |
| Reverse scan over every asset path (10,330 files, `Assets/` + `Packages/`) | 1541 ms |
| Reverse scan scoped to `Assets/` | 21 ms in this fixture (9 files) |
| Real-project `Assets/` file counts on this machine | 436 / 874 / 3465 / 8380 |
| Implied `Assets/`-scoped scan at ~0.15 ms per asset | roughly 65 ms – 1.3 s across those projects |
| `SearchDatabase` exposes `ready` / `updating` / `needsUpdate` | index freshness *is* observable — recorded, but unused, since no Search path survives |

## Decisions to lock

### D1. One action on `manage_assets`, direction required

`manage_assets deps --path <asset> --direction <forward|reverse>`, `ReadOnly`.

`direction` is **required**. The two answer opposite questions, both are
plausible readings of "dependencies", and picking one silently would make the
answer to "is anything still using this?" look like "here is what it uses".

No new tool: this is an AssetDatabase query and `manage_assets` owns those.

### D2. Forward — `GetDependencies`, exact

`--recursive` (default `false`) selects the direct or transitive set. The
asset's own path is excluded from the result so the list is purely "what this
uses".

### D3. Reverse — an exact scan, scoped to `Assets/` by default

Every candidate's direct dependencies are checked against the target. Exact,
index-free, and correct the instant an asset is written.

`--scope` (`assets` default, `all` to include `Packages/`) exists because
package contents are immutable — nothing the agent does can change what a
package references, so scanning them is usually paid-for-nothing. The
measured difference is 21 ms versus 1541 ms in this fixture.

The response always reports `scanned` and `elapsed_ms`. A scan whose cost
grows with the project must not hide that cost.

### D4. Bounded output, honest truncation

`--limit` (default 100, max 1000) with `total`, `returned`, and `truncated`,
matching `manage_assets find` and `menu list`. A truncated reverse result
carries an `agent_hint` saying the list is incomplete — the difference between
"three things reference this" and "at least three" decides whether a delete is
safe.

### D5. Folders are skipped, the target is excluded

Folders are not scanned (they have no dependencies of their own) and the
target never appears in its own result in either direction.

### D6. Missing target fails, it does not return empty

A `--path` with no asset returns `ASSET_NOT_FOUND`. An empty reverse result
must mean "nothing references this", never "that path was a typo" — the whole
point of the action is to make a delete decision safe.

## Admission gate

1. **Failure prevented** — deleting or moving an asset that scenes and prefabs
   still reference, with no way to check first; and no way to explain what an
   asset pulls in.
2. **Existing surface reuse** — one action on the tool that already owns
   AssetDatabase queries. Unity Search was measured and rejected (its query
   space is a subset of `manage_assets find` + `find_gameobjects` + `menu
   list`, `dep:` and `#property` return nothing, and `ref:` is index-lagged).
3. **Contract and safety** — `ReadOnly`; no approval, no ledger impact,
   no Unity version gate (`AssetDatabase.GetDependencies` predates Unity 6).
4. **Regression evidence** — connector contract and safety expectations for
   the new action, plus a live matrix on a disposable fixture: a material
   referenced by a prefab must appear in the prefab's forward set and the
   prefab must appear in the material's reverse set, recursive versus direct
   must differ, and an unreferenced asset must return an empty reverse set
   while a bad path returns `ASSET_NOT_FOUND`.
5. **Surface cost** — +0 tools, +1 action, +4 parameters (`direction`,
   `recursive`, `scope`, `limit` reused from the existing `find` vocabulary).
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review.

## Verification plan

- Fixture asset graph: `Q9Mat.mat` ← referenced by `Q9Cube.prefab`, which also
  pulls in a shader.
- `deps --path Q9Cube.prefab --direction forward` lists the material;
  `--recursive` additionally reaches the shader; neither lists the prefab
  itself. (Confirm whether Unity includes the queried path in the recursive
  result and strip it if so — measured `recursive:true` returned 2 entries for
  a graph with one direct dependency.)
- `deps --path Q9Mat.mat --direction reverse` lists exactly the prefab, and
  reports `scanned` and `elapsed_ms`.
- Reverse on a freshly created, unreferenced asset returns an empty list — the
  case Unity Search got wrong, verified in the same call that creates it.
- `--scope all` returns a superset and a visibly larger `scanned`.
- A nonexistent `--path` returns `ASSET_NOT_FOUND`, not an empty list.
- Three-bucket gate: `compile-exact-source.ps1` with zero warnings, then
  `HeraAgent.Editor.Tests` green in each bucket, then the live matrix.
