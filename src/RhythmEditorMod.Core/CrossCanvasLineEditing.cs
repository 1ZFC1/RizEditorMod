namespace RhythmEditorMod.Core;

/// <summary>
/// Pure helpers for the cross-canvas line clipboard (2026-09-24 request). A line's
/// <c>linePoints[].canvasIndex</c> is per node, so one line can span several canvases - see
/// <c>baseline/json文件各项含义.txt</c> ("一条线的节点可以在不同画布上") and
/// <c>baseline/CROSS_CANVAS_HOLD_EXPERIMENT.md</c>. The original editor box-selects by rendered
/// groups and its paste rewrites every point onto the current canvas, so such lines could neither be
/// selected nor pasted unchanged; these helpers carry the data side of the plugin's own path.
/// </summary>
public static class CrossCanvasLineEditing
{
    /// <summary>Hold notes keep the canvas used for their end position in <c>otherInformations[1]</c>.</summary>
    public const int HoldType = 2;

    /// <summary>Distinct canvases a line uses, in point order.</summary>
    public static List<int> DistinctCanvases(LineClipboardEntry line)
    {
        var canvases = new List<int>();
        foreach (var point in line.Points)
        {
            if (!canvases.Contains(point.CanvasIndex))
                canvases.Add(point.CanvasIndex);
        }

        return canvases;
    }

    /// <summary>How many of the lines use more than one canvas.</summary>
    public static int CountCrossCanvas(IReadOnlyList<LineClipboardEntry> lines)
    {
        var count = 0;
        foreach (var line in lines)
        {
            if (DistinctCanvases(line).Count > 1)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Value of an ease-aware key point sequence at a beat - used for the canvas x movement
    /// (<c>CanvasMove.xPositionKeyPoints</c>), whose <c>easeType</c> describes the motion from this
    /// node to the next. <paramref name="ease"/> is the original's own curve
    /// (<c>GetEase.GetEaseWithProgress</c>), so the plugin never re-implements it.
    /// </summary>
    public static bool TryEvaluateKeyPoints(
        IReadOnlyList<CanvasEventClipboardPoint> points,
        double beat,
        Func<int, double, double> ease,
        out double value)
    {
        value = 0d;
        if (points.Count == 0)
            return false;

        var index = -1;
        for (var candidate = 0; candidate < points.Count; candidate++)
        {
            if (points[candidate].Beat <= beat && (index < 0 || points[candidate].Beat > points[index].Beat))
                index = candidate;
        }

        if (index < 0)
        {
            // Before the first node: the canvas keeps its first value.
            value = points[0].Value;
            return true;
        }

        var start = points[index];
        var next = -1;
        for (var candidate = 0; candidate < points.Count; candidate++)
        {
            if (points[candidate].Beat > start.Beat && (next < 0 || points[candidate].Beat < points[next].Beat))
                next = candidate;
        }

        if (next < 0)
        {
            value = start.Value;
            return true;
        }

        var span = points[next].Beat - start.Beat;
        if (span <= 0d)
        {
            value = start.Value;
            return true;
        }

        var progress = (beat - start.Beat) / span;
        var eased = ease(start.EaseType, progress);
        value = start.Value + (points[next].Value - start.Value) * eased;
        return true;
    }

    /// <summary>
    /// Places clipboard lines the way the original paste does - the anchor lands on the mouse's beat and x - and adds the canvas offset the original lacks: every
    /// point's canvas moves by the same amount, so a line spanning canvases 1-3 copied on canvas 2 and
    /// pasted on canvas 4 comes back as 3-5. Hold end times follow the time shift, Hold end canvases
    /// follow the canvas shift, and the colour key points move with the line.
    ///
    /// <paramref name="mirror"/> reflects the shape about the anchor, which is the same result as the
    /// original's two steps composed (it negates every <c>xPosition</c> when it fills the mirror
    /// clipboard, then pastes aligned to the mouse).
    ///
    /// The whole placement is refused (with a reason) when any resulting canvas does not exist, so a
    /// paste can never write half-valid data.
    /// </summary>
    public static bool TryPlace(
        IReadOnlyList<LineClipboardEntry> lines,
        int canvasOffset,
        double targetBeat,
        double anchorTargetX,
        bool mirror,
        IReadOnlyCollection<int> existingCanvases,
        out LineClipboardEntry[] result,
        out string error)
    {
        result = Array.Empty<LineClipboardEntry>();
        error = string.Empty;

        if (lines.Count == 0)
        {
            error = "剪贴板为空。";
            return false;
        }

        if (!TryFindAnchor(lines, out var anchor))
        {
            error = "剪贴板里没有任何节点，无法确定粘贴锚点。";
            return false;
        }

        if (existingCanvases.Count == 0)
        {
            error = "谱面没有画布数据，无法换算画布编号。";
            return false;
        }

        var deltaTime = targetBeat - anchor.Time;
        var deltaX = anchorTargetX - anchor.XPosition;

        var placed = new LineClipboardEntry[lines.Count];
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];

            var points = new LinePointClipboardEntry[line.Points.Count];
            for (var pointIndex = 0; pointIndex < line.Points.Count; pointIndex++)
            {
                var point = line.Points[pointIndex];
                var canvas = point.CanvasIndex + canvasOffset;
                if (!existingCanvases.Contains(canvas))
                {
                    error = $"第 {lineIndex + 1} 条线的节点画布 {point.CanvasIndex} 偏移 {canvasOffset:+#;-#;0} 后为 {canvas}，"
                        + "谱面中没有这个画布，已放弃粘贴。";
                    return false;
                }

                var x = mirror
                    ? anchorTargetX - (point.XPosition - anchor.XPosition)
                    : point.XPosition + deltaX;
                points[pointIndex] = point with
                {
                    Time = point.Time + deltaTime,
                    XPosition = x,
                    CanvasIndex = canvas,
                };
            }

            var notes = new NoteClipboardEntry[line.Notes.Count];
            for (var noteIndex = 0; noteIndex < line.Notes.Count; noteIndex++)
            {
                var note = line.Notes[noteIndex];
                var other = note.Other.ToArray();
                if (note.Type == HoldType && other.Length >= 2)
                {
                    other[0] += deltaTime;
                    other[1] = Math.Round(other[1]) + canvasOffset;
                }

                notes[noteIndex] = note with { Time = note.Time + deltaTime, Other = other };
            }

            placed[lineIndex] = line with
            {
                Points = points,
                Notes = notes,
                JudgeRingColors = ShiftColorBeats(line.JudgeRingColors, deltaTime),
                LineColors = ShiftColorBeats(line.LineColors, deltaTime),
            };
        }

        result = placed;
        return true;
    }

    /// <summary>Two beats closer than this count as the same beat when looking for the anchor.</summary>
    private const double BeatTolerance = 1e-4;

    /// <summary>
    /// The paste anchor: across every line of the clipboard, the node on the earliest beat - and when
    /// several nodes share that beat, the one with the largest x. That is the rule the original's paste
    /// follows for its "earliest event" alignment.
    /// </summary>
    private static bool TryFindAnchor(IReadOnlyList<LineClipboardEntry> lines, out LinePointClipboardEntry anchor)
    {
        anchor = default;
        var found = false;
        foreach (var line in lines)
        {
            foreach (var point in line.Points)
            {
                if (!found)
                {
                    anchor = point;
                    found = true;
                    continue;
                }

                if (point.Time < anchor.Time - BeatTolerance)
                {
                    anchor = point;
                    continue;
                }

                if (Math.Abs(point.Time - anchor.Time) <= BeatTolerance && point.XPosition > anchor.XPosition)
                    anchor = point;
            }
        }

        return found;
    }
    private static ColorEventClipboardPoint[] ShiftColorBeats(
        IReadOnlyList<ColorEventClipboardPoint> points,
        double deltaTime)
    {
        if (points.Count == 0)
            return Array.Empty<ColorEventClipboardPoint>();

        var shifted = new ColorEventClipboardPoint[points.Count];
        for (var index = 0; index < points.Count; index++)
            shifted[index] = points[index] with { Beat = points[index].Beat + deltaTime };
        return shifted;
    }
}
