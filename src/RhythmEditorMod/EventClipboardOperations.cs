using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

internal static class EventClipboardOperations
{
    private const float BeatTolerance = 1e-5f;

    /// <summary>
    /// Copies (or cuts) the colour events of the current line inside the drag's beat range.
    /// Which of the two arrays is taken is decided by the caller from the selection's
    /// horizontal position, never from ColorEditor.editingLineColor: that Interop property
    /// throws NullReferenceException at runtime (see docs/DECISIONS.md D-019).
    /// </summary>
    public static bool TryCopyColorRange(
        ColorEditor editor,
        float firstBeat,
        float secondBeat,
        bool cut,
        bool includeLineColor,
        bool includeJudgeRing,
        out ColorEventClipboardPoint[] lineColorClipboard,
        out ColorEventClipboardPoint[] judgeRingClipboard,
        out string status)
    {
        lineColorClipboard = Array.Empty<ColorEventClipboardPoint>();
        judgeRingClipboard = Array.Empty<ColorEventClipboardPoint>();
        status = string.Empty;
        try
        {
            var project = RequireProject(editor);
            var lineIndex = editor.nowLineIndex;
            var lineColorPoints = includeLineColor
                ? RequireColorPoints(project, lineIndex, lineColor: true, create: false)
                : null;
            var judgeRingPoints = includeJudgeRing
                ? RequireColorPoints(project, lineIndex, lineColor: false, create: false)
                : null;

            var lineColorSelected = lineColorPoints == null
                ? new List<int>()
                : IndicesInRange(lineColorPoints, firstBeat, secondBeat);
            var judgeRingSelected = judgeRingPoints == null
                ? new List<int>()
                : IndicesInRange(judgeRingPoints, firstBeat, secondBeat);

            if (lineColorSelected.Count == 0 && judgeRingSelected.Count == 0)
            {
                status = $"线 {lineIndex} 的第 {Math.Min(firstBeat, secondBeat):0.###}–{Math.Max(firstBeat, secondBeat):0.###} 拍内没有可复制的颜色事件，剪贴板未改变。";
                return true;
            }

            lineColorClipboard = BuildClipboard(lineColorPoints, lineColorSelected);
            judgeRingClipboard = BuildClipboard(judgeRingPoints, judgeRingSelected);

            if (cut)
            {
                // Both arrays are emptied inside one Stepping so undo restores them together.
                var removeLineColor = lineColorSelected.ToArray();
                var removeJudgeRing = judgeRingSelected.ToArray();
                Array.Reverse(removeLineColor);
                Array.Reverse(removeJudgeRing);
                System.Action mutation = () =>
                {
                    if (lineColorPoints != null)
                    {
                        foreach (var index in removeLineColor)
                            lineColorPoints.RemoveAt(index);
                    }

                    if (judgeRingPoints != null)
                    {
                        foreach (var index in removeJudgeRing)
                            judgeRingPoints.RemoveAt(index);
                    }
                };
                project.editorControl.Stepping((Il2CppSystem.Action)mutation);
                Refresh(editor);
            }

            var parts = new List<string>();
            if (lineColorClipboard.Length > 0)
                parts.Add($"{lineColorClipboard.Length} 个线色覆盖事件");
            if (judgeRingClipboard.Length > 0)
                parts.Add($"{judgeRingClipboard.Length} 个判定环事件");
            status = $"{(cut ? "已剪切并复制" : "已复制")}线 {lineIndex} 的 {string.Join("、", parts)}。";
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            status = $"{(cut ? "剪切" : "复制")}颜色事件失败：{error.Message}";
            return true;
        }
    }

    private static ColorEventClipboardPoint[] BuildClipboard(
        Il2CppSystem.Collections.Generic.List<ColorKeyPoint>? points,
        IReadOnlyList<int> selected)
    {
        if (points == null || selected.Count == 0)
            return Array.Empty<ColorEventClipboardPoint>();

        var clipboard = new ColorEventClipboardPoint[selected.Count];
        for (var index = 0; index < selected.Count; index++)
        {
            var point = points[selected[index]];
            clipboard[index] = new ColorEventClipboardPoint(
                point.time,
                point.startColor.r, point.startColor.g, point.startColor.b, point.startColor.a,
                point.endColor.r, point.endColor.g, point.endColor.b, point.endColor.a);
        }

        return clipboard;
    }

    /// <summary>
    /// Pastes the arrays the last copy actually filled. Which arrays those are comes from
    /// the copy, not from ColorEditor.editingLineColor (see D-019).
    /// </summary>
    public static string PasteColorClipboard(
        ColorEditor editor,
        IReadOnlyList<ColorEventClipboardPoint> lineColorClipboard,
        IReadOnlyList<ColorEventClipboardPoint> judgeRingClipboard,
        bool alignToCurrentBeat,
        bool includeLineColor,
        bool includeJudgeRing)
    {
        var pasteLineColor = includeLineColor && lineColorClipboard.Count > 0;
        var pasteJudgeRing = includeJudgeRing && judgeRingClipboard.Count > 0;
        if (!pasteLineColor && !pasteJudgeRing)
            throw new InvalidOperationException("颜色事件剪贴板为空，请先框选复制或剪切。");

        var project = RequireProject(editor);
        var lineIndex = editor.nowLineIndex;
        var lineColorPoints = pasteLineColor
            ? RequireColorPoints(project, lineIndex, lineColor: true, create: true)
            : null;
        var judgeRingPoints = pasteJudgeRing
            ? RequireColorPoints(project, lineIndex, lineColor: false, create: true)
            : null;
        if ((pasteLineColor && lineColorPoints == null) || (pasteJudgeRing && judgeRingPoints == null))
            throw new InvalidOperationException("当前线没有可写入的颜色事件列表。");

        var lineColorPasted = pasteLineColor
            ? OffsetToAnchor(lineColorClipboard, alignToCurrentBeat)
            : Array.Empty<ColorEventClipboardPoint>();
        var judgeRingPasted = pasteJudgeRing
            ? OffsetToAnchor(judgeRingClipboard, alignToCurrentBeat)
            : Array.Empty<ColorEventClipboardPoint>();

        System.Action mutation = () =>
        {
            InsertPasted(lineColorPoints, lineColorPasted);
            InsertPasted(judgeRingPoints, judgeRingPasted);
        };
        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        Refresh(editor);

        var mode = alignToCurrentBeat ? "最早事件对齐播放头" : "保持原拍数";
        var parts = new List<string>();
        if (lineColorPasted.Length > 0)
            parts.Add($"{lineColorPasted.Length} 个线色覆盖事件");
        if (judgeRingPasted.Length > 0)
            parts.Add($"{judgeRingPasted.Length} 个判定环事件");
        return $"已向线 {lineIndex} 粘贴 {string.Join("、", parts)}（{mode}）。同拍已有事件已覆盖，其余顺序保留。";
    }

    private static ColorEventClipboardPoint[] OffsetToAnchor(
        IReadOnlyList<ColorEventClipboardPoint> clipboard,
        bool alignToCurrentBeat)
    {
        var delta = alignToCurrentBeat
            ? ProgressControl.GetBeats() - clipboard.Min(point => point.Beat)
            : 0d;
        var pasted = new ColorEventClipboardPoint[clipboard.Count];
        for (var index = 0; index < clipboard.Count; index++)
            pasted[index] = clipboard[index] with { Beat = clipboard[index].Beat + delta };
        return pasted;
    }

    private static void InsertPasted(
        Il2CppSystem.Collections.Generic.List<ColorKeyPoint>? points,
        ColorEventClipboardPoint[] pasted)
    {
        if (points == null || pasted.Length == 0)
            return;

        // Sized for the final length: the array grows by one for every inserted event.
        var beats = new double[points.Count + pasted.Length];
        for (var index = 0; index < pasted.Length; index++)
        {
            var point = pasted[index];
            var beat = (float)point.Beat;
            var startColor = Channels(point.StartR, point.StartG, point.StartB, point.StartA);
            var endColor = Channels(point.EndR, point.EndG, point.EndB, point.EndA);

            var count = points.Count;
            for (var scan = 0; scan < count; scan++)
                beats[scan] = points[scan].time;
            var current = new ArraySegment<double>(beats, 0, count);

            var matches = ColorEventPasteTargeting.FindSameBeatIndices(current, beat, BeatTolerance);
            int targetIndex;
            if (matches.Count == 0)
            {
                targetIndex = ColorEventPasteTargeting.FindInsertIndex(current, beat, BeatTolerance);
                points.Insert(targetIndex, new ColorKeyPoint
                {
                    time = beat,
                    startColor = startColor,
                    endColor = endColor,
                });
            }
            else
            {
                // Collapse every event already on this beat into one. Charts touched by the
                // earlier duplicate-creating builds can hold several, and the LAST index wins
                // the same-time priority rule, so overwriting only the first left the visible
                // colour to a stale entry (the reported purple-turns-white). D-020.
                targetIndex = matches[0];
                var survivor = points[targetIndex];
                survivor.startColor = startColor;
                survivor.endColor = endColor;
                for (var remove = matches.Count - 1; remove >= 1; remove--)
                    points.RemoveAt(matches[remove]);
            }

            if (index == pasted.Length - 1)
            {
                // endColor means "the colour at the NEXT event's time", so the source's last
                // endColor describes a segment outside the copied range. Re-point it at what
                // follows in the destination, otherwise the paste paints a foreign colour
                // (the reported purple-turns-white). D-020.
                var successor = targetIndex + 1;
                points[targetIndex].endColor = successor < points.Count
                    ? points[successor].startColor
                    : startColor;
            }
        }
    }

    public static LineClipboardEntry[] CaptureLines(LineEditor editor)
    {
        return CaptureLineList(editor.lineClipboard);
    }

    public static LineClipboardEntry[] CaptureMirrorLines(LineEditor editor)
    {
        return CaptureLineList(editor.mirrorLineClipboard);
    }

    /// <summary>Captures whole <see cref="Line"/> objects, e.g. the plugin's own box selection.</summary>
    public static LineClipboardEntry[] CaptureLines(IReadOnlyList<Line> lines)
    {
        var captured = new LineClipboardEntry[lines.Count];
        for (var index = 0; index < lines.Count; index++)
            captured[index] = CaptureLine(lines[index]);
        return captured;
    }

    public static string ReplaceLineClipboard(LineEditor editor, IReadOnlyList<LineClipboardEntry> lines)
    {
        return ReplaceLineList(editor, lineClipboard: true, lines);
    }

    public static string ReplaceMirrorLineClipboard(LineEditor editor, IReadOnlyList<LineClipboardEntry> lines)
    {
        return ReplaceLineList(editor, lineClipboard: false, lines);
    }

    private static LineClipboardEntry[] CaptureLineList(Il2CppSystem.Collections.Generic.List<Line>? lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<LineClipboardEntry>();

        var captured = new LineClipboardEntry[lines.Count];
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            captured[lineIndex] = CaptureLine(lines[lineIndex]);
        return captured;
    }

    private static string ReplaceLineList(
        LineEditor editor,
        bool lineClipboard,
        IReadOnlyList<LineClipboardEntry> lines)
    {
        var list = lineClipboard ? editor.lineClipboard : editor.mirrorLineClipboard;
        if (list == null)
        {
            list = new Il2CppSystem.Collections.Generic.List<Line>();
            if (lineClipboard)
                editor.lineClipboard = list;
            else
                editor.mirrorLineClipboard = list;
        }
        else
        {
            list.Clear();
        }

        foreach (var source in lines)
            list.Add(CreateLine(source));
        return $"已用系统剪切板替换内部线剪贴板，共 {lines.Count} 条线。";
    }

    internal static LineClipboardEntry CaptureLine(Line line)
    {
        var points = Array.Empty<LinePointClipboardEntry>();
        if (line.linePoints != null)
        {
            points = new LinePointClipboardEntry[line.linePoints.Count];
            for (var index = 0; index < line.linePoints.Count; index++)
            {
                var point = line.linePoints[index];
                points[index] = new LinePointClipboardEntry(
                    point.time,
                    point.xPosition,
                    point.easeType,
                    point.canvasIndex,
                    point.floorPosition,
                    point.color.r,
                    point.color.g,
                    point.color.b,
                    point.color.a);
            }
        }

        var notes = Array.Empty<NoteClipboardEntry>();
        if (line.notes != null)
        {
            notes = new NoteClipboardEntry[line.notes.Count];
            for (var index = 0; index < line.notes.Count; index++)
            {
                var note = line.notes[index];
                var other = Array.Empty<double>();
                if (note.otherInformations != null)
                {
                    other = new double[note.otherInformations.Count];
                    for (var otherIndex = 0; otherIndex < note.otherInformations.Count; otherIndex++)
                        other[otherIndex] = note.otherInformations[otherIndex];
                }

                notes[index] = new NoteClipboardEntry(note.type, note.time, note.floorPosition, other);
            }
        }

        return new LineClipboardEntry(
            points,
            notes,
            CaptureColorKeyPoints(line.judgeRingColor),
            CaptureColorKeyPoints(line.lineColor));
    }

    /// <summary>
    /// Copies a line's colour array. <c>Line</c> has exactly four data fields and these two are half
    /// of them, so leaving them out made the editor's own line clipboard lose colour information
    /// when Ctrl+D restored a snapshot of it (2026-09-23 bug report).
    /// </summary>
    private static ColorEventClipboardPoint[] CaptureColorKeyPoints(
        Il2CppSystem.Collections.Generic.List<ColorKeyPoint>? source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<ColorEventClipboardPoint>();

        var captured = new ColorEventClipboardPoint[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            var point = source[index];
            var start = point.startColor;
            var end = point.endColor;
            captured[index] = new ColorEventClipboardPoint(
                point.time,
                start.r,
                start.g,
                start.b,
                start.a,
                end.r,
                end.g,
                end.b,
                end.a);
        }

        return captured;
    }

    private static Il2CppSystem.Collections.Generic.List<ColorKeyPoint> CreateColorKeyPoints(
        IReadOnlyList<ColorEventClipboardPoint> source)
    {
        var created = new Il2CppSystem.Collections.Generic.List<ColorKeyPoint>();
        foreach (var point in source)
        {
            created.Add(new ColorKeyPoint
            {
                time = (float)point.Beat,
                startColor = Channels(point.StartR, point.StartG, point.StartB, point.StartA),
                endColor = Channels(point.EndR, point.EndG, point.EndB, point.EndA),
            });
        }

        return created;
    }

    internal static Line CreateLine(LineClipboardEntry source)
    {
        var line = new Line();
        line.linePoints = new Il2CppSystem.Collections.Generic.List<LinePoint>();
        foreach (var point in source.Points)
        {
            line.linePoints.Add(new LinePoint
            {
                time = (float)point.Time,
                xPosition = (float)point.XPosition,
                easeType = point.EaseType,
                canvasIndex = point.CanvasIndex,
                floorPosition = (float)point.FloorPosition,
                color = Channels(point.R, point.G, point.B, point.A),
            });
        }

        line.notes = new Il2CppSystem.Collections.Generic.List<Note>();
        foreach (var note in source.Notes)
        {
            var created = new Note
            {
                type = note.Type,
                time = (float)note.Time,
                floorPosition = (float)note.FloorPosition,
            };
            created.otherInformations = new Il2CppSystem.Collections.Generic.List<float>();
            foreach (var value in note.Other)
                created.otherInformations.Add((float)value);
            line.notes.Add(created);
        }

        line.judgeRingColor = CreateColorKeyPoints(source.JudgeRingColors);
        line.lineColor = CreateColorKeyPoints(source.LineColors);
        return line;
    }

    private static List<int> IndicesInRange(
        Il2CppSystem.Collections.Generic.List<ColorKeyPoint> points,
        float firstBeat,
        float secondBeat)
    {
        var beats = new double[points.Count];
        for (var index = 0; index < points.Count; index++)
            beats[index] = points[index].time;
        return SpeedEditing.FindClosedRangeIndices(beats, firstBeat, secondBeat).ToList();
    }

    private static ProjectInformation RequireProject(ColorEditor editor)
    {
        var project = editor.projectInformation;
        if (project == null || project.chart == null || project.editorControl == null)
            throw new InvalidOperationException("当前没有已打开且可编辑的谱面。");
        return project;
    }

    private static Il2CppSystem.Collections.Generic.List<ColorKeyPoint>? RequireColorPoints(
        ProjectInformation project,
        int lineIndex,
        bool lineColor,
        bool create)
    {
        var lines = project.chart.lines;
        if (lines == null || lineIndex < 0 || lineIndex >= lines.Count || lines[lineIndex] == null)
            throw new InvalidOperationException($"线 {lineIndex} 不存在。");

        var line = lines[lineIndex];
        var points = lineColor ? line.lineColor : line.judgeRingColor;
        if (points != null || !create)
            return points;

        points = new Il2CppSystem.Collections.Generic.List<ColorKeyPoint>();
        if (lineColor)
            line.lineColor = points;
        else
            line.judgeRingColor = points;
        return points;
    }

    private static void Refresh(ColorEditor editor)
    {
        editor.ReloadTags();
        editor.MoveTags();
    }

    /// <summary>
    /// The chart stores colour channels as bytes: the interop exposes ColorKeyPoint.startColor /
    /// endColor and LinePoint.color as UnityEngine.Color32, and the JSON holds 0–255 values.
    /// Building a UnityEngine.Color here and letting the implicit conversion run would clamp
    /// every channel to [0,1] and scale it to 255 — turning any real colour into white.
    /// </summary>
    private static Color32 Channels(double red, double green, double blue, double alpha) =>
        new(ToChannel(red), ToChannel(green), ToChannel(blue), ToChannel(alpha));

    private static byte ToChannel(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0d, 255d);
}
