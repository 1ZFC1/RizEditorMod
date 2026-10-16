namespace RhythmEditorMod.Core;

public readonly record struct CanvasEventClipboardPoint(
    double Beat,
    double Value,
    int EaseType,
    double FloorPosition);

public static class CanvasEventEditing
{
    public static double SnapToSubdivision(double value, int subdivision)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "The value to snap must be finite.");
        if (subdivision <= 0)
            throw new ArgumentOutOfRangeException(nameof(subdivision), "The subdivision must be positive.");

        return Math.Round(value * subdivision, MidpointRounding.ToEven) / subdivision;
    }

    public static IReadOnlyList<CanvasEventClipboardPoint> CreatePastePoints(
        IReadOnlyList<CanvasEventClipboardPoint> source,
        double targetBeat,
        double targetValue,
        bool mirrorHorizontally)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count == 0)
            return Array.Empty<CanvasEventClipboardPoint>();
        if (!double.IsFinite(targetBeat))
            throw new ArgumentOutOfRangeException(nameof(targetBeat), "The mouse paste anchor must be finite.");
        if (!double.IsFinite(targetValue))
            throw new ArgumentOutOfRangeException(nameof(targetValue), "The mouse paste anchor must be finite.");

        var anchor = source[0];
        if (!double.IsFinite(anchor.Beat) || !double.IsFinite(anchor.Value))
            throw new ArgumentOutOfRangeException(nameof(source), "Canvas-event coordinates must be finite.");
        var beatOffset = targetBeat - anchor.Beat;
        var horizontalDirection = mirrorHorizontally ? -1.0 : 1.0;

        var result = new CanvasEventClipboardPoint[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            var point = source[index];
            if (!double.IsFinite(point.Beat)
                || !double.IsFinite(point.Value)
                || !double.IsFinite(point.FloorPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(source), "Canvas-event values must be finite.");
            }

            result[index] = point with
            {
                Beat = point.Beat + beatOffset,
                Value = targetValue + horizontalDirection * (point.Value - anchor.Value),
            };
        }
        return result;
    }
}
