[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$expectedGameAssemblyHash = 'F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876'
$gameAssemblyPath = Join-Path $repositoryRoot 'GameAssembly.dll'
$interopPath = Join-Path $repositoryRoot 'BepInEx\interop\Assembly-CSharp.dll'
$testProject = Join-Path $repositoryRoot 'tests\RhythmEditorMod.Core.Tests\RhythmEditorMod.Core.Tests.csproj'
$pluginProject = Join-Path $repositoryRoot 'src\RhythmEditorMod\RhythmEditorMod.csproj'
$pluginOutput = Join-Path $repositoryRoot 'src\RhythmEditorMod\bin\Release\net6.0\RhythmEditorMod.dll'
$coreOutput = Join-Path $repositoryRoot 'src\RhythmEditorMod.Core\bin\Release\net6.0\RhythmEditorMod.Core.dll'
$installDirectory = Join-Path $repositoryRoot 'BepInEx\plugins\RhythmEditorMod'

if (-not (Test-Path -LiteralPath $gameAssemblyPath -PathType Leaf)) {
    throw "GameAssembly.dll was not found in the working-copy root: $repositoryRoot"
}

$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $gameAssemblyPath).Hash
if ($actualHash -ne $expectedGameAssemblyHash) {
    throw "Unsupported GameAssembly.dll. Expected $expectedGameAssemblyHash, got $actualHash. Nothing was installed."
}

if (-not (Test-Path -LiteralPath $interopPath -PathType Leaf)) {
    throw 'BepInEx IL2CPP interfaces are missing. Complete the documented BepInEx bootstrap before building the plugin.'
}

Push-Location $repositoryRoot
try {
    & dotnet run --project $testProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Core tests failed with exit code $LASTEXITCODE. Nothing was installed."
    }

    & dotnet build $pluginProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Plugin build failed with exit code $LASTEXITCODE. Nothing was installed."
    }

    $disabledPlugin = Join-Path $installDirectory 'RhythmEditorMod.dll.disabled'
    if (Test-Path -LiteralPath $disabledPlugin -PathType Leaf) {
        throw "A disabled plugin copy already exists. Enable it or move it aside before installing: $disabledPlugin"
    }

    New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
    Copy-Item -Force -LiteralPath $pluginOutput -Destination (Join-Path $installDirectory 'RhythmEditorMod.dll')
    Copy-Item -Force -LiteralPath $coreOutput -Destination (Join-Path $installDirectory 'RhythmEditorMod.Core.dll')

    Write-Host 'PASS: tests and build completed.'
    Write-Host "Installed plugin: $installDirectory"
    Get-FileHash -Algorithm SHA256 -LiteralPath `
        (Join-Path $installDirectory 'RhythmEditorMod.dll'), `
        (Join-Path $installDirectory 'RhythmEditorMod.Core.dll') | Format-Table -AutoSize
}
finally {
    Pop-Location
}
