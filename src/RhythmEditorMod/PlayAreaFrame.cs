using UnityEngine;

namespace RhythmEditorMod;

/// <summary>
/// Draws the "play area" frame - the rectangle that the original editor would leave uncovered, i.e.
/// what the player actually sees during preview playback (2026-09-24 request, D-028).
///
/// Geometry (all **fixed world values**, so the whole frame scales with the plugin's uniform zoom and
/// follows window/projection changes):
/// <list type="bullet">
/// <item><description>left / right - the **outer** edges of <c>LeftSide</c> / <c>RightSide</c>
/// (world ±2.813). They project to the same band as <c>blackMask</c>'s two UI bars at 100 % zoom, but
/// unlike those screen-space bars they shrink with the zoom, which is what the frame must do.</description></item>
/// <item><description>bottom - the top edge of <c>BottomSideForward</c> (world −2.9).
/// <c>BottomSideBack</c> is deliberately excluded: its upper edge (−1.9) is one unit too high.</description></item>
/// <item><description>top - the **outer** edge of <c>TopSide</c> (world +4.1), matching the outer-edge
/// rule used for left/right. Verified by the user against three earlier candidates.</description></item>
/// </list>
///
/// Colour: <c>themes[inChallengeTime ? 1 : 0].colorsList[2]</c> - the chart's UI colour, documented in
/// docs/CHART_FORMAT.md - so it follows the normal/Riztime theme by itself. Its channels are **0-255**
/// (verified: <c>themes[0].colorsList[0]</c> equals the measured background colour 190,185,204), hence
/// the division by 255 before the value reaches <c>GUI.color</c>.
///
/// Drawn when either of these holds:
/// <list type="bullet">
/// <item><description><c>预览蒙版：关</c> **and** playback with preview is running (the case the frame
/// exists for: the masking is suppressed and the frame shows what was hidden), or</description></item>
/// <item><description>the overall zoom is not 100 % (the frame is useful while looking at an
/// unzoomed-out view).</description></item>
/// </list>
///
/// The border is 2 px and does <b>not</b> scale with the zoom. <c>GUI.DrawTexture</c> is stripped from
/// this build ("Method unstripping failed"), so the border uses a solid <c>GUIStyle</c> (a 1x1 white
/// texture created by the plugin) through <c>GUI.Box</c>, with the plain <c>GUI.Box</c> the panel
/// already relies on as the fallback.
/// </summary>
internal static class PlayAreaFrame
{
    private const float BorderPixels = 2f;

    private static readonly string[] TopCovers = { "TopSide" };

    private static readonly string[] LeftCovers = { "LeftSide" };

    private static readonly string[] RightCovers = { "RightSide" };

    private static readonly string[] BottomCovers = { "BottomSideForward" };

    private static Texture2D? _texture;
    private static GUIStyle? _style;
    private static bool _styleUnavailable;
    private static bool _loggedStyleFailure;
    private static bool _loggedDrawFailure;
    private static bool _drawing;
    private static string _lastRect = "<未记录>";
    private static string _lastDrawingReason = "<未记录>";

    /// <summary>
    /// Called from the panel's <c>OnGUI</c> before its visibility check, so the frame is independent
    /// of whether the plugin panel is shown.
    /// </summary>
    internal static void Draw()
    {
        try
        {
            if (!ShouldDraw() || !TryGetEdges(out var left, out var right, out var bottom, out var top))
            {
                ReportStopped();
                return;
            }

            var camera = EditorCamera();
            if (camera == null)
            {
                ReportStopped();
                return;
            }

            var texture = Texture();
            if (texture == null)
            {
                ReportStopped();
                return;
            }

            var z = MaskPlaneZ();
            var leftScreen = camera.WorldToScreenPoint(new Vector3(left, 0f, z)).x;
            var rightScreen = camera.WorldToScreenPoint(new Vector3(right, 0f, z)).x;
            var bottomScreen = camera.WorldToScreenPoint(new Vector3(0f, bottom, z)).y;
            var topScreen = camera.WorldToScreenPoint(new Vector3(0f, top, z)).y;

            var x = Mathf.Min(leftScreen, rightScreen);
            var width = Mathf.Abs(rightScreen - leftScreen);
            // Unity's screen space grows upwards, IMGUI's downwards.
            var guiTop = Screen.height - topScreen;
            var guiBottom = Screen.height - bottomScreen;
            var height = Mathf.Max(1f, guiBottom - guiTop);

            var color = UiColor();
            DrawBorder(x, guiTop, width, height, texture, color);
            _lastRect = $"屏幕 x={x:0.#} y={guiTop:0.#} w={width:0.#} h={height:0.#}，世界 x[{left:0.###}, {right:0.###}] y[{bottom:0.###}, {top:0.###}]，颜色 RGB({color.r * 255f:0}, {color.g * 255f:0}, {color.b * 255f:0})";
            ReportDrawing();
        }
        catch (Exception error)
        {
            if (!_loggedDrawFailure)
            {
                _loggedDrawFailure = true;
                Plugin.LogSource.LogWarning($"预览蒙版：绘制游玩区域边框失败：{error.GetType().Name}：{error.Message}");
            }
        }
    }

    /// <summary>Suppressed masking during preview, or any zoom other than 100 %.</summary>
    private static bool ShouldDraw()
    {
        if (Math.Abs(EditorNavigation.HorizontalScale - 1f) > 0.001f)
            return true;

        return !PreviewMaskControl.Enabled && PreviewMaskControl.IsPreviewPlaybackRunning();
    }

    /// <summary>
    /// Takes the outermost edges over both <c>playMask</c> and <c>editorMask</c>, so a child missing
    /// from one of them cannot break the frame. Only <c>BottomSideForward</c> counts for the bottom.
    /// </summary>
    private static bool TryGetEdges(out float left, out float right, out float bottom, out float top)
    {
        var l = float.PositiveInfinity;
        var r = float.NegativeInfinity;
        var b = float.PositiveInfinity;
        var t = float.NegativeInfinity;

        try
        {
            var project = Project();
            Collect(project?.playMask);
            Collect(project?.editorMask);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"预览蒙版：读取游玩区域边界失败：{error.GetType().Name}");
        }

        left = l;
        right = r;
        bottom = b;
        top = t;
        return float.IsFinite(l) && float.IsFinite(r) && float.IsFinite(b) && float.IsFinite(t) && r > l && t > b;

        void Collect(GameObject? root)
        {
            if (root == null)
                return;
            Walk(root.transform, 0);

            void Walk(Transform node, int depth)
            {
                if (depth > 0)
                {
                    var name = node.name;
                    if (Contains(TopCovers, name) || Contains(LeftCovers, name) || Contains(RightCovers, name) || Contains(BottomCovers, name))
                    {
                        var renderer = CustomToolbarTool.FindComponent<Renderer>(node);
                        if (renderer != null)
                        {
                            var bounds = renderer.bounds;
                            if (Contains(TopCovers, name))
                                t = Mathf.Max(t, bounds.max.y);
                            else if (Contains(LeftCovers, name))
                                l = Mathf.Min(l, bounds.min.x);
                            else if (Contains(RightCovers, name))
                                r = Mathf.Max(r, bounds.max.x);
                            else
                                b = Mathf.Min(b, bounds.max.y);
                        }
                    }
                }

                if (depth >= 3)
                    return;
                for (var index = 0; index < node.childCount; index++)
                    Walk(node.GetChild(index), depth + 1);
            }
        }
    }

    /// <summary>The chart's UI colour for the active theme state; white when unavailable.</summary>
    private static Color UiColor()
    {
        try
        {
            var project = Project();
            var themes = project?.chart?.themes;
            if (themes == null || themes.Count == 0)
                return Color.white;

            var index = project != null && project.inChallengeTime ? 1 : 0;
            if (index >= themes.Count)
                index = 0;

            var colors = themes[index]?.colorsList;
            if (colors == null || colors.Count < 3)
                return Color.white;

            // The channels are 0-255, not 0-1 (docs/CHART_FORMAT.md, evidence M1 cross-checked
            // against the measured background colour).
            var raw = colors[2];
            return new Color(
                Mathf.Clamp01(raw.r / 255f),
                Mathf.Clamp01(raw.g / 255f),
                Mathf.Clamp01(raw.b / 255f),
                Mathf.Clamp01(raw.a / 255f));
        }
        catch (Exception error)
        {
            if (!_loggedDrawFailure)
            {
                _loggedDrawFailure = true;
                Plugin.LogSource.LogWarning($"预览蒙版：读取主题 UI 色失败：{error.GetType().Name}");
            }

            return Color.white;
        }
    }

    private static void DrawBorder(float x, float y, float width, float height, Texture2D texture, Color color)
    {
        var edges = new[]
        {
            new Rect(x, y, width, BorderPixels),
            new Rect(x, y + height - BorderPixels, width, BorderPixels),
            new Rect(x, y, BorderPixels, height),
            new Rect(x + width - BorderPixels, y, BorderPixels, height),
        };

        var style = Style(texture);
        if (style != null)
        {
            try
            {
                var previous = GUI.color;
                GUI.color = color;
                foreach (var edge in edges)
                    GUI.Box(edge, string.Empty, style);
                GUI.color = previous;
                return;
            }
            catch (Exception error)
            {
                _styleUnavailable = true;
                if (!_loggedStyleFailure)
                {
                    _loggedStyleFailure = true;
                    Plugin.LogSource.LogWarning($"预览蒙版：纯色样式不可用（{error.GetType().Name}：{error.Message}），改用默认 GUI.Box 绘制边框。");
                }
            }
        }

        var fallbackColor = GUI.color;
        GUI.color = color;
        foreach (var edge in edges)
            GUI.Box(edge, string.Empty);
        GUI.color = fallbackColor;
    }

    private static GUIStyle? Style(Texture2D texture)
    {
        if (_styleUnavailable)
            return null;
        if (_style != null)
            return _style;

        try
        {
            var style = new GUIStyle();
            style.normal.background = texture;
            _style = style;
            return _style;
        }
        catch (Exception error)
        {
            _styleUnavailable = true;
            if (!_loggedStyleFailure)
            {
                _loggedStyleFailure = true;
                Plugin.LogSource.LogWarning($"预览蒙版：创建纯色样式失败（{error.GetType().Name}），改用默认 GUI.Box 绘制边框。");
            }

            return null;
        }
    }

    private static Texture2D? Texture()
    {
        if (_texture != null)
            return _texture;

        try
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _texture = texture;
            return _texture;
        }
        catch (Exception error)
        {
            if (!_loggedStyleFailure)
            {
                _loggedStyleFailure = true;
                Plugin.LogSource.LogWarning($"预览蒙版：创建边框纹理失败（{error.GetType().Name}），改用默认 GUI.Box 绘制边框。");
            }

            return null;
        }
    }

    /// <summary>Logs only the transitions, so the frame never floods the log while it is visible.</summary>
    private static void ReportDrawing()
    {
        if (_drawing)
            return;
        _drawing = true;
        Plugin.LogSource.LogInfo(
            $"预览蒙版：开始绘制游玩区域边框（2px 固定线宽，颜色取主题 UI 色）；缩放={EditorNavigation.HorizontalScale * 100f:0}%，{_lastRect}");
        _lastDrawingReason = $"{_lastRect}（缩放 {EditorNavigation.HorizontalScale * 100f:0}%）";
    }

    private static void ReportStopped()
    {
        if (!_drawing)
            return;
        _drawing = false;
        Plugin.LogSource.LogInfo($"预览蒙版：已停止绘制游玩区域边框（上次几何 {_lastDrawingReason}）。");
    }

    private static float MaskPlaneZ()
    {
        try
        {
            var project = Project();
            var root = project?.playMask?.transform ?? project?.editorMask?.transform;
            return root == null ? 0f : root.position.z;
        }
        catch
        {
            return 0f;
        }
    }

    private static bool Contains(string[] names, string candidate)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, candidate, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static Camera? EditorCamera()
    {
        try
        {
            return ProjectInformation.instance?.inputControl?._camera;
        }
        catch
        {
            return null;
        }
    }

    private static ProjectInformation? Project()
    {
        try
        {
            return ProjectInformation.instance;
        }
        catch
        {
            return null;
        }
    }
}
