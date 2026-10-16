using System.Text.Json;
using BepInEx;
using RhythmEditorMod.Core;

namespace RhythmEditorMod;

/// <summary>
/// Persistence for the "last three projects" list: a small, human-readable JSON file under the
/// plugin's config directory (D-024). Deliberately not the editor's PlayerPrefs: the list is
/// plugin state, and keeping it in a file makes it inspectable, editable and backup-able.
/// </summary>
internal static class ProjectSlots
{
    private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "RhythmEditorMod", "projects.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static IReadOnlyList<ProjectSlot> _slots = ProjectSlotList.Empty;

    internal static IReadOnlyList<ProjectSlot> Slots => _slots;

    internal static string StoragePath => FilePath;

    /// <summary>Reads the list; any failure is logged and leaves the list empty rather than throwing.</summary>
    internal static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Plugin.LogSource.LogInfo($"工程槽位：还没有配置文件，将在首次记录时创建 {FilePath}");
                _slots = ProjectSlotList.Empty;
                return;
            }

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(FilePath), Options);
            if (entries == null)
            {
                _slots = ProjectSlotList.Empty;
                return;
            }

            var slots = new List<ProjectSlot>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ChartPath))
                    continue;
                slots.Add(new ProjectSlot(entry.ChartPath, entry.SongPath ?? string.Empty, entry.LastOpenedUtc ?? string.Empty));
            }

            _slots = slots;
            Plugin.LogSource.LogInfo($"工程槽位：已读取 {slots.Count} 条（{FilePath}）");
        }
        catch (Exception error)
        {
            _slots = ProjectSlotList.Empty;
            Plugin.LogSource.LogError($"工程槽位：读取失败，已按空列表继续：{error.Message}");
        }
    }

    /// <summary>Records a project as most recent and writes the file back.</summary>
    internal static void Remember(string? chartPath, string? songPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return;

        var before = _slots;
        _slots = ProjectSlotList.Promote(_slots, new ProjectSlot(
            chartPath!,
            songPath ?? string.Empty,
            DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")));

        if (ReferenceEquals(before, _slots) || SlotsEqual(before, _slots))
            return;

        Save();
        Plugin.LogSource.LogInfo(
            $"工程槽位：已记录 {Path.GetFileName(chartPath)}，当前共 {_slots.Count} 条：{Describe()}");
    }

    private static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var entries = new List<Entry>();
            foreach (var slot in _slots)
                entries.Add(new Entry { ChartPath = slot.ChartPath, SongPath = slot.SongPath, LastOpenedUtc = slot.LastOpenedUtc });

            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, Options));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"工程槽位：写入失败：{error}");
        }
    }

    private static string Describe()
    {
        var parts = new List<string>();
        foreach (var slot in _slots)
            parts.Add($"{Path.GetFileName(slot.ChartPath)}+{Path.GetFileName(slot.SongPath)}");
        return string.Join(" | ", parts);
    }

    private static bool SlotsEqual(IReadOnlyList<ProjectSlot> left, IReadOnlyList<ProjectSlot> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].ChartPath != right[index].ChartPath || left[index].SongPath != right[index].SongPath)
                return false;
        }

        return true;
    }

    private sealed class Entry
    {
        public string? ChartPath { get; set; }

        public string? SongPath { get; set; }

        public string? LastOpenedUtc { get; set; }
    }
}
