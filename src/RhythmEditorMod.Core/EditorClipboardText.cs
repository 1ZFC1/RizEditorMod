using System.Globalization;

namespace RhythmEditorMod.Core;

public readonly record struct SpeedClipboardEntry(double Beat, double Value, int EaseType);

public readonly record struct ColorEventClipboardPoint(
    double Beat,
    double StartR,
    double StartG,
    double StartB,
    double StartA,
    double EndR,
    double EndG,
    double EndB,
    double EndA);

public readonly record struct LinePointClipboardEntry(
    double Time,
    double XPosition,
    int EaseType,
    int CanvasIndex,
    double FloorPosition,
    double R,
    double G,
    double B,
    double A);

public readonly record struct NoteClipboardEntry(
    int Type,
    double Time,
    double FloorPosition,
    IReadOnlyList<double> Other);

/// <summary>
/// One line of the chart. <see cref="Line"/> declares exactly four data fields - linePoints, notes,
/// judgeRingColor and lineColor (docs/CHART_FORMAT.md, evidence M1) - so all four are carried here;
/// dropping the two colour arrays used to make Ctrl+D in the line tool delete the line's colour
/// information from the editor's own clipboard (2026-09-23 bug report).
/// </summary>
public readonly record struct LineClipboardEntry(
    IReadOnlyList<LinePointClipboardEntry> Points,
    IReadOnlyList<NoteClipboardEntry> Notes,
    IReadOnlyList<ColorEventClipboardPoint> JudgeRingColors,
    IReadOnlyList<ColorEventClipboardPoint> LineColors)
{
    /// <summary>Points and notes only; no colour events (used by the pre-2026-09-23 text format).</summary>
    public LineClipboardEntry(
        IReadOnlyList<LinePointClipboardEntry> points,
        IReadOnlyList<NoteClipboardEntry> notes)
        : this(points, notes, Array.Empty<ColorEventClipboardPoint>(), Array.Empty<ColorEventClipboardPoint>())
    {
    }
}

/// <summary>
/// Text form of the plugin's in-memory speed and canvas-event clipboards.
/// </summary>
public static class EditorClipboardText
{
    public const string SpeedHeader = "rzemod-clipboard speed 1";
    public const string CanvasEventHeader = "rzemod-clipboard canvas-event 1";
    public const string LineColorHeader = "rzemod-clipboard line-color 1";
    public const string JudgeRingHeader = "rzemod-clipboard judge-ring 1";
    public const string CameraEventHeader = "rzemod-clipboard camera-event 1";
    public const string LinesHeader = "rzemod-clipboard lines 1";

    /// <summary>Markers of the two per-line colour arrays (the counts follow on the same row).</summary>
    private const string LineColorMarker = "lineColor";

    private const string JudgeRingMarker = "judgeRing";

    public static string FormatSpeed(IReadOnlyList<SpeedClipboardEntry> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var lines = new string[points.Count + 1];
        lines[0] = SpeedHeader;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            lines[index + 1] = string.Join(
                ' ',
                Format(point.Beat),
                Format(point.Value),
                point.EaseType.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join('\n', lines);
    }

    public static string FormatCanvasEvents(IReadOnlyList<CanvasEventClipboardPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var lines = new string[points.Count + 1];
        lines[0] = CanvasEventHeader;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            lines[index + 1] = string.Join(
                ' ',
                Format(point.Beat),
                Format(point.Value),
                point.EaseType.ToString(CultureInfo.InvariantCulture),
                Format(point.FloorPosition));
        }

        return string.Join('\n', lines);
    }

    public static bool TryParseSpeed(string? text, out SpeedClipboardEntry[] points, out string error)
    {
        points = Array.Empty<SpeedClipboardEntry>();
        if (!TryReadRows(text, SpeedHeader, 3, "流速", out var rows, out error))
            return false;

        var parsed = new SpeedClipboardEntry[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!TryParseFinite(row[0], out var beat)
                || !TryParseFinite(row[1], out var value)
                || !int.TryParse(row[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var easeType))
            {
                error = $"系统剪切板第 {index + 1} 行不是有效的流速节点。";
                return false;
            }

            if (index > 0 && beat < parsed[index - 1].Beat)
            {
                error = "系统剪切板中的流速节点拍数必须按时间排列。";
                return false;
            }

            parsed[index] = new SpeedClipboardEntry(beat, value, easeType);
        }

        points = parsed;
        return true;
    }

    public static bool TryParseCanvasEvents(string? text, out CanvasEventClipboardPoint[] points, out string error)
    {
        points = Array.Empty<CanvasEventClipboardPoint>();
        if (!TryReadRows(text, CanvasEventHeader, 4, "画布事件", out var rows, out error))
            return false;

        var parsed = new CanvasEventClipboardPoint[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!TryParseFinite(row[0], out var beat)
                || !TryParseFinite(row[1], out var value)
                || !int.TryParse(row[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var easeType)
                || !TryParseFinite(row[3], out var floorPosition))
            {
                error = $"系统剪切板第 {index + 1} 行不是有效的画布事件。";
                return false;
            }

            if (index > 0 && beat < parsed[index - 1].Beat)
            {
                error = "系统剪切板中的画布事件拍数必须按时间排列。";
                return false;
            }

            parsed[index] = new CanvasEventClipboardPoint(beat, value, easeType, floorPosition);
        }

        points = parsed;
        return true;
    }

    public static string FormatColorEvents(string header, IReadOnlyList<ColorEventClipboardPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var lines = new string[points.Count + 1];
        lines[0] = header;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            lines[index + 1] = string.Join(
                ' ',
                Format(point.Beat),
                Format(point.StartR),
                Format(point.StartG),
                Format(point.StartB),
                Format(point.StartA),
                Format(point.EndR),
                Format(point.EndG),
                Format(point.EndB),
                Format(point.EndA));
        }

        return string.Join('\n', lines);
    }

    public static bool TryParseColorEvents(
        string? text,
        string expectedHeader,
        string kindLabel,
        out ColorEventClipboardPoint[] points,
        out string error)
    {
        points = Array.Empty<ColorEventClipboardPoint>();
        if (!TryReadRows(text, expectedHeader, 9, kindLabel, out var rows, out error))
            return false;

        var parsed = new ColorEventClipboardPoint[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!TryParseFinite(row[0], out var beat)
                || !TryChannel(row[1], out var startR)
                || !TryChannel(row[2], out var startG)
                || !TryChannel(row[3], out var startB)
                || !TryChannel(row[4], out var startA)
                || !TryChannel(row[5], out var endR)
                || !TryChannel(row[6], out var endG)
                || !TryChannel(row[7], out var endB)
                || !TryChannel(row[8], out var endA))
            {
                error = $"系统剪切板第 {index + 1} 行不是有效的{kindLabel}。";
                return false;
            }

            parsed[index] = new ColorEventClipboardPoint(
                beat, startR, startG, startB, startA, endR, endG, endB, endA);
        }

        points = parsed;
        return true;
    }

    public static string FormatCameraEvents(IReadOnlyList<CanvasEventClipboardPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var lines = new string[points.Count + 1];
        lines[0] = CameraEventHeader;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            lines[index + 1] = string.Join(
                ' ',
                Format(point.Beat),
                Format(point.Value),
                point.EaseType.ToString(CultureInfo.InvariantCulture),
                Format(point.FloorPosition));
        }

        return string.Join('\n', lines);
    }

    public static bool TryParseCameraEvents(string? text, out CanvasEventClipboardPoint[] points, out string error)
    {
        points = Array.Empty<CanvasEventClipboardPoint>();
        if (!TryReadRows(text, CameraEventHeader, 4, "摄像机事件", out var rows, out error))
            return false;

        var parsed = new CanvasEventClipboardPoint[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!TryParseFinite(row[0], out var beat)
                || !TryParseFinite(row[1], out var value)
                || !int.TryParse(row[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var easeType)
                || !TryParseFinite(row[3], out var floorPosition))
            {
                error = $"系统剪切板第 {index + 1} 行不是有效的摄像机事件。";
                return false;
            }

            parsed[index] = new CanvasEventClipboardPoint(beat, value, easeType, floorPosition);
        }

        points = parsed;
        return true;
    }

    public static string FormatLines(IReadOnlyList<LineClipboardEntry> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var builder = new System.Text.StringBuilder();
        builder.Append(LinesHeader);
        foreach (var line in lines)
        {
            builder.Append('\n').Append("line");
            builder.Append('\n').Append("points ").Append(line.Points.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var point in line.Points)
            {
                builder.Append('\n').Append(string.Join(
                    ' ',
                    Format(point.Time),
                    Format(point.XPosition),
                    point.EaseType.ToString(CultureInfo.InvariantCulture),
                    point.CanvasIndex.ToString(CultureInfo.InvariantCulture),
                    Format(point.FloorPosition),
                    Format(point.R),
                    Format(point.G),
                    Format(point.B),
                    Format(point.A)));
            }

            builder.Append('\n').Append("notes ").Append(line.Notes.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var note in line.Notes)
            {
                builder.Append('\n').Append(string.Join(
                    ' ',
                    note.Type.ToString(CultureInfo.InvariantCulture),
                    Format(note.Time),
                    Format(note.FloorPosition),
                    note.Other.Count.ToString(CultureInfo.InvariantCulture)));
                foreach (var value in note.Other)
                    builder.Append(' ').Append(Format(value));
            }

            AppendColorSection(builder, LineColorMarker, line.LineColors);
            AppendColorSection(builder, JudgeRingMarker, line.JudgeRingColors);
        }

        return builder.ToString();
    }

    /// <summary>Writes one colour array of a line: a marker with a count, then one event per row.</summary>
    private static void AppendColorSection(
        System.Text.StringBuilder builder,
        string marker,
        IReadOnlyList<ColorEventClipboardPoint> points)
    {
        builder.Append('\n').Append(marker).Append(' ').Append(points.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var point in points)
        {
            builder.Append('\n').Append(string.Join(
                ' ',
                Format(point.Beat),
                Format(point.StartR),
                Format(point.StartG),
                Format(point.StartB),
                Format(point.StartA),
                Format(point.EndR),
                Format(point.EndG),
                Format(point.EndB),
                Format(point.EndA)));
        }
    }

    /// <summary>
    /// Reads one optional colour section ("lineColor N" / "judgeRing N" followed by N rows of
    /// 9 fields) from the row after the cursor. An absent section yields an empty array and is not
    /// an error, so clips written by an older build keep working; a present but malformed section is
    /// an error. <paramref name="cursor"/> keeps the same meaning as elsewhere in this parser: it
    /// stays on the last row the section consumed, so the caller's own cursor++ lands on the next
    /// marker.
    /// </summary>
    private static bool TryTakeColorSection(
        string[] rows,
        ref int cursor,
        string expectedMarker,
        out ColorEventClipboardPoint[] points,
        out string error)
    {
        points = Array.Empty<ColorEventClipboardPoint>();
        error = string.Empty;

        // Look ahead without consuming: the section may simply not be there.
        var probe = cursor + 1;
        while (probe < rows.Length && rows[probe].Trim().Length == 0)
            probe++;
        if (probe >= rows.Length)
            return true;

        var markerRow = rows[probe].Trim();
        var separator = markerRow.IndexOf(' ');
        var markerName = separator > 0 ? markerRow[..separator] : markerRow;
        var countText = separator > 0 ? markerRow[(separator + 1)..].Trim() : string.Empty;

        if (markerName == LineColorMarker || markerName == JudgeRingMarker)
        {
            if (markerName != expectedMarker)
            {
                error = $"系统剪切板中的线颜色段顺序不正确（先遇到 {markerName}）。";
                return false;
            }

            if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0)
            {
                error = $"系统剪切板中的 {markerName} 数量不正确。";
                return false;
            }

            cursor = probe;
            points = new ColorEventClipboardPoint[count];
            for (var index = 0; index < count; index++)
            {
                if (!TryTakeFields(rows, ref cursor, 9, out var fields, out error))
                    return false;
                if (!TryParseFinite(fields[0], out var beat)
                    || !TryChannel(fields[1], out var startR)
                    || !TryChannel(fields[2], out var startG)
                    || !TryChannel(fields[3], out var startB)
                    || !TryChannel(fields[4], out var startA)
                    || !TryChannel(fields[5], out var endR)
                    || !TryChannel(fields[6], out var endG)
                    || !TryChannel(fields[7], out var endB)
                    || !TryChannel(fields[8], out var endA))
                {
                    error = $"系统剪切板中的 {markerName} 事件格式不正确。";
                    return false;
                }

                points[index] = new ColorEventClipboardPoint(
                    beat, startR, startG, startB, startA, endR, endG, endB, endA);
            }
        }

        return true;
    }

    public static bool TryParseLines(string? text, out LineClipboardEntry[] lines, out string error)
    {
        lines = Array.Empty<LineClipboardEntry>();
        var source = string.IsNullOrWhiteSpace(text) ? string.Empty : text.TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(source))
        {
            error = "系统剪切板是空的。";
            return false;
        }

        var rows = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cursor = 0;
        while (cursor < rows.Length && rows[cursor].Trim().Length == 0)
            cursor++;
        if (cursor >= rows.Length)
        {
            error = "系统剪切板是空的。";
            return false;
        }

        var header = rows[cursor].Trim();
        if (header != LinesHeader)
        {
            error = IsKnownHeader(header)
                ? "系统剪切板里是另一类数据，不能替换当前线剪贴板。"
                : "系统剪切板不是线剪贴板数据。";
            return false;
        }

        var parsed = new List<LineClipboardEntry>();
        cursor++;
        while (cursor < rows.Length)
        {
            var marker = rows[cursor].Trim();
            if (marker.Length == 0)
            {
                cursor++;
                continue;
            }

            if (marker != "line")
            {
                error = "系统剪切板中的线数据缺少 line 标记。";
                return false;
            }

            if (!TryTakeCount(rows, ref cursor, "points", out var pointCount, out error))
                return false;
            var points = new LinePointClipboardEntry[pointCount];
            for (var index = 0; index < pointCount; index++)
            {
                if (!TryTakeFields(rows, ref cursor, 9, out var fields, out error))
                    return false;
                if (fields.Length != 9
                    || !TryParseFinite(fields[0], out var time)
                    || !TryParseFinite(fields[1], out var xPosition)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var easeType)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var canvasIndex)
                    || !TryParseFinite(fields[4], out var floorPosition)
                    || !TryChannel(fields[5], out var red)
                    || !TryChannel(fields[6], out var green)
                    || !TryChannel(fields[7], out var blue)
                    || !TryChannel(fields[8], out var alpha))
                {
                    error = "系统剪切板中的线节点格式不正确。";
                    return false;
                }

                points[index] = new LinePointClipboardEntry(
                    time, xPosition, easeType, canvasIndex, floorPosition, red, green, blue, alpha);
            }

            if (!TryTakeCount(rows, ref cursor, "notes", out var noteCount, out error))
                return false;
            var notes = new NoteClipboardEntry[noteCount];
            for (var index = 0; index < noteCount; index++)
            {
                if (!TryTakeFields(rows, ref cursor, 4, out var fields, out error))
                    return false;
                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type)
                    || !TryParseFinite(fields[1], out var time)
                    || !TryParseFinite(fields[2], out var floorPosition)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var otherCount)
                    || otherCount < 0
                    || fields.Length != 4 + otherCount)
                {
                    error = "系统剪切板中的音符格式不正确。";
                    return false;
                }

                var other = new double[otherCount];
                for (var otherIndex = 0; otherIndex < otherCount; otherIndex++)
                {
                    if (!TryParseFinite(fields[4 + otherIndex], out other[otherIndex]))
                    {
                        error = "系统剪切板中的音符附加数据格式不正确。";
                        return false;
                    }
                }

                notes[index] = new NoteClipboardEntry(type, time, floorPosition, other);
            }

            // The two colour arrays are optional so texts exported before this change still parse;
            // such a text simply carries no colour events.
            if (!TryTakeColorSection(rows, ref cursor, LineColorMarker, out var lineColors, out error)
                || !TryTakeColorSection(rows, ref cursor, JudgeRingMarker, out var judgeRingColors, out error))
            {
                return false;
            }

            parsed.Add(new LineClipboardEntry(points, notes, judgeRingColors, lineColors));
            cursor++;
        }

        if (parsed.Count == 0)
        {
            error = "系统剪切板里没有线。";
            return false;
        }

        lines = parsed.ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryReadRows(
        string? text,
        string expectedHeader,
        int columns,
        string kindLabel,
        out List<string[]> rows,
        out string error)
    {
        rows = new List<string[]>();
        var source = string.IsNullOrWhiteSpace(text) ? string.Empty : text.TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(source))
        {
            error = "系统剪切板是空的。";
            return false;
        }

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var start = 0;
        while (start < lines.Length && lines[start].Trim().Length == 0)
            start++;
        if (start >= lines.Length)
        {
            error = "系统剪切板是空的。";
            return false;
        }

        var header = lines[start].Trim();
        if (header != expectedHeader)
        {
            error = IsKnownHeader(header)
                ? $"系统剪切板里是另一类数据，不能替换当前{kindLabel}剪贴板。"
                : $"系统剪切板不是{kindLabel}剪贴板数据。";
            return false;
        }

        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
                continue;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != columns)
            {
                error = $"系统剪切板第 {rows.Count + 1} 行格式不正确。";
                return false;
            }

            rows.Add(fields);
        }

        if (rows.Count == 0)
        {
            error = $"系统剪切板里没有{kindLabel}节点。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Format(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

    private static bool IsKnownHeader(string header)
    {
        return header == SpeedHeader
            || header == CanvasEventHeader
            || header == LineColorHeader
            || header == JudgeRingHeader
            || header == CameraEventHeader
            || header == LinesHeader;
    }

    private static bool TryChannel(string text, out double value)
    {
        return TryParseFinite(text, out value);
    }

    private static bool TryTakeCount(string[] rows, ref int cursor, string label, out int count, out string error)
    {
        cursor++;
        while (cursor < rows.Length && rows[cursor].Trim().Length == 0)
            cursor++;
        if (cursor >= rows.Length)
        {
            count = 0;
            error = $"系统剪切板中的线数据缺少 {label}。";
            return false;
        }

        var fields = rows[cursor].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2
            || fields[0] != label
            || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
            || count < 0)
        {
            count = 0;
            error = $"系统剪切板中的 {label} 行格式不正确。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryTakeFields(string[] rows, ref int cursor, int minimum, out string[] fields, out string error)
    {
        cursor++;
        while (cursor < rows.Length && rows[cursor].Trim().Length == 0)
            cursor++;
        if (cursor >= rows.Length)
        {
            fields = Array.Empty<string>();
            error = "系统剪切板中的线数据提前结束。";
            return false;
        }

        fields = rows[cursor].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < minimum)
        {
            error = "系统剪切板中的线数据列数不够。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseFinite(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }
}
