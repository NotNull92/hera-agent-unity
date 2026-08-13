# Player Toolchain Design — writing the scripting backend and API level

Status: LOCKED (implemented). Scope: wave 11 of the editor-workflow surface queue;
resolves the read-only carve-out in `docs/SETTINGS_SURFACE_DESIGN.md` D2.

## Problem

`manage_settings get_player` reports `scripting_backend` and
`api_compatibility_level`; `set_player` refuses to change either. Wave 2 wrote
them off in one line — "their writes trigger a full domain reload and deserve
their own gate" — so an agent asked to switch a project to IL2CPP, or to move
it off a legacy .NET profile, has to fall back to hand-written `exec`.

## Live measurement (`6000.3.5f2`)

The premise turned out to be half right. Polling the heartbeat state through
each write, with a known domain reload as the calibration case:

| Action | States observed |
|---|---|
| **calibration** — touch a script, `editor refresh --compile` | `ready, compiling, reloading` |
| `SetScriptingBackend(Standalone, IL2CPP)` | `ready` only |
| `SetApiCompatibilityLevel(Standalone, NET_Unity_4_8)` | `ready, compiling, unreachable, reloading` |

**The scripting backend does not recompile anything.** It changes what a
*player* build produces; the Editor always runs Mono, so nothing about the
Editor's own assemblies changes.

**The API compatibility level does.** It swaps the reference assemblies editor
scripts compile against, so Unity rebuilds them and the Connector goes
unreachable mid-write.

An earlier attempt to measure this by counting `[Hera] HTTP server started`
console lines reported "no reload" for both. That instrument was invalid — the
calibration case also showed no increase, because the console is cleared across
a reload. The numbers above come from the calibrated instrument.

Unity offers **no API for which values are valid**: there is no
`GetSupportedScriptingBackends` or equivalent, and of the ten enum values only
`ScriptingImplementation.CoreCLR` carries `[Obsolete]` ("support is still a
work in progress and is disabled for now"). The legacy API levels — `NET_2_0`,
`NET_2_0_Subset`, `NET_Web`, `NET_Micro` — are unmarked despite being dead in
Unity 6.

## Decisions to lock

### D1. A curated enum, not the raw Unity enum

Because Unity publishes no valid set, the schema names the values that are real
in Unity 6 and rejects the rest:

| Parameter | Accepted | Maps to |
|---|---|---|
| `scripting_backend` | `mono2x`, `il2cpp` | `ScriptingImplementation.Mono2x` / `.IL2CPP` |
| `api_compatibility_level` | `net_standard`, `net_framework` | `ApiCompatibilityLevel.NET_Standard` / `.NET_Unity_4_8` |

`WinRTDotNET` is UWP-legacy and `CoreCLR` is disabled by Unity's own attribute;
the four legacy .NET profiles produce a project that no longer builds. Exposing
them because the enum happens to contain them would be handing an agent a way
to break a project with a value Unity's own UI does not offer.

`get_player` keeps reporting the **raw** value, because a project may already
hold one of the legacy values — the fixture reads `NET_Standard_2_0` — and
reporting it as something else would be a lie. Read is descriptive; write is
curated.

### D2. The API-level write responds before it recompiles

`SetApiCompatibilityLevel` makes the Editor unreachable while it rebuilds, so
the caller has to get its answer first.

This was first drafted as a deferral onto `EditorApplication.delayCall`, and
implementing it proved both halves of that wrong. `delayCall` only runs when
something wakes the editor pump — `HttpServer.ForceEditorUpdate()` does that
per incoming command, which is why the deferral in `build start` works: the CLI
keeps polling `build status` afterwards. `set_player` has no follow-up call, so
a deferred write sat unfired indefinitely, and the tool reported `applied` for
a change that never reached `ProjectSettings.asset`.

The deferral was also unnecessary. Unity starts the rebuild after the current
editor tick, so a plain inline write returns its response first anyway —
measured end to end: the response arrives, and the Editor then goes
`compiling → unreachable → reloading`. The write is therefore inline, the same
as every other `set_player` field.

The response still reports what happened: `recompile_triggered: true` for the
API level, `false` for the backend, measured rather than assumed. The action
also carries `may_reload_domain`, so the approval summary a human reads before
saying yes stops claiming the operation cannot reload the domain.

### D3. The backend write stays ordinary

No recompile means no special handling. It joins the existing `set_player`
fields under the same `Destructive` classification every `manage_settings`
`set_*` already carries, and the same `dry_run` preview.

### D4. `dry_run` covers both, and reports the recompile before it happens

`set_player --dry_run true` reports what would change **and** whether applying
it would recompile. Finding that out by doing it is exactly what an agent
cannot afford here.

### D5. Target is the active build target, and the response names it

Both settings are per-`NamedBuildTarget`. Hera writes the active target's, and
the response reports which one, because "set the backend" silently applying to
Standalone in a project currently targeting Android would be wrong in a way the
caller could not see. Switching the active target stays out of scope — it is a
separate deferred item with its own destructive profile.

### D6. Omitted fields stay unchanged

Unchanged from wave 2's `set_*` contract: a field not named in the call is not
touched.

## Admission gate

1. **Failure prevented** — a project cannot be moved to IL2CPP or off a legacy
   .NET profile through Hera; the agent falls back to `exec`, where nothing
   validates the value and nothing warns that the Editor is about to become
   unreachable.
2. **Existing surface reuse** — no new tool or action; two existing read-only
   fields on `manage_settings set_player` become writable.
3. **Contract and safety** — `Destructive` and approval-gated like every other
   `set_*`; the curated enums bound the input; `set_player` carries
   `may_reload_domain` so the approval summary is honest about the API-level
   write.
4. **Regression evidence** — live matrix: `dry_run` previews both, applying the
   backend leaves the Editor `ready` throughout, applying the API level returns
   a response and then recompiles, both round-trip through `get_player`, and a
   rejected legacy value names what is accepted.
5. **Surface cost** — +0 tools, +0 actions, +2 writable parameters, +2 response
   fields (`build_target`, `recompile_triggered`). Catalog growth measured against the standing budget.
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review.

## Verification plan

- `set_player --dry_run true --scripting_backend il2cpp` reports the change and
  `recompile_triggered: false`; the same for `--api_compatibility_level
  net_framework` reports `true`. Neither changes anything.
- Applying the backend: `get_player` reflects it, and the heartbeat stays
  `ready` across the call.
- Applying the API level: the CLI receives its response, and the Editor then
  goes `compiling` — the response is not lost.
- `--scripting_backend coreclr` and `--api_compatibility_level net_2_0` are
  rejected by the schema, naming the accepted values.
- Settings are restored to the fixture's originals afterwards.
- Three-bucket gate: zero-warning compile, release-gate suite green per bucket.

## Result

Implemented and verified on `6000.3.5f2`, with zero-warning compiles and 17/17
release-gate tests on all three buckets.

| Check | Observed |
|---|---|
| `get_player` baseline | `Mono2x` / `NET_Standard_2_0` — a value outside the write set, reported raw |
| `dry_run` backend | `applied`, `recompile_triggered: false`, nothing changed |
| `dry_run` API level | `applied`, `recompile_triggered: true`, nothing changed |
| `scripting_backend: coreclr` | rejected: `value must be one of 'mono2x', 'il2cpp'` |
| apply backend | response `recompile_triggered: false`; heartbeat stayed `ready` throughout; `get_player` → `IL2CPP` |
| apply API level | response delivered, then `compiling → unreachable → reloading`; `get_player` after reload → `NET_Unity_4_8` |
| approval summary | `may_reload_domain: true` |
| restore | one call returned both fields to the fixture baseline |

`NET_Standard` and `NET_Standard_2_0` share a numeric value, so a project
already on the modern profile reports the older name. That is Unity's naming,
not a mapping error, and is why the read side is left raw.
