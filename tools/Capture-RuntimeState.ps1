param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('before', 'after')]
    [string]$Phase,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [string]$WorkspaceRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Split-Path -Parent $PSScriptRoot
}

$workspace = (Resolve-Path -LiteralPath $WorkspaceRoot).Path
$phaseDirectory = Join-Path $OutputRoot $Phase
[System.IO.Directory]::CreateDirectory($phaseDirectory) | Out-Null

function Get-RelativePath {
    param([string]$Root, [string]$FullName)

    return $FullName.Substring($Root.Length + 1).Replace('\', '/')
}

function Get-FileInventory {
    param(
        [string]$Root,
        [string]$Label,
        [string[]]$ExcludedPrefixes = @()
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return @()
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $resolvedExclusions = @(
        foreach ($prefix in $ExcludedPrefixes) {
            [System.IO.Path]::GetFullPath($prefix).TrimEnd('\')
        }
    )

    return @(
        Get-ChildItem -LiteralPath $resolvedRoot -File -Force -Recurse | Where-Object {
            $candidate = $_.FullName
            -not ($resolvedExclusions | Where-Object {
                $candidate.Equals($_, [System.StringComparison]::OrdinalIgnoreCase) -or
                $candidate.StartsWith($_ + '\', [System.StringComparison]::OrdinalIgnoreCase)
            })
        } | ForEach-Object {
            [PSCustomObject]@{
                scope          = $Label
                relativePath   = Get-RelativePath -Root $resolvedRoot -FullName $_.FullName
                length         = $_.Length
                lastWriteUtc   = $_.LastWriteTimeUtc.ToString('o')
                sha256         = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        } | Sort-Object scope, relativePath
    )
}

function Write-Utf8Json {
    param([object]$Value, [string]$Path)

    $json = $Value | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}

$artifactRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
$workspaceInventory = Get-FileInventory -Root $workspace -Label 'workspace' -ExcludedPrefixes @(
    (Join-Path $workspace '.git'),
    $artifactRoot
)

$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$roamingAppData = [Environment]::GetFolderPath('ApplicationData')
$localLow = Join-Path (Split-Path -Parent $localAppData) 'LocalLow'

$appDataScopes = @(
    [PSCustomObject]@{ Label = 'LocalLow/PigeonGames'; Path = (Join-Path $localLow 'PigeonGames') },
    [PSCustomObject]@{ Label = 'Local/PigeonGames'; Path = (Join-Path $localAppData 'PigeonGames') },
    [PSCustomObject]@{ Label = 'Roaming/PigeonGames'; Path = (Join-Path $roamingAppData 'PigeonGames') }
)

$appDataInventory = @(
    foreach ($scope in $appDataScopes) {
        Get-FileInventory -Root $scope.Path -Label $scope.Label
    }
)

$registryKeys = @(
    'HKCU\Software\PigeonGames',
    'HKCU\Software\Unity\UnityEditor\PigeonGames'
)

$registrySnapshot = foreach ($key in $registryKeys) {
    $providerPath = $key.Replace('HKCU', 'Registry::HKEY_CURRENT_USER')
    if (Test-Path -LiteralPath $providerPath) {
        $output = & reg.exe query $key /s
        $registryExitCode = $LASTEXITCODE
    } else {
        $output = @()
        $registryExitCode = 1
    }

    [PSCustomObject]@{
        key      = $key
        exitCode = $registryExitCode
        output   = @($output | ForEach-Object { [string]$_ })
    }
}

$processSnapshot = Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -match '^(Rizline Editor|steam|steamwebhelper)$'
} | ForEach-Object {
    $processStartTime = $null
    $processPath = $null
    try { $processStartTime = $_.StartTime.ToUniversalTime().ToString('o') } catch { }
    try { $processPath = $_.Path } catch { }

    [PSCustomObject]@{
        processName = $_.ProcessName
        id          = $_.Id
        startTime   = $processStartTime
        path        = $processPath
    }
}

$summary = [PSCustomObject]@{
    phase               = $Phase
    capturedAtUtc       = [DateTime]::UtcNow.ToString('o')
    workspaceRoot       = $workspace
    workspaceFileCount  = $workspaceInventory.Count
    appDataFileCount    = $appDataInventory.Count
}

Write-Utf8Json -Value $summary -Path (Join-Path $phaseDirectory 'summary.json')
Write-Utf8Json -Value $workspaceInventory -Path (Join-Path $phaseDirectory 'workspace-files.json')
Write-Utf8Json -Value $appDataInventory -Path (Join-Path $phaseDirectory 'appdata-files.json')
Write-Utf8Json -Value $registrySnapshot -Path (Join-Path $phaseDirectory 'registry.json')
Write-Utf8Json -Value @($processSnapshot) -Path (Join-Path $phaseDirectory 'processes.json')

$summary
