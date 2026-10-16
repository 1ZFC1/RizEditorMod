namespace RhythmEditorMod.Core;

/// <summary>
/// Uniform zoom stepping for the editor (2026-10-15 change): additive 10 percentage points between
/// 40 % and 200 % - 17 stops: 40, 50, … 100, 110, … 200. The previous behaviour multiplied by a factor
/// (1.12 per notch) and stopped at 100 %.
/// </summary>
public static class ZoomStepping
{
    public const float Minimum = 0.4f;

    public const float Maximum = 2f;

    /// <summary>One wheel notch moves the zoom by this much (10 percentage points).</summary>
    public const float Step = 0.1f;

    /// <summary>
    /// The zoom after moving <paramref name="notches"/> steps (negative shrinks). The result is snapped
    /// to the 10 % grid first, so repeated stepping cannot drift off it, and then clamped to the range.
    /// </summary>
    public static float Next(float current, int notches)
    {
        var clamped = Clamp(current);
        if (notches == 0)
            return clamped;

        var next = clamped + Step * notches;
        next = MathF.Round(next * 10f) / 10f;
        return Clamp(next);
    }

    /// <summary>Wheel movement converted to whole steps; a movement smaller than a notch still counts as one.</summary>
    public static int Notches(float scrollDelta)
    {
        if (!float.IsFinite(scrollDelta) || Math.Abs(scrollDelta) < 0.01f)
            return 0;

        var notches = (int)Math.Round(scrollDelta, MidpointRounding.AwayFromZero);
        return notches == 0 ? Math.Sign(scrollDelta) : notches;
    }

    public static float Clamp(float value) =>
        !float.IsFinite(value) ? 1f : Math.Clamp(value, Minimum, Maximum);

    /// <summary>True at exactly 100 %, i.e. when the editor's own projection must be left alone.</summary>
    public static bool IsNeutral(float value) => Math.Abs(Clamp(value) - 1f) < 1e-4f;
}
