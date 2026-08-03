param(
    [string]$Project2022_3,
    [string]$Project2023_2,
    [string]$Project6000_0_2,
    [string]$Project6000_3_4,
    [string]$Project6000_5Plus,

    [string[]]$RuntimeBuckets = @(),
    [string]$RuntimeFilter = "HeraAgent.Tests",

    [ValidateRange(1000, 3600000)]
    [int]$TimeoutMs = 600000,

    [switch]$AllowBlocked
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repository = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$compileScript = Join-Path $PSScriptRoot "compile-exact-source.ps1"
$runtimeScript = Join-Path $PSScriptRoot "run-package-tests.ps1"
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source

$buckets = [ordered]@{
    "2022.3" = $Project2022_3
    "2023.2" = $Project2023_2
    "6000.0-6000.2" = $Project6000_0_2
    "6000.3-6000.4" = $Project6000_3_4
    "6000.5+" = $Project6000_5Plus
}

$knownBuckets = @($buckets.Keys)
foreach ($bucket in $RuntimeBuckets) {
    if ($knownBuckets -notcontains $bucket) {
        throw "Unknown runtime bucket '$bucket'. Use one of: $($knownBuckets -join ', ')"
    }
}
$runtimeSet = [Collections.Generic.HashSet[string]]::new(
    [string[]]$RuntimeBuckets,
    [StringComparer]::Ordinal)

function Invoke-IsolatedScript([string]$script, [string[]]$arguments) {
    $output = @(& $pwsh -NoProfile -File $script @arguments 2>&1)
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = (($output | ForEach-Object { $_.ToString() }) -join "`n").Trim()
    }
}

$records = @()
foreach ($entry in $buckets.GetEnumerator()) {
    $bucket = $entry.Key
    $projectInput = $entry.Value
    $compileStatus = "BLOCKED"
    $runtimeStatus = if ($runtimeSet.Contains($bucket)) { "BLOCKED" } else { "NOT_REQUESTED" }
    $diagnostic = ""
    $project = ""

    if ([string]::IsNullOrWhiteSpace($projectInput)) {
        $diagnostic = "project path was not supplied"
    }
    elseif (-not (Test-Path -LiteralPath $projectInput -PathType Container)) {
        $diagnostic = "project path does not exist"
        $project = $projectInput
    }
    else {
        $project = (Resolve-Path $projectInput).Path
        $compile = Invoke-IsolatedScript $compileScript @(
            "-ProjectPath", $project,
            "-RepositoryRoot", $repository)
        if ($compile.ExitCode -eq 0) {
            $compileStatus = "PASS"
        }
        else {
            $compileStatus = "FAIL"
            $diagnostic = $compile.Output
        }

        if ($runtimeSet.Contains($bucket)) {
            if ($compileStatus -ne "PASS") {
                $runtimeStatus = "BLOCKED"
            }
            else {
                $runtime = Invoke-IsolatedScript $runtimeScript @(
                    "-ProjectPath", $project,
                    "-Filter", $RuntimeFilter,
                    "-TimeoutMs", $TimeoutMs.ToString())
                if ($runtime.ExitCode -eq 0) {
                    $runtimeStatus = "PASS"
                }
                else {
                    $runtimeStatus = "FAIL"
                    $diagnostic = $runtime.Output
                }
            }
        }
    }

    $records += [pscustomobject]@{
        bucket = $bucket
        project = $project
        compile = $compileStatus
        runtime = $runtimeStatus
        diagnostic = $diagnostic
    }
}

$compilePass = @($records | Where-Object compile -eq "PASS").Count
$runtimePass = @($records | Where-Object runtime -eq "PASS").Count
$failed = @($records | Where-Object { $_.compile -eq "FAIL" -or $_.runtime -eq "FAIL" })
$blocked = @($records | Where-Object { $_.compile -eq "BLOCKED" -or $_.runtime -eq "BLOCKED" })
$result = [pscustomobject]@{
    schema = "hera.compatibility-matrix/1"
    generated_at = [DateTimeOffset]::UtcNow.ToString("O")
    compile_pass = $compilePass
    runtime_pass = $runtimePass
    failed = $failed.Count
    blocked = $blocked.Count
    records = $records
}
$result | ConvertTo-Json -Depth 8

if ($failed.Count -gt 0) {
    exit 1
}
if ($blocked.Count -gt 0 -and -not $AllowBlocked) {
    exit 2
}
