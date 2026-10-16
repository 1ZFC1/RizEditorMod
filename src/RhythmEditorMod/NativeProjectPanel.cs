using Il2CppInterop.Runtime;
using RhythmEditorMod.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// The plugin tool's own body, built from copies of a real tool body so it looks and behaves like
/// the editor's other tool panels instead of like the start screen.
///
/// Clone sources (identified by the 2026-09-23 body probe):
/// - body     : <c>Canvas/Toolbar/Tools/Edit Canvas/Body</c> - the most compact real body
///              (<c>Body &gt; Title{BG}, Lock, Canvas Title, AddCanvas, DeleteCanvas</c>)
/// - button   : the same body's <c>DeleteCanvas</c> (a native labelled button: Graphic + Button + Text)
/// - label    : the same body's <c>Canvas Title</c>
///
/// The clone keeps the body's own Title bar and layout, drops the source tool's controls, and adds
/// the three project slots, the picker and a status line. <c>ToolbarBodyHolder</c> components cloned
/// from the source are stripped (they register themselves to the button they came from), and a fresh
/// holder pointing at the plugin's own button is added instead.
///
/// The body keeps the plugin's own name (<c>PluginBody</c>) and is shown whenever the plugin tool
/// is selected; it deliberately does not follow the mouse. Two attempts at the toolbar's own
/// hover/lock behaviour - including handing the body to the game by naming it <c>Body</c> - were
/// rolled back on the user's request (2026-09-23), because a body that fades out also sets
/// <c>blocksRaycasts = false</c> and could no longer be reached with the pointer.
///
/// Note: Il2CppInterop hands out some UI objects as base <c>Transform</c> proxies, so every
/// RectTransform lookup goes through <c>TryCast</c> (see <see cref="Rect"/>).
/// </summary>
internal static class NativeProjectPanel
{
    /// <summary>Fixed title-bar caption; the toolbar entry uses the same word.</summary>
    private const string TitleText = "项目";

    /// <summary>
    /// The body keeps the plugin's own name rather than the native <c>Body</c>, so the toolbar does
    /// not claim it as one of its own tool bodies.
    /// </summary>
    private const string BodyObjectName = "PluginBody";

    private static GameObject? _root;
    private static bool _lastVisible;
    private static bool _visibilityKnown;
    private static readonly List<Action<string>?> SlotSetters = new();
    private static Action<string>? _pickSetter;
    private static Action<string>? _statusSetter;
    private static Transform? _titleNode;
    private static string _status = string.Empty;
    private static bool _failed;

    internal static bool IsReady => _root != null;

    internal static string Diagnostics { get; private set; } = "尚未创建";

    /// <summary>Builds once the toolbar entry exists, then keeps the body in sync.</summary>
    internal static void Tick()
    {
        if (_failed)
            return;

        try
        {
            if (_root == null)
            {
                if (!TryBuild())
                    return;
            }

            if (_root == null)
                return;

            UpdateVisibility();
            if (_lastVisible)
                Refresh();
        }
        catch (Exception error)
        {
            _failed = true;
            Diagnostics = $"原生 body 创建失败，已回退到插件面板：{error.Message}";
            Plugin.LogSource.LogError($"项目 body：{Diagnostics}");
        }
    }

    /// <summary>
    /// The body is shown whenever the plugin tool is selected and hidden otherwise - deliberately
    /// <b>not</b> following the mouse.
    ///
    /// Two attempts at the toolbar's own behaviour were rolled back on the user's request
    /// (2026-09-23): mirroring the hover/lock rule, and then handing the body to the game by naming
    /// it <c>Body</c> and registering a holder. Both made the panel hard to use, because a fading
    /// body sets <c>blocksRaycasts = false</c>: it disappeared the moment the pointer left the
    /// toolbar button, so the pointer could never travel into the panel to click a slot.
    /// </summary>
    private static void UpdateVisibility()
    {
        if (_root == null)
            return;

        var visible = CustomToolbarTool.IsSelected;
        if (_visibilityKnown && visible == _lastVisible)
            return;

        _visibilityKnown = true;
        _lastVisible = visible;
        SetVisible(visible);
        Plugin.LogSource.LogInfo(
            $"项目 body：{(visible ? "显示" : "隐藏")}（工具选中={visible}，本版本不跟随鼠标；"
            + $"焦点画布={SafeFocusCanvas()}，谱面={SafeChartPath()}，槽位={ProjectSlots.Slots.Count}/{ProjectSlotList.Capacity}，"
            + $"状态行='{_status}'）");
    }

    /// <summary>CanvasGroup is what the game fades; without one, fall back to SetActive.</summary>
    private static void SetVisible(bool visible)
    {
        try
        {
            var group = CustomToolbarTool.FindComponent<CanvasGroup>(_root!.transform);
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
                _root.SetActive(true); // native bodies stay active and only fade
                return;
            }

            _root.SetActive(visible);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：切换显隐失败：{error.GetType().Name}");
        }
    }

    internal static void Refresh()
    {
        try
        {
            var slots = ProjectSlots.Slots;
            var current = ProjectInformation.instance?.filePath ?? string.Empty;
            for (var index = 0; index < SlotSetters.Count; index++)
            {
                var setter = SlotSetters[index];
                setter?.Invoke(index < slots.Count
                    ? SlotText(index, slots[index], current)
                    : $"{index + 1}. （空）");
            }

            _pickSetter?.Invoke("选择文件…");

            // The panel owns both texts: nothing outside this class writes them, and re-asserting every
            // frame keeps a foreign write (or a stale value) from sticking.
            if (_titleNode != null)
                SetAllTexts(_titleNode, TitleText, "Title", Color.white);
            _statusSetter?.Invoke(_status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：刷新文字失败：{error.Message}");
        }
    }

    /// <summary>
    /// The project panel's own status line. It is deliberately independent of
    /// <c>SpeedEasePanel.ShowStatus</c>: that one carries every plugin feature's message (easing,
    /// clipboard, zoom, …), and mirroring it here made the project body show those messages instead of
    /// its own (2026-10-15 bug report).
    /// </summary>
    internal static void SetStatusText(string status)
    {
        var previous = _status;
        _status = status ?? string.Empty;
        if (previous != _status)
            Plugin.LogSource.LogInfo($"项目 body：状态行 '{previous}' -> '{_status}'");
        try
        {
            _statusSetter?.Invoke(_status);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：写入状态行失败：{error.Message}");
        }
    }

    private static string SafeFocusCanvas()
    {
        try
        {
            return ProjectInformation.SelectedCanvas.ToString();
        }
        catch (Exception error)
        {
            return $"<不可用：{error.GetType().Name}>";
        }
    }

    private static string SafeChartPath()
    {
        try
        {
            var path = ProjectInformation.instance?.filePath;
            return string.IsNullOrEmpty(path) ? "<未保存>" : path;
        }
        catch (Exception error)
        {
            return $"<不可用：{error.GetType().Name}>";
        }
    }

    private static string SlotText(int index, ProjectSlot slot, string currentChart)
    {
        var isCurrent = string.Equals(slot.ChartPath, currentChart, StringComparison.OrdinalIgnoreCase);
        var song = string.IsNullOrEmpty(slot.SongPath) ? string.Empty : $" + {Path.GetFileName(slot.SongPath)}";
        return $"{(isCurrent ? "▶ " : string.Empty)}{index + 1}. {Path.GetFileName(slot.ChartPath)}{song}";
    }

    private static bool TryBuild()
    {
        var button = CustomToolbarTool.Button;
        if (button == null)
            return false;

        var bodySource = FindEditCanvasBody();
        if (bodySource == null)
        {
            Diagnostics = "找不到可克隆的工具 body（Edit Canvas/Body），回退到插件面板。";
            Plugin.LogSource.LogWarning($"项目 body：{Diagnostics}");
            return false;
        }

        var buttonSource = bodySource.Find("DeleteCanvas");
        var labelSource = bodySource.Find("Canvas Title");
        if (buttonSource == null)
        {
            Diagnostics = "克隆源按钮（Edit Canvas/Body/DeleteCanvas）不存在，回退到插件面板。";
            Plugin.LogSource.LogWarning($"项目 body：{Diagnostics}");
            return false;
        }

        var parent = button.transform;
        var existing = parent.Find(BodyObjectName);
        if (existing != null)
        {
            Plugin.LogSource.LogInfo("项目 body：已存在同名 body，先销毁重建。");
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        var root = UnityEngine.Object.Instantiate(bodySource.gameObject, parent);
        root.name = BodyObjectName;

        // The clone must not carry the source tool's controls, nor any body holder that would
        // register it to the original button.
        DestroyChildrenExcept(root.transform, "Title", "Lock");
        StripBodyHolders(root);

        // The source body is only as large as the source tool's controls (205x85), which would
        // leave our rows outside the panel.
        ResizeBody(root);

        Plugin.LogSource.LogInfo(
            $"项目 body：容器组件 {Describe(root)}；rect {Geometry(root.transform)}，alpha={AlphaOf(root)}，active={root.activeSelf}");
        Plugin.LogSource.LogInfo(
            $"项目 body：源按钮组件 {Describe(buttonSource.gameObject)}；rect {Geometry(buttonSource)}");

        // Title bar: a fixed caption ("项目"). The source bodies stretch their title box with
        // anchors (sizeDelta.x is 0), so the box is anchored to the panel's top-left with an
        // explicit size taken from layout.json - copying the reference's sizeDelta would keep it
        // stretched across the whole panel.
        var titleNode = root.transform.Find("Title");
        _titleNode = titleNode;
        if (titleNode != null)
        {
            SetAllTexts(titleNode, TitleText, "Title", Color.white);
            ApplyTitleBox(titleNode);

            // Native bodies draw the title bar above their content; the rows are added later, so
            // without this the title ends up underneath them.
            titleNode.SetAsLastSibling();
        }

        // Three project slots, the picker and a status line, all copies of the same native button.
        var rowWidth = Math.Max(60f, PluginLayout.PanelWidth - PluginLayout.LeftMargin - PluginLayout.RightMargin);
        SlotSetters.Clear();
        for (var index = 0; index < ProjectSlotList.Capacity; index++)
        {
            var itemIndex = index;
            var item = UnityEngine.Object.Instantiate(buttonSource.gameObject, root.transform);
            item.name = $"Slot{index + 1}";
            Place(item, PluginLayout.LeftMargin, -PluginLayout.FirstRowY - index * PluginLayout.RowHeight, rowWidth, PluginLayout.RowHeight - 2f);
            SlotSetters.Add(MakeSetter(item, item.name));
            Wire(item, () => ActivateSlot(itemIndex));
        }

        var pick = UnityEngine.Object.Instantiate(buttonSource.gameObject, root.transform);
        pick.name = "PickFiles";
        Place(pick, PluginLayout.LeftMargin, -PluginLayout.FirstRowY - ProjectSlotList.Capacity * PluginLayout.RowHeight, rowWidth, PluginLayout.RowHeight - 2f);
        _pickSetter = MakeSetter(pick, pick.name);
        Wire(pick, () => ShowStatus(ProjectSwitcher.PickFiles()));

        if (labelSource != null)
        {
            var status = UnityEngine.Object.Instantiate(labelSource.gameObject, root.transform);
            status.name = "Status";
            Place(
                status,
                PluginLayout.LeftMargin,
                -PluginLayout.FirstRowY - (ProjectSlotList.Capacity + 1) * PluginLayout.RowHeight,
                rowWidth,
                PluginLayout.RowHeight + PluginLayout.StatusExtraHeight);
            _statusSetter = MakeSetter(status, status.name, allowWrap: true, maxChars: PluginLayout.StatusMaxChars);
            _statusSetter?.Invoke(ProjectSwitcher.Status);
        }

        _root = root;
        Diagnostics = "原生 body 已创建";
        Refresh();
        LogLayout(root);
        Plugin.LogSource.LogInfo(
            $"项目 body：已创建（{root.transform.childCount} 个子物体），父物体 '{parent.name}'");
        return true;
    }

    /// <summary>The cloned panel keeps the source body's size; grow it so the rows fit inside.</summary>
    private static void ResizeBody(GameObject root)
    {
        var rect = Rect(root.transform);
        if (rect == null)
        {
            Plugin.LogSource.LogWarning("项目 body：容器取不到 RectTransform，尺寸未调整。");
            return;
        }

        var before = $"{rect.sizeDelta.x:0.#}x{rect.sizeDelta.y:0.#}";
        rect.sizeDelta = new Vector2(PluginLayout.PanelWidth, PluginLayout.PanelHeight);

        // The plugin entry is the last toolbar button, so the panel has to grow upwards from it;
        // the source body grows downwards because that tool sits higher in the list. Both the
        // anchor/offset and the pivot live in layout.json so the position can be tuned by hand.
        rect.anchorMin = new Vector2(PluginLayout.AnchorX, PluginLayout.AnchorY);
        rect.anchorMax = new Vector2(PluginLayout.AnchorX, PluginLayout.AnchorY);
        rect.pivot = new Vector2(PluginLayout.PivotX, PluginLayout.PivotY);
        rect.anchoredPosition = new Vector2(PluginLayout.OffsetX, PluginLayout.OffsetY);
        Plugin.LogSource.LogInfo(
            $"项目 body：尺寸 {before} -> {PluginLayout.PanelWidth:0}x{PluginLayout.PanelHeight:0}；{PluginLayout.Describe()}");
    }

    private static string AlphaOf(GameObject root)
    {
        var value = AlphaValue(root);
        return value < 0f ? "<无 CanvasGroup>" : value.ToString("0.##");
    }

    /// <summary>Numeric alpha of the body's CanvasGroup, or -1 when it has none.</summary>
    private static float AlphaValue(GameObject root)
    {
        try
        {
            var group = CustomToolbarTool.FindComponent<CanvasGroup>(root.transform);
            return group == null ? -1f : group.alpha;
        }
        catch
        {
            return -1f;
        }
    }

    /// <summary>Every child's final geometry, so an invisible row can be diagnosed from the log.</summary>
    private static void LogLayout(GameObject root)
    {
        for (var index = 0; index < root.transform.childCount; index++)
        {
            var child = root.transform.GetChild(index);
            Plugin.LogSource.LogInfo(
                $"项目 body：  子物体 '{child.name}' rect {Geometry(child)} active={child.gameObject.activeSelf}" +
                $"，alpha={AlphaOf(child.gameObject)}");
        }
    }

    /// <summary>
    /// Pins the title bar to the panel's top-left with an explicit size so the box hugs "项目".
    /// The Publish body's title is logged alongside as the reference the user asked for; its own
    /// sizeDelta is 0 wide because it stretches, which is why it cannot be copied directly.
    /// </summary>
    private static void ApplyTitleBox(Transform titleNode)
    {
        try
        {
            var own = Rect(titleNode);
            if (own == null)
            {
                Plugin.LogSource.LogWarning("项目 body：标题栏取不到 RectTransform，尺寸未调整。");
                return;
            }

            var reference = FindBodyChild("Publish", "Title");
            var before = Geometry(titleNode);

            // Either an exact width (TitleWidth > 0) or "fit the caption plus padding"; the two are
            // never combined, so both knobs in layout.json have a visible effect.
            var textWidth = MeasureTextWidth(titleNode);
            var width = PluginLayout.TitleWidth > 0
                ? PluginLayout.TitleWidth
                : textWidth + PluginLayout.TitlePaddingX;
            own.anchorMin = new Vector2(0f, 1f);
            own.anchorMax = new Vector2(0f, 1f);
            own.pivot = new Vector2(0f, 1f);
            own.sizeDelta = new Vector2(width, PluginLayout.TitleHeight);
            own.anchoredPosition = new Vector2(PluginLayout.TitleX, PluginLayout.TitleY);
            Plugin.LogSource.LogInfo(
                $"项目 body：标题栏 {before} -> {Geometry(titleNode)}" +
                $"（{(PluginLayout.TitleWidth > 0 ? $"固定宽 {PluginLayout.TitleWidth:0}" : $"文字宽 {textWidth:0.#} + 留白 {PluginLayout.TitlePaddingX:0}")}；" +
                $"参照 发布/Body/Title {(reference == null ? "<未找到>" : Geometry(reference))}）");

            // The caption and its background are children of the title box; they were laid out for
            // the source body's own (stretched) box, so they are stretched to fill this one instead
            // of drifting into the content area below.
            StretchChildren(titleNode);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：调整标题栏失败：{error.GetType().Name}");
        }
    }

    /// <summary>Widest text in a subtree, used to size the title box around its caption.</summary>
    private static float MeasureTextWidth(Transform root)
    {
        var width = 0f;
        try
        {
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var tmp = CustomToolbarTool.FindComponent<TMPro.TMP_Text>(node);
                if (tmp != null)
                    width = Math.Max(width, tmp.preferredWidth);

                var text = CustomToolbarTool.FindComponent<Text>(node);
                if (text != null)
                    width = Math.Max(width, text.preferredWidth);

                for (var index = 0; index < node.childCount; index++)
                    stack.Push(node.GetChild(index));
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：测量标题文字宽度失败：{error.GetType().Name}");
        }

        return width;
    }

    /// <summary>Makes every direct child fill the given node, so a resized header keeps its content.</summary>
    private static void StretchChildren(Transform parent)
    {
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            var rect = Rect(child);
            if (rect == null)
                continue;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            Plugin.LogSource.LogInfo($"项目 body：标题栏子物体 '{child.name}' 拉伸填满标题框 {Geometry(child)}");
        }
    }

    /// <summary>
    /// Writes a caption into every text component of a subtree. A title node can hold more than one
    /// (the visible label lives on a child), and setting only the first leaves the old caption on
    /// screen - that is what made the plugin body look like a second title under "编辑画布".
    ///
    /// This runs every frame (the panel re-asserts its own texts), so it only logs when a caption or a
    /// colour actually changes - logging every write flooded the log with hundreds of identical lines
    /// (2026-10-15 report).
    /// </summary>
    private static void SetAllTexts(Transform root, string value, string label, Color color)
    {
        try
        {
            var stack = new Stack<Transform>();
            stack.Push(root);
            var changed = 0;
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var tmp = CustomToolbarTool.FindComponent<TMPro.TMP_Text>(node);
                if (tmp != null)
                {
                    if (tmp.text != value || tmp.color != color)
                        Plugin.LogSource.LogInfo($"项目 body：{label} 文本（{node.name}，TMP）'{tmp.text}' -> '{value}'，颜色 {color}");
                    tmp.color = color;
                    tmp.text = value;
                    changed++;
                }

                var text = CustomToolbarTool.FindComponent<Text>(node);
                if (text != null)
                {
                    if (text.text != value || text.color != color)
                        Plugin.LogSource.LogInfo($"项目 body：{label} 文本（{node.name}，Text）'{text.text}' -> '{value}'，颜色 {color}");
                    text.color = color;
                    text.text = value;
                    changed++;
                }

                for (var index = 0; index < node.childCount; index++)
                    stack.Push(node.GetChild(index));
            }

            if (changed == 0)
                Plugin.LogSource.LogWarning($"项目 body：{label} 子树里没有文本组件，标题无法修改。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：改写 {label} 文本失败：{error.GetType().Name}");
        }
    }

    /// <summary>Looks up a child inside another toolbar button's body (e.g. Publish/Body/Title).</summary>
    private static Transform? FindBodyChild(string buttonName, string childName)
    {
        try
        {
            var buttons = UnityEngine.Object.FindObjectsOfType<ToolbarToolButton>(true);
            if (buttons == null)
                return null;
            foreach (var candidate in buttons)
            {
                if (candidate == null || candidate.name != buttonName)
                    continue;
                var body = candidate.transform.Find("Body");
                return body?.Find(childName);
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：查找 {buttonName}/Body/{childName} 失败：{error.GetType().Name}");
        }

        return null;
    }

    private static Transform? FindEditCanvasBody()
    {
        try
        {
            var buttons = UnityEngine.Object.FindObjectsOfType<ToolbarToolButton>(true);
            if (buttons == null)
                return null;
            foreach (var candidate in buttons)
            {
                if (candidate != null && candidate.name == "Edit Canvas")
                    return candidate.transform.Find("Body");
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"项目 body：查找 Edit Canvas 失败：{error}");
        }

        return null;
    }

    private static void ActivateSlot(int index)
    {
        var slots = ProjectSlots.Slots;
        if (index < 0 || index >= slots.Count)
        {
            ShowStatus($"{index + 1}. 槽位为空：先用「选择文件…」打开一个工程，它会被自动记住。");
            return;
        }

        if (ProjectSwitcher.IsBusy)
        {
            ShowStatus("正在切换中，请稍候…");
            return;
        }

        ShowStatus(ProjectSwitcher.SwitchTo(slots[index].ChartPath, slots[index].SongPath));
    }

    private static void ShowStatus(string status)
    {
        // UI message only: it belongs in the panel, not in the log.
        SetStatusText(status);
    }

    /// <summary>
    /// A cloned button keeps its serialized listeners, which still point at the original component;
    /// our own actions are attached through the interop delegate helper instead.
    /// </summary>
    private static void Wire(GameObject item, Action action)
    {
        try
        {
            var button = CustomToolbarTool.FindComponent<Button>(item.transform);
            if (button == null)
            {
                Plugin.LogSource.LogWarning($"项目 body：'{item.name}' 没有 Button 组件，点击不会生效。");
                return;
            }

            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"项目 body：给 '{item.name}' 挂点击回调失败：{error}");
        }
    }

    /// <summary>
    /// Label setter for a cloned widget; supports both uGUI Text and TextMeshPro. The cloned
    /// button comes from the source tool's "DeleteCanvas", whose label is red, so the colour is
    /// forced to black, best-fit is enabled (a long name shrinks instead of spilling out) and the
    /// text is cut at the configured length.
    /// </summary>
    private static Action<string>? MakeSetter(GameObject item, string label, bool allowWrap = false, int? maxChars = null)
    {
        try
        {
            var limit = maxChars ?? PluginLayout.MaxLabelChars;
            var tmp = CustomToolbarTool.FindComponent<TMPro.TMP_Text>(item.transform);
            if (tmp != null)
            {
                tmp.color = Color.black;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 8f;
                tmp.fontSizeMax = 22f;
                if (allowWrap)
                    tmp.enableWordWrapping = true;
                Plugin.LogSource.LogInfo($"项目 body：'{label}' 的文本是 TextMeshPro（黑色 + 自动缩放）。");
                return value => tmp.text = Trim(value, limit);
            }

            var text = CustomToolbarTool.FindComponent<Text>(item.transform);
            if (text != null)
            {
                text.color = Color.black;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 8;
                text.resizeTextMaxSize = 22;
                text.horizontalOverflow = allowWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                Plugin.LogSource.LogInfo($"项目 body：'{label}' 的文本是 uGUI Text（黑色 + 自动缩放）。");
                return value => text.text = Trim(value, limit);
            }

            Plugin.LogSource.LogWarning($"项目 body：'{label}' 里找不到文本组件，标签不会更新。");
            return null;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：查找 '{label}' 的文本失败：{error.GetType().Name}");
            return null;
        }
    }

    /// <summary>Keeps labels inside their box; the limits come from layout.json.</summary>
    private static string Trim(string? value, int? limit = null)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var max = Math.Max(8, limit ?? PluginLayout.MaxLabelChars);
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    /// <summary>
    /// Il2CppInterop can hand out a base Transform proxy for a RectTransform, so the cast must go
    /// through TryCast; a plain <c>as RectTransform</c> silently returns null.
    /// </summary>
    private static RectTransform? Rect(Transform node)
    {
        try
        {
            return CustomToolbarTool.FindComponent<RectTransform>(node);
        }
        catch
        {
            return null;
        }
    }

    private static Vector2? AnchorOf(Transform node)
    {
        var rect = Rect(node);
        return rect == null ? null : rect.anchoredPosition;
    }

    private static void Place(GameObject item, float x, float y, float width, float height)
    {
        var rect = Rect(item.transform);
        if (rect == null)
        {
            Plugin.LogSource.LogWarning($"项目 body：'{item.name}' 取不到 RectTransform，位置未调整。");
            return;
        }

        // Anchor to the body's top-left corner so the rows stack downwards inside the panel,
        // whatever anchors the cloned button originally had.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static string Geometry(Transform node)
    {
        var rect = Rect(node);
        if (rect == null)
            return "<没有 RectTransform>";
        return $"{rect.sizeDelta.x:0.#}x{rect.sizeDelta.y:0.#} @({rect.anchoredPosition.x:0.#},{rect.anchoredPosition.y:0.#}) " +
            $"anchor={rect.anchorMin}..{rect.anchorMax} pivot={rect.pivot}";
    }

    private static void DestroyChildrenExcept(Transform parent, params string[] keep)
    {
        for (var index = parent.childCount - 1; index >= 0; index--)
        {
            var child = parent.GetChild(index);
            if (child == null)
                continue;
            var name = child.name;
            var keepIt = false;
            foreach (var allowed in keep)
            {
                if (name == allowed)
                    keepIt = true;
            }

            if (!keepIt)
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// <summary>Removes ToolbarBodyHolder from the clone so it cannot register to the source button.</summary>
    private static void StripBodyHolders(GameObject root)
    {
        try
        {
            var targets = new List<GameObject> { root };
            for (var index = 0; index < root.transform.childCount; index++)
                targets.Add(root.transform.GetChild(index).gameObject);

            var removed = 0;
            foreach (var target in targets)
            {
                var components = target.GetComponents<Component>();
                if (components == null)
                    continue;
                foreach (var component in components)
                {
                    if (component == null)
                        continue;
                    var holder = component.TryCast<ToolbarBodyHolder>();
                    if (holder == null)
                        continue;
                    UnityEngine.Object.Destroy(holder);
                    removed++;
                }
            }

            if (removed > 0)
                Plugin.LogSource.LogInfo($"项目 body：已移除克隆体上的 {removed} 个 ToolbarBodyHolder。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"项目 body：清理 body holder 失败：{error.Message}");
        }
    }

    private static string Describe(GameObject target)
    {
        try
        {
            var components = target.GetComponents<Component>();
            if (components == null)
                return "<none>";
            var names = new List<string>();
            foreach (var component in components)
                names.Add(component == null ? "<null>" : component.GetType().Name);
            return string.Join(", ", names);
        }
        catch (Exception error)
        {
            return $"<读取失败 {error.GetType().Name}>";
        }
    }
}
