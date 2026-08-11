param(
    [string]$TemplateProject = 'C:\Users\PC\Desktop\Cowork\test6000.3.5f2'
)

$ErrorActionPreference = 'Stop'
$destination = Join-Path ([IO.Path]::GetTempPath()) ('hera-ui-ab-fixture-smoke-' + [Guid]::NewGuid().ToString('N'))

try {
    $createdText = & pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'New-Fixture.ps1') `
        -TemplateProject $TemplateProject -Destination $destination
    if ($LASTEXITCODE -ne 0) { throw "New-Fixture failed with exit $LASTEXITCODE" }
    $created = $createdText | ConvertFrom-Json
    if ($created.path -ne $destination) { throw 'Created fixture path mismatch.' }
    if ($created.fixture_profile -ne 'minimal-ugui') { throw "Unexpected fixture profile: $($created.fixture_profile)" }

    $manifest = Get-Content -LiteralPath (Join-Path $destination 'Packages\manifest.json') -Raw | ConvertFrom-Json
    $properties = @($manifest.dependencies.PSObject.Properties)
    $heraReference = [string]$manifest.dependencies.'com.notnull92.hera-agent-unity'
    if ($heraReference -notlike 'file:*/.hera-ab/Connector') {
        throw "Unexpected fixture-local Connector reference: $heraReference"
    }
    if ([string]$manifest.dependencies.'com.unity.ugui' -ne '2.0.0') {
        throw "Unexpected uGUI pin: $($manifest.dependencies.'com.unity.ugui')"
    }
    $unexpected = @($properties | Where-Object {
        $_.Name -ne 'com.notnull92.hera-agent-unity' -and
        $_.Name -ne 'com.unity.ugui' -and
        -not $_.Name.StartsWith('com.unity.modules.', [StringComparison]::Ordinal)
    })
    if ($unexpected.Count -ne 0) {
        throw "Minimal fixture contains unrelated registry dependencies: $($unexpected.Name -join ', ')"
    }
    if ($properties.Count -ne [int]$created.manifest_dependency_count) {
        throw 'Manifest dependency count differs from creation evidence.'
    }

    if (-not (Test-Path -LiteralPath $created.connector_snapshot -PathType Container)) {
        throw "Connector snapshot is missing: $($created.connector_snapshot)"
    }
    foreach ($removed in @('Editor\Tests', 'Editor\TestRunner')) {
        if (Test-Path -LiteralPath (Join-Path $created.connector_snapshot $removed)) {
            throw "Fixture Connector retained excluded package test surface: $removed"
        }
    }

    $scene = Join-Path $destination 'Assets\Scenes\SampleScene.unity'
    $sceneText = Get-Content -LiteralPath $scene -Raw
    if ($sceneText.Contains('GameObject:') -or $sceneText.Contains('m_Script:')) {
        throw 'Minimal fixture Scene retained GameObjects or script references.'
    }
    $resetScene = Join-Path $destination 'Assets\Scenes\__HeraABReset.unity'
    if (-not (Test-Path -LiteralPath $resetScene -PathType Leaf)) {
        throw 'Minimal fixture is missing the live-reset parking Scene.'
    }
    $resetSceneText = Get-Content -LiteralPath $resetScene -Raw
    if ($resetSceneText.Contains('GameObject:') -or $resetSceneText.Contains('m_Script:')) {
        throw 'Live-reset parking Scene retained GameObjects or script references.'
    }

    $recovery = Join-Path $destination 'Temp\__Backupscenes'
    if (Test-Path -LiteralPath $recovery -PathType Container) {
        $count = @(Get-ChildItem -LiteralPath $recovery -File -Recurse -ErrorAction SilentlyContinue).Count
        if ($count -ne 0) { throw "Fresh fixture inherited $count Scene Recovery backup file(s)." }
    }

    Add-Content -LiteralPath $scene -Value '# benchmark smoke mutation'
    $owned = Join-Path $destination 'Assets\HeraBenchmark'
    [IO.Directory]::CreateDirectory($owned) | Out-Null
    [IO.File]::WriteAllText((Join-Path $owned 'sentinel.txt'), 'x', [Text.UTF8Encoding]::new($false))

    $resetText = & pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Reset-Fixture.ps1') -ProjectPath $destination
    if ($LASTEXITCODE -ne 0) { throw "Reset-Fixture failed with exit $LASTEXITCODE" }
    $reset = $resetText | ConvertFrom-Json
    if ($reset.recovery_backup_files -ne 0) { throw 'Reset reported recovery backup files.' }
    if (Test-Path -LiteralPath $owned) { throw 'Reset did not remove benchmark-owned output.' }

    $baselineHash = (Get-FileHash -LiteralPath (Join-Path $destination '.hera-ab\baseline\SampleScene.unity') -Algorithm SHA256).Hash
    $sceneHash = (Get-FileHash -LiteralPath $scene -Algorithm SHA256).Hash
    if ($baselineHash -ne $sceneHash) { throw 'Reset scene hash differs from baseline.' }

    $unmarked = Join-Path ([IO.Path]::GetTempPath()) ('hera-ui-ab-unmarked-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($unmarked) | Out-Null
    try {
        $psi = [Diagnostics.ProcessStartInfo]::new('pwsh')
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.ArgumentList.Add('-NoLogo')
        $psi.ArgumentList.Add('-NoProfile')
        $psi.ArgumentList.Add('-File')
        $psi.ArgumentList.Add((Join-Path $PSScriptRoot 'Reset-Fixture.ps1'))
        $psi.ArgumentList.Add('-ProjectPath')
        $psi.ArgumentList.Add($unmarked)
        $process = [Diagnostics.Process]::Start($psi)
        $process.WaitForExit()
        if ($process.ExitCode -eq 0) { throw 'Unmarked reset unexpectedly succeeded.' }
    }
    finally {
        Remove-Item -LiteralPath $unmarked -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host 'FIXTURE_RESET_PASS'
}
finally {
    if (Test-Path -LiteralPath $destination) {
        $marker = Join-Path $destination '.hera-ui-ab-fixture.json'
        if (Test-Path -LiteralPath $marker -PathType Leaf) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }
        else {
            Write-Warning "Leaving unexpected unmarked fixture path: $destination"
        }
    }
}

exit 0
