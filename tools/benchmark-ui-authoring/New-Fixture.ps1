param(
    [Parameter(Mandatory = $true)][string]$TemplateProject,
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

$template = (Resolve-Path -LiteralPath $TemplateProject).Path
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$destinationFull = [IO.Path]::GetFullPath($Destination)

if (Test-Path -LiteralPath $destinationFull) {
    throw "Destination already exists; refusing to overwrite: $destinationFull"
}

$versionFile = Join-Path $template 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "Template is not a Unity project: $template"
}
$versionText = Get-Content -LiteralPath $versionFile -Raw
if ($versionText -notmatch 'm_EditorVersion:\s*([^\r\n]+)') {
    throw "Cannot read Unity version from $versionFile"
}
$unityVersion = $Matches[1].Trim()
if ($unityVersion -ne '6000.3.5f2') {
    throw "M0-M5 fixture is frozen to Unity 6000.3.5f2; template reports $unityVersion"
}

$templateManifestPath = Join-Path $template 'Packages\manifest.json'
$templateScenePath = Join-Path $template 'Assets\Scenes\SampleScene.unity'
if (-not (Test-Path -LiteralPath $templateManifestPath -PathType Leaf)) {
    throw "Template manifest is missing: $templateManifestPath"
}
if (-not (Test-Path -LiteralPath $templateScenePath -PathType Leaf)) {
    throw "Template baseline Scene is missing: $templateScenePath"
}
$templateManifest = Get-Content -LiteralPath $templateManifestPath -Raw | ConvertFrom-Json
$uguiProperty = $templateManifest.dependencies.PSObject.Properties['com.unity.ugui']
if ($null -eq $uguiProperty) {
    throw 'Template manifest must provide com.unity.ugui so the exact 6000.3-compatible version is pinned.'
}

# Build a deliberately tiny benchmark project. The old template copied 2D,
# Performance Test, IDE, URP, Visual Scripting, and other unrelated registry
# packages. Those packages can recompile/fail independently and corrupt the A/B.
[IO.Directory]::CreateDirectory($destinationFull) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $destinationFull 'Assets\Scenes')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $destinationFull 'Packages')) | Out-Null
Copy-Item -LiteralPath (Join-Path $template 'ProjectSettings') -Destination (Join-Path $destinationFull 'ProjectSettings') -Recurse

# Use the source Scene only for its engine-owned scene-settings prefix. Strip
# every GameObject so no URP/2D MonoBehaviour or asset reference remains.
$templateSceneText = [IO.File]::ReadAllText($templateScenePath)
$firstGameObject = $templateSceneText.IndexOf('--- !u!1 &', [StringComparison]::Ordinal)
if ($firstGameObject -le 0) {
    throw 'Template Scene does not contain an expected GameObject boundary.'
}
$minimalScene = $templateSceneText.Substring(0, $firstGameObject).TrimEnd() + [Environment]::NewLine
if ($minimalScene.Contains('GameObject:') -or $minimalScene.Contains('m_Script:')) {
    throw 'Minimal Scene extraction unexpectedly retained a GameObject or script reference.'
}
$scenePath = Join-Path $destinationFull 'Assets\Scenes\SampleScene.unity'
[IO.File]::WriteAllText($scenePath, $minimalScene, [Text.UTF8Encoding]::new($false))
foreach ($relative in @('Assets\Scenes.meta', 'Assets\Scenes\SampleScene.unity.meta')) {
    $source = Join-Path $template $relative
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        $target = Join-Path $destinationFull $relative
        Copy-Item -LiteralPath $source -Destination $target
    }
}

# A second empty Scene is the live-reset parking spot. Runs switch here before
# restoring SampleScene on disk, so the benchmark can reuse one warm Editor
# process without ever overwriting the Scene that is currently loaded.
$resetScenePath = Join-Path $destinationFull 'Assets\Scenes\__HeraABReset.unity'
[IO.File]::WriteAllText($resetScenePath, $minimalScene, [Text.UTF8Encoding]::new($false))
$resetMeta = @(
    'fileFormatVersion: 2',
    ('guid: ' + [Guid]::NewGuid().ToString('N')),
    'DefaultImporter:',
    '  externalObjects: {}',
    '  userData: ',
    '  assetBundleName: ',
    '  assetBundleVariant: '
) -join [Environment]::NewLine
[IO.File]::WriteAllText($resetScenePath + '.meta', $resetMeta + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

$benchmarkStateDirectory = Join-Path $destinationFull '.hera-ab'
[IO.Directory]::CreateDirectory($benchmarkStateDirectory) | Out-Null
$connectorSource = Join-Path $repository 'AgentConnector'
$connectorSnapshot = Join-Path $benchmarkStateDirectory 'Connector'
if (-not (Test-Path -LiteralPath $connectorSource -PathType Container)) {
    throw "Repository Connector source is missing: $connectorSource"
}
Copy-Item -LiteralPath $connectorSource -Destination $connectorSnapshot -Recurse

# Package tests are not part of the authoring benchmark and would pull the Test
# Framework back into the compile graph. Remove them only from the disposable
# snapshot, never from the repository package.
foreach ($relative in @('Editor\Tests', 'Editor\Tests.meta', 'Editor\TestRunner', 'Editor\TestRunner.meta')) {
    $path = Join-Path $connectorSnapshot $relative
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
$connectorPackage = Get-Content -LiteralPath (Join-Path $connectorSnapshot 'package.json') -Raw | ConvertFrom-Json

# Keep only Hera, uGUI, and Unity's built-in engine modules. Hera resolves
# Newtonsoft through its own package dependency; uGUI resolves ui/imgui too,
# while the explicit built-in module list preserves Hera's full engine compile
# surface without pulling unrelated registry packages into the fixture.
$dependencies = [ordered]@{}
$connectorPath = $connectorSnapshot.Replace('\', '/')
$dependencies['com.notnull92.hera-agent-unity'] = 'file:' + $connectorPath
$dependencies['com.unity.ugui'] = [string]$uguiProperty.Value
foreach ($property in @($templateManifest.dependencies.PSObject.Properties | Sort-Object Name)) {
    if ($property.Name.StartsWith('com.unity.modules.', [StringComparison]::Ordinal)) {
        $dependencies[$property.Name] = [string]$property.Value
    }
}
$manifest = [ordered]@{ dependencies = $dependencies }
$manifestPath = Join-Path $destinationFull 'Packages\manifest.json'
[IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 16) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

$baselineDirectory = Join-Path $benchmarkStateDirectory 'baseline'
[IO.Directory]::CreateDirectory($baselineDirectory) | Out-Null
Copy-Item -LiteralPath $scenePath -Destination (Join-Path $baselineDirectory 'SampleScene.unity')
$sceneMetaPath = $scenePath + '.meta'
if (Test-Path -LiteralPath $sceneMetaPath -PathType Leaf) {
    Copy-Item -LiteralPath $sceneMetaPath -Destination (Join-Path $baselineDirectory 'SampleScene.unity.meta')
}

$repositoryHead = (& git -C $repository rev-parse HEAD).Trim()
$marker = [ordered]@{
    schema = 'hera.ui-authoring-ab-fixture/1'
    fixture_profile = 'minimal-ugui'
    created_at = [DateTimeOffset]::UtcNow.ToString('o')
    unity_version = $unityVersion
    template_project = $template
    repository_root = $repository
    repository_head = $repositoryHead
    connector_version = [string]$connectorPackage.version
    connector_snapshot = '.hera-ab/Connector'
    baseline_scene = 'Assets/Scenes/SampleScene.unity'
    reset_scene = 'Assets/Scenes/__HeraABReset.unity'
    manifest_dependency_count = $dependencies.Count
}
$markerPath = Join-Path $destinationFull '.hera-ui-ab-fixture.json'
[IO.File]::WriteAllText(
    $markerPath,
    ($marker | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

$agentRules = @'
# Hera UI Authoring A/B Fixture

This is a disposable benchmark project.

- Use only the `hera-agent-unity` command resolved from PATH. Do not search for or invoke another Hera binary.
- Do not inspect the parent Hera repository, benchmark harness, scoring oracles, previous run results, or the fixture's `.hera-ab/` benchmark-internal directory.
- Do not edit `.unity`, `.prefab`, `.asset`, `.meta`, or C# files directly.
- Do not create scripts or code generators for the UI task.
- Perform UI mutations only through the Hera authoring commands allowed by the active benchmark arm.
- `exec` and `html-to-uidoc` are intentionally forbidden by the benchmark shim.
- You may use normal read/verification commands and the common overlay capture capability exposed by the shim.
- Verify the visible UI and final Console state before reporting completion.
'@
[IO.File]::WriteAllText(
    (Join-Path $destinationFull 'AGENTS.md'),
    $agentRules.TrimStart() + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

Write-Output ([pscustomobject]@{
    schema = 'hera.ui-authoring-ab-fixture-created/1'
    fixture_profile = 'minimal-ugui'
    path = $destinationFull
    unity_version = $unityVersion
    connector_version = [string]$connectorPackage.version
    connector_snapshot = $connectorSnapshot
    repository_head = $repositoryHead
    manifest = $manifestPath
    manifest_dependency_count = $dependencies.Count
    marker = $markerPath
    baseline_scene_sha256 = (Get-FileHash (Join-Path $baselineDirectory 'SampleScene.unity') -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Compress)
