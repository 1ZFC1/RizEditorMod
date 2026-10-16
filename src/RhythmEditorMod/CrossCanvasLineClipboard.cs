using Il2CppInterop.Runtime.InteropTypes.Arrays;
using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

/// <summary>
/// Copy/paste for lines whose nodes live on several canvases (2026-09-24 request).
///
/// Why the original cannot do it, taken from the original's own code (see
/// <c>docs/features/CROSS_CANVAS_LINES.md</c>): its box selection walks the rendered
/// <c>linesGroups</c> and matches the drag rectangle against rendered positions, and its paste rewrites
/// every point onto the current canvas (<c>LineEditor.Paste</c> performs no canvas conversion at all -
/// verified by disassembly and by a run), so a cross-canvas line is flattened into a single-canvas one.
///
/// The plugin takes over only while the user holds <c>Shift</c> during the drag. The rectangle itself is
/// still the original's: its own <c>LineEditor.SelectLines</c> coroutine is started on the plugin's
/// behalf, so its visuals and its beat math are reused. Without <c>Shift</c> everything runs the
/// original path unchanged.
///
/// Selection keeps whole lines, but only lines that have a node on the canvas being edited: such a node
/// has to be inside the rectangle (both axes), and then the entire line - including its nodes on other
/// canvases - is copied. Lines living only on other canvases are not picked up.
///
/// Paste reuses the original's placement rule: the anchor (the first line's first node) lands on the
/// mouse's beat and x, with the vertical snapping the original applies. On top of that every point's
/// canvas is shifted by <c>target canvas - source canvas</c>, so a line spanning canvases 1-3 copied on
/// canvas 2 and pasted on canvas 4 comes back as 3-5. The mirror variant (<c>Ctrl+Shift+V</c>) reflects
/// the shape about that anchor, which is what the original produces by negating every <c>xPosition</c>
/// in its mirror clipboard and then pasting aligned to the mouse.
/// </summary>
internal static class CrossCanvasLineClipboard
{
    private sealed class PendingSelection
    {
        public LineEditor Editor = null!;
        public bool Cut;
        public LineEditor._SelectLines_d__35? State;
    }

    private sealed class HighlightedSegment
    {
        public LineInformation Segment = null!;
        public Color StartColor;
        public Color EndColor;
        public Color HighlightColor;
    }

    private sealed class HighlightedLine
    {
        public Line Line = null!;
        public List<HighlightedSegment> Segments = new();
    }

    /// <summary>Lines highlighted right now; the highlight only lives while the drag is in progress.</summary>
    private static readonly List<Line> LiveLines = new();
    private static readonly List<HighlightedLine> Highlights = new();
    private static LineEditor._SelectLines_d__35? _pendingState;
    private static LineClipboardEntry[] _clipboard = Array.Empty<LineClipboardEntry>();
    private static int _sourceCanvas = int.MinValue;
    private static bool _pending;
    private static bool _shiftSeen;
    private static bool _pastePending;
    private static bool _pasteMirror;
    private static string _status = string.Empty;
    private static int _lastPickCount = -1;
    private static float _nextPickLogAt;
    private static bool _loggedHighlightFailure;

    internal static bool HasClipboard => _clipboard.Length > 0;

    private static bool ShiftHeld =>
        Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>
    /// Starts the original selection rectangle on the plugin's behalf (Ctrl+C / Ctrl+X while the line
    /// tool is active). The rectangle is drawn by the coroutine, so it must be started on a MonoBehaviour
    /// - the same owner the other tools use.
    /// </summary>
    internal static void BeginSelection(LineEditor editor, bool cut)
    {
        try
        {
            if (_pending)
                return;

            ClearSelection("开始新的框选");

            // The original only shows its selection rectangle while a canvas is focused.
            if (!SpeedClipboardController.HasFocusedCanvas())
            {
                SetStatus("跨画布复制：请先在线编辑里点一下画布（没有聚焦画布时原版不会显示框选矩形）。");
                return;
            }

            var request = new PendingSelection { Editor = editor, Cut = cut };
            System.Action<Il2CppReferenceArray<Line>> completed = nativeLines => CompleteSelection(request, nativeLines);
            var routine = editor.SelectLines(
                cut ? editor.cutColor : editor.copyColor,
                (Il2CppSystem.Action<Il2CppReferenceArray<Line>>)completed);
            request.State = routine.Cast<LineEditor._SelectLines_d__35>();
            if (request.State == null)
                Plugin.LogSource.LogWarning("跨画布线：无法读取原版框选协程的范围，Shift 模式将退回原版复制。");

            var coroutineOwner = SpeedEasePanel.Instance
                ?? throw new InvalidOperationException("编辑器面板尚未初始化。");
            coroutineOwner.StartCoroutine(routine);
            _pending = true;
            _pendingState = request.State;
            _shiftSeen = ShiftHeld;

            var action = cut ? "剪切" : "复制";
            SetStatus($"跨画布{action}：按住左键拖出矩形框选区（按住 Shift 才会按整条跨画布线选中），按 Esc 或右键取消。");
        }
        catch (Exception error)
        {
            _pending = false;
            Plugin.LogSource.LogError($"[跨画布线] 启动框选失败：{error}");
            SpeedEasePanel.Instance?.SetEventStatus($"跨画布线：启动框选失败（{error.Message}）。");
        }
    }

    /// <summary>
    /// Per-frame bookkeeping while the line tool is active: flush a queued paste, keep the live
    /// highlight in sync with the drag rectangle, sample Shift, and notice a cancelled drag.
    /// </summary>
    internal static void Track()
    {
        try
        {
            FlushPendingPaste();
            RunRenderCheck();

            if (_pending)
            {
                if (ShiftHeld)
                    _shiftSeen = true;

                UpdateLiveHighlight();

                // Safety net: a cancelled rectangle (Esc / right click) ends the coroutine without
                // running its callback, which would otherwise leave the highlight behind.
                var state = _pendingState;
                if (state != null && state.__1__state == -1 && !Input.GetMouseButton(0))
                    FinishPending();
                return;
            }

            if (Highlights.Count > 0)
                ClearHighlight();
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"[跨画布线] 状态跟踪失败：{error}");
        }
    }

    /// <summary>
    /// The live preview: while the rectangle is dragged **with Shift held**, every line whose node on
    /// the canvas being edited is inside the rectangle right now is highlighted, and lines that leave
    /// the rectangle lose their highlight again. Without Shift nothing is highlighted, so the original
    /// selection behaviour is untouched.
    /// </summary>
    private static void UpdateLiveHighlight()
    {
        var state = _pendingState;
        if (state == null)
            return;

        // The editor repaints line colours every frame, so the highlight is re-asserted here - and it
        // only exists while Shift and the mouse button are actually held down.
        if (!Input.GetMouseButton(0) || !ShiftHeld)
        {
            ClearHighlight();
            return;
        }

        var picked = PickByRectangle(state, detail: false);
        for (var index = 0; index < picked.Count; index++)
        {
            if (!Contains(LiveLines, picked[index]))
            {
                LiveLines.Add(picked[index]);
                AddHighlight(picked[index]);
            }
        }

        for (var index = LiveLines.Count - 1; index >= 0; index--)
        {
            if (Contains(picked, LiveLines[index]))
                continue;
            RemoveHighlight(LiveLines[index]);
            LiveLines.RemoveAt(index);
        }

        for (var index = 0; index < Highlights.Count; index++)
        {
            for (var segment = 0; segment < Highlights[index].Segments.Count; segment++)
                WriteHighlight(Highlights[index].Segments[segment], Highlights[index].Segments[segment].HighlightColor);
        }
    }

    /// <summary>Drops the highlight of one line (it left the rectangle).</summary>
    private static void RemoveHighlight(Line line)
    {
        for (var index = Highlights.Count - 1; index >= 0; index--)
        {
            if (!SameLine(Highlights[index].Line, line))
                continue;
            RestoreSegments(Highlights[index]);
            Highlights.RemoveAt(index);
        }
    }

    private static void ClearHighlight()
    {
        for (var index = 0; index < Highlights.Count; index++)
            RestoreSegments(Highlights[index]);
        Highlights.Clear();
        LiveLines.Clear();
    }

    /// <summary>Ends a pending drag that will never call back (cancelled rectangle).</summary>
    private static void FinishPending()
    {
        _pending = false;
        _pendingState = null;
        ClearHighlight();
        SetStatus("跨画布框选已取消。");
    }

    /// <summary>Called when the original selection rectangle finishes.</summary>
    private static void CompleteSelection(PendingSelection request, Il2CppReferenceArray<Line>? nativeLines)
    {
        _pending = false;
        _pendingState = null;
        // The highlight belongs to the drag itself: it ends as soon as the selection is finished.
        ClearHighlight();
        try
        {
            if (!_shiftSeen || request.State == null)
            {
                // No Shift (or the rectangle range is unreadable): behave exactly like the original, and
                // drop the plugin's own clipboard so the next Ctrl+V pastes what was just copied here.
                if (request.Cut)
                    request.Editor.CutLines(nativeLines, false);
                else
                    request.Editor.CopyLines(nativeLines, false);
                ForgetClipboard("原版复制已接管线剪贴板");
                return;
            }

            var picked = PickByRectangle(request.State, detail: true);
            if (picked.Count == 0)
            {
                SetStatus("跨画布选择：框内没有被完全框住的节点，剪贴板未改变。");
                return;
            }

            _clipboard = EventClipboardOperations.CaptureLines(picked);
            _sourceCanvas = ProjectInformation.SelectedCanvas;
            ClearNativeClipboards(request.Editor);

            var crossCanvas = CrossCanvasLineEditing.CountCrossCanvas(_clipboard);
            var points = 0;
            foreach (var line in _clipboard)
                points += line.Points.Count;
            var note = request.Cut ? "（Shift 剪切暂按复制处理，原谱面未删除）" : string.Empty;
            SetStatus(
                $"跨画布选择：已选中并复制 {picked.Count} 条线{note}，其中跨画布 {crossCanvas} 条，共 {points} 个节点，"
                + $"源画布 {_sourceCanvas}，原版线剪贴板已清空。");
            Plugin.LogSource.LogInfo(_status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"[跨画布线] 完成框选失败：{error}");
            SpeedEasePanel.Instance?.SetEventStatus($"跨画布线：完成框选失败（{error.Message}）。");
        }
    }

    /// <summary>
    /// Keeps whole lines, but only those with a node on the canvas being edited: the node has to be
    /// inside the rectangle in both axes, then the whole line is taken. While the rectangle is being
    /// dragged this runs every frame, so it only logs when the result changes (at most twice a second).
    /// </summary>
    private static List<Line> PickByRectangle(LineEditor._SelectLines_d__35 state, bool detail)
    {
        var picked = new List<Line>();
        var project = ProjectInformation.instance;
        var groups = project?.linesGroups;
        var camera = project?.inputControl?._camera;
        if (groups == null || camera == null)
            return picked;

        var selectedCanvas = ProjectInformation.SelectedCanvas;

        // The original stores the drag origin in the UI-canvas space (its rectangle code compares it
        // against mousePosition * _scale), so the origin has to be scaled back before it can be
        // compared with rendered screen positions. With _scale == 1 this is a no-op, which is why the
        // mismatch only showed up after the wheel zoom changed the scale.
        var start = state._selectionStart_5__2;
        var scale = state._scale_5__3;
        if (float.IsFinite(scale) && scale > 0.0001f && Math.Abs(scale - 1f) > 1e-4f)
            start /= scale;

        var end = (Vector2)Input.mousePosition;
        var minX = Mathf.Min(start.x, end.x);
        var maxX = Mathf.Max(start.x, end.x);
        var minY = Mathf.Min(start.y, end.y);
        var maxY = Mathf.Max(start.y, end.y);

        var scannedGroups = 0;
        var scannedSegments = 0;
        var sample = string.Empty;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            var line = group?.lineData;
            if (group == null || line == null)
                continue;
            scannedGroups++;

            var hit = false;
            var segments = group.lines;
            if (segments != null)
            {
                for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    if (segment == null || segment.lineData == null)
                        continue;

                    // Only nodes of the canvas being edited take part in the hit test, so lines that
                    // merely happen to be drawn elsewhere inside the rectangle are not collected.
                    if (segment.lineData.canvasIndex != selectedCanvas)
                        continue;
                    scannedSegments++;

                    foreach (var world in new[] { segment.startLineTransform, segment.endLineTransform })
                    {
                        var screen = camera.WorldToScreenPoint(world);
                        if (screen.z <= 0f)
                            continue;
                        if (sample.Length == 0)
                            sample = $"画布 {segment.lineData.canvasIndex} @({screen.x:0.#}, {screen.y:0.#})";
                        if (screen.x >= minX && screen.x <= maxX && screen.y >= minY && screen.y <= maxY)
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                        break;
                }
            }

            if (!hit)
                continue;
            if (!Contains(picked, line))
                picked.Add(line);
        }

        LogPick(detail, picked.Count, minX, maxX, minY, maxY, selectedCanvas, scannedGroups, scannedSegments, sample, scale);
        return picked;
    }

    /// <summary>Keeps the log readable: full detail once at the end, otherwise only on changes.</summary>
    private static void LogPick(
        bool detail,
        int picked,
        float minX,
        float maxX,
        float minY,
        float maxY,
        int selectedCanvas,
        int scannedGroups,
        int scannedSegments,
        string sample,
        float scale)
    {
        var now = Time.unscaledTime;
        var changed = picked != _lastPickCount;
        if (!detail && (!changed || now < _nextPickLogAt))
            return;

        _lastPickCount = picked;
        _nextPickLogAt = now + 0.5f;

        Plugin.LogSource.LogInfo(
            $"[跨画布线] 框选范围 屏幕 x[{minX:0.#}, {maxX:0.#}] y[{minY:0.#}, {maxY:0.#}]（UI 缩放 {scale:0.####}），"
            + $"当前画布 {selectedCanvas}，扫描 {scannedGroups} 组 / {scannedSegments} 段，命中 {picked} 条线"
            + (!detail ? "（框选中）" : string.Empty)
            + (picked == 0 && sample.Length > 0 ? $"；示例节点屏幕坐标 {sample}" : string.Empty));
    }

    /// <summary>Highlights every segment of one line with the original's copy colour.</summary>
    private static void AddHighlight(Line line)
    {
        var project = ProjectInformation.instance;
        var groups = project?.linesGroups;
        if (groups == null)
            return;

        Color color;
        try
        {
            color = SpeedClipboardController.LineEditorInstance?.copyColor ?? new Color(1f, 0.85f, 0.2f, 1f);
        }
        catch
        {
            color = new Color(1f, 0.85f, 0.2f, 1f);
        }

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            if (group == null || !SameLine(group.lineData, line))
                continue;

            var entry = new HighlightedLine { Line = line };
            var segments = group.lines;
            if (segments != null)
            {
                for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    var renderer = segment?.lineRenderer;
                    if (segment == null || renderer == null)
                        continue;

                    var highlighted = new HighlightedSegment
                    {
                        Segment = segment,
                        StartColor = renderer.startColor,
                        EndColor = renderer.endColor,
                    };
                    entry.Segments.Add(highlighted);
                    WriteHighlight(highlighted, color);
                }
            }

            if (entry.Segments.Count > 0)
                Highlights.Add(entry);
        }
    }

    private static void WriteHighlight(HighlightedSegment entry, Color color)
    {
        entry.HighlightColor = color;
        try
        {
            var renderer = entry.Segment?.lineRenderer;
            if (renderer == null)
                return;
            renderer.startColor = color;
            renderer.endColor = color;
        }
        catch (Exception error)
        {
            if (_loggedHighlightFailure)
                return;
            _loggedHighlightFailure = true;
            Plugin.LogSource.LogWarning($"[跨画布线] 高亮写入失败：{error.GetType().Name}：{error.Message}");
        }
    }

    private static void RestoreSegments(HighlightedLine entry)
    {
        foreach (var segment in entry.Segments)
        {
            try
            {
                var renderer = segment.Segment?.lineRenderer;
                if (renderer == null)
                    continue;
                renderer.startColor = segment.StartColor;
                renderer.endColor = segment.EndColor;
            }
            catch (Exception error)
            {
                if (_loggedHighlightFailure)
                    continue;
                _loggedHighlightFailure = true;
                Plugin.LogSource.LogWarning($"[跨画布线] 恢复高亮失败：{error.GetType().Name}");
            }
        }
    }

    private static bool SameLine(Line? left, Line? right)
    {
        if (left == null || right == null)
            return false;
        return left.Pointer == right.Pointer;
    }

    /// <summary>Drops everything that belongs to a finished or abandoned drag.</summary>
    internal static void ClearSelection(string reason)
    {
        _pending = false;
        _pendingState = null;
        ClearHighlight();
        _status = reason;
    }

    /// <summary>
    /// Drops the plugin's own line clipboard. Called whenever the original becomes the owner of the line
    /// clipboard (its own copy/cut, or a line import from the system clipboard), so Ctrl+V always pastes
    /// the most recent copy instead of a stale one.
    /// </summary>
    internal static void ForgetClipboard(string reason)
    {
        if (_clipboard.Length == 0)
            return;

        var lines = _clipboard.Length;
        var points = 0;
        foreach (var line in _clipboard)
            points += line.Points.Count;
        var sourceCanvas = _sourceCanvas == int.MinValue ? "<未记录>" : _sourceCanvas.ToString();

        _clipboard = Array.Empty<LineClipboardEntry>();
        _sourceCanvas = int.MinValue;
        Plugin.LogSource.LogInfo(
            $"跨画布剪贴板已清空（{reason}）：丢弃 {lines} 条线 / {points} 个节点，源画布={sourceCanvas}；之后的 Ctrl+V 交回原版。");
    }

    /// <summary>Clears the original's line clipboards so its paste cannot use flattened data.</summary>
    private static void ClearNativeClipboards(LineEditor editor)
    {
        try
        {
            if (editor.lineClipboard == null)
                editor.lineClipboard = new Il2CppSystem.Collections.Generic.List<Line>();
            else
                editor.lineClipboard.Clear();

            if (editor.mirrorLineClipboard == null)
                editor.mirrorLineClipboard = new Il2CppSystem.Collections.Generic.List<Line>();
            else
                editor.mirrorLineClipboard.Clear();
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"[跨画布线] 清空原版剪贴板失败：{error.GetType().Name}");
        }
    }

    /// <summary>
    /// Queues the plugin's own paste. Returns false when the plugin holds nothing, so the original
    /// handler keeps working for ordinary (single-canvas) copies. Like every other paste in the plugin
    /// the write happens in the next <c>LateUpdate</c> instead of inside the shortcut callback.
    /// </summary>
    internal static bool QueuePaste(bool mirror)
    {
        if (_clipboard.Length == 0)
            return false;

        _pastePending = true;
        _pasteMirror = mirror;
        SetStatus($"跨画布粘贴：已排队（{(mirror ? "镜像" : "普通")}），将在下一帧写入谱面。");
        return true;
    }

    private static void FlushPendingPaste()
    {
        if (!_pastePending)
            return;

        var mirror = _pasteMirror;
        _pastePending = false;
        _pasteMirror = false;
        PasteNow(mirror);
    }

    /// <summary>The actual write: place the lines at the mouse and append them inside one Stepping.</summary>
    private static void PasteNow(bool mirror)
    {
        try
        {
            var project = ProjectInformation.instance;
            var chart = project?.chart;
            if (project == null || chart?.lines == null || project.editorControl == null)
            {
                SetStatus("跨画布粘贴：当前没有已打开的谱面。");
                return;
            }

            var targetCanvas = ProjectInformation.SelectedCanvas;
            if (targetCanvas < 0)
            {
                SetStatus("跨画布粘贴：无法确定目标画布，请先在线编辑里点一下画布。");
                return;
            }

            var offset = _sourceCanvas == int.MinValue ? 0 : targetCanvas - _sourceCanvas;

            // The original's mouse anchor: the beat (snapped to the vertical subdivision) and the
            // absolute canvas value under the cursor.
            var mouseAnchor = EditorOperations.GetCanvasPasteAnchor(project, "跨画布线");
            var anchorCanvas = _clipboard[0].Points[0].CanvasIndex + offset;
            // A line point's x is relative to its canvas, and the drawn grid lives in that same local
            // frame, so the mouse has to be converted to a local x first and snapped there. Snapping the
            // absolute value (as the canvas-event paste does, where values really are absolute) and then
            // subtracting the frame lands off the grid - that was the "strange numbers" report.
            if (!TryGetCanvasFrame(project, anchorCanvas, mouseAnchor.Beat, out var canvasFrame, out var frameDetail))
            {
                SetStatus($"跨画布粘贴：无法换算画布 {anchorCanvas} 的横坐标，已放弃粘贴。");
                return;
            }

            var camera = project.inputControl?._camera;
            if (camera == null)
            {
                SetStatus("跨画布粘贴：原编辑器相机不可用，已放弃粘贴。");
                return;
            }

            var mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);

            // The x step follows the editor's own horizontal subdivision (snapHLinesSplit; a value of 2
            // shows as "1/2" and means 0.5 steps), exactly like the beats follow the vertical one. The
            // local x itself comes from the measured mapping, because the unit constant the plugin used
            // to carry (5.5625) is ~1 % off - the drawn geometry gives 5.625.
            var hasMapping = TryMeasureCanvasMapping(project, anchorCanvas, out var slope, out var intercept, out var mappingDetail);
            float localRaw;
            string mapDetail;
            if (hasMapping)
            {
                localRaw = (mouseWorld.x - intercept) / slope;
                mapDetail = mappingDetail;
            }
            else
            {
                localRaw = mouseWorld.x / EditorOperations.CanvasEditorWorldUnitsPerChartUnit - canvasFrame;
                mapDetail = $"映射不可用，退回局部原值 {localRaw:0.####}（{mappingDetail}）";
            }

            // The drawn SnapLines are the base x grid; snapHLinesSplit subdivides that base grid (1 = the
            // base itself, 2 = halves, 4 = quarters - the vertical slider reads 1, which is why beats
            // snap to whole beats). The base spacing is measured from the drawn lines, so nothing here
            // depends on a hard-coded unit.
            var subdivision = HorizontalSubdivision(project, out var subdivisionDetail);
            var baseStepLocal = hasMapping ? BaseGridStepLocal(slope, out var baseDetail) : float.NaN;
            if (!float.IsFinite(baseStepLocal) || baseStepLocal <= 0f)
                baseStepLocal = 0.1f;
            var stepLocal = baseStepLocal / subdivision;
            var snapCount = Math.Max(1, (int)Math.Round(1f / stepLocal));
            var anchorBeat = mouseAnchor.Beat;
            var anchorTargetX = (float)CanvasEventEditing.SnapToSubdivision(localRaw, snapCount);
            var snapDetail = $"局部原值 {localRaw:0.####} → 吸附 {anchorTargetX:0.####}"
                + $"（基础格距 {baseStepLocal:0.####} ÷ 水平细分 {subdivision} = 步长 {stepLocal:0.####}，{subdivisionDetail}）；{mapDetail}";
            var canvases = CollectCanvasIndices(chart);
            if (!CrossCanvasLineEditing.TryPlace(
                    _clipboard,
                    offset,
                    anchorBeat,
                    anchorTargetX,
                    mirror,
                    canvases,
                    out var placed,
                    out var error))
            {
                SetStatus($"跨画布粘贴：{error}");
                return;
            }

            project.editorControl.Stepping((Il2CppSystem.Action)(() =>
            {
                foreach (var entry in placed)
                    chart.lines.Add(EventClipboardOperations.CreateLine(entry));
                project.editorControl.SetFloorPosition();
            }));

            ScheduleRenderCheck();
            SetStatus(
                $"跨画布粘贴：已写入 {placed.Length} 条线（{(mirror ? "镜像" : "普通")}），"                + $"锚点 拍={mouseAnchor.Beat:0.###} x={anchorTargetX:0.####}（{snapDetail}；{frameDetail}），"
                + $"画布偏移 {offset:+#;-#;0}（源 {_sourceCanvas} → 目标 {targetCanvas}）。");
            Plugin.LogSource.LogInfo(_status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"[跨画布线] 粘贴失败：{error}");
            SpeedEasePanel.Instance?.SetEventStatus($"跨画布粘贴失败（{error.Message}）。");
        }
    }

    private static readonly List<float> GridLineWorldX = new();
    private static float _gridReadAt = -999f;

    /// <summary>
    /// Reads the editor's own vertical grid: a <c>SnapLines</c> parent holding thin
    /// <c>SnapLine</c> children. Those world positions are the lines the user actually sees, so they -
    /// not a subdivision property - decide where a paste snaps.
    /// </summary>
    private static void RefreshGridLines()
    {
        if (GridLineWorldX.Count > 0 && Time.unscaledTime - _gridReadAt < 1f)
            return;

        _gridReadAt = Time.unscaledTime;
        try
        {
            var root = GameObject.Find("SnapLines");
            if (root == null)
            {
                GridLineWorldX.Clear();
                return;
            }

            var positions = new List<float>();
            var transform = root.transform;
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child == null)
                    continue;
                var x = child.position.x;
                if (!float.IsFinite(x) || Math.Abs(x) > 30f)
                    continue;
                if (!positions.Contains(x))
                    positions.Add(x);
            }

            positions.Sort();
            if (positions.Count >= 2)
            {
                GridLineWorldX.Clear();
                GridLineWorldX.AddRange(positions);
                var first = GridLineWorldX[0];
                var last = GridLineWorldX[^1];
                var step = (last - first) / (GridLineWorldX.Count - 1);
                Plugin.LogSource.LogInfo(
                    $"[跨画布线] 网格线 {GridLineWorldX.Count} 条（世界 x {first:0.####} … {last:0.####}，平均间距 {step:0.#####}）");
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"[跨画布线] 读取网格线失败：{error.GetType().Name}");
        }
    }

    /// <summary>
    /// The drawn base grid spacing expressed in local chart units (measured: 0.5625 world / 5.625 per
    /// unit = 0.1). Everything is taken from the objects the editor actually drew, so no constant of the
    /// plugin is involved.
    /// </summary>
    private static float BaseGridStepLocal(float slope, out string detail)
    {
        detail = string.Empty;
        if (GridLineWorldX.Count < 2 || !float.IsFinite(slope) || Math.Abs(slope) < 1e-4f)
            return float.NaN;

        var spacing = (GridLineWorldX[^1] - GridLineWorldX[0]) / (GridLineWorldX.Count - 1);
        var step = spacing / slope;
        detail = $"基础格距 {spacing:0.#####} 世界 = {step:0.#####} 局部";
        return step;
    }
    /// <summary>The drawn grid line closest to a world x.</summary>
    private static float NearestGridLine(float worldX, out string detail)
    {
        detail = string.Empty;
        if (GridLineWorldX.Count == 0)
        {
            detail = "未读到网格线";
            return float.NaN;
        }

        var nearest = GridLineWorldX[0];
        var distance = Math.Abs(nearest - worldX);
        foreach (var line in GridLineWorldX)
        {
            var candidate = Math.Abs(line - worldX);
            if (candidate < distance)
            {
                distance = candidate;
                nearest = line;
            }
        }

        detail = $"最近网格线 {nearest:0.####}（相距 {distance:0.####} 世界单位）";
        return nearest;
    }

    /// <summary>
    /// Measures how a local <c>xPosition</c> maps to a world x, using two already drawn nodes of the
    /// same canvas: the editor positions a canvas by the playhead, so both nodes share one frame and the
    /// mapping is affine. This removes the unit constant (<c>5.5625</c> looks ~1 % off the drawn grid).
    /// </summary>
    private static bool TryMeasureCanvasMapping(
        ProjectInformation project,
        int canvasIndex,
        out float slope,
        out float intercept,
        out string detail)
    {
        slope = 0f;
        intercept = 0f;
        detail = string.Empty;

        var groups = project.linesGroups;
        if (groups == null)
        {
            detail = "无渲染分组";
            return false;
        }

        var firstWorld = float.NaN;
        var firstLocal = float.NaN;
        var bestWorld = float.NaN;
        var bestLocal = float.NaN;
        var bestSpan = 0.05f;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var segments = groups[groupIndex]?.lines;
            if (segments == null)
                continue;

            for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                var point = segments[segmentIndex]?.lineData;
                if (point == null || point.canvasIndex != canvasIndex)
                    continue;

                var world = segments[segmentIndex].startLineTransform.x;
                if (!float.IsFinite(world))
                    continue;

                if (float.IsNaN(firstWorld))
                {
                    firstWorld = world;
                    firstLocal = point.xPosition;
                    continue;
                }

                var span = Math.Abs(point.xPosition - firstLocal);
                if (span > bestSpan)
                {
                    bestSpan = span;
                    bestWorld = world;
                    bestLocal = point.xPosition;
                }
            }
        }

        if (float.IsNaN(firstWorld) || float.IsNaN(bestWorld))
        {
            detail = "同画布可用的渲染节点不足（需要两个不同 x 的节点）";
            return false;
        }

        slope = (bestWorld - firstWorld) / (bestLocal - firstLocal);
        if (!float.IsFinite(slope) || Math.Abs(slope) < 1e-4f)
        {
            detail = $"斜率异常（{slope:0.####}）";
            return false;
        }

        intercept = firstWorld - slope * firstLocal;
        detail = $"实测映射 斜率={slope:0.####} 截距={intercept:0.####}"
            + $"（节点 x={firstLocal:0.###}→世界 {firstWorld:0.###}，x={bestLocal:0.###}→世界 {bestWorld:0.###}）";
        return true;
    }
    /// <summary>
    /// The horizontal (x) subdivision of the drawn grid, i.e. how many grid steps fit in one chart
    /// unit. <c>SnapControl.snapHLinesSplit</c> is the slider the user sees as 水平细分; the
    /// <c>VerticalSnapDensity</c> fallback is only used when that slider is unavailable. Every
    /// candidate is reported so a wrong pick can never pass unnoticed.
    /// </summary>
    private static int HorizontalSubdivision(ProjectInformation project, out string detail)
    {
        var slider = float.NaN;
        var beatSlider = float.NaN;
        var snapOn = false;
        var withoutSnapLine = 0;
        try
        {
            slider = project.snapHLinesSplit?.value ?? float.NaN;
            beatSlider = project.snapLinesSplit?.value ?? float.NaN;
            snapOn = project.verticalSnapLineIsOn;
            withoutSnapLine = project.verticalSnapDensityWithoutSnapLine;
        }
        catch (Exception error)
        {
            detail = $"读取细分失败（{error.GetType().Name}）";
            return Math.Max(1, ProjectInformation.VerticalSnapDensity);
        }

        var density = ProjectInformation.VerticalSnapDensity;
        detail = $"水平线滑条={slider:0.###}，垂直线滑条={beatSlider:0.###}，VerticalSnapDensity={density}，"
            + $"无吸附线密度={withoutSnapLine}，吸附线开={snapOn}";

        // The slider holds the subdivision count: 2 is displayed as "1/2" and means 0.5 steps.
        if (float.IsFinite(slider) && slider > 0f)
            return slider < 1f ? Math.Max(1, (int)Math.Round(1f / slider)) : Math.Max(1, (int)Math.Round(slider));
        return Math.Max(1, density);
    }
    /// <summary>
    /// The canvas frame the mouse must be measured in. The editor positions the whole canvas by the
    /// **playhead** time and draws every point in canvas-local coordinates, so the frame is
    /// <c>canvasX(playhead)</c> - not <c>canvasX(target beat)</c> and not their difference. Evidence
    /// (run of 2026-10-15): at playhead beat 3 / target beat 4.5 the drawn frame measured 1.2135, equal
    /// to <c>canvasX(3) = 1.2</c>, while <c>canvasX(4.5)</c> was 0.
    ///
    /// The frame is therefore taken from the rendered geometry whenever possible, which needs no
    /// assumption about how the canvas animation is evaluated; the eased value is only the fallback.
    /// </summary>
    private static bool TryGetCanvasFrame(
        ProjectInformation project,
        int canvasIndex,
        float beat,
        out float frame,
        out string detail)
    {
        frame = 0f;
        detail = string.Empty;

        var playhead = project.nowChartTime;
        var hasEvaluated = TryEvaluateCanvasX(project, canvasIndex, playhead, out var atPlayhead);
        var hasRendered = TryGetRenderedCanvasFrame(project, canvasIndex, beat, out var byRender, out var nodeInfo);
        if (!hasEvaluated && !hasRendered)
            return false;

        frame = hasRendered ? byRender : atPlayhead;
        detail = $"画布 {canvasIndex}：基准={frame:0.####}（取自{(hasRendered ? "渲染" : "缓动求值")}；"
            + $"画布在播放头拍 {playhead:0.###} 的 x={atPlayhead:0.####}"
            + (hasRendered ? $"，渲染基准 {byRender:0.####}" : string.Empty)
            + (nodeInfo.Length > 0 ? $"，{nodeInfo}" : string.Empty)
            + "）";
        return true;
    }

    /// <summary>
    /// Cross-check of <see cref="TryGetCanvasFrame"/> straight from what the editor drew: for a node on
    /// this canvas, <c>renderedWorldX / units - xPosition</c> is the frame it was drawn in.
    /// </summary>
    private static bool TryGetRenderedCanvasFrame(
        ProjectInformation project,
        int canvasIndex,
        float beat,
        out float frame,
        out string nodeInfo)
    {
        frame = 0f;
        nodeInfo = string.Empty;
        var groups = project.linesGroups;
        if (groups == null)
            return false;

        var units = EditorOperations.CanvasEditorWorldUnitsPerChartUnit;
        var bestDistance = float.MaxValue;
        var found = false;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var segments = groups[groupIndex]?.lines;
            if (segments == null)
                continue;

            for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                var point = segment?.lineData;
                if (segment == null || point == null || point.canvasIndex != canvasIndex)
                    continue;

                var distance = Mathf.Abs(point.time - beat);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                frame = segment.startLineTransform.x / units - point.xPosition;
                nodeInfo = $"最近节点 拍={point.time:0.###} x={point.xPosition:0.####}";
                found = true;
            }
        }

        return found;
    }

    /// <summary>The canvas's own x (chart units) at a beat, using the original's ease curves.</summary>
    private static bool TryEvaluateCanvasX(ProjectInformation project, int canvasIndex, float beat, out float value)
    {
        value = 0f;
        var moves = project.chart?.canvasMoves;
        if (moves == null)
            return false;

        for (var moveIndex = 0; moveIndex < moves.Count; moveIndex++)
        {
            var move = moves[moveIndex];
            if (move == null || move.index != canvasIndex)
                continue;

            var keyPoints = move.xPositionKeyPoints;
            if (keyPoints == null || keyPoints.Count == 0)
                return false;

            var points = new List<CanvasEventClipboardPoint>(keyPoints.Count);
            for (var keyIndex = 0; keyIndex < keyPoints.Count; keyIndex++)
            {
                var key = keyPoints[keyIndex];
                points.Add(new CanvasEventClipboardPoint(key.time, key.value, key.easeType, 0));
            }

            if (!CrossCanvasLineEditing.TryEvaluateKeyPoints(points, beat, Ease, out var evaluated))
                return false;
            value = (float)evaluated;
            return true;
        }

        return false;
    }

    /// <summary>The original's own ease curve, so no easing is re-implemented here.</summary>
    private static double Ease(int easeType, double progress)
    {
        try
        {
            return GetEase.GetEaseWithProgress((float)progress, easeType);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"[跨画布线] 缓动求值失败（改用线性）：{error.GetType().Name}");
            return progress;
        }
    }

    private static List<int> CollectCanvasIndices(Chart chart)
    {
        var canvases = new List<int>();
        var moves = chart.canvasMoves;
        if (moves == null)
            return canvases;

        for (var index = 0; index < moves.Count; index++)
        {
            var move = moves[index];
            if (move != null && !canvases.Contains(move.index))
                canvases.Add(move.index);
        }

        return canvases;
    }

    private static float _renderCheckAt = -1f;

    /// <summary>
    /// Read-only check that the editor picked the new lines up. Nothing is forced any more: stepping the
    /// canvas index to trigger a rebuild changed (and sometimes failed to change) the focused canvas, so
    /// the editor's own refresh is relied on - the write already goes through <c>EditControl.Stepping</c>,
    /// the same call the original's paste ends with. The groups legitimately lag within the same frame,
    /// so the check is repeated half a second later and only then reported.
    /// </summary>
    private static void ScheduleRenderCheck()
    {
        _renderCheckAt = Time.unscaledTime + 0.5f;
    }

    private static void RunRenderCheck()
    {
        if (_renderCheckAt < 0f || Time.unscaledTime < _renderCheckAt)
            return;
        _renderCheckAt = -1f;

        try
        {
            var project = ProjectInformation.instance;
            var lines = project?.chart?.lines?.Count ?? 0;
            var groups = project?.linesGroups?.Count ?? 0;
            if (lines == groups)
                return;

            Plugin.LogSource.LogWarning(
                $"[跨画布线] 谱面线数 {lines}，渲染分组 {groups}：0.5 秒后渲染仍未跟上，切一次画布即可看到。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"[跨画布线] 渲染检查失败：{error.GetType().Name}");
        }
    }

    private static void SetStatus(string status)
    {
        _status = status;
        SpeedEasePanel.Instance?.SetEventStatus(status);
    }

    private static bool Contains(List<Line> lines, Line? candidate)
    {
        if (candidate == null)
            return false;

        foreach (var line in lines)
        {
            if (line != null && line.Pointer == candidate.Pointer)
                return true;
        }

        return false;
    }

    /// <summary>Small always-visible readout so the selection/clipboard state is not guesswork.</summary>
    internal static void DrawOverlay()
    {
        if (!_pending && LiveLines.Count == 0 && !HasClipboard)
            return;

        var text = HasClipboard
            ? (_pending
                ? $"框选中：已框住 {LiveLines.Count} 条线（Shift 整条选择）· 剪贴板 {_clipboard.Length} 条"
                : $"跨画布剪贴板 {_clipboard.Length} 条 · Ctrl+V 粘贴 / Ctrl+Shift+V 镜像")
            : $"框选中：已框住 {LiveLines.Count} 条线（Shift 整条选择）";
        if (_pastePending)
            text = $"跨画布粘贴已排队（{(_pasteMirror ? "镜像" : "普通")}）…";
        GUI.Label(new Rect(Screen.width - 470f, 34f, 456f, 22f), text);
    }
}
