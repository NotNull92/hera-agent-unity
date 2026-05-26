$ErrorActionPreference = "Stop"

$repo = "NotNull92/hera-agent"

# Old Money ANSI color palette.
# Use [char]27 instead of `e so this parses on Windows PowerShell 5.1
# (where the `e escape does not exist and would be emitted literally).
$ESC = [char]27
$ansiOk = (-not $env:NO_COLOR) -and `
          ($PSVersionTable.PSVersion.Major -ge 7 -or $Host.UI.SupportsVirtualTerminal)

if ($ansiOk) {
    $Gold     = "$ESC[38;2;201;162;39m"
    $Burgundy = "$ESC[38;2;114;47;55m"
    $Sage     = "$ESC[38;2;85;107;47m"
    $Cream    = "$ESC[38;2;245;241;232m"
    $WarmGray = "$ESC[38;2;139;129;120m"
    $Reset    = "$ESC[0m"
    $Bold     = "$ESC[1m"
} else {
    $Gold = ""; $Burgundy = ""; $Sage = ""; $Cream = ""; $WarmGray = ""; $Reset = ""; $Bold = ""
}

function Write-Step($label, $value) {
    Write-Host "  $($Bold)$($Cream)$($label):$($Reset) $($Gold)$value$($Reset)"
}

function Write-Done($msg) {
    Write-Host "  $($Sage)✓$($Reset) $($Cream)$msg$($Reset)"
}

# Named Write-Fail to avoid shadowing the built-in Write-Error cmdlet.
function Write-Fail($msg) {
    Write-Host "  $($Burgundy)✗$($Reset) $($Cream)$msg$($Reset)"
}

# Install into WindowsApps — Windows 10+ keeps this on the default user PATH,
# so new terminals (and IDEs launched from a fresh shell) pick up hera-agent
# without us touching the PATH registry at all.
$installDir = "$env:LOCALAPPDATA\Microsoft\WindowsApps"
$exe = "$installDir\hera-agent.exe"

Write-Host ""
Write-Host "$($Bold)$($Gold)  ╦ ╦ ╔═╗ ╔═╗ ╔═╗   ╔═╗ ╔═╗ ╔═╗ ╔╗╔ ╔╦╗   ╦   ╔╦╗ ╔╦╗ ╔═╗$($Reset)"
Write-Host "$($Bold)$($Gold)  ╠═╣ ║╣  ╠╦╝ ╠═╣   ╠═╣ ║ ╦ ║╣  ║║║  ║    ║    ║   ║  ║╣ $($Reset)"
Write-Host "$($Bold)$($Gold)  ╩ ╩ ╚═╝ ╩╚═ ╩ ╩   ╩ ╩ ╚═╝ ╚═╝ ╝╚╝  ╩    ╚══ ╚╩╝  ╩  ╚═╝$($Reset)"
Write-Host ""

# Migrate from legacy install location ($LOCALAPPDATA\hera-agent) if present.
$legacyDir = "$env:LOCALAPPDATA\hera-agent"
$legacyExe = "$legacyDir\hera-agent.exe"
if (Test-Path $legacyExe) {
    try {
        Remove-Item -Path $legacyExe -Force -ErrorAction Stop
        Write-Done "Removed legacy binary"
    } catch {
        Write-Host "  $($WarmGray)⚠ Could not remove legacy binary: $_$($Reset)"
    }
}
if ((Test-Path $legacyDir) -and (-not (Get-ChildItem -Path $legacyDir -Force -ErrorAction SilentlyContinue))) {
    try { Remove-Item -Path $legacyDir -Force -ErrorAction Stop } catch { }
}

# Clean any legacy PATH entries pointing at the old install dir
# (single OR double backslash variants from earlier installs).
$legacyNorm = [System.IO.Path]::GetFullPath($legacyDir).TrimEnd('\')
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$entries = if ($userPath) { $userPath -split ';' } else { @() }
$filtered = $entries | Where-Object {
    if (-not $_) { return $false }
    try { return ([System.IO.Path]::GetFullPath($_).TrimEnd('\')) -ne $legacyNorm }
    catch { return $true }
}
$newPath = $filtered -join ';'
if ($newPath -ne $userPath) {
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Done "Cleaned legacy PATH entries"
}

# Download the binary into the canonical WindowsApps location.
$url = "https://github.com/$repo/releases/latest/download/hera-agent-windows-amd64.exe"
Write-Step "Acquiring" "hera-agent for windows/amd64..."
Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
Write-Done "Binary acquired"

Write-Host ""
Write-Host "$($Bold)$($Sage)  ✓ Your instrument has been commissioned.$($Reset)"
Write-Host ""
Write-Step "Established at" $exe
Write-Host ""
Write-Host "  $($Cream)Any NEW terminal or IDE will recognize 'hera-agent' immediately$($Reset)"
Write-Host "  $($WarmGray)(WindowsApps resides on the default user PATH).$($Reset)"
Write-Host ""
Write-Host "  $($Cream)Should an open terminal not yet recognize it, refresh with:$($Reset)"
Write-Host "$($Gold)    `$env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' + [Environment]::GetEnvironmentVariable('Path','Machine')$($Reset)"
Write-Host ""
Write-Host "$($Bold)$($Cream)  Next, instruct your agent to employ it:$($Reset)"
Write-Host "    $($Cream)- Discover: inquire of Claude Code CLI or Codex in any terminal:$($Reset)"
Write-Host "$($Gold)        'Verify that hera-agent is installed and survey its capabilities.'$($Reset)"
Write-Host "    $($Cream)- Commission (recommended): add to your project's CLAUDE.md / AGENTS.md:$($Reset)"
Write-Host "$($Gold)        'For all Unity endeavours, employ hera-agent.'$($Reset)"
Write-Host ""

& $exe version
