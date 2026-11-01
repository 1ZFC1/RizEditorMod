namespace RhythmEditorMod.Core;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
{
    public string ToHex() => $"{R:X2}{G:X2}{B:X2}{A:X2}";

    public static Rgba32 FromChartChannels(float r, float g, float b, float a)
    {
        var usesByteScale = r > 1f || g > 1f || b > 1f || a > 1f;
        var scale = usesByteScale ? 255f : 1f;
        return FromNormalized(r / scale, g / scale, b / scale, a / scale);
    }

    public static Rgba32 FromNormalized(float r, float g, float b, float a) => new(
        ToByte(r),
        ToByte(g),
        ToByte(b),
        ToByte(a));

    public static bool TryParseHex(string? text, out Rgba32 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
            hex = hex[1..];
        if (hex.Length != 8)
            return false;

        try
        {
            color = new Rgba32(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEmptyPaletteSlot(string? hex) =>
        string.IsNullOrWhiteSpace(hex)
        || string.Equals(hex.Trim(), "FFFFFFFF", StringComparison.OrdinalIgnoreCase);

    private static byte ToByte(float value)
    {
        if (!float.IsFinite(value))
            return 0;
        return (byte)Math.Clamp((int)Math.Round(value * 255.0), 0, 255);
    }
}

public static class ChartColorCollection
{
    /// <summary>
    /// Builds the next palette layout from the current slots and every chart-colour occurrence.
    /// The chart colours are deduplicated, sorted by descending occurrence count (ties keep the
    /// first-seen order) and placed at the front. The old non-empty slots that are not part of the
    /// front block keep their order directly after it, and entries that no longer fit are dropped;
    /// any old copy of a front colour is removed so the palette never holds a duplicate. Repeating
    /// the same import is idempotent instead of shifting the old colours again on every click.
    /// </summary>
    public static PaletteInsertionPlan PlanFrequencyInsert(
        IReadOnlyList<string?> existingSlots,
        IReadOnlyList<Rgba32> occurrences)
    {
        ArgumentNullException.ThrowIfNull(existingSlots);
        ArgumentNullException.ThrowIfNull(occurrences);

        var capacity = existingSlots.Count;
        if (capacity <= 0)
            throw new ArgumentException("Palette capacity must be positive.", nameof(existingSlots));

        var counts = new Dictionary<Rgba32, int>();
        var firstSeen = new Dictionary<Rgba32, int>();
        for (var index = 0; index < occurrences.Count; index++)
        {
            var color = occurrences[index];
            // Fully transparent colours are not useful as palette entries; skip them before
            // counting so they neither enter the front block nor affect the frequency order.
            if (color.A == 0)
                continue;

            if (counts.TryGetValue(color, out var count))
            {
                counts[color] = count + 1;
                continue;
            }

            counts[color] = 1;
            firstSeen[color] = index;
        }

        var ordered = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => firstSeen[pair.Key])
            .Select(pair => pair.Key)
            .ToArray();

        var front = new List<Rgba32>(Math.Min(capacity, ordered.Length));
        var frontSet = new HashSet<Rgba32>();
        for (var index = 0; index < ordered.Length && front.Count < capacity; index++)
        {
            front.Add(ordered[index]);
            frontSet.Add(ordered[index]);
        }

        var frontCount = front.Count;
        var dropped = ordered.Length - frontCount;
        var oldRest = new List<string>(capacity);
        var movedFromOld = 0;

        for (var index = 0; index < capacity; index++)
        {
            var text = existingSlots[index]?.Trim() ?? string.Empty;
            if (Rgba32.IsEmptyPaletteSlot(text))
                continue;

            if (Rgba32.TryParseHex(text, out var color))
            {
                if (frontSet.Contains(color))
                {
                    // The same colour is re-inserted at the front; drop the old copy so the
                    // palette keeps no duplicates.
                    movedFromOld++;
                    continue;
                }

                oldRest.Add(color.ToHex());
                continue;
            }

            oldRest.Add(text);
        }

        var next = new string[capacity];
        Array.Fill(next, string.Empty);
        for (var index = 0; index < frontCount; index++)
            next[index] = front[index].ToHex();

        // Keep the old non-front colours in their original order directly after the front block.
        // This makes a repeated import of the same colours idempotent: the old colours are not
        // shifted again by the whole front size on every click.
        var available = capacity - frontCount;
        var evicted = 0;
        for (var index = 0; index < oldRest.Count; index++)
        {
            if (index < available)
            {
                next[frontCount + index] = oldRest[index];
                continue;
            }

            if (!string.IsNullOrEmpty(oldRest[index]))
                evicted++;
        }

        return new PaletteInsertionPlan(
            next,
            frontCount,
            movedFromOld,
            evicted,
            dropped,
            counts.Count);
    }

    public static IReadOnlyList<string> BuildEmptySlots(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var slots = new string[capacity];
        Array.Fill(slots, string.Empty);
        return slots;
    }
}

public readonly record struct PaletteInsertionPlan(
    IReadOnlyList<string> Slots,
    int Added,
    int MovedFromOld,
    int Evicted,
    int Dropped,
    int ChartColorCount);
