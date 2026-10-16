namespace RhythmEditorMod.Core;

/// <summary>
/// Sampling for the plugin's extra easing curves.
///
/// The editor's own easings are a fixed 19 x 101 table queried by <c>GetEase</c>, and the chart
/// only stores an index that the game itself interprets, so an extra curve cannot become a new
/// index: it has to be baked into ordinary Linear key points. See
/// <c>docs/features/EXTRA_EASING.md</c> and DECISIONS D-023.
///
/// Every curve here is an easing function <c>f: [0,1] -&gt; R</c> with <c>f(0) = 0</c> and
/// <c>f(1) = 1</c>, applied the same way the editor's own easings are:
/// <code>
/// value(u) = startValue + (endValue - startValue) * f(u)
/// </code>
/// so the first and last values stay exact and the neighbouring segments are untouched. The three
/// elastic curves come from the standard set (the same family as the editor's In/Out/InOut
/// easings); <c>Out</c> is the formula the user supplied, <c>In</c> mirrors it, and <c>InOut</c>
/// is point symmetric about (0.5, 0.5).
///
/// Samples are placed **adaptively**: an interval is bisected while the curve's midpoint deviates
/// from the chord by more than the allowed tolerance, so sharp parts get many nodes and flat parts
/// very few. A grid-locked sampler was tried first and failed loudly: at 2 beats / 1-4 beat
/// subdivision / 4 cycles every sample landed exactly on a zero of the sine, so the baked curve
/// came out perfectly straight (see the failure record in D-023).
/// </summary>
public static class EaseFit
{
    /// <summary>Chord error allowed per interval, as a fraction of the segment's value change.</summary>
    public const double DefaultTolerance = 0.01;

    /// <summary>Bisection depth cap; the sample cap below normally stops it first.</summary>
    public const int MaxDepth = 12;

    /// <summary>Above this the caller is expected to warn before applying (D-011).</summary>
    public const int LargeSampleWarning = 4096;

    public const double BeatTolerance = 1e-5;

    private const double AmplitudeEpsilon = 1e-9;
    private const double TwoPiOverThree = 2.0 * Math.PI / 3.0;
    private const double TwoPiOverFourAndAHalf = 2.0 * Math.PI / 4.5;

    public readonly record struct Sample(double Beat, double Value);

    /// <summary>
    /// Elastic that starts slowly, winds up backwards first, and snaps onto the end value.
    /// <c>-2^(10x-10) * sin((10x - 10.75) * 2pi/3)</c>, the mirror of <see cref="OutElastic"/>.
    /// </summary>
    public static double InElastic(double x)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        return -Math.Pow(2, 10 * x - 10) * Math.Sin((x * 10 - 10.75) * TwoPiOverThree);
    }

    /// <summary>
    /// The curve supplied by the user: overshoots past the end value, then settles onto it.
    /// <c>2^(-10x) * sin((10x - 0.75) * 2pi/3) + 1</c>.
    /// </summary>
    public static double OutElastic(double x)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        return Math.Pow(2, -10 * x) * Math.Sin((x * 10 - 0.75) * TwoPiOverThree) + 1;
    }

    /// <summary>
    /// Elastic on both ends: winds up backwards, crosses the middle exactly at (0.5, 0.5), then
    /// overshoots the end value and settles. Uses its own period <c>2pi/4.5</c>.
    /// </summary>
    public static double InOutElastic(double x)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        var argument = (20 * x - 11.125) * TwoPiOverFourAndAHalf;
        return x < 0.5
            ? -(Math.Pow(2, 20 * x - 10) * Math.Sin(argument)) / 2
            : (Math.Pow(2, -20 * x + 10) * Math.Sin(argument)) / 2 + 1;
    }

    /// <summary>
    /// How far the curve travels past its own endpoints, as a fraction of the segment change. The
    /// elastic curves deliberately overshoot; this is reported to the user instead of an amplitude
    /// setting.
    /// </summary>
    public static double MaxExcursion(Func<double, double> curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        var excursion = 0.0;
        for (var step = 0; step <= 4000; step++)
        {
            var value = curve(step / 4000.0);
            excursion = Math.Max(excursion, Math.Max(value - 1.0, -value));
        }

        return excursion;
    }

    /// <summary>
    /// Uniform samples a fixed grid would need to reach the same chord tolerance. Used to show that
    /// adaptive placement is cheaper, and to keep that property under test.
    /// </summary>
    public static int EstimateUniformCount(Func<double, double> curve, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (tolerance <= 0)
            return 0;

        const int steps = 4000;
        var step = 1.0 / steps;
        var maxSecond = 0.0;
        for (var index = 1; index < steps; index++)
        {
            var x = index * step;
            var second = Math.Abs(curve(x + step) - 2 * curve(x) + curve(x - step)) / (step * step);
            maxSecond = Math.Max(maxSecond, second);
        }

        if (maxSecond <= 0)
            return 0;

        var spacing = Math.Sqrt(8.0 * tolerance / maxSecond);
        return spacing <= 0 ? LargeSampleWarning : (int)Math.Ceiling(1.0 / spacing);
    }

    /// <summary>Interior samples of one segment, in beat order, placed where the curve bends.</summary>
    public static IReadOnlyList<Sample> SampleCurve(
        double startBeat,
        double startValue,
        double endBeat,
        double endValue,
        Func<double, double> curve,
        double toleranceRatio,
        out string note)
    {
        note = string.Empty;
        if (endBeat <= startBeat + BeatTolerance)
        {
            note = "这一段没有下一节点（区间长度为零），未生成节点。";
            return Array.Empty<Sample>();
        }

        var delta = endValue - startValue;
        if (Math.Abs(delta) <= AmplitudeEpsilon)
        {
            note = "该段首尾数值相同，曲线会全程停在同一个值上，未生成节点；请选择首尾有位移的区间。";
            return Array.Empty<Sample>();
        }

        var tolerance = Math.Max(toleranceRatio, 1e-9);
        var positions = CollectBreakpoints(curve, tolerance, out var truncated);
        if (positions.Count == 0)
        {
            note = "曲线在该容差下已是直线，未生成节点。";
            return Array.Empty<Sample>();
        }

        var span = endBeat - startBeat;
        var samples = new List<Sample>(positions.Count);
        foreach (var u in positions)
        {
            var beat = startBeat + span * u;
            var value = startValue + delta * curve(u);
            if (beat <= startBeat + BeatTolerance || beat >= endBeat - BeatTolerance)
                continue;
            if (!double.IsFinite(beat) || !double.IsFinite(value))
            {
                note = "采样过程中出现非有限数值，已停止生成。";
                break;
            }

            samples.Add(new Sample(beat, value));
        }

        if (truncated || samples.Count > LargeSampleWarning)
        {
            note = $"预计生成 {samples.Count} 个节点，超过 {LargeSampleWarning} 的建议上限；仍会照样生成，请确认这是有意的（可加大容差或缩短区间）。";
        }

        return samples;
    }

    /// <summary>
    /// Adaptive bisection: split every interval whose midpoint is further from the chord than the
    /// tolerance. This is what makes sampling follow the curve instead of the beat grid.
    /// </summary>
    private static List<double> CollectBreakpoints(Func<double, double> curve, double tolerance, out bool truncated)
    {
        truncated = false;
        var points = new List<double> { 0.0, 1.0 };
        var pending = new Stack<(double From, double To, int Depth)>();
        pending.Push((0.0, 1.0, 0));

        while (pending.Count > 0)
        {
            var (from, to, depth) = pending.Pop();
            if (depth >= MaxDepth || points.Count >= LargeSampleWarning)
            {
                truncated = true;
                continue;
            }

            var middle = (from + to) / 2.0;
            var fromValue = curve(from);
            var toValue = curve(to);

            // Probe the quartiles as well as the midpoint. A single midpoint test has a blind spot
            // for point-symmetric curves: InOut Elastic passes exactly through the chord midpoint
            // at u = 0.5, so a midpoint-only test would call the whole curve straight and bake
            // nothing at all (caught by the "inOut elastic is point symmetric" test).
            var maxDeviation = 0.0;
            for (var probe = 1; probe <= 3; probe++)
            {
                var t = probe / 4.0;
                var value = curve(from + (to - from) * t);
                var chord = fromValue + (toValue - fromValue) * t;
                maxDeviation = Math.Max(maxDeviation, Math.Abs(value - chord));
            }

            if (maxDeviation <= tolerance)
                continue;

            points.Add(middle);
            pending.Push((from, middle, depth + 1));
            pending.Push((middle, to, depth + 1));
        }

        points.Sort();
        var unique = new List<double>(points.Count);
        foreach (var point in points)
        {
            if (unique.Count == 0 || point - unique[^1] > BeatTolerance)
                unique.Add(point);
        }

        // The endpoints themselves are not samples: they already exist as the segment's two keys.
        unique.RemoveAll(point => point <= BeatTolerance || point >= 1.0 - BeatTolerance);
        return unique;
    }
}
