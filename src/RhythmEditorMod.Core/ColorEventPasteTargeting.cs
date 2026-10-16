namespace RhythmEditorMod.Core;

/// <summary>
/// Targeting rules for pasting colour events into an existing key-point array.
///
/// The editor does not allow two events on one beat: a leftover duplicate also wins the
/// same-time priority rule, so the rendered colour of that beat changes. A paste therefore
/// overwrites the event already on the target beat instead of inserting a second one.
/// </summary>
public static class ColorEventPasteTargeting
{
    public const double DefaultTolerance = 1e-5;

    /// <summary>Index of the event already sitting on <paramref name="beat"/>, or -1.</summary>
    public static int FindSameBeatIndex(
        IReadOnlyList<double> beats,
        double beat,
        double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(beats);
        for (var index = 0; index < beats.Count; index++)
        {
            if (Math.Abs(beats[index] - beat) <= tolerance)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Every event sitting on <paramref name="beat"/>, in array order. A chart edited by
    /// earlier versions can already hold several, and the later index wins the same-time
    /// priority rule, so a paste must collapse them rather than overwrite just the first.
    /// </summary>
    public static IReadOnlyList<int> FindSameBeatIndices(
        IReadOnlyList<double> beats,
        double beat,
        double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(beats);
        var matches = new List<int>();
        for (var index = 0; index < beats.Count; index++)
        {
            if (Math.Abs(beats[index] - beat) <= tolerance)
                matches.Add(index);
        }

        return matches;
    }

    /// <summary>Index the new event goes to so the array stays sorted by beat.</summary>
    public static int FindInsertIndex(
        IReadOnlyList<double> beats,
        double beat,
        double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(beats);
        for (var index = 0; index < beats.Count; index++)
        {
            if (beats[index] > beat + tolerance)
                return index;
        }

        return beats.Count;
    }
}
