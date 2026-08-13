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

# A run that dies before its finally block leaves the package enabled as a
# testable. Every later run then restores that leaked state byte-for-byte and
# stays silent, while the catalog payload below is measured against a package
# whose test fixtures declare [HeraTool] classes. Refuse to start instead: the
# baseline this gate defends would be contaminated by tools that never ship.
$startingTestables = @()
$startingProperty = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).PSObject.Properties["testables"]
if ($null -ne $startingProperty) {
    $startingTestables = @($startingProperty.Value)
}
if ($startingTestables -contains "com.notnull92.hera-agent-unity") {
    throw ("Package tests are already enabled in $manifest before this run started. " +
        "A previous run leaked its testables entry, so the production catalog cannot be " +
        "measured here. Remove `"com.notnull92.hera-agent-unity`" from testables and re-run.")
}
$primaryError = $null
$testExit = 0
$restoreExit = 0
$catalogFile = $null
$catalogReportFile = $null
$catalogBaseline = Join-Path $repository "docs\metrics\catalog-payload-baseline.json"

Push-Location $repository
try {
    # Measure the production package before enabling the test assembly. Test
    # fixtures intentionally declare [HeraTool] classes and must not enter the
    # built-in runtime surface baseline.
    & go run . --project $project --timeout $TimeoutMs editor refresh --compile
    if ($LASTEXITCODE -ne 0) {
        throw "Unity compilation failed before catalog payload validation"
    }
    if (-not (Test-Path -LiteralPath $catalogBaseline)) {
        throw "Catalog payload baseline not found: $catalogBaseline"
    }
    $catalogFile = Join-Path ([IO.Path]::GetTempPath()) (
        "hera-tool-catalog-" + [Guid]::NewGuid().ToString("N") + ".json")
    $catalogReportFile = Join-Path ([IO.Path]::GetTempPath()) (
        "hera-tool-catalog-report-" + [Guid]::NewGuid().ToString("N") + ".json")
    $catalogOutput = & go run . --project $project --timeout $TimeoutMs `
        list --catalog --schema_version hera.tool-catalog/1
    if ($LASTEXITCODE -ne 0) {
        throw "Live Unity tool catalog export failed"
    }
    [IO.File]::WriteAllText(
        $catalogFile,
        ([string]::Join([Environment]::NewLine, [string[]]$catalogOutput) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false))

    & go run ./tools/catalog-payload-report `
        --catalog $catalogFile `
        --compare $catalogBaseline `
        --fail-on-change `
        --output $catalogReportFile
    if ($LASTEXITCODE -ne 0) {
        if (Test-Path -LiteralPath $catalogReportFile) {
            [Console]::Error.WriteLine([IO.File]::ReadAllText($catalogReportFile))
        }
        throw "Unity tool catalog differs from the reviewed payload baseline"
    }

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
    foreach ($temporaryPath in @($catalogFile, $catalogReportFile)) {
        if ($null -ne $temporaryPath -and (Test-Path -LiteralPath $temporaryPath)) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
    Pop-Location
}

if ($null -ne $primaryError) {
    throw $primaryError
}
if ($testExit -ne 0 -or $restoreExit -ne 0) {
    exit 1
}