[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Enable', 'Disable')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installDirectory = Join-Path $repositoryRoot 'BepInEx\plugins\RhythmEditorMod'
$enabledPath = Join-Path $installDirectory 'RhythmEditorMod.dll'
$disabledPath = Join-Path $installDirectory 'RhythmEditorMod.dll.disabled'

if (Get-Process -Name 'Rizline Editor' -ErrorAction SilentlyContinue) {
    throw 'Exit Rizline Editor before enabling or disabling the plugin.'
}

if ($Mode -eq 'Enable') {
    if (Test-Path -LiteralPath $enabledPath -PathType Leaf) {
        Write-Host 'The plugin is already enabled.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath $disabledPath -PathType Leaf)) {
        throw 'No installed or disabled RhythmEditorMod.dll was found. Run Build-Install-RhythmEditorMod.ps1 first.'
    }
    Move-Item -LiteralPath $disabledPath -Destination $enabledPath
    Write-Host 'RhythmEditorMod is enabled.'
}
else {
    if (Test-Path -LiteralPath $disabledPath -PathType Leaf) {
        Write-Host 'The plugin is already disabled.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath $enabledPath -PathType Leaf)) {
        throw 'RhythmEditorMod.dll is not installed.'
    }
    Move-Item -LiteralPath $enabledPath -Destination $disabledPath
    Write-Host 'RhythmEditorMod is disabled. Its source, configuration and generated charts were not removed.'
}
