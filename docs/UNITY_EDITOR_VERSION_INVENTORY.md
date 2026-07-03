# Unity Editor Version Inventory

This document records the local Unity Editor layouts used to improve Hera's
version-specific accuracy. Regenerate the table with:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-editor-inventory/inventory-unity-editors.ps1
```

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
| `6000.5.0f1` | yes | `2.5.0` | `5.0.0` | `6.5.0` | 85 | `%UNITY_HUB_EDITOR%\6000.5.0f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll` | `%UNITY_HUB_EDITOR%\6000.5.0f1\Editor\Data\DotNetSdk\dotnet.exe` |

## Hera Implications

1. `6000.5.0f1` is the only checked version whose primary Roslyn compiler lives
   under `Data\DotNetSdk\sdk\<version>\Roslyn\bincore\csc.dll`. Earlier checked
   versions use `Data\DotNetSdkRoslyn\csc.dll`.
2. `6000.0.35f1` belongs to the `6000.0` docs bucket and still uses uGUI `2.0.0`.
3. `6000.5.0f1` is the first checked version with uGUI `2.5.0`, matching Hera's
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
- `UiDocFixer` has a menu smoke test that locks the uGUI package profile for
  every checked docs bucket: `2022.3 -> com.unity.ugui@1.0`,
  `2023.2`/`6000.0`/`6000.3 -> com.unity.ugui@2.0`, and
  `6000.5 -> com.unity.ugui@2.5`.
- Runtime `ui_doc apply` on `6000.0.35f1` returned `docs_version: 6000.0`
  and `ugui_package: com.unity.ugui@2.0`.
- `EntityIdCompat` now has a menu smoke test that verifies the int
  `instance_id` contract round-trips through the compatibility shim. Direct
  legacy ID API usage is isolated to `EntityIdCompat`; `manage_gameobject
  duplicate` uses the shim for source/clone comparison. Runtime duplicate
  probe on `6000.0.35f1` returned distinct source and clone IDs with no console
  errors.

## Next Tasks

- Refresh the versioned docs bundles only when Unity publishes a new
  ScriptReference revision or Hera adds another docs bucket.
