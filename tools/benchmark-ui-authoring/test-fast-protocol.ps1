$ErrorActionPreference = 'Stop'

$runner = (Resolve-Path (Join-Path $PSScriptRoot 'Run-Screening.ps1')).Path

function Invoke-Plan {
    $output = & pwsh -NoLogo -NoProfile -File $runner -Protocol fast -PlanOnly 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Fast plan failed: $($output -join [Environment]::NewLine)" }
    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Assert-Rejected {
    param([string[]]$Arguments)

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $null = & pwsh -NoLogo -NoProfile -File $runner -Protocol fast -PlanOnly @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    if ($exitCode -eq 0) { throw "Fast override unexpectedly succeeded: $($Arguments -join ' ')" }
}

$plan = Invoke-Plan
if ($plan.schema -ne 'hera.ui-authoring-ab-wave-plan/1') { throw "Unexpected plan schema: $($plan.schema)" }
if ($plan.protocol -ne 'fast') { throw "Unexpected protocol: $($plan.protocol)" }
if ($plan.expected_cells -ne 12) { throw "Expected 12 cells, got $($plan.expected_cells)" }
if ((@($plan.arms) -join ',') -ne 'uidoc,primitives_batch') { throw "Unexpected fast arms: $(@($plan.arms) -join ',')" }
if ($plan.repetitions -ne 2) { throw "Expected two repetitions, got $($plan.repetitions)" }
if ($plan.codex_timeout_minutes -ne 4) { throw "Expected four-minute sessions, got $($plan.codex_timeout_minutes)" }
if ($plan.max_attempts_per_cell -ne 1) { throw "Expected one attempt, got $($plan.max_attempts_per_cell)" }
if ($plan.admission_cutoff_minutes -ne 53) { throw "Expected 53-minute admission cutoff, got $($plan.admission_cutoff_minutes)" }
if ($plan.wave_deadline_minutes -ne 60) { throw "Expected 60-minute deadline, got $($plan.wave_deadline_minutes)" }
if ((@($plan.arm_order[0]) -join ',') -ne 'uidoc,primitives_batch') { throw 'Unexpected first arm order.' }
if ((@($plan.arm_order[1]) -join ',') -ne 'primitives_batch,uidoc') { throw 'Unexpected second arm order.' }

Assert-Rejected @('-CodexTimeoutMinutes','15')
Assert-Rejected @('-Repetitions','3')
Assert-Rejected @('-MaxAttemptsPerCell','2')

Write-Host 'FAST_PROTOCOL_PASS'
