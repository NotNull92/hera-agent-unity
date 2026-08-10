param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$UnityEditorPath
)

$ErrorActionPreference = "Stop"
$project = (Resolve-Path $ProjectPath).Path
$repository = (Resolve-Path $RepositoryRoot).Path
$versionFile = Join-Path $project "ProjectSettings\ProjectVersion.txt"
$versionText = Get-Content -LiteralPath $versionFile -Raw
if ($versionText -notmatch "m_EditorVersion:\s*([^\r\n]+)") {
    throw "Cannot read m_EditorVersion from $versionFile"
}
$unityVersion = $Matches[1].Trim()
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $UnityEditorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
}
$editorDirectory = Split-Path -Parent (Resolve-Path $UnityEditorPath).Path
$editorData = Join-Path $editorDirectory "Data"
$compiler = Join-Path $editorData "DotNetSdkRoslyn\csc.dll"
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Get-ChildItem -LiteralPath (Join-Path $editorData "DotNetSdk") -Filter csc.dll -File -Recurse |
        Sort-Object { $_.FullName.Length } |
        Select-Object -First 1 -ExpandProperty FullName
}
$dotnet = Join-Path $editorData "DotNetSdk\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    $dotnet = Join-Path $editorData "NetCoreRuntime\dotnet.exe"
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf) -or -not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "Cannot resolve Unity compiler runtime under $editorData"
}

function Find-ResponseFile([string]$name) {
    $path = Get-ChildItem -LiteralPath (Join-Path $project "Library\Bee") -Filter $name -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw "Cannot find $name under $project\Library\Bee"
    }
    return $path
}

function Write-ResponseFile([string]$path, [string[]]$lines) {
    [IO.File]::WriteAllLines($path, $lines, [Text.UTF8Encoding]::new($false))
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) ("hera-exact-source-" + [Guid]::NewGuid().ToString("N"))
[IO.Directory]::CreateDirectory($temporary) | Out-Null
try {
    $editorOutput = Join-Path $temporary "HeraAgent.Editor.dll"
    $editorReference = Join-Path $temporary "HeraAgent.Editor.ref.dll"
    $editorLines = Get-Content -LiteralPath (Find-ResponseFile "HeraAgent.Editor.rsp") |
        Where-Object { $_ -notmatch '^\s*".+\.cs"\s*$' } |
        ForEach-Object {
            if ($_ -match '^-out:') { '-out:"' + $editorOutput + '"' }
            elseif ($_ -match '^-refout:') { '-refout:"' + $editorReference + '"' }
            else { $_ }
        }
    $editorSources = Get-ChildItem -LiteralPath (Join-Path $repository "AgentConnector\Editor") -Filter *.cs -File -Recurse |
        Where-Object { $_.FullName -notmatch '[/\\](TestRunner|Tests)[/\\]' } |
        Sort-Object FullName |
        ForEach-Object { '"' + $_.FullName + '"' }
    $editorResponse = Join-Path $temporary "HeraAgent.Editor.rsp"
    Write-ResponseFile $editorResponse @($editorLines + $editorSources)

    Push-Location $project
    try {
        $editorDiagnostics = @(& $dotnet $compiler "@$editorResponse" 2>&1)
        $editorDiagnostics | ForEach-Object { Write-Output $_ }
        if ($LASTEXITCODE -ne 0 -or $editorDiagnostics -match '\b(?:warning|error) CS\d+') {
            throw "HeraAgent.Editor exact-source compile failed or produced diagnostics"
        }

        $testOutput = Join-Path $temporary "HeraAgent.TestRunner.dll"
        $testReference = Join-Path $temporary "HeraAgent.TestRunner.ref.dll"
        $testLines = Get-Content -LiteralPath (Find-ResponseFile "HeraAgent.TestRunner.rsp") |
        Where-Object { $_ -notmatch '^\s*".+\.cs"\s*$' } |
            ForEach-Object {
                if ($_ -match '^-out:') { '-out:"' + $testOutput + '"' }
                elseif ($_ -match '^-refout:') { '-refout:"' + $testReference + '"' }
                elseif ($_ -match '^-r:".*HeraAgent\.Editor\.ref\.dll"$') { '-r:"' + $editorReference + '"' }
                else { $_ }
            }
        $testSources = Get-ChildItem -LiteralPath (Join-Path $repository "AgentConnector\Editor\TestRunner") -Filter *.cs -File -Recurse |
            Sort-Object FullName |
            ForEach-Object { '"' + $_.FullName + '"' }
        $testResponse = Join-Path $temporary "HeraAgent.TestRunner.rsp"
        Write-ResponseFile $testResponse @($testLines + $testSources)
        $testDiagnostics = @(& $dotnet $compiler "@$testResponse" 2>&1)
        $testDiagnostics | ForEach-Object { Write-Output $_ }
        if ($LASTEXITCODE -ne 0 -or $testDiagnostics -match '\b(?:warning|error) CS\d+') {
            throw "HeraAgent.TestRunner exact-source compile failed or produced diagnostics"
        }
    }
    finally {
        Pop-Location
    }
    Write-Output "PASS $unityVersion $project"
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if (-not $resolvedTemporary.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected temporary path: $resolvedTemporary"
    }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force -ErrorAction SilentlyContinue
}
