param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OraclePath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$HeraCli,
    [string]$ProbePath = (Join-Path $PSScriptRoot 'probe.cs')
)

$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$oracleFile = (Resolve-Path -LiteralPath $OraclePath).Path
$probe = (Resolve-Path -LiteralPath $ProbePath).Path
$cli = (Resolve-Path -LiteralPath $HeraCli).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$tracePath = Join-Path $output 'scorer-trace.log'
function Trace([string]$Message) {
    [IO.File]::AppendAllText($tracePath,([DateTimeOffset]::UtcNow.ToString('o') + ' ' + $Message + [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
}
Trace 'start'

$markerPath = Join-Path $project '.hera-ui-ab-fixture.json'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw "Scoring is restricted to a marked benchmark fixture: $project"
}
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($marker.schema -ne 'hera.ui-authoring-ab-fixture/1') {
    throw "Unexpected fixture marker schema: $($marker.schema)"
}

$oracle = Get-Content -LiteralPath $oracleFile -Raw | ConvertFrom-Json
if ($oracle.schema -ne 'hera.ui-authoring-ab-oracle/1') {
    throw "Unexpected oracle schema: $($oracle.schema)"
}

function Invoke-External {
    param([string]$FileName, [string[]]$Arguments)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    foreach ($argument in $Arguments) { [void]$psi.ArgumentList.Add($argument) }
    $psi.Environment['HERA_AGENT_NO_PATH_CHECK'] = '1'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [void]$process.Start()
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    $watch.Stop()
    return [pscustomobject]@{
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
        wall_ms = [Math]::Round($watch.Elapsed.TotalMilliseconds, 3)
    }
}

function Parse-JsonText {
    param([string]$Text)
    $trimmed = $(if ($null -eq $Text) { '' } else { $Text }).Trim()
    if ($trimmed.Length -eq 0) { return $null }
    try { return $trimmed | ConvertFrom-Json }
    catch { }
    $lines = @($trimmed -split "`r?`n")
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        $line = $lines[$i].Trim()
        if ($line.Length -eq 0) { continue }
        try { return $line | ConvertFrom-Json }
        catch { }
    }
    return $null
}

function Invoke-HeraData {
    param([string[]]$CommandArguments)
    $base = @('--project', $project, '--compact-json') + $CommandArguments
    $first = Invoke-External -FileName $cli -Arguments $base
    if ($first.exit_code -eq 0) {
        $data = Parse-JsonText $first.stdout
        if ($null -eq $data -and -not [string]::IsNullOrWhiteSpace($first.stdout)) {
            return [pscustomobject]@{ data = $first.stdout.Trim(); first = $first; approved = $false; second = $null }
        }
        return [pscustomobject]@{ data = $data; first = $first; approved = $false; second = $null }
    }

    $errorEnvelope = Parse-JsonText $first.stderr
    if ($null -eq $errorEnvelope -or $errorEnvelope.code -ne 'APPROVAL_REQUIRED') {
        throw "Hera command failed. args=$($CommandArguments -join ' ') exit=$($first.exit_code) stderr=$($first.stderr.Trim())"
    }
    $token = [string]$errorEnvelope.data.token
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Approval response did not contain data.token: $($first.stderr.Trim())"
    }
    $secondArgs = $base + @('--approve', $token)
    $second = Invoke-External -FileName $cli -Arguments $secondArgs
    if ($second.exit_code -ne 0) {
        throw "Approved Hera command failed. args=$($CommandArguments -join ' ') exit=$($second.exit_code) stderr=$($second.stderr.Trim())"
    }
    $data = Parse-JsonText $second.stdout
    if ($null -eq $data -and -not [string]::IsNullOrWhiteSpace($second.stdout)) {
        $data = $second.stdout.Trim()
    }
    return [pscustomobject]@{ data = $data; first = $first; approved = $true; second = $second }
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    return @($value)
}

Trace 'probe-begin'
$probeInvocation = Invoke-HeraData @('exec', '--file', $probe, '--depth', '8')
Trace 'probe-complete'
$snapshot = $probeInvocation.data
if ($null -eq $snapshot -or $null -eq $snapshot.nodes) {
    throw 'Probe did not return a scene snapshot.'
}

$projectScoreDirectory = Join-Path $project '.hera-ab\score'
[IO.Directory]::CreateDirectory($projectScoreDirectory) | Out-Null
$captureTemp = Join-Path $projectScoreDirectory ('capture-' + [Guid]::NewGuid().ToString('N') + '.png')
Trace 'capture-begin'
$captureInvocation = Invoke-HeraData @('ui_doc', 'capture', '--width', '1280', '--height', '720', '--out', $captureTemp)
Trace 'capture-complete'
if (-not (Test-Path -LiteralPath $captureTemp -PathType Leaf)) {
    throw "Visual verifier did not create capture: $captureTemp"
}
$finalCapture = Join-Path $output 'final-capture.png'
Copy-Item -LiteralPath $captureTemp -Destination $finalCapture -Force
Remove-Item -LiteralPath $captureTemp -Force -ErrorAction SilentlyContinue

Trace 'console-begin'
$consoleInvocation = Invoke-HeraData @('console', '--type', 'error', '--lines', '1')
Trace 'console-complete'
$consoleData = $consoleInvocation.data
$consoleErrorCount = 0
if ($null -eq $consoleData) {
    $consoleErrorCount = 0
}
elseif ($consoleData -is [System.Array]) {
    $consoleErrorCount = @($consoleData).Count
}
elseif ($null -ne $consoleData.entries) {
    $consoleErrorCount = @(As-Array $consoleData.entries).Count
}
elseif ($null -ne $consoleData.logs) {
    $consoleErrorCount = @(As-Array $consoleData.logs).Count
}
elseif ($null -ne $consoleData.count) {
    $consoleErrorCount = [int]$consoleData.count
}
else {
    $consoleErrorCount = 1
}

Trace 'write-snapshot-begin'
$snapshot | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'final-state.json') -Encoding utf8
$consoleData | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'console-errors.json') -Encoding utf8
Trace 'write-snapshot-complete'

function Node-At([string]$path) {
    return @($snapshot.nodes | Where-Object { $_.path -eq $path }) | Select-Object -First 1
}
function Raycast-At([string]$path) {
    return @($snapshot.raycasts | Where-Object { $_.path -eq $path }) | Select-Object -First 1
}
function Approx([double]$a, [double]$b, [double]$tolerance) {
    return [Math]::Abs($a - $b) -le $tolerance
}
function Vec-Matches($actual, $expected, [double]$tolerance) {
    if ($null -eq $actual -or $null -eq $expected) { return $false }
    return (Approx ([double]$actual.x) ([double]$expected[0]) $tolerance) -and
           (Approx ([double]$actual.y) ([double]$expected[1]) $tolerance)
}
function Size-Matches($actualRect, $expected, [double]$tolerance) {
    if ($null -eq $actualRect -or $null -eq $actualRect.size) { return $false }
    return (Approx ([double]$actualRect.size.width) ([double]$expected[0]) $tolerance) -and
           (Approx ([double]$actualRect.size.height) ([double]$expected[1]) $tolerance)
}
function Expected-Anchor([string]$preset) {
    switch ($preset) {
        'stretch' { return @(@(0.0,0.0), @(1.0,1.0)) }
        'top-center' { return @(@(0.5,1.0), @(0.5,1.0)) }
        'middle-center' { return @(@(0.5,0.5), @(0.5,0.5)) }
        'middle-left' { return @(@(0.0,0.5), @(0.0,0.5)) }
        default { return $null }
    }
}
function Anchor-Matches($actualRect, [string]$preset, [double]$tolerance = 0.0001) {
    $expected = Expected-Anchor $preset
    if ($null -eq $expected -or $null -eq $actualRect) { return $false }
    return (Vec-Matches $actualRect.anchor_min $expected[0] $tolerance) -and
           (Vec-Matches $actualRect.anchor_max $expected[1] $tolerance)
}
function Hex-ToBytes([string]$hex) {
    $value = $hex.TrimStart('#')
    if ($value.Length -eq 6) { $value += 'FF' }
    if ($value.Length -ne 8) { throw "Unsupported color literal: $hex" }
    return @(0,2,4,6 | ForEach-Object { [Convert]::ToInt32($value.Substring($_,2),16) })
}
function Color-Matches($actual, [string]$expectedHex, [int]$tolerance) {
    if ($null -eq $actual) { return $false }
    $expected = Hex-ToBytes $expectedHex
    $actualBytes = @(
        [Math]::Round([double]$actual.r * 255),
        [Math]::Round([double]$actual.g * 255),
        [Math]::Round([double]$actual.b * 255),
        [Math]::Round([double]$actual.a * 255)
    )
    for ($i=0; $i -lt 4; $i++) {
        if ([Math]::Abs([double]$actualBytes[$i] - [double]$expected[$i]) -gt $tolerance) { return $false }
    }
    return $true
}
function Component-Present($node, [string]$component) {
    if ($null -eq $node) { return $false }
    switch ($component) {
        'Canvas' { return $null -ne $node.canvas }
        'Image' { return $null -ne $node.image }
        'Text' { return $null -ne $node.text }
        'Button' { return $null -ne $node.button }
        default { return $false }
    }
}
function Text-Matches($text, $check, [int]$fontTolerance, [int]$colorTolerance) {
    if ($null -eq $text) { return 0.0 }
    $tests = New-Object System.Collections.Generic.List[bool]
    if ($null -ne $check.value) { $tests.Add(([string]$text.value -ceq [string]$check.value)) }
    if ($null -ne $check.font_size) { $tests.Add((Approx ([double]$text.font_size) ([double]$check.font_size) $fontTolerance)) }
    if ($null -ne $check.color) { $tests.Add((Color-Matches $text.color ([string]$check.color) $colorTolerance)) }
    if ($null -ne $check.alignment) { $tests.Add(([string]$text.alignment).ToLowerInvariant().Contains(([string]$check.alignment).ToLowerInvariant())) }
    $tests.Add([bool]$text.visibly_configured)
    if ($tests.Count -eq 0) { return 1.0 }
    return (@($tests | Where-Object { $_ }).Count / [double]$tests.Count)
}
function Button-Visual-Fraction($node, $check, [int]$fontTolerance, [int]$colorTolerance) {
    if ($null -eq $node -or $null -eq $node.button) { return 0.0 }
    $tests = @(
        (Color-Matches $node.image.color ([string]$check.image_rgba) $colorTolerance),
        ([string]$node.button.label -ceq [string]$check.label),
        (Approx ([double]$node.button.label_font_size) ([double]$check.label_font_size) $fontTolerance),
        (Color-Matches $node.button.label_color ([string]$check.label_rgba) $colorTolerance),
        [bool]$node.button.label_visible
    )
    return (@($tests | Where-Object { $_ }).Count / [double]$tests.Count)
}
function Compare-Capture([string]$actualPath, [string]$referencePath) {
    Add-Type -AssemblyName System.Drawing
    $actual = [System.Drawing.Bitmap]::FromFile($actualPath)
    $reference = [System.Drawing.Bitmap]::FromFile($referencePath)
    try {
        if ($actual.Width -ne $reference.Width -or $actual.Height -ne $reference.Height) { return 1.0 }
        [double]$sum = 0
        [long]$samples = 0
        $step = 8
        for ($y=0; $y -lt $actual.Height; $y += $step) {
            for ($x=0; $x -lt $actual.Width; $x += $step) {
                $a=$actual.GetPixel($x,$y); $r=$reference.GetPixel($x,$y)
                $sum += [Math]::Abs($a.R-$r.R)+[Math]::Abs($a.G-$r.G)+[Math]::Abs($a.B-$r.B)
                $samples += 3
            }
        }
        $mae = if ($samples -eq 0) { 255.0 } else { $sum / $samples }
        if ($mae -le 20) { $fraction=1.0 }
        elseif ($mae -le 35) { $fraction=0.75 }
        elseif ($mae -le 50) { $fraction=0.50 }
        elseif ($mae -le 70) { $fraction=0.25 }
        else { $fraction=0.0 }
        return [pscustomobject]@{ fraction=$fraction; rgb_mae=[Math]::Round($mae,3) }
    }
    finally { $actual.Dispose(); $reference.Dispose() }
}

$positionTolerance = [double]$oracle.tolerances.position_px
$sizeTolerance = [double]$oracle.tolerances.size_px
$colorTolerance = [int]$oracle.tolerances.color_channel_8bit
$fontTolerance = [int]$oracle.tolerances.font_size
$checkResults = New-Object System.Collections.Generic.List[object]

Trace 'checks-begin'
foreach ($check in $oracle.checks) {
    Trace ('check ' + [string]$check.id)
    [double]$fraction = 0.0
    $evidence = $null
    switch ([string]$check.kind) {
        'object_component' {
            $node = Node-At $check.path
            $fraction = if (Component-Present $node $check.component) { 1.0 } else { 0.0 }
        }
        'object_exists' {
            $fraction = if ($null -ne (Node-At $check.path)) { 1.0 } else { 0.0 }
        }
        'required_text_objects' {
            $items=@($check.paths); $passed=0
            foreach($path in $items){$node=Node-At $path; if($null -ne $node -and $null -ne $node.text){$passed++}}
            $fraction = if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'required_button_objects' {
            $items=@($check.paths); $passed=0
            foreach($path in $items){$node=Node-At $path; if($null -ne $node -and $null -ne $node.button){$passed++}}
            $fraction = if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'button_labels_present' {
            $items=@($check.paths); $passed=0
            foreach($path in $items){$node=Node-At $path; if($null -ne $node.button -and -not [string]::IsNullOrWhiteSpace([string]$node.button.label) -and [bool]$node.button.label_visible){$passed++}}
            $fraction = if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'numbered_children' {
            $total=[int]$check.last-[int]$check.first+1; $passed=0
            for($i=[int]$check.first;$i -le [int]$check.last;$i++){$name='{0}{1:D2}' -f $check.prefix,$i;$node=Node-At ($check.parent+'/'+$name);if(Component-Present $node $check.component){$passed++}}
            $fraction=$passed/[double]$total
        }
        'numbered_child_labels' {
            $total=[int]$check.last-[int]$check.first+1; $passed=0
            for($i=[int]$check.first;$i -le [int]$check.last;$i++){$slot='{0}{1:D2}' -f $check.slot_prefix,$i;$node=Node-At ($check.parent+'/'+$slot+'/'+$check.label_name);if(Component-Present $node $check.component){$passed++}}
            $fraction=$passed/[double]$total
        }
        'canvas_scaler' {
            $node=Node-At $check.path; $tests=@()
            $tests += ($null -ne $node.scaler)
            if($null -ne $node.scaler){$tests += ([string]$node.scaler.scale_mode -eq [string]$check.scale_mode);$tests += (Vec-Matches $node.scaler.reference_resolution $check.reference_resolution 0.01);$tests += (Approx ([double]$node.scaler.match) ([double]$check.match) 0.001)}
            $fraction=@($tests|Where-Object{$_}).Count/[double]$tests.Count
        }
        'rect' {
            $node=Node-At $check.path; $tests=New-Object System.Collections.Generic.List[bool]
            if($null -eq $node -or $null -eq $node.rect){$fraction=0.0;break}
            if($null -ne $check.anchor){$tests.Add((Anchor-Matches $node.rect ([string]$check.anchor)))}
            if($null -ne $check.pivot){$tests.Add((Vec-Matches $node.rect.pivot $check.pivot 0.001))}
            if($null -ne $check.anchored_position){$tests.Add((Vec-Matches $node.rect.anchored_position $check.anchored_position $positionTolerance))}
            if($null -ne $check.size){$tests.Add((Size-Matches $node.rect $check.size $sizeTolerance))}
            $fraction=if($tests.Count){@($tests|Where-Object{$_}).Count/[double]$tests.Count}else{1.0}
        }
        'multi_rect_centers' {
            $items=@($check.items);$passed=0
            foreach($item in $items){$node=Node-At $item.path;if($null -ne $node -and (Vec-Matches $node.rect.anchored_position $item.center $positionTolerance)){$passed++}}
            $fraction=if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'numbered_rects' {
            $total=[int]$check.last-[int]$check.first+1;$passedUnits=0;$units=$total*2
            for($i=[int]$check.first;$i -le [int]$check.last;$i++){$name='{0}{1:D2}' -f $check.prefix,$i;$node=Node-At ($check.parent+'/'+$name);$index=$i-[int]$check.first;if($null -ne $node){if(Size-Matches $node.rect $check.size $sizeTolerance){$passedUnits++};if(Vec-Matches $node.rect.anchored_position $check.centers[$index] $positionTolerance){$passedUnits++}}}
            $fraction=$passedUnits/[double]$units
        }
        'image_color' {
            $node=Node-At $check.path;$fraction=if($null -ne $node.image -and (Color-Matches $node.image.color $check.rgba $colorTolerance)){1.0}else{0.0}
        }
        'text' {
            $node=Node-At $check.path;$fraction=Text-Matches $node.text $check $fontTolerance $colorTolerance
        }
        'multi_image_color' {
            $items=@($check.items);$passed=0;foreach($item in $items){$node=Node-At $item.path;if($null -ne $node.image -and (Color-Matches $node.image.color $item.rgba $colorTolerance)){$passed++}};$fraction=if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'button_visual' {
            $fraction=Button-Visual-Fraction (Node-At $check.path) $check $fontTolerance $colorTolerance
        }
        'numbered_slot_visuals' {
            $total=[int]$check.last-[int]$check.first+1;$passed=0;$units=$total*5
            for($i=[int]$check.first;$i -le [int]$check.last;$i++){$name='{0}{1:D2}' -f $check.prefix,$i;$slot=Node-At ($check.parent+'/'+$name);$label=Node-At ($check.parent+'/'+$name+'/'+$check.label_name);$idx=$i-[int]$check.first;if($null -ne $slot.image -and (Color-Matches $slot.image.color $check.image_rgba $colorTolerance)){$passed++};if($null -ne $label.text){if([string]$label.text.value -ceq [string]$check.label_values[$idx]){$passed++};if(Approx ([double]$label.text.font_size) ([double]$check.label_font_size) $fontTolerance){$passed++};if(Color-Matches $label.text.color $check.label_rgba $colorTolerance){$passed++};if([bool]$label.text.visibly_configured){$passed++}}};$fraction=$passed/[double]$units
        }
        'reference_colors' {
            $items=@($check.items);$passed=0;foreach($item in $items){$node=Node-At $item.path;if($null -ne $node.image -and (Color-Matches $node.image.color $item.rgba $colorTolerance)){$passed++}};$fraction=if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'text_values_visible' {
            $items=@($check.items);$passed=0
            foreach($item in $items){$node=Node-At $item.path;if($null -eq $node){continue};if($null -ne $node.text){if([string]$node.text.value -ceq [string]$item.value -and [bool]$node.text.visibly_configured){$passed++}}elseif($null -ne $node.button){if([string]$node.button.label -ceq [string]$item.value -and [bool]$node.button.label_visible){$passed++}}}
            $fraction=if($items.Count){$passed/[double]$items.Count}else{1.0}
        }
        'capture_composition' {
            $reference=(Resolve-Path -LiteralPath (Join-Path $repository $check.reference)).Path
            $comparison=Compare-Capture $finalCapture $reference
            $fraction=[double]$comparison.fraction;$evidence=$comparison
        }
        'event_system' {
            $fraction=if([int]$snapshot.event_system_count -gt 0){1.0}else{0.0}
        }
        'raycast_top' {
            $ray=Raycast-At $check.path;$fraction=if($null -ne $ray -and [bool]$ray.reachable){1.0}else{0.0};$evidence=$ray
        }
        'raycast_top_all' {
            $items=@($check.paths);$passed=0;$details=@();foreach($path in $items){$ray=Raycast-At $path;$details+=$ray;if($null -ne $ray -and [bool]$ray.reachable){$passed++}};$fraction=if($items.Count){$passed/[double]$items.Count}else{1.0};$evidence=$details
        }
        'root_canvas_count' {
            $fraction=if([int]$snapshot.root_canvas_count -eq [int]$check.expected){1.0}else{0.0};$evidence=[int]$snapshot.root_canvas_count
        }
        'unique_names' {
            $names=@($check.names);$passed=0;foreach($name in $names){if(@($snapshot.nodes|Where-Object{$_.name -ceq [string]$name}).Count -eq 1){$passed++}};$fraction=if($names.Count){$passed/[double]$names.Count}else{1.0}
        }
        'child_name_count' {
            $matches=@($snapshot.nodes|Where-Object{$_.parent -eq [string]$check.parent -and $_.name.StartsWith([string]$check.prefix,[StringComparison]::Ordinal)});$good=$matches.Count -eq [int]$check.expected;if($good -and [bool]$check.unique){$good=(@($matches.name|Sort-Object -Unique).Count -eq $matches.Count)};$fraction=if($good){1.0}else{0.0};$evidence=$matches.name
        }
        'visible_root_whitelist' {
            $allowed=@($check.allowed);$unexpected=@($snapshot.root_names|Where-Object{$allowed -notcontains [string]$_});$fraction=if($unexpected.Count -eq 0){1.0}else{0.0};$evidence=$unexpected
        }
        'console_errors' {
            $fraction=if($consoleErrorCount -eq [int]$check.expected){1.0}else{0.0};$evidence=$consoleErrorCount
        }
        default { throw "Unknown oracle check kind: $($check.kind)" }
    }
    if($fraction -lt 0){$fraction=0};if($fraction -gt 1){$fraction=1}
    $earned=[Math]::Round([double]$check.points*$fraction,3)
    $checkResults.Add([pscustomobject]@{id=[string]$check.id;category=[string]$check.category;kind=[string]$check.kind;points=[double]$check.points;fraction=[Math]::Round($fraction,4);earned=$earned;evidence=$evidence})
}

$scoreMeasure = $checkResults | Measure-Object -Property earned -Sum
$score=[Math]::Round([double]$scoreMeasure.Sum,3)
$criticalFailures=@()
foreach($critical in @($oracle.critical)){$result=$checkResults|Where-Object{$_.id -eq [string]$critical}|Select-Object -First 1;if($null -eq $result){throw "Critical check id is missing from oracle checks: $critical"};if([double]$result.fraction -lt 0.9999){$criticalFailures += [string]$critical}}
$strictPass=$criticalFailures.Count -eq 0
$categories=[ordered]@{}
foreach($category in @('structure','geometry','visual','interaction','cleanliness')){$categoryMeasure=$checkResults|Where-Object{$_.category -eq $category}|Measure-Object -Property earned -Sum;$categories[$category]=[Math]::Round([double]$categoryMeasure.Sum,3)}

$result=[ordered]@{
    schema='hera.ui-authoring-ab-score/1'
    task=[string]$oracle.task
    strict_pass=$strictPass
    accuracy_score=$score
    categories=$categories
    critical_failures=$criticalFailures
    console_error_count=$consoleErrorCount
    capture_path='final-capture.png'
    checks=$checkResults
    probe_approval_used=[bool]$probeInvocation.approved
    capture_approval_used=[bool]$captureInvocation.approved
}
$annotations=[ordered]@{
    schema='hera.ui-authoring-ab-annotations/1'
    root_canvas_count=[int]$snapshot.root_canvas_count
    event_system_count=[int]$snapshot.event_system_count
    raycasts=$snapshot.raycasts
}
[IO.File]::WriteAllText((Join-Path $output 'final-annotations.json'),($annotations|ConvertTo-Json -Depth 12)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
$resultPath=Join-Path $output 'score.json'
[IO.File]::WriteAllText($resultPath,($result|ConvertTo-Json -Depth 16)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output (($result|ConvertTo-Json -Compress -Depth 16))
