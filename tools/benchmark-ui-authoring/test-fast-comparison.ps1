$ErrorActionPreference = 'Stop'

$comparer = (Resolve-Path (Join-Path $PSScriptRoot 'Compare-Results.ps1')).Path
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('hera-fast-comparison-' + [Guid]::NewGuid().ToString('N'))

function Write-Json {
    param([string]$Path, $Value)

    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

function New-FastWave {
    param(
        [string]$Path,
        [switch]$GenericOnlyCriticalFailure
    )

    [IO.Directory]::CreateDirectory($Path) | Out-Null
    Write-Json (Join-Path $Path 'wave.json') ([ordered]@{
        schema = 'hera.ui-authoring-ab-wave/1'
        protocol = 'fast'
        status = 'fast_complete'
    })

    foreach ($task in @('T01','T02','T03')) {
        foreach ($arm in @('uidoc','primitives_batch')) {
            for ($repetition = 1; $repetition -le 2; $repetition++) {
                $cell = Join-Path $Path (Join-Path $task (Join-Path $arm (Join-Path ("rep-{0:D2}" -f $repetition) 'attempt-01')))
                $critical = if ($GenericOnlyCriticalFailure -and $task -eq 'T02' -and $arm -eq 'primitives_batch' -and $repetition -eq 1) { @('required_item_missing') } else { @() }
                Write-Json (Join-Path $cell 'run.json') ([ordered]@{
                    schema = 'hera.ui-authoring-ab-run/1'
                    task = $task
                    arm = $arm
                    repetition = $repetition
                    benchmark_valid = $true
                    score = 80
                    strict_pass = $true
                    agent_wall_ms = 240000
                    wall_ms = 250000
                    hera_calls = 8
                    mutation_calls = 4
                    verification_calls = 4
                    estimated_tool_result_tokens = 120
                    provider_usage = $null
                })
                Write-Json (Join-Path $cell 'score.json') ([ordered]@{ critical_failures = $critical })
            }
        }
    }
}

function Invoke-Comparison {
    param([string]$Wave)

    $output = & pwsh -NoLogo -NoProfile -File $comparer -WaveDirectory $Wave 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Fast comparison failed: $($output -join [Environment]::NewLine)" }
    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

try {
    $equalWave = Join-Path $temporary 'equal'
    New-FastWave -Path $equalWave
    $equal = Invoke-Comparison -Wave $equalWave
    if ($equal.protocol -ne 'fast') { throw "Expected fast protocol, got $($equal.protocol)" }
    if ($equal.expected_runs -ne 12) { throw "Expected 12 fast runs, got $($equal.expected_runs)" }
    if ($equal.decision -ne 'reduction_candidate') { throw "Expected reduction candidate, got $($equal.decision)" }

    $criticalWave = Join-Path $temporary 'generic-critical'
    New-FastWave -Path $criticalWave -GenericOnlyCriticalFailure
    $critical = Invoke-Comparison -Wave $criticalWave
    if ($critical.decision -ne 'inconclusive') { throw "Expected inconclusive generic-critical result, got $($critical.decision)" }

    Write-Host 'FAST_COMPARISON_PASS'
}
finally {
    Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue
}
