param(
    [Parameter(Mandatory = $true)][string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$markerPath = Join-Path $project '.hera-ui-ab-fixture.json'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw "Refusing to reset unmarked project: $project"
}

$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($marker.schema -ne 'hera.ui-authoring-ab-fixture/1') {
    throw "Unexpected benchmark fixture marker schema: $($marker.schema)"
}
if ($marker.unity_version -ne '6000.3.5f2') {
    throw "Benchmark fixture Unity version drifted: $($marker.unity_version)"
}

$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object {
        $line = [string]$_.CommandLine
        -not [string]::IsNullOrWhiteSpace($line) -and
        $line.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
if ($running) {
    $pids = ($running | Select-Object -ExpandProperty ProcessId) -join ', '
    throw "Refusing to reset while this benchmark project is open in Unity. PID(s): $pids"
}

$backupDirectory = Join-Path $project 'Temp\__Backupscenes'
if (Test-Path -LiteralPath $backupDirectory -PathType Container) {
    $backupFiles = @(Get-ChildItem -LiteralPath $backupDirectory -File -Recurse -ErrorAction SilentlyContinue)
    if ($backupFiles.Count -gt 0) {
        throw "Scene Recovery backup detected; refusing destructive reset: $($backupFiles[0].FullName)"
    }
}

$baselineDirectory = Join-Path $project '.hera-ab\baseline'
$baselineScene = Join-Path $baselineDirectory 'SampleScene.unity'
$baselineMeta = Join-Path $baselineDirectory 'SampleScene.unity.meta'
if (-not (Test-Path -LiteralPath $baselineScene -PathType Leaf)) {
    throw "Baseline scene is missing: $baselineScene"
}

$scene = Join-Path $project 'Assets\Scenes\SampleScene.unity'
$sceneMeta = $scene + '.meta'
Copy-Item -LiteralPath $baselineScene -Destination $scene -Force
if (Test-Path -LiteralPath $baselineMeta -PathType Leaf) {
    Copy-Item -LiteralPath $baselineMeta -Destination $sceneMeta -Force
}

# These paths are benchmark-owned outputs only. They are removed only inside a
# marker-verified disposable fixture. User projects are never accepted here.
$ownedAssetPaths = @(
    'Assets\HeraGenerated',
    'Assets\HeraImported',
    'Assets\HeraBenchmark'
)
$removed = @()
foreach ($relative in $ownedAssetPaths) {
    $path = Join-Path $project $relative
    $meta = $path + '.meta'
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
        $removed += $relative.Replace('\', '/')
    }
    if (Test-Path -LiteralPath $meta -PathType Leaf) {
        Remove-Item -LiteralPath $meta -Force
        $removed += ($relative + '.meta').Replace('\', '/')
    }
}

$sceneSha = (Get-FileHash -LiteralPath $scene -Algorithm SHA256).Hash.ToLowerInvariant()
$baselineSha = (Get-FileHash -LiteralPath $baselineScene -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sceneSha -ne $baselineSha) {
    throw 'Scene reset verification failed: restored scene hash differs from baseline.'
}

$result = [ordered]@{
    schema = 'hera.ui-authoring-ab-fixture-reset/1'
    path = $project
    scene = 'Assets/Scenes/SampleScene.unity'
    scene_sha256 = $sceneSha
    removed_owned_paths = $removed
    recovery_backup_files = 0
}
Write-Output (($result | ConvertTo-Json -Depth 8) -replace "`r?`n", '')
