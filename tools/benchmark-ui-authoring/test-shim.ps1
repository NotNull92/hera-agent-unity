$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$shim = (Resolve-Path (Join-Path $PSScriptRoot 'shim\hera-agent-unity.cmd')).Path
$real = (Get-Command hera-agent-unity -ErrorAction Stop).Source
$temp = Join-Path ([IO.Path]::GetTempPath()) ('hera-ab-shim-test-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null

function Invoke-Case {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$ExpectedExitCode
    )

    Write-Host "=== $Label ==="
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & $shim @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference

    if ($output) {
        $output | ForEach-Object { Write-Host $_ }
    }
    Write-Host "exit=$exitCode expected=$ExpectedExitCode"
    if ($exitCode -ne $ExpectedExitCode) {
        throw "$Label expected exit $ExpectedExitCode, got $exitCode"
    }
}

try {
    $env:HERA_AB_REAL_CLI = $real
    $env:HERA_AB_LOG = Join-Path $temp 'calls.jsonl'

    $env:HERA_AB_ARM = 'uidoc'
    Invoke-Case -Label 'allow version' -Arguments @('version') -ExpectedExitCode 0
    Invoke-Case -Label 'allow list describing ui_doc' -Arguments @('list', '--tool', 'ui_doc') -ExpectedExitCode 0
    Invoke-Case -Label 'block uidoc manage_ui mutation' -Arguments @('manage_ui', 'create', '--element', 'button', '--name', 'X') -ExpectedExitCode 78
    Invoke-Case -Label 'block editor lifecycle' -Arguments @('editor', 'restart') -ExpectedExitCode 78
    Invoke-Case -Label 'block user global asset config' -Arguments @('asset-config', 'ui-system', 'uitk') -ExpectedExitCode 78

    $env:HERA_AB_ARM = 'primitives'
    Invoke-Case -Label 'block primitives ui_doc apply' -Arguments @('ui_doc', 'apply', '--file', 'nowhere.json') -ExpectedExitCode 78
    Invoke-Case -Label 'block primitives batch' -Arguments @('batch', '--file', 'nowhere.json') -ExpectedExitCode 78
    Invoke-Case -Label 'block scene lifecycle mutation' -Arguments @('scene', 'save') -ExpectedExitCode 78

    $env:HERA_AB_ARM = 'primitives_batch'
    $badBatch = Join-Path $temp 'bad-batch.json'
    [IO.File]::WriteAllText(
        $badBatch,
        '{"commands":[{"command":"ui_doc","params":{"action":"apply","doc":{}}}]}',
        [Text.UTF8Encoding]::new($false))
    Invoke-Case -Label 'block nested ui_doc in batch' -Arguments @('batch', '--file', $badBatch) -ExpectedExitCode 78

    $offSurfaceBatch = Join-Path $temp 'off-surface-batch.json'
    [IO.File]::WriteAllText(
        $offSurfaceBatch,
        '{"commands":[{"command":"manage_assets","params":{"action":"find","query":"x"}}]}',
        [Text.UTF8Encoding]::new($false))
    Invoke-Case -Label 'block off-surface batch command' -Arguments @('batch', '--file', $offSurfaceBatch) -ExpectedExitCode 78

    $rows = @(Get-Content -LiteralPath $env:HERA_AB_LOG | ForEach-Object { $_ | ConvertFrom-Json })
    if ($rows.Count -ne 10) {
        throw "expected 10 call logs, got $($rows.Count)"
    }
    if (@($rows | Where-Object { $_.allowed -eq $false }).Count -ne 8) {
        throw 'expected eight forbidden call logs'
    }
    if (@($rows | Where-Object { $_.allowed -eq $true }).Count -ne 2) {
        throw 'expected two allowed call logs'
    }

    Write-Host 'SHIM_POLICY_PASS'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:HERA_AB_REAL_CLI, Env:HERA_AB_LOG, Env:HERA_AB_ARM -ErrorAction SilentlyContinue
}

exit 0
