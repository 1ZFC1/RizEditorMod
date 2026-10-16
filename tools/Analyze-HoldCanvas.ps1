param(
    [string]$ChartRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ChartRoot)) {
    $ChartRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '#Samples'
}

$resolvedRoot = (Resolve-Path -LiteralPath $ChartRoot).Path
$files = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter '*.json' | Sort-Object FullName)

$counts = [ordered]@{
    HoldTotal                    = 0
    StartAtOrPreviousMatch       = 0
    EndStrictPreviousMatch       = 0
    EndAtOrPreviousMatch         = 0
    EndExactlyOnPoint            = 0
    ExactEndStrictPreviousMatch  = 0
    ExactEndAtOrPreviousMatch    = 0
}

$mismatches = @()

foreach ($file in $files) {
    $document = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json

    for ($lineIndex = 0; $lineIndex -lt @($document.lines).Count; $lineIndex++) {
        $line = $document.lines[$lineIndex]
        $points = @($line.linePoints | Sort-Object { [double]$_.time })

        foreach ($note in @($line.notes)) {
            if ([int]$note.type -ne 2) {
                continue
            }

            $counts.HoldTotal++
            $startTime = [double]$note.time
            $endTime = [double]$note.otherInformations[0]
            $storedCanvas = [int][double]$note.otherInformations[1]

            $startAtOrPrevious = @(
                $points |
                    Where-Object { [double]$_.time -le $startTime } |
                    Select-Object -Last 1
            )
            $endStrictPrevious = @(
                $points |
                    Where-Object { [double]$_.time -lt $endTime } |
                    Select-Object -Last 1
            )
            $endAtOrPrevious = @(
                $points |
                    Where-Object { [double]$_.time -le $endTime } |
                    Select-Object -Last 1
            )
            $endExactlyOnPoint = @(
                $points |
                    Where-Object { [math]::Abs([double]$_.time - $endTime) -lt 0.0000001 }
            ).Count -gt 0

            if ($startAtOrPrevious.Count -gt 0 -and $storedCanvas -eq [int]$startAtOrPrevious[0].canvasIndex) {
                $counts.StartAtOrPreviousMatch++
            }
            if ($endStrictPrevious.Count -gt 0 -and $storedCanvas -eq [int]$endStrictPrevious[0].canvasIndex) {
                $counts.EndStrictPreviousMatch++
            } elseif ($mismatches.Count -lt 20) {
                $mismatches += [PSCustomObject]@{
                    File           = $file.FullName.Substring($resolvedRoot.Length + 1)
                    Line           = $lineIndex
                    Start          = $startTime
                    End            = $endTime
                    StoredCanvas   = $storedCanvas
                    ExpectedCanvas = if ($endStrictPrevious.Count -gt 0) {
                        [int]$endStrictPrevious[0].canvasIndex
                    } else {
                        $null
                    }
                }
            }
            if ($endAtOrPrevious.Count -gt 0 -and $storedCanvas -eq [int]$endAtOrPrevious[0].canvasIndex) {
                $counts.EndAtOrPreviousMatch++
            }

            if ($endExactlyOnPoint) {
                $counts.EndExactlyOnPoint++
                if ($endStrictPrevious.Count -gt 0 -and $storedCanvas -eq [int]$endStrictPrevious[0].canvasIndex) {
                    $counts.ExactEndStrictPreviousMatch++
                }
                if ($endAtOrPrevious.Count -gt 0 -and $storedCanvas -eq [int]$endAtOrPrevious[0].canvasIndex) {
                    $counts.ExactEndAtOrPreviousMatch++
                }
            }
        }
    }
}

[PSCustomObject]@{
    JsonFiles                       = $files.Count
    HoldTotal                       = $counts.HoldTotal
    StartAtOrPreviousMatch          = "$($counts.StartAtOrPreviousMatch)/$($counts.HoldTotal)"
    EndStrictPreviousMatch          = "$($counts.EndStrictPreviousMatch)/$($counts.HoldTotal)"
    EndAtOrPreviousMatch            = "$($counts.EndAtOrPreviousMatch)/$($counts.HoldTotal)"
    EndExactlyOnPoint               = $counts.EndExactlyOnPoint
    ExactEndStrictPreviousMatch      = "$($counts.ExactEndStrictPreviousMatch)/$($counts.EndExactlyOnPoint)"
    ExactEndAtOrPreviousMatch        = "$($counts.ExactEndAtOrPreviousMatch)/$($counts.EndExactlyOnPoint)"
}

if ($mismatches.Count -gt 0) {
    Write-Output ''
    Write-Output 'First strict-previous end canvas mismatches:'
    $mismatches | Format-Table -AutoSize
}
