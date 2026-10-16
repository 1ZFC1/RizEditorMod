param(
    [Parameter(Mandatory = $true)]
    [string]$SnapshotRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-JsonArray {
    param([string]$Path)

    $value = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    return @($value)
}

function Compare-Inventory {
    param(
        [object[]]$Before,
        [object[]]$After,
        [string]$Kind
    )

    $beforeMap = @{}
    foreach ($item in $Before) {
        $beforeMap["$($item.scope)|$($item.relativePath)"] = $item
    }

    $afterMap = @{}
    foreach ($item in $After) {
        $afterMap["$($item.scope)|$($item.relativePath)"] = $item
    }

    $keys = @($beforeMap.Keys + $afterMap.Keys | Sort-Object -Unique)
    foreach ($key in $keys) {
        if (-not $beforeMap.ContainsKey($key)) {
            [PSCustomObject]@{ kind = $Kind; status = 'added'; path = $key; beforeHash = $null; afterHash = $afterMap[$key].sha256 }
        } elseif (-not $afterMap.ContainsKey($key)) {
            [PSCustomObject]@{ kind = $Kind; status = 'removed'; path = $key; beforeHash = $beforeMap[$key].sha256; afterHash = $null }
        } elseif ($beforeMap[$key].sha256 -ne $afterMap[$key].sha256) {
            [PSCustomObject]@{ kind = $Kind; status = 'changed'; path = $key; beforeHash = $beforeMap[$key].sha256; afterHash = $afterMap[$key].sha256 }
        }
    }
}

$beforeDirectory = Join-Path $SnapshotRoot 'before'
$afterDirectory = Join-Path $SnapshotRoot 'after'

$workspaceChanges = Compare-Inventory `
    -Before (Read-JsonArray (Join-Path $beforeDirectory 'workspace-files.json')) `
    -After (Read-JsonArray (Join-Path $afterDirectory 'workspace-files.json')) `
    -Kind 'workspace'

$appDataChanges = Compare-Inventory `
    -Before (Read-JsonArray (Join-Path $beforeDirectory 'appdata-files.json')) `
    -After (Read-JsonArray (Join-Path $afterDirectory 'appdata-files.json')) `
    -Kind 'appdata'

$beforeRegistry = Get-Content -LiteralPath (Join-Path $beforeDirectory 'registry.json') -Raw -Encoding UTF8
$afterRegistry = Get-Content -LiteralPath (Join-Path $afterDirectory 'registry.json') -Raw -Encoding UTF8

$changes = @($workspaceChanges) + @($appDataChanges)
$summary = [PSCustomObject]@{
    workspaceChanges = @($workspaceChanges).Count
    appDataChanges   = @($appDataChanges).Count
    registryChanged = ($beforeRegistry -ne $afterRegistry)
}

$summary
$changes | Sort-Object kind, path | Format-Table kind, status, path -AutoSize
