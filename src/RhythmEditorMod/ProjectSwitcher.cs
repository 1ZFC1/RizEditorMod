using UnityEngine;

namespace RhythmEditorMod;

/// <summary>
/// Switches the open project inside the running editor.
///
/// The editor keeps the working copy of a chart in memory (<c>ProjectInformation.chart</c>) and
/// only writes the chosen chart file on Ctrl+S; the music is copied to a temp file
/// (<c>ProjectInformation.songCachePath</c>, `%TEMP%\tmpXXXX.tmp`) on every load and the previous
/// copy is left behind. So a switch is not a file move: it must first make sure the current
/// changes are on disk, then let the game's own load chain run again.
///
/// Flow (D-024, docs/features/PROJECT_SWITCH.md):
/// <list type="number">
/// <item>If <c>notSaved</c> and the chart already has a path, call the original <c>FileControl.Save</c>
/// (which itself runs AutoSave first); if there is no path, refuse instead of opening "save as".</item>
/// <item>Point <c>PlayerPrefs</c> <c>lastFilePath</c> / <c>lastSongPath</c> at the target and call the
/// original <c>FileControl.OpenLastProject()</c>, so the game's own read, deserialize, music load,
/// auto-save baseline rebuild and load check all run unchanged.</item>
/// <item>Wait for the target to actually be loaded (path matches, <c>loadOver</c> and
/// <c>_songLoadOver</c>), then remember it and delete the previous temp music copy.</item>
/// </list>
///
/// Verified on 2026-09-23 in the working copy: the shortcut path can be used again mid-session
/// (reload of the same project), a real switch changes <c>filePath</c> and the in-memory chart
/// (230 -> 537 lines between two samples), and each load leaves the old temp music file behind.
/// </summary>
internal static class ProjectSwitcher
{
    private const float LoadTimeoutSeconds = 30f;
    private const float RecordPollSeconds = 2f;

    private static bool _loading;
    private static float _deadline;
    private static float _nextRecordPollAt;
    private static string _targetChart = string.Empty;
    private static string _previousSongCache = string.Empty;
    private static string _lastChart = string.Empty;
    private static string _lastSong = string.Empty;
    private static string _status = "选择下方工程即可切换；改动会在切换前自动保存。";

    internal static string Status => _status;

    internal static bool IsBusy => _loading;

    /// <summary>Called every frame: records newly opened projects and watches a pending switch.</summary>
    internal static void Tick()
    {
        try
        {
            WatchLoad();

            if (Time.unscaledTime < _nextRecordPollAt)
                return;
            _nextRecordPollAt = Time.unscaledTime + RecordPollSeconds;
            RecordCurrent();
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"工程切换：Tick 失败：{error}");
        }
    }

    /// <summary>
    /// Records whatever project is open, however it was opened (dialog or shortcut), and reports it
    /// on the status line so the panel always shows the chart and music actually in use.
    /// </summary>
    private static void RecordCurrent()
    {
        var project = ProjectInformation.instance;
        var chart = project?.filePath;
        var song = project?.fileControl?.songPath;
        if (string.IsNullOrEmpty(chart) || string.IsNullOrEmpty(song))
            return;

        ProjectSlots.Remember(chart, song);

        if (string.Equals(chart, _lastChart, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(song, _lastSong, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var first = _lastChart.Length == 0;
        _lastChart = chart!;
        _lastSong = song!;
        if (_loading)
            return; // the switch flow reports completion itself

        var prefix = first ? "当前工程" : "已打开";
        SetStatus($"{prefix}：{Path.GetFileName(chart)} + {Path.GetFileName(song)}");
    }

    /// <summary>Switches to a stored project. Returns a status line for the panel and the log.</summary>
    internal static string SwitchTo(string chartPath, string songPath)
    {
        if (_loading)
            return "正在切换中，请稍候…";

        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
            return $"谱面文件不存在：{chartPath}";

        var song = songPath ?? string.Empty;
        if (song.Length > 0 && !File.Exists(song))
            return $"音乐文件不存在：{song}";

        if (!TrySaveCurrent(out var saveError))
        {
            SetStatus(saveError);
            return saveError;
        }

        var project = ProjectInformation.instance;
        var fileControl = project?.fileControl;
        if (project == null || fileControl == null)
        {
            const string unavailable = "编辑器状态不可用，无法切换。";
            SetStatus(unavailable);
            return unavailable;
        }

        try
        {
            _previousSongCache = project.songCachePath ?? string.Empty;
            PlayerPrefs.SetString("lastFilePath", chartPath);
            PlayerPrefs.SetString("lastSongPath", song);
            PlayerPrefs.Save();

            _targetChart = chartPath;
            _loading = true;
            _deadline = Time.unscaledTime + LoadTimeoutSeconds;

            var status = $"正在切换到 {Path.GetFileName(chartPath)} …";
            SetStatus(status);
            Plugin.LogSource.LogInfo($"工程切换：开始切换到「{chartPath}」+「{song}」");
            fileControl.OpenLastProject();
            return status;
        }
        catch (Exception error)
        {
            _loading = false;
            var status = $"切换失败：{error.Message}";
            Plugin.LogSource.LogError($"工程切换：{status}");
            SetStatus(status);
            return status;
        }
    }

    /// <summary>Opens the original two-step picker (chart, then music).</summary>
    internal static string PickFiles()
    {
        if (_loading)
            return "正在切换中，请稍候…";

        if (!TrySaveCurrent(out var saveError))
        {
            SetStatus(saveError);
            return saveError;
        }

        var fileControl = ProjectInformation.instance?.fileControl;
        if (fileControl == null)
        {
            const string unavailable = "编辑器状态不可用，无法打开文件。";
            SetStatus(unavailable);
            return unavailable;
        }

        try
        {
            // The game's own two dialogs; whatever it loads is picked up by RecordCurrent(), which
            // then replaces this line with the chart and music that were actually opened.
            fileControl.OpenProject();
            const string status = "已打开原版选择对话框（先谱面、再音乐）；取消则当前工程不变。";
            SetStatus(status);
            return status;
        }
        catch (Exception error)
        {
            var status = $"打开文件失败：{error.Message}";
            Plugin.LogSource.LogError($"工程切换：{status}");
            SetStatus(status);
            return status;
        }
    }

    /// <summary>
    /// Makes sure the current chart is on disk before anything replaces it in memory. Reports a
    /// refusal instead of silently switching when the project has never been saved.
    /// </summary>
    private static bool TrySaveCurrent(out string error)
    {
        error = string.Empty;
        var project = ProjectInformation.instance;
        var fileControl = project?.fileControl;
        if (project == null || fileControl == null)
        {
            error = "编辑器状态不可用。";
            return false;
        }

        if (!ProjectInformation.notSaved)
            return true;

        var path = project.filePath;
        if (string.IsNullOrEmpty(path))
        {
            error = "当前工程从未保存过（没有谱面路径），已取消切换；请先用 Ctrl+Shift+S 另存为。";
            Plugin.LogSource.LogWarning($"工程切换：{error}");
            return false;
        }

        try
        {
            fileControl.Save();
        }
        catch (Exception saveError)
        {
            error = $"保存当前工程失败，已取消切换：{saveError.Message}";
            Plugin.LogSource.LogError($"工程切换：{error}");
            return false;
        }

        if (ProjectInformation.notSaved)
        {
            error = $"保存后编辑器仍报告未保存（{Path.GetFileName(path)}），已取消切换以免丢改动。";
            Plugin.LogSource.LogError($"工程切换：{error}");
            return false;
        }

        Plugin.LogSource.LogInfo($"工程切换：已保存当前工程 {Path.GetFileName(path)}");
        return true;
    }

    /// <summary>
    /// Completion test: the target path must be the open one and both load flags must be up, so a
    /// switch is never declared finished while the music is still loading.
    /// </summary>
    private static void WatchLoad()
    {
        if (!_loading)
            return;

        var project = ProjectInformation.instance;
        var fileControl = project?.fileControl;
        var chart = project?.filePath ?? string.Empty;
        var arrived = string.Equals(chart, _targetChart, StringComparison.OrdinalIgnoreCase);
        var loaded = ProjectInformation.loadOver && (fileControl == null || fileControl._songLoadOver);

        if (arrived && loaded)
        {
            _loading = false;
            ProjectSlots.Remember(chart, fileControl?.songPath);
            CleanupPreviousSongCache(project?.songCachePath);
            var status = $"已切换到 {Path.GetFileName(chart)}" +
                (string.IsNullOrEmpty(fileControl?.songPath) ? string.Empty : $" + {Path.GetFileName(fileControl!.songPath)}");
            SetStatus(status);
            Plugin.LogSource.LogInfo($"工程切换：{status}，当前谱面线数 {project?.chart?.lines?.Count ?? -1}");
            return;
        }

        if (Time.unscaledTime > _deadline)
        {
            _loading = false;
            var status = $"切换超时：等待 {LoadTimeoutSeconds:0} 秒后仍未确认「{Path.GetFileName(_targetChart)}」加载完成。";
            SetStatus(status);
            Plugin.LogSource.LogWarning($"工程切换：{status}");
        }
    }

    /// <summary>
    /// Each load copies the music to a fresh <c>%TEMP%\tmpXXXX.tmp</c> and leaves the previous copy
    /// behind. Only that exact kind of file is removed, and only once the new project has loaded.
    /// </summary>
    private static void CleanupPreviousSongCache(string? currentCache)
    {
        try
        {
            if (string.IsNullOrEmpty(_previousSongCache))
                return;
            if (string.Equals(_previousSongCache, currentCache, StringComparison.OrdinalIgnoreCase))
                return;
            if (!File.Exists(_previousSongCache))
                return;

            var tempRoot = Path.GetTempPath();
            if (!_previousSongCache.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.LogSource.LogInfo($"工程切换：旧音乐缓存在临时目录之外（{_previousSongCache}），不删除。");
                return;
            }

            if (!string.Equals(Path.GetExtension(_previousSongCache), ".tmp", StringComparison.OrdinalIgnoreCase))
            {
                Plugin.LogSource.LogInfo($"工程切换：旧音乐缓存不是 .tmp（{_previousSongCache}），不删除。");
                return;
            }

            File.Delete(_previousSongCache);
            Plugin.LogSource.LogInfo($"工程切换：已删除上一次的音乐缓存 {Path.GetFileName(_previousSongCache)}");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"工程切换：清理旧音乐缓存失败（不影响切换）：{error.Message}");
        }
        finally
        {
            _previousSongCache = string.Empty;
        }
    }

    private static void SetStatus(string status)
    {
        _status = status;
        // Straight into the project body's own status line; it no longer shares the plugin-wide status
        // (which carries other features' messages).
        NativeProjectPanel.SetStatusText(status);
    }
}
