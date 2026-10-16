# Publishes the plug-in sources to the public repository workspace.
#
# The development repository (`RhythmEditor_working`) stays untouched: this script mirrors the files
# tracked by git into a separate folder (`..\RizEditorMod` by default) which is its own git repository
# pointing at GitHub. Files that must not be published (personal notes) are skipped, absolute local
# paths are rewritten, and only README.md / LICENSE / .git / .github are preserved from the sweep, so
# re-running the script after every release is safe.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1 -Commit -Message "..."
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1 -Commit -Push

[CmdletBinding()]
param(
    [string]$Target,
    [string]$RemoteUrl = 'https://github.com/1ZFC1/RizEditorMod.git',
    [string]$Message,
    [switch]$Commit,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Target)) {
    $Target = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '..\RizEditorMod'))
} else {
    $Target = [IO.Path]::GetFullPath($Target)
}

# Never published: the user's personal notes.
$excluded = @(
    'docs/待实现功能.txt',
    'docs/问题BUG.txt'
)

# Preserved in the target folder: they are maintained there (or belong to git itself).
$preservedPrefixes = @('.git\', '.github\', 'README.md', 'LICENSE')

# Local paths that are rewritten to relative form before publishing.
$pathRewrites = [ordered]@{
    'RhythmEditor_working' = 'RhythmEditor_working'
    'RhythmEditor_working' = 'RhythmEditor_working'
    'RhythmEditor_original' = 'RhythmEditor_original'
    'RhythmEditor_original' = 'RhythmEditor_original'
    'RhythmEditor_modded' = 'RhythmEditor_modded'
    'RhythmEditor_modded' = 'RhythmEditor_modded'
    'RZEMOD' = 'RZEMOD'
    'RZEMOD' = 'RZEMOD'
}

function Get-TrackedFiles {
    # `git ls-files` escapes non-ASCII paths and quotes them; reading the NUL-separated list with an
    # explicit UTF-8 decoder keeps Chinese file names intact (PowerShell 5.1 would otherwise decode the
    # native output with the console code page and produce illegal path characters).
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git'
    $startInfo.Arguments = "-C `"$repositoryRoot`" -c core.quotepath=false ls-files -z"
    $startInfo.RedirectStandardOutput = $true
    $startInfo.UseShellExecute = $false
    $startInfo.StandardOutputEncoding = [Text.Encoding]::UTF8

    $process = [Diagnostics.Process]::Start($startInfo)
    $output = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw 'git ls-files 失败：请确认开发仓库可用。'
    }

    return $output -split "`0" | Where-Object { $_ -and ($excluded -notcontains $_) }
}

function Write-PublishedFile {
    param([string]$RelativePath)

    $source = Join-Path $repositoryRoot ($RelativePath -replace '/', '\')
    $destination = Join-Path $Target ($RelativePath -replace '/', '\')
    $destinationDirectory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    $bytes = [IO.File]::ReadAllBytes($source)
    $text = $null
    try {
        $text = [Text.Encoding]::UTF8.GetString($bytes)
    } catch {
        $text = $null
    }

    $rewritten = 0
    if ($null -ne $text -and $text.IndexOf([char]0) -lt 0) {
        foreach ($pair in $pathRewrites.GetEnumerator()) {
            if ($text.Contains($pair.Key)) {
                $rewritten += ([regex]::Matches($text, [regex]::Escape($pair.Key))).Count
                $text = $text.Replace($pair.Key, $pair.Value)
            }
        }

        if ($rewritten -gt 0) {
            [IO.File]::WriteAllText($destination, $text, (New-Object Text.UTF8Encoding($false)))
            return $rewritten
        }
    }

    [IO.File]::WriteAllBytes($destination, $bytes)
    return 0
}

Write-Host "公开工作区：$Target"
if (-not (Test-Path -LiteralPath $Target)) {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    Write-Host '已创建公开工作区目录。'
}

$tracked = Get-TrackedFiles
$published = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$rewriteTotal = 0
foreach ($relative in $tracked) {
    $published.Add(($relative -replace '/', '\')) | Out-Null
    $rewriteTotal += Write-PublishedFile -RelativePath $relative
}

# Remove everything that is no longer part of the published set (except the preserved entries).
$removed = 0
Get-ChildItem -LiteralPath $Target -Recurse -File -Force | ForEach-Object {
    $relative = $_.FullName.Substring($Target.Length).TrimStart('\')
    $isPreserved = $false
    foreach ($prefix in $preservedPrefixes) {
        if ($relative.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $isPreserved = $true
            break
        }
    }

    if ($isPreserved -or $published.Contains($relative)) {
        return
    }

    Remove-Item -LiteralPath $_.FullName -Force
    $script:removed++
}

# Drop directories that became empty.
Get-ChildItem -LiteralPath $Target -Recurse -Directory -Force |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        if ($_.FullName -like "$Target\.git*") {
            return
        }

        if (-not (Get-ChildItem -LiteralPath $_.FullName -Force)) {
            Remove-Item -LiteralPath $_.FullName -Force
        }
    }

Write-Host "已同步 $($tracked.Count) 个文件（重写本地绝对路径 $rewriteTotal 处，删除 $removed 个多余文件）。"
Write-Host "未发布（私人笔记）：$($excluded -join '、')"

if (-not $Commit) {
    Write-Host '未提交（加 -Commit 可提交，加 -Push 可同时推送）。'
    exit 0
}

if (-not (Test-Path -LiteralPath (Join-Path $Target '.git'))) {
    & git -C $Target init -b main 2>&1 | Out-String | Out-Host
    & git -C $Target remote add origin $RemoteUrl 2>&1 | Out-String | Out-Host
    Write-Host "已初始化仓库并设置 origin=$RemoteUrl"
}

$ErrorActionPreference = 'Continue'
$resolvedMessage = if ([string]::IsNullOrWhiteSpace($Message)) { 'sync: publish from RhythmEditor_working' } else { $Message }
$existingRemote = & git -C $Target remote
if (-not $existingRemote) {
    & git -C $Target remote add origin $RemoteUrl 2>&1 | Out-String | Out-Host
}

& git -C $Target add -A 2>&1 | Out-String | Out-Host
& git -C $Target commit -m $resolvedMessage 2>&1 | Out-String | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Warning '没有需要提交的改动。'
}

if ($Push) {
    & git -C $Target push -u origin main 2>&1 | Out-String | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning '推送失败：请确认远程仓库已创建、并且已完成 GitHub 授权。'
    }
}
