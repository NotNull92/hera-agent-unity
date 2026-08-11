param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$HeraCli
)

$ErrorActionPreference = 'Stop'

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$cli = (Resolve-Path -LiteralPath $HeraCli).Path
$markerPath = Join-Path $project '.hera-ui-ab-fixture.json'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw "Refusing live reset of unmarked project: $project"
}
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($marker.schema -ne 'hera.ui-authoring-ab-fixture/1' -or $marker.fixture_profile -ne 'minimal-ugui') {
    throw "Live reset requires a minimal-ugui benchmark fixture."
}
if ($marker.unity_version -ne '6000.3.5f2') {
    throw "Benchmark fixture Unity version drifted: $($marker.unity_version)"
}

function Get-MainUnityProcess {
    $matches = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $line = [string]$_.CommandLine
        -not [string]::IsNullOrWhiteSpace($line) -and
        $line.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $line.IndexOf('-batchMode', [StringComparison]::OrdinalIgnoreCase) -lt 0 -and
        $line.IndexOf('AssetImportWorker', [StringComparison]::OrdinalIgnoreCase) -lt 0
    })
    if ($matches.Count -ne 1) {
        throw "Live reset requires exactly one benchmark Unity main process; found $($matches.Count)."
    }
    return $matches[0]
}

[void](Get-MainUnityProcess)

$backupDirectory = Join-Path $project 'Temp\__Backupscenes'
if (Test-Path -LiteralPath $backupDirectory -PathType Container) {
    $backupFiles = @(Get-ChildItem -LiteralPath $backupDirectory -File -Recurse -ErrorAction SilentlyContinue)
    if ($backupFiles.Count -gt 0) {
        throw "Scene Recovery backup detected before live reset: $($backupFiles[0].FullName)"
    }
}

function Invoke-Raw {
    param([string[]]$Arguments)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $cli
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.Environment['HERA_AGENT_NO_PATH_CHECK'] = '1'
    foreach ($argument in (@('--project',$project,'--timeout','60000','--compact-json') + $Arguments)) {
        [void]$psi.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Stdout = $stdoutTask.GetAwaiter().GetResult()
        Stderr = $stderrTask.GetAwaiter().GetResult()
    }
}

function Parse-LastEnvelope([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    $lines = $Text -split "`r?`n"
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        $line = $lines[$i].Trim()
        if (-not $line.StartsWith('{')) { continue }
        try { return ($line | ConvertFrom-Json) } catch { }
    }
    return $null
}

function Invoke-Hera {
    param([string[]]$Arguments, [switch]$ApprovalAllowed)
    $first = Invoke-Raw $Arguments
    if ($first.ExitCode -eq 0) { return $first }
    if (-not $ApprovalAllowed) {
        throw "Hera live-reset command failed: $($Arguments -join ' ')`n$($first.Stderr)"
    }
    $envelope = Parse-LastEnvelope $first.Stderr
    if ($null -eq $envelope -or [string]$envelope.code -ne 'APPROVAL_REQUIRED' -or [string]::IsNullOrWhiteSpace([string]$envelope.data.token)) {
        throw "Expected approval preflight for live-reset command: $($Arguments -join ' ')`n$($first.Stderr)"
    }
    $second = Invoke-Raw ($Arguments + @('--approve',[string]$envelope.data.token))
    if ($second.ExitCode -ne 0) {
        throw "Approved live-reset command failed: $($Arguments -join ' ')`n$($second.Stderr)"
    }
    return $second
}

# Leave the task Scene before touching its backing file. The active Scene is
# always clean because Run-One saves it out-of-band after the agent exits.
[void](Invoke-Hera @('scene','load',[string]$marker.reset_scene))

$baselineDirectory = Join-Path $project '.hera-ab\baseline'
$baselineScene = Join-Path $baselineDirectory 'SampleScene.unity'
$baselineMeta = Join-Path $baselineDirectory 'SampleScene.unity.meta'
$scene = Join-Path $project 'Assets\Scenes\SampleScene.unity'
$sceneMeta = $scene + '.meta'
if (-not (Test-Path -LiteralPath $baselineScene -PathType Leaf)) {
    throw "Baseline scene is missing: $baselineScene"
}
Copy-Item -LiteralPath $baselineScene -Destination $scene -Force
if (Test-Path -LiteralPath $baselineMeta -PathType Leaf) {
    Copy-Item -LiteralPath $baselineMeta -Destination $sceneMeta -Force
}

# Remove only benchmark-owned/generated Asset outputs.
$removed = New-Object System.Collections.Generic.List[string]
foreach ($relative in @('Assets\HeraGenerated','Assets\HeraImported','Assets\HeraBenchmark')) {
    foreach ($candidate in @($relative, $relative + '.meta')) {
        $path = Join-Path $project $candidate
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            $removed.Add($candidate.Replace('\','/'))
        }
    }
}

# Fresh Codex sessions must not see JSON plans, captures, scripts, or scratch
# directories created by an earlier arm. Keep only project/runtime infrastructure
# and normal Unity-generated solution files at the project root.
$allowedRootNames = @(
    'Assets','Packages','ProjectSettings','Library','Temp','Logs','UserSettings','obj',
    '.hera-ab','.hera-ui-ab-fixture.json','AGENTS.md'
)
foreach ($entry in @(Get-ChildItem -LiteralPath $project -Force)) {
    if ($allowedRootNames -contains $entry.Name) { continue }
    if (-not $entry.PSIsContainer -and $entry.Name -match '\.(csproj|sln|slnx)$') { continue }
    Remove-Item -LiteralPath $entry.FullName -Recurse -Force
    $removed.Add($entry.Name)
}

$sceneSha = (Get-FileHash -LiteralPath $scene -Algorithm SHA256).Hash.ToLowerInvariant()
$baselineSha = (Get-FileHash -LiteralPath $baselineScene -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sceneSha -ne $baselineSha) {
    throw 'Live scene reset verification failed: SampleScene differs from baseline.'
}

# Refresh while parked in ResetScene, then reopen the restored task Scene.
[void](Invoke-Hera @('editor','refresh'))
[void](Invoke-Hera @('scene','load',[string]$marker.baseline_scene))
$infoRun = Invoke-Hera @('scene','info')
$info = Parse-LastEnvelope $infoRun.Stdout
if ($null -eq $info) {
    # scene info prints its data object directly rather than an envelope.
    try { $info = $infoRun.Stdout.Trim() | ConvertFrom-Json } catch { }
}
if ($null -eq $info -or [string]$info.active.path -ne [string]$marker.baseline_scene) {
    throw "Live reset did not reopen baseline Scene: $($infoRun.Stdout)"
}
if (@($info.loaded).Count -ne 1 -or [int]$info.loaded[0].rootCount -ne 0) {
    throw "Baseline Scene is not empty after live reset: $($infoRun.Stdout)"
}

# Console is experiment state too. Clear it out-of-band so one arm cannot poison
# the next arm's cleanliness score. This destructive action is approval-bound.
[void](Invoke-Hera @('console','--clear') -ApprovalAllowed)

$result = [ordered]@{
    schema = 'hera.ui-authoring-ab-live-reset/1'
    project = $project
    unity_pid = [int](Get-MainUnityProcess).ProcessId
    active_scene = [string]$marker.baseline_scene
    scene_sha256 = $sceneSha
    root_count = 0
    removed_paths = @($removed)
    recovery_backup_files = 0
}
Write-Output (($result | ConvertTo-Json -Compress -Depth 8))
