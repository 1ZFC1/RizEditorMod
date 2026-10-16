using HarmonyLib;
using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

public sealed class SpeedEasePanel : MonoBehaviour
{
    // Names match the original ease bar / confirmed easeType mapping in docs/CHART_FORMAT.md.
    private static readonly string[] EasingNames =
    {
        "Linear", "In Quad", "Out Quad", "InOut Quad",
        "In Cubic", "Out Cubic", "InOut Cubic",
        "In Quart", "Out Quart", "InOut Quart",
        "In Quint", "Out Quint", "InOut Quint",
        "Zero", "One", "In Circle", "Out Circle", "Sin", "Cos",
    };

    private CapturedEndpoint? _start;
    private CapturedEndpoint? _end;
    private int _easingType;
    private bool _visible = true;
    private bool _easeDropdownOpen;
    private string _speedStatus = "请把播放头移到已有流速节点的拍数，再依次记录起点和终点。";
    private string _canvasStatus = "画布事件剪贴板已就绪。复制或剪切时请在按下快捷键后拖动框选。";
    private string _colorStatus = "读取当前谱面中的主题色、线基础色、线颜色事件和判定环颜色，写入画线工具的调色板空槽。";
    private string _eventStatus = "框选复制后，可打开导出开关把内容写入系统剪切板。";
    private string _publishStatus = "打开谱面后，这里会显示画布、线和音符统计。";
    private string _customStatus = "插件工具栏入口的占位状态。";
    private bool _thinPreviewLines;
    private bool _exportToSystemClipboard;
    private string _clipboardMessage = "导出关闭。打开后，复制和剪切在框选结束时写入系统剪切板。";
    private float _panelWidthScale = 1f;
    private float _panelAlpha = 0.92f;
    private int _draggingSlider; // 0 none, 1 width, 2 alpha
    private PendingOperation? _pending;
    private Rect _panelRect;
    private Rect _captureStartRect;
    private Rect _captureEndRect;
    private Rect _easeDropdownRect;
    private Rect _generateRect;
    private Rect _clearRect;
    private Rect _continueRect;
    private Rect _cancelRect;
    private Rect _importColorsRect;
    private Rect _thinLineRect;
    private Rect _previewMaskRect;
    private Rect _breakZoomRect;
    private Rect _panelWidthSliderRect;
    private Rect _panelAlphaSliderRect;
    private Rect _exportClipboardRect;
    private Rect _importClipboardRect;
    private Rect _clipboardHintRect;
    private Rect[] _projectSlotRects = Array.Empty<Rect>();
    private Rect _projectPickRect;
    private Rect[] _easeOptionRects = Array.Empty<Rect>();

    internal static SpeedEasePanel? Instance { get; private set; }
    internal static SpeedEditor? ActiveSpeedEditor { get; set; }
    internal static CanvasEditor? ActiveCanvasEditor { get; set; }
    internal static LineEditor? ActiveLineEditor { get; set; }
    internal static ColorEditor? ActiveColorEditor { get; set; }
    internal static CameraEditor? ActiveCameraEditor { get; set; }
    internal static NoteEditor? ActiveNoteEditor { get; set; }
    internal static ThemeEditor? ActiveThemeEditor { get; set; }
    internal static ToolbarToolButton? ActiveToolButton { get; set; }
    internal static PublishControl? ActivePublishControl { get; set; }
    internal static bool SpeedEditorIsActive
    {
        get
        {
            try
            {
                return ActiveSpeedEditor != null && ActiveSpeedEditor.isActiveAndEnabled;
            }
            catch
            {
                return false;
            }
        }
    }
    internal static bool CanvasEditorIsActive
    {
        get
        {
            try
            {
                return ActiveCanvasEditor != null && ActiveCanvasEditor.isActiveAndEnabled;
            }
            catch
            {
                return false;
            }
        }
    }
    internal static bool LineEditorIsActive
    {
        get
        {
            try
            {
                return ActiveLineEditor != null && ActiveLineEditor.isActiveAndEnabled;
            }
            catch
            {
                return false;
            }
        }
    }
    internal static bool ColorEditorIsActive => IsEditorActive(ActiveColorEditor);
    internal static bool CameraEditorIsActive => IsEditorActive(ActiveCameraEditor);
    internal static bool NoteEditorIsActive => IsEditorActive(ActiveNoteEditor);
    internal static bool ThemeEditorIsActive => IsEditorActive(ActiveThemeEditor);
    internal static bool PublishControlIsActive => IsPublishToolActive();
    internal static bool ClipboardEditorIsActive =>
        SpeedEditorIsActive || CanvasEditorIsActive || ColorEditorIsActive || CameraEditorIsActive;
    internal static bool ExportToSystemClipboard => Instance != null && Instance._exportToSystemClipboard;

    private static bool IsEditorActive(Behaviour? behaviour)
    {
        try
        {
            return behaviour != null && behaviour.isActiveAndEnabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Publish and cursor are the only two toolbar buttons that own no toolObject, so this
    /// check cannot key on toolObject alone. The runtime toolbar dump shows the only
    /// structural difference between them: Publish is "Icon, Control, Title, Body(5)" while
    /// cursor is "Icon, Title" - the Body child is what separates them. Everything is still
    /// resolved inside the selected button's own subtree, so a resident PublishControl
    /// elsewhere in the scene can never cause a false positive.
    /// </summary>
    private static bool IsPublishToolActive()
    {
        try
        {
            var button = ActiveToolButton;
            if (button == null || button.toolObject != null)
                return false;

            // The plugin's own entry carries a Body child as well (so the toolbar manages its
            // visibility like any native tool), which would otherwise match the publish shape.
            if (button == CustomToolbarTool.Button)
                return false;

            return HasBodyChild(button) || FindPublishControl(button) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A tool button shows its body panel only while it is selected, so the presence of a
    /// direct "Body" child is a stable per-button trait. Confirmed for all 8 buttons.
    /// </summary>
    private static bool HasBodyChild(ToolbarToolButton button)
    {
        return button.transform.Find("Body") != null;
    }

    /// <summary>
    /// Extra confirmation that a button really hosts the publish form. Never searches the whole
    /// scene, and never throws into the panel routing. The walk avoids the generic
    /// GetComponentInChildren&lt;T&gt;(bool), which throws VerificationException in this interop build
    /// (evidence: 2026-09-23 log, D-022) and made this confirmation silently useless before.
    /// </summary>
    private static PublishControl? FindPublishControl(ToolbarToolButton button)
    {
        return CustomToolbarTool.FindComponent<PublishControl>(button.transform);
    }

    /// <summary>
    /// The cursor tool is the only toolbar entry that selects without a toolObject at all
    /// and owns no body panel, so it has nothing to key on. Confirmed from the runtime log,
    /// where it selects as "&lt;no toolObject&gt;" while every other tool owns a toolObject
    /// named "&lt;X&gt; Editor", and where its only children are "Icon, Title" while Publish
    /// (the other null-toolObject button) carries "Icon, Control, Title, Body". The "no known
    /// editor is active" guard keeps any other tool from borrowing this panel; see
    /// docs/DECISIONS.md D-018.
    /// </summary>
    private static bool IsCursorToolActive()
    {
        try
        {
            var button = ActiveToolButton;
            if (button == null)
                return false;
            if (button.toolObject != null)
                return false;
            if (HasBodyChild(button))
                return false;
            return !NoteEditorIsActive
                && !LineEditorIsActive
                && !ColorEditorIsActive
                && !ThemeEditorIsActive
                && !CameraEditorIsActive
                && !SpeedEditorIsActive
                && !CanvasEditorIsActive
                && !PublishControlIsActive;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Records the toolbar button name, the toolObject it owns, its structural traits and the
    /// panel routing verdict. The basic-settings panel is keyed on the button owning no
    /// toolObject and no Body child (docs/DECISIONS.md D-018), so this line is the evidence
    /// trail whenever tool detection stops matching on a different build.
    /// </summary>
    internal static void LogSelectedTool(ToolbarToolButton? button)
    {
        try
        {
            if (button == null)
            {
                Plugin.LogSource.LogInfo("已选择工具：<按钮为 null>");
                return;
            }

            var toolObjectName = button.toolObject == null ? "<无 toolObject>" : button.toolObject.name;
            var body = button.transform.Find("Body");
            var publishControl = FindPublishControl(button);
            Plugin.LogSource.LogInfo(
                $"已选择工具：按钮 {button.name}，toolObject {toolObjectName}，" +
                $"Body={(body == null ? "无" : "有")}，" +
                $"PublishControl={(publishControl == null ? "无" : publishControl.transform.name)}，" +
                $"面板 {GetPanelMode()}");
        }
        catch
        {
        }
    }

    public SpeedEasePanel(IntPtr pointer) : base(pointer)
    {
    }

    public void Awake()
    {
        Instance = this;
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        Plugin.LogSource.LogInfo("流速缓动面板初始化完成。");
    }

    public void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Update()
    {
        EditorNavigation.HandleShortcuts();
        CustomToolbarTool.Tick();
        ProjectSwitcher.Tick();
        NativeProjectPanel.Tick();
        if (Input.GetKeyDown(KeyCode.F8))
            _visible = !_visible;
        SpeedClipboardController.HandleKeyboard();
        if (!_visible)
            return;

        var mode = GetPanelMode();
        if (mode == PanelMode.None)
            return;

        UpdateLayout();
        UpdateSliderDrag(mode);

        if (!Input.GetMouseButtonDown(0))
            return;

        var mouse = Input.mousePosition;
        var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
        if (!_panelRect.Contains(guiPoint))
        {
            if (_easeDropdownOpen)
                _easeDropdownOpen = false;
            return;
        }

        if (mode == PanelMode.Cursor)
        {
            if (_breakZoomRect.Contains(guiPoint))
                ToggleBreakZoomLimits();
            else if (_thinLineRect.Contains(guiPoint))
                ToggleThinPreviewLines();
            else if (_previewMaskRect.Contains(guiPoint))
                TogglePreviewMask();
            else if (_panelWidthSliderRect.Contains(guiPoint))
                _draggingSlider = 1;
            else if (_panelAlphaSliderRect.Contains(guiPoint))
                _draggingSlider = 2;
            return;
        }

        if (mode == PanelMode.Publish)
            return;

        if (mode == PanelMode.CustomTool)
        {
            // The native body handles its own buttons; the IMGUI panel is only the fallback for
            // when the native widgets could not be built.
            if (NativeProjectPanel.IsReady)
                return;

            for (var index = 0; index < _projectSlotRects.Length; index++)
            {
                if (!_projectSlotRects[index].Contains(guiPoint))
                    continue;
                ActivateProjectSlot(index);
                return;
            }

            if (_projectPickRect.Contains(guiPoint))
            {
                ShowStatus(ProjectSwitcher.PickFiles());
                return;
            }

            return;
        }

        if (mode == PanelMode.CanvasEvent)
        {
            if (_exportClipboardRect.Contains(guiPoint))
                ToggleSystemClipboardExport();
            else if (_importClipboardRect.Contains(guiPoint))
                ImportSystemClipboard(ClipboardTarget.CanvasEvent);
            return;
        }

        if (mode == PanelMode.Line)
        {
            if (_importColorsRect.Contains(guiPoint))
                ImportChartColors();
            else if (_exportClipboardRect.Contains(guiPoint))
                ToggleSystemClipboardExport();
            else if (_importClipboardRect.Contains(guiPoint))
                ImportSystemClipboard(ClipboardTarget.Lines);
            return;
        }

        if (mode == PanelMode.ColorEvent || mode == PanelMode.Theme)
        {
            if (_importColorsRect.Contains(guiPoint))
                ImportChartColors();
            else if (mode == PanelMode.ColorEvent)
                HandleClipboardClick(guiPoint, ColorClipboardTarget());
            return;
        }

        if (mode == PanelMode.Camera)
        {
            HandleClipboardClick(guiPoint, ClipboardTarget.CameraEvent);
            return;
        }

        if (_pending.HasValue)
        {
            if (_continueRect.Contains(guiPoint))
                ContinuePending();
            else if (_cancelRect.Contains(guiPoint))
                CancelPending();
            else if (_exportClipboardRect.Contains(guiPoint))
                ToggleSystemClipboardExport();
            else if (_importClipboardRect.Contains(guiPoint))
                ImportSystemClipboard(ClipboardTarget.Speed);
            return;
        }

        if (_easeDropdownOpen)
        {
            if (_exportClipboardRect.Contains(guiPoint))
            {
                _easeDropdownOpen = false;
                ToggleSystemClipboardExport();
                return;
            }

            if (_importClipboardRect.Contains(guiPoint))
            {
                _easeDropdownOpen = false;
                ImportSystemClipboard(ClipboardTarget.Speed);
                return;
            }

            if (_easeDropdownRect.Contains(guiPoint))
            {
                _easeDropdownOpen = false;
                return;
            }

            for (var index = 0; index < _easeOptionRects.Length; index++)
            {
                if (!_easeOptionRects[index].Contains(guiPoint))
                    continue;
                _easingType = index;
                _easeDropdownOpen = false;
                SetSpeedStatus($"已选择缓动：{EasingNames[_easingType]}。");
                Plugin.LogSource.LogInfo(_speedStatus);
                return;
            }

            _easeDropdownOpen = false;
            return;
        }

        if (_captureStartRect.Contains(guiPoint))
            Capture(true);
        else if (_captureEndRect.Contains(guiPoint))
            Capture(false);
        else if (_easeDropdownRect.Contains(guiPoint))
            _easeDropdownOpen = !_easeDropdownOpen;
        else if (_generateRect.Contains(guiPoint))
            PrepareGenerate();
        else if (_clearRect.Contains(guiPoint))
            ClearEndpoints();
        else if (_exportClipboardRect.Contains(guiPoint))
            ToggleSystemClipboardExport();
        else if (_importClipboardRect.Contains(guiPoint))
            ImportSystemClipboard(ClipboardTarget.Speed);
    }

    public void LateUpdate()
    {
        SpeedClipboardController.FlushPendingPaste();
        EditorOperations.UpdatePreviewLineWidth(_thinPreviewLines);
        PreviewMaskControl.Update();
        EditorNavigation.ApplyHorizontalZoom();
        CrossCanvasLineClipboard.Track();
    }

    public void OnGUI()
    {
        if (EditorNavigation.BreakZoomLimits && Math.Abs(EditorNavigation.HorizontalScale - 1f) > 0.02f)
            GUI.Label(new Rect(Screen.width - 188f, 8f, 170f, 22f), $"缩放 {EditorNavigation.HorizontalScale * 100f:0}%");

        // The play-area frame is independent of the panel's visibility, so it is drawn above the
        // early return below (same level as the zoom readout). The cross-canvas clipboard readout is
        // drawn here for the same reason: it belongs to the line tool, which has no plugin panel.
        PlayAreaFrame.Draw();
        CrossCanvasLineClipboard.DrawOverlay();

        if (!_visible)
            return;
        var mode = GetPanelMode();
        if (mode == PanelMode.None)
            return;
        if (mode == PanelMode.CustomTool && NativeProjectPanel.IsReady)
            return; // native body only: drawing here would also swallow its mouse events
        UpdateLayout();
        var previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_panelAlpha));
        var guiEvent = Event.current;
        if (guiEvent != null
            && _panelRect.Contains(guiEvent.mousePosition)
            && guiEvent.isMouse
            && !_panelWidthSliderRect.Contains(guiEvent.mousePosition)
            && !_panelAlphaSliderRect.Contains(guiEvent.mousePosition))
            guiEvent.Use();

        try
        {
            if (mode == PanelMode.Cursor)
            {
                DrawBasicSettingsPanel();
                return;
            }

            if (mode == PanelMode.Publish)
            {
                DrawPublishPanel();
                return;
            }

            if (mode == PanelMode.CustomTool)
            {
                DrawCustomToolPanel();
                return;
            }

            if (mode == PanelMode.CanvasEvent)
            {
                DrawCanvasEventPanel();
                return;
            }

            if (mode == PanelMode.Line)
            {
                DrawLinePanel();
                return;
            }

            if (mode == PanelMode.Theme)
            {
                DrawThemePanel();
                return;
            }

            if (mode == PanelMode.ColorEvent)
            {
                DrawColorPanel();
                return;
            }

            if (mode == PanelMode.Camera)
            {
                DrawEventPanel(
                    "摄像机移动剪贴板（F8 显示/隐藏）",
                    "只框选摄像机移动，不框选缩放。\nCtrl+C/X：拖动框选复制或剪切。\nCtrl+V：最早事件对齐鼠标。\nCtrl+Shift+V：左右镜像并对齐鼠标。\n剪切保留首个事件，粘贴覆盖同拍旧事件。",
                    110f);
                return;
            }

            GUI.depth = 0;
            GUI.Box(_panelRect, "画布流速缓动（F8 显示/隐藏）");
            var x = _panelRect.x + 14f;
            var y = _panelRect.y + 28f;
            GUI.Label(new Rect(x, y, ContentWidth(), 22f), $"当前垂直细分：1/{SafeSplitCount()}");
            y += 24f;
            GUI.Label(new Rect(x, y, ContentWidth(), 48f), _speedStatus);
            y += 52f;

            DrawButton(_captureStartRect, "记录起点");
            DrawButton(_captureEndRect, "记录终点");
            y += 34f;
            GUI.Label(new Rect(x, y, ContentWidth(), 22f), FormatEndpoint("起点", _start));
            y += 21f;
            GUI.Label(new Rect(x, y, ContentWidth(), 22f), FormatEndpoint("终点", _end));
            y += 28f;

            GUI.Label(new Rect(x, y, 52f, 28f), "缓动");
            DrawButton(_easeDropdownRect, $"{EasingNames[_easingType]} {(_easeDropdownOpen ? "▲" : "▼")}");
            y += 36f;
            if (_easeDropdownOpen)
            {
                for (var index = 0; index < EasingNames.Length; index++)
                    DrawButton(_easeOptionRects[index], EasingNames[index]);

                y += EasingNames.Length * 24f + 8f;
            }

            GUI.Label(new Rect(x, y, ContentWidth(), 24f), EstimateText());
            y += 28f;

            DrawButton(_generateRect, "生成并替换区间");
            DrawButton(_clearRect, "清除起止点");
            y += 39f;
            GUI.Label(
                new Rect(x, y, ContentWidth(), 62f),
                "流速剪贴板：Ctrl+C/X 后拖动框选；Ctrl+V 保持原拍数粘贴；Ctrl+Shift+V 对齐播放头。粘贴会覆盖同拍旧节点。");
            DrawClipboardButtons();

            if (_pending is { } pending)
            {
                var warningRect = new Rect(x, _panelRect.yMax - 140f, ContentWidth(), 52f);
                GUI.Box(warningRect, $"警告：本次预计生成 {pending.EstimatedCount} 个节点，超过 4096。是否继续？");
                DrawButton(_continueRect, "继续生成");
                DrawButton(_cancelRect, "取消");
            }
        }
        finally
        {
            GUI.color = previousColor;
        }
    }

    internal void SetSpeedStatus(string status)
    {
        _speedStatus = status;
    }

    internal void SetCanvasStatus(string status)
    {
        _canvasStatus = status;
    }

    internal void SetEventStatus(string status)
    {
        _eventStatus = status;
        _clipboardMessage = status;
    }

    internal void SetClipboardMessage(string message)
    {
        _clipboardMessage = message;
    }

    internal static bool PointerBlocksChartInput()
    {
        var panel = Instance;
        if (panel == null || !panel._visible || GetPanelMode() == PanelMode.None)
            return false;

        var mouse = Input.mousePosition;
        var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
        return panel._panelRect.width > 0f && panel._panelRect.Contains(guiPoint);
    }

    internal static bool TryGetActiveSpeedEditor(out SpeedEditor editor)
    {
        editor = ActiveSpeedEditor!;
        return SpeedEditorIsActive;
    }

    internal static bool TryGetActiveCanvasEditor(out CanvasEditor editor)
    {
        editor = ActiveCanvasEditor!;
        return CanvasEditorIsActive;
    }

    internal static bool TryGetActiveLineEditor(out LineEditor editor)
    {
        editor = ActiveLineEditor!;
        return LineEditorIsActive;
    }

    internal static bool TryGetActiveColorEditor(out ColorEditor editor)
    {
        editor = ActiveColorEditor!;
        return ColorEditorIsActive;
    }

    internal static bool TryGetActiveCameraEditor(out CameraEditor editor)
    {
        editor = ActiveCameraEditor!;
        return CameraEditorIsActive;
    }

    private void UpdateLayout()
    {
        var mode = GetPanelMode();
        var width = 350f * Mathf.Clamp(_panelWidthScale, 0.7f, 1.4f);
        float height = mode switch
        {
            PanelMode.Cursor => 336f,
            PanelMode.Publish => 280f,
            PanelMode.CustomTool => 380f,
            PanelMode.CanvasEvent => 320f,
            PanelMode.ColorEvent => 470f,
            PanelMode.Theme => 220f,
            PanelMode.Camera => 330f,
            PanelMode.Line => 330f,
            _ => (_pending.HasValue ? 560f : 470f) + (_easeDropdownOpen ? EasingNames.Length * 24f + 8f : 0f),
        };
        var left = Math.Max(8f, Screen.width - width - 18f);
        var top = Math.Max(8f, Math.Min(90f, Screen.height - height - 8f));
        _panelRect = new Rect(left, top, width, height);

        var x = left + 14f;
        var content = width - 28f;
        _clipboardHintRect = new Rect(x, top + height - 116f, content, 36f);
        _exportClipboardRect = new Rect(x, top + height - 74f, content, 28f);
        _importClipboardRect = new Rect(x, top + height - 40f, content, 28f);

        if (mode == PanelMode.Cursor)
        {
            _breakZoomRect = new Rect(x, top + 48f, content, 28f);
            _thinLineRect = new Rect(x, top + 84f, content, 28f);
            _previewMaskRect = new Rect(x, top + 120f, content, 28f);
            _panelWidthSliderRect = new Rect(x, top + 182f, content, 22f);
            _panelAlphaSliderRect = new Rect(x, top + 236f, content, 22f);
            return;
        }

        if (mode == PanelMode.Publish)
            return;

        if (mode == PanelMode.CustomTool)
        {
            _projectSlotRects = new Rect[ProjectSlotList.Capacity];
            for (var index = 0; index < _projectSlotRects.Length; index++)
                _projectSlotRects[index] = new Rect(x, top + 116f + index * 32f, content, 28f);
            _projectPickRect = new Rect(x, top + 220f, content, 28f);
            return;
        }

        if (mode == PanelMode.ColorEvent)
        {
            _importColorsRect = new Rect(x, top + 246f, content, 32f);
            return;
        }

        if (mode == PanelMode.Line || mode == PanelMode.Theme)
        {
            _importColorsRect = new Rect(x, top + 118f, content, 32f);
            return;
        }

        if (mode != PanelMode.Speed)
            return;

        var captureY = top + 104f;
        _captureStartRect = new Rect(x, captureY, content * 0.48f, 28f);
        _captureEndRect = new Rect(x + content * 0.52f, captureY, content * 0.48f, 28f);
        var easingY = captureY + 83f;
        _easeDropdownRect = new Rect(x + 56f, easingY, content - 56f, 28f);
        var optionsTop = easingY + 32f;
        _easeOptionRects = new Rect[EasingNames.Length];
        for (var index = 0; index < EasingNames.Length; index++)
            _easeOptionRects[index] = new Rect(x + 56f, optionsTop + index * 24f, content - 56f, 22f);

        var generateY = optionsTop
            + (_easeDropdownOpen ? EasingNames.Length * 24f + 8f : 0f)
            + 28f;
        _generateRect = new Rect(x, generateY, content * 0.48f, 32f);
        _clearRect = new Rect(x + content * 0.52f, generateY, content * 0.48f, 32f);
        _continueRect = new Rect(x, top + height - 151f, content * 0.48f, 27f);
        _cancelRect = new Rect(x + content * 0.52f, top + height - 151f, content * 0.48f, 27f);
    }

    private float ContentWidth() => Math.Max(120f, _panelRect.width - 28f);

    private void DrawCanvasEventPanel()
    {
        GUI.Box(_panelRect, "画布事件剪贴板（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 32f;
        GUI.Label(new Rect(x, y, ContentWidth(), 50f), _canvasStatus);
        y += 58f;
        GUI.Label(
            new Rect(x, y, ContentWidth(), 94f),
            "Ctrl+C：拖动框选后复制\nCtrl+X：拖动框选后剪切\nCtrl+V：最早事件对齐鼠标粘贴\nCtrl+Shift+V：左右镜像并对齐鼠标\n框选只使用上下端点；粘贴覆盖同拍旧事件。");
        DrawClipboardButtons();
    }

    private void DrawLinePanel()
    {
        GUI.Box(_panelRect, "画线工具（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 28f;
        GUI.Label(new Rect(x, y, ContentWidth(), 78f), _colorStatus);
        DrawButton(_importColorsRect, "一键加入调色板");
        GUI.Label(new Rect(x, _panelRect.y + 158f, ContentWidth(), 36f), "打开导出后，原版复制或剪切线时会同时写入系统剪切板。导入只替换内部线剪贴板。");
        DrawClipboardButtons();
    }

    private void DrawColorPanel()
    {
        GUI.Box(_panelRect, "颜色事件剪贴板（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 28f;
        GUI.Label(new Rect(x, y, ContentWidth(), 66f), _eventStatus);
        GUI.Label(
            new Rect(x, y + 70f, ContentWidth(), 140f),
            "Ctrl+C/X：拖动框选，只看上下端拍数决定范围。\n" +
            "选区整个在大地 x=0 左侧：只取线色覆盖。\n" +
            "整个在右侧：只取判定环。跨过 x=0：两者都取。\n" +
            "Ctrl+V：粘贴上次复制的内容，保持原拍数；\n" +
            "Ctrl+Shift+V：最早事件对齐播放头。\n" +
            "目标拍已有事件时覆盖它，不再产生同拍重复。");
        DrawButton(_importColorsRect, "一键加入调色板");
        DrawClipboardButtons();
    }

    private void DrawThemePanel()
    {
        GUI.Box(_panelRect, "主题颜色（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 28f;
        GUI.Label(new Rect(x, y, ContentWidth(), 72f), _colorStatus);
        DrawButton(_importColorsRect, "一键加入调色板");
    }

    private void DrawBasicSettingsPanel()
    {
        GUI.Box(_panelRect, "基础设置（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        DrawButton(_breakZoomRect, EditorNavigation.BreakZoomLimits ? "突破缩放极限：开" : "突破缩放极限：关");
        DrawButton(_thinLineRect, _thinPreviewLines ? "预览细线：开" : "预览细线：关");
        DrawButton(_previewMaskRect, PreviewMaskControl.Enabled ? "预览蒙版：开" : "预览蒙版：关");
        GUI.Label(new Rect(x, _panelRect.y + 158f, ContentWidth(), 22f), $"面板宽度 {_panelWidthScale * 100f:0}%");
        DrawSliderTrack(_panelWidthSliderRect, (_panelWidthScale - 0.7f) / 0.7f);
        GUI.Label(new Rect(x, _panelRect.y + 212f, ContentWidth(), 22f), $"面板透明度 {_panelAlpha * 100f:0}%");
        DrawSliderTrack(_panelAlphaSliderRect, (_panelAlpha - 0.35f) / 0.65f);
        GUI.Label(new Rect(x, _panelRect.y + 266f, ContentWidth(), 40f), PreviewMaskControl.Status);
        GUI.Label(new Rect(x, _panelRect.y + 306f, ContentWidth(), 24f), "Ctrl+Shift+滚轮 整体缩放，Ctrl+M 复位。");
    }

    private void DrawPublishPanel()
    {
        RefreshPublishStats();
        GUI.Box(_panelRect, "发布统计（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 32f;
        GUI.Label(new Rect(x, y, ContentWidth(), 220f), _publishStatus);
    }

    /// <summary>
    /// The plugin's own toolbar entry: switch the open project without restarting the editor.
    /// The flow, file handling and the reason it must save first are in D-024.
    /// </summary>
    private void DrawCustomToolPanel()
    {
        GUI.Box(_panelRect, $"{CustomToolbarTool.ButtonTitle}（F8 显示/隐藏）");
        var x = _panelRect.x + 14f;
        var top = _panelRect.y;
        var y = top + 34f;

        GUI.Label(new Rect(x, y, ContentWidth(), 40f), DescribeCurrentProject());
        y += 46f;

        var slots = ProjectSlots.Slots;
        var currentChart = ProjectInformation.instance?.filePath ?? string.Empty;
        GUI.Label(new Rect(x, y, ContentWidth(), 20f), $"最近工程（{slots.Count}/{ProjectSlotList.Capacity}）");
        y += 24f;

        for (var index = 0; index < ProjectSlotList.Capacity; index++)
        {
            var rect = index < _projectSlotRects.Length
                ? _projectSlotRects[index]
                : new Rect(x, y, ContentWidth(), 28f);
            DrawButton(rect, DescribeSlot(index, index < slots.Count ? slots[index] : null, currentChart));
        }

        DrawButton(_projectPickRect, "选择文件…（原版两步：谱面 → 音乐）");
        GUI.Label(new Rect(x, top + 258f, ContentWidth(), 20f), $"槽位文件：{ProjectSlots.StoragePath}");
        GUI.Label(new Rect(x, top + 284f, ContentWidth(), 88f), ProjectSwitcher.Status);
    }

    private static string DescribeCurrentProject()
    {
        try
        {
            var project = ProjectInformation.instance;
            if (project == null)
                return "当前工程：<编辑器状态不可用>";

            var chart = project.filePath ?? string.Empty;
            var song = project.fileControl?.songPath ?? string.Empty;
            var dirty = ProjectInformation.notSaved ? "（有未保存改动，切换前会自动保存）" : "（已保存）";
            if (chart.Length == 0)
                return $"当前工程：<未保存的新工程> {dirty}";

            return $"当前工程：{Path.GetFileName(chart)}\n音乐：{(song.Length == 0 ? "<无>" : Path.GetFileName(song))} {dirty}";
        }
        catch (Exception error)
        {
            return $"当前工程：<读取失败 {error.GetType().Name}>";
        }
    }

    private static string DescribeSlot(int index, ProjectSlot? slot, string currentChart)
    {
        if (slot == null)
            return $"{index + 1}. （空）";

        var value = slot.Value;
        var isCurrent = string.Equals(value.ChartPath, currentChart, StringComparison.OrdinalIgnoreCase);
        var song = string.IsNullOrEmpty(value.SongPath) ? "<无音乐>" : Path.GetFileName(value.SongPath);
        return $"{(isCurrent ? "▶ " : string.Empty)}{index + 1}. {Path.GetFileName(value.ChartPath)} + {song}";
    }

    private void ActivateProjectSlot(int index)
    {
        var slots = ProjectSlots.Slots;
        if (index < 0 || index >= slots.Count)
        {
            ShowStatus($"{index + 1}. 槽位为空：先用「选择文件…」打开一个工程，它会被自动记住。");
            return;
        }

        var slot = slots[index];
        if (ProjectSwitcher.IsBusy)
        {
            ShowStatus("正在切换中，请稍候…");
            return;
        }

        ShowStatus(ProjectSwitcher.SwitchTo(slot.ChartPath, slot.SongPath));
    }

    private void RefreshPublishStats()
    {
        try
        {
            Chart? chart = null;
            if (ActivePublishControl?.projectInformation != null)
                chart = ActivePublishControl.projectInformation.chart;
            chart ??= ProjectInformation.instance?.chart;
            _publishStatus = EditorOperations.FormatChartStatistics(chart);
        }
        catch (Exception error)
        {
            _publishStatus = $"统计失败：{error.Message}";
        }
    }

    private void UpdateSliderDrag(PanelMode mode)
    {
        if (mode != PanelMode.Cursor)
        {
            _draggingSlider = 0;
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            _draggingSlider = 0;
            return;
        }

        if (_draggingSlider == 0)
            return;

        var mouse = Input.mousePosition;
        var guiX = mouse.x;
        Rect track = _draggingSlider == 1 ? _panelWidthSliderRect : _panelAlphaSliderRect;
        var t = track.width <= 1f ? 0f : Mathf.Clamp01((guiX - track.x) / track.width);
        if (_draggingSlider == 1)
            _panelWidthScale = Mathf.Lerp(0.7f, 1.4f, t);
        else
            _panelAlpha = Mathf.Lerp(0.35f, 1f, t);
    }

    private void DrawEventPanel(string title, string help, float helpHeight = 88f)
    {
        GUI.Box(_panelRect, title);
        var x = _panelRect.x + 14f;
        var y = _panelRect.y + 32f;
        GUI.Label(new Rect(x, y, ContentWidth(), 44f), _eventStatus);
        y += 48f;
        GUI.Label(new Rect(x, y, ContentWidth(), helpHeight), help);
        DrawClipboardButtons();
    }

    private void DrawClipboardButtons()
    {
        GUI.Label(_clipboardHintRect, _clipboardMessage);
        DrawButton(_exportClipboardRect, _exportToSystemClipboard ? "导出到剪切板：开" : "导出到剪切板：关");
        DrawButton(_importClipboardRect, "从剪切板导入");
    }

    private void ToggleBreakZoomLimits()
    {
        ShowStatus(EditorNavigation.SetBreakZoomLimits(!EditorNavigation.BreakZoomLimits));
        Plugin.LogSource.LogInfo(_clipboardMessage);
    }

    private void ToggleSystemClipboardExport()
    {
        _exportToSystemClipboard = !_exportToSystemClipboard;
        var status = _exportToSystemClipboard
            ? "已打开系统剪切板导出。框选复制或剪切结束后会写入系统剪切板。"
            : "已关闭系统剪切板导出。复制和剪切只更新内部剪贴板。";
        ShowStatus(status);
    }

    private void ImportSystemClipboard(ClipboardTarget target)
    {
        ShowStatus(SpeedClipboardController.ImportSystemClipboard(target));
    }

    internal void ShowStatus(string status)
    {
        _clipboardMessage = status;
        // Deliberately not mirrored into the project body: that body has its own status line and must
        // not show messages from other features (e.g. canvas-speed easing) - see NativeProjectPanel.
        switch (GetPanelMode())
        {
            case PanelMode.CanvasEvent:
                SetCanvasStatus(status);
                break;
            case PanelMode.ColorEvent:
            case PanelMode.Camera:
            case PanelMode.Line:
            case PanelMode.Theme:
            case PanelMode.Cursor:
                SetEventStatus(status);
                break;
            case PanelMode.Publish:
                _publishStatus = status;
                break;
            case PanelMode.CustomTool:
                _customStatus = status;
                break;
            default:
                SetSpeedStatus(status);
                break;
        }

        Plugin.LogSource.LogInfo(status);
    }

    private void HandleClipboardClick(Vector2 guiPoint, ClipboardTarget target)
    {
        if (_exportClipboardRect.Contains(guiPoint))
            ToggleSystemClipboardExport();
        else if (_importClipboardRect.Contains(guiPoint))
            ImportSystemClipboard(target);
    }

    private static ClipboardTarget ColorClipboardTarget() => ClipboardTarget.ColorEvent;

    private static void DrawButton(Rect rect, string label)
    {
        GUI.Box(rect, label);
    }

    private static void DrawSliderTrack(Rect rect, float normalized)
    {
        GUI.Box(rect, string.Empty);
        var fill = Mathf.Clamp01(normalized);
        if (fill <= 0.01f)
            return;
        var fillRect = new Rect(rect.x + 2f, rect.y + 2f, Math.Max(4f, (rect.width - 4f) * fill), rect.height - 4f);
        GUI.Box(fillRect, string.Empty);
    }

    private void ClearEndpoints()
    {
        _start = null;
        _end = null;
        _pending = null;
        SetSpeedStatus("已清除记录的起点和终点。");
    }

    private void Capture(bool captureStart)
    {
        Plugin.LogSource.LogInfo($"点击了记录{(captureStart ? "起点" : "终点")}按钮。");
        var editor = ActiveSpeedEditor;
        if (!EditorOperations.TryCaptureSpeedPointAtCurrentBeat(editor, out var endpoint, out var error))
        {
            SetSpeedStatus(error);
            return;
        }

        if (captureStart)
            _start = endpoint;
        else
            _end = endpoint;
        _pending = null;
        SetSpeedStatus($"已记录{(captureStart ? "起点" : "终点")}：画布 {endpoint.CanvasIndex}，第 {endpoint.Beat:0.######} 拍，流速 {endpoint.Value:0.######}。");
    }

    private void PrepareGenerate()
    {
        Plugin.LogSource.LogInfo("点击了生成并替换区间按钮。");
        try
        {
            if (!_start.HasValue || !_end.HasValue)
                throw new InvalidOperationException("请先记录起点和终点两个流速节点。");
            var splitCount = SafeSplitCount();
            var estimated = EditorOperations.EstimateSamples(_start.Value, _end.Value, splitCount);
            var pending = new PendingOperation(_start.Value, _end.Value, splitCount, _easingType, estimated);
            if (estimated > 4096)
            {
                _pending = pending;
                SetSpeedStatus($"大规模操作警告：预计生成 {estimated} 个节点。");
                return;
            }
            Execute(pending);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SetSpeedStatus($"生成失败：{error.Message}");
        }
    }

    private void ContinuePending()
    {
        if (_pending is { } pending)
            Execute(pending);
    }

    private void CancelPending()
    {
        _pending = null;
        SetSpeedStatus("已取消生成，谱面没有发生变化。");
    }

    private void Execute(PendingOperation operation)
    {
        _pending = null;
        try
        {
            var editor = ActiveSpeedEditor;
            if (editor == null)
                throw new InvalidOperationException("当前无法访问流速编辑器，请确认谱面已经打开。");
            SetSpeedStatus(EditorOperations.ApplyEase(
                editor,
                operation.Start,
                operation.End,
                operation.SplitCount,
                operation.EasingType));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            SetSpeedStatus($"生成失败：{error.Message}");
        }
    }

    private void ToggleThinPreviewLines()
    {
        _thinPreviewLines = !_thinPreviewLines;
        if (!_thinPreviewLines)
            EditorOperations.UpdatePreviewLineWidth(false);
        _colorStatus = _thinPreviewLines
            ? "预览细线已打开。进入带预览的播放后，谱面线宽约为原来的 75%。"
            : "预览细线已关闭，线宽会在退出预览后恢复。";
        Plugin.LogSource.LogInfo(_colorStatus);
    }

    private void TogglePreviewMask()
    {
        // The Cursor panel shows PreviewMaskControl.Status, so the message is not duplicated here.
        PreviewMaskControl.SetEnabled(!PreviewMaskControl.Enabled);
    }

    private void ImportChartColors()
    {
        Plugin.LogSource.LogInfo("点击了一键加入调色板按钮。");
        try
        {
            var chart = ActiveLineEditor?.projectInformation?.chart
                ?? ProjectInformation.instance?.chart;
            _colorStatus = EditorOperations.ImportChartColorsToPalette(chart);
            Plugin.LogSource.LogInfo(_colorStatus);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError(error);
            _colorStatus = $"导入失败：{error.Message}";
        }
    }

    private string EstimateText()
    {
        if (!_start.HasValue || !_end.HasValue)
            return "预计节点数：请先记录两个端点";
        try
        {
            return $"预计节点数：{EditorOperations.EstimateSamples(_start.Value, _end.Value, SafeSplitCount())}";
        }
        catch (Exception error)
        {
            return $"预计节点数：{error.Message}";
        }
    }

    private static int SafeSplitCount()
    {
        try
        {
            return Math.Max(1, ProjectInformation.SnapLinesSplitCount);
        }
        catch
        {
            return 1;
        }
    }

    private static string FormatEndpoint(string label, CapturedEndpoint? endpoint)
    {
        return endpoint is { } value
            ? $"{label}：画布 {value.CanvasIndex}，第 {value.Beat:0.######} 拍，流速 {value.Value:0.######}"
            : $"{label}：尚未记录";
    }

    private static PanelMode GetPanelMode()
    {
        // The plugin's own toolbar entry is matched by identity, not by structure: it is created
        // by the plugin itself, so a reference comparison cannot be fooled by another tool.
        if (CustomToolbarTool.IsSelected)
            return PanelMode.CustomTool;
        if (ColorEditorIsActive)
            return PanelMode.ColorEvent;
        if (ThemeEditorIsActive)
            return PanelMode.Theme;
        if (CameraEditorIsActive)
            return PanelMode.Camera;
        if (SpeedEditorIsActive)
            return PanelMode.Speed;
        if (CanvasEditorIsActive)
            return PanelMode.CanvasEvent;
        if (LineEditorIsActive)
            return PanelMode.Line;
        if (PublishControlIsActive)
            return PanelMode.Publish;
        if (IsCursorToolActive())
            return PanelMode.Cursor;
        return PanelMode.None;
    }

    private enum PanelMode
    {
        None,
        Speed,
        CanvasEvent,
        ColorEvent,
        Theme,
        Camera,
        Line,
        Cursor,
        Publish,
        CustomTool,
    }

    private readonly record struct PendingOperation(
        CapturedEndpoint Start,
        CapturedEndpoint End,
        int SplitCount,
        int EasingType,
        long EstimatedCount);
}

[HarmonyPatch(typeof(SpeedEditor), nameof(SpeedEditor.Start))]
internal static class SpeedEditorStartPatch
{
    private static void Postfix(SpeedEditor __instance)
    {
        SpeedEasePanel.ActiveSpeedEditor = __instance;
    }
}

[HarmonyPatch(typeof(SpeedEditor), nameof(SpeedEditor.OnEnable))]
internal static class SpeedEditorEnablePatch
{
    private static void Postfix(SpeedEditor __instance)
    {
        SpeedEasePanel.ActiveSpeedEditor = __instance;
    }
}

[HarmonyPatch(typeof(SpeedEditor), nameof(SpeedEditor.OnDisable))]
internal static class SpeedEditorDisablePatch
{
    private static void Postfix(SpeedEditor __instance)
    {
        if (SpeedEasePanel.ActiveSpeedEditor == __instance)
            SpeedEasePanel.ActiveSpeedEditor = null;
    }
}

[HarmonyPatch(typeof(CanvasEditor), nameof(CanvasEditor.Start))]
internal static class CanvasEditorStartPatch
{
    private static void Postfix(CanvasEditor __instance)
    {
        SpeedEasePanel.ActiveCanvasEditor = __instance;
        ExtraEasing.InstallFor(__instance);
    }
}

[HarmonyPatch(typeof(CanvasEditor), nameof(CanvasEditor.OnEnable))]
internal static class CanvasEditorEnablePatch
{
    private static void Postfix(CanvasEditor __instance)
    {
        SpeedEasePanel.ActiveCanvasEditor = __instance;
        ExtraEasing.InstallFor(__instance);
    }
}

[HarmonyPatch(typeof(CanvasEditor), nameof(CanvasEditor.OnDisable))]
internal static class CanvasEditorDisablePatch
{
    private static void Postfix(CanvasEditor __instance)
    {
        if (SpeedEasePanel.ActiveCanvasEditor == __instance)
            SpeedEasePanel.ActiveCanvasEditor = null;
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.ToolInput))]
internal static class LineEditorToolInputPatch
{
    private static bool Prefix()
    {
        if (!SpeedEasePanel.PointerBlocksChartInput())
            return true;

        return !Input.GetMouseButton(0)
            && !Input.GetMouseButtonDown(0)
            && !Input.GetMouseButtonUp(0);
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.Start))]
internal static class LineEditorStartPatch
{
    private static void Postfix(LineEditor __instance)
    {
        SpeedEasePanel.ActiveLineEditor = __instance;
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.Update))]
internal static class LineEditorUpdatePatch
{
    private static void Postfix(LineEditor __instance)
    {
        SpeedEasePanel.ActiveLineEditor = __instance;
    }
}

[HarmonyPatch(typeof(LineEditor), nameof(LineEditor.OnDisable))]
internal static class LineEditorDisablePatch
{
    private static void Postfix(LineEditor __instance)
    {
        if (SpeedEasePanel.ActiveLineEditor == __instance)
            SpeedEasePanel.ActiveLineEditor = null;
    }
}

[HarmonyPatch(typeof(ColorEditor), nameof(ColorEditor.Start))]
internal static class ColorEditorStartPatch
{
    private static void Postfix(ColorEditor __instance)
    {
        SpeedEasePanel.ActiveColorEditor = __instance;
    }
}

[HarmonyPatch(typeof(ColorEditor), nameof(ColorEditor.Update))]
internal static class ColorEditorUpdatePatch
{
    private static void Postfix(ColorEditor __instance)
    {
        SpeedEasePanel.ActiveColorEditor = __instance;
    }
}

[HarmonyPatch(typeof(ColorEditor), nameof(ColorEditor.OnDisable))]
internal static class ColorEditorDisablePatch
{
    private static void Postfix(ColorEditor __instance)
    {
        if (SpeedEasePanel.ActiveColorEditor == __instance)
            SpeedEasePanel.ActiveColorEditor = null;
    }
}

[HarmonyPatch(typeof(CameraEditor), nameof(CameraEditor.Start))]
internal static class CameraEditorStartPatch
{
    private static void Postfix(CameraEditor __instance)
    {
        SpeedEasePanel.ActiveCameraEditor = __instance;
        ExtraEasing.InstallFor(__instance);
    }
}

[HarmonyPatch(typeof(CameraEditor), nameof(CameraEditor.OnEnable))]
internal static class CameraEditorEnablePatch
{
    private static void Postfix(CameraEditor __instance)
    {
        SpeedEasePanel.ActiveCameraEditor = __instance;
        ExtraEasing.InstallFor(__instance);
    }
}

[HarmonyPatch(typeof(CameraEditor), nameof(CameraEditor.OnDisable))]
internal static class CameraEditorDisablePatch
{
    private static void Postfix(CameraEditor __instance)
    {
        if (SpeedEasePanel.ActiveCameraEditor == __instance)
            SpeedEasePanel.ActiveCameraEditor = null;
    }
}

[HarmonyPatch(typeof(NoteEditor), nameof(NoteEditor.Start))]
internal static class NoteEditorStartPatch
{
    private static void Postfix(NoteEditor __instance)
    {
        SpeedEasePanel.ActiveNoteEditor = __instance;
    }
}

[HarmonyPatch(typeof(NoteEditor), nameof(NoteEditor.Update))]
internal static class NoteEditorUpdatePatch
{
    private static void Postfix(NoteEditor __instance)
    {
        SpeedEasePanel.ActiveNoteEditor = __instance;
    }
}

[HarmonyPatch(typeof(NoteEditor), nameof(NoteEditor.OnDisable))]
internal static class NoteEditorDisablePatch
{
    private static void Postfix(NoteEditor __instance)
    {
        if (SpeedEasePanel.ActiveNoteEditor == __instance)
            SpeedEasePanel.ActiveNoteEditor = null;
    }
}

[HarmonyPatch(typeof(ThemeEditor), nameof(ThemeEditor.Start))]
internal static class ThemeEditorStartPatch
{
    private static void Postfix(ThemeEditor __instance)
    {
        SpeedEasePanel.ActiveThemeEditor = __instance;
    }
}

[HarmonyPatch(typeof(ThemeEditor), nameof(ThemeEditor.Update))]
internal static class ThemeEditorUpdatePatch
{
    private static void Postfix(ThemeEditor __instance)
    {
        SpeedEasePanel.ActiveThemeEditor = __instance;
    }
}

[HarmonyPatch(typeof(ThemeEditor), nameof(ThemeEditor.OnDisable))]
internal static class ThemeEditorDisablePatch
{
    private static void Postfix(ThemeEditor __instance)
    {
        if (SpeedEasePanel.ActiveThemeEditor == __instance)
            SpeedEasePanel.ActiveThemeEditor = null;
    }
}

[HarmonyPatch(typeof(ToolbarToolButton), nameof(ToolbarToolButton.SelectTool))]
internal static class ToolbarToolSelectPatch
{
    private static void Postfix(ToolbarToolButton __instance)
    {
        SpeedEasePanel.ActiveToolButton = __instance;
        SpeedEasePanel.LogSelectedTool(__instance);
    }
}

[HarmonyPatch(typeof(ToolbarToolButton), nameof(ToolbarToolButton.UnselectTool))]
internal static class ToolbarToolUnselectPatch
{
    private static void Postfix(ToolbarToolButton __instance)
    {
        if (SpeedEasePanel.ActiveToolButton == __instance)
            SpeedEasePanel.ActiveToolButton = null;
    }
}

[HarmonyPatch(typeof(ToolbarToolButton), nameof(ToolbarToolButton.Update))]
internal static class ToolbarToolUpdatePatch
{
    private static void Postfix(ToolbarToolButton __instance)
    {
        try
        {
            if (ToolbarToolButton.activeTool == __instance)
                SpeedEasePanel.ActiveToolButton = __instance;
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(ToolbarToolButton), nameof(ToolbarToolButton.Start))]
internal static class ToolbarToolStartPatch
{
    private static void Postfix(ToolbarToolButton __instance)
    {
        try
        {
            if (ToolbarToolButton.activeTool == __instance)
                SpeedEasePanel.ActiveToolButton = __instance;
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(PublishControl), nameof(PublishControl.Start))]
internal static class PublishControlStartPatch
{
    private static void Postfix(PublishControl __instance)
    {
        SpeedEasePanel.ActivePublishControl = __instance;
    }
}
