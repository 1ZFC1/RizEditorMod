using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

internal enum ClipboardTarget
{
    None,
    Speed,
    CanvasEvent,
    ColorEvent,
    CameraEvent,
    Lines,
}

internal static class SpeedClipboardController
{
    private const float SelectionPixelsPerRealSecond = 108f;
    private static SpeedClipboardPoint[] _speedClipboard = Array.Empty<SpeedClipboardPoint>();
    private static CanvasEventClipboardPoint[] _canvasEventClipboard = Array.Empty<CanvasEventClipboardPoint>();
    private static ColorEventClipboardPoint[] _lineColorClipboard = Array.Empty<ColorEventClipboardPoint>();
    private static ColorEventClipboardPoint[] _judgeRingClipboard = Array.Empty<ColorEventClipboardPoint>();
    // Which of the two arrays the last colour copy filled; paste follows the copy (D-019).
    private static bool _lastColorCopyLineColor;
    private static bool _lastColorCopyJudgeRing;
    private static CanvasEventClipboardPoint[] _cameraClipboard = Array.Empty<CanvasEventClipboardPoint>();
    private static bool _pastePending;
    private static ClipboardTarget _pendingPasteTarget;
    private static bool _pendingAlternatePaste;
    private static bool _suppressNativeLineExport;
    internal static LineEditor? LineEditorInstance { get; set; }

    private sealed class SelectionRequest
    {
        public LineEditor._SelectLines_d__35? State;
        public ClipboardTarget Target;
        public bool Cut;
        public bool DeleteOnly;
    }

    private sealed class LineDeleteRequest
    {
        public LineEditor Editor = null!;
        public LineClipboardEntry[] Saved = Array.Empty<LineClipboardEntry>();
        public LineClipboardEntry[] SavedMirror = Array.Empty<LineClipboardEntry>();
    }

    public static void HandleKeyboard()
    {
        if (IsTextInputActive())
            return;
        var controlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!controlPressed)
            return;

        var target = GetActiveTarget();
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (target != ClipboardTarget.None)
                BeginSelectionFromKeyboard(target, cut: true, deleteOnly: true);
            else if (SpeedEasePanel.LineEditorIsActive)
                BeginLineDeleteOnly();
            return;
        }

        if (target == ClipboardTarget.None)
            return;

        if (Input.GetKeyDown(KeyCode.C))
            BeginSelectionFromKeyboard(target, cut: false);
        else if (Input.GetKeyDown(KeyCode.X))
            BeginSelectionFromKeyboard(target, cut: true);
        else if (Input.GetKeyDown(KeyCode.V))
        {
            var shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            QueuePaste(target, alternatePaste: shiftPressed);
        }
    }

    public static void BeginSelection(LineEditor lineEditor, bool cut)
    {
        BeginSelection(lineEditor, GetActiveTarget(), cut, deleteOnly: false);
    }

    private static void BeginSelection(LineEditor lineEditor, ClipboardTarget target, bool cut, bool deleteOnly = false)
    {
        try
        {
            if (target == ClipboardTarget.None)
                return;

            // Il2Cpp delegates do not reliably capture locals; keep flags on a heap object.
            var request = new SelectionRequest
            {
                Target = target,
                Cut = cut,
                DeleteOnly = deleteOnly,
            };
            System.Action<Il2CppReferenceArray<Line>> completed = _ =>
                CompleteSelection(request.State, request.Target, request.Cut, request.DeleteOnly);
            var routine = lineEditor.SelectLines(
                cut || deleteOnly ? lineEditor.cutColor : lineEditor.copyColor,
                (Il2CppSystem.Action<Il2CppReferenceArray<Line>>)completed);
            request.State = routine.Cast<LineEditor._SelectLines_d__35>();
            if (request.State == null)
                throw new InvalidOperationException("无法读取原编辑器框选协程的时间范围。" );

            var coroutineOwner = SpeedEasePanel.Instance;
            if (coroutineOwner == null)
                throw new InvalidOperationException("画布工具面板尚未初始化。" );
            coroutineOwner.StartCoroutine(routine);
            var label = TargetLabel(target);
            var action = deleteOnly ? "删除" : cut ? "剪切" : "复制";
            SetStatus(target, $"{action}{label}：请按住左键拖出矩形框选区，按 Esc 或右键取消。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SetStatus(target, $"无法开始{TargetLabel(target)}框选：{error.Message}");
        }
    }

    private static void BeginSelectionFromKeyboard(ClipboardTarget target, bool cut, bool deleteOnly = false)
    {
        if (target == ClipboardTarget.ColorEvent && !HasFocusedCanvas())
        {
            SetStatus(target, "颜色框选前请先聚焦一个画布：原编辑器在没有聚焦画布时不会显示框选矩形。" );
            return;
        }

        var lineEditor = LineEditorInstance;
        if (lineEditor == null)
        {
            var returnTab = target switch
            {
                ClipboardTarget.Speed => "画布流速编辑",
                ClipboardTarget.CameraEvent => "摄像机编辑",
                ClipboardTarget.ColorEvent => "颜色编辑",
                _ => "画布编辑",
            };
            SetStatus(target, $"首次使用框选前，请先切换一次“线编辑”标签，再返回“{returnTab}”标签。" );
            return;
        }
        BeginSelection(lineEditor, target, cut, deleteOnly);
    }

    private static void BeginLineDeleteOnly()
    {
        var lineEditor = LineEditorInstance;
        if (lineEditor == null)
        {
            SpeedEasePanel.Instance?.SetEventStatus("无法删除线：LineEditor 尚未缓存。");
            return;
        }

        try
        {
            var request = new LineDeleteRequest
            {
                Editor = lineEditor,
                Saved = EventClipboardOperations.CaptureLines(lineEditor),
                SavedMirror = EventClipboardOperations.CaptureMirrorLines(lineEditor),
            };
            System.Action<Il2CppReferenceArray<Line>> completed = lines =>
            {
                try
                {
                    _suppressNativeLineExport = true;
                    try
                    {
                        request.Editor.CutLines(lines, false);
                    }
                    finally
                    {
                        _suppressNativeLineExport = false;
                    }

                    EventClipboardOperations.ReplaceLineClipboard(request.Editor, request.Saved);
                    EventClipboardOperations.ReplaceMirrorLineClipboard(request.Editor, request.SavedMirror);
                    var count = lines?.Length ?? 0;
                    var status = $"已删除 {count} 条线（未写入软件剪贴板）。";
                    SpeedEasePanel.Instance?.SetEventStatus(status);
                    Plugin.LogSource.LogInfo(status);
                }
                catch (Exception error)
                {
                    _suppressNativeLineExport = false;
                    Plugin.LogSource.LogError(error);
                    SpeedEasePanel.Instance?.SetEventStatus($"删除线失败：{error.Message}");
                }
            };
            var routine = lineEditor.SelectLines(
                lineEditor.cutColor,
                (Il2CppSystem.Action<Il2CppReferenceArray<Line>>)completed);
            var coroutineOwner = SpeedEasePanel.Instance
                ?? throw new InvalidOperationException("画布工具面板尚未初始化。");
            coroutineOwner.StartCoroutine(routine);
            SpeedEasePanel.Instance?.SetEventStatus("删除线：请按住左键拖出矩形框选区，按 Esc 或右键取消。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SpeedEasePanel.Instance?.SetEventStatus($"无法开始删除线框选：{error.Message}");
        }
    }

    private static bool IsTextInputActive()
    {
        try
        {
            return ProjectInformation.isInput;
        }
        catch
        {
            return false;
        }
    }

    public static void QueuePaste(bool alternatePaste)
    {
        QueuePaste(GetActiveTarget(), alternatePaste);
    }

    private static void QueuePaste(ClipboardTarget target, bool alternatePaste)
    {
        if (target == ClipboardTarget.None)
            return;
        _pendingPasteTarget = target;
        _pendingAlternatePaste = alternatePaste;
        _pastePending = true;
    }

    public static void FlushPendingPaste()
    {
        if (!_pastePending)
            return;

        var target = _pendingPasteTarget;
        var alternatePaste = _pendingAlternatePaste;
        _pastePending = false;
        _pendingPasteTarget = ClipboardTarget.None;

        try
        {
            switch (target)
            {
                case ClipboardTarget.Speed when SpeedEasePanel.TryGetActiveSpeedEditor(out var speedEditor):
                    SetStatus(target, EditorOperations.PasteSpeedClipboard(speedEditor, _speedClipboard, alternatePaste));
                    break;
                case ClipboardTarget.CanvasEvent when SpeedEasePanel.TryGetActiveCanvasEditor(out var canvasEditor):
                    SetStatus(target, EditorOperations.PasteCanvasEventClipboard(canvasEditor, _canvasEventClipboard, alternatePaste));
                    break;
                case ClipboardTarget.ColorEvent when SpeedEasePanel.TryGetActiveColorEditor(out var colorEditor):
                    SetStatus(target, EventClipboardOperations.PasteColorClipboard(
                        colorEditor,
                        _lineColorClipboard,
                        _judgeRingClipboard,
                        alternatePaste,
                        _lastColorCopyLineColor,
                        _lastColorCopyJudgeRing));
                    break;
                case ClipboardTarget.CameraEvent when SpeedEasePanel.TryGetActiveCameraEditor(out var cameraEditor):
                    SetStatus(target, EditorOperations.PasteCameraMoveClipboard(cameraEditor, _cameraClipboard, alternatePaste));
                    break;
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SetStatus(target, $"粘贴{TargetLabel(target)}失败：{error.Message}");
        }
    }

    private static void CompleteSelection(
        LineEditor._SelectLines_d__35? state,
        ClipboardTarget target,
        bool cut,
        bool deleteOnly)
    {
        try
        {
            if (state == null)
                throw new InvalidOperationException("原编辑器框选状态已经失效。" );

            ProjectInformation? project = null;
            SpeedEditor? speedEditor = null;
            CanvasEditor? canvasEditor = null;
            ColorEditor? colorEditor = null;
            CameraEditor? cameraEditor = null;
            if (target == ClipboardTarget.Speed && SpeedEasePanel.TryGetActiveSpeedEditor(out speedEditor))
                project = speedEditor.projectInformation;
            else if (target == ClipboardTarget.CanvasEvent && SpeedEasePanel.TryGetActiveCanvasEditor(out canvasEditor))
                project = canvasEditor.projectInformation;
            else if (target == ClipboardTarget.ColorEvent
                && SpeedEasePanel.TryGetActiveColorEditor(out colorEditor))
                project = colorEditor.projectInformation;
            else if (target == ClipboardTarget.CameraEvent && SpeedEasePanel.TryGetActiveCameraEditor(out cameraEditor))
                project = cameraEditor.projectInformation;
            else
                throw new InvalidOperationException($"框选结束时{TargetLabel(target)}所在标签已不再启用。" );

            if (project == null || project.chart == null)
                throw new InvalidOperationException("当前没有已打开的谱面。" );
            if (!float.IsFinite(project.speed) || Math.Abs(project.speed) < 1e-6f)
                throw new InvalidOperationException("当前编辑器垂直缩放速度无效。" );

            var firstBeat = state._startPositionY_5__7;
            var mouseY = Input.mousePosition.y;
            var secondRealTime = mouseY * state._scale_5__3
                / SelectionPixelsPerRealSecond
                / project.speed
                + state._yOffset_5__5;
            var secondBeat = project.chart.RealTimeToChartTime(secondRealTime);

            // deleteOnly reuses the cut mutation, then restores the previous software clipboard.
            var effectiveCut = cut || deleteOnly;
            var previousSpeed = _speedClipboard;
            var previousCanvas = _canvasEventClipboard;
            var previousLineColor = _lineColorClipboard;
            var previousJudgeRing = _judgeRingClipboard;
            var previousCamera = _cameraClipboard;
            string status;
            switch (target)
            {
                case ClipboardTarget.Speed:
                    EditorOperations.TryCopySpeedRange(speedEditor!, firstBeat, secondBeat, effectiveCut, out var speedSelected, out status);
                    if (deleteOnly)
                    {
                        _speedClipboard = previousSpeed;
                        status = ToDeleteStatus(status);
                    }
                    else if (speedSelected.Length > 0)
                    {
                        _speedClipboard = speedSelected;
                        status = AppendSystemExport(target, status);
                    }
                    break;
                case ClipboardTarget.CanvasEvent:
                    EditorOperations.TryCopyCanvasEventRange(canvasEditor!, firstBeat, secondBeat, effectiveCut, out var canvasSelected, out status);
                    if (deleteOnly)
                    {
                        _canvasEventClipboard = previousCanvas;
                        status = ToDeleteStatus(status);
                    }
                    else if (canvasSelected.Length > 0)
                    {
                        _canvasEventClipboard = canvasSelected;
                        status = AppendSystemExport(target, status);
                    }
                    break;
                case ClipboardTarget.ColorEvent:
                {
                    // The horizontal extent of the drag decides which of the two colour
                    // arrays is taken; the beat range decides which events inside it.
                    var side = ClassifyColorSelection(state, project);
                    EventClipboardOperations.TryCopyColorRange(
                        colorEditor!,
                        firstBeat,
                        secondBeat,
                        effectiveCut,
                        side != ColorSide.Right,
                        side != ColorSide.Left,
                        out var lineColorSelected,
                        out var judgeRingSelected,
                        out status);
                    status = $"{status}（{ColorSideLabel(side)}）";
                    if (deleteOnly)
                    {
                        _lineColorClipboard = previousLineColor;
                        _judgeRingClipboard = previousJudgeRing;
                        status = ToDeleteStatus(status);
                    }
                    else if (lineColorSelected.Length > 0 || judgeRingSelected.Length > 0)
                    {
                        // Paste follows the most recent copy that actually produced content.
                        _lastColorCopyLineColor = lineColorSelected.Length > 0;
                        _lastColorCopyJudgeRing = judgeRingSelected.Length > 0;
                        if (_lastColorCopyLineColor)
                            _lineColorClipboard = lineColorSelected;
                        if (_lastColorCopyJudgeRing)
                            _judgeRingClipboard = judgeRingSelected;
                        status = AppendSystemExport(target, status);
                    }

                    break;
                }
                case ClipboardTarget.CameraEvent:
                    EditorOperations.TryCopyCameraMoveRange(cameraEditor!, firstBeat, secondBeat, effectiveCut, out var cameraSelected, out status);
                    if (deleteOnly)
                    {
                        _cameraClipboard = previousCamera;
                        status = ToDeleteStatus(status);
                    }
                    else if (cameraSelected.Length > 0)
                    {
                        _cameraClipboard = cameraSelected;
                        status = AppendSystemExport(target, status);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"当前标签不能框选{TargetLabel(target)}。");
            }
            SetStatus(target, status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            var action = deleteOnly ? "删除" : cut ? "剪切" : "复制";
            SetStatus(target, $"{action}{TargetLabel(target)}失败：{error.Message}");
        }
    }

    private enum ColorSide
    {
        Left,
        Right,
        Both,
    }

    private static string ColorSideLabel(ColorSide side) => side switch
    {
        ColorSide.Left => "框选在 x=0 左侧，只取线色覆盖",
        ColorSide.Right => "框选在 x=0 右侧，只取判定环",
        _ => "框选跨过 x=0，两者都取",
    };

    /// <summary>
    /// Classifies a colour drag by its horizontal extent against ground x = 0.
    /// The two screen ends are the coroutine's drag start and the current mouse, both
    /// converted with the editor camera — the same conversion the canvas paste anchor uses.
    /// Measured 2026-09-22: _selectionStart_5__2 is the drag start in screen coordinates,
    /// while _startPositionX_5__6 is a canvas-space value and must not be used here.
    /// </summary>
    private static ColorSide ClassifyColorSelection(
        LineEditor._SelectLines_d__35 state,
        ProjectInformation project)
    {
        var camera = project.inputControl?._camera
            ?? throw new InvalidOperationException("原编辑器尚未初始化鼠标坐标换算组件。");
        var start = state._selectionStart_5__2;
        var end = Input.mousePosition;
        var startX = camera.ScreenToWorldPoint(new Vector3(start.x, start.y, 0f)).x;
        var endX = camera.ScreenToWorldPoint(new Vector3(end.x, end.y, 0f)).x;
        if (!float.IsFinite(startX) || !float.IsFinite(endX))
            throw new InvalidOperationException("原编辑器相机返回了无效的框选横坐标。" );

        var min = Math.Min(startX, endX);
        var max = Math.Max(startX, endX);
        if (max <= 0f)
            return ColorSide.Left;
        if (min >= 0f)
            return ColorSide.Right;
        return ColorSide.Both;
    }

    private static string ToDeleteStatus(string cutStatus)
    {
        return cutStatus
            .Replace("已剪切并复制", "已删除", StringComparison.Ordinal)
            .Replace("已剪切", "已删除", StringComparison.Ordinal)
            + "（未写入软件剪贴板）";
    }

    public static string ImportSystemClipboard(ClipboardTarget target)
    {
        try
        {
            var text = SystemClipboard.GetText();
            if (target == ClipboardTarget.Speed)
            {
                if (!EditorClipboardText.TryParseSpeed(text, out var entries, out var error))
                    return error;

                var imported = new SpeedClipboardPoint[entries.Length];
                for (var index = 0; index < entries.Length; index++)
                {
                    var entry = entries[index];
                    imported[index] = new SpeedClipboardPoint((float)entry.Beat, (float)entry.Value, entry.EaseType);
                }

                _speedClipboard = imported;
                return $"已用系统剪切板替换内部流速剪贴板，共 {imported.Length} 个节点。";
            }

            if (target == ClipboardTarget.ColorEvent)
            {
                // A colour copy may have written both blocks, so each header is located
                // inside the text rather than assumed to sit at the top.
                var lineColorText = ExtractBlock(text, EditorClipboardText.LineColorHeader);
                var judgeRingText = ExtractBlock(text, EditorClipboardText.JudgeRingHeader);
                if (lineColorText == null && judgeRingText == null)
                    return "系统剪切板里没有线色覆盖或判定环事件的区块。";

                var parts = new List<string>();
                if (lineColorText != null)
                {
                    if (!EditorClipboardText.TryParseColorEvents(
                            lineColorText, EditorClipboardText.LineColorHeader, "线色覆盖", out var lineColors, out var lineColorError))
                        return lineColorError;
                    _lineColorClipboard = lineColors;
                    _lastColorCopyLineColor = true;
                    parts.Add($"{lineColors.Length} 个线色覆盖事件");
                }

                if (judgeRingText != null)
                {
                    if (!EditorClipboardText.TryParseColorEvents(
                            judgeRingText, EditorClipboardText.JudgeRingHeader, "判定环", out var judgeRing, out var judgeRingError))
                        return judgeRingError;
                    _judgeRingClipboard = judgeRing;
                    _lastColorCopyJudgeRing = true;
                    parts.Add($"{judgeRing.Length} 个判定环事件");
                }

                return $"已用系统剪切板替换内部颜色剪贴板，共 {string.Join("和", parts)}。";
            }

            if (target == ClipboardTarget.CameraEvent)
            {
                if (!EditorClipboardText.TryParseCameraEvents(text, out var cameraEvents, out var cameraError))
                    return cameraError;
                _cameraClipboard = cameraEvents;
                return $"已用系统剪切板替换内部摄像机事件剪贴板，共 {cameraEvents.Length} 个事件。";
            }

            if (target == ClipboardTarget.Lines)
            {
                if (!EditorClipboardText.TryParseLines(text, out var lines, out var lineError))
                    return lineError;
                if (!SpeedEasePanel.TryGetActiveLineEditor(out var lineEditor))
                    return "请先打开画线工具，再导入线剪贴板。";
                CrossCanvasLineClipboard.ForgetClipboard("系统剪切板导入线");
                return EventClipboardOperations.ReplaceLineClipboard(lineEditor, lines);
            }

            if (!EditorClipboardText.TryParseCanvasEvents(text, out var events, out var canvasError))
                return canvasError;

            _canvasEventClipboard = events;
            return $"已用系统剪切板替换内部画布事件剪贴板，共 {events.Length} 个事件。";
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            return $"读取系统剪切板失败：{error.Message}";
        }
    }

    /// <summary>
    /// Returns the slice of clipboard text that belongs to one block, or null when that
    /// block is absent. A colour copy can legitimately write both the line-colour and the
    /// judge-ring block, so the requested header must be located rather than assumed first.
    /// </summary>
    private static string? ExtractBlock(string? text, string header)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var start = Array.IndexOf(lines, header);
        if (start < 0)
            return null;

        var end = lines.Length;
        for (var index = start + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("rzemod-clipboard ", StringComparison.Ordinal))
            {
                end = index;
                break;
            }
        }

        return string.Join('\n', lines, start, end - start);
    }

    private static string AppendSystemExport(ClipboardTarget target, string status)
    {
        if (!SpeedEasePanel.ExportToSystemClipboard)
            return status;

        try
        {
            SystemClipboard.SetText(FormatSystemClipboard(target));
            var exported = status + "已写入系统剪切板。";
            SpeedEasePanel.Instance?.SetClipboardMessage(exported);
            return exported;
        }
            catch (Exception error)
            {
                Plugin.LogSource.LogError(error);
                var failed = status + $"写入系统剪切板失败：{error.Message}";
                SpeedEasePanel.Instance?.SetClipboardMessage(failed);
                return failed;
            }
    }

    private static string FormatSystemClipboard(ClipboardTarget target)
    {
        switch (target)
        {
            case ClipboardTarget.CanvasEvent:
                return EditorClipboardText.FormatCanvasEvents(_canvasEventClipboard);
            case ClipboardTarget.ColorEvent:
            {
                var blocks = new List<string>();
                if (_lastColorCopyLineColor)
                    blocks.Add(EditorClipboardText.FormatColorEvents(EditorClipboardText.LineColorHeader, _lineColorClipboard));
                if (_lastColorCopyJudgeRing)
                    blocks.Add(EditorClipboardText.FormatColorEvents(EditorClipboardText.JudgeRingHeader, _judgeRingClipboard));
                return string.Join('\n', blocks);
            }
            case ClipboardTarget.CameraEvent:
                return EditorClipboardText.FormatCameraEvents(_cameraClipboard);
            default:
                var entries = new SpeedClipboardEntry[_speedClipboard.Length];
                for (var index = 0; index < _speedClipboard.Length; index++)
                {
                    var point = _speedClipboard[index];
                    entries[index] = new SpeedClipboardEntry(point.Beat, point.Value, point.EaseType);
                }

                return EditorClipboardText.FormatSpeed(entries);
        }
    }

    /// <summary>
    /// The original selection rectangle only appears while a canvas is focused. Starting a
    /// colour selection without one leaves the user with no box at all, so the plugin refuses
    /// up front and also leaves the original editor's own actions alone in that state.
    /// </summary>
    internal static bool HasFocusedCanvas()
    {
        try
        {
            var project = ProjectInformation.instance;
            return project != null && project.focusCanvasIndex >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static ClipboardTarget GetActiveTarget()
    {
        if (SpeedEasePanel.ColorEditorIsActive)
            return ClipboardTarget.ColorEvent;
        if (SpeedEasePanel.CameraEditorIsActive)
            return ClipboardTarget.CameraEvent;
        if (SpeedEasePanel.SpeedEditorIsActive)
            return ClipboardTarget.Speed;
        if (SpeedEasePanel.CanvasEditorIsActive)
            return ClipboardTarget.CanvasEvent;
        return ClipboardTarget.None;
    }

    private static string TargetLabel(ClipboardTarget target)
    {
        return target switch
        {
            ClipboardTarget.CanvasEvent => "画布事件",
            ClipboardTarget.ColorEvent => "颜色事件",
            ClipboardTarget.CameraEvent => "摄像机移动",
            ClipboardTarget.Lines => "线",
            _ => "流速",
        };
    }

    private static void SetStatus(ClipboardTarget target, string status)
    {
        switch (target)
        {
            case ClipboardTarget.CanvasEvent:
                SpeedEasePanel.Instance?.SetCanvasStatus(status);
                break;
            case ClipboardTarget.ColorEvent:
            case ClipboardTarget.CameraEvent:
            case ClipboardTarget.Lines:
                SpeedEasePanel.Instance?.SetEventStatus(status);
                break;
            default:
                SpeedEasePanel.Instance?.SetSpeedStatus(status);
                break;
        }

        Plugin.LogSource.LogInfo(status);
    }

    internal static void ExportNativeLines(LineEditor editor)
    {
        if (_suppressNativeLineExport || !SpeedEasePanel.ExportToSystemClipboard)
            return;

        try
        {
            var lines = EventClipboardOperations.CaptureLines(editor);
            if (lines.Length == 0)
                return;
            SystemClipboard.SetText(EditorClipboardText.FormatLines(lines));
            var status = $"已把 {lines.Length} 条线写入系统剪切板。";
            SpeedEasePanel.Instance?.SetEventStatus(status);
            Plugin.LogSource.LogInfo(status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SpeedEasePanel.Instance?.SetEventStatus($"写入线剪切板失败：{error.Message}");
        }
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.Start))]
internal static class LineEditorCachePatch
{
    private static void Postfix(LineEditor __instance)
    {
        SpeedClipboardController.LineEditorInstance = __instance;
    }
}

/// <summary>
/// The plugin owns colour copy/paste for the colour tool, so the original editor's own actions
/// must not also run. Its clipboard is a separate list, and running both leaves two events on
/// one beat — the later index then wins the same-time priority rule and the rendered colour
/// changes (docs/DECISIONS.md D-019).
///
/// When no canvas is focused the original selection rectangle never appears, so the plugin
/// cannot act either; the original actions are left alone in that state rather than blocked,
/// otherwise Ctrl+C/X/V would do nothing at all.
/// </summary>
[HarmonyPatch(typeof(ColorEditor), "CopyColors")]
internal static class ColorEditorCopyColorsPatch
{
    private static bool Prefix() =>
        !SpeedEasePanel.ColorEditorIsActive || !SpeedClipboardController.HasFocusedCanvas();
}

[HarmonyPatch(typeof(ColorEditor), "PasteColors")]
internal static class ColorEditorPasteColorsPatch
{
    private static bool Prefix() =>
        !SpeedEasePanel.ColorEditorIsActive || !SpeedClipboardController.HasFocusedCanvas();
}

[HarmonyPatch(typeof(LineEditor), "_Start_b__28_3")]
internal static class SpeedCopyShortcutPatch
{
    private static bool Prefix(LineEditor __instance)
    {
        if (SpeedEasePanel.ClipboardEditorIsActive)
        {
            SpeedClipboardController.BeginSelection(__instance, cut: false);
            return false;
        }

        // Line tool: the plugin owns the drag so Shift can select whole cross-canvas lines; without
        // Shift the completion handler runs the original copy unchanged.
        if (SpeedEasePanel.LineEditorIsActive)
        {
            CrossCanvasLineClipboard.BeginSelection(__instance, cut: false);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(LineEditor), "_Start_b__28_5")]
internal static class SpeedCutShortcutPatch
{
    private static bool Prefix(LineEditor __instance)
    {
        if (SpeedEasePanel.ClipboardEditorIsActive)
        {
            SpeedClipboardController.BeginSelection(__instance, cut: true);
            return false;
        }

        if (SpeedEasePanel.LineEditorIsActive)
        {
            CrossCanvasLineClipboard.BeginSelection(__instance, cut: true);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(LineEditor), "_Start_b__28_8")]
internal static class SpeedPasteExactShortcutPatch
{
    private static bool Prefix()
    {
        if (SpeedEasePanel.ClipboardEditorIsActive)
        {
            SpeedClipboardController.QueuePaste(alternatePaste: false);
            return false;
        }

        // Only when the plugin holds a cross-canvas clipboard; otherwise the original paste runs.
        return !CrossCanvasLineClipboard.QueuePaste(mirror: false);
    }
}

[HarmonyPatch(typeof(LineEditor), "_Start_b__28_9")]
internal static class SpeedPasteAlignedShortcutPatch
{
    private static bool Prefix()
    {
        if (SpeedEasePanel.ClipboardEditorIsActive)
        {
            SpeedClipboardController.QueuePaste(alternatePaste: true);
            return false;
        }

        return !CrossCanvasLineClipboard.QueuePaste(mirror: true);
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.CopyLines))]
internal static class LineCopyExportPatch
{
    private static void Postfix(LineEditor __instance)
    {
        SpeedClipboardController.ExportNativeLines(__instance);
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.CutLines))]
internal static class LineCutExportPatch
{
    private static void Postfix(LineEditor __instance)
    {
        SpeedClipboardController.ExportNativeLines(__instance);
    }
}
