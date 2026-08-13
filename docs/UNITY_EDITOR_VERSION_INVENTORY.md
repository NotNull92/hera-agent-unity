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

### Connector 0.0.104 three-bucket gate (2026-08-13)

`compile-exact-source.ps1` passed with zero warnings in all three buckets; each
bucket then ran the release-gate suite and the asset-handle matrix.

| Bucket | Representative | Fixture | Release-gate tests | Handle matrix |
|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 17/17 | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 17/17 | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 17/17 | PASS |

Identical per bucket: a guid emitted by `manage_assets find` round-tripped into
`deps`, `manage_material get`, and `manage_asset_import get`, each reporting the
resolved path; three materials embedded in one container returned red, green,
and blue through their own `guid:<guid>:<fileId>` handles; a `Packages/` handle
was refused by a mutating action with the resolved path named; and a handle was
refused as a create destination.

On `6000.3.5f2` additionally: the GUID still resolved after `manage_assets
move` while the recorded path failed, a scene-object GlobalObjectId returned
`NOT_AN_ASSET`, and a main-asset handle passed to `manage_material` returned
`NOT_A_MATERIAL` naming the resolved type.

The catalog kept 33 tools / 111 actions — only parameter descriptions grew
(assets and full profiles +1930 bytes, scene +909) — and the baseline was
regenerated in the same review.


### Connector 0.0.103 three-bucket gate (2026-08-13)

Fix-only release: three tools serialized their results in PascalCase while
their schemas declared snake_case. `compile-exact-source.ps1` passed with zero
warnings in all three buckets; each bucket then ran the release-gate suite —
which now includes a check over all 60 declared result types — and a
schema-versus-payload sweep over the affected actions.

| Bucket | Representative | Fixture | Release-gate tests | Conformance sweep |
|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 17/17 | 9/9 conformant |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 17/17 | 9/9 conformant |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 17/17 | 9/9 conformant |

The catalog regenerated byte-for-byte (`--fail-on-change` clean), which is the
evidence that the schema was already right and only the wire format was wrong.
The sweep's tenth row, `build status`, is reported as undeclared rather than
non-conformant: that action declares no `ResultType`, so its `data` schema is
an open object and extra fields are permitted. That gap is recorded in
`docs/handoffs/ACTIVE.md`.

Note for future gates: `compile-exact-source.ps1` builds `HeraAgent.Editor` and
`HeraAgent.TestRunner` only. A compile error introduced in
`AgentConnector/Editor/Tests/` passes that script and surfaces only when a
`testables` Editor compiles the test assembly — which happened while writing
the new check.


### Connector 0.0.102 three-bucket gate (2026-08-13)

Fix-only release: the test-callback registration leak. `compile-exact-source.ps1`
passed with zero errors and zero warnings in all three buckets; each bucket
then ran the release-gate suite three times in one session while counting
`RunTests+TestCallbacks` registrations in the framework's callbacks holder.

| Bucket | Representative | Fixture | Leaked callbacks after 3 runs | Release-gate tests | Status |
|---|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 0 | 17/17 ×3 | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 0 | 17/17 ×4 | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 0 | 17/17 ×3 | PASS |

Before the fix, the same measurement on `6000.3.5f2` from a clean Editor read
0 → 1 → 2 over two EditMode runs, and file-based instrumentation inside
`DisposeApi` reported `apiAlive=False` on every run. The abandoned-client
reproduction (killing the CLI 200/600/1500 ms into a run) grew the count
1→2→3→4→5→6 before the fix and stayed flat at 1 — Unity's own
`PerformanceTestRunSaver`, the only non-Hera registration — after it.

### Connector 0.0.101 three-bucket gate (2026-08-13)

`compile-exact-source.ps1` passed with zero errors and zero warnings in all
three buckets; each bucket then enabled `testables`, ran the release-gate
suite, exercised `manage_assets deps`, and had its manifest restored
byte-for-byte.

| Bucket | Representative | Fixture | Release-gate tests | Status |
|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 17/17 | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 17/17 | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 17/17 | PASS |

Per bucket, against a material referenced by one prefab: forward listed the
material and excluded the prefab itself; reverse listed exactly the prefab;
reverse on an unreferenced material returned an empty list; `--scope all`
returned the same hit over the whole project; a missing `--direction` was
refused by the strict schema; and a nonexistent path returned
`ASSET_NOT_FOUND` rather than an empty list.

Reverse-scan cost, reported by the action itself:

| Bucket | `Assets/` scope | `all` scope |
|---|---|---|
| `6000.0.35f1` | 11 scanned, 17 ms | 10180 scanned, 2227 ms |
| `6000.3.5f2` | 11 scanned, 24 ms | 10332 scanned, 1555 ms |
| `6000.5.6f1` | 47 scanned, 166 ms | 10846 scanned, 3667 ms |

The same run carried a create-then-query-in-one-call probe comparing Unity
Search's `ref:` against the AssetDatabase scan, for a reference that had just
been written:

| Bucket | Unity Search `ref:` | AssetDatabase scan | Truth |
|---|---|---|---|
| `6000.0.35f1` | 1 | 1 | 1 |
| `6000.3.5f2` | **0** | 1 | 1 |
| `6000.5.6f1` | **0** | 1 | 1 |

That intermittency is why `deps` does not use Unity Search
(`docs/ASSET_DEPENDENCY_DESIGN.md`).

### Connector 0.0.100 three-bucket gate (2026-08-13)

`compile-exact-source.ps1` passed with zero errors and zero warnings in all
three buckets, then each bucket enabled `testables`, ran the Connector's
release-gate suite, exercised the prefab surface, and had its manifest
restored byte-for-byte.

| Bucket | Representative | Fixture | Release-gate tests | Status |
|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 17/17 | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 17/17 | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 17/17 | PASS |

Per-bucket prefab matrix: `create` reported `asset_type: Regular`; `create`
from an **inactive** GameObject succeeded (it failed before, because
`GameObject.Find` cannot see inactive objects); `list_overrides` on an edited
instance reported the Rigidbody override, and `--include_default` added the
instance root's own GameObject and Transform — the difference between one
entry and three, and on a root-only edit the difference between "no
overrides" and the truth; targeting a child returned `instance_root: /P`;
`apply` cleared the overrides and a fresh instantiate read the applied value;
`add_component --child` landed on the descendant; `create` from an instance
reported `asset_type: Variant`; `unpack` without `--mode` was refused by the
strict schema; `unpack --mode outermost` succeeded and the object then
reported `NOT_A_PREFAB_INSTANCE`.

On `6000.3.5f2` the full round trip was additionally verified end to end:
edit → `list_overrides` → `apply` → destroy → re-instantiate read the applied
mass, and a further edit followed by `revert` restored the applied value.

All gate results were taken from clean Editor sessions. See the wave-6 entry
in `docs/handoffs/ACTIVE.md` for an unresolved Test Runner condition observed
in a long-running session.

### Connector 0.0.99 three-bucket gate (2026-08-13)

The working-tree Connector installed as a `file:` UPM dependency in each
bucket's fixture. `tools/verify-unity-package/compile-exact-source.ps1` passed
with zero errors and zero warnings in all three buckets before any Editor
launch.

**This gate ran the Connector's own release-gate tests for the first time.**
Earlier gates compiled the package and read the console but never executed
`HeraAgent.Editor.Tests`, which is why seven stale expectations shipped red
from `0.0.92` onward. Each bucket enabled `testables` for the run and had its
manifest restored byte-for-byte afterwards.

| Bucket | Representative | Fixture | Release-gate tests | Status |
|---|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | 17/17 | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | `test6000.3.5f2` | 17/17 | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | 17/17 | PASS |

Per-bucket functional smokes: `test list` returned the assembly/category
summary, `test cancel` on an idle port reported `was_running: false`, a run
narrowed by a nonexistent category returned `NO_TESTS_MATCHED`, and
`manage_packages search` resolved a package with its compatible-version list.
That last check also demonstrates why the field exists: `com.unity.ai.navigation`
reported six compatible versions on `6000.3.5f2` and one on `6000.5.6f1`.

On `6000.3.5f2` the cancel path was exercised against a live 30-second
PlayMode test: a second run was refused with `TEST_RUN_ALREADY_RUNNING`,
`test cancel` reported `nunit_cancel_requested: true`, the waiting client was
released with `TEST_RUN_CANCELLED`, and the next run started normally.

### Connector 0.0.94 three-bucket gate (covers 0.0.92–0.0.94, 2026-08-12)

Exact working-tree source at `connector-0.0.94` installed as a `file:` UPM
dependency into Library-reset disposable fixtures; the `6000.3`–`6000.4`
bucket was covered by per-item live verification on the connected project.
Each bucket reached `ready`, reported zero console errors, discovered 30
tools, produced no `HeraAgent.Editor.Tests` assembly, and kept zero
`Editor/Tests` sources in the `HeraAgent.Editor` response file. Functional
smokes per bucket: `scene hierarchy --components` returned the fixture tree,
`profiler stats` read render statistics (`render_available=true` in every
bucket, so the reflected render surface exists on 6000.0 and 6000.5 too), and
a `manage_animation` create → `get_clip` → delete round trip preserved frame
rate and loop. On `6000.5.6f1` the `manage_editor` selection round trip was
re-exercised.

| Bucket | Representative | Fixture | Status |
|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | live project (per-item) | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | PASS |

### Connector 0.0.91 three-bucket gate (covers 0.0.90 + 0.0.91, 2026-08-12)

Exact working-tree source at `connector-0.0.91` installed as a `file:` UPM
dependency into Library-reset disposable fixtures; each bucket reached `ready`,
reported zero console errors, discovered 30 tools, resolved its own
`unity_docs` bucket, produced no `HeraAgent.Editor.Tests` assembly, and kept
zero `Editor/Tests` sources in the `HeraAgent.Editor` response file. The
instance-id round trip (`find_gameobjects` id → `manage_components list` →
`manage_editor set_selection`/`get_selection`) passed in every bucket.

| Bucket | Representative | Fixture | Status |
|---|---|---|---|
| `6000.0`–`6000.2` | `6000.0.35f1` | `Test6.0.35f1` | PASS |
| `6000.3`–`6000.4` | `6000.3.5f2` | live project | PASS |
| `6000.5+` | `6000.5.6f1` | `test6.5` | PASS |

On `6000.5.6f1` the EntityId → int conversion operator was confirmed present
and bound (reflected delegate), and its value matches `GetHashCode()` there —
unlike `6000.3.5f2`, where the two diverge and the operator is required.

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
