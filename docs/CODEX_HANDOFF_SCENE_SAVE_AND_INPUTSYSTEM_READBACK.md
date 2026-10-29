# Codex Handoff — Untitled Scene Save Hang and Input System Readback/Delta Delivery

Status: **FINAL v2** (v1 reviewed read-only by Codex: ITERATE, 0 critical / 5 important / 3 minor; all resolved below).
Branch (create from `main`): `fix/scene-save-and-inputsystem-readback`. Independent of the Unity 2022.3 Developer Mode branch. Commit this file into the branch as `docs/CODEX_HANDOFF_SCENE_SAVE_AND_INPUTSYSTEM_READBACK.md` in the first commit.

## Goal

Fix three defects that exist on `main` (same code on 6000.6.6f1 and on 2022.3 Developer Mode):

1. `scene save` on an untitled dirty scene opens Unity's native modal Save dialog and freezes the Editor.
2. Input System `keyboard`/`mouse` responses report post-action values from the wrong state (`pressed_after`, `position`, `delta`, `scroll`).
3. Input System `mouse --mode scroll` does not reach player code (observed). `--mode delta` is suspected to share the cause (**hypothesis — confirm with a baseline probe before changing code**).

Non-goals: a "save as" path for untitled scenes; blocking modals that a caller explicitly requests through the generic `menu` or `exec` tools; new tools, actions, fields, settings or error codes; changes to the EventSystem (uGUI) backend; changes to global Input System policy.

## Feature admission

1. **Failure prevented:** (1) one agent command blocked the Editor behind a modal until a human pressed Cancel, and queued requests then ran late; (2) agents are told input failed when it succeeded; (3) scroll injection silently does nothing.
2. **Existing surface reuse:** `save_all` already returns `SCENE_UNSAVED`; the recorder already samples in the configured player update (`onAfterUpdate` + `IsConfiguredUpdate()`, `Core/InputQaInputSystem.cs:625-663`); `CancelResult`-style existing codes are reused.
3. **Contract and safety:** no schema change. Behavioral meaning of the readback fields is defined below (D2) and documented in the `input` help/agent guide. No approval/ledger change.
4. **Regression evidence:** Verification below.
5. **Surface cost:** zero tools/actions/fields/codes. The catalog must stay byte-equivalent; `Tools/ManageScene.cs:13` does not promise saving untitled scenes, so no description change is expected. If any description changes, submit the description diff and payload diff together and regenerate the baseline only as a reviewed change.

## Measured and source facts

| # | Fact | Evidence |
|---|---|---|
| S1 | `Tools/ManageScene.cs:331,341`: a dirty scene with empty `path` reaches `EditorSceneManager.SaveScene(scene)`; on 2022.3.62f2 this opened the native "Save Scene" modal, the heartbeat went stale, later requests returned `OPERATION_OUTCOME_UNKNOWN`, and queued commands executed after Cancel. 6000.6 has identical source; the native dialog there is not yet observed. | 2022.3 run logs; source |
| S2 | `Tools/ManageScene.Authoring.cs:160-170` (`save_all`) rejects dirty untitled scenes with `SCENE_UNSAVED`. | Source |
| S3 | `scene create --mode additive` is refused by Unity while an untitled scene is unsaved; `--mode single` refuses dirty scenes with `SCENE_DIRTY`. Neither opened a dialog. | 2022.3 logs |
| I1 | A probe `MonoBehaviour` reading `Keyboard.current`/`Mouse.current` each `Update` observed injected `a` down/up, position `(320,200)` and left down/up on 2022.3.62f2 + Input System 1.14.0 and 6000.6.6f1 + 1.20.0. | Probe logs, both lines |
| I2 | In those runs the responses reported `pressed_after: false` while `A` was held and `position: [0,0]` after the move. | Same logs |
| I3 | The probe never observed `scroll` after `mouse --mode scroll --scroll_delta 0,3` on either line. `delta` was not probed. | Same logs |
| I4 | Apply path: `ApplyOnNextConfiguredUpdate` registers an `onBeforeUpdate` callback gated by `IsConfiguredUpdate`, then `RunExplicitUpdate` temporarily switches `updateMode` and calls `InputSystem.Update()` from an Editor callback; the callback writes with `InputState.Change` (`Core/InputQaInputSystem.cs:786-935`). | Source |
| I5 | Readback happens after `EditorUpdate.Wait(SettleFrames)` in an `EditorApplication.update` context (`:195-207`, `:319-336`). Input System keeps separate Editor and player buffers in Play Mode and switches per update type (1.14 `State/InputStateBuffers.cs:124,148`; 1.20 `:125,149`). | Source |
| I6 | Update order in both packages: buffer `SwitchTo` → `device.OnNextUpdate` (Mouse/Pointer reset `delta`/`scroll`) → user `onBeforeUpdate` → event processing (1.14 `InputManager.cs:2706,2727,2731`; 1.20 `:2964,2985,2989`; reset at 1.14 `Devices/Pointer.cs:232`, `Devices/Mouse.cs:327`; 1.20 `:212`, `:329`). So a value written in `onBeforeUpdate` survives **that** update; it is zeroed by the **next** update. **Inferred root cause of I3:** Hera writes during its forced input-only update, and the next natural update resets the transient value before player `Update` reads it. Buttons/position persist across updates, which matches I1. Not yet instrumented. | Package sources, both versions |
| I7 | `QueueDeltaStateEvent<TDelta>(InputControl, TDelta, double time = -1)` is public in both versions (1.14 `InputSystem.cs:2679`, 1.20 `:2641`). `InputSystem.Update(InputUpdateType)` is internal in both. Queued events pass focus/background filters; direct `InputState.Change` does not. | Package sources |
| I8 | `EditorUpdate.Wait` counts Editor ticks, not player input updates or wall-clock time; `settle_frames` may be 0 (`Core/InputQaResolver.cs:30`). The current cancellation registration cancels the task but leaves the `onBeforeUpdate` callback subscribed until the next matching update (`Core/InputQaInputSystem.cs:857,873`). | Source |

## Design

### D0. Instrument first (scratch only, not committed)

Before changing code, run a baseline probe on 6000.6.6f1 + 1.20.0 that logs, per frame: input update type and frame for Hera's apply callback, for `onAfterUpdate`, and for the player probe's `Update`/`FixedUpdate` reads, for key, move, click, scroll and **delta**. Record whether the apply lands in the forced update and the reset in the next natural update (confirms I6), and whether `delta` fails like `scroll`. If the evidence contradicts I6, stop and report.

### D1. `scene save` never opens a dialog

- In `Tools/ManageScene.cs` single-scene `save`, after the existing clean-scene no-op (`saved=false`, unchanged), if `string.IsNullOrEmpty(scene.path)` return `ErrorResponse("SCENE_UNSAVED", "[Hera] I can't save the untitled scene '<name>': it has no asset path, and saving it would open Unity's Save dialog. Save it once from the Editor, or create scenes with `hera-agent-unity scene create <Assets/...unity>` before editing.")`. The scene stays dirty.
- **Modal scope (decided):** this change covers the typed `scene` tool. Audit result to record (from review): `Authoring.cs:117` passes an explicit validated path (safe); `save_all` already guards untitled (`:164,173`); no `SaveCurrentModifiedScenesIfUserWantsTo` call exists; `DisplayDialog` calls live in the settings window UI only. Generic `menu` (`Tools/ExecuteMenuItem.cs:63`, blacklist only `File/Quit`) and `exec` can still open modals when the caller explicitly asks for them; they are **out of scope**. Do not claim "no agent call can open a modal". Add one line to the `menu` help/agent guide only if an equivalent caution does not already exist (`Core/UnityPitfalls.cs:63` covers `exec`).
- Tests (EditMode, with a finite host timeout and a window-list check in live evidence, never relying on an infinite hang): dirty untitled → `SCENE_UNSAVED`, still dirty; clean untitled → existing `saved=false`; dirty scene with a path → saved; `save_all` with a mixed set (one untitled dirty + one saved dirty) → rejected before any partial save.

### D2/D3. One request flow for apply, delivery and readback

Every Input System keyboard/mouse state change (key down/up, button down/up, position, delta, scroll) goes through one helper with this order:

1. **Prepare** (on the main thread, before anything is written): resolve the control, create a per-request state with a one-shot `onBeforeUpdate` apply callback and a one-shot `onAfterUpdate` capture callback, and a finite **wall-clock deadline** independent of `settle_frames` (a single constant, documented; e.g. 2000 ms — pick and justify).
2. **Apply in a natural configured update:** the `onBeforeUpdate` callback runs only when `IsConfiguredUpdate()` is true and it is **not** a Hera-forced update. Write with direct `InputState.Change` (as today), so focus filtering stays as it is now and buttons keep working without Game view focus. Request a player-loop tick with `EditorApplication.QueuePlayerLoopUpdate()` instead of forcing `InputSystem.Update()`.
   - Remove `RunExplicitUpdate` from this path. If the implementer finds a case where the forced update is still required (e.g. persistent controls while the player loop does not tick), keep it **only** for persistent controls (keys/buttons/position) and **never** for `delta`/`scroll`; record the evidence.
   - `QueueDeltaStateEvent` is **not** the default: it passes focus filters and would change today's delivery semantics. Use it only if D0 shows direct `Change` in the natural update still loses the value, and then verify focus behavior explicitly.
3. **Capture in the same update:** the `onAfterUpdate` callback of the **same** update that ran the apply takes one `InputQaPhysicalSnapshot` (reuse `Capture()`), then unsubscribes both callbacks. This preserves transient `delta`/`scroll` exactly as player code sees them in that frame. Never copy the requested value into the response as if observed.
4. **Settle:** then wait `settle_frames` Editor ticks as today (no extra input update is forced for sampling).
5. **Respond:** response fields come from the captured snapshot. Define in help/agent guide: "values are the player-side state at the end of the input update that applied this request's last change". For `press`/`click` (down then up) that is the release update, so `pressed_after` is `false` by definition; for `down` it is `true`.
6. **Finish exactly once**, on success, deadline, cancellation, Play Mode exit, pause, or domain reload: unsubscribe both callbacks immediately (fixes I8), complete the task once, and leave no pending apply that could fire later.

**Ownership:** add to `HeldKeys`/`HeldMouseButtons` only after the apply callback actually ran for a down; on a failed/expired down nothing is held. A release that times out keeps the hold record so the existing cleanup can release it later. Never flush or reset unrelated real user input.

**Errors (existing codes only):** Play Mode paused → existing `INPUTSYSTEM_PLAY_MODE_PAUSED`; Play Mode exited → existing `INPUTSYSTEM_PLAY_MODE_REQUIRED`; deadline with Play Mode running (no matching player update, e.g. Manual mode not pumped, or focus policy halting updates) → existing `INPUTSYSTEM_INVOCATION_FAILED` with a message naming the deadline and the current `updateMode`. If a new code seems necessary, stop and raise it as a contract change instead.

**Update modes:** Dynamic → applied and captured in the Dynamic update before `Update`. Fixed → in a Fixed update before `FixedUpdate`; record whether `Update` also sees it. Manual → no natural update exists; the request waits for the user's own `InputSystem.Update()` pump until the deadline, then fails honestly. Document this limit; do not pump on the user's behalf.

**sequence / record / replay:** they share the helper. Each step's capture comes from its apply update, so no extra sampling update is added; replay's recorded frame gaps (`Core/InputQaReplay.cs:134`, last step settle 0) and the recorder's per-update records (`Core/InputQaRecording.cs:312-339`) must not gain duplicates or extra gaps.

## Verification

Record commands, exit codes, logs, Unity/package versions, the exported SHA, CLI path and the Input System settings (`updateMode`, `editorInputBehaviorInPlayMode`, `backgroundBehavior`, `runInBackground`, Game view focus) for each probe run. Do not change global Input System policy to make a test pass.

1. Go checks per `CLAUDE.md` (real-path `TMPDIR` on macOS; default-`TMPDIR` symlink failures already exist on `main`).
2. Install the exported commit SHA (not the working tree) as a `file:` package in fresh blank projects.
3. **D0 baseline** on the unpatched SHA (6000.6.6f1 + 1.20.0): update-type/frame log; scroll and delta observed or not.
4. **6000.6.6f1 gate:** normal install 0 errors / 0 warnings, ready, console 0; testables suite green with a non-zero count; catalog compare unchanged.
5. **Probe matrix after the fix** (6000.6.6f1 + 1.20.0, activeInputHandler Both, Dynamic):
   - key `down` → probe `a=True`, response `pressed_after: true`; `up` → `False`/`false`; `press` → probe sees down then up.
   - move `(320,200)` → probe and response equal; click → probe down then up, response consistent.
   - scroll `(0,3)` → probe sees it exactly once in the apply frame; response `scroll=(0,3)`; next update back to 0.
   - delta `(12,-4)` → same as scroll.
   - `settle_frames` 0, 1 and 5 for scroll and key down.
   - Game view focused and unfocused with the project's default policy (record what happens; buttons must not regress versus baseline).
   - Fixed mode (one key + one scroll) and Manual mode without a pump → bounded `INPUTSYSTEM_INVOCATION_FAILED`, callbacks unsubscribed, nothing applied later after a manual pump.
   - Pause during a pending request → `INPUTSYSTEM_PLAY_MODE_PAUSED` within the deadline; resume → no late scroll; held controls cleaned by the existing cleanup.
   - `sequence` with key + scroll + delta steps → each observed once; `record` → `replay` round trip with no duplicate events or added gaps; existing input tests green.
6. **Scene:** untitled dirty → `SCENE_UNSAVED` immediately, window list shows no Save dialog, Editor stays `ready`; clean untitled → `saved=false`; saved dirty → saved; `save_all` mixed → rejected with no partial save.
7. **Unity 2022.3.62f2:** run 5 (Input System 1.14.0) and 6 only after the Developer Mode branch is merged; otherwise record **deferred**, not PASS.

## Execution rules

- Branch from `main`; local conventional commits; **no push, tag, release, or `hera-agent-unity update`** without user approval.
- Exclusive Unity control during implementation; close every Editor you start; record and restore `~/.hera-agent-unity/asset-config.json` exactly if touched.
- `.meta` rule for new files; English agent-facing strings; `[Hera] I …` style; no absolute paths or external research-tool references in repo files or commits.
- Connector: patch bump from the `main` version at branch time. CLI unchanged unless Go changes.
- If D0 or any fact above proves wrong, or a step can only pass by changing the design or adding a code/field, stop and report with evidence before continuing.

## Evidence file

`docs/handoffs/scene-save-and-inputsystem-readback-<date>.md`: D0 instrumentation result, modal audit table (typed scene paths fixed; generic `menu`/`exec` explicitly excluded), PASS/FAIL/BLOCKED/deferred table per step, the deadline constant and its justification, focus/update-mode observations, and remaining risks.
