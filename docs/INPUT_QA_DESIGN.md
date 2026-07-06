# Input QA Tool Design

Design status: phase 1 EventSystem backend implemented for `state`, `inspect`, `click`, `pointer_down`, `pointer_up`, `submit`, `scroll`, and `drag`; Input System and native Windows backends remain planned.
Investigated against: Unity 6000.3.5f2, `hera-agent-unity` CLI v0.0.36, connector tools discovered on 2026-07-06.

This document defines a precise input-event QA surface for Hera. The goal is to let agents verify Unity UI and gameplay interactions when external Computer Use cannot capture Unity screenshot state or cannot click physical screen coordinates.

---

## Problem

Current QA can observe Unity through Hera (`status`, `console`, `screenshot`, `ui_doc capture`, Play Mode, tests), but it cannot perform real interaction through a dedicated Hera input tool.

When Codex Computer Use cannot acquire screenshot state for the Unity Editor window, it refuses coordinate clicks. In that environment:

- A top-level Unity Editor window may exist.
- A GameView may exist inside Unity as a child window.
- Hera can still run Play Mode, capture Game View or UI, and read console logs.
- Physical coordinate-click QA is blocked because the external click tool has no trusted screenshot state.

The missing product surface is an internal Unity input tool that can drive:

- uGUI `EventSystem` interactions.
- New Input System device events.
- Optional native Windows click fallback for the rare cases where OS-level input is required.

---

## Code Evidence

### Current tool registry and dispatch

- `AgentConnector/Editor/ToolDiscovery.cs`
  - Finds `[HeraTool]` classes by reflection.
  - Supports default `HandleCommand(JObject)` handlers.
  - Supports `[HeraAction]` action handlers.
  - Generates `list --tool <name>` schemas from nested `Parameters`.
- `AgentConnector/Editor/CommandRouter.cs`
  - Serializes all tool execution through a `SemaphoreSlim`.
  - Runs commands on Unity main thread via `HttpServer.ProcessQueue`.
  - Awaits `Task<object>` and `Task` tool handlers.
  - Therefore an input tool can safely wait one or more editor frames.
- `cmd/dispatch.go`
  - Default behavior sends unknown top-level categories directly as Unity tool names.
  - A new `input` tool can work without Go wrapper if it parses `args[0]` as action.
  - A Go wrapper is only needed for richer CLI behavior.

### Existing target and output helpers

- `AgentConnector/Editor/Core/TargetResolver.cs`
  - Resolves `instance_id` or hierarchy `path`.
  - Should be reused for target selection.
- `AgentConnector/Editor/Core/HierarchyPath.cs`
  - Builds and resolves `/Root/Child` paths.
  - Fallback path walk covers inactive objects.
- `AgentConnector/Editor/Core/EntityIdCompat.cs`
  - Keeps int `instance_id` contract across Unity 6000.5 entity-id API changes.
  - Must be reused for response IDs.
- `AgentConnector/Editor/Core/Response.cs`
  - Provides `SuccessResponse`, `ErrorResponse`, `agent_hint`, and timing support.
- `AgentConnector/Editor/Core/ToolParams.cs`
  - Provides current parameter parsing surface.

### Current UI and visual QA surfaces

- `AgentConnector/Editor/Tools/ManageUI.cs`
  - Creates Canvas, GraphicRaycaster, Button, Text, and EventSystem.
  - Uses `ComponentTypeResolver` to avoid compile-time package assumptions.
  - Picks `InputSystemUIInputModule` or `StandaloneInputModule` by compile defines.
- `AgentConnector/Editor/Tools/UiDoc.cs`
  - `capture` renders overlay uGUI canvases to PNG by temporarily routing canvases through a camera.
  - This is the correct visual verification companion for input QA.
- `AgentConnector/Editor/Tools/EditorScreenshot.cs`
  - Captures scene/game/editor windows and has internal editor-capture fallback.

### Runtime API availability confirmed in Unity

Confirmed through `hera-agent-unity describe_type` and `find_method` in the running Editor:

- `UnityEngine.EventSystems.EventSystem`
  - `RaycastAll(PointerEventData, List<RaycastResult>)`
  - `SetSelectedGameObject(...)`
- `UnityEngine.EventSystems.ExecuteEvents`
  - `pointerDownHandler`
  - `pointerUpHandler`
  - `pointerClickHandler`
  - `beginDragHandler`
  - `dragHandler`
  - `endDragHandler`
  - `scrollHandler`
  - `submitHandler`
  - `moveHandler`
  - `selectHandler`
- `UnityEngine.EventSystems.PointerEventData`
  - writable `position`, `pressPosition`, `delta`, `button`, `clickCount`, `pointerCurrentRaycast`, `pointerPressRaycast`, `pointerPress`, `rawPointerPress`, `pointerDrag`, etc.
- `UnityEngine.UI.GraphicRaycaster`
  - `Raycast(PointerEventData, List<RaycastResult>)`
- `UnityEngine.EventSystems.PhysicsRaycaster` and `Physics2DRaycaster`
  - available through `BaseRaycaster` and `EventSystem.RaycastAll`.
- `Unity.InputSystem`
  - loaded in the investigated project.
  - `InputSystem.QueueStateEvent`, `QueueTextEvent`, `Update`, `AddDevice`, `FindControl`.
- `Unity.InputSystem.TestFramework`
  - loaded in the investigated project, but it should not be a runtime dependency of the connector tool.

### External API references

- Unity `EventSystem.RaycastAll`: raycasts using all configured `BaseRaycaster` instances.
  - https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.EventSystems.EventSystem.html
- Unity Input System events: queued input events are processed on the next input update, or manually through `InputSystem.Update`.
  - https://docs.unity3d.com/Packages/com.unity.inputsystem@1.4/manual/Events.html
- Unity Input System test support: `InputTestFixture` creates isolated input setups for tests, not for normal runtime use.
  - https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/manual/Testing.html

---

## Terminology

### Physical OS click QA

An operating-system mouse event sent to the Unity window or GameView HWND. This is closest to a real user's mouse, but it depends on visible/restored windows, platform APIs, focus, DPI, and screen coordinates.

### Unity EventSystem input QA

An input event synthesized inside Unity through `EventSystem.RaycastAll` and `ExecuteEvents`. This is not an OS mouse click, but it verifies the real Unity UI event path:

- EventSystem exists.
- Raycasters are configured.
- CanvasGroup and raycast blockers affect hit order.
- The intended target is the top hit or is blocked.
- The same Unity event interfaces receive pointer events.

### Input System device QA

An input event queued into `Unity.InputSystem` devices (`Mouse`, `Keyboard`, `Touchscreen`, `Gamepad`) through `InputSystem.QueueStateEvent` or related APIs. This verifies code that reads `InputAction`, `Mouse.current`, `Keyboard.current`, etc.

### Handler invocation

Directly calling `Button.onClick.Invoke()` or a custom method. This is explicitly not sufficient for QA because it bypasses raycast, interactability, EventSystem, focus, and blocker behavior.

---

## Goals

1. Add a Hera `input` tool that can drive interaction from inside Unity without Computer Use.
2. Make uGUI pointer QA precise enough to distinguish:
   - Target missing.
   - No EventSystem.
   - No active raycaster.
   - Target is not under the pointer.
   - Target is blocked by another UI element.
   - Target is inactive or not interactable.
   - Event handlers executed successfully.
3. Provide structured output that agents can cite as evidence.
4. Keep payloads compact by default.
5. Support Play Mode QA loops:
   - `editor play --wait`
   - `input inspect`
   - `input click`
   - `console --type error`
   - `ui_doc capture` or `screenshot --view game`
6. Preserve existing no-boilerplate tool discovery pattern.
7. Avoid hard dependency on optional packages where possible.
8. Support Windows native click only as an opt-in fallback, not as the primary path.

---

## Non-Goals

1. Do not replace Computer Use entirely.
2. Do not claim EventSystem QA is a physical OS click.
3. Do not use direct `Button.onClick.Invoke()` as the main click path.
4. Do not make Windows native input the default.
5. Do not mutate scene setup just to make QA pass unless the user explicitly asks.
6. Do not add project-specific custom code to game projects.
7. Do not support IMGUI Editor chrome in phase 1.
8. Do not require `Unity.InputSystem.TestFramework` for connector runtime behavior.

---

## Proposed Command Surface

Primary tool:

```bash
hera-agent-unity input <action> [flags]
```

Because generic CLI dispatch already forwards unknown top-level commands, the C# `input` tool can parse `args[0]` as action. A Go wrapper is optional and should only be added if needed for command-specific help, file loading, or richer argument normalization.

### Actions

```bash
input inspect
input click
input double_click
input pointer_down
input pointer_up
input drag
input scroll
input submit
input key
input text
input state
```

### Target flags

All target-bearing actions accept:

```bash
--instance_id <N>
--path </Canvas/Button>
--target <path|id|selector>
--text <visible text>
--name <name substring>
--tag <tag>
--component <type>
```

Phase 1 must support `instance_id`, `path`, and `target` only. `text`, `name`, `tag`, and `component` are phase 2 selector conveniences because they need ambiguity handling.

### Coordinate flags

```bash
--position <x,y>
--normalized <x,y>
--offset <x,y>
--from <x,y|path|id>
--to <x,y|path|id>
--to_normalized <x,y>
```

Rules:

- `position` is screen-pixel coordinates in Unity bottom-left screen space for EventSystem/InputSystem.
- `normalized` is relative to target RectTransform rect.
- `offset` is relative to target center in local UI pixels.
- If no coordinate is supplied, use the target's effective clickable center.

### Backend flags

```bash
--backend eventsystem
--backend inputsystem
--backend native-win32
--backend auto
```

Defaults:

- `inspect`: `auto`, but reports every applicable backend status.
- `click`, `drag`, `scroll`, `submit`: `eventsystem`.
- `key`, `text`: `inputsystem` when available, otherwise EventSystem submit/update-selected fallback for selected UI fields.
- `native-win32`: never selected by `auto` in phase 1.

### Timing flags

```bash
--hold_ms <N>          # default 50 for click, 0 for inspect
--settle_frames <N>    # default 1
--steps <N>            # drag move steps, default 8
--button left|right|middle
--click_count <N>
--strict true|false
```

Timing behavior:

- Pointer down and pointer up should happen on separate frames by default.
- `settle_frames` waits after the final event before returning.
- `strict=true` fails when the action did not reach the expected handler or target.

---

## Response Contract

Every action should return `SuccessResponse` or `ErrorResponse`.

### Common success data

```json
{
  "backend": "eventsystem",
  "action": "click",
  "target": {
    "instance_id": 123,
    "name": "StartButton",
    "path": "/Canvas/MainMenu/StartButton",
    "active": true
  },
  "point": {
    "screen": [540, 960],
    "source": "rect_center"
  },
  "event_system": {
    "instance_id": 456,
    "path": "/EventSystem",
    "input_module": "InputSystemUIInputModule"
  },
  "raycast": {
    "top_hit_path": "/Canvas/MainMenu/StartButton",
    "target_hit": true,
    "target_top_hit": true,
    "hits": [
      {
        "rank": 0,
        "instance_id": 123,
        "path": "/Canvas/MainMenu/StartButton",
        "module": "GraphicRaycaster",
        "distance": 0
      }
    ]
  },
  "executed": {
    "pointer_enter": true,
    "pointer_down": true,
    "pointer_up": true,
    "pointer_click": true
  },
  "selected_after": {
    "instance_id": 123,
    "path": "/Canvas/MainMenu/StartButton"
  },
  "frames_waited": 2
}
```

### Compact mode

Default data should be compact:

```json
{
  "backend": "eventsystem",
  "target_id": 123,
  "target_path": "/Canvas/MainMenu/StartButton",
  "point": [540, 960],
  "target_top_hit": true,
  "executed": ["down", "up", "click"],
  "blocked_by": null
}
```

Verbose details can be enabled with:

```bash
--details true
```

---

## Error Codes

Use stable `code` fields. Agents must branch on `code`, not messages.

### Target and selector errors

| Code | Meaning |
|:---|:---|
| `INPUT_MISSING_ACTION` | No action supplied. |
| `INPUT_UNKNOWN_ACTION` | Unknown action. |
| `INPUT_MISSING_TARGET` | Action requires a target but none was provided. |
| `INPUT_TARGET_NOT_FOUND` | `path`, `instance_id`, or selector found nothing. |
| `INPUT_TARGET_AMBIGUOUS` | Selector matched multiple targets. |
| `INPUT_TARGET_INACTIVE` | Target or parent is inactive when active-only input is required. |
| `INPUT_TARGET_NOT_UI` | EventSystem UI action was requested for a non-RectTransform target. |

### EventSystem backend errors

| Code | Meaning |
|:---|:---|
| `INPUT_NO_EVENT_SYSTEM` | No active `EventSystem.current` or scene EventSystem. |
| `INPUT_NO_RAYCASTER` | No enabled `BaseRaycaster` can raycast at the point. |
| `INPUT_RAYCAST_MISS` | Raycast returned no hits at point. |
| `INPUT_TARGET_NOT_HIT` | Target was not in the hit stack. |
| `INPUT_TARGET_BLOCKED` | Target was hit but another object is above it. |
| `INPUT_TARGET_NOT_INTERACTABLE` | Target has `Selectable` or `CanvasGroup` state that prevents interaction. |
| `INPUT_HANDLER_NOT_FOUND` | No handler exists for requested action. |
| `INPUT_HANDLER_NOT_EXECUTED` | Handler was expected but `ExecuteEvents` returned false. |

### Input System backend errors

| Code | Meaning |
|:---|:---|
| `INPUTSYSTEM_UNAVAILABLE` | `Unity.InputSystem` assembly or required type not loaded. |
| `INPUTSYSTEM_DEVICE_UNAVAILABLE` | Requested device cannot be found or created. |
| `INPUTSYSTEM_CONTROL_NOT_FOUND` | Requested control path/key/button cannot be resolved. |
| `INPUTSYSTEM_EVENT_FAILED` | Queueing or updating input event failed. |

### Native Windows backend errors

| Code | Meaning |
|:---|:---|
| `INPUT_NATIVE_UNSUPPORTED_OS` | Native backend requested on non-Windows. |
| `INPUT_NATIVE_HWND_NOT_FOUND` | Unity/GameView HWND could not be located. |
| `INPUT_WINDOW_MINIMIZED` | Unity window is minimized; click is unsafe unless `--restore` is true. |
| `INPUT_NATIVE_COORDS_UNAVAILABLE` | Could not map target to screen coordinates. |
| `INPUT_NATIVE_FOCUS_DENIED` | Foreground/focus could not be set. |
| `INPUT_NATIVE_SEND_FAILED` | `SendInput` or equivalent failed. |

---

## Architecture

### Connector files

Add:

```text
AgentConnector/Editor/Tools/Input.cs
AgentConnector/Editor/Tools/Input.cs.meta
AgentConnector/Editor/Core/InputQaResolver.cs
AgentConnector/Editor/Core/InputQaResolver.cs.meta
AgentConnector/Editor/Core/InputQaEventSystem.cs
AgentConnector/Editor/Core/InputQaEventSystem.cs.meta
AgentConnector/Editor/Core/InputQaInputSystem.cs
AgentConnector/Editor/Core/InputQaInputSystem.cs.meta
AgentConnector/Editor/Core/InputQaNativeWin32.cs
AgentConnector/Editor/Core/InputQaNativeWin32.cs.meta
AgentConnector/Editor/Tests/InputQaTests.cs
AgentConnector/Editor/Tests/InputQaTests.cs.meta
```

The exact file split can be reduced during implementation, but separate helpers keep `Input.cs` small and testable.

### CLI and docs files

Minimum:

```text
cmd/help/input.txt
docs/COMMANDS.md
docs/INPUT_QA_DESIGN.md
docs/INDEX.md
README.md
README.ko.md
CHANGELOG.md
AgentConnector/package.json
```

Optional:

```text
cmd/input.go
cmd/input_test.go
```

Do not add `cmd/input.go` unless the default passthrough is insufficient. The generic parser already supports:

```bash
hera-agent-unity input click --path /Canvas/Button --settle_frames 2
```

as:

```json
{
  "args": ["click"],
  "path": "/Canvas/Button",
  "settle_frames": 2
}
```

### Tool declaration

```csharp
[HeraTool(
    Name = "input",
    Description = "Unity input QA: inspect and synthesize EventSystem, Input System, and optional native Windows input events.",
    RequiresPlayMode = false,
    Examples = new[] {
        "input inspect --path /Canvas/StartButton",
        "input click --path /Canvas/StartButton",
        "input drag --path /Canvas/Slider --to_normalized 0.8,0.5",
        "input key --key Space",
        "input text --path /Canvas/InputField --value player1"
    }
)]
public static class Input
{
    public class Parameters { ... }
    public static object HandleCommand(JObject raw) { ... }
}
```

Use `InputQa` or `InputTool` as class name if `Input` conflicts with `UnityEngine.Input`. Keep tool name `input`.

---

## Backend 1: EventSystem

This is the phase-1 primary backend.

### Inspect algorithm

1. Resolve target.
   - Use `instance_id` first.
   - Use `path` second.
   - Use `target` as path-or-id convenience.
2. Resolve target `RectTransform` for uGUI actions.
3. Resolve candidate screen point.
   - Explicit `position`: use directly.
   - `normalized`: convert within target rect.
   - `offset`: target center + offset.
   - Default: target rect center.
4. Resolve active EventSystem.
   - Prefer `EventSystem.current`.
   - If null, find scene EventSystem through version-gated object lookup.
   - Do not auto-create EventSystem in inspect/click by default.
5. Create `PointerEventData`.
   - Set `position`.
   - Set `pressPosition`.
   - Set `button`.
   - Set `clickCount`.
   - Set `pointerId`.
6. Call `EventSystem.RaycastAll`.
7. Sort and normalize hits as Unity returns them.
8. Determine:
   - `target_hit`
   - `target_top_hit`
   - `blocked_by`
   - `handler_target`
   - `click_handler_target`
   - `drag_handler_target`
9. Inspect interactability:
   - `Selectable.interactable`.
   - `Selectable.IsInteractable()` if accessible.
   - Parent `CanvasGroup` `interactable`, `blocksRaycasts`, `ignoreParentGroups`.
   - `Graphic.raycastTarget` on target and blockers.
10. Return compact diagnostics.

### Click algorithm

1. Run inspect internally.
2. If `strict` and target is not top hit, return `INPUT_TARGET_BLOCKED`.
3. Build `PointerEventData` from inspect point and raycast.
4. Execute pointer enter on top hit or target.
5. Find press target:
   - `ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject)`.
   - Fallback to `ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject)`.
6. Set:
   - `pointerPress`
   - `rawPointerPress`
   - `pointerPressRaycast`
   - `eligibleForClick`
   - `clickTime`
   - `clickCount`
7. Execute `pointerDownHandler`.
8. Wait `hold_ms` or at least one frame by default.
9. Execute `pointerUpHandler`.
10. Execute `pointerClickHandler` only if press and release target still match or if `strict=false`.
11. Set selected object through `EventSystem.SetSelectedGameObject`.
12. Wait `settle_frames`.
13. Return executed handler list and final selected object.

### Drag algorithm

1. Inspect start point.
2. Execute `initializePotentialDrag`.
3. Execute `pointerDown`.
4. Determine `pointerDrag` with `ExecuteEvents.GetEventHandler<IDragHandler>`.
5. Execute `beginDragHandler`.
6. For each step:
   - update `position`, `delta`, `dragging = true`.
   - execute `dragHandler`.
   - wait one frame if requested.
7. Execute `pointerUp`.
8. Execute `endDragHandler`.
9. If final raycast has drop target, execute `dropHandler`.

### Scroll algorithm

1. Inspect point.
2. Set `scrollDelta`.
3. Use `ExecuteEvents.ExecuteHierarchy` with `scrollHandler`.

### Submit algorithm

1. Resolve target.
2. Set selected object.
3. Execute `submitHandler`.

### Key/text fallback

For selected uGUI fields:

1. Set selected object.
2. Send `updateSelectedHandler`.
3. For TMP/InputField, prefer InputSystem backend for actual text.
4. Do not mutate text properties directly in the input tool; use `manage_components set` for direct state edits.

---

## Backend 2: Input System

This backend is for gameplay and project code that reads input devices directly.

### Requirements

1. Do not compile-reference `Unity.InputSystem` from `HeraAgent.Editor` unless the asmdef is updated with optional package references and version defines.
2. Prefer reflection for phase 1 of this backend:
   - Detect `UnityEngine.InputSystem.InputSystem, Unity.InputSystem`.
   - Detect device classes (`Mouse`, `Keyboard`, `Touchscreen`, `Gamepad`).
   - Detect low-level state structs (`MouseState`, `KeyboardState`) if needed.
3. Do not use `InputTestFixture` in runtime tool code.
   - It resets global InputSystem state and is meant for tests.
   - It may disturb the user's project state.

### Actions

```bash
input key --key Space --backend inputsystem
input text --value "player1" --backend inputsystem
input click --position 540,960 --backend inputsystem
input touch --position 540,960 --phase began|moved|ended
```

### Algorithm

1. Resolve `InputSystem` assembly.
2. Resolve or create device only if safe.
   - If `Keyboard.current` exists, use it.
   - If not, return `INPUTSYSTEM_DEVICE_UNAVAILABLE` unless `--create_device true`.
3. Queue state/text event.
4. Call `InputSystem.Update()` only if `--update true` or if the backend requires immediate processing.
5. Wait `settle_frames`.
6. Return device/control summary.

### Safety

InputSystem events can affect actual gameplay state. This is intended in Play Mode but surprising in Edit Mode. Default policy:

- Allow `key` and `text` in Play Mode.
- In Edit Mode, require `--allow_edit_mode true` for InputSystem backend.
- Never reset the whole InputSystem from this tool.

---

## Backend 3: Native Win32

This backend is optional and Windows-only. It should be implemented last.

### Why it exists

Some QA standards require evidence that a physical OS click path was attempted. Computer Use may be blocked because Unity screenshot state is unavailable. Hera can still send native Windows input if it can locate the Unity window and GameView child HWND.

### Evidence from current environment

The investigated Unity process exposed:

- Top-level Unity Editor HWND from `Process.MainWindowHandle`.
- Child HWND with class `UnityGUIViewWndClass` and title `UnityEditor.GameView`.
- Current top-level window was minimized (`IsIconic=true`), which explains why native clicking is unsafe without restoring the window.

### Required behavior

Native backend must:

1. Be Windows-only.
2. Fail with `INPUT_NATIVE_UNSUPPORTED_OS` on other platforms.
3. Detect minimized Unity windows and fail with `INPUT_WINDOW_MINIMIZED` unless `--restore true`.
4. Locate child GameView HWND by:
   - class `UnityGUIViewWndClass`
   - title `UnityEditor.GameView`
   - process ID match
5. Map target point to GameView client coordinates.
6. Convert client coordinates to screen coordinates through `ClientToScreen`.
7. Optionally set foreground only with `--focus true`.
8. Save and restore mouse cursor position unless `--restore_mouse false`.
9. Use `SendInput` for down/up.
10. Return HWND, rect, screen point, and whether focus/restore was performed.

### Do not use as default

Native input is fragile:

- It depends on focus and window state.
- It can interfere with the user's mouse.
- It is platform-specific.
- DPI scaling and multi-monitor coordinates can break naive math.

Therefore `backend=auto` must not select native backend in phase 1.

---

## UI Toolkit Runtime Support

UI Toolkit is not phase 1.

Confirmed APIs exist:

- `IPanel.Pick(Vector2)`
- `VisualElement.SendEvent(EventBase)`
- `PointerEventBase<T>.GetPooled(...)`
- `MouseEventBase<T>.GetPooled(...)`
- `PointerEventHelper.GetPooled(...)`

However UI Toolkit event creation has more internal state and panel-coordinate rules than uGUI. Support should be added as `backend=uitoolkit` only after uGUI and InputSystem are stable.

Phase 2 tasks:

1. Detect `UIDocument`.
2. Pick panel element at screen point.
3. Send pooled pointer/mouse events.
4. Return picked visual element name, type, and hierarchy.

---

## QA Classification

Agents should not report all input checks as the same level of evidence.

Use this language:

```text
Physical OS click QA: BLOCKED, because Computer Use could not acquire Unity screenshot state.
Unity EventSystem input QA: PASS, target was hit through EventSystem.RaycastAll and pointer handlers executed.
InputSystem gameplay QA: PASS, queued device events were processed by InputSystem.
Visual result QA: PASS, ui_doc capture/screenshot showed expected state.
```

The input tool should return an `evidence_level` field:

| Value | Meaning |
|:---|:---|
| `eventsystem` | Unity UI event path verified. |
| `inputsystem` | New Input System device path verified. |
| `native_os` | OS-level Windows input attempted. |
| `handler_only` | Direct handler invocation only; should be avoided for pass/fail QA. |

---

## Implementation Tasks

### Phase 0: Preflight and API lock

Acceptance:

- The connected Editor can still run `doctor`, `status`, and `list --compact`.
- No code has been changed yet.

Tasks:

1. Re-run bootstrap:
   - `hera-agent-unity doctor --json`
   - `hera-agent-unity status`
   - `hera-agent-unity list --compact`
2. Query the live Editor for API availability:
   - `describe_type UnityEngine.EventSystems.EventSystem --members all`
   - `describe_type UnityEngine.EventSystems.PointerEventData --members all`
   - `describe_type UnityEngine.EventSystems.ExecuteEvents --members properties`
   - `describe_type UnityEngine.UI.GraphicRaycaster --members all`
   - `list_assemblies --filter InputSystem --include_location`
3. Record any Unity-version deltas in `docs/COMMANDS.md` notes if discovered.

QA:

- `hera-agent-unity console --type error --lines 20` returns no new connector errors.

### Phase 1: Add EventSystem input core

Files:

- `AgentConnector/Editor/Tools/Input.cs`
- `AgentConnector/Editor/Core/InputQaResolver.cs`
- `AgentConnector/Editor/Core/InputQaEventSystem.cs`
- `.meta` files for new C# files

Tasks:

1. Create `input` Hera tool with action parsing:
   - `action` parameter wins.
   - Else `args[0]`.
   - Valid actions: `inspect`, `click`, `pointer_down`, `pointer_up`, `drag`, `scroll`, `submit`, `state`.
2. Add parameter schema:
   - `action`
   - `backend`
   - `instance_id`
   - `path`
   - `target`
   - `position`
   - `normalized`
   - `offset`
   - `button`
   - `click_count`
   - `hold_ms`
   - `settle_frames`
   - `strict`
   - `details`
3. Implement target resolution:
   - Reuse `TargetResolver.ResolveGameObject`.
   - Add local helper to parse `target` as int ID or path.
   - Return `INPUT_TARGET_NOT_FOUND` and `INPUT_TARGET_AMBIGUOUS` where applicable.
4. Implement point resolution:
   - `RectTransformUtility.WorldToScreenPoint` for target rect center.
   - Screen-space overlay support.
   - Screen-space camera support.
   - World-space canvas support with event camera.
5. Implement EventSystem resolution:
   - `EventSystem.current`.
   - Version-gated object lookup fallback.
   - Return input module type in diagnostics.
6. Implement raycast stack:
   - Create `PointerEventData`.
   - Call `EventSystem.RaycastAll`.
   - Convert hits to compact shapes using `EntityIdCompat` and `HierarchyPath`.
7. Implement blockers:
   - Top hit not target -> `blocked_by`.
   - If strict click, fail with `INPUT_TARGET_BLOCKED`.
8. Implement interactability checks:
   - `Selectable.interactable`.
   - Parent `CanvasGroup` rules.
   - `Graphic.raycastTarget` for UI graphics.
9. Implement `inspect`.
10. Implement `click` event sequence:
    - pointer enter
    - pointer down
    - one frame or `hold_ms`
    - pointer up
    - pointer click
    - select target
    - settle frames
11. Implement `submit`.
12. Implement `scroll`.
13. Implement `drag`.
14. Make all waits use `Task<object>` and editor-frame delay.

QA:

- `hera-agent-unity editor refresh --compile --timeout 120000`
- `hera-agent-unity list --compact` contains `input`.
- `hera-agent-unity list --tool input --compact-json` returns schema.
- `hera-agent-unity input state --compact-json` returns EventSystem status.
- `hera-agent-unity console --type error --lines 20`.

### Phase 2: Add EventSystem test scene or self-test

Files:

- `AgentConnector/Editor/Tests/InputQaTests.cs`
- Optional temporary scene/object creation inside tests only

Tasks:

1. Create a test helper that builds:
   - Canvas
   - GraphicRaycaster
   - EventSystem
   - Button
   - Blocking panel
   - Slider-like draggable object
2. Test `inspect` target hit.
3. Test `inspect` blocked target.
4. Test `click` triggers pointer handler.
5. Test `click` fails when blocked in strict mode.
6. Test `submit` invokes submit handler.
7. Test `drag` calls begin/drag/end in order.
8. Test inactive target error.
9. Cleanup all created objects.

QA:

- Run the menu test through `hera-agent-unity menu "HeraAgent/Tests/InputQa"` if using manual menu tests.
- Prefer Unity Test Framework tests if existing test runner supports the assembly.
- `hera-agent-unity test --mode EditMode --filter HeraAgent.Tests.InputQaTests` if converted to UTF tests.
- `hera-agent-unity console --type error --lines 50`.

### Phase 3: Add help and command docs

Files:

- `cmd/help/input.txt`
- `docs/COMMANDS.md`
- `docs/INDEX.md`
- `README.md`
- `README.ko.md`

Tasks:

1. Add `cmd/help/input.txt`.
2. Add `input` to `cmd/help/general.txt`.
3. Add full command reference in `docs/COMMANDS.md`.
4. Add design doc link in `docs/INDEX.md`.
5. Add concise README mention under Unity QA / common commands.
6. Add Korean README equivalent.

QA:

- `hera-agent-unity help input` prints the help text.
- `go test ./cmd`.

### Phase 4: Add optional Input System backend

Files:

- `AgentConnector/Editor/Core/InputQaInputSystem.cs`
- Possibly update `AgentConnector/Editor/HeraAgent.asmdef` only if reflection is insufficient.

Tasks:

1. Implement reflection-based type detection.
2. Add `input state --backend inputsystem`.
3. Implement keyboard key press/release.
4. Implement text input through `QueueTextEvent`.
5. Implement mouse move/down/up by screen position.
6. Implement optional device creation behind `--create_device true`.
7. Ensure Edit Mode requires `--allow_edit_mode true`.
8. Do not use `InputTestFixture` in production tool.

QA:

- In Play Mode, queue a key and verify a test MonoBehaviour observed an InputAction or device state.
- `hera-agent-unity console --type error --lines 50`.

### Phase 5: Add optional native Windows backend

Files:

- `AgentConnector/Editor/Core/InputQaNativeWin32.cs`

Tasks:

1. Add `#if UNITY_EDITOR_WIN` guarded implementation.
2. P/Invoke:
   - `EnumChildWindows`
   - `GetWindowThreadProcessId`
   - `GetClassName`
   - `GetWindowText`
   - `GetWindowRect`
   - `ClientToScreen`
   - `IsIconic`
   - `ShowWindow`
   - `SetForegroundWindow`
   - `GetCursorPos`
   - `SetCursorPos`
   - `SendInput`
3. Locate GameView child HWND.
4. Add `input state --backend native-win32`.
5. Add `input click --backend native-win32`.
6. Fail if minimized unless `--restore true`.
7. Focus only with `--focus true`.
8. Restore mouse unless disabled.

QA:

- Minimized Unity returns `INPUT_WINDOW_MINIMIZED`.
- Restored Unity can locate GameView HWND.
- Native click returns `native_os` evidence with hwnd and screen point.
- Manual visual verification through `screenshot --view game` or `ui_doc capture`.

### Phase 6: Integrate QA recipes into agent rules

Files:

- `AGENTS.md`
- `cmd/doctor_agent_rules.go`
- `.github/copilot-instructions.md`
- `.cursor/rules/hera-agent-unity.mdc`
- `.agents/skills/hera-agent-unity/SKILL.md`
- `CLAUDE.md`

Tasks:

1. Add rule: if Computer Use physical click is blocked, try Hera `input inspect` and `input click`.
2. Add rule: distinguish physical OS click QA from Unity EventSystem QA.
3. Add blocked wording:
   - `Physical OS click QA: BLOCKED`
   - `Unity EventSystem input QA: PASS/FAIL`
4. Regenerate or sync tool-specific rules using existing project process.

QA:

- `hera-agent-unity doctor --agent-rules` includes the new guidance when expected.
- Documentation does not contain local absolute paths.

### Phase 7: Release hygiene

Files:

- `AgentConnector/package.json`
- `CHANGELOG.md`

Tasks:

1. Bump connector package version if C# connector changed.
2. Do not bump CLI git tag unless Go code changed.
3. Add changelog entry with:
   - New `input` tool.
   - EventSystem backend.
   - InputSystem/native backend status if included.
   - Known limitations.

QA:

- `go test ./...`
- `git diff --check`
- `hera-agent-unity editor refresh --compile --timeout 120000`
- `hera-agent-unity console --type error --lines 50`
- `hera-agent-unity list --compact`
- `hera-agent-unity list --tool input --compact-json`

---

## Acceptance Criteria

The feature is complete only when all applicable criteria pass:

1. `input` appears in `list --compact`.
2. `list --tool input` documents actions and flags.
3. `input inspect --path <button>` reports raycast stack and top-hit status.
4. `input click --path <button>` drives actual EventSystem pointer handlers, not handler-only invocation.
5. A blocked button returns `INPUT_TARGET_BLOCKED`.
6. Missing EventSystem returns `INPUT_NO_EVENT_SYSTEM`.
7. Non-interactable Selectable returns `INPUT_TARGET_NOT_INTERACTABLE`.
8. Console has no errors after the command.
9. Visual state can be checked with `ui_doc capture` or `screenshot --view game`.
10. Documentation tells agents how to classify physical vs Unity-level QA.
11. No UnityEngine.Object is returned directly in data.
12. Default payload remains compact.

---

## Must Not Have

1. No default native OS clicks.
2. No direct `Button.onClick.Invoke()` as the primary click path.
3. No unconditional scene mutation to add EventSystem or Canvas.
4. No dependency on Computer Use.
5. No dependency on `Unity.InputSystem.TestFramework` for normal tool execution.
6. No broad `console --lines 0` in docs examples.
7. No local absolute paths in checked-in docs.
8. No Unity 6000.5 obsolete API warnings.
9. No concurrent command assumptions; all waits must work under `CommandRouter` serialization.

---

## Recommended First Implementation Slice

Implement only this first:

```text
input state
input inspect --path|--instance_id
input click --path|--instance_id --backend eventsystem
```

Do not start with InputSystem or native Windows. The EventSystem slice solves the reported QA blocker for uGUI buttons with the lowest risk and provides the diagnostics needed to decide whether further backends are necessary.

Minimum manual QA sequence:

```bash
hera-agent-unity editor refresh --compile --timeout 120000
hera-agent-unity list --compact
hera-agent-unity editor play --wait
hera-agent-unity input inspect --path /Canvas/StartButton --details true
hera-agent-unity input click --path /Canvas/StartButton --settle_frames 2
hera-agent-unity console --type error --lines 20
hera-agent-unity ui_doc capture --out captures/after-click.png
hera-agent-unity editor stop
```

If the project has no suitable button, create a temporary test scene/object in `InputQaTests` rather than relying on a user project scene.
