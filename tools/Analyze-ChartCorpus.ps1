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
if ($files.Count -eq 0) {
    throw "No JSON files found under: $resolvedRoot"
}

$script:PathStats = @{}
$topLevelKeySets = @{}

function Get-RelativePath {
    param([string]$Root, [string]$FullName)

    return $FullName.Substring($Root.Length + 1).Replace('\', '/')
}

function Add-Observation {
    param(
        [string]$Path,
        [string]$Type,
        [string]$File,
        [object]$Value
    )

    if (-not $script:PathStats.ContainsKey($Path)) {
        $script:PathStats[$Path] = [PSCustomObject]@{
            Types         = @{}
            Files         = @{}
            Occurrences   = 0
            NumericMin    = $null
            NumericMax    = $null
            ArrayLengthMin = $null
            ArrayLengthMax = $null
        }
    }

    $entry = $script:PathStats[$Path]
    $entry.Types[$Type] = $true
    $entry.Files[$File] = $true
    $entry.Occurrences++

    if ($Type -eq 'number') {
        $number = [double]$Value
        if ($null -eq $entry.NumericMin -or $number -lt $entry.NumericMin) {
            $entry.NumericMin = $number
        }
        if ($null -eq $entry.NumericMax -or $number -gt $entry.NumericMax) {
            $entry.NumericMax = $number
        }
    }

    if ($Type -eq 'array') {
        $length = $Value.Count
        if ($null -eq $entry.ArrayLengthMin -or $length -lt $entry.ArrayLengthMin) {
            $entry.ArrayLengthMin = $length
        }
        if ($null -eq $entry.ArrayLengthMax -or $length -gt $entry.ArrayLengthMax) {
            $entry.ArrayLengthMax = $length
        }
    }
}

function Visit-JsonValue {
    param(
        [object]$Value,
        [string]$Path,
        [string]$File
    )

    if ($null -eq $Value) {
        Add-Observation -Path $Path -Type 'null' -File $File -Value $null
        return
    }

    if ($Value -is [System.Array]) {
        Add-Observation -Path $Path -Type 'array' -File $File -Value $Value
        foreach ($item in $Value) {
            Visit-JsonValue -Value $item -Path ($Path + '[]') -File $File
        }
        return
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        Add-Observation -Path $Path -Type 'object' -File $File -Value $Value
        foreach ($property in $Value.PSObject.Properties) {
            Visit-JsonValue -Value $property.Value -Path ($Path + '.' + $property.Name) -File $File
        }
        return
    }

    if ($Value -is [bool]) {
        $type = 'boolean'
    } elseif (
        $Value -is [byte] -or
        $Value -is [int16] -or
        $Value -is [int32] -or
        $Value -is [int64] -or
        $Value -is [single] -or
        $Value -is [double] -or
        $Value -is [decimal]
    ) {
        $type = 'number'
    } elseif ($Value -is [string]) {
        $type = 'string'
    } else {
        $type = $Value.GetType().FullName
    }

    Add-Observation -Path $Path -Type $type -File $File -Value $Value
}

foreach ($file in $files) {
    $relativePath = Get-RelativePath -Root $resolvedRoot -FullName $file.FullName
    $document = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $keySet = ($document.PSObject.Properties.Name -join ',')
    if (-not $topLevelKeySets.ContainsKey($keySet)) {
        $topLevelKeySets[$keySet] = 0
    }
    $topLevelKeySets[$keySet]++
    Visit-JsonValue -Value $document -Path '$' -File $relativePath
}

Write-Output "Chart root: $resolvedRoot"
Write-Output "JSON files: $($files.Count)"
Write-Output 'Top-level key sets:'
foreach ($keySet in ($topLevelKeySets.Keys | Sort-Object)) {
    Write-Output "  $($topLevelKeySets[$keySet]) x $keySet"
}
Write-Output ''

$rows = foreach ($path in ($script:PathStats.Keys | Sort-Object)) {
    $entry = $script:PathStats[$path]
    if ($entry.Types.ContainsKey('array')) {
        $observedRange = "length $($entry.ArrayLengthMin)..$($entry.ArrayLengthMax)"
    } elseif ($entry.Types.ContainsKey('number')) {
        $observedRange = "value $($entry.NumericMin)..$($entry.NumericMax)"
    } else {
        $observedRange = ''
    }

    [PSCustomObject]@{
        Path          = $path
        Types         = (($entry.Types.Keys | Sort-Object) -join '|')
        Occurrences   = $entry.Occurrences
        Files         = $entry.Files.Count
        ObservedRange = $observedRange
    }
}

$rows | Format-Table -AutoSize
