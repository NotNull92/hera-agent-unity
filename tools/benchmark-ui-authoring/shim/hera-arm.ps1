param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ForwardArgs
)

$ErrorActionPreference = 'Stop'

$arm = $env:HERA_AB_ARM
$realCli = $env:HERA_AB_REAL_CLI
$logPath = $env:HERA_AB_LOG

if ($arm -notin @('uidoc', 'primitives', 'primitives_batch')) {
    [Console]::Error.WriteLine("HERA_AB_INVALID_ARM: expected uidoc, primitives, or primitives_batch")
    exit 78
}
if ([string]::IsNullOrWhiteSpace($realCli) -or -not (Test-Path -LiteralPath $realCli -PathType Leaf)) {
    [Console]::Error.WriteLine("HERA_AB_REAL_CLI_MISSING: set HERA_AB_REAL_CLI to the pinned Hera executable")
    exit 78
}
if ([string]::IsNullOrWhiteSpace($logPath)) {
    [Console]::Error.WriteLine("HERA_AB_LOG_MISSING: set HERA_AB_LOG to the run JSONL path")
    exit 78
}

$globalValueFlags = @('--port', '--project', '--timeout')

function Find-TopLevelCommand([string[]]$argv) {
    for ($i = 0; $i -lt $argv.Count; $i++) {
        $token = [string]$argv[$i]
        if ($token.StartsWith('-', [StringComparison]::Ordinal)) {
            if ($globalValueFlags -contains $token) { $i++ }
            continue
        }
        return [pscustomobject]@{ Name = $token; Index = $i }
    }
    return $null
}

function Next-Token([string[]]$argv, [int]$index) {
    if ($index + 1 -ge $argv.Count) { return $null }
    return $argv[$index + 1]
}

function Read-BatchDocument([string[]]$argv, [int]$commandIndex) {
    for ($i = $commandIndex + 1; $i -lt $argv.Count; $i++) {
        if ($argv[$i] -eq '--file') {
            if ($i + 1 -ge $argv.Count) {
                return [pscustomobject]@{ Error = '--file requires a path'; Document = $null }
            }
            $path = $argv[$i + 1]
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                return [pscustomobject]@{ Error = "batch file not found: $path"; Document = $null }
            }
            try {
                return [pscustomobject]@{ Error = $null; Document = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json) }
            }
            catch {
                return [pscustomobject]@{ Error = "invalid batch JSON: $($_.Exception.Message)"; Document = $null }
            }
        }
    }
    return [pscustomobject]@{ Error = 'benchmark batch arm requires --file so the shim can inspect the complete plan'; Document = $null }
}

function Test-BatchPolicy($document) {
    if ($null -eq $document -or $null -eq $document.commands) {
        return 'batch JSON needs commands[]'
    }
    $allowedBatchCommands = @('manage_ui', 'manage_components', 'manage_gameobject')
    foreach ($item in $document.commands) {
        $name = [string]$item.command
        if ($name -notin $allowedBatchCommands) {
            return "batch command '$name' is outside the frozen generic UI authoring surface"
        }
    }
    return $null
}

function Read-CallDocument([string[]]$argv, [int]$commandIndex) {
    for ($i = $commandIndex + 2; $i -lt $argv.Count; $i++) {
        if ($argv[$i] -eq '--json') {
            if ($i + 1 -ge $argv.Count) { return $null }
            try { return ($argv[$i + 1] | ConvertFrom-Json) } catch { return $null }
        }
        if ($argv[$i] -eq '--file') {
            if ($i + 1 -ge $argv.Count) { return $null }
            $path = $argv[$i + 1]
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
            try { return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json) } catch { return $null }
        }
    }
    return $null
}

function Evaluate-Policy([string[]]$argv) {
    $found = Find-TopLevelCommand $argv
    if ($null -eq $found) {
        return [pscustomobject]@{ Allowed = $true; Classification = 'other'; Reason = $null }
    }

    $command = $found.Name
    $action = Next-Token $argv $found.Index

    # The runner owns Editor lifecycle, Scene persistence, and user-global
    # configuration. These calls only add noise or can invalidate a run.
    if ($command -eq 'editor') {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'editor lifecycle commands are owned by the benchmark runner' }
    }
    if ($command -eq 'scene' -and $action -in @('load', 'save', 'close')) {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'scene lifecycle mutations are owned by the benchmark runner' }
    }
    if ($command -eq 'asset-config') {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'asset-config is user-global and frozen for the benchmark wave' }
    }
    if ($command -eq 'console' -and ($argv -contains '--clear')) {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'console clear would erase benchmark evidence' }
    }
    if ($command -eq 'menu') {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'menu mutations are outside the frozen UI authoring surface' }
    }
    if ($command.StartsWith('manage_', [StringComparison]::Ordinal) -and $command -notin @('manage_ui', 'manage_components', 'manage_gameobject')) {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = "$command is outside the frozen UI authoring surface" }
    }
    if ($command -eq 'exec') {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'exec is disabled so no arm can bypass its assigned UI authoring surface' }
    }
    if ($command -eq 'html-to-uidoc') {
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'html-to-uidoc is outside the frozen authoring comparison' }
    }
    if ($command -eq 'call') {
        $tool = $action
        if ($tool -eq 'ui_doc') {
            $request = Read-CallDocument $argv $found.Index
            if ($null -ne $request -and [string]$request.action -eq 'capture') {
                return [pscustomobject]@{ Allowed = $true; Classification = 'verification_capture'; Reason = $null }
            }
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'typed call ui_doc is allowed only for neutral action=capture' }
        }
        if ($tool -in @('manage_ui', 'manage_components', 'manage_gameobject', 'exec')) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = "typed call $tool is disabled; use the established command so the shim can enforce the arm" }
        }
        return [pscustomobject]@{ Allowed = $true; Classification = 'verification'; Reason = $null }
    }
    if ($command -eq 'batch') {
        if ($arm -ne 'primitives_batch') {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'batch is only allowed in primitives_batch' }
        }
        $read = Read-BatchDocument $argv $found.Index
        if ($read.Error) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = $read.Error }
        }
        $batchReason = Test-BatchPolicy $read.Document
        if ($batchReason) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = $batchReason }
        }
        return [pscustomobject]@{ Allowed = $true; Classification = 'mutation_batch'; Reason = $null }
    }
    if ($command -eq 'ui_doc') {
        if ($action -eq 'capture') {
            return [pscustomobject]@{ Allowed = $true; Classification = 'verification_capture'; Reason = $null }
        }
        if ($arm -eq 'uidoc' -and $action -in @('apply', 'export')) {
            $kind = if ($action -eq 'apply') { 'mutation' } else { 'verification' }
            return [pscustomobject]@{ Allowed = $true; Classification = $kind; Reason = $null }
        }
        return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = "ui_doc $action is not allowed in arm $arm" }
    }
    if ($command -eq 'manage_ui') {
        $mutation = $action -in @('create', 'set_anchor', 'set_rect')
        if ($arm -eq 'uidoc' -and $mutation) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'manage_ui mutation is disabled in uidoc arm' }
        }
        return [pscustomobject]@{ Allowed = $true; Classification = $(if ($mutation) { 'mutation' } else { 'verification' }); Reason = $null }
    }
    if ($command -eq 'manage_components') {
        $mutation = $action -in @('add', 'remove', 'set')
        if ($arm -eq 'uidoc' -and $mutation) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'manage_components mutation is disabled in uidoc arm' }
        }
        return [pscustomobject]@{ Allowed = $true; Classification = $(if ($mutation) { 'mutation' } else { 'verification' }); Reason = $null }
    }
    if ($command -eq 'manage_gameobject') {
        $mutation = $action -ne 'get_transform'
        if ($arm -eq 'uidoc' -and $mutation) {
            return [pscustomobject]@{ Allowed = $false; Classification = 'forbidden'; Reason = 'manage_gameobject mutation is disabled in uidoc arm' }
        }
        return [pscustomobject]@{ Allowed = $true; Classification = $(if ($mutation) { 'mutation' } else { 'verification' }); Reason = $null }
    }

    return [pscustomobject]@{ Allowed = $true; Classification = 'other'; Reason = $null }
}

function Append-Log($entry) {
    $directory = Split-Path -Parent $logPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    $line = $entry | ConvertTo-Json -Compress -Depth 8
    [IO.File]::AppendAllText($logPath, $line + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

$policy = Evaluate-Policy $ForwardArgs
$started = [DateTimeOffset]::UtcNow
$stopwatch = [Diagnostics.Stopwatch]::StartNew()

if (-not $policy.Allowed) {
    $stopwatch.Stop()
    $entry = [ordered]@{
        schema = 'hera.ui-authoring-ab-call/1'
        started_at = $started.ToString('o')
        arm = $arm
        argv = @($ForwardArgs)
        classification = $policy.Classification
        allowed = $false
        reason = $policy.Reason
        exit_code = 78
        wall_ms = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
        stdout_bytes = 0
        stderr_bytes = [Text.Encoding]::UTF8.GetByteCount($policy.Reason)
    }
    Append-Log $entry
    [Console]::Error.WriteLine("HERA_AB_FORBIDDEN: $($policy.Reason)")
    exit 78
}

$psi = [Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $realCli
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
foreach ($arg in $ForwardArgs) {
    [void]$psi.ArgumentList.Add($arg)
}

$process = [Diagnostics.Process]::new()
$process.StartInfo = $psi
[void]$process.Start()
$outTask = $process.StandardOutput.ReadToEndAsync()
$errTask = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()
$stdout = $outTask.GetAwaiter().GetResult()
$stderr = $errTask.GetAwaiter().GetResult()
$stopwatch.Stop()

if ($stdout.Length -gt 0) { [Console]::Out.Write($stdout) }
if ($stderr.Length -gt 0) { [Console]::Error.Write($stderr) }

$entry = [ordered]@{
    schema = 'hera.ui-authoring-ab-call/1'
    started_at = $started.ToString('o')
    arm = $arm
    argv = @($ForwardArgs)
    classification = $policy.Classification
    allowed = $true
    reason = $null
    exit_code = $process.ExitCode
    wall_ms = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
    stdout_bytes = [Text.Encoding]::UTF8.GetByteCount($stdout)
    stderr_bytes = [Text.Encoding]::UTF8.GetByteCount($stderr)
}
Append-Log $entry
exit $process.ExitCode
