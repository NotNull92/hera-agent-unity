# Unity Editor Version Inventory

This document records the local Unity Editor layouts used to improve Hera's
version-specific accuracy. Regenerate the table with:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-editor-inventory/inventory-unity-editors.ps1
```

## Supported release-gate buckets

Hera targets Unity 6+ (`6000.0` minimum). The release gate covers three
buckets; representative Editors below. `2022.3.62f2` and `2023.2.22f1` remain
in the inventory as historical rows only — they are outside the supported
range.

| Bucket | Representative |
|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` |
| `6000.3`–`6000.4` | `6000.3.5f2` |
| `6000.5+` | `6000.5.6f1` |

## Current Inventory

Path token: `%UNITY_HUB_EDITOR%`

Default resolver: `%ProgramFiles%\Unity\Hub\Editor` on Windows Unity Hub
installs. Override the scanner with `-HubRoot` for non-default install roots.

| Unity | Editor | uGUI | TMP | Entities | Built-in packages | Primary csc.dll | Primary dotnet |
|---|---:|---:|---:|---:|---:|---|---|
| `2022.3.62f2` | yes | `1.0.0` |  |  | 59 | `%UNITY_HUB_EDITOR%\2022.3.62f2\Editor\Data\DotNetSdkRoslyn\csc.dll` | `%UNITY_HUB_EDITOR%\2022.3.62f2\Editor\Data\NetCoreRuntime\dotnet.exe` |
| `2023.2.22f1` | yes | `2.0.0` | `5.0.0` |  | 62 | `%UNITY_HUB_EDITOR%\2023.2.22f1\Editor\Data\DotNetSdkRoslyn\csc.dll` | `%UNITY_HUB_EDITOR%\2023.2.22f1\Editor\Data\NetCoreRuntime\dotnet.exe` |
| `6000.0.35f1` | yes | `2.0.0` | `5.0.0` |  | 66 | `%UNITY_HUB_EDITOR%\6000.0.35f1\Editor\Data\DotNetSdkRoslyn\csc.dll` | `%UNITY_HUB_EDITOR%\6000.0.35f1\Editor\Data\NetCoreRuntime\dotnet.exe` |
| `6000.3.5f2` | yes | `2.0.0` | `5.0.0` |  | 75 | `%UNITY_HUB_EDITOR%\6000.3.5f2\Editor\Data\DotNetSdkRoslyn\csc.dll` | `%UNITY_HUB_EDITOR%\6000.3.5f2\Editor\Data\NetCoreRuntime\dotnet.exe` |
| `6000.5.6f1` | yes | `2.5.0` | `5.0.0` | `6.5.0` | 85 | `%UNITY_HUB_EDITOR%\6000.5.6f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll` | `%UNITY_HUB_EDITOR%\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe` |

## Hera Implications

1. `6000.5.6f1` is the only checked version whose primary Roslyn compiler lives
   under `Data\DotNetSdk\sdk\<version>\Roslyn\bincore\csc.dll`. Earlier checked
   versions use `Data\DotNetSdkRoslyn\csc.dll`.
2. `6000.0.35f1` belongs to the `6000.0` docs bucket and still uses uGUI `2.0.0`.
3. `6000.5.6f1` is the checked representative with uGUI `2.5.0`, matching Hera's
   `6000.5` UI diagnostics bucket.
4. `2022.3.62f2` is the only checked version with uGUI `1.0.0`.
5. The installed editors do not include `Data\Documentation\en\ScriptReference`;
   Hera ships generated `unity_docs_*.jsonl.gz.bytes` bundles instead of reading
   local Editor documentation folders at runtime.
6. Compiler selection now prefers the running Editor's bundled tools before
   external SDKs, covering both `DotNetSdkRoslyn` and
   `DotNetSdk\sdk\<version>` layouts. Saved config paths that point inside a
   different Unity Editor install are ignored, while external SDK overrides
   remain valid.

## Completed Checks

### Connector 0.0.76 exact-source refactor compile matrix (development only)

The repository's current Connector and TestRunner sources were compiled with
`tools/verify-unity-package/compile-exact-source.ps1` against existing Bee
response files. The script does not launch or modify an Editor project and
fails on compiler warnings as well as errors.

| Bucket | Representative | Status |
|---|---|---|
| `2022.3` | `2022.3.62f2` | PASS |
| `2023.2` | `2023.2.22f1` | PASS |
| `6000.0`–`6000.2` | `6000.0.35f1` | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | PASS |
| `6000.5+` | `6000.5.6f1` | PASS |

### Connector 0.0.75 clean UPM compile matrix (development only)

| Bucket | Representative | Status | Evidence |
|---|---|---|---|
| `2022.3` | `2022.3.62f2` | PASS | `Test2022.3.62f2`, 2026-08-03. |
| `2023.2` | `2023.2.22f1` | PASS | `Test2023.2.22f1`, 2026-08-03. |
| `6000.0`–`6000.2` | `6000.0.35f1` | PASS | `Test6.0.35f1`, 2026-08-03. |
| `6000.3`–`6000.4` | `6000.3.5f2` | PASS | `test6000.3.5f2`, 2026-08-03. |
| `6000.5+` | `6000.5.6f1` | PASS | `test6.5`, 2026-08-03. |

The `2022.3.62f2` pass used Connector `0.0.75` as a normal local UPM
dependency before deleting the disposable project's `Library`. `Editor.log`
confirmed `Rebuilding Library because the asset database could not be found`,
reported `AssetDatabase: script compilation time: 18.981486s`, completed the
initial Asset Pipeline refresh in `91.793 seconds`, and completed project load
in `232.762 seconds`. UPM package resolution took `126.31 seconds`. The Editor
reached `ready`; the final `console --type error --lines 50` result contained
zero errors.

In the normal-install pass, `HeraAgent.Editor.rsp` contained zero sources under
`AgentConnector/Editor/Tests/`, no `HeraAgent.Editor.Tests.dll` existed, and the
loaded Hera assemblies were only `HeraAgent.Editor` and
`HeraAgent.TestRunner`. A separate test-enabled pass added the package to
`testables`, compiled `HeraAgent.Editor.Tests` from 24 test sources while the
production rsp still contained zero test sources, and loaded the test assembly
with zero console errors. The manifest was then restored: `testables` was
absent, the ScriptAssemblies test DLL was removed, and only the two normal Hera
assemblies remained loaded.

After the successful `2022.3.62f2` load, Unity's `Open Project` splash remained
visible even though the log recorded project-load completion and Hera reported
`ready`. Win32 inspection identified it as a `UnitySplashWindow` owned by the
same Unity process launched by Unity Hub. UPM connected before Hera assemblies
loaded, and Hera contains no Unity-project launch or `OpenProject` path, so the
splash is retained as a Unity 2022.3/Windows observation rather than a Hera
failure. A closely matching report exists for
[Unity 2022.3.62f1](https://discussions.unity.com/t/unity-2022-3-62f1-after-project-opened-the-open-project-page-cant-close/1682833).

The `2023.2.22f1` pass used Connector `0.0.75` as a normal local UPM
dependency before deleting the disposable project's `Library`. `Editor.log`
confirmed `Rebuilding Library because the asset database could not be found`,
reported `AssetDatabase: script compilation time: 36.266383s`, completed the
initial Asset Pipeline refresh in `56.062 seconds`, and completed project load
in `121.976 seconds`. UPM package resolution took `59.69 seconds`. The Editor
reached `ready`; the final `console --type error --lines 50` result contained
zero errors.

In the normal-install pass, `HeraAgent.Editor.rsp` contained zero sources under
`AgentConnector/Editor/Tests/`, no test rsp or `HeraAgent.Editor.Tests.dll`
existed, and only `HeraAgent.Editor` and `HeraAgent.TestRunner` were loaded. A
separate test-enabled pass added the package to `testables`, compiled
`HeraAgent.Editor.Tests` from 24 test sources while the production rsp still
contained zero test sources, and loaded the test assembly with zero console
errors. The manifest was then restored: `testables` was absent, the
ScriptAssemblies test DLL was removed, and only the two normal Hera assemblies
remained loaded.

The `6000.0.35f1` pass used Connector `0.0.75` as a normal local UPM
dependency after deleting the disposable project's `Library`. `Editor.log`
confirmed `Rebuilding Library because the asset database could not be found`,
reported `AssetDatabase: script compilation time: 59.562373s`, and completed
the compile phase in `64137.050ms`. The Editor reached `ready`; the final
`console --type error --lines 50` result contained zero errors.

In the normal-install pass, `HeraAgent.Editor.rsp` contained zero sources under
`AgentConnector/Editor/Tests/`, no `HeraAgent.Editor.Tests.dll` existed, and the
loaded Hera assemblies were only `HeraAgent.Editor` and
`HeraAgent.TestRunner`. A separate test-enabled pass added the package to
`testables`, compiled `HeraAgent.Editor.Tests` from 24 test sources while the
production rsp still contained zero test sources, and loaded the test assembly
with zero console errors. The manifest was then restored: `testables` was
absent, the ScriptAssemblies test DLL was removed, and only the two normal Hera
assemblies remained loaded.

During the clean import Unity's Script Updater emitted transient `CS0234`
messages from `com.unity.2d.pixel-perfect@5.0.3` before the successful final
compile. They were outside Hera and did not remain in the Unity console; this
template-specific observation is retained here rather than hidden.

The `6000.3.5f2` pass used Connector `0.0.75` as a normal local UPM
dependency after deleting the disposable project's `Library`. `Editor.log`
confirmed `Rebuilding Library because the asset database could not be found`,
reported `AssetDatabase: script compilation time: 52.279759s`, completed the
initial Asset Pipeline refresh in `293.611 seconds`, and resolved packages in
`184.90 seconds`. The Editor reached `ready`; the final
`console --type error --lines 50` result contained zero errors.

In the normal-install pass, `HeraAgent.Editor.rsp` contained zero sources under
`AgentConnector/Editor/Tests/`, no `HeraAgent.Editor.Tests.dll` existed, and
the loaded Hera assemblies were only `HeraAgent.Editor` and
`HeraAgent.TestRunner`. A separate test-enabled pass added the package to
`testables`, compiled `HeraAgent.Editor.Tests` from 24 test sources while the
production rsp still contained zero test sources, and loaded the test assembly
with zero console errors. The manifest was then restored byte-for-byte, the
package lock remained byte-for-byte unchanged, the ScriptAssemblies test DLL
was removed, and only the two normal Hera assemblies remained loaded.

The `6000.5.6f1` pass used Connector `0.0.75` as a normal local UPM dependency
after deleting the disposable project's `Library`. `Editor.log` confirmed
`Rebuilding Library because the asset database could not be found`, reported
`AssetDatabase: script compilation time: 26.700184s`, completed the initial
Asset Pipeline refresh in `186.758 seconds`, and resolved packages in
`101.14 seconds`. The clean import exposed three `CS0618` warnings in
`InputQaResolver` for Unity 6000.5 object-discovery overloads. Hera was updated
to use the non-deprecated `FindAnyObjectByType` and
`FindObjectsByType(FindObjectsInactive)` paths on 6000.5+, after which both the
6000.5 and 6000.3 live Editors recompiled with zero errors and zero warnings.
The `input state` path also executed successfully on both version branches.

In the normal-install pass, `HeraAgent.Editor.rsp` contained zero sources under
`AgentConnector/Editor/Tests/`, no `HeraAgent.Editor.Tests.dll` existed, and
the loaded Hera assemblies were only `HeraAgent.Editor` and
`HeraAgent.TestRunner`. A separate test-enabled pass added the package to
`testables`, compiled `HeraAgent.Editor.Tests` from 24 test sources while the
production rsp still contained zero test sources, and loaded the test assembly
with zero console errors or warnings. The manifest was then restored
byte-for-byte, the package lock remained byte-for-byte unchanged, the
ScriptAssemblies test DLL was removed, and only the two normal Hera assemblies
remained loaded.

- `6000.0.35f1` is covered by the `UnityVersionCompat` bucket test.
- `ExecCompileCache` has a menu smoke test for legacy `DotNetSdkRoslyn`,
  versioned `DotNetSdk`, legacy `NetCoreRuntime`, modern `DotNetSdk` dotnet,
  and stale Unity-bundled config rejection.
- Runtime probe on `6000.0.35f1` selected
  `%UNITY_HUB_EDITOR%\6000.0.35f1\Editor\Data\DotNetSdkRoslyn\csc.dll` and
  `%UNITY_HUB_EDITOR%\6000.0.35f1\Editor\Data\NetCoreRuntime\dotnet.exe`.
- `UnityDocsStore` has a menu smoke test that loads the bundled docs index,
  accepts either the current docs bucket or the `6000.0` fallback, verifies the
  legacy `GameObject` page, and verifies typo suggestions.
- Runtime `unity_docs GameObject` on `6000.0.35f1` returned
  `docs_version: 6000.0`.
- Exact ScriptReference bundles were generated from Unity's official offline
  documentation zips for all checked buckets: `2022.3` (28201 entries),
  `2023.2` (30573 entries), `6000.0` (31610 entries), `6000.3` (35442
  entries), and `6000.5` (41901 entries). Each bundle contains full member
  pages for representative checks such as `Rigidbody.mass`,
  `GameObject.AddComponent`, and `AssetDatabase.Refresh`.
- Heartbeat/status reporting now exposes the running Editor's docs bucket and
  compiler/runtime kind. Verified on `6000.0.35f1` with local CLI output:
  `Docs: 6000.0` and `Compiler: csc=external dotnet=external` for the current
  project override configuration.
- `EntityIdCompat` now has a menu smoke test that verifies the int
  `instance_id` contract round-trips through the compatibility shim. Direct
  legacy ID API usage is isolated to `EntityIdCompat`; `manage_gameobject
  duplicate` uses the shim for source/clone comparison. Runtime duplicate
  probe on `6000.0.35f1` returned distinct source and clone IDs with no console
  errors.

## Next Tasks

- Refresh the versioned docs bundles only when Unity publishes a new
  ScriptReference revision or Hera adds another docs bucket.
