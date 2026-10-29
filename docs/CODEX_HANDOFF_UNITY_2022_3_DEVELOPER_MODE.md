# Codex Handoff — Unity 2022.3 Developer Mode

Status: **FINAL v2** (v1 reviewed read-only by Codex: ITERATE; all required items resolved below).

## Goal

Let the Connector install, compile and run in **Unity 2022.3** (representative
`2022.3.62f2`) as an opt-in **Developer Mode**, while keeping every existing
`6000.6+` requirement intact.

- Unity `6000.6+` stays the supported line. Its code path, catalog and existing release-gate requirements are preserved. This change **adds** a separate 2022.3 verification. It does not relax or replace the 6000.6 gate.
- Unity `2022.3` becomes an opt-in, unsupported line. The Connector compiles there, but it refuses every request dispatched to Unity until the user turns on Developer Mode.
- Every other Editor line (`2022.1`/`2022.2`, `2023.x`, `6000.0`–`6000.5`) stops at an explicit `#error`.

Non-goals: a 2022.3 `unity_docs` corpus, support for other lines, a second package, a new tool or action, a CLI ↔ Connector version handshake, or any change to the transport model.

## Feature admission (CLAUDE.md gate)

1. **Failure prevented:** a user who must work in Unity 2022.3.62f2 cannot use Hera at all. Measured with an unmodified Connector `0.2.13` added by git URL to a blank 2022.3.62f2 project: UPM resolution fails (`com.unity.ugui … Resolved version [1.0.0] does not satisfy requested version [2.6.0]`). The source then has 9 compiler errors in 4 files, plus 1 in `HeraAgent.TestRunner` with test-framework 1.1.33.
2. **Existing surface reuse:** no new tool, action or transport. The change reuses `CommandRouter` dispatch, `HeraSettings`, the shared `asset-config.json` and the existing boolean `asset-config` subcommand pattern. The existing `CancelResult.NunitCancelRequested` field reports cancel capability.
3. **Contract and safety:** one new string error code `DEVELOPER_MODE_REQUIRED` (non-retryable). No output-schema change. No approval or ledger change. Unity boundary enforced at compile time (D2).
4. **Regression evidence:** see Verification.
5. **Surface cost:** 0 tools, 0 actions, 0 schema fields. One asset-config key and one CLI subcommand. Connector package size unchanged.
6. **Reviewed baseline:** the 6000.6 catalog must be byte-equivalent (same hash/payload). No baseline regeneration is expected. If it changes, stop and report.

## Measured facts

Static facts come from reference sets and metadata. Runtime facts are named as such.

| # | Fact | Evidence (local bundle outside the repo; described, not linked) |
|---|---|---|
| F1 | A git package declaring `"unity": "6000.0"` (`connector-0.1.2`) was resolved and compiled by 2022.3.62f2. This shows only that this package's `unity` value did not block this install path. It is not a general claim about the field. | 0.1.2 probe import logs |
| F2 | A package depending on `"com.unity.ugui": "1.0.0"` resolves to built-in **2.6.0** on 6000.6.7f1 and **1.0.0** on 2022.3.62f2. Both compile code using `UnityEngine.UI.Image` and `UnityEngine.EventSystems.EventSystem`. | ugui probe lockfiles and logs |
| F3 | A blank 6000.6 project manifest has no `com.unity.ugui`, so the Connector must keep declaring it (also in `docs/DECISION_LEDGER.md`). Lower the version, do not drop it. | 6000.6.7f1 blank manifest |
| F4 | Unmodified `0.2.13` on 6000.6.7f1: production and TestRunner assemblies built, no CS errors or warnings in the log, `[Hera] HTTP server started`, clean batchmode exit. This baseline covers install, compile and initial load, not full behavior. | 6000.6.7f1 import log |
| F5 | 2022.3 production errors: `EntityIdCompat.cs:18,19,20,26(×2)` (`UnityEngine.EntityId`, `Resources.EntityIdIsValid`, `EditorUtility.EntityIdToObject`, `Object.GetEntityId` do not exist). Missing single-argument `FindObjectsByType(…, FindObjectsInactive)` at `InputQaResolver.cs:207` (generic), `Bake.cs:307` (**non-generic, `Type` argument**), `EditorScreenshot.cs:437` and `:507` (generic). | Headless compile with 2022.3's Bee reference set |
| F6 | On 6000.6.7f1 the 2022.3 replacements are **CS0619 errors** (`EditorUtility.InstanceIDToObject(int)`, `Resources.InstanceIDIsValid(int)`, `Object.GetInstanceID()`). The `(FindObjectsInactive, FindObjectsSortMode)` overloads and `FindObjectsSortMode` are **CS0618 warnings**. On 2022.3.62f2 the same code gives 0 errors and 0 warnings. → The 2022.3 path must sit entirely behind preprocessor gates. | API probe compiled against both Bee reference sets |
| F7 | 2022.3 signatures: `int Object.GetInstanceID()`, `Object EditorUtility.InstanceIDToObject(int)`, `bool Resources.InstanceIDIsValid(int)`. `FindObjectsByType` overloads are `(SortMode)` and `(Inactive, SortMode)` plus the `Type` equivalents. None are `[Obsolete]`. | Assembly metadata, 2022.3 XML docs |
| F8 | Static survey: 954 unique Unity symbols bound by the 2022.3 reference set. The only gaps are the EntityId family (4) and `FindObjectsByType` (1 signature). `LogEntries`, `LogEntry` and `GameView` are internal but present. Their reflection success at runtime is **not** verified. | Symbol survey (969 records incl. supplementary/unverified rows) |
| F9 | `TestRunnerApi.CancelTestRun` is absent in test-framework 1.1.33 (`RunTests.cs:674`, CS0117). The 6000.6 built-in test-framework CHANGELOG records the cancel API under **1.4.0**. | Compile against 1.1.33; CHANGELOG |
| F10 | Both 2022.3 and 6000.6.7f1 Editor assemblies reference `unity-4.8-api` and no `System.Runtime.Loader`. Statically, the `exec` `Assembly.Load` fallback is the same on both lines. Runtime not verified. | Both Bee rsp files |
| F11 | 2022.3 defines include `UNITY_2022_3`, `UNITY_2022_3_62`, `UNITY_2022_3_OR_NEWER`, and no `UNITY_6000_*`. 6000.6.7f1 defines include `UNITY_2022_3_OR_NEWER`, `UNITY_6000_0_OR_NEWER` … `UNITY_6000_6_OR_NEWER`, but **not** `UNITY_2022_3`. | Both Bee rsp files |
| F12 | `unity_docs` returns `docs_version` (the corpus version, `6000.6`). Its `unity_version` field is the entry's documentation version, **not** the running Editor's version. The real Editor version is in `status`/heartbeat (`cmd/status.go`). | `Tools/UnityDocs.cs:83-95` |

Unverified on 2022.3 (packages not installed during measurement): TextMeshPro (`com.unity.textmeshpro`), Timeline, Input System, AI Navigation.

## Design

### D1. Package metadata — `AgentConnector/package.json`

- `"com.unity.ugui": "2.6.0"` → `"1.0.0"` (F2, F3).
- `"unity": "6000.6"` → `"2022.3"`. This states the lowest line the package now compiles on. The real boundary is D2.
- Connector version → **`0.3.0`** (minor: support boundary change). Tag only at release time with user approval.

### D2. Version allow-list — `AgentConnector/Editor/Core/EntityIdCompat.cs`

This file owns the version split and is the only place with an `#error`:

```csharp
#if UNITY_6000_6_OR_NEWER
    // existing 64-bit EntityId implementation, unchanged
#elif UNITY_2022_3
    // Unity 2022.3 Developer Mode: the 32-bit instance id travels inside the 64-bit id contract.
    public static Object ToObject(long id)
    {
        if (id < int.MinValue || id > int.MaxValue) return null;
        var instanceId = (int)id;
        return UnityEngine.Resources.InstanceIDIsValid(instanceId) ? EditorUtility.InstanceIDToObject(instanceId) : null;
    }
    public static long IdOf(Object o) => o.GetInstanceID();
#else
#error "[Hera] I run on Unity 6000.6+, and on Unity 2022.3 in Developer Mode. This Editor is outside both lines."
#endif
```

- The current file has no `using UnityEngine;`. Qualify `UnityEngine.Resources` (as above) or add the using.
- The public `long` signature is unchanged, so the 48 call sites in 17 files stay untouched.
- Rewrite the class summary to describe both lines in Unity terms.
- The `#error` stops normal compilation of the whole project on an unsupported line, and other files may report errors next to it. That is accepted and must be stated in the README. Do not promise "only this error".
- Every other 2022.3 gate in this change uses the same pair: `#if UNITY_6000_6_OR_NEWER` … `#elif UNITY_2022_3` (or `#else` where D2 already guarantees only these two lines can compile).

### D3. `FindObjectsByType` — four call sites

```csharp
#if UNITY_6000_6_OR_NEWER
    Object.FindObjectsByType<T>(FindObjectsInactive.X)                  // generic sites
    UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.X)   // Bake.cs:307
#else
    Object.FindObjectsByType<T>(FindObjectsInactive.X, FindObjectsSortMode.None)
    UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.X, FindObjectsSortMode.None)
#endif
```

Keep each site's existing `Include`/`Exclude` value and its generic or non-generic form. Gating the four expressions in place is preferred over a new helper.

### D4. Test framework cancel — `TestRunner/`

- `HeraAgent.TestRunner.asmdef` `versionDefines` += `{ "name": "com.unity.test-framework", "expression": "1.4.0", "define": "HERA_TEST_FRAMEWORK_CANCEL_API" }`.
- **With the define** (6000.6 / test-framework ≥ 1.4.0): current behavior, byte-for-byte.
- **Without the define** (2022.3 with 1.1.33), an *active* run (a run started in this Editor process whose completion callback is still registered) **cannot be cancelled**. The cancel action must:
  - keep the pending record and the run lock,
  - write no `TEST_RUN_CANCELLED` result,
  - return `was_running = true`, `nunit_cancel_requested = false`, and an honest message, e.g. `"[Hera] I can't stop a running test on this test-framework version; it will finish and record its own result."`
- Pending records with **no** active run in this process (left over after a domain reload or an Editor restart) keep today's release behavior. If the existing code cannot tell an active run from a stale record, add the smallest in-memory marker set when the run starts and cleared on completion, and document it.
- No new output fields (reuse `CancelResult`). The catalog stays unchanged.

### D5. Developer Mode setting and gate

**Storage.** Boolean `developer_mode` in the shared `~/.hera-agent-unity/asset-config.json`, default `false`. It is global on purpose: the intended workflow is install → work → remove in a project, and a global key leaves nothing behind in that project. It has no effect on `6000.6+`. It does affect every 2022.3 project on the machine, and the README says so.

**CLI (Go).** Follow the existing boolean subcommand pattern (`assetConfigBoolFlag`, `cmd/asset_config.go`):
- `hera-agent-unity asset-config developer-mode` → show; `… developer-mode on|off` → set. `--json` stays read-only and includes the new key.
- `internal/assetconfig`: add the typed field to `AssetConfig`. Update **all** of `json.go`: the unmarshal temp struct, the assignment into `AssetConfig`, the marshal field map, and the known-field strip list (unknown-key preservation already exists. The field is needed for typed read/write). Add the subcommand to help text (`cmd/help/*.txt` / `printTopicHelp`) and the TUI if the TUI lists the other mode toggles.
- Go tests: load/save round-trip and unknown-key preservation with the new key.

**Connector (C#).**
- `HeraSettings.DeveloperMode`: same mtime-cached read as the other toggles. Wire it through the snapshot type, parse, publish, current-snapshot and reset-defaults paths. Keep the existing semantics (missing file or missing key → `false`; malformed or locked read → last good value) and document them.
- Hera Settings window: add the field to the Model **and** to `ConfigFieldNames` (`HeraAgentAssetConfigWindow.Model.cs`, used by `MergeConfig`). Render the "Developer Mode (Unity 2022.3)" toggle only under `#if UNITY_2022_3`. On 6000.6 the window must not write `developer_mode` at all, so its hidden default cannot overwrite the shared value.

**Gate order.**
- Single request (`CommandRouter.DispatchInternal`): protocol-version validation first (existing `hera.execution/1` rule), **then** the Developer Mode check, then catalog validation, `list`, handler, approval, ledger.
- Batch (`DispatchBatch`): Developer Mode check before request preparation, approval and atomic validation.
- Code: `#if UNITY_2022_3 if (!HeraSettings.DeveloperMode) return DeveloperModeRequired(); #endif`.
- `DeveloperModeRequired()` returns `ErrorResponse("DEVELOPER_MODE_REQUIRED", "[Hera] I'm installed in Unity 2022.3, which I only serve in Developer Mode. Turn it on in Hera Settings or run `hera-agent-unity asset-config developer-mode on`, then retry.")`. String code, not added to the retryable allow-list (`Response.cs`). No change to `contracts/runtime-contracts.json` (it has no error-code registry).
- Guarantee: a well-formed request dispatched to a locked 2022.3 Editor gets `DEVELOPER_MODE_REQUIRED` with no side effects (no mutation, no approval token, no ledger entry). Malformed protocol requests keep their existing error. "Immediately" is not promised, because dispatch still waits for the command lock.
- HTTP server, heartbeat, `status` and `ping` keep running while locked.

**CLI cache behavior (contract).**
- *Cold catalog cache:* the Connector's `DEVELOPER_MODE_REQUIRED` must reach the user as a standard compact JSON envelope with `code: "DEVELOPER_MODE_REQUIRED"` preserved. `cmd/call.go` and `cmd/dispatch.go` currently wrap provider errors as plain Go errors. Fix that in the CLI layer with a Go test, without touching the strict `call` validation semantics.
- *Warm catalog cache:* local-only operations (`list --profiles`, `call --validate-only`, cached schema reads) may still succeed, because they never reach Unity. The contract is "**every request dispatched to the Connector is refused**", not "every CLI command fails". Document this.

### D6. Tests — `AgentConnector/Editor/Tests/`

- Seven files use 6000.6-only APIs (`EntityIdCompatTests`, `ReleaseGateTests`, `AtomicBatchTests`, `ExecSerializerTests`, `SceneGameObjectToolTests`, `ScreenshotAnnotationTests`, `ScreenshotPhysicsTests`). Branch only the expected-id computation per line. Do not delete coverage.
- Add 2022.3 cases: `EntityIdCompat` round-trip for positive, negative and `0` ids, a destroyed object, and `int` boundary ± 1 as `long` → `null`. Developer Mode gate: locked → `DEVELOPER_MODE_REQUIRED` with no side effect, unlocked → dispatch.
- The new gate must not break existing dispatch tests on 2022.3. A fixture sets `HeraSettings` to a temp config with `developer_mode: true` and restores it (`s_ConfigPathOverride` exists). Isolate the OFF tests.
- `HeraAgent.Editor.Tests` compiles in a 2022.3 `testables` pass with test-framework 1.1.33, and on 6000.6 with 1.8.0.

### D7. Documentation

- `README.md`, `README.ko.md`:
  - Add a support row for `2022.3` — Developer Mode (opt-in, unsupported, representative `2022.3.62f2`).
  - Replace the claim that Package Manager refuses older Editors with what actually happens: the ugui requirement and the D2 compile boundary stop unsupported lines.
  - Add a short Developer Mode section: how to enable it and that it is machine-global.
  - List what is unverified or limited there: TMP, Timeline, Input System, AI Navigation; `unity_docs` answers from the `6000.6` corpus, so compare with the Editor version in `status`; `run_tests cancel` cannot stop an active run on test-framework < 1.4.0.
- `CLAUDE.md`: correct the line saying the `unity` field enforces the floor. Add a 2022.3.62f2 Developer Mode pass to the connector release gate **in addition to** the 6000.6 representative pass. Keep it developer-only.
- `docs/UNITY_EDITOR_VERSION_INVENTORY.md`: add 2022.3.62f2 as the Developer Mode representative. The 6000.6 representative stays `6000.6.6f1`. Do not change it in this work.
- `CHANGELOG.md`. In `AGENTS.md`, add **user-facing** Developer Mode guidance only (do not copy the developer release gate). Then run `go run ./tools/sync-agent-guides` and `--check`.
- Agent-facing strings in English. Describe features in Unity terms only. No absolute paths and no external research-tool references in repo files or commit messages.

### D8. Out of scope

| Item | Decision |
|---|---|
| 2022.3 `unity_docs` corpus | Not now. Document the limit (F12). |
| `exec` collectible unload | Unchanged. Statically the same on 6000.6 (F10). |
| Other Editor lines | `#error` (D2). |
| Blocking local-only CLI operations while locked | Not now (D5 warm-cache contract). |
| CLI ↔ Connector version handshake | Out of model (CLAUDE.md). |

## Verification plan

Record every command, exit code, Unity version, CLI binary path and exported SHA in the evidence file.

1. **Go:** `go clean -testcache`, `gofmt -w .`, `~/go/bin/golangci-lint run ./...`, `~/go/bin/golangci-lint fmt --diff`, `go test ./...`.
2. **Install source:** commit first, then `git archive <exact SHA>` to a scratch folder and add it to Unity projects as `file:`. Never point Unity at the working tree. Re-export after each fix commit.
3. **Shared setting hygiene:** before any Unity step, record whether `asset-config.json` exists and its `developer_mode` value. Restore both exactly at the end.
4. **2022.3.62f2 normal install (blank project):**
   - UPM resolves. Hera assemblies have 0 errors and 0 warnings. The production rsp has 0 `Tests/` sources and no `HeraAgent.Editor.Tests` assembly exists.
   - With Developer Mode OFF, a dispatched request returns `DEVELOPER_MODE_REQUIRED`. Turn it ON (CLI subcommand), then the Editor is `ready` and `console --type error` = 0.
   - Second project or step: add test-framework 1.1.33; `HeraAgent.TestRunner` compiles.
   - Separate `testables` project: `HeraAgent.Editor.Tests` compiles and the Hera EditMode tests run with a non-zero test count, all green.
5. **6000.6 regression:**
   - 6000.6.7f1 blank project: 0 errors, 0 warnings, TestRunner built, Hera release-gate suite green with a non-zero count, `developer_mode` ON/OFF has no effect, the Settings window does not write the key.
   - The required representative **6000.6.6f1 is not installed** on the measuring Mac. Record its gate as **BLOCKED**, separate from the 6000.6.7f1 PASS. Do not reword the gate around it.
6. **Catalog (6000.6.7f1, same package set as the baseline run):** `hera-agent-unity --project <proj> list --catalog --schema_version hera.tool-catalog/1 > catalog.json`, then `go run ./tools/catalog-payload-report --catalog catalog.json --compare docs/metrics/catalog-payload-baseline.json --fail-on-change`. Expected: no change (hash and payload). Any change → stop and report.
7. **2022.3 live matrix** (CLI built from the branch into a scratch path; the installed CLI untouched; `--project` pinned):
   - Gate: cold and warm catalog cache × raw `list`, strict `call`, a legacy command, `batch`. OFF → envelope with `code: DEVELOPER_MODE_REQUIRED` and no side effects. ON → works without an Editor restart. ON → OFF → refused again.
   - Settings round-trip: CLI set → C# window shows it → window save (2022.3) → CLI reads it; unknown keys survive. On 6000.6, a window save leaves the key untouched.
   - Scene query; create GameObject; set property; save; re-read via a different read call; close and reopen the scene; value persists.
   - Prefab create → save → reload; override list.
   - `editor refresh --compile` (domain reload) → reconnect; Play Mode enter/exit → reconnect; Editor quit and relaunch → reconnect, saved state intact.
   - Instance ids: positive, negative, `0`, destroyed object, int boundary ± 1.
   - Duplicate safety: repeat an identical mutation with the same approval/operation id after a simulated lost response; count resulting objects. Request during compile. Stale heartbeat present. Repeated `exec` (20×) for exceptions.
   - Tests: `test --mode EditMode` with test-framework 1.1.33. Cancel **during** a bounded long-running test: the response says it cannot cancel, a new run is still refused while it runs, and the durable result after completion is the real result (not overwritten by a cancel).
   - `manage_ui` without TMP (legacy `Text` fallback / `TMP_NOT_INSTALLED`). `unity_docs` shows `docs_version 6000.6`, while `status` shows Editor 2022.3.62f2.
8. **6000.6.7f1 live smoke:** status, one mutation + save, domain-reload reconnect, `run_tests cancel` unchanged.
9. Close every Editor you started. Restore the shared setting (step 3).

## Execution rules

- Branch `feat/unity-2022-3-developer-mode` from `main`. Make local conventional commits (this handoff doc may go in the first commit). **No push, tag, release, or `hera-agent-unity update`** without explicit user approval.
- You have exclusive control of Unity during implementation. Nobody else runs Unity.
- `.meta` rule: any new file or folder under `AgentConnector/` ships its `.meta` with a fresh GUID (see `CLAUDE.md`).
- Versions: Connector → `0.3.0`. The CLI changes too. Propose the next CLI tag in the evidence file, but do not tag.
- If a fact above proves wrong, or a step can only pass by changing the design, stop, record the evidence, and report to the requester before continuing.

## Evidence file

`docs/handoffs/unity-2022-3-developer-mode-<date>.md`: per-step PASS / FAIL / BLOCKED table with commands, exit codes and log locations (scratch paths described, not hard-coded); exported SHAs; CLI binary version; remaining risks; proposed CLI version.
