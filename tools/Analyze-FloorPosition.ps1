param(
    [string]$ChartRoot,
    [double]$Tolerance = 0.001
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ChartRoot)) {
    $ChartRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '#Samples'
}

$resolvedRoot = (Resolve-Path -LiteralPath $ChartRoot).Path
$files = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter '*.json' | Sort-Object FullName)

function Get-IntegratedFloorPosition {
    param(
        [object]$Canvas,
        [double]$Time,
        [double]$Bpm
    )

    $floorPosition = 0.0
    $previousTime = 0.0
    $previousSpeed = 1.0
    $keyPoints = @($Canvas.speedKeyPoints | Sort-Object { [double]$_.time })

    foreach ($keyPoint in $keyPoints) {
        $keyPointTime = [double]$keyPoint.time
        if ($keyPointTime -gt $Time) {
            break
        }

        $floorPosition += ($keyPointTime - $previousTime) * 60.0 / $Bpm * $previousSpeed
        $previousTime = $keyPointTime
        $previousSpeed = [double]$keyPoint.value
    }

    $floorPosition += ($Time - $previousTime) * 60.0 / $Bpm * $previousSpeed
    return $floorPosition
}

function Get-AnchoredFloorPosition {
    param(
        [object]$Canvas,
        [double]$Time,
        [double]$Bpm
    )

    $anchor = @(
        $Canvas.speedKeyPoints |
            Where-Object { [double]$_.time -le $Time } |
            Sort-Object { [double]$_.time } |
            Select-Object -Last 1
    )

    if ($anchor.Count -eq 0) {
        return $Time * 60.0 / $Bpm
    }

    $keyPoint = $anchor[0]
    return [double]$keyPoint.floorPosition +
        ($Time - [double]$keyPoint.time) * 60.0 / $Bpm * [double]$keyPoint.value
}

$counts = [ordered]@{
    SpeedKeyPointTotal = 0
    SpeedKeyPointMatch = 0
    LinePointTotal     = 0
    LinePointMatch     = 0
    LinePointAnchoredMatch = 0
    HoldEndTotal       = 0
    HoldEndMatch       = 0
    HoldEndAnchoredMatch = 0
    HoldPlainSeconds   = 0
}

$maximumErrors = [ordered]@{
    SpeedKeyPoint = 0.0
    LinePoint     = 0.0
    LinePointAnchored = 0.0
    HoldEnd       = 0.0
    HoldEndAnchored = 0.0
}

$holdMismatches = @()

foreach ($file in $files) {
    $document = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $bpm = [double]$document.bPM
    $canvasMap = @{}

    foreach ($canvas in @($document.canvasMoves)) {
        $canvasMap[[int]$canvas.index] = $canvas

        foreach ($keyPoint in @($canvas.speedKeyPoints)) {
            $predicted = Get-IntegratedFloorPosition -Canvas $canvas -Time ([double]$keyPoint.time) -Bpm $bpm
            $actual = [double]$keyPoint.floorPosition
            $absoluteError = [math]::Abs($predicted - $actual)
            $counts.SpeedKeyPointTotal++
            if ($absoluteError -lt $Tolerance) {
                $counts.SpeedKeyPointMatch++
            }
            if ($absoluteError -gt $maximumErrors.SpeedKeyPoint) {
                $maximumErrors.SpeedKeyPoint = $absoluteError
            }
        }
    }

    for ($lineIndex = 0; $lineIndex -lt @($document.lines).Count; $lineIndex++) {
        $line = $document.lines[$lineIndex]

        foreach ($point in @($line.linePoints)) {
            $canvasIndex = [int]$point.canvasIndex
            if (-not $canvasMap.ContainsKey($canvasIndex)) {
                continue
            }

            $predicted = Get-IntegratedFloorPosition -Canvas $canvasMap[$canvasIndex] -Time ([double]$point.time) -Bpm $bpm
            $actual = [double]$point.floorPosition
            $absoluteError = [math]::Abs($predicted - $actual)
            $counts.LinePointTotal++
            if ($absoluteError -lt $Tolerance) {
                $counts.LinePointMatch++
            }
            if ($absoluteError -gt $maximumErrors.LinePoint) {
                $maximumErrors.LinePoint = $absoluteError
            }

            $anchored = Get-AnchoredFloorPosition -Canvas $canvasMap[$canvasIndex] -Time ([double]$point.time) -Bpm $bpm
            $anchoredError = [math]::Abs($anchored - $actual)
            if ($anchoredError -lt $Tolerance) {
                $counts.LinePointAnchoredMatch++
            }
            if ($anchoredError -gt $maximumErrors.LinePointAnchored) {
                $maximumErrors.LinePointAnchored = $anchoredError
            }
        }

        foreach ($note in @($line.notes)) {
            if ([int]$note.type -ne 2) {
                continue
            }

            $canvasIndex = [int][double]$note.otherInformations[1]
            $endTime = [double]$note.otherInformations[0]
            $actual = [double]$note.otherInformations[2]
            if (-not $canvasMap.ContainsKey($canvasIndex)) {
                continue
            }

            $predicted = Get-IntegratedFloorPosition -Canvas $canvasMap[$canvasIndex] -Time $endTime -Bpm $bpm
            $absoluteError = [math]::Abs($predicted - $actual)
            $counts.HoldEndTotal++
            if ($absoluteError -lt $Tolerance) {
                $counts.HoldEndMatch++
            } elseif ($holdMismatches.Count -lt 20) {
                $holdMismatches += [PSCustomObject]@{
                    File       = $file.FullName.Substring($resolvedRoot.Length + 1)
                    Line       = $lineIndex
                    Canvas     = $canvasIndex
                    EndTime    = $endTime
                    Actual     = $actual
                    Predicted  = $predicted
                    Error      = $absoluteError
                }
            }
            if ($absoluteError -gt $maximumErrors.HoldEnd) {
                $maximumErrors.HoldEnd = $absoluteError
            }

            $anchored = Get-AnchoredFloorPosition -Canvas $canvasMap[$canvasIndex] -Time $endTime -Bpm $bpm
            $anchoredError = [math]::Abs($anchored - $actual)
            if ($anchoredError -lt $Tolerance) {
                $counts.HoldEndAnchoredMatch++
            }
            if ($anchoredError -gt $maximumErrors.HoldEndAnchored) {
                $maximumErrors.HoldEndAnchored = $anchoredError
            }

            $plainSeconds = $endTime * 60.0 / $bpm
            if ([math]::Abs($plainSeconds - $actual) -lt $Tolerance) {
                $counts.HoldPlainSeconds++
            }
        }
    }
}

[PSCustomObject]@{
    JsonFiles                   = $files.Count
    Tolerance                   = $Tolerance
    SpeedKeyPointIntegralMatch  = "$($counts.SpeedKeyPointMatch)/$($counts.SpeedKeyPointTotal)"
    SpeedKeyPointMaximumError   = $maximumErrors.SpeedKeyPoint
    LinePointIntegralMatch      = "$($counts.LinePointMatch)/$($counts.LinePointTotal)"
    LinePointMaximumError       = $maximumErrors.LinePoint
    LinePointAnchoredMatch      = "$($counts.LinePointAnchoredMatch)/$($counts.LinePointTotal)"
    LinePointAnchoredMaxError   = $maximumErrors.LinePointAnchored
    HoldEndIntegralMatch        = "$($counts.HoldEndMatch)/$($counts.HoldEndTotal)"
    HoldEndMaximumError         = $maximumErrors.HoldEnd
    HoldEndAnchoredMatch        = "$($counts.HoldEndAnchoredMatch)/$($counts.HoldEndTotal)"
    HoldEndAnchoredMaxError     = $maximumErrors.HoldEndAnchored
    HoldEndPlainSecondsMatch    = "$($counts.HoldPlainSeconds)/$($counts.HoldEndTotal)"
}

if ($holdMismatches.Count -gt 0) {
    Write-Output ''
    Write-Output 'First Hold end mismatches:'
    $holdMismatches | Format-Table -AutoSize
}
