param(
    [Parameter(Mandatory = $true)][string]$WaveDirectory,
    [string]$OutputJson,
    [string]$OutputMarkdown
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bench = Join-Path $repository 'docs\benchmarks\ui-doc-ab'
$manifest = Get-Content -LiteralPath (Join-Path $bench 'manifest.json') -Raw | ConvertFrom-Json
$wave = (Resolve-Path -LiteralPath $WaveDirectory).Path
$waveMetaPath = Join-Path $wave 'wave.json'
if (-not (Test-Path -LiteralPath $waveMetaPath -PathType Leaf)) { throw "Wave is missing wave.json: $wave" }
$waveMeta = Get-Content -LiteralPath $waveMetaPath -Raw | ConvertFrom-Json
$protocol = if ([string]::IsNullOrWhiteSpace([string]$waveMeta.protocol)) { 'formal' } else { [string]$waveMeta.protocol }
if ($protocol -notin @('formal','fast')) { throw "Unknown wave protocol: $protocol" }
$config = if ($protocol -eq 'fast') { $manifest.fast } else { $manifest }
if ($null -eq $config) { throw "Manifest does not define protocol: $protocol" }
$expectedStatus = if ($protocol -eq 'fast') { 'fast_complete' } else { 'screening_complete' }
if ([string]$waveMeta.status -ne $expectedStatus) { throw "Wave is not eligible for comparison: protocol=$protocol status=$($waveMeta.status) expected=$expectedStatus" }
$armIds = if ($protocol -eq 'fast') { @($config.arms | ForEach-Object { [string]$_ }) } else { @($config.arms | ForEach-Object { [string]$_.id }) }
$repetitions = if ($protocol -eq 'fast') { [int]$config.repetitions } else { [int]$manifest.screening_repetitions }
if ($armIds.Count -lt 2 -or $armIds -notcontains 'uidoc') { throw "Protocol arms are invalid: $($armIds -join ', ')" }
if ([string]::IsNullOrWhiteSpace($OutputJson)) { $OutputJson = Join-Path $wave 'comparison.json' }
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) { $OutputMarkdown = Join-Path $wave 'comparison.md' }

function Median([double[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) { return $null }
    $sorted = @($values | Sort-Object)
    $n = $sorted.Count
    if ($n % 2 -eq 1) { return [double]$sorted[[int][Math]::Floor($n/2)] }
    return ([double]$sorted[$n/2-1] + [double]$sorted[$n/2]) / 2.0
}
function Mean([double[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) { return $null }
    return [double](($values | Measure-Object -Average).Average)
}
function Round3($value) {
    if ($null -eq $value) { return $null }
    return [Math]::Round([double]$value,3)
}

$runFiles = @(Get-ChildItem -LiteralPath $wave -Filter run.json -File -Recurse)
$records = New-Object System.Collections.Generic.List[object]
foreach ($file in $runFiles) {
    try {
        $run = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ($run.schema -ne 'hera.ui-authoring-ab-run/1' -or -not [bool]$run.benchmark_valid) { continue }
        $scorePath = Join-Path $file.Directory.FullName 'score.json'
        if (-not (Test-Path -LiteralPath $scorePath -PathType Leaf)) { continue }
        $score = Get-Content -LiteralPath $scorePath -Raw | ConvertFrom-Json
        $records.Add([pscustomobject]@{
            task = [string]$run.task
            arm = [string]$run.arm
            repetition = [int]$run.repetition
            score = [double]$run.score
            strict_pass = [bool]$run.strict_pass
            agent_wall_ms = [double]$run.agent_wall_ms
            total_wall_ms = [double]$run.wall_ms
            calls = [int]$run.hera_calls
            mutation_calls = [int]$run.mutation_calls
            verification_calls = [int]$run.verification_calls
            tool_result_tokens = [double]$run.estimated_tool_result_tokens
            input_tokens = if($null -ne $run.provider_usage){[double]$run.provider_usage.input_tokens}else{0}
            cached_input_tokens = if($null -ne $run.provider_usage){[double]$run.provider_usage.cached_input_tokens}else{0}
            output_tokens = if($null -ne $run.provider_usage){[double]$run.provider_usage.output_tokens}else{0}
            reasoning_output_tokens = if($null -ne $run.provider_usage){[double]$run.provider_usage.reasoning_output_tokens}else{0}
            critical_failures = @($score.critical_failures)
            directory = $file.Directory.FullName
        })
    }
    catch { Write-Warning "Skipping invalid result $($file.FullName): $($_.Exception.Message)" }
}

$expectedCount = [int]$manifest.tasks.Count * $armIds.Count * $repetitions
$cellErrors = New-Object System.Collections.Generic.List[string]
foreach ($task in @($manifest.tasks)) {
    foreach ($armId in $armIds) {
        for ($rep=1; $rep -le $repetitions; $rep++) {
            $count = @($records | Where-Object { $_.task -eq $task.id -and $_.arm -eq $armId -and $_.repetition -eq $rep }).Count
            if ($count -ne 1) { $cellErrors.Add("$($task.id)/$armId/rep-$rep valid_count=$count") }
        }
    }
}

$armSummary = New-Object System.Collections.Generic.List[object]
foreach ($armId in $armIds) {
    $subset = @($records | Where-Object { $_.arm -eq $armId })
    $armSummary.Add([pscustomobject]@{
        arm = $armId
        runs = $subset.Count
        strict_passes = @($subset | Where-Object { $_.strict_pass }).Count
        mean_score = Round3 (Mean @($subset.score))
        median_score = Round3 (Median @($subset.score))
        median_agent_wall_ms = Round3 (Median @($subset.agent_wall_ms))
        mean_agent_wall_ms = Round3 (Mean @($subset.agent_wall_ms))
        mean_calls = Round3 (Mean @($subset.calls))
        mean_mutation_calls = Round3 (Mean @($subset.mutation_calls))
        mean_verification_calls = Round3 (Mean @($subset.verification_calls))
        mean_tool_result_tokens = Round3 (Mean @($subset.tool_result_tokens))
        mean_input_tokens = Round3 (Mean @($subset.input_tokens))
        mean_cached_input_tokens = Round3 (Mean @($subset.cached_input_tokens))
        mean_output_tokens = Round3 (Mean @($subset.output_tokens))
        mean_reasoning_output_tokens = Round3 (Mean @($subset.reasoning_output_tokens))
    })
}

$taskArmSummary = New-Object System.Collections.Generic.List[object]
foreach ($task in @($manifest.tasks)) {
    foreach ($armId in $armIds) {
        $subset = @($records | Where-Object { $_.task -eq $task.id -and $_.arm -eq $armId })
        $taskArmSummary.Add([pscustomobject]@{
            task = [string]$task.id
            arm = $armId
            runs = $subset.Count
            strict_passes = @($subset | Where-Object { $_.strict_pass }).Count
            mean_score = Round3 (Mean @($subset.score))
            median_agent_wall_ms = Round3 (Median @($subset.agent_wall_ms))
            mean_calls = Round3 (Mean @($subset.calls))
            mean_input_tokens = Round3 (Mean @($subset.input_tokens))
        })
    }
}

$genericCandidates = @($armSummary | Where-Object { $_.arm -ne 'uidoc' })
$bestGeneric = $genericCandidates | Sort-Object -Property @{Expression='mean_score';Descending=$true},@{Expression='strict_passes';Descending=$true},@{Expression='median_agent_wall_ms';Descending=$false} | Select-Object -First 1
$uidoc = $armSummary | Where-Object { $_.arm -eq 'uidoc' } | Select-Object -First 1

$perTask = New-Object System.Collections.Generic.List[object]
$maxUidocAdvantage = [double]::NegativeInfinity
$genericOnlyPatterns = New-Object System.Collections.Generic.List[object]
$genericOnlyThreshold = if ($protocol -eq 'fast') { 1 } else { 2 }
foreach ($task in @($manifest.tasks)) {
    $u = $taskArmSummary | Where-Object { $_.task -eq $task.id -and $_.arm -eq 'uidoc' } | Select-Object -First 1
    $g = $taskArmSummary | Where-Object { $_.task -eq $task.id -and $_.arm -eq $bestGeneric.arm } | Select-Object -First 1
    $advantage = [double]$u.mean_score - [double]$g.mean_score
    if ($advantage -gt $maxUidocAdvantage) { $maxUidocAdvantage = $advantage }
    $perTask.Add([pscustomobject]@{
        task = [string]$task.id
        uidoc_mean = [double]$u.mean_score
        generic_mean = [double]$g.mean_score
        uidoc_advantage = Round3 $advantage
        uidoc_strict = [int]$u.strict_passes
        generic_strict = [int]$g.strict_passes
    })

    $uRuns = @($records | Where-Object { $_.task -eq $task.id -and $_.arm -eq 'uidoc' })
    $gRuns = @($records | Where-Object { $_.task -eq $task.id -and $_.arm -eq $bestGeneric.arm })
    $genericIds = @($gRuns | ForEach-Object { $_.critical_failures } | Where-Object { $_ } | Sort-Object -Unique)
    foreach ($failureId in $genericIds) {
        $gCount = @($gRuns | Where-Object { $_.critical_failures -contains $failureId }).Count
        $uCount = @($uRuns | Where-Object { $_.critical_failures -contains $failureId }).Count
        if ($gCount -ge $genericOnlyThreshold -and $uCount -eq 0) {
            $genericOnlyPatterns.Add([pscustomobject]@{task=[string]$task.id;failure=[string]$failureId;generic_count=$gCount;uidoc_count=$uCount})
        }
    }
}

$overallAdvantage = [double]$uidoc.mean_score - [double]$bestGeneric.mean_score
$strictDelta = [int]$uidoc.strict_passes - [int]$bestGeneric.strict_passes
$reduction = if ($protocol -eq 'fast') {
    (
        [int]$uidoc.strict_passes -eq [int]$bestGeneric.strict_passes -and
        [Math]::Abs($overallAdvantage) -le [double]$config.decision.reduction_candidate.overall_mean_abs_delta_max -and
        @($perTask | Where-Object { [Math]::Abs([double]$_.uidoc_advantage) -gt [double]$config.decision.reduction_candidate.per_task_mean_abs_delta_max }).Count -eq 0 -and
        $genericOnlyPatterns.Count -le [int]$config.decision.reduction_candidate.generic_only_critical_failures
    )
}
else {
    (
    [int]$uidoc.strict_passes -eq [int]$bestGeneric.strict_passes -and
    [Math]::Abs($overallAdvantage) -le [double]$config.decision.removal_candidate.overall_mean_abs_delta_max -and
    @($perTask | Where-Object { [double]$_.uidoc_advantage -gt [double]$config.decision.removal_candidate.per_task_generic_deficit_max }).Count -eq 0 -and
    $genericOnlyPatterns.Count -eq 0
    )
}
$retention = (
    $overallAdvantage -ge [double]$config.decision.retention_candidate.overall_uidoc_advantage_min -or
    $maxUidocAdvantage -ge [double]$config.decision.retention_candidate.single_task_uidoc_advantage_min -or
    $strictDelta -ge [int]$config.decision.retention_candidate.extra_uidoc_strict_passes_min
)
$decision = if($cellErrors.Count -gt 0){'incomplete'}elseif($protocol -eq 'fast' -and $retention){'retain_pending_simplification'}elseif($protocol -eq 'fast' -and $reduction){'reduction_candidate'}elseif($protocol -eq 'fast'){'inconclusive'}elseif($retention){'retention_candidate'}elseif($reduction){'removal_candidate'}else{'borderline_confirmation_required'}

$result = [ordered]@{
    schema = 'hera.ui-authoring-ab-comparison/1'
    wave = Split-Path -Leaf $wave
    protocol = $protocol
    wave_status = [string]$waveMeta.status
    arms = $armIds
    valid_runs = $records.Count
    expected_runs = $expectedCount
    expected_screening_runs = $expectedCount
    cell_errors = $cellErrors
    arm_summary = $armSummary
    task_arm_summary = $taskArmSummary
    best_generic_arm = [string]$bestGeneric.arm
    overall_uidoc_advantage = Round3 $overallAdvantage
    strict_pass_delta_uidoc_minus_generic = $strictDelta
    max_single_task_uidoc_advantage = Round3 $maxUidocAdvantage
    per_task = $perTask
    generic_only_critical_patterns = $genericOnlyPatterns
    decision = $decision
}

[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputJson),($result|ConvertTo-Json -Depth 16)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# ui_doc Authoring A/B Comparison')
$lines.Add('')
$lines.Add('Wave: ' + (Split-Path -Leaf $wave))
$lines.Add('Protocol: ' + $protocol)
$lines.Add('')
$lines.Add("Decision: **$decision**")
$lines.Add('')
$lines.Add("Valid runs: $($records.Count) / $expectedCount")
$lines.Add('')
$lines.Add('| Arm | Strict | Mean score | Median agent ms | Mean calls | Mean input tokens |')
$lines.Add('|---|---:|---:|---:|---:|---:|')
foreach($row in $armSummary){$lines.Add("| $($row.arm) | $($row.strict_passes)/$($row.runs) | $($row.mean_score) | $($row.median_agent_wall_ms) | $($row.mean_calls) | $($row.mean_input_tokens) |")}
$lines.Add('')
$lines.Add("Best generic arm: **$($bestGeneric.arm)**")
$lines.Add('')
$lines.Add("Overall uidoc advantage: **$(Round3 $overallAdvantage) points**")
$lines.Add('')
$lines.Add('| Task | uidoc mean | generic mean | uidoc advantage | uidoc strict | generic strict |')
$lines.Add('|---|---:|---:|---:|---:|---:|')
foreach($row in $perTask){$lines.Add("| $($row.task) | $($row.uidoc_mean) | $($row.generic_mean) | $($row.uidoc_advantage) | $($row.uidoc_strict) | $($row.generic_strict) |")}
if($genericOnlyPatterns.Count -gt 0){
    $genericOnlyHeading = if($protocol -eq 'fast'){'Generic-only critical failures:'}else{'Generic-only repeated critical failure patterns:'}
    $lines.Add('')
    $lines.Add($genericOnlyHeading)
    foreach($p in $genericOnlyPatterns){$lines.Add("- $($p.task): $($p.failure) generic=$($p.generic_count), uidoc=$($p.uidoc_count)")}
}
if($cellErrors.Count -gt 0){$lines.Add('');$lines.Add('Incomplete cells:');foreach($e in $cellErrors){$lines.Add("- $e")}}
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputMarkdown),$lines,[Text.UTF8Encoding]::new($false))

Write-Output (($result|ConvertTo-Json -Compress -Depth 16))
if($decision -eq 'incomplete'){exit 2}
exit 0
