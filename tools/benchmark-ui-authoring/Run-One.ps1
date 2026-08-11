param(
    [Parameter(Mandatory = $true)][ValidateSet('T01','T02','T03')][string]$Task,
    [Parameter(Mandatory = $true)][ValidateSet('uidoc','primitives','primitives_batch')][string]$Arm,
    [Parameter(Mandatory = $true)][int]$Repetition,
    [Parameter(Mandatory = $true)][string]$FixturePath,
    [Parameter(Mandatory = $true)][string]$HeraCli,
    [Parameter(Mandatory = $true)][string]$ResultDirectory,
    [string]$ExpectedAssetConfigSha256 = '',
    [string]$Model = 'gpt-5.6-sol',
    [string]$ReasoningEffort = 'medium',
    [int]$CodexTimeoutMinutes = 15,
    [switch]$ReuseRunningEditor
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$fixture = (Resolve-Path -LiteralPath $FixturePath).Path
$cli = (Resolve-Path -LiteralPath $HeraCli).Path
$result = [IO.Path]::GetFullPath($ResultDirectory)
[IO.Directory]::CreateDirectory($result) | Out-Null

$markerPath = Join-Path $fixture '.hera-ui-ab-fixture.json'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { throw "Unmarked benchmark fixture: $fixture" }
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($marker.schema -ne 'hera.ui-authoring-ab-fixture/1') { throw "Unexpected fixture marker schema: $($marker.schema)" }
if ($marker.unity_version -ne '6000.3.5f2') { throw "Unexpected fixture Unity version: $($marker.unity_version)" }

$bench = Join-Path $repository 'docs\benchmarks\ui-doc-ab'
$manifest = Get-Content -LiteralPath (Join-Path $bench 'manifest.json') -Raw | ConvertFrom-Json
$taskRecord = @($manifest.tasks | Where-Object { $_.id -eq $Task }) | Select-Object -First 1
if ($null -eq $taskRecord) { throw "Task is not in benchmark manifest: $Task" }
$promptPath = Join-Path $bench $taskRecord.prompt
$oraclePath = Join-Path $bench $taskRecord.oracle
$prompt = Get-Content -LiteralPath $promptPath -Raw

$shimDirectory = (Resolve-Path (Join-Path $PSScriptRoot 'shim')).Path
$shimLog = Join-Path $result 'hera-calls.jsonl'
$agentEvents = Join-Path $result 'agent-events.jsonl'
$agentStderr = Join-Path $result 'agent-stderr.txt'
$lastMessage = Join-Path $result 'last-message.txt'
foreach ($path in @($shimLog,$agentEvents,$agentStderr,$lastMessage)) {
    if (Test-Path -LiteralPath $path) { throw "Result file already exists; refusing to overwrite: $path" }
}

# The benchmark never changes user-global asset-config.json. All arms inherit the
# same real settings. Freeze only its SHA and required uGUI backend so a concurrent
# settings change cannot silently contaminate one arm.
$assetConfigPath = Join-Path $HOME '.hera-agent-unity\asset-config.json'
$assetConfigSha256 = if (Test-Path -LiteralPath $assetConfigPath -PathType Leaf) {
    (Get-FileHash -LiteralPath $assetConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
} else { 'absent' }
if (-not [string]::IsNullOrWhiteSpace($ExpectedAssetConfigSha256) -and $assetConfigSha256 -ne $ExpectedAssetConfigSha256) {
    throw "User asset-config changed during benchmark wave. expected=$ExpectedAssetConfigSha256 actual=$assetConfigSha256"
}
if ($assetConfigSha256 -ne 'absent') {
    $assetConfig = Get-Content -LiteralPath $assetConfigPath -Raw | ConvertFrom-Json
    if ([string]$assetConfig.ui_system -ne 'ugui') {
        throw "M0-M5 benchmark is frozen to ui_system=ugui, but current user asset-config reports '$($assetConfig.ui_system)'."
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [hashtable]$Environment = @{},
        [string]$StandardInput,
        [int]$TimeoutSeconds = 0
    )
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $null -ne $StandardInput
    if ($psi.RedirectStandardInput) { $psi.StandardInputEncoding = [Text.UTF8Encoding]::new($false) }
    $psi.CreateNoWindow = $true
    foreach ($argument in $Arguments) { [void]$psi.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $psi.Environment[$key] = [string]$Environment[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [void]$process.Start()
    if ($null -ne $StandardInput) {
        $process.StandardInput.Write($StandardInput)
        $process.StandardInput.Close()
    }
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
        wall_ms = [Math]::Round($watch.Elapsed.TotalMilliseconds,3)
        timed_out = $timedOut
    }
}

function Invoke-HeraSetup([string[]]$Arguments) {
    # Cold first import of the disposable fixture can spend >100s in UPM before
    # the Connector heartbeat exists. The source CLI must wait for the same PID,
    # never blindly relaunch after that expected cold-start window.
    $full = @('--project',$fixture,'--timeout','240000','--compact-json') + $Arguments
    $run = Invoke-External -FileName $cli -Arguments $full -Environment @{ HERA_AGENT_NO_PATH_CHECK = '1' }
    if ($run.exit_code -ne 0) {
        throw "Hera setup command failed: $($Arguments -join ' ')`n$($run.stderr)"
    }
    return $run
}

function Find-BenchmarkUnityProcess {
    $matches = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $line = [string]$_.CommandLine
        -not [string]::IsNullOrWhiteSpace($line) -and
        $line.IndexOf($fixture,[StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $line.IndexOf('-batchMode',[StringComparison]::OrdinalIgnoreCase) -lt 0 -and
        $line.IndexOf('AssetImportWorker',[StringComparison]::OrdinalIgnoreCase) -lt 0
    })
    if ($matches.Count -gt 1) { throw "Multiple main Unity processes match benchmark fixture: $($matches.ProcessId -join ', ')" }
    return $matches | Select-Object -First 1
}

function Close-BenchmarkUnity {
    $info = Find-BenchmarkUnityProcess
    if ($null -eq $info) { return $true }
    $pidToClose = [int]$info.ProcessId
    $process = [Diagnostics.Process]::GetProcessById($pidToClose)
    if (-not $process.CloseMainWindow()) {
        Write-Warning "CloseMainWindow returned false for benchmark Unity PID $pidToClose"
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $still = Get-Process -Id $pidToClose -ErrorAction SilentlyContinue
        if ($null -eq $still) { return $true }
    }
    Write-Warning "Benchmark Unity PID $pidToClose did not exit within 60s. It will not be force-killed by the benchmark runner."
    return $false
}

$existing = Find-BenchmarkUnityProcess
if ($ReuseRunningEditor) {
    if ($null -eq $existing) { throw 'ReuseRunningEditor requires the exact benchmark Unity Editor to already be running.' }
    $resetRun = Invoke-External -FileName 'pwsh' -Arguments @(
        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Reset-LiveFixture.ps1'),
        '-ProjectPath',$fixture,'-HeraCli',$cli)
    if ($resetRun.exit_code -ne 0) { throw "Live fixture reset failed: $($resetRun.stderr)`n$($resetRun.stdout)" }
}
else {
    if ($null -ne $existing) { throw "Benchmark fixture is already open in Unity PID $($existing.ProcessId)" }
    $resetRun = Invoke-External -FileName 'pwsh' -Arguments @(
        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Reset-Fixture.ps1'),'-ProjectPath',$fixture)
    if ($resetRun.exit_code -ne 0) { throw "Fixture reset failed: $($resetRun.stderr)" }
}

$unityPid = $null
$codexRun = $null
$scoreData = $null
$startedAt = [DateTimeOffset]::UtcNow
$runWatch = [Diagnostics.Stopwatch]::StartNew()
$closeSucceeded = $false
try {
    if ($ReuseRunningEditor) {
        $unityInfo = Find-BenchmarkUnityProcess
        if ($null -eq $unityInfo) { throw 'Benchmark Unity disappeared after live reset.' }
        $unityPid = [int]$unityInfo.ProcessId
    }
    else {
        [void](Invoke-HeraSetup @('editor','launch'))
        $unityInfo = Find-BenchmarkUnityProcess
        if ($null -eq $unityInfo) { throw 'Hera launch returned but the exact benchmark Unity process was not found.' }
        $unityPid = [int]$unityInfo.ProcessId
        [void](Invoke-HeraSetup @('scene','load','Assets/Scenes/SampleScene.unity'))
    }

    $armInstruction = switch ($Arm) {
        'uidoc' { 'Benchmark arm: use ui_doc apply/export for UI authoring. Generic manage_ui/manage_components/manage_gameobject mutations and batch are intentionally unavailable. ui_doc capture is allowed for visual verification.' }
        'primitives' { 'Benchmark arm: ui_doc authoring and batch are intentionally unavailable. Author UI only with manage_ui, manage_components, and manage_gameobject. ui_doc capture is available only as neutral visual verification.' }
        'primitives_batch' { 'Benchmark arm: ui_doc authoring is intentionally unavailable. Author UI with manage_ui, manage_components, manage_gameobject, and batch. Batch plans must be passed with --file. ui_doc capture is available only as neutral visual verification.' }
    }
    $benchmarkInstruction = @"

$armInstruction
Use only the `hera-agent-unity` command resolved from PATH. Do not inspect benchmark internals, environment variables, scoring files, or alternate Hera binaries. For visual verification, use the common neutral command `hera-agent-unity ui_doc capture --width 1280 --height 720`; if you capture more than once, use a fresh output path each time and never use `--overwrite`. The currently docked Game View window aspect is not part of the benchmark. Work autonomously until you have built and visually verified the requested UI. Do not ask for clarification.
"@
    $fullPrompt = $prompt.TrimEnd() + $benchmarkInstruction

    $codex = (Get-Command codex -ErrorAction Stop).Source
    $codexVersionRun = Invoke-External -FileName $codex -Arguments @('--version')
    $codexVersion = $codexVersionRun.stdout.Trim()
    $codexArgs = @(
        'exec','--dangerously-bypass-approvals-and-sandbox',
        '--ephemeral','--json','--ignore-user-config','--ignore-rules','--skip-git-repo-check',
        '-C',$fixture,'-m',$Model,
        '-c',('model_reasoning_effort="' + $ReasoningEffort + '"'),
        '-o',$lastMessage
    )
    if ($Task -eq 'T03') {
        $reference = (Resolve-Path -LiteralPath (Join-Path $bench $taskRecord.reference.path)).Path
        $codexArgs += @('-i',$reference)
    }
    $codexArgs += '-'

    $alternateHeraPaths = @(
        Get-Command hera-agent-unity -All -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source -Unique |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    $childPath = $shimDirectory + [IO.Path]::PathSeparator + $env:PATH
    $environment = @{
        PATH = $childPath
        HERA_AB_ARM = $Arm
        HERA_AB_REAL_CLI = $cli
        HERA_AB_LOG = $shimLog
        HERA_AGENT_COMPACT_JSON = '1'
        HERA_AGENT_NO_PATH_CHECK = '1'
    }
    $codexRun = Invoke-External -FileName $codex -Arguments $codexArgs -Environment $environment -StandardInput $fullPrompt -TimeoutSeconds ($CodexTimeoutMinutes*60)
    [IO.File]::WriteAllText($agentEvents,$codexRun.stdout,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($agentStderr,$codexRun.stderr,[Text.UTF8Encoding]::new($false))

    # Save whatever UI state the arm produced. This is neutral persistence after
    # mutation access has ended and prevents a close-time Save Scene prompt.
    [void](Invoke-HeraSetup @('scene','save'))

    $scoreRun = Invoke-External -FileName 'pwsh' -Arguments @(
        '-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Score-Run.ps1'),
        '-ProjectPath',$fixture,'-OraclePath',$oraclePath,'-OutputDirectory',$result,'-HeraCli',$cli)
    if ($scoreRun.exit_code -ne 0) { throw "Scorer failed: $($scoreRun.stderr)`n$($scoreRun.stdout)" }
    $scoreData = ($scoreRun.stdout.Trim() | ConvertFrom-Json)

    $eventText = $codexRun.stdout
    $eventObjects = @()
    foreach ($line in ($eventText -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $eventObjects += ($line | ConvertFrom-Json) } catch { }
    }
    $mcpCalls = @($eventObjects | Where-Object { $_.item.type -eq 'mcp_tool_call' })
    if ($mcpCalls.Count -gt 0) {
        $mcpNames = @($mcpCalls | ForEach-Object { ([string]$_.item.server) + ':' + ([string]$_.item.tool) })
        throw "Agent event audit found MCP/tool bypass attempt(s): $($mcpNames -join ', ')"
    }
    $bypassMarkers = @($cli, 'HERA_AB_REAL_CLI') + $alternateHeraPaths
    $bypassMarkers = @($bypassMarkers | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $bypassHits = @($bypassMarkers | Where-Object { $eventText.IndexOf($_,[StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($bypassHits.Count -gt 0) {
        throw "Agent event audit found alternate Hera binary/bypass marker(s): $($bypassHits -join ', ')"
    }
}
finally {
    $runWatch.Stop()
    if ($ReuseRunningEditor) {
        $closeSucceeded = $true
    }
    else {
        try { $closeSucceeded = Close-BenchmarkUnity } catch { Write-Warning $_.Exception.Message; $closeSucceeded = $false }
    }
}

if ($ReuseRunningEditor) {
    $afterRunUnity = Find-BenchmarkUnityProcess
    if ($null -eq $afterRunUnity -or [int]$afterRunUnity.ProcessId -ne [int]$unityPid) {
        throw 'Benchmark Unity process changed or disappeared during a reused-Editor run.'
    }
}
elseif (-not $closeSucceeded) {
    throw 'Benchmark Unity did not close gracefully.'
}

$assetConfigAfterSha256 = if (Test-Path -LiteralPath $assetConfigPath -PathType Leaf) {
    (Get-FileHash -LiteralPath $assetConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
} else { 'absent' }
if ($assetConfigAfterSha256 -ne $assetConfigSha256) {
    throw "User asset-config changed during run. before=$assetConfigSha256 after=$assetConfigAfterSha256"
}

$backupDirectory = Join-Path $fixture 'Temp\__Backupscenes'
$backupCount = if(Test-Path -LiteralPath $backupDirectory){@(Get-ChildItem -LiteralPath $backupDirectory -File -Recurse -ErrorAction SilentlyContinue).Count}else{0}
if ($backupCount -ne 0) { throw "Benchmark run left $backupCount Scene Recovery backup file(s)." }

$calls = if(Test-Path -LiteralPath $shimLog){@(Get-Content -LiteralPath $shimLog | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_ | ConvertFrom-Json })}else{@()}
$allowedCalls = @($calls | Where-Object { $_.allowed -eq $true })
$mutationCalls = @($allowedCalls | Where-Object { $_.classification -in @('mutation','mutation_batch') })
$verificationCalls = @($allowedCalls | Where-Object { $_.classification -like 'verification*' })
$forbiddenCalls = @($calls | Where-Object { $_.allowed -eq $false }).Count
$stdoutBytes = [long](($allowedCalls | Measure-Object -Property stdout_bytes -Sum).Sum)
$stderrBytes = [long](($allowedCalls | Measure-Object -Property stderr_bytes -Sum).Sum)
$cliSha = (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash.ToLowerInvariant()
# A fixed Codex timeout is a measured outcome, not an infrastructure retry.
# Score the exact Unity state at the cutoff. A command that the arm shim blocks
# before execution is also a measured usability/following failure, not a reason
# to give that arm a fresh attempt. Only infrastructure/audit failure invalidates.
$codexCompletedOrTimedOut = ($null -ne $codexRun) -and (($codexRun.exit_code -eq 0) -or [bool]$codexRun.timed_out)
$benchmarkValid = $codexCompletedOrTimedOut

$providerUsage = $null
if ($null -ne $codexRun -and -not [string]::IsNullOrWhiteSpace($codexRun.stdout)) {
    foreach ($line in ($codexRun.stdout -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $evt = $line | ConvertFrom-Json
            if ($evt.type -eq 'turn.completed' -and $null -ne $evt.usage) {
                $providerUsage = $evt.usage
            }
        }
        catch { }
    }
}

$finishedAt = [DateTimeOffset]::UtcNow
$runRecord = [ordered]@{
    schema='hera.ui-authoring-ab-run/1'
    task=$Task
    arm=$Arm
    repetition=$Repetition
    repo_commit=[string]$marker.repository_head
    cli_sha256=$cliSha
    connector_version=[string]$marker.connector_version
    unity_version=[string]$marker.unity_version
    unity_pid=$unityPid
    codex_version=$codexVersion
    model=$Model
    reasoning_effort=$ReasoningEffort
    asset_config_sha256=$assetConfigSha256
    started_at=$startedAt.ToString('o')
    finished_at=$finishedAt.ToString('o')
    wall_ms=[Math]::Round($runWatch.Elapsed.TotalMilliseconds,3)
    agent_wall_ms=if($null -ne $codexRun){[double]$codexRun.wall_ms}else{$null}
    codex_exit_code=if($null -ne $codexRun){$codexRun.exit_code}else{$null}
    codex_timed_out=if($null -ne $codexRun){[bool]$codexRun.timed_out}else{$false}
    hera_calls=$allowedCalls.Count
    mutation_calls=$mutationCalls.Count
    verification_calls=$verificationCalls.Count
    forbidden_calls=$forbiddenCalls
    benchmark_valid=$benchmarkValid
    stdout_bytes=$stdoutBytes
    stderr_bytes=$stderrBytes
    estimated_tool_result_tokens=[Math]::Ceiling(($stdoutBytes+$stderrBytes)/4.0)
    provider_usage=$providerUsage
    score=if($null -ne $scoreData){[double]$scoreData.accuracy_score}else{$null}
    strict_pass=if($null -ne $scoreData){[bool]$scoreData.strict_pass}else{$false}
    recovery_backup_files=$backupCount
    editor_reused=[bool]$ReuseRunningEditor
    graceful_unity_close=if($ReuseRunningEditor){$null}else{$closeSucceeded}
}
[IO.File]::WriteAllText((Join-Path $result 'run.json'),($runRecord|ConvertTo-Json -Depth 12)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output (($runRecord|ConvertTo-Json -Compress -Depth 12))

if ($null -eq $codexRun) { exit 2 }
if ($codexRun.exit_code -ne 0 -and -not [bool]$codexRun.timed_out) { exit 2 }
exit 0
