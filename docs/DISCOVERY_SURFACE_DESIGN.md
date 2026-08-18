# Discovery Surface Design — knowing what exists before running it

Status: LOCKED (user approval, 2026-08-13). Scope: wave 5 of the
editor-workflow surface queue.

## Problem

Two surfaces make an agent commit to an expensive, irreversible operation
without letting it check what it is committing to first.

**Tests.** `test --filter Foo` builds an NUnit filter from a single string and
runs it. When the string matches nothing, the run completes with zero tests
and Hera reports **success**. Measured on `6000.3.5f2`:

```
> hera-agent-unity test --mode EditMode --filter NoSuchTestNameZZZ
{"total":0,"passed":0,"failed":0,"skipped":0,"failures":[],"passes":[]}
```

Exit status 0, no error code. An agent that mistypes an assembly or class
name — or runs before that assembly compiles — is told its feature is
verified. There is no way to enumerate the tests that exist, and no way to
select tests by NUnit category even though the framework supports it. A run
that hangs cannot be cancelled: the pending-run file keeps every later `test`
call returning `TEST_RUN_ALREADY_RUNNING` until the Editor is killed.

**Packages.** `manage_packages add` takes an identifier and starts an
asynchronous job that can reload the domain. A wrong identifier costs that
whole round trip to learn "not found", and nothing tells the agent which
versions of a package are compatible with *this* Editor. There is no way to
discover a package's exact id from a partial name.

## Live API ground truth (`6000.3.5f2`)

| Fact | Measured |
|---|---|
| `TestRunnerApi.RetrieveTestList(TestMode, Action<ITestAdaptor>)` | exists; **asynchronous** — the callback had not fired when the calling frame returned |
| Test tree shape | root suite → assembly suite (`FullName` is the **full .dll path**, `IsTestAssembly=true`) → namespace suites → class suite → test cases |
| `ITestAdaptor.Categories` | `["Uncategorized"]` on an uncategorised test; actual names otherwise; empty on suites |
| `Filter` fields | `testMode`, `testNames`, `groupNames`, **`categoryNames`**, **`assemblyNames`**, `targetPlatform` |
| `TestRunnerApi.Execute` | returns a `string` run guid (currently discarded by Hera) |
| `TestRunnerApi.CancelTestRun(string guid)` | exists |
| `Client.Search(name)` | **exact-name lookup only** — `Search("navigation")` failed `NotFound`; `Search("com.unity.cinemachine@2.9.7")` resolved |
| `Client.SearchAll()` | 174 packages in 3.9–5.0 s, `versions.compatible` / `versions.all` / `recommended` / `keywords` / `description` all populated |
| `Client.Resolve()` | returns `void` — no request object, no completion signal |
| `EditorApplication.SignalTick` | **non-public** |
| Unfocused editor tick rate | heartbeat writes at 1001–1109 ms against a 1.0 s target — no meaningful throttle |

## Decisions to lock

### D1. `test list` — enumerate before running

New `list` action on the existing `test` tool. `RetrieveTestList` is
asynchronous, so the handler awaits `EditorUpdate.Next()` until the callback
lands, the same pattern `manage_packages list` uses for `Client.List`.

Payload shape follows the `menu list` precedent, for the same reason (a real
project has thousands of tests and must not flood the agent's context):

- **No filter** → per-assembly counts plus the category histogram. Assembly
  names are reduced from the reported .dll path to the bare assembly name,
  because that is what `--assembly` accepts.
- **With `--filter` / `--category` / `--assembly`** → bounded flat list of
  leaf test full names (`--limit`, default 200), with `total`, `returned`,
  and `truncated`, and an `agent_hint` when truncated.

`Uncategorized` is dropped from output rather than echoed — it is the test
framework's placeholder, not a category the agent can filter on.

`list` is `ReadOnly` and takes `--mode` (default `EditMode`), since the two
modes enumerate different trees.

### D2. Honest zero-match runs

When a run finishes with `total == 0` **and** a selector (`--filter`,
`--category`, or `--assembly`) was supplied, the result is
`NO_TESTS_MATCHED` (error), not a success envelope. An unfiltered run of a
project with no tests stays a success — that is a true statement about the
project.

This changes an existing response contract: a case that previously reported
success now reports failure. That is the point; the old answer was wrong.

### D3. Category and assembly selectors

`test` gains `--category` and `--assembly`, mapping to `Filter.categoryNames`
and `Filter.assemblyNames`. `--filter` keeps its current meaning
(`testNames` + `groupNames`) so existing invocations are unaffected. All
three combine, as NUnit intersects them.

### D4. `test cancel` — best-effort NUnit cancel, guaranteed unstick

New `cancel` action. Two layers, because they fail independently:

1. If the pending-run record carries the NUnit run guid, call
   `CancelTestRun(guid)` and report whether the framework accepted it.
2. **Always** write an interrupted result file and clear the pending record.

Layer 2 is what actually matters: it is the only way out of the permanent
`TEST_RUN_ALREADY_RUNNING` lockout today. The guid is added to the existing
pending-run JSON, which already survives domain reloads, so cancel works
across the PlayMode reload. `cancel` is `Write` and idempotent — cancelling
when nothing is running reports `was_running: false`, not an error.

### D5. `manage_packages search` — one path, `SearchAll` + local filter

New `search` action taking `--filter` (required) and `--limit`
(default 25, max 174 in practice).

`Client.Search` is rejected as the backing call despite its name: it is an
exact-id lookup, so it cannot answer "what is the navigation package
called?" — the actual question. `SearchAll()` returns the whole visible
registry in ~4 s; filtering 174 entries locally over name, display name,
description, and keywords costs nothing and subsumes exact lookup (an exact
id is a substring of itself). One code path, no query-shape branching.

Per hit: `name`, `version`, `display_name`, `description` (first sentence,
capped), `compatible_versions` (from `versions.compatible`),
`recommended`, `deprecated`. `compatible_versions` is the field that
prevents the failure — it names exactly what this Editor will accept before
the agent spends a domain reload on `add`.

`search` is `ReadOnly` and reuses `ListAsync`'s 60 s deadline and
`EditorUpdate.Next()` pumping.

### D6. Dropped from this wave, with reasons

| Candidate | Why not |
|---|---|
| `menu list` | Already shipped. `menu list [--filter] [--limit]` exists with group-count and bounded-flat-list modes. |
| `set_autotick` | No failure to prevent. Measured unfocused heartbeat cadence is 1001–1109 ms against a 1.0 s target, so the Editor is not meaningfully throttled at Hera's granularity, and `HttpServer.ForceEditorUpdate` already repaints on unfocused command dispatch. `EditorApplication.SignalTick` is non-public, so building it would mean a reflection binding for no measured gain. |
| `manage_packages resolve` | `Client.Resolve()` returns `void`, so the action has no completion signal to report. A contract-shaped answer is still constructible — measured 2026-08-18, an external caller returned `status: completed, applied: true` in all three buckets while `packages-lock.json` kept its byte-identical hash and mtime — but that field asserts an outcome the API cannot confirm, which is the thing worth refusing. Hera also has no path that hand-edits `manifest.json`; `manage_packages` exists precisely so that never happens. No failure evidence. |
| `manage_assets import` | An agent copying an external file into `Assets/` already has its own filesystem tools plus `editor refresh`. Fails the "existing surface reuse" gate. |

## Admission gate

1. **Failure prevented** — a mistyped or not-yet-compiled test selector
   reports success with zero tests run (reproduced above); a hung test run
   locks out every later `test` call until the Editor is killed; a wrong
   package identifier costs an async job and a domain reload to discover,
   and version compatibility is unknowable before installing.
2. **Existing surface reuse** — every item lands as an action or flag on an
   existing tool. No new top-level tool. Four candidates were dropped for
   failing this test (D6).
3. **Contract and safety** — `test list` and `manage_packages search` are
   `ReadOnly`; `test cancel` is `Write` and idempotent; `NO_TESTS_MATCHED`
   is a new error code on an existing action. No approval-gated risk change.
4. **Regression evidence** — connector contract tests for the new actions,
   plus a live matrix on a disposable fixture carrying a purpose-built test
   assembly with mixed categories.
5. **Surface cost** — +0 tools, +3 actions (`test list`, `test cancel`,
   `manage_packages search`), +3 parameters (`--category`, `--assembly`,
   `--limit` on list/search).
6. **Reviewed baseline** — `docs/metrics/catalog-payload-baseline.json`
   regenerated in the same review.

## Verification plan

- Fixture with a probe test assembly: 2 classes, categories `Smoke` / `Slow`
  / none, so category and assembly selectors have something real to select.
- `test list` with no filter → assembly counts; with `--category Smoke` →
  exactly the categorised test; with a nonsense filter → empty, not an error.
- `test --category Smoke` runs one test; `test --filter NoSuchZZZ` now
  returns `NO_TESTS_MATCHED`; `test --mode EditMode` with no selector still
  passes.
- `test cancel` during a PlayMode run, then a fresh `test` run succeeds —
  proving the lockout is gone.
- `manage_packages search --filter navigation` returns
  `com.unity.ai.navigation` with its compatible-version list; a nonsense
  filter returns zero hits as a success, not an error.
- Three-bucket gate (`6000.0.35f1`, `6000.3.5f2`, `6000.5.6f1`) including
  `compile-exact-source.ps1` with zero warnings.
