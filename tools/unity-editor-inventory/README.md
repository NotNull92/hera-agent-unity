# Unity Editor Inventory

This tool records the Unity Hub editor layouts that affect Hera compatibility:
compiler discovery, bundled runtime paths, managed assemblies, and built-in
package versions.

Run from the repository root on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-editor-inventory/inventory-unity-editors.ps1
```

Emit machine-readable JSON:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-editor-inventory/inventory-unity-editors.ps1 -Json
```

Scan a custom version list:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-editor-inventory/inventory-unity-editors.ps1 `
  -Versions 2022.3.62f2,2023.2.22f1,6000.0.35f1,6000.3.5f2,6000.5.0f1
```

The output is intentionally focused on paths consumed by Hera code:

- `ExecCompileCache` and the settings window compiler path detection.
- `UnityVersionCompat`, `UnityDocsStore`, and docs bucket selection.
- `UiDocFixer`, `UiDocSchema`, and uGUI package rule selection.
- `describe_type`, `find_method`, and `list_assemblies` ground-truth probes.
