namespace RhythmEditorMod.Core;

public readonly record struct SpeedSample(double Beat, double Value);

public static class SpeedEditing
{
    private const double Epsilon = 1e-6;

    public static long EstimateSampleCount(double startBeat, double endBeat, int splitCount)
    {
        ValidateInterval(startBeat, endBeat, splitCount);

        var scaledDuration = (endBeat - startBeat) * splitCount;
        var nearestInteger = Math.Round(scaledDuration);
        if (Math.Abs(scaledDuration - nearestInteger) <= Epsilon * Math.Max(1.0, Math.Abs(scaledDuration)))
            return checked((long)nearestInteger + 1L);

        return checked((long)Math.Floor(scaledDuration) + 2L);
    }

    public static IReadOnlyList<SpeedSample> GenerateSamples(
        double startBeat,
        double endBeat,
        double startValue,
        double endValue,
        int splitCount,
        Func<double, double> easing)
    {
        ValidateInterval(startBeat, endBeat, splitCount);
        ArgumentNullException.ThrowIfNull(easing);
        if (!double.IsFinite(startValue) || !double.IsFinite(endValue))
            throw new ArgumentOutOfRangeException(nameof(startValue), "Speed values must be finite.");

        var count = EstimateSampleCount(startBeat, endBeat, splitCount);
        if (count > int.MaxValue)
            throw new InvalidOperationException("The requested interval contains too many samples for one in-memory operation.");

        var samples = new List<SpeedSample>((int)count);
        var duration = endBeat - startBeat;
        for (long step = 0; step < count - 1; step++)
        {
            var beat = startBeat + step / (double)splitCount;
            if (beat >= endBeat - Epsilon)
                break;

            var progress = (beat - startBeat) / duration;
            var eased = easing(Math.Clamp(progress, 0.0, 1.0));
            if (!double.IsFinite(eased))
                throw new InvalidOperationException("The easing function returned a non-finite value.");
            samples.Add(new SpeedSample(beat, startValue + (endValue - startValue) * eased));
        }

        samples.Add(new SpeedSample(endBeat, endValue));
        return samples;
    }

    /// <summary>
    /// Drops internal samples whose float beat collapses onto either endpoint.
    /// Keeps endpoints themselves out of the insert list (indices 0 and Count-1).
    /// Two neighbouring samples can also collapse onto the *same* float beat; inserting both would
    /// put two speed events on one beat (reported 2026-09-23), so the later one is dropped as well.
    /// The result is therefore strictly increasing.
    /// </summary>
    public static IReadOnlyList<SpeedSample> FilterInsertSamples(
        IReadOnlyList<SpeedSample> samples,
        float startTime,
        float endTime,
        float epsilon = 1e-4f)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count <= 2)
            return Array.Empty<SpeedSample>();

        var result = new List<SpeedSample>(samples.Count - 2);
        float? previousInserted = null;
        for (var index = 1; index < samples.Count - 1; index++)
        {
            var sample = samples[index];
            var time = (float)sample.Beat;
            if (Math.Abs(time - startTime) <= epsilon || Math.Abs(time - endTime) <= epsilon)
                continue;
            if (previousInserted.HasValue && time == previousInserted.Value)
                continue;
            result.Add(sample);
            previousInserted = time;
        }

        return result;
    }

    /// <summary>
    /// True when no beat is earlier than the one before it. Same-beat duplicates are allowed: real
    /// charts contain them (an earlier version of the speed-easing generator could collapse two
    /// samples onto one beat) and the game resolves them by giving the later entry priority, so the
    /// plugin must be able to read, copy and delete such a list instead of refusing to work with it.
    /// </summary>
    public static bool IsAscending(IReadOnlyList<double> beats, double tolerance = Epsilon)
    {
        ArgumentNullException.ThrowIfNull(beats);
        for (var index = 1; index < beats.Count; index++)
        {
            var current = beats[index];
            var previous = beats[index - 1];
            if (!double.IsFinite(current) || !double.IsFinite(previous))
                continue;
            if (current < previous - tolerance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Indices whose beat repeats the previous entry's beat, i.e. the entries the game resolves by
    /// priority. Used to report how dirty a list already is before the plugin touches it.
    /// </summary>
    public static IReadOnlyList<int> FindSameBeatRepeats(IReadOnlyList<double> beats, double tolerance = Epsilon)
    {
        ArgumentNullException.ThrowIfNull(beats);
        var result = new List<int>();
        for (var index = 1; index < beats.Count; index++)
        {
            var current = beats[index];
            var previous = beats[index - 1];
            if (!double.IsFinite(current) || !double.IsFinite(previous))
                continue;
            if (Math.Abs(current - previous) <= tolerance)
                result.Add(index);
        }

        return result;
    }

    /// <summary>
    /// Collapses entries that share a beat, keeping the later one (the game's array priority), so a
    /// clipboard or a paste target never carries two entries for one beat. Requires ascending input;
    /// callers validate that first.
    /// </summary>
    public static IReadOnlyList<T> CollapseSameBeatKeepLast<T>(
        IReadOnlyList<T> items,
        Func<T, double> beatOf,
        double tolerance = Epsilon)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(beatOf);
        if (items.Count < 2)
            return items;

        var result = new List<T>(items.Count) { items[0] };
        for (var index = 1; index < items.Count; index++)
        {
            if (Math.Abs(beatOf(items[index]) - beatOf(result[^1])) <= tolerance)
                result[^1] = items[index];
            else
                result.Add(items[index]);
        }

        return result;
    }

    public static IReadOnlyList<int> FindClosedRangeDeletionIndices(
        IReadOnlyList<double> beats,
        double currentBeat,
        double clickedBeat,
        int preservedIndex = 0)
    {
        return FindClosedRangeIndices(beats, currentBeat, clickedBeat, preservedIndex, descending: true);
    }

    public static IReadOnlyList<int> FindClosedRangeIndices(
        IReadOnlyList<double> beats,
        double firstBeat,
        double secondBeat,
        int? excludedIndex = null,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(beats);
        if (!double.IsFinite(firstBeat) || !double.IsFinite(secondBeat))
            throw new ArgumentOutOfRangeException(nameof(firstBeat), "Beat values must be finite.");

        var lower = Math.Min(firstBeat, secondBeat) - Epsilon;
        var upper = Math.Max(firstBeat, secondBeat) + Epsilon;
        var result = new List<int>();
        for (var index = 0; index < beats.Count; index++)
        {
            if (index == excludedIndex)
                continue;
            var beat = beats[index];
            if (double.IsFinite(beat) && beat >= lower && beat <= upper)
                result.Add(index);
        }

        if (descending)
            result.Reverse();
        return result;
    }

    public static IReadOnlyList<double> CreatePasteBeats(
        IReadOnlyList<double> sourceBeats,
        double? alignFirstToBeat = null)
    {
        ArgumentNullException.ThrowIfNull(sourceBeats);
        if (sourceBeats.Count == 0)
            return Array.Empty<double>();
        if (alignFirstToBeat.HasValue && !double.IsFinite(alignFirstToBeat.Value))
            throw new ArgumentOutOfRangeException(nameof(alignFirstToBeat), "The paste anchor must be finite.");

        var first = sourceBeats[0];
        if (!double.IsFinite(first))
            throw new ArgumentOutOfRangeException(nameof(sourceBeats), "Clipboard beats must be finite.");
        var offset = alignFirstToBeat.HasValue ? alignFirstToBeat.Value - first : 0.0;
        var result = new double[sourceBeats.Count];
        for (var index = 0; index < sourceBeats.Count; index++)
        {
            var source = sourceBeats[index];
            var target = source + offset;
            if (!double.IsFinite(source) || !double.IsFinite(target))
                throw new ArgumentOutOfRangeException(nameof(sourceBeats), "Clipboard and target beats must be finite.");
            if (index > 0 && source <= sourceBeats[index - 1] + Epsilon)
                throw new InvalidOperationException("Clipboard beats must be strictly increasing.");
            result[index] = target;
        }
        return result;
    }

    /// <summary>
    /// Indices of existing entries that a paste would overwrite. The tolerance is a parameter so the
    /// caller can use the same "same beat" threshold as the rest of the editor: with a tighter
    /// threshold than the insert step uses, an existing node a few micro-beats away from a target
    /// survived the replacement and ended up beside the pasted node, i.e. two speeds on one beat.
    /// </summary>
    public static IReadOnlyList<int> FindPasteReplacementIndices(
        IReadOnlyList<double> existingBeats,
        IReadOnlyList<double> targetBeats,
        double tolerance = Epsilon)
    {
        ArgumentNullException.ThrowIfNull(existingBeats);
        ArgumentNullException.ThrowIfNull(targetBeats);
        var result = new List<int>();
        for (var existingIndex = existingBeats.Count - 1; existingIndex >= 0; existingIndex--)
        {
            var existing = existingBeats[existingIndex];
            if (!double.IsFinite(existing))
                continue;
            for (var targetIndex = 0; targetIndex < targetBeats.Count; targetIndex++)
            {
                if (Math.Abs(existing - targetBeats[targetIndex]) <= tolerance)
                {
                    result.Add(existingIndex);
                    break;
                }
            }
        }
        return result;
    }

    private static void ValidateInterval(double startBeat, double endBeat, int splitCount)
    {
        if (!double.IsFinite(startBeat) || !double.IsFinite(endBeat) || endBeat <= startBeat)
            throw new ArgumentOutOfRangeException(nameof(endBeat), "The end beat must be later than the start beat.");
        if (splitCount < 1)
            throw new ArgumentOutOfRangeException(nameof(splitCount), "The subdivision must be at least 1.");
    }
}
