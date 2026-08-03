param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [string]$Filter = "HeraAgent.Tests",

    [ValidateRange(1000, 3600000)]
    [int]$TimeoutMs = 600000,

    [ValidateRange(0, 60)]
    [int]$StabilizationSeconds = 8
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repository = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = (Resolve-Path $ProjectPath).Path
$manifest = Join-Path $project "Packages\manifest.json"
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Unity package manifest not found: $manifest"
}

$original = [IO.File]::ReadAllBytes($manifest)
$beforeHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
$primaryError = $null
$testExit = 0
$restoreExit = 0

Push-Location $repository
try {
    try {
        $document = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $testables = @()
        $testablesProperty = $document.PSObject.Properties["testables"]
        if ($null -ne $testablesProperty) {
            $testables = @($testablesProperty.Value)
        }
        if ($testables -notcontains "com.notnull92.hera-agent-unity") {
            $testables += "com.notnull92.hera-agent-unity"
        }
        $document | Add-Member -NotePropertyName testables -NotePropertyValue $testables -Force
        [IO.File]::WriteAllText(
            $manifest,
            ($document | ConvertTo-Json -Depth 100),
            [Text.UTF8Encoding]::new($false))

        & go run . --project $project --timeout $TimeoutMs editor refresh --compile
        if ($LASTEXITCODE -ne 0) {
            throw "Unity compilation failed after enabling package tests"
        }
        if ($StabilizationSeconds -gt 0) {
            Start-Sleep -Seconds $StabilizationSeconds
        }

        & go run . --project $project --timeout $TimeoutMs test --mode EditMode --filter $Filter
        $testExit = $LASTEXITCODE
        if ($testExit -ne 0) {
            throw "Unity package tests failed with exit code $testExit"
        }
    }
    catch {
        $primaryError = $_
    }
    finally {
        [IO.File]::WriteAllBytes($manifest, $original)
        $afterHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($afterHash -ne $beforeHash) {
            $restoreExit = 1
            if ($null -eq $primaryError) {
                $primaryError = [System.Exception]::new(
                    "Unity manifest restoration hash mismatch: $beforeHash != $afterHash")
            }
        }

        & go run . --project $project --timeout $TimeoutMs editor refresh --compile
        if ($LASTEXITCODE -ne 0) {
            $restoreExit = $LASTEXITCODE
            if ($null -eq $primaryError) {
                $primaryError = [System.Exception]::new(
                    "Unity compilation failed after restoring the package manifest")
            }
        }
    }
}
finally {
    Pop-Location
}

if ($null -ne $primaryError) {
    throw $primaryError
}
if ($testExit -ne 0 -or $restoreExit -ne 0) {
    exit 1
}