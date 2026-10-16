using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// Gives the plugin its own toolbar entry by cloning an existing button at runtime. The editor
/// exposes no extension point for tools (a search of every toolbar type found no registry, only
/// the static <see cref="ToolbarToolButton.activeTool"/> self-reference), so the only supported
/// way in is to build a button that looks and behaves like the original ones.
///
/// The cursor button is the clone source: it is the only button with no toolObject and no Body
/// child (confirmed by the toolbar dump recorded in docs/DECISIONS.md D-021), so it carries
/// exactly the parts a plugin-owned tool needs - Icon, Title, CanvasGroup, Button - and none of
/// the original tool bodies. The clone is placed after the last original button and bound to
/// key "9" (the original tools use 1-8).
///
/// Everything here is runtime-only: nothing is written to the game files, and the entry
/// disappears when the editor closes. See docs/DECISIONS.md D-022.
/// </summary>
internal static class CustomToolbarTool
{
    internal const string ButtonName = "RhythmEditorTool";
    internal const string ButtonTitle = "项目";
    internal const KeyCode Shortcut = KeyCode.Alpha9;

    private const float RetryIntervalSeconds = 2f;

    private static ToolbarToolButton? _button;
    private static float _nextAttemptAt;
    private static string _diagnostics = "等待编辑器工具栏出现…";

    internal static ToolbarToolButton? Button => _button;

    /// <summary>The diagnostics line shown in the plugin panel, so the wiring is visible in game.</summary>
    internal static string Diagnostics => _diagnostics;

    internal static bool IsSelected
    {
        get
        {
            try
            {
                return _button != null && SpeedEasePanel.ActiveToolButton == _button;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Creates the entry once the editor toolbar exists. Safe to call every frame: creation is
    /// skipped while the button is alive, and a destroyed button (scene reload) is rebuilt.
    /// </summary>
    internal static void Tick()
    {
        try
        {
            if (_button != null)
            {
                HandleShortcut();
                return;
            }

            if (Time.unscaledTime < _nextAttemptAt)
                return;
            _nextAttemptAt = Time.unscaledTime + RetryIntervalSeconds;
            TryCreate();
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"自定义工具按钮：创建失败：{error}");
        }
    }

    /// <summary>
    /// Key 9 is handled by the plugin rather than by the original toolbar. The clone is created
    /// after the toolbar's own start-up, so any key-to-button mapping the toolbar builds at that
    /// moment cannot know about it: the 2026-09-23 run (working copy, 16:05) recorded a 9 press
    /// that selected nothing until the button was clicked with the mouse. Selecting through the
    /// original SelectTool keeps every downstream effect identical to a mouse click.
    ///
    /// A focused value box also swallows the press first (2026-09-23 bug report: typing 9 into a
    /// speed box jumped to the plugin tool). The original buttons skip their shortcut while one of
    /// their <c>inputFields</c> is focused; the plugin checks the same state the rest of the plugin
    /// already uses for its other shortcuts, <see cref="EditorNavigation.TextInputActive"/>.
    /// </summary>
    private static void HandleShortcut()
    {
        try
        {
            if (!Input.GetKeyDown(Shortcut))
                return;

            if (EditorNavigation.TextInputActive())
            {
                Plugin.LogSource.LogInfo($"自定义工具按钮：正在编辑输入框，已忽略快捷键 {Shortcut}。");
                return;
            }

            var button = _button;
            if (button == null || IsSelected)
                return;

            button.SelectTool();
            Plugin.LogSource.LogInfo($"自定义工具按钮：按 {Shortcut} 选中插件工具（快捷键由插件自行处理）。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"自定义工具按钮：快捷键选择失败：{error}");
        }
    }

    private static void TryCreate()
    {
        var buttons = UnityEngine.Object.FindObjectsOfType<ToolbarToolButton>(true);
        if (buttons == null || buttons.Length == 0)
            return;

        foreach (var candidate in buttons)
        {
            if (candidate != null && candidate.name == ButtonName)
            {
                Adopt(candidate, "接管已存在的按钮");
                return;
            }
        }

        var source = FindCursorButton(buttons);
        if (source == null)
        {
            _diagnostics = "找不到光标按钮，无法克隆。";
            Plugin.LogSource.LogError("自定义工具按钮：工具栏里找不到「无 toolObject 且无 Body」的光标按钮。");
            return;
        }

        var parent = source.transform.parent;
        var clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
        clone.name = ButtonName;
        clone.transform.SetSiblingIndex(parent.childCount - 1);

        var button = clone.GetComponent<ToolbarToolButton>();
        if (button == null)
        {
            _diagnostics = "克隆体缺少 ToolbarToolButton。";
            Plugin.LogSource.LogError("自定义工具按钮：克隆体上没有 ToolbarToolButton。");
            return;
        }

        button.keyCode = Shortcut;
        button.toolObject = null;
        button.locked = false;

        var titleChanged = SetTitleText(clone, ButtonTitle);
        var icon = DescribeIcon(clone);
        var sourceComponents = DescribeComponents(source.gameObject);
        var cloneComponents = DescribeComponents(clone);

        try
        {
            button.Start();
        }
        catch (Exception startError)
        {
            Plugin.LogSource.LogError($"自定义工具按钮：克隆体 Start() 抛错：{startError}");
        }

        Adopt(button, "新建克隆按钮");

        Plugin.LogSource.LogInfo(
            $"自定义工具按钮：已创建 '{ButtonName}'（标题{(titleChanged ? "已改" : "未改")}，快捷键 {Shortcut}，" +
            $"sibling={clone.transform.GetSiblingIndex()}/{parent.childCount - 1}，图标 {icon}）");
        Plugin.LogSource.LogInfo($"自定义工具按钮：光标按钮组件 {sourceComponents}");
        Plugin.LogSource.LogInfo($"自定义工具按钮：克隆体组件 {cloneComponents}");
    }

    private static void Adopt(ToolbarToolButton button, string how)
    {
        _button = button;
        _diagnostics = $"工具栏入口『{ButtonTitle}』就绪（快捷键 9）。";
        Plugin.LogSource.LogInfo($"自定义工具按钮：{how}，name='{button.name}'，keyCode={button.keyCode}");
        VerifySelection();
    }

    /// <summary>
    /// Proves the clone really is selectable through the game's own path instead of assuming it.
    /// The toolbar buttons carry no <c>UnityEngine.UI.Button</c> at all - mouse selection lives in
    /// the original <c>ToolbarToolButton.Update</c>, which the clone runs on its own - so there is
    /// nothing to wire up here; this only checks that selecting it lands on the plugin tool, and
    /// then puts the editor back on the previous tool so startup does not steal the selection.
    /// </summary>
    private static void VerifySelection()
    {
        var button = _button;
        if (button == null)
            return;

        var previous = SpeedEasePanel.ActiveToolButton;
        try
        {
            var before = previous == null ? "<null>" : previous.name;
            button.SelectTool();
            var hit = SpeedEasePanel.ActiveToolButton == button;
            Plugin.LogSource.LogInfo(
                $"自定义工具按钮：选择自检（SelectTool 前 activeTool={before}）命中克隆体={hit}。");
            if (!hit)
                _diagnostics = "选择自检未通过，请把日志发给维护者。";
        }
        catch (Exception error)
        {
            _diagnostics = "选择自检抛错，请查看日志。";
            Plugin.LogSource.LogError($"自定义工具按钮：选择自检失败：{error}");
        }
        finally
        {
            RestoreSelection(previous);
        }
    }

    private static void RestoreSelection(ToolbarToolButton? previous)
    {
        try
        {
            if (previous == null || previous == _button)
                return;
            if (SpeedEasePanel.ActiveToolButton == previous)
                return;
            previous.SelectTool();
            Plugin.LogSource.LogInfo($"自定义工具按钮：自检后已恢复原工具 '{previous.name}'。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"自定义工具按钮：恢复原工具失败：{error}");
        }
    }

    /// <summary>
    /// The cursor button (the only entry with neither a toolObject nor a Body child) is the clone
    /// source; the same structural rule the panel routing uses in docs/DECISIONS.md D-021.
    /// </summary>
    private static ToolbarToolButton? FindCursorButton(ToolbarToolButton[] buttons)
    {
        foreach (var candidate in buttons)
        {
            if (candidate == null || candidate.toolObject != null)
                continue;
            if (candidate.transform.Find("Body") == null)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Renames the cloned entry. Which component carries the title text is not known ahead of
    /// time, so both candidates are tried; the hit is logged as evidence. Lookups go through the
    /// non-generic Type overloads because the generic GetComponentInChildren&lt;T&gt;(bool) throws
    /// VerificationException in this interop build (see the failure record in D-022).
    /// </summary>
    private static bool SetTitleText(GameObject clone, string text)
    {
        try
        {
            var title = clone.transform.Find("Title");
            if (title == null)
            {
                Plugin.LogSource.LogError("自定义工具按钮：克隆体里找不到 Title 子物体。");
                return false;
            }

            // A title node can carry more than one text component (the visible label may sit on a
            // child), and only the first one used to be written - which is why the toolbar tooltip
            // kept showing the clone source's title ("光标"). Write them all.
            var changed = 0;
            var stack = new Stack<Transform>();
            stack.Push(title);
            while (stack.Count > 0)
            {
                var node = stack.Pop();

                var tmp = FindComponent<TMPro.TextMeshProUGUI>(node);
                if (tmp != null)
                {
                    Plugin.LogSource.LogInfo($"自定义工具按钮：标题文本（{node.name}，TMP）'{tmp.text}' -> '{text}'");
                    tmp.text = text;
                    changed++;
                }

                var ugui = FindComponent<Text>(node);
                if (ugui != null)
                {
                    Plugin.LogSource.LogInfo($"自定义工具按钮：标题文本（{node.name}，uGUI Text）'{ugui.text}' -> '{text}'");
                    ugui.text = text;
                    changed++;
                }

                for (var index = 0; index < node.childCount; index++)
                    stack.Push(node.GetChild(index));
            }

            if (changed > 0)
            {
                Plugin.LogSource.LogInfo($"自定义工具按钮：Title 子树共改写 {changed} 个文本组件。");
                return true;
            }

            Plugin.LogSource.LogError(
                $"自定义工具按钮：Title 下没有 TextMeshProUGUI / Text。Title 下的组件：{DescribeComponents(title.gameObject)}");
            return false;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"自定义工具按钮：设置标题失败：{error}");
            return false;
        }
    }

    /// <summary>Logs where the cloned icon comes from, so a dedicated sprite can be chosen later.</summary>
    private static string DescribeIcon(GameObject clone)
    {
        try
        {
            var icon = clone.transform.Find("Icon");
            if (icon == null)
                return "<无 Icon>";

            var image = FindComponent<Image>(icon);
            if (image == null)
                return $"<Icon 下无 Image；组件：{DescribeComponents(icon.gameObject)}>";

            var sprite = image.sprite;
            return sprite == null ? "<无 sprite>" : $"{sprite.name}（Image.sprite）";
        }
        catch (Exception error)
        {
            return $"<读取失败 {error.GetType().Name}>";
        }
    }

    /// <summary>
    /// Component lookup inside a subtree.
    /// </summary>
    /// <remarks>
    /// Neither generic route is usable here: <c>GetComponentInChildren&lt;T&gt;(bool)</c> throws
    /// <c>VerificationException</c> at runtime, and the non-generic <c>GetComponentInChildren(Type, bool)</c>
    /// needs an <c>Il2CppSystem.Type</c> that managed code cannot build from <c>typeof</c>. So the
    /// subtree is walked with the proven <c>GetComponents&lt;Component&gt;()</c> call and each
    /// component is probed with <c>TryCast</c>. Evidence and the failure record are in D-022.
    /// </remarks>
    internal static T? FindComponent<T>(Transform root) where T : Il2CppObjectBase
    {
        try
        {
            var pending = new Stack<Transform>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                var components = current.GetComponents<Component>();
                if (components != null)
                {
                    foreach (var component in components)
                    {
                        if (component == null)
                            continue;
                        var match = component.TryCast<T>();
                        if (match != null)
                            return match;
                    }
                }

                for (var index = 0; index < current.childCount; index++)
                    pending.Push(current.GetChild(index));
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"查找 {typeof(T).Name} 失败：{error.GetType().Name}");
        }

        return null;
    }

    /// <summary>Component type names of a GameObject, used as evidence in the log.</summary>
    internal static string DescribeComponents(GameObject target)
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
