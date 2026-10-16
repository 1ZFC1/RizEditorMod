using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

internal readonly record struct CapturedEndpoint(int CanvasIndex, float Beat, float Value);
internal readonly record struct SpeedClipboardPoint(float Beat, float Value, int EaseType);
internal readonly record struct CanvasPasteAnchor(float Beat, float Value);

internal static class EditorOperations
{
    private const float BeatTolerance = 1e-5f;
    private const float CaptureBeatTolerance = 1e-4f;
    // Constants used by the original CanvasEditor.Update/InputControl.GetMousePosition.
    internal const float CanvasEditorWorldUnitsPerChartUnit = 5.5625f;
    private const float EditorTimelineWorldYOffset = 2.4f;

    public static bool TryCaptureSpeedPointAtCurrentBeat(SpeedEditor? editor, out CapturedEndpoint endpoint, out string error)
    {
        endpoint = default;
        error = string.Empty;
        if (editor == null)
        {
            error = "当前无法访问流速编辑器，请确认谱面已经打开。";
            return false;
        }

        try
        {
            var project = RequireProject(editor);
            var canvasIndex = ProjectInformation.SelectedCanvas;
            var currentBeat = ProgressControl.GetBeats();
            var speedPoints = RequireCanvas(project, canvasIndex).speedKeyPoints;
            var foundIndex = -1;
            for (var index = 0; index < speedPoints.Count; index++)
            {
                if (Math.Abs(speedPoints[index].time - currentBeat) > CaptureBeatTolerance)
                    continue;
                if (foundIndex >= 0)
                {
                    error = $"当前画布 {canvasIndex} 在第 {currentBeat:0.######} 拍存在多个流速节点，无法确定唯一端点。";
                    return false;
                }
                foundIndex = index;
            }

            if (foundIndex < 0)
            {
                error = $"当前画布 {canvasIndex} 在第 {currentBeat:0.######} 拍没有流速节点，请先在这一拍设置流速。";
                return false;
            }

            var point = speedPoints[foundIndex];
            endpoint = new CapturedEndpoint(canvasIndex, point.time, point.value);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.LogSource.LogError(exception);
            error = $"读取当前拍流速失败：{exception.Message}";
            return false;
        }
    }

    public static long EstimateSamples(CapturedEndpoint start, CapturedEndpoint end, int splitCount)
    {
        ValidateEndpoints(start, end);
        return SpeedEditing.EstimateSampleCount(start.Beat, end.Beat, splitCount);
    }

    public static string ApplyEase(
        SpeedEditor editor,
        CapturedEndpoint start,
        CapturedEndpoint end,
        int splitCount,
        int easingType)
    {
        ValidateEndpoints(start, end);
        var project = RequireProject(editor);
        var speedPoints = RequireCanvas(project, start.CanvasIndex).speedKeyPoints;
        ValidateOrdered(speedPoints);

        var startIndex = FindUniquePointIndex(speedPoints, start.Beat, "起点");
        var endIndex = FindUniquePointIndex(speedPoints, end.Beat, "终点");
        if (endIndex <= startIndex)
            throw new InvalidOperationException("所选端点的顺序与当前流速节点列表不再一致，请重新记录端点。" );

        var currentStart = speedPoints[startIndex];
        var currentEnd = speedPoints[endIndex];
        var samples = SpeedEditing.GenerateSamples(
            currentStart.time,
            currentEnd.time,
            currentStart.value,
            currentEnd.value,
            splitCount,
            progress => GetEase.GetEaseWithProgress((float)progress, easingType));

        var removedCount = endIndex - startIndex - 1;
        // float cast can collapse the first internal sample onto the start node (首位节点),
        // which leaves two speed events at one beat. Skip endpoint-colliding inserts.
        var insertSamples = SpeedEditing.FilterInsertSamples(samples, currentStart.time, currentEnd.time);
        var insertedCount = insertSamples.Count;
        System.Action mutation = () =>
        {
            for (var index = endIndex - 1; index > startIndex; index--)
                speedPoints.RemoveAt(index);

            var insertAt = startIndex + 1;
            foreach (var sample in insertSamples)
            {
                speedPoints.Insert(insertAt++, new KeyPoint
                {
                    time = (float)sample.Beat,
                    value = (float)sample.Value,
                    easeType = 0,
                    floorPosition = 0,
                });
            }

            project.editorControl.SetFloorPosition();
        };

        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        Refresh(editor, project);
        return $"生成完成：区间共 {2 + insertedCount} 个节点，其中新增 {insertedCount} 个，并替换了 {removedCount} 个原区间内部节点。";
    }

    public static string FormatChartStatistics(Chart? chart)
    {
        if (chart == null)
            return "当前没有已打开的谱面。";

        var canvasCount = chart.canvasMoves?.Count ?? 0;
        var lineCount = chart.lines?.Count ?? 0;
        var tap = 0;
        var drag = 0;
        var hold = 0;
        var other = 0;
        if (chart.lines != null)
        {
            for (var lineIndex = 0; lineIndex < chart.lines.Count; lineIndex++)
            {
                var notes = chart.lines[lineIndex]?.notes;
                if (notes == null)
                    continue;
                for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    switch (notes[noteIndex].type)
                    {
                        case 0:
                            tap++;
                            break;
                        case 1:
                            drag++;
                            break;
                        case 2:
                            hold++;
                            break;
                        default:
                            other++;
                            break;
                    }
                }
            }
        }

        var totalNotes = tap + drag + hold + other;
        var otherText = other > 0 ? $"\n其他音符：{other}" : string.Empty;
        return $"画布：{canvasCount}\n线：{lineCount}\n音符合计：{totalNotes}\nTap：{tap}\nHold：{hold}\nDrag：{drag}{otherText}";
    }

    public static bool TryCopySpeedRange(
        SpeedEditor editor,
        float firstBeat,
        float secondBeat,
        bool cut,
        out SpeedClipboardPoint[] clipboard,
        out string status)
    {
        clipboard = Array.Empty<SpeedClipboardPoint>();
        status = string.Empty;
        try
        {
            var project = RequireProject(editor);
            var canvasIndex = ProjectInformation.SelectedCanvas;
            var speedPoints = RequireCanvas(project, canvasIndex).speedKeyPoints;
            ValidateOrdered(speedPoints);
            var duplicateNote = DescribeSameBeatRepeats(speedPoints);
            var beats = BeatsOf(speedPoints);

            var selectedIndices = SpeedEditing.FindClosedRangeIndices(beats, firstBeat, secondBeat);
            if (selectedIndices.Count == 0)
            {
                status = $"画布 {canvasIndex} 的第 {Math.Min(firstBeat, secondBeat):0.###}–{Math.Max(firstBeat, secondBeat):0.###} 拍内没有流速节点，剪贴板未改变{duplicateNote}。";
                return true;
            }

            var selected = new SpeedClipboardPoint[selectedIndices.Count];
            for (var index = 0; index < selectedIndices.Count; index++)
            {
                var point = speedPoints[selectedIndices[index]];
                selected[index] = new SpeedClipboardPoint(point.time, point.value, point.easeType);
            }

            // Same-beat pairs inside the selection would make the clipboard itself invalid for
            // pasting (paste requires strictly increasing beats), so the earlier one is dropped here
            // and the later one - the one the game gives priority to - is kept.
            var collapsed = SpeedEditing.CollapseSameBeatKeepLast(selected, point => point.Beat, BeatTolerance);
            clipboard = collapsed is SpeedClipboardPoint[] array ? array : collapsed.ToArray();
            var merged = selected.Length - clipboard.Length;

            if (!cut)
            {
                status = $"已复制画布 {canvasIndex} 的 {clipboard.Length} 个流速节点（第 {clipboard[0].Beat:0.###}–{clipboard[^1].Beat:0.###} 拍）"
                    + (merged > 0 ? $"，已合并 {merged} 个同拍项（后一项优先）" : string.Empty)
                    + $"{duplicateNote}。";
                return true;
            }

            var deletionIndices = SpeedEditing.FindClosedRangeIndices(
                beats,
                firstBeat,
                secondBeat,
                excludedIndex: 0,
                descending: true);
            if (deletionIndices.Count > 0)
            {
                System.Action mutation = () =>
                {
                    foreach (var index in deletionIndices)
                        speedPoints.RemoveAt(index);
                    project.editorControl.SetFloorPosition();
                };
                project.editorControl.Stepping((Il2CppSystem.Action)mutation);
                Refresh(editor, project);
            }

            var protectedText = selectedIndices[0] == 0 ? "；首个流速节点已保留" : string.Empty;
            status = $"已剪切并复制画布 {canvasIndex} 的 {clipboard.Length} 个流速节点，删除其中 {deletionIndices.Count} 个{protectedText}"
                + (merged > 0 ? $"，剪贴板合并了 {merged} 个同拍项（后一项优先）" : string.Empty)
                + $"{duplicateNote}。";
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            status = $"{(cut ? "剪切" : "复制")}流速失败：{error.Message}";
            return true;
        }
    }

    public static string PasteSpeedClipboard(
        SpeedEditor editor,
        IReadOnlyList<SpeedClipboardPoint> clipboard,
        bool alignToCurrentBeat)
    {
        if (clipboard.Count == 0)
            throw new InvalidOperationException("流速剪贴板为空，请先框选复制或剪切。" );

        var project = RequireProject(editor);
        var canvasIndex = ProjectInformation.SelectedCanvas;
        var speedPoints = RequireCanvas(project, canvasIndex).speedKeyPoints;
        ValidateOrdered(speedPoints);
        var duplicateNote = DescribeSameBeatRepeats(speedPoints);

        // A clipboard filled by an older version (or imported from text) may still hold two entries
        // for one beat; pasting requires strictly increasing beats, so the later entry wins here too.
        var collapsedClipboard = SpeedEditing.CollapseSameBeatKeepLast(clipboard, point => point.Beat, BeatTolerance);
        var droppedFromClipboard = clipboard.Count - collapsedClipboard.Count;
        if (droppedFromClipboard > 0)
        {
            Plugin.LogSource.LogWarning(
                $"流速剪贴板中有 {droppedFromClipboard} 个同拍重复项，已按「后一项优先」合并后再粘贴。");
            clipboard = collapsedClipboard;
        }

        var sourceBeats = new double[clipboard.Count];
        for (var index = 0; index < clipboard.Count; index++)
            sourceBeats[index] = clipboard[index].Beat;
        var targetBeats = SpeedEditing.CreatePasteBeats(
            sourceBeats,
            alignToCurrentBeat ? ProgressControl.GetBeats() : null);

        var existingBeats = new double[speedPoints.Count];
        for (var index = 0; index < speedPoints.Count; index++)
            existingBeats[index] = speedPoints[index].time;
        // Same tolerance as the insert step below, so a node a few micro-beats from a target beat is
        // replaced instead of surviving next to the pasted one.
        var replacementIndices = SpeedEditing.FindPasteReplacementIndices(existingBeats, targetBeats, BeatTolerance);

        System.Action mutation = () =>
        {
            foreach (var index in replacementIndices)
                speedPoints.RemoveAt(index);

            for (var clipboardIndex = 0; clipboardIndex < clipboard.Count; clipboardIndex++)
            {
                var source = clipboard[clipboardIndex];
                var targetBeat = (float)targetBeats[clipboardIndex];
                var insertAt = 0;
                while (insertAt < speedPoints.Count && speedPoints[insertAt].time < targetBeat - BeatTolerance)
                    insertAt++;
                speedPoints.Insert(insertAt, new KeyPoint
                {
                    time = targetBeat,
                    value = source.Value,
                    easeType = source.EaseType,
                    floorPosition = 0,
                });
            }
            project.editorControl.SetFloorPosition();
        };

        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        Refresh(editor, project);
        var mode = alignToCurrentBeat ? "对齐当前播放头" : "保持原拍数";
        var clipboardNote = droppedFromClipboard > 0 ? $"，剪贴板中 {droppedFromClipboard} 个同拍项已合并" : string.Empty;
        return $"已向画布 {canvasIndex} 粘贴 {clipboard.Count} 个流速节点（{mode}），覆盖 {replacementIndices.Count} 个同拍旧节点"
            + $"{clipboardNote}{duplicateNote}。";
    }

    public static bool TryCopyCanvasEventRange(
        CanvasEditor editor,
        float firstBeat,
        float secondBeat,
        bool cut,
        out CanvasEventClipboardPoint[] clipboard,
        out string status)
    {
        clipboard = Array.Empty<CanvasEventClipboardPoint>();
        status = string.Empty;
        try
        {
            var project = RequireProject(editor);
            var canvasIndex = ProjectInformation.SelectedCanvas;
            var eventPoints = RequireCanvas(project, canvasIndex).xPositionKeyPoints;
            ValidateOrdered(eventPoints, "画布事件");
            var beats = new double[eventPoints.Count];
            for (var index = 0; index < eventPoints.Count; index++)
                beats[index] = eventPoints[index].time;

            var selectedIndices = SpeedEditing.FindClosedRangeIndices(beats, firstBeat, secondBeat);
            if (selectedIndices.Count == 0)
            {
                status = $"画布 {canvasIndex} 的第 {Math.Min(firstBeat, secondBeat):0.###}–{Math.Max(firstBeat, secondBeat):0.###} 拍内没有画布事件，剪贴板未改变。";
                return true;
            }

            clipboard = new CanvasEventClipboardPoint[selectedIndices.Count];
            for (var index = 0; index < selectedIndices.Count; index++)
            {
                var point = eventPoints[selectedIndices[index]];
                clipboard[index] = new CanvasEventClipboardPoint(
                    point.time,
                    point.value,
                    point.easeType,
                    point.floorPosition);
            }

            if (!cut)
            {
                status = $"已复制画布 {canvasIndex} 的 {clipboard.Length} 个画布事件（第 {clipboard[0].Beat:0.###}–{clipboard[^1].Beat:0.###} 拍）。";
                return true;
            }

            var deletionIndices = SpeedEditing.FindClosedRangeIndices(
                beats,
                firstBeat,
                secondBeat,
                excludedIndex: 0,
                descending: true);
            if (deletionIndices.Count > 0)
            {
                System.Action mutation = () =>
                {
                    foreach (var index in deletionIndices)
                        eventPoints.RemoveAt(index);
                };
                project.editorControl.Stepping((Il2CppSystem.Action)mutation);
                Refresh(editor, project);
            }

            var protectedText = selectedIndices[0] == 0 ? "；首个画布事件已保留" : string.Empty;
            status = $"已剪切并复制画布 {canvasIndex} 的 {clipboard.Length} 个画布事件，删除其中 {deletionIndices.Count} 个{protectedText}。";
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            status = $"{(cut ? "剪切" : "复制")}画布事件失败：{error.Message}";
            return true;
        }
    }

    public static string PasteCanvasEventClipboard(
        CanvasEditor editor,
        IReadOnlyList<CanvasEventClipboardPoint> clipboard,
        bool mirrorHorizontally)
    {
        if (clipboard.Count == 0)
            throw new InvalidOperationException("画布事件剪贴板为空，请先框选复制或剪切。" );

        var project = RequireProject(editor);
        var canvasIndex = ProjectInformation.SelectedCanvas;
        var eventPoints = RequireCanvas(project, canvasIndex).xPositionKeyPoints;
        ValidateOrdered(eventPoints, "画布事件");
        var mouseAnchor = GetCanvasPasteAnchor(project);

        var pasted = CanvasEventEditing.CreatePastePoints(
            clipboard,
            mouseAnchor.Beat,
            mouseAnchor.Value,
            mirrorHorizontally);
        var targetBeats = new double[pasted.Count];
        for (var index = 0; index < pasted.Count; index++)
            targetBeats[index] = pasted[index].Beat;

        var existingBeats = new double[eventPoints.Count];
        for (var index = 0; index < eventPoints.Count; index++)
            existingBeats[index] = eventPoints[index].time;
        var replacementIndices = SpeedEditing.FindPasteReplacementIndices(existingBeats, targetBeats);

        System.Action mutation = () =>
        {
            foreach (var index in replacementIndices)
                eventPoints.RemoveAt(index);

            for (var clipboardIndex = 0; clipboardIndex < pasted.Count; clipboardIndex++)
            {
                var source = pasted[clipboardIndex];
                var targetBeat = (float)source.Beat;
                var insertAt = 0;
                while (insertAt < eventPoints.Count && eventPoints[insertAt].time < targetBeat - BeatTolerance)
                    insertAt++;
                eventPoints.Insert(insertAt, new KeyPoint
                {
                    time = targetBeat,
                    value = (float)source.Value,
                    easeType = source.EaseType,
                    floorPosition = (float)source.FloorPosition,
                });
            }
        };

        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        Refresh(editor, project);
        var mode = mirrorHorizontally ? "镜像并对齐鼠标" : "对齐鼠标";
        return $"已向画布 {canvasIndex} 粘贴 {clipboard.Count} 个画布事件（{mode}），覆盖 {replacementIndices.Count} 个同拍旧事件。";
    }

    public static bool TryCopyCameraMoveRange(
        CameraEditor editor,
        float firstBeat,
        float secondBeat,
        bool cut,
        out CanvasEventClipboardPoint[] clipboard,
        out string status)
    {
        clipboard = Array.Empty<CanvasEventClipboardPoint>();
        status = string.Empty;
        try
        {
            var project = RequireProject(editor);
            var cameraMove = RequireCameraMove(project);
            switch (IdentifyCameraList(editor, cameraMove))
            {
                case CameraListKind.Scale:
                    status = "摄像机缩放不支持框选，剪贴板未改变。";
                    return true;
                case CameraListKind.Unknown:
                    status = "当前列表不是摄像机移动，剪贴板未改变。";
                    return true;
            }

            var eventPoints = cameraMove.xPositionKeyPoints;
            ValidateOrdered(eventPoints, "摄像机移动");
            var beats = new double[eventPoints.Count];
            for (var index = 0; index < eventPoints.Count; index++)
                beats[index] = eventPoints[index].time;

            var selectedIndices = SpeedEditing.FindClosedRangeIndices(beats, firstBeat, secondBeat);
            if (selectedIndices.Count == 0)
            {
                status = $"第 {Math.Min(firstBeat, secondBeat):0.###}–{Math.Max(firstBeat, secondBeat):0.###} 拍内没有摄像机移动，剪贴板未改变。";
                return true;
            }

            clipboard = new CanvasEventClipboardPoint[selectedIndices.Count];
            for (var index = 0; index < selectedIndices.Count; index++)
            {
                var point = eventPoints[selectedIndices[index]];
                clipboard[index] = new CanvasEventClipboardPoint(
                    point.time,
                    point.value,
                    point.easeType,
                    point.floorPosition);
            }

            if (!cut)
            {
                status = $"已复制 {clipboard.Length} 个摄像机移动（第 {clipboard[0].Beat:0.###}–{clipboard[^1].Beat:0.###} 拍）。";
                return true;
            }

            var deletionIndices = SpeedEditing.FindClosedRangeIndices(
                beats,
                firstBeat,
                secondBeat,
                excludedIndex: 0,
                descending: true);
            if (deletionIndices.Count > 0)
            {
                System.Action mutation = () =>
                {
                    foreach (var index in deletionIndices)
                        eventPoints.RemoveAt(index);
                };
                project.editorControl.Stepping((Il2CppSystem.Action)mutation);
                RefreshCameraMove(editor, project);
            }

            var protectedText = selectedIndices[0] == 0 ? "；首个摄像机移动已保留" : string.Empty;
            status = $"已剪切并复制 {clipboard.Length} 个摄像机移动，删除其中 {deletionIndices.Count} 个{protectedText}。";
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            status = $"{(cut ? "剪切" : "复制")}摄像机移动失败：{error.Message}";
            return true;
        }
    }

    public static string PasteCameraMoveClipboard(
        CameraEditor editor,
        IReadOnlyList<CanvasEventClipboardPoint> clipboard,
        bool mirrorHorizontally)
    {
        if (clipboard.Count == 0)
            throw new InvalidOperationException("摄像机移动剪贴板为空，请先框选复制或剪切。");

        var project = RequireProject(editor);
        var cameraMove = RequireCameraMove(project);
        switch (IdentifyCameraList(editor, cameraMove))
        {
            case CameraListKind.Scale:
                throw new InvalidOperationException("摄像机缩放不支持框选。请改到摄像机移动后再粘贴。");
            case CameraListKind.Unknown:
                throw new InvalidOperationException("当前列表不是摄像机移动，不能粘贴。");
        }

        var eventPoints = cameraMove.xPositionKeyPoints;
        ValidateOrdered(eventPoints, "摄像机移动");
        var mouseAnchor = GetCanvasPasteAnchor(project, "摄像机移动");
        var pasted = CanvasEventEditing.CreatePastePoints(
            clipboard,
            mouseAnchor.Beat,
            mouseAnchor.Value,
            mirrorHorizontally);
        var targetBeats = new double[pasted.Count];
        for (var index = 0; index < pasted.Count; index++)
            targetBeats[index] = pasted[index].Beat;

        var existingBeats = new double[eventPoints.Count];
        for (var index = 0; index < eventPoints.Count; index++)
            existingBeats[index] = eventPoints[index].time;
        var replacementIndices = SpeedEditing.FindPasteReplacementIndices(existingBeats, targetBeats);

        System.Action mutation = () =>
        {
            foreach (var index in replacementIndices)
                eventPoints.RemoveAt(index);

            for (var clipboardIndex = 0; clipboardIndex < pasted.Count; clipboardIndex++)
            {
                var source = pasted[clipboardIndex];
                var targetBeat = (float)source.Beat;
                var insertAt = 0;
                while (insertAt < eventPoints.Count && eventPoints[insertAt].time < targetBeat - BeatTolerance)
                    insertAt++;
                eventPoints.Insert(insertAt, new KeyPoint
                {
                    time = targetBeat,
                    value = (float)source.Value,
                    easeType = source.EaseType,
                    floorPosition = (float)source.FloorPosition,
                });
            }
        };

        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        RefreshCameraMove(editor, project);
        var mode = mirrorHorizontally ? "镜像并对齐鼠标" : "对齐鼠标";
        return $"已粘贴 {clipboard.Count} 个摄像机移动（{mode}），覆盖 {replacementIndices.Count} 个同拍旧事件。";
    }

    internal static CanvasPasteAnchor GetCanvasPasteAnchor(ProjectInformation project, string label = "画布事件")
    {
        var inputControl = project.inputControl;
        var camera = inputControl?._camera;
        var progress = project.progressControl;
        if (camera == null || progress == null)
            throw new InvalidOperationException("原编辑器尚未初始化鼠标坐标换算组件。" );

        var screenPosition = Input.mousePosition;
        var mouseWorld = camera.ScreenToWorldPoint(screenPosition);
        if (!float.IsFinite(mouseWorld.x) || !float.IsFinite(mouseWorld.y))
            throw new InvalidOperationException("原编辑器相机返回了无效的鼠标世界坐标。" );

        var horizontalSubdivision = ProjectInformation.VerticalSnapDensity;
        if (horizontalSubdivision <= 0)
            throw new InvalidOperationException("原编辑器返回了无效的水平细分值。");

        var splitCount = ProjectInformation.SnapLinesSplitCount;
        if (splitCount <= 0)
            throw new InvalidOperationException("原编辑器返回了无效的垂直细分值。");
        if (!float.IsFinite(project.speed) || Math.Abs(project.speed) < 1e-6f)
            throw new InvalidOperationException("当前编辑器垂直缩放速度无效。" );

        // Reproduce CanvasEditor.Update for the horizontal value. Unlike line points,
        // canvas-event values are absolute canvas positions, so no focused-canvas
        // transform is subtracted.
        var rawTargetValue = mouseWorld.x / CanvasEditorWorldUnitsPerChartUnit;
        var targetValue = (float)CanvasEventEditing.SnapToSubdivision(
            rawTargetValue,
            horizontalSubdivision);

        // Reproduce InputControl.GetMousePosition for the timeline coordinate without
        // reading its Nullable<Vector2> fields, whose generated interop wrappers lose
        // components at runtime.
        var targetRealTime = project.nowTime
            + (mouseWorld.y + EditorTimelineWorldYOffset) / project.speed;
        var beatDelta = progress.RealTimeRangeToChartTime(project.nowTime, targetRealTime);
        var rawTargetBeat = project.nowChartTime + beatDelta;
        var targetBeat = (float)Math.Max(
            0,
            CanvasEventEditing.SnapToSubdivision(rawTargetBeat, splitCount));

        if (!float.IsFinite(targetBeat) || !float.IsFinite(targetValue))
            throw new InvalidOperationException("原编辑器返回了无效的画布粘贴锚点。");

        Plugin.LogSource.LogInfo(
            $"{label}粘贴锚点：beat={targetBeat:0.######}，value={targetValue:0.######}，" +
            $"screen=({screenPosition.x:0.###}, {screenPosition.y:0.###})，" +
            $"world=({mouseWorld.x:0.######}, {mouseWorld.y:0.######})，" +
            $"rawBeat={rawTargetBeat:0.######}，rawCanvasValue={rawTargetValue:0.######}。" );
        return new CanvasPasteAnchor(targetBeat, targetValue);
    }

    private static ProjectInformation RequireProject(SpeedEditor editor)
    {
        var project = editor.projectInformation;
        if (project == null || project.chart == null || project.editorControl == null)
            throw new InvalidOperationException("当前没有已打开且可编辑的谱面。" );
        return project;
    }

    private static ProjectInformation RequireProject(CanvasEditor editor)
    {
        var project = editor.projectInformation;
        if (project == null || project.chart == null || project.editorControl == null)
            throw new InvalidOperationException("当前没有已打开且可编辑的谱面。" );
        return project;
    }

    private static ProjectInformation RequireProject(CameraEditor editor)
    {
        var project = editor.projectInformation;
        if (project == null || project.chart == null || project.editorControl == null)
            throw new InvalidOperationException("当前没有已打开且可编辑的谱面。");
        return project;
    }

    private static CanvasMove RequireCanvas(ProjectInformation project, int canvasIndex)
    {
        var canvases = project.chart.canvasMoves;
        if (canvases == null)
            throw new InvalidOperationException("当前谱面没有画布列表。" );
        for (var index = 0; index < canvases.Count; index++)
        {
            if (canvases[index].index == canvasIndex)
                return canvases[index];
        }
        throw new InvalidOperationException($"当前谱面中找不到画布 {canvasIndex}。" );
    }

    private static void ValidateEndpoints(CapturedEndpoint start, CapturedEndpoint end)
    {
        if (start.CanvasIndex != end.CanvasIndex)
            throw new InvalidOperationException("起点和终点必须属于同一画布。" );
        if (end.Beat <= start.Beat + BeatTolerance)
            throw new InvalidOperationException("终点流速节点必须晚于起点流速节点。" );
    }

    private static int FindUniquePointIndex(Il2CppSystem.Collections.Generic.List<KeyPoint> points, float beat, string label)
    {
        var found = -1;
        for (var index = 0; index < points.Count; index++)
        {
            if (Math.Abs(points[index].time - beat) > BeatTolerance)
                continue;
            if (found >= 0)
                throw new InvalidOperationException($"第 {beat:0.######} 拍存在多个与已记录{label}匹配的流速节点，无法确定唯一端点。" );
            found = index;
        }
        if (found < 0)
            throw new InvalidOperationException($"已记录的{label}流速节点（第 {beat:0.######} 拍）已经不存在，请重新记录。" );
        return found;
    }

    /// <summary>
    /// The list has to be ascending, but **same-beat duplicates are tolerated** (2026-09-23 bug
    /// report). Real charts contain them - an earlier version of the speed-easing generator could
    /// collapse two samples onto one beat - and the game resolves them by giving the later entry
    /// priority. Refusing outright used to make such a chart impossible to clean up: even Ctrl+D
    /// failed with "流速必须已按事件严格排列", so the duplicates could never be removed.
    /// Only genuinely descending data is still refused, because the range maths would be ambiguous
    /// and the plugin must not reorder the array.
    /// </summary>
    private static void ValidateOrdered(
        Il2CppSystem.Collections.Generic.List<KeyPoint> points,
        string label = "流速")
    {
        var beats = BeatsOf(points);
        if (SpeedEditing.IsAscending(beats, BeatTolerance))
            return;

        for (var index = 1; index < beats.Length; index++)
        {
            if (beats[index] < beats[index - 1] - BeatTolerance)
            {
                throw new InvalidOperationException(
                    $"{label}节点必须按时间升序：第 {beats[index]:0.######} 拍排在第 {beats[index - 1]:0.######} 拍之后但时间更早。插件不会擅自重排节点。");
            }
        }
    }

    /// <summary>Beats of a key-point list, for the range helpers and duplicate reporting.</summary>
    private static double[] BeatsOf(Il2CppSystem.Collections.Generic.List<KeyPoint> points)
    {
        var beats = new double[points.Count];
        for (var index = 0; index < points.Count; index++)
            beats[index] = points[index].time;
        return beats;
    }

    /// <summary>
    /// Note appended to status lines when the list already contains same-beat duplicates, so the
    /// user knows why the data looks odd and that those nodes can now be selected and deleted.
    /// </summary>
    private static string DescribeSameBeatRepeats(
        Il2CppSystem.Collections.Generic.List<KeyPoint> points,
        string label = "流速")
    {
        var beats = BeatsOf(points);
        var repeats = SpeedEditing.FindSameBeatRepeats(beats, BeatTolerance);
        if (repeats.Count == 0)
            return string.Empty;

        var first = string.Join("、", repeats.Take(4).Select(index => beats[index].ToString("0.###")));
        var more = repeats.Count > 4 ? "…" : string.Empty;
        Plugin.LogSource.LogWarning(
            $"{label}：当前列表有 {repeats.Count} 个与前一个节点同拍的重复节点（第 {first}{more} 拍）。" +
            "已按「后一项优先」处理，可框选删除以彻底清理。");
        return $"；注意：当前列表有 {repeats.Count} 个同拍重复节点（第 {first}{more} 拍），可框选删除";
    }

    private static void Refresh(SpeedEditor editor, ProjectInformation project)
    {
        if (project.canvasControl != null)
        {
            project.canvasControl.GetInformations();
            project.canvasControl.SetPosition();
        }
        editor.ReloadTags();
        editor.MoveTags();
        editor.UpdateCanvasMovePreview();
    }

    private static CameraMove RequireCameraMove(ProjectInformation project)
    {
        var cameraMove = project.chart.cameraMove;
        if (cameraMove?.xPositionKeyPoints == null || cameraMove.scaleKeyPoints == null)
            throw new InvalidOperationException("当前谱面没有摄像机移动列表。");
        return cameraMove;
    }

    internal enum CameraListKind
    {
        Move,
        Scale,
        Unknown,
    }

    /// <summary>
    /// Which camera list the user is editing (horizontal move or scale). Reused by the extra-easing
    /// bake, which has to write into the same list the camera tool is showing.
    /// </summary>
    internal static CameraListKind IdentifyCameraList(CameraEditor editor, CameraMove cameraMove)
    {
        var shown = editor.cameraKeyPoints;
        var movePoints = cameraMove.xPositionKeyPoints;
        var scalePoints = cameraMove.scaleKeyPoints;
        if (shown == null)
            return CameraListKind.Unknown;
        if (SameInstances(shown, movePoints))
            return CameraListKind.Move;
        if (SameInstances(shown, scalePoints))
            return CameraListKind.Scale;
        if (SameContent(shown, movePoints) && !SameContent(shown, scalePoints))
            return CameraListKind.Move;
        if (SameContent(shown, scalePoints) && !SameContent(shown, movePoints))
            return CameraListKind.Scale;
        if (shown.Count != 0)
            return CameraListKind.Unknown;

        var moveCount = movePoints.Count;
        var scaleCount = scalePoints.Count;
        if (scaleCount > 0 && moveCount == 0)
            return CameraListKind.Scale;
        if (moveCount > 0 && scaleCount == 0)
            return CameraListKind.Move;
        if (moveCount == 0 && scaleCount == 0)
            return CameraListKind.Move;
        return CameraListKind.Unknown;
    }

    private static bool SameInstances(
        Il2CppSystem.Collections.Generic.List<CanvasKeyPointInformation> shown,
        Il2CppSystem.Collections.Generic.List<KeyPoint> chart)
    {
        if (shown.Count == 0 || shown.Count != chart.Count)
            return false;
        for (var index = 0; index < shown.Count; index++)
        {
            var wrapper = shown[index];
            if (wrapper?.keyData == null || wrapper.keyData != chart[index])
                return false;
        }

        return true;
    }

    private static bool SameContent(
        Il2CppSystem.Collections.Generic.List<CanvasKeyPointInformation> shown,
        Il2CppSystem.Collections.Generic.List<KeyPoint> chart)
    {
        if (shown.Count == 0 || shown.Count != chart.Count)
            return false;
        for (var index = 0; index < shown.Count; index++)
        {
            var data = shown[index]?.keyData;
            var point = chart[index];
            if (data == null || point == null)
                return false;
            if (Math.Abs(data.time - point.time) > BeatTolerance
                || Math.Abs(data.value - point.value) > BeatTolerance
                || data.easeType != point.easeType)
                return false;
        }

        return true;
    }

    internal static void RefreshCameraMove(CameraEditor editor, ProjectInformation project)
    {
        var camera = project.cameraControl;
        if (camera != null)
        {
            camera.FleshInfo();
            camera.SetXPosition();
        }

        var count = project.chart.cameraMove.xPositionKeyPoints.Count;
        if (editor._focusCameraKeyPointIndex >= count)
            editor._focusCameraKeyPointIndex = count > 0 ? count - 1 : -1;
        editor.SetCameraKeyPoint();
    }

    internal static void Refresh(CanvasEditor editor, ProjectInformation project)
    {
        if (project.canvasControl != null)
        {
            project.canvasControl.GetInformations();
            project.canvasControl.SetPosition();
        }
        editor.SetCanvasKeyPoint();
    }

    public static string ImportChartColorsToPalette(Chart? chart)
    {
        if (chart == null)
            throw new InvalidOperationException("当前没有已打开的谱面。");

        var project = ProjectInformation.instance;
        if (project == null)
            throw new InvalidOperationException("当前无法访问编辑器项目。");

        var panels = ProjectInformation.colorPanels;
        if (panels == null || panels.Length == 0)
            throw new InvalidOperationException("调色板尚未初始化。请先点开画线工具里的颜色选择器，再重试。");

        var uniqueColors = ChartColorCollection.UniqueInOrder(CollectChartColors(chart));
        if (uniqueColors.Count == 0)
            return "当前谱面没有可导入的颜色。";

        var existing = new string[panels.Length];
        for (var index = 0; index < panels.Length; index++)
            existing[index] = ToRgba(panels[index]).ToHex();

        var merged = ChartColorCollection.MergeIntoSlots(existing, uniqueColors);
        for (var index = 0; index < panels.Length; index++)
        {
            var hex = string.IsNullOrEmpty(merged.Slots[index]) ? "FFFFFFFF" : merged.Slots[index];
            if (!Rgba32.TryParseHex(hex, out var rgba))
                continue;
            panels[index] = new UnityEngine.Color(
                rgba.R / 255f,
                rgba.G / 255f,
                rgba.B / 255f,
                rgba.A / 255f);
            PlayerPrefs.SetString(PalettePrefKey(index), hex);
        }

        ProjectInformation.colorPanels = panels;
        PlayerPrefs.Save();
        try
        {
            ProjectInformation.OnColorPanelsChanged?.Invoke();
        }
        catch (Exception exception)
        {
            Plugin.LogSource.LogWarning($"OnColorPanelsChanged 调用失败：{exception.Message}");
        }

        PaintPaletteImages();
        return $"已写入调色板：新增 {merged.Added}，跳过重复 {merged.Duplicate}，槽位不足 {merged.Overflow}（谱面去重后 {uniqueColors.Count} 色）。";
    }

    private static IEnumerable<Rgba32> CollectChartColors(Chart chart)
    {
        if (chart.themes != null)
        {
            for (var themeIndex = 0; themeIndex < chart.themes.Count; themeIndex++)
            {
                var colors = chart.themes[themeIndex]?.colorsList;
                if (colors == null)
                    continue;
                for (var index = 0; index < colors.Count; index++)
                    yield return ToRgba(colors[index]);
            }
        }

        if (chart.lines == null)
            yield break;

        for (var lineIndex = 0; lineIndex < chart.lines.Count; lineIndex++)
        {
            var line = chart.lines[lineIndex];
            if (line == null)
                continue;

            if (line.linePoints != null)
            {
                for (var index = 0; index < line.linePoints.Count; index++)
                    yield return ToRgba(line.linePoints[index].color);
            }

            if (line.lineColor != null)
            {
                for (var index = 0; index < line.lineColor.Count; index++)
                {
                    var point = line.lineColor[index];
                    yield return ToRgba(point.startColor);
                    yield return ToRgba(point.endColor);
                }
            }

            if (line.judgeRingColor != null)
            {
                for (var index = 0; index < line.judgeRingColor.Count; index++)
                {
                    var point = line.judgeRingColor[index];
                    yield return ToRgba(point.startColor);
                    yield return ToRgba(point.endColor);
                }
            }
        }
    }

    private static Rgba32 ToRgba(UnityEngine.Color color) =>
        Rgba32.FromChartChannels(color.r, color.g, color.b, color.a);

    private static void PaintPaletteImages()
    {
        try
        {
            if (ProjectInformation.instance == null)
                return;
        }
        catch
        {
            return;
        }

        var panels = ProjectInformation.colorPanels;
        if (panels == null)
            return;

        ColorSelectorControl[] selectors;
        try
        {
            selectors = Resources.FindObjectsOfTypeAll<ColorSelectorControl>();
        }
        catch (Exception exception)
        {
            Plugin.LogSource.LogWarning($"查找调色板控件失败：{exception.Message}");
            return;
        }

        foreach (var selector in selectors)
        {
            if (selector == null)
                continue;

            var presets = selector.colorPresets;
            if (presets == null)
                continue;
            for (var index = 0; index < presets.Length && index < panels.Length; index++)
            {
                if (presets[index] == null)
                    continue;
                presets[index].color = panels[index];
            }
        }
    }

    private static readonly Dictionary<int, PreviewLineWidth> PreviewLineWidths = new();
    private const float PreviewLineWidthScale = 0.75f;

    public static void UpdatePreviewLineWidth(bool enabled)
    {
        ProjectInformation? project = null;
        try
        {
            project = ProjectInformation.instance;
        }
        catch
        {
            // ProjectInformation is unavailable before the editor scene finishes loading.
        }

        var previewing = enabled
            && project != null
            && ProjectInformation.isPlaying
            && !ProjectInformation.isPlayingWithoutPreview;
        if (!previewing)
        {
            RestorePreviewLineWidths();
            return;
        }

        var groups = project!.linesGroups;
        if (groups == null)
            return;

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var lines = groups[groupIndex]?.lines;
            if (lines == null)
                continue;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var renderer = lines[lineIndex]?.lineRenderer;
                if (renderer == null)
                    continue;
                try
                {
                    var id = renderer.GetInstanceID();
                    if (!PreviewLineWidths.TryGetValue(id, out var state))
                    {
                        state = new PreviewLineWidth(renderer, renderer.widthMultiplier);
                        PreviewLineWidths[id] = state;
                    }

                    renderer.widthMultiplier = state.WidthMultiplier * PreviewLineWidthScale;
                }
                catch (Exception)
                {
                    // One destroyed renderer must not stop the other lines from updating.
                }
            }
        }
    }

    private static void RestorePreviewLineWidths()
    {
        if (PreviewLineWidths.Count == 0)
            return;

        foreach (var state in PreviewLineWidths.Values)
        {
            try
            {
                if (state.Renderer != null)
                    state.Renderer.widthMultiplier = state.WidthMultiplier;
            }
            catch (Exception)
            {
                // Keep clearing the saved widths even if one renderer is already gone.
            }
        }

        PreviewLineWidths.Clear();
    }

    private static string PalettePrefKey(int index) => $"colorPanels{index}";

    private sealed class PreviewLineWidth
    {
        public PreviewLineWidth(LineRenderer renderer, float widthMultiplier)
        {
            Renderer = renderer;
            WidthMultiplier = widthMultiplier;
        }

        public LineRenderer Renderer { get; }
        public float WidthMultiplier { get; }
    }
}
