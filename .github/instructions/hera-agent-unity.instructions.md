---
applyTo: "**/*.cs,**/*.unity,**/*.prefab,**/*.asmdef,**/*.mat,**/*.asset,**/Assets/**"
---

# Unity Editor Instructions

These files affect Unity Editor behavior. Prefer `hera-agent-unity` before guessing.

## First Checks

Run these when the task depends on a live Unity Editor:

1. `hera-agent-unity doctor --json`
2. `hera-agent-unity status`
3. `hera-agent-unity list --compact`

Use `console --type error --lines 20` before assuming compilation or runtime errors.

## Ultra Hera

Ultra Hera tells the agent how carefully to verify Unity work after using Hera.

- `light` is the default. Light loop: confirm the goal, read only needed state, change code/scene/Inspector, verify compile or state, check console errors, re-read the changed target, retry 1-2 times if needed, and report evidence.
- `ultra` applies Light to every task, then upgrades strict requests to stronger checks: console errors must be 0, Inspector/GameObject/asset state is re-read, PlayMode or Unity tests run when relevant, and screenshots or `ui_doc` capture are used for visual work.

Use Ultra for requests like "play it and confirm", "match the UI", "check the Inspector too", `정확히 검증해줘`, or `인스펙터까지 확실히 봐줘`.

## Low-Payload Commands

- `list --compact` for tool discovery.
- `find_gameobjects --ids` for follow-up object edits.
- `find_gameobjects --fields instance_id,name,path` when names may collide.
- `scene info` for active scene state.
- `describe_type`, `find_method`, and `unity_docs` before guessing Unity API signatures.
- `ui_doc export/apply/capture/sample` for Unity UI work.

## C# Connector Safety

- Add `.meta` files with new Unity assets or scripts under `AgentConnector`.
- Watch for C# name conflicts: `Object`, `PackageInfo`, `Random`, `Debug`.
- Keep connector code compatible with Unity 2022.3+, 2023.2, 6000.0, 6000.3, and 6000.5+.
- Use `UnityVersionCompat` for version-specific docs and API behavior.
- Do not return Unity objects directly from `exec`; return primitive fields or IDs.
