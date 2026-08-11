$ErrorActionPreference = 'Stop'

$runner = (Resolve-Path (Join-Path $PSScriptRoot 'Run-One.ps1')).Path
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
if ($errors.Count -ne 0) { throw "Run-One parse failed: $($errors[0].Message)" }
$definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-External' }, $true)
if ($null -eq $definition) { throw 'Run-One Invoke-External function not found.' }
. ([scriptblock]::Create($definition.Extent.Text))

$prompt = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\benchmarks\ui-doc-ab\tasks\T03-crystal-forge-reference.md') -Raw
$child = @'
$stream = [Console]::OpenStandardInput()
$memory = [IO.MemoryStream]::new()
$stream.CopyTo($memory)
$bytes = $memory.ToArray()
$strict = [Text.UTF8Encoding]::new($false,$true)
try {
    [void]$strict.GetString($bytes)
    $utf8Valid = $true
}
catch {
    $utf8Valid = $false
}
$hex = [Convert]::ToHexString($bytes)
[pscustomobject]@{
    utf8_valid = $utf8Valid
    has_diamond = $hex.Contains('E29786')
    has_middle_dot = $hex.Contains('C2B7')
} | ConvertTo-Json -Compress
'@
$encodedChild = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($child))
$probe = Invoke-External -FileName 'pwsh' -Arguments @('-NoLogo','-NoProfile','-EncodedCommand',$encodedChild) -StandardInput $prompt
if ($probe.exit_code -ne 0) { throw "UTF-8 probe child failed: $($probe.stderr)" }
$result = $probe.stdout.Trim() | ConvertFrom-Json
if (-not [bool]$result.utf8_valid -or -not [bool]$result.has_diamond -or -not [bool]$result.has_middle_dot) {
    throw "Run-One must deliver the T03 prompt as UTF-8. valid=$($result.utf8_valid) diamond=$($result.has_diamond) middle_dot=$($result.has_middle_dot)"
}

Write-Host 'RUN_ONE_UTF8_PASS'
