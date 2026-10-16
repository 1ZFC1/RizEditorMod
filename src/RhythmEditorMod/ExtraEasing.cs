using HarmonyLib;
using RhythmEditorMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// Adds the plugin's extra easing curves to the editor's ease dropdowns and bakes the chosen curve
/// into ordinary Linear key points.
///
/// The chart stores an ease index that the game itself interprets, and the editor's own table is a
/// fixed 19 x 101 one that throws for any index above 18 (docs/features/EXTRA_EASING.md), so an
/// extra curve cannot become a new index: selecting an appended option does not store an ease at
/// all. The plugin expands the curve into many <c>easeType = 0</c> nodes across the segment, which
/// the game plays back unchanged. See D-023.
///
/// Supported tools, each with its own dropdown and target list:
/// <list type="bullet">
/// <item><description><b>canvas</b> - <c>CanvasEditor.easeType</c>; writes the selected canvas's
/// <c>xPositionKeyPoints</c>; intercepted on <c>CanvasEditor.OnEaseTypeChange</c>.</description></item>
/// <item><description><b>camera</b> - <c>CameraEditor.easeType</c> and <c>CameraEditor.selectEase</c>;
/// writes <c>cameraMove.xPositionKeyPoints</c> only. The scale list is deliberately **not**
/// supported (the user removed it after an <c>IndexOutOfRangeException</c>): the horizontal move is
/// what the elastic curves are for, and scale stays exactly as the original editor left it.
/// Its selection is intercepted at <c>Dropdown.set_value</c> (see <see cref="EaseDropdownValuePatch"/>),
/// because the camera's dropdown wiring is not the canvas one.</description></item>
/// </list>
///
/// The line tool is **not** supported, by the user's decision on 2026-09-23: an implementation
/// existed (it baked the selected line point's segment and polled/ intercepted the dropdown), but
/// the line tool is driven by dragging to create lines and Ctrl+dragging to change node properties,
/// so a dropdown-driven bake felt disconnected from how the original works. It was removed again and
/// the line tool behaves exactly like the original.
/// </summary>
internal static class ExtraEasing
{
    internal readonly record struct Preset(string Label, Func<double, double> Curve);

    /// <summary>
    /// Appended right after the 19 original options; matched by label and by being past the native
    /// count, never by a hardcoded index of our own. The names follow the editor's own style
    /// (<c>In Quad</c>, <c>Out Circle</c>, ...), so they sit at the end of the list.
    /// </summary>
    internal static readonly Preset[] Presets =
    {
        new("In Elastic", EaseFit.InElastic),
        new("Out Elastic", EaseFit.OutElastic),
        new("InOut Elastic", EaseFit.InOutElastic),
    };

    /// <summary>
    /// Appends the extra options. Called from the editors' own <c>Start</c>/<c>OnEnable</c> patches,
    /// so there is no periodic scanning: the work happens exactly when an editor (re)appears, which
    /// is also when a scene reload recreates its dropdown.
    /// </summary>
    internal static void InstallFor(CanvasEditor editor)
    {
        try
        {
            CanvasEase.Dropdown = editor?.easeType;
            TryInstall(CanvasEase);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：安装画布下拉选项失败：{error}");
        }
    }

    /// <summary>
    /// Same for the camera. Both of its ease dropdowns share one option list, so both are extended
    /// with the same three curves; nothing is withdrawn per list.
    /// </summary>
    internal static void InstallFor(CameraEditor editor)
    {
        try
        {
            CameraEase.Dropdown = editor?.easeType;
            CameraSelect.Dropdown = editor?.selectEase;
            TryInstall(CameraEase);
            TryInstall(CameraSelect);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：安装摄像机下拉选项失败：{error}");
        }
    }

    /// <summary>One dropdown the plugin appends to, with the first index it owns in that list.</summary>
    private sealed class DropdownSlot
    {
        internal DropdownSlot(string name)
        {
            Name = name;
        }

        internal string Name { get; }

        internal Dropdown? Dropdown { get; set; }

        internal int FirstExtraIndex { get; set; } = int.MaxValue;
    }

    private static readonly DropdownSlot CanvasEase = new("画布·缓动");
    private static readonly DropdownSlot CameraEase = new("摄像机·缓动");
    private static readonly DropdownSlot CameraSelect = new("摄像机·选择");

    private static void TryInstall(DropdownSlot slot)
    {
        var dropdown = slot.Dropdown;
        if (dropdown?.options == null)
            return;

        // Wait for the scene's real ease list before touching it.
        if (dropdown.options.Count < 19)
            return;

        var added = 0;
        var nativeCount = dropdown.options.Count;
        foreach (var preset in Presets)
        {
            if (HasOption(dropdown, preset.Label))
                continue;
            dropdown.options.Add(new Dropdown.OptionData(preset.Label));
            added++;
        }

        if (added == 0)
            return;

        slot.FirstExtraIndex = Math.Min(slot.FirstExtraIndex, nativeCount);
        dropdown.RefreshShownValue();
        Plugin.LogSource.LogInfo(
            $"额外缓动：{slot.Name} 下拉框现有 {dropdown.options.Count} 项（原生 {nativeCount} 项，本次新增 {added} 项）：" +
            $"{string.Join(" | ", Presets.Select(p => p.Label))}");
    }

    private static bool HasOption(Dropdown dropdown, string label)
    {
        var options = dropdown.options;
        if (options == null)
            return false;
        for (var index = 0; index < options.Count; index++)
        {
            if (options[index].text == label)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True only for an option the plugin appended: the label must match and the index must be at
    /// or past the native count, so a future build that ships its own "In Elastic" would keep it.
    /// </summary>
    private static bool IsExtra(DropdownSlot slot, string? label, int optionIndex)
    {
        if (string.IsNullOrEmpty(label) || optionIndex < slot.FirstExtraIndex)
            return false;
        foreach (var preset in Presets)
        {
            if (preset.Label == label)
                return true;
        }

        return false;
    }

    /// <summary>Label of the canvas ease dropdown when it currently holds one of the plugin's curves.</summary>
    internal static string? SelectedCanvasExtraLabel(Dropdown? dropdown)
    {
        return SelectedExtraLabel(CanvasEase, dropdown);
    }

    /// <summary>
    /// Same for the camera. The camera tool has two ease dropdowns, so both are checked; only the
    /// one that actually shows a plugin curve counts.
    /// </summary>
    internal static string? SelectedCameraExtraLabel(CameraEditor? editor)
    {
        if (editor == null)
            return null;
        return SelectedExtraLabel(CameraEase, editor.easeType)
            ?? SelectedExtraLabel(CameraSelect, editor.selectEase);
    }

    /// <summary>
    /// Label at a given option index of either camera ease dropdown, when that option is one of the
    /// plugin's curves. <c>SelectEaseType(int)</c> may be called with the option index of the
    /// dropdown that changed rather than with the dropdown's current value, so both are checked.
    /// </summary>
    internal static string? ExtraLabelAt(CameraEditor? editor, int optionIndex)
    {
        if (editor == null)
            return null;
        return LabelAt(CameraEase, editor.easeType, optionIndex)
            ?? LabelAt(CameraSelect, editor.selectEase, optionIndex);
    }

    private static string? LabelAt(DropdownSlot slot, Dropdown? dropdown, int optionIndex)
    {
        try
        {
            var options = dropdown?.options;
            if (dropdown == null || options == null || optionIndex < 0 || optionIndex >= options.Count)
                return null;
            var label = options[optionIndex].text;
            return IsExtra(slot, label, optionIndex) ? label : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// True for the camera dropdowns, whose selection the plugin intercepts at
    /// <c>Dropdown.set_value</c> instead of through a tool callback (the camera's wiring is not the
    /// canvas one). Identity is compared by reference, so other dropdowns are never affected.
    /// </summary>
    internal static bool IsInterceptedDropdown(Dropdown dropdown)
    {
        if (dropdown == null)
            return false;
        return ReferenceEquals(dropdown, CameraEase.Dropdown)
            || ReferenceEquals(dropdown, CameraSelect.Dropdown);
    }

    /// <summary>Label of the plugin's curve at <paramref name="optionIndex"/> on an intercepted dropdown.</summary>
    internal static string? InterceptedLabelAt(Dropdown dropdown, int optionIndex)
    {
        if (ReferenceEquals(dropdown, CameraEase.Dropdown))
            return LabelAt(CameraEase, dropdown, optionIndex);
        if (ReferenceEquals(dropdown, CameraSelect.Dropdown))
            return LabelAt(CameraSelect, dropdown, optionIndex);
        return null;
    }

    /// <summary>Runs the bake for whichever intercepted dropdown the selection came from.</summary>
    internal static string ApplyForInterceptedDropdown(Dropdown dropdown, string label)
    {
        if (!IsInterceptedDropdown(dropdown))
            return "该下拉框不受插件管理，未修改谱面。";

        var camera = FindFirst<CameraEditor>();
        return camera == null ? "找不到「编辑摄像机」工具，未修改谱面。" : Apply(camera, label);
    }

    private static string? SelectedExtraLabel(DropdownSlot slot, Dropdown? dropdown)
    {
        try
        {
            var options = dropdown?.options;
            if (dropdown == null || options == null || dropdown.value < 0 || dropdown.value >= options.Count)
                return null;
            var label = options[dropdown.value].text;
            return IsExtra(slot, label, dropdown.value) ? label : null;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"额外缓动：读取 {slot.Name} 下拉框失败：{error.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// Bakes the selected curve into the canvas event segment the playhead is in. The chart is only
    /// touched when everything needed is present, so a missing chart never writes anything.
    /// </summary>
    internal static string Apply(CanvasEditor editor, string label)
    {
        try
        {
            if (!TryGetPreset(label, out var preset))
                return $"未知的额外缓动：{label}";

            var project = editor.projectInformation ?? ProjectInformation.instance;
            if (project?.chart == null || project.editorControl == null)
                return "当前没有已打开且可编辑的谱面。";

            var canvasIndex = ProjectInformation.SelectedCanvas;
            var points = FindCanvasPoints(project, canvasIndex);
            if (points == null)
                return $"当前谱面中找不到画布 {canvasIndex} 的横向事件列表。";

            return BakeKeyPoints(
                points,
                project.nowChartTime,
                preset,
                label,
                $"画布 {canvasIndex} 的横向事件",
                project,
                () => EditorOperations.Refresh(editor, project));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：画布拟合失败：{error}");
            return $"拟合失败：{error.Message}";
        }
    }

    /// <summary>
    /// Camera support: horizontal movement only. The scale list is intentionally left alone - the
    /// user asked for it to stay exactly as the original editor behaves after scaling caused an
    /// <c>IndexOutOfRangeException</c>.
    /// </summary>
    internal static string Apply(CameraEditor editor, string label)
    {
        try
        {
            if (!TryGetPreset(label, out var preset))
                return $"未知的额外缓动：{label}";

            var project = editor.projectInformation ?? ProjectInformation.instance;
            if (project?.chart == null || project.editorControl == null)
                return "当前没有已打开且可编辑的谱面。";

            var cameraMove = project.chart.cameraMove;
            if (cameraMove?.xPositionKeyPoints == null || cameraMove.scaleKeyPoints == null)
                return "当前谱面没有摄像机移动列表。";

            if (EditorOperations.IdentifyCameraList(editor, cameraMove) == EditorOperations.CameraListKind.Scale)
                return "摄像机缩放不支持弹性拟合（已按你的要求移除）；请切到「横向移动」再选。";

            var points = cameraMove.xPositionKeyPoints;
            return BakeKeyPoints(
                points,
                project.nowChartTime,
                preset,
                label,
                "摄像机横向移动",
                project,
                () => EditorOperations.RefreshCameraMove(editor, project));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：摄像机拟合失败：{error}");
            return $"拟合失败：{error.Message}";
        }
    }

    private static bool TryGetPreset(string label, out Preset preset)
    {
        preset = Presets.FirstOrDefault(p => p.Label == label);
        return preset.Label == label;
    }

    /// <summary>
    /// The shared bake for canvas and camera key points: locate the segment around the current beat,
    /// sample the curve adaptively, replace the interior nodes inside one <c>Stepping</c> (a single
    /// undo step) and refresh the tool, so their behaviour stays identical.
    /// </summary>
    private static string BakeKeyPoints(
        Il2CppSystem.Collections.Generic.List<KeyPoint> points,
        double currentBeat,
        Preset preset,
        string label,
        string subject,
        ProjectInformation project,
        Action refresh)
    {
        var startIndex = FindSegmentIndex(points, currentBeat, subject, out var note);
        if (startIndex < 0)
            return note;

        var startPoint = points[startIndex];
        var endPoint = points[startIndex + 1];
        var startBeat = startPoint.time;
        var endBeat = endPoint.time;

        var samples = EaseFit.SampleCurve(
            startBeat,
            startPoint.value,
            endBeat,
            endPoint.value,
            preset.Curve,
            EaseFit.DefaultTolerance,
            out var sampleNote);

        if (samples.Count == 0)
            return sampleNote.Length > 0 ? sampleNote : "没有生成任何节点。";

        var startFloor = startPoint.floorPosition;
        var endFloor = endPoint.floorPosition;

        System.Action mutation = () =>
        {
            RemoveInterior(points, startBeat, endBeat);
            points[startIndex].easeType = 0;

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                var insertAt = startIndex + 1 + index;
                var u = (sample.Beat - startBeat) / (endBeat - startBeat);
                points.Insert(insertAt, new KeyPoint
                {
                    time = (float)sample.Beat,
                    value = (float)sample.Value,
                    easeType = 0,
                    floorPosition = (float)(startFloor + (endFloor - startFloor) * u),
                });
            }

            project.editorControl.SetFloorPosition();
        };

        project.editorControl.Stepping((Il2CppSystem.Action)mutation);
        refresh();

        var status =
            $"已按「{label}」拟合{subject}的第 {startBeat:0.###}–{endBeat:0.###} 拍（当前拍 {currentBeat:0.###}）：" +
            $"插入 {samples.Count} 个 Linear 节点（曲线最大偏离端点 {EaseFit.MaxExcursion(preset.Curve) * 100:0.#}% × 区间变化量，按曲线弯曲程度自适应采样）。";
        if (sampleNote.Length > 0)
            status += $" 注意：{sampleNote}";
        return status;
    }

    /// <summary>
    /// The segment being edited is the one the playhead sits in, which is how the original editor
    /// picks it: the last key point whose beat is not after the current beat, up to the next one.
    /// There is no "focused key point" to read - <c>_focusCanvasKeyPointIndex</c> stays -1 during
    /// normal editing (confirmed in the 2026-09-23 run: the game's own <c>OnEaseTypeChange</c>
    /// fires with that index at -1 and leaves the chart alone).
    /// </summary>
    private static int FindSegmentIndex(
        Il2CppSystem.Collections.Generic.List<KeyPoint> points,
        double currentBeat,
        string subject,
        out string note)
    {
        note = string.Empty;
        if (points.Count < 2)
        {
            note = $"{subject}少于 2 个关键点，没有可拟合的区间。";
            return -1;
        }

        const double tolerance = 1e-4;
        var index = -1;
        for (var candidate = 0; candidate < points.Count; candidate++)
        {
            if (points[candidate].time <= currentBeat + tolerance)
                index = candidate;
            else
                break;
        }

        if (index < 0)
        {
            note = $"当前拍 {currentBeat:0.###} 在{subject}的第一个关键点（第 {points[0].time:0.###} 拍）之前，没有可拟合的区间。";
            return -1;
        }

        if (index + 1 >= points.Count)
        {
            note = $"当前拍 {currentBeat:0.###} 在{subject}的最后一个关键点（第 {points[index].time:0.###} 拍）之后，它后面没有可拟合的一段。";
            return -1;
        }

        return index;
    }

    /// <summary>Drops the interior nodes of the segment so a second fit replaces the first.</summary>
    private static void RemoveInterior(
        Il2CppSystem.Collections.Generic.List<KeyPoint> points,
        double startBeat,
        double endBeat)
    {
        for (var index = points.Count - 1; index >= 0; index--)
        {
            var beat = points[index].time;
            if (beat > startBeat + 1e-5 && beat < endBeat - 1e-5)
                points.RemoveAt(index);
        }
    }

    private static Il2CppSystem.Collections.Generic.List<KeyPoint>? FindCanvasPoints(
        ProjectInformation project,
        int canvasIndex)
    {
        var canvases = project.chart?.canvasMoves;
        if (canvases == null)
            return null;
        for (var index = 0; index < canvases.Count; index++)
        {
            if (canvases[index].index == canvasIndex)
                return canvases[index].xPositionKeyPoints;
        }

        return null;
    }

    private static T? FindFirst<T>() where T : Component
    {
        var found = UnityEngine.Object.FindObjectsOfType<T>(true);
        return found == null || found.Length == 0 ? null : found[0];
    }
}

/// <summary>
/// Intercepts the canvas ease dropdown: values that belong to the plugin's extra curves are baked
/// here and the original handler is skipped; the original 19 options are untouched.
/// </summary>
[HarmonyPatch(typeof(CanvasEditor), nameof(CanvasEditor.OnEaseTypeChange))]
internal static class CanvasEaseTypeChangePatch
{
    private static bool Prefix(CanvasEditor __instance)
    {
        try
        {
            var label = ExtraEasing.SelectedCanvasExtraLabel(__instance.easeType);
            if (label == null)
                return true;

            var status = ExtraEasing.Apply(__instance, label);
            SpeedEasePanel.Instance?.ShowStatus(status);
            return false;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：拦截画布缓动切换失败：{error}");
            return true;
        }
    }
}

/// <summary>
/// The same interception for the camera tool. Its ease can come from either of its two dropdowns
/// (<c>easeType</c> / <c>selectEase</c>), so both are checked; the bake only writes the horizontal
/// move list.
/// </summary>
[HarmonyPatch(typeof(CameraEditor), nameof(CameraEditor.OnEaseTypeChange))]
internal static class CameraEaseTypeChangePatch
{
    private static bool Prefix(CameraEditor __instance)
    {
        try
        {
            var label = ExtraEasing.SelectedCameraExtraLabel(__instance);
            if (label == null)
                return true;

            var status = ExtraEasing.Apply(__instance, label);
            SpeedEasePanel.Instance?.ShowStatus(status);
            return false;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：拦截摄像机缓动切换失败：{error}");
            return true;
        }
    }
}

/// <summary>
/// Second camera entry point. The canvas tool routes an ease change through
/// <c>OnEaseTypeChange</c>, but the camera also exposes <c>SelectEaseType(int)</c> (wired with a
/// dropdown closure), so a selection can arrive here instead. Without this the original code would
/// store the appended index (19+) in a key point and the ease table lookup would read out of bounds
/// - the <c>IndexOutOfRangeException</c> seen on 2026-09-23.
/// </summary>
[HarmonyPatch(typeof(CameraEditor), nameof(CameraEditor.SelectEaseType))]
internal static class CameraSelectEaseTypePatch
{
    private static bool Prefix(CameraEditor __instance, int easeType)
    {
        try
        {
            // Either the selected option index or an ease value: both are just an index into the
            // same dropdown, so the plugin's own options are recognisable either way.
            var label = ExtraEasing.SelectedCameraExtraLabel(__instance)
                ?? ExtraEasing.ExtraLabelAt(__instance, easeType);
            if (label == null)
                return true;

            var status = ExtraEasing.Apply(__instance, label);
            SpeedEasePanel.Instance?.ShowStatus(status);
            return false;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：拦截摄像机 SelectEaseType 失败：{error}");
            return true;
        }
    }
}
