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
$rows = @()

foreach ($file in $files) {
    $document = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json

    for ($index = 0; $index -lt @($document.challengeTimes).Count; $index++) {
        $challenge = $document.challengeTimes[$index]
        $checkPoint = [double]$challenge.checkPoint
        $start = [double]$challenge.start
        $end = [double]$challenge.end

        $rows += [PSCustomObject]@{
            File          = $file.FullName.Substring($resolvedRoot.Length + 1)
            Index         = $index
            CheckPoint    = $checkPoint
            Start         = $start
            End           = $end
            TransTime     = [double]$challenge.transTime
            Relation      = if ($checkPoint -lt $start) {
                'before'
            } elseif ($checkPoint -gt $end) {
                'after'
            } else {
                'within'
            }
        }
    }
}

[PSCustomObject]@{
    JsonFiles                 = $files.Count
    ChallengeTimeTotal        = $rows.Count
    CheckPointMinimum         = ($rows | Measure-Object CheckPoint -Minimum).Minimum
    CheckPointMaximum         = ($rows | Measure-Object CheckPoint -Maximum).Maximum
    CheckPointEqualsStart     = @($rows | Where-Object { $_.CheckPoint -eq $_.Start }).Count
    CheckPointBeforeStart     = @($rows | Where-Object { $_.CheckPoint -lt $_.Start }).Count
    CheckPointAfterStart      = @($rows | Where-Object { $_.CheckPoint -gt $_.Start }).Count
    CheckPointWithinInterval  = @(
        $rows |
            Where-Object { $_.CheckPoint -ge $_.Start -and $_.CheckPoint -le $_.End }
    ).Count
}

Write-Output ''
$rows | Format-Table File, Index, CheckPoint, Start, End, TransTime, Relation -AutoSize
