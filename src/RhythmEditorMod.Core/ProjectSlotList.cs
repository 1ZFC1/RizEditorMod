namespace RhythmEditorMod.Core;

/// <summary>One remembered project: its chart file and the music that was loaded with it.</summary>
public readonly record struct ProjectSlot(string ChartPath, string SongPath, string LastOpenedUtc);

/// <summary>
/// The "last three projects" list shown by the plugin's own toolbar entry.
///
/// Kept as pure data so the ordering rules are testable: newest first, one entry per chart file
/// (paths compared case-insensitively, the way Windows treats them), capped at
/// <see cref="Capacity"/> entries. A project with no chart path never enters the list, so an
/// unsaved "new project" cannot be remembered and then fail to open.
/// </summary>
public static class ProjectSlotList
{
    public const int Capacity = 3;

    public static IReadOnlyList<ProjectSlot> Empty { get; } = Array.Empty<ProjectSlot>();

    /// <summary>
    /// Moves <paramref name="entry"/> to the front, refreshing it if that chart was already listed
    /// and dropping the oldest entry when the list is full.
    /// </summary>
    public static IReadOnlyList<ProjectSlot> Promote(IReadOnlyList<ProjectSlot>? slots, ProjectSlot entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ChartPath))
            return slots ?? Empty;

        var result = new List<ProjectSlot>(Capacity);
        var song = entry.SongPath;
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (PathsEqual(slot.ChartPath, entry.ChartPath))
                {
                    // Keep the known music when the caller could not supply one this time.
                    if (string.IsNullOrWhiteSpace(song))
                        song = slot.SongPath;
                    continue;
                }

                result.Add(slot);
                if (result.Count >= Capacity)
                    break;
            }
        }

        result.Insert(0, entry with { SongPath = song });
        if (result.Count > Capacity)
            result.RemoveRange(Capacity, result.Count - Capacity);
        return result;
    }

    /// <summary>Index of the slot holding this chart path, or -1.</summary>
    public static int IndexOf(IReadOnlyList<ProjectSlot>? slots, string? chartPath)
    {
        if (slots == null || string.IsNullOrWhiteSpace(chartPath))
            return -1;
        for (var index = 0; index < slots.Count; index++)
        {
            if (PathsEqual(slots[index].ChartPath, chartPath))
                return index;
        }

        return -1;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
