[CmdletBinding()]
param(
    [string]$Version = '0.2.0',
    [string]$OutputRoot,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\releases'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$releaseName = "RhythmEditorMod-$Version-win-x64"
$releaseDirectory = Join-Path $OutputRoot $releaseName
$packageDirectory = Join-Path $releaseDirectory 'RhythmEditorMod'
$zipPath = Join-Path $OutputRoot "$releaseName.zip"
$hashPath = "$zipPath.sha256"
$expectedGameAssemblyHash = 'F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876'
$managerProject = Join-Path $repositoryRoot 'installer\RhythmEditorMod.Manager\RhythmEditorMod.Manager.csproj'
$launcherSource = Join-Path $repositoryRoot 'installer\RhythmEditorMod.Manager\ManagerLauncher.cpp'
$managerBuild = Join-Path $repositoryRoot 'installer\RhythmEditorMod.Manager\bin\Release\net8.0\package-publish'
$launcherBuild = Join-Path $repositoryRoot 'installer\RhythmEditorMod.Manager\bin\Release\RhythmEditorMod.Manager.Launcher.exe'

if ((Test-Path -LiteralPath $releaseDirectory) -or (Test-Path -LiteralPath $zipPath)) {
    if (-not $Force) {
        throw "Release output already exists. Choose a new version/output path or pass -Force: $releaseName"
    }
    if (Test-Path -LiteralPath $releaseDirectory) {
        Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $hashPath) {
        Remove-Item -LiteralPath $hashPath -Force
    }
}

& (Join-Path $repositoryRoot 'tools\Build-Install-RhythmEditorMod.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Plugin tests/build failed with exit code $LASTEXITCODE."
}

& dotnet publish $managerProject --configuration Release --no-restore --output $managerBuild `
    "-p:Version=$Version" "-p:AssemblyVersion=$Version.0" "-p:FileVersion=$Version.0"
if ($LASTEXITCODE -ne 0) {
    throw "Manager build failed with exit code $LASTEXITCODE."
}

& g++ -std=c++20 -O2 -static -static-libgcc -static-libstdc++ -municode `
    $launcherSource -o $launcherBuild
if ($LASTEXITCODE -ne 0) {
    throw "Native manager launcher build failed with exit code $LASTEXITCODE."
}

$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$runtimeRoot = Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App'
$runtimeVersion = Get-ChildItem -LiteralPath $runtimeRoot -Directory |
    Where-Object { $_.Name -like '8.*' } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $runtimeVersion) {
    throw 'No local .NET 8 x64 runtime was found for the private manager runtime.'
}
$hostFxr = Join-Path $dotnetRoot "host\fxr\$($runtimeVersion.Name)\hostfxr.dll"
if (-not (Test-Path -LiteralPath $hostFxr -PathType Leaf)) {
    throw "Matching hostfxr.dll was not found: $hostFxr"
}

New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
Copy-Item -LiteralPath $launcherBuild -Destination (Join-Path $packageDirectory 'RhythmEditorMod.Manager.exe')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\PACKAGE_README.md') -Destination (Join-Path $packageDirectory 'README.md')

$privateRuntime = Join-Path $packageDirectory 'manager-runtime'
New-Item -ItemType Directory -Force -Path $privateRuntime | Out-Null
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'dotnet.exe') -Destination $privateRuntime
New-Item -ItemType Directory -Force -Path (Join-Path $privateRuntime "host\fxr\$($runtimeVersion.Name)") | Out-Null
Copy-Item -LiteralPath $hostFxr -Destination (Join-Path $privateRuntime "host\fxr\$($runtimeVersion.Name)\hostfxr.dll")
New-Item -ItemType Directory -Force -Path (Join-Path $privateRuntime 'shared\Microsoft.NETCore.App') | Out-Null
Copy-Item -LiteralPath $runtimeVersion.FullName -Destination (Join-Path $privateRuntime 'shared\Microsoft.NETCore.App') -Recurse
Copy-Item -Path (Join-Path $managerBuild '*') -Destination $privateRuntime -Force

$payload = Join-Path $packageDirectory 'payload'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
foreach ($rootFile in @('.doorstop_version', 'doorstop_config.ini', 'winhttp.dll')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $rootFile) -Destination (Join-Path $payload $rootFile)
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'dotnet') -Destination $payload -Recurse

$payloadBepInEx = Join-Path $payload 'BepInEx'
New-Item -ItemType Directory -Force -Path $payloadBepInEx | Out-Null
foreach ($directory in @('core', 'interop', 'unity-libs')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "BepInEx\$directory") -Destination $payloadBepInEx -Recurse
}
New-Item -ItemType Directory -Force -Path (Join-Path $payloadBepInEx 'metadata') | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'BepInEx\metadata\global-metadata.standard.dat') `
    -Destination (Join-Path $payloadBepInEx 'metadata\global-metadata.standard.dat')
New-Item -ItemType Directory -Force -Path (Join-Path $payloadBepInEx 'config') | Out-Null
$configText = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'BepInEx\config\BepInEx.cfg'))
$configText = [Text.RegularExpressions.Regex]::Replace(
    $configText,
    '(?m)^UpdateInteropAssemblies\s*=\s*true\s*$',
    'UpdateInteropAssemblies = false')
[IO.File]::WriteAllText(
    (Join-Path $payloadBepInEx 'config\BepInEx.cfg'),
    $configText,
    [Text.UTF8Encoding]::new($false))

$payloadPlugin = Join-Path $payloadBepInEx 'plugins\RhythmEditorMod'
New-Item -ItemType Directory -Force -Path $payloadPlugin | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'BepInEx\plugins\RhythmEditorMod\RhythmEditorMod.dll') -Destination $payloadPlugin
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'BepInEx\plugins\RhythmEditorMod\RhythmEditorMod.Core.dll') -Destination $payloadPlugin

$fileEntries = foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring($payload.Length + 1).Replace('\', '/')
    $role = if ($relative -like 'BepInEx/plugins/RhythmEditorMod/*') {
        'plugin'
    }
    elseif ($relative -eq 'BepInEx/config/BepInEx.cfg') {
        'config'
    }
    else {
        'runtime'
    }
    [ordered]@{
        Path = $relative
        Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash
        Role = $role
    }
}
$manifest = [ordered]@{
    SchemaVersion = 1
    PackageVersion = $Version
    BepInExVersion = '6.0.0-be.785'
    ExpectedGameAssemblySha256 = $expectedGameAssemblyHash
    Files = @($fileEntries)
}
[IO.File]::WriteAllText(
    (Join-Path $packageDirectory 'manifest.json'),
    ($manifest | ConvertTo-Json -Depth 5),
    [Text.UTF8Encoding]::new($false))

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
[IO.File]::WriteAllText(
    $hashPath,
    "$zipHash  $([IO.Path]::GetFileName($zipPath))`r`n",
    [Text.UTF8Encoding]::new($false))

$payloadBytes = (Get-ChildItem -LiteralPath $payload -Recurse -File | Measure-Object Length -Sum).Sum
$packageBytes = (Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host "PASS: exported $releaseName"
Write-Host "Folder: $packageDirectory"
Write-Host "ZIP: $zipPath"
Write-Host "ZIP SHA-256: $zipHash"
Write-Host "Payload files: $($fileEntries.Count)"
Write-Host "Payload bytes: $payloadBytes"
Write-Host "Package bytes: $packageBytes"
