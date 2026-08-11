param(
    [string]$TemplateProject = 'C:\Users\PC\Desktop\Cowork\test6000.3.5f2',
    [string]$Wave = '',
    [string]$ResultRoot,
    [string]$Model = 'gpt-5.6-sol',
    [string]$ReasoningEffort = 'medium',
    [int]$Repetitions = 3,
    [int]$MaxAttemptsPerCell = 3,
    [int]$CodexTimeoutMinutes = 15,
    [ValidateSet('formal','fast')][string]$Protocol = 'formal',
    [switch]$PlanOnly,
    [switch]$KeepFixture
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bench = Join-Path $repository 'docs\benchmarks\ui-doc-ab'
$manifest = Get-Content -LiteralPath (Join-Path $bench 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'hera.ui-authoring-ab-manifest/1') { throw 'Unexpected benchmark manifest schema.' }
if ($manifest.unity_version -ne '6000.3.5f2') { throw 'Screening manifest Unity version drifted.' }
if ($manifest.ui_system -ne 'ugui') { throw 'Screening manifest UI system drifted.' }

$fast = $Protocol -eq 'fast'
if ($fast) {
    $fastConfig = $manifest.fast
    if ($null -eq $fastConfig) { throw 'Fast protocol is missing from the benchmark manifest.' }
    if (@($fastConfig.arms).Count -ne 2 -or (@($fastConfig.arms) -join ',') -ne 'uidoc,primitives_batch') { throw 'Fast manifest arms drifted.' }
    if ([int]$fastConfig.repetitions -ne 2 -or [int]$fastConfig.codex_timeout_minutes -ne 4 -or [int]$fastConfig.max_attempts_per_cell -ne 1) { throw 'Fast manifest cell budget drifted.' }
    if ([int]$fastConfig.admission_cutoff_minutes -ne 53 -or [int]$fastConfig.wave_deadline_minutes -ne 60) { throw 'Fast manifest wave limit drifted.' }
    if (@($fastConfig.order).Count -ne 2 -or (@($fastConfig.order[0]) -join ',') -ne 'uidoc,primitives_batch' -or (@($fastConfig.order[1]) -join ',') -ne 'primitives_batch,uidoc') { throw 'Fast manifest order drifted.' }
    if ($PSBoundParameters.ContainsKey('Repetitions') -and $Repetitions -ne [int]$fastConfig.repetitions) { throw "Fast repetitions must match the manifest: $($fastConfig.repetitions)." }
    if ($PSBoundParameters.ContainsKey('MaxAttemptsPerCell') -and $MaxAttemptsPerCell -ne [int]$fastConfig.max_attempts_per_cell) { throw "Fast attempts must match the manifest: $($fastConfig.max_attempts_per_cell)." }
    if ($PSBoundParameters.ContainsKey('CodexTimeoutMinutes') -and $CodexTimeoutMinutes -ne [int]$fastConfig.codex_timeout_minutes) { throw "Fast Codex timeout must match the manifest: $($fastConfig.codex_timeout_minutes) minute(s)." }
    $Repetitions = [int]$fastConfig.repetitions
    $MaxAttemptsPerCell = [int]$fastConfig.max_attempts_per_cell
    $CodexTimeoutMinutes = [int]$fastConfig.codex_timeout_minutes
    $armIds = @($fastConfig.arms)
    $armOrders = [System.Collections.Generic.List[object]]::new()
    foreach ($order in @($fastConfig.order)) { $armOrders.Add(@($order)) }
    $admissionCutoffMinutes = [int]$fastConfig.admission_cutoff_minutes
    $waveDeadlineMinutes = [int]$fastConfig.wave_deadline_minutes
}
else {
    if ($CodexTimeoutMinutes -ne [int]$manifest.codex_timeout_minutes) { throw "Codex timeout must match frozen manifest: $($manifest.codex_timeout_minutes) minute(s)." }
    if ($Repetitions -lt 1 -or $Repetitions -gt [int]$manifest.confirmation_repetitions) { throw "Repetitions must be 1..$($manifest.confirmation_repetitions)" }
    $armIds = @($manifest.arms | ForEach-Object { [string]$_.id })
    $armOrders = [System.Collections.Generic.List[object]]::new()
    foreach ($order in @($manifest.screening_order)) { $armOrders.Add(@($order)) }
    $admissionCutoffMinutes = $null
    $waveDeadlineMinutes = $null
}

$expectedCells = [int]$manifest.tasks.Count * $armIds.Count * $Repetitions
if ([string]::IsNullOrWhiteSpace($Wave)) {
    $prefix = if ($fast) { 'fast' } else { 'screening' }
    $Wave = $prefix + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
}

if ($PlanOnly) {
    $cells = New-Object System.Collections.Generic.List[object]
    for ($rep = 1; $rep -le $Repetitions; $rep++) {
        $orderIndex = ($rep - 1) % $armOrders.Count
        foreach ($task in @($manifest.tasks)) {
            foreach ($arm in @($armOrders[$orderIndex])) {
                $cells.Add([pscustomobject]@{ task = [string]$task.id; arm = [string]$arm; repetition = $rep })
            }
        }
    }
    $plan = [ordered]@{
        schema = 'hera.ui-authoring-ab-wave-plan/1'
        protocol = $Protocol
        expected_cells = $expectedCells
        arms = $armIds
        repetitions = $Repetitions
        codex_timeout_minutes = $CodexTimeoutMinutes
        max_attempts_per_cell = $MaxAttemptsPerCell
        arm_order = $armOrders
        admission_cutoff_minutes = $admissionCutoffMinutes
        wave_deadline_minutes = $waveDeadlineMinutes
        cells = $cells
    }
    Write-Output (($plan | ConvertTo-Json -Compress -Depth 8))
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ResultRoot)) {
    $ResultRoot = Join-Path $bench 'results'
}
$resultRootFull = [IO.Path]::GetFullPath($ResultRoot)
$waveDirectory = Join-Path $resultRootFull $Wave
if (Test-Path -LiteralPath $waveDirectory) {
    throw "Wave directory already exists; choose a new -Wave: $waveDirectory"
}
[IO.Directory]::CreateDirectory($waveDirectory) | Out-Null

# Baseline measurement must happen before production changes. Docs and benchmark
# harness files may be dirty while the workstream itself is under construction.
$changed = @(& git -C $repository status --porcelain | ForEach-Object { $_.Substring(3).Trim() })
$unexpected = @($changed | Where-Object {
    $_ -and
    $_ -notlike 'docs/handoffs/*' -and
    $_ -notlike 'docs\handoffs\*' -and
    $_ -notlike 'docs/benchmarks/ui-doc-ab/*' -and
    $_ -notlike 'docs\benchmarks\ui-doc-ab\*' -and
    $_ -notlike 'docs/superpowers/plans/*' -and
    $_ -notlike 'docs\superpowers\plans\*' -and
    $_ -notlike 'tools/benchmark-ui-authoring/*' -and
    $_ -notlike 'tools\benchmark-ui-authoring\*'
})
if ($unexpected.Count -gt 0) {
    throw "Refusing baseline screening with unrelated/production changes: $($unexpected -join ', ')"
}

$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('hera-ui-ab-wave-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $workRoot 'project'
$binDirectory = Join-Path $workRoot 'bin'
$cli = Join-Path $binDirectory 'hera-agent-unity.exe'
[IO.Directory]::CreateDirectory($binDirectory) | Out-Null

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int]$TimeoutSeconds = 0
    )
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    foreach ($argument in $Arguments) { [void]$psi.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [void]$process.Start()
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    $timedOut = $false
    if ($TimeoutSeconds -gt 0) {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            try { $process.Kill($true) } catch { }
            $process.WaitForExit()
        }
    }
    else { $process.WaitForExit() }
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    $watch.Stop()
    return [pscustomobject]@{
        exit_code = if($timedOut){124}else{$process.ExitCode}
        stdout = $stdout
        stderr = $stderr
        timed_out = $timedOut
        wall_ms = [Math]::Round($watch.Elapsed.TotalMilliseconds,3)
    }
}

function Find-FixtureUnity {
    $matches = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $line = [string]$_.CommandLine
        -not [string]::IsNullOrWhiteSpace($line) -and
        $line.IndexOf($fixture,[StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $line.IndexOf('-batchMode',[StringComparison]::OrdinalIgnoreCase) -lt 0 -and
        $line.IndexOf('AssetImportWorker',[StringComparison]::OrdinalIgnoreCase) -lt 0
    })
    if ($matches.Count -gt 1) { throw "Multiple benchmark Unity processes: $($matches.ProcessId -join ', ')" }
    return $matches | Select-Object -First 1
}

function Close-FixtureUnity {
    $info = Find-FixtureUnity
    if ($null -eq $info) { return $true }
    $process = [Diagnostics.Process]::GetProcessById([int]$info.ProcessId)
    [void]$process.CloseMainWindow()
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if ($null -eq (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { return $true }
    }
    return $false
}

function Assert-NoRecoveryBackups {
    $backup = Join-Path $fixture 'Temp\__Backupscenes'
    $count = if(Test-Path -LiteralPath $backup){@(Get-ChildItem -LiteralPath $backup -File -Recurse -ErrorAction SilentlyContinue).Count}else{0}
    if ($count -ne 0) { throw "Benchmark fixture contains $count Scene Recovery backup file(s)." }
}

$waveStartedAt = [DateTimeOffset]::UtcNow
$admissionDeadline = if ($fast) { $waveStartedAt.AddMinutes($admissionCutoffMinutes) } else { $null }
$hardDeadline = if ($fast) { $waveStartedAt.AddMinutes($waveDeadlineMinutes) } else { $null }
$waveJson = Join-Path $waveDirectory 'wave.json'

$assetConfigPath = Join-Path $HOME '.hera-agent-unity\asset-config.json'
$assetConfigSha256 = if (Test-Path -LiteralPath $assetConfigPath -PathType Leaf) {
    (Get-FileHash -LiteralPath $assetConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
} else { 'absent' }
if ($assetConfigSha256 -ne 'absent') {
    $assetConfig = Get-Content -LiteralPath $assetConfigPath -Raw | ConvertFrom-Json
    if ([string]$assetConfig.ui_system -ne 'ugui') {
        throw "Screening is frozen to ui_system=ugui, but current user asset-config reports '$($assetConfig.ui_system)'."
    }
}

$waveMeta = [ordered]@{
    schema = 'hera.ui-authoring-ab-wave/1'
    wave = $Wave
    protocol = $Protocol
    created_at = $waveStartedAt.ToString('o')
    repo_commit = (& git -C $repository rev-parse HEAD).Trim()
    model = $Model
    reasoning_effort = $ReasoningEffort
    asset_config_sha256 = $assetConfigSha256
    repetitions = $Repetitions
    max_attempts_per_cell = $MaxAttemptsPerCell
    expected_cells = $expectedCells
    arms = $armIds
    arm_order = $armOrders
    codex_timeout_minutes = $CodexTimeoutMinutes
    admission_cutoff_minutes = $admissionCutoffMinutes
    wave_deadline_minutes = $waveDeadlineMinutes
    admission_deadline_at = if ($null -ne $admissionDeadline) { $admissionDeadline.ToString('o') } else { $null }
    hard_deadline_at = if ($null -ne $hardDeadline) { $hardDeadline.ToString('o') } else { $null }
    template_project = (Resolve-Path -LiteralPath $TemplateProject).Path
    fixture_path = $fixture
    source_cli = $cli
    status = 'initializing'
}

function Write-WaveMeta {
    [IO.File]::WriteAllText($waveJson,($waveMeta|ConvertTo-Json -Depth 12)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
}

$waveSucceeded = $false
$fastDeadlineExceeded = $false
Write-WaveMeta
try {
    Write-Host '=== build source CLI ==='
    $build = Invoke-External -FileName 'go' -Arguments @('build','-o',$cli,'.') -TimeoutSeconds 180
    if ($build.exit_code -ne 0) { throw "go build failed: $($build.stderr)" }
    $waveMeta.cli_sha256 = (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash.ToLowerInvariant()
    $waveMeta.cli_bytes = (Get-Item -LiteralPath $cli).Length

    Write-Host '=== create disposable fixture ==='
    $create = Invoke-External -FileName 'pwsh' -Arguments @(
        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'New-Fixture.ps1'),
        '-TemplateProject',$TemplateProject,'-Destination',$fixture) -TimeoutSeconds 120
    if ($create.exit_code -ne 0) { throw "New-Fixture failed: $($create.stderr)" }
    $created = $create.stdout.Trim() | ConvertFrom-Json
    $waveMeta.fixture_profile = [string]$created.fixture_profile
    $waveMeta.manifest_dependency_count = [int]$created.manifest_dependency_count
    $waveMeta.connector_version = [string]$created.connector_version
    $waveMeta.baseline_scene_sha256 = [string]$created.baseline_scene_sha256

    # Launch exactly once. Every measured cell gets a fresh Codex session and a
    # hash-verified live Scene reset, but shares this warm Editor process. This
    # removes package/import/restart variance from the authoring comparison.
    Write-Host '=== cold fixture warmup ==='
    $warm = Invoke-External -FileName $cli -Arguments @(
        '--project',$fixture,'--timeout','240000','editor','launch') -TimeoutSeconds 300
    if ($warm.exit_code -ne 0) { throw "fixture warmup launch failed: $($warm.stderr)" }
    $warmInfo = Find-FixtureUnity
    if ($null -eq $warmInfo) { throw 'Warmup launch returned without exact fixture Unity PID.' }
    $waveEditorPid = [int]$warmInfo.ProcessId
    $waveMeta.warmup_unity_pid = $waveEditorPid
    $waveMeta.warmup_wall_ms = [double]$warm.wall_ms
    $waveMeta.editor_reused_across_cells = $true

    $initialReset = Invoke-External -FileName 'pwsh' -Arguments @(
        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Reset-LiveFixture.ps1'),
        '-ProjectPath',$fixture,'-HeraCli',$cli) -TimeoutSeconds 120
    if ($initialReset.exit_code -ne 0) { throw "initial live reset failed: $($initialReset.stderr)`n$($initialReset.stdout)" }
    Assert-NoRecoveryBackups

    $waveMeta.status = 'running'
    Write-WaveMeta

    for ($rep = 1; $rep -le $Repetitions; $rep++) {
        $orderIndex = ($rep - 1) % $armOrders.Count
        $armOrder = @($armOrders[$orderIndex])
        foreach ($task in @($manifest.tasks)) {
            foreach ($arm in $armOrder) {
                if ($fast -and [DateTimeOffset]::UtcNow -ge $admissionDeadline) {
                    $fastDeadlineExceeded = $true
                    throw "Fast wave reached its $admissionCutoffMinutes-minute admission cutoff before $($task.id)/$arm/rep-$rep."
                }
                $cellSucceeded = $false
                for ($attempt = 1; $attempt -le $MaxAttemptsPerCell; $attempt++) {
                    $cell = Join-Path $waveDirectory (Join-Path $task.id (Join-Path $arm (Join-Path ("rep-{0:D2}" -f $rep) ("attempt-{0:D2}" -f $attempt))))
                    [IO.Directory]::CreateDirectory($cell) | Out-Null
                    Write-Host ("=== {0} {1} rep {2}/{3} attempt {4} ===" -f $task.id,$arm,$rep,$Repetitions,$attempt)
                    $runTimeoutSeconds = ($CodexTimeoutMinutes + 8) * 60
                    if ($fast) {
                        $remainingSeconds = [int][Math]::Floor(($hardDeadline - [DateTimeOffset]::UtcNow).TotalSeconds)
                        if ($remainingSeconds -le 0) {
                            $fastDeadlineExceeded = $true
                            throw "Fast wave reached its $waveDeadlineMinutes-minute hard deadline before $($task.id)/$arm/rep-$rep."
                        }
                        $runTimeoutSeconds = [Math]::Min($runTimeoutSeconds,$remainingSeconds)
                    }
                    $run = Invoke-External -FileName 'pwsh' -Arguments @(
                        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Run-One.ps1'),
                        '-Task',$task.id,'-Arm',$arm,'-Repetition',[string]$rep,
                        '-FixturePath',$fixture,'-HeraCli',$cli,'-ResultDirectory',$cell,
                        '-ExpectedAssetConfigSha256',$assetConfigSha256,
                        '-Model',$Model,'-ReasoningEffort',$ReasoningEffort,
                        '-CodexTimeoutMinutes',[string]$CodexTimeoutMinutes,
                        '-ReuseRunningEditor') -TimeoutSeconds $runTimeoutSeconds

                    if ($fast -and $run.timed_out -and [DateTimeOffset]::UtcNow -ge $hardDeadline) {
                        $fastDeadlineExceeded = $true
                        throw "Fast wave reached its $waveDeadlineMinutes-minute hard deadline while scoring $($task.id)/$arm/rep-$rep."
                    }

                    $runPath = Join-Path $cell 'run.json'
                    $valid = $false
                    if (Test-Path -LiteralPath $runPath -PathType Leaf) {
                        try {
                            $runRecord = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
                            if ($fast) {
                                $requiredArtifacts = @('run.json','agent-events.jsonl','agent-stderr.txt','hera-calls.jsonl','score.json','final-capture.png','final-annotations.json','console-errors.json')
                                $missingArtifacts = @($requiredArtifacts | Where-Object { -not (Test-Path -LiteralPath (Join-Path $cell $_) -PathType Leaf) })
                                if ($missingArtifacts.Count -gt 0) { throw "Fast run is missing required artifact(s): $($missingArtifacts -join ', ')" }
                                if ([int]$runRecord.hera_calls -eq 0 -and [double]$runRecord.agent_wall_ms -lt 1000) { throw 'Fast run had a zero-call process-start failure.' }
                            }
                            $valid = [bool]$runRecord.benchmark_valid -and $run.exit_code -eq 0
                            Write-Host ("result: valid={0} score={1} strict={2} agent_ms={3} calls={4}" -f $valid,$runRecord.score,$runRecord.strict_pass,$runRecord.agent_wall_ms,$runRecord.hera_calls)
                        }
                        catch { Write-Warning "Could not parse run.json: $($_.Exception.Message)" }
                    }
                    else {
                        Write-Warning ("Run-One exit {0} produced no run.json. stderr: {1}" -f $run.exit_code,$run.stderr.Trim())
                    }

                    Assert-NoRecoveryBackups
                    $afterCellUnity = Find-FixtureUnity
                    if ($null -eq $afterCellUnity -or [int]$afterCellUnity.ProcessId -ne $waveEditorPid) {
                        throw 'Shared benchmark Unity process changed or disappeared during screening.'
                    }

                    if ($valid) {
                        $cellSucceeded = $true
                        break
                    }
                }
                if (-not $cellSucceeded) {
                    throw "No valid run after $MaxAttemptsPerCell attempt(s): task=$($task.id) arm=$arm rep=$rep"
                }
            }
        }
    }

    $waveMeta.status = if ($fast) { 'fast_complete' } else { 'screening_complete' }
    $waveMeta.finished_at = [DateTimeOffset]::UtcNow.ToString('o')
    $waveMeta.wave_wall_ms = [Math]::Round(([DateTimeOffset]::UtcNow - $waveStartedAt).TotalMilliseconds,3)
    Write-WaveMeta
    $waveSucceeded = $true
    Write-Host "SCREENING_PASS $waveDirectory"
}
catch {
    if ($fast) {
        $waveMeta.status = if ($fastDeadlineExceeded) { 'incomplete' } else { 'invalid' }
        $waveMeta.terminal_reason = $_.Exception.Message
        $waveMeta.finished_at = [DateTimeOffset]::UtcNow.ToString('o')
        $waveMeta.wave_wall_ms = [Math]::Round(([DateTimeOffset]::UtcNow - $waveStartedAt).TotalMilliseconds,3)
        Write-WaveMeta
        $terminalName = if ($waveMeta.status -eq 'incomplete') { 'INCOMPLETE.md' } else { 'INVALID.md' }
        [IO.File]::WriteAllText((Join-Path $waveDirectory $terminalName),(('# ' + $waveMeta.status) + [Environment]::NewLine + [Environment]::NewLine + $waveMeta.terminal_reason + [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
    }
    throw
}
finally {
    try {
        if ($null -ne (Find-FixtureUnity)) {
            if (-not (Close-FixtureUnity)) { Write-Warning "Fixture Unity could not be closed gracefully: $fixture" }
        }
    }
    catch { Write-Warning $_.Exception.Message }

    $canRemove = $false
    if (Test-Path -LiteralPath (Join-Path $fixture '.hera-ui-ab-fixture.json') -PathType Leaf) {
        try {
            Assert-NoRecoveryBackups
            $canRemove = $null -eq (Find-FixtureUnity)
            if (-not $canRemove) { Write-Warning 'Fixture cleanup skipped because benchmark Unity is still running.' }
        }
        catch { Write-Warning $_.Exception.Message }
    }
    if ($canRemove -and $waveSucceeded -and -not $KeepFixture) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    elseif (Test-Path -LiteralPath $workRoot) {
        Write-Warning "Benchmark work directory retained for inspection: $workRoot"
    }
}

if (-not $waveSucceeded) { exit 1 }
exit 0
