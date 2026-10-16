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
    public static IReadOnlyList<Rgba32> UniqueInOrder(IEnumerable<Rgba32> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var seen = new HashSet<uint>();
        var result = new List<Rgba32>();
        foreach (var color in colors)
        {
            var key = Pack(color);
            if (!seen.Add(key))
                continue;
            result.Add(color);
        }

        return result;
    }

    public static PaletteMergeResult MergeIntoSlots(
        IReadOnlyList<string?> existingSlots,
        IReadOnlyList<Rgba32> chartColors)
    {
        ArgumentNullException.ThrowIfNull(existingSlots);
        ArgumentNullException.ThrowIfNull(chartColors);

        var next = existingSlots.Select(slot => slot?.Trim() ?? string.Empty).ToArray();
        var occupied = new HashSet<uint>();
        for (var index = 0; index < next.Length; index++)
        {
            if (Rgba32.IsEmptyPaletteSlot(next[index]))
            {
                next[index] = string.Empty;
                continue;
            }

            if (Rgba32.TryParseHex(next[index], out var existing))
                occupied.Add(Pack(existing));
        }

        var added = 0;
        var duplicate = 0;
        var overflow = 0;
        foreach (var color in UniqueInOrder(chartColors))
        {
            var key = Pack(color);
            if (!occupied.Add(key))
            {
                duplicate++;
                continue;
            }

            var slot = Array.FindIndex(next, Rgba32.IsEmptyPaletteSlot);
            if (slot < 0)
            {
                overflow++;
                occupied.Remove(key);
                continue;
            }

            next[slot] = color.ToHex();
            added++;
        }

        return new PaletteMergeResult(next, added, duplicate, overflow);
    }

    private static uint Pack(Rgba32 color) =>
        ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A;
}

public readonly record struct PaletteMergeResult(
    IReadOnlyList<string> Slots,
    int Added,
    int Duplicate,
    int Overflow);
