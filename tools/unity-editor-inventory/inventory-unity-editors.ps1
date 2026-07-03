param(
    [string[]]$Versions = @(
        "2022.3.62f2",
        "2023.2.22f1",
        "6000.0.35f1",
        "6000.3.5f2",
        "6000.5.0f1"
    ),
    [string]$HubRoot = (Join-Path $env:ProgramFiles "Unity\Hub\Editor"),
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Get-FirstPaths {
    param(
        [string]$Root,
        [string]$Filter,
        [int]$Limit = 8
    )

    if (-not (Test-Path -LiteralPath $Root)) {
        return @()
    }

    return @(
        Get-ChildItem -LiteralPath $Root -Recurse -Filter $Filter -File -ErrorAction SilentlyContinue |
            Select-Object -First $Limit -ExpandProperty FullName
    )
}

function Get-PackageVersion {
    param(
        [string]$PackageRoot,
        [string]$PackageName
    )

    $path = Join-Path $PackageRoot "$PackageName\package.json"
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    try {
        $pkg = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        return $pkg.version
    }
    catch {
        return "parse_error"
    }
}

function Get-Count {
    param(
        [string]$Path,
        [string]$Filter = "*"
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0
    }

    return @(
        Get-ChildItem -LiteralPath $Path -Filter $Filter -File -ErrorAction SilentlyContinue
    ).Count
}

function Get-DirectoryCount {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0
    }

    return @(
        Get-ChildItem -LiteralPath $Path -Directory -ErrorAction SilentlyContinue
    ).Count
}

function Shorten-Path {
    param([string]$Path)

    if ([string]::IsNullOrEmpty($Path)) {
        return ""
    }

    $normalizedRoot = $HubRoot.TrimEnd("\", "/")
    return $Path.Replace($normalizedRoot, "%UNITY_HUB_EDITOR%")
}

$rows = foreach ($version in $Versions) {
    $root = Join-Path $HubRoot $version
    $data = Join-Path $root "Editor\Data"
    $managed = Join-Path $data "Managed"
    $packageRoot = Join-Path $data "Resources\PackageManager\BuiltInPackages"
    $unityExe = Join-Path $root "Editor\Unity.exe"
    $monoExe = Join-Path $data "MonoBleedingEdge\bin\mono.exe"
    $unityEditorDll = Join-Path $managed "UnityEditor.dll"
    $coreModule = @(Get-FirstPaths -Root $managed -Filter "UnityEngine.CoreModule.dll" -Limit 1)

    [pscustomobject]@{
        version = $version
        unityExe = $unityExe
        exeExists = Test-Path -LiteralPath $unityExe
        dataPath = $data
        dataExists = Test-Path -LiteralPath $data
        cscDlls = @(Get-FirstPaths -Root $data -Filter "csc.dll")
        cscExes = @(Get-FirstPaths -Root $data -Filter "csc.exe")
        dotnets = @(Get-FirstPaths -Root $data -Filter "dotnet.exe")
        monoExe = $monoExe
        monoExists = Test-Path -LiteralPath $monoExe
        managedDllCount = Get-Count -Path $managed -Filter "*.dll"
        unityEditorDll = $unityEditorDll
        unityEditorDllExists = Test-Path -LiteralPath $unityEditorDll
        unityEngineDllCount = Get-Count -Path $managed -Filter "UnityEngine*.dll"
        coreModule = if ($coreModule.Count -gt 0) { $coreModule[0] } else { $null }
        builtInPackageCount = Get-DirectoryCount -Path $packageRoot
        uguiVersion = Get-PackageVersion -PackageRoot $packageRoot -PackageName "com.unity.ugui"
        textMeshProVersion = Get-PackageVersion -PackageRoot $packageRoot -PackageName "com.unity.textmeshpro"
        entitiesVersion = Get-PackageVersion -PackageRoot $packageRoot -PackageName "com.unity.entities"
        documentationScriptReferenceExists = Test-Path -LiteralPath (Join-Path $data "Documentation\en\ScriptReference")
    }
}

if ($Json) {
    $rows | ConvertTo-Json -Depth 6
    exit 0
}

"# Unity Editor Inventory"
""
"Path token: ``%UNITY_HUB_EDITOR%``"
"Default resolver: ``%ProgramFiles%\Unity\Hub\Editor`` on Windows Unity Hub installs. Override with ``-HubRoot`` when needed."
""
"| Unity | Editor | uGUI | TMP | Entities | Built-in packages | Primary csc.dll | Primary dotnet |"
"|---|---:|---:|---:|---:|---:|---|---|"
foreach ($row in $rows) {
    $csc = if ($row.cscDlls.Count -gt 0) { Shorten-Path $row.cscDlls[0] } else { "" }
    $dotnet = if ($row.dotnets.Count -gt 0) { Shorten-Path $row.dotnets[0] } else { "" }
    $editor = if ($row.exeExists) { "yes" } else { "no" }
    $ugui = if ($row.uguiVersion) { $row.uguiVersion } else { "" }
    $tmp = if ($row.textMeshProVersion) { $row.textMeshProVersion } else { "" }
    $entities = if ($row.entitiesVersion) { $row.entitiesVersion } else { "" }
    "| ``$($row.version)`` | $editor | ``$ugui`` | ``$tmp`` | ``$entities`` | $($row.builtInPackageCount) | ``$csc`` | ``$dotnet`` |"
}
