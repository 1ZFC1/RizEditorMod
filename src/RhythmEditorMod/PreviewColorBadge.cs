using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// Colour corner badges for the colour previews (the 98x28 / 98x42 preview images and the
/// teardrop ColorTag before/after preview). Rectangular previews get the same opaque triangle as
/// the palette slots; teardrops get a circle centred on the bottom-right corner, clipped by the
/// teardrop Mask, so the visible shape is the intersection of the circle and the droplet.
/// </summary>
internal static class PreviewColorBadge
{
    private const string TriangleName = "PreviewColorTriangle";
    private const string CircleName = "PreviewColorCircle";
    private const float TriangleLeg = 14f;
    private const float TriangleInset = 1f;
    private const float CircleAreaFraction = 1f / 6f;
    private const float FallbackCircleRadius = 14f;
    private const float Diagonal = 0.70710678f;
    private const float SquareToLeg = 1.41421356f;

    private static Sprite? _circleSprite;
    private static bool _circleWarningLogged;
    private static bool _triangleReadyLogged;
    private static bool _circleReadyLogged;
    private static bool _failureLogged;
    private static readonly List<Image> _circleBadges = new();

    /// <summary>
    /// The teardrop circle is a child of the ColorTag preview hierarchy, not of the color tool
    /// itself. Hide it whenever the color editor is not the active tool so it cannot leak into
    /// the time/speed (or any other) toolbar.
    /// </summary>
    internal static void Tick()
    {
        var visible = SpeedEasePanel.ColorEditorIsActive;
        for (var index = _circleBadges.Count - 1; index >= 0; index--)
        {
            var circle = _circleBadges[index];
            if (circle == null)
            {
                _circleBadges.RemoveAt(index);
                continue;
            }

            var parent = circle.transform.parent;
            var shouldShow = visible && parent != null && parent.gameObject.activeInHierarchy;
            if (circle.gameObject.activeSelf != shouldShow)
                circle.gameObject.SetActive(shouldShow);
        }
    }

    internal static void RefreshSelector(ColorSelectorControl? selector)
    {
        if (selector == null)
            return;

        RefreshTriangle(selector.colorPreview);
        RefreshTriangle(selector.externalColorPreview);
    }

    internal static void RefreshTag(ColorTag? tag)
    {
        if (tag == null)
            return;

        try
        {
            var before = tag.colorBefore;
            var after = tag.colorAfter;
            if (before == null && after == null)
                return;

            var parent = before != null ? before.transform.parent : after!.transform.parent;
            if (parent == null)
                parent = tag.transform;

            var color = before != null ? before.color : after!.color;
            // The bottom half is "Before"; if it is fully transparent, prefer the After colour so
            // the badge is not a black/transparent blob.
            if (before != null && before.color.a <= 0.001f && after != null && after.color.a > 0.001f)
                color = after.color;

            // Judge-ring previews face the other way; put their badge on the lower-left so the
            // line-colour (lower-right) and judge-ring (lower-left) badges are symmetric.
            var leftSide = IsJudgeRingTag(tag);

            // The teardrop's rounded end is a circle whose radius is half the short side. Use the
            // same radius for the badge, and solve the two-circle distance that makes the lens
            // area 1/6 of the preview area. Place that centre on the lower-left/lower-right diagonal.
            var circle = EnsureCircle(parent);
            if (circle == null)
                return;

            var parentRect = RectOf(parent);
            var circleRect = RectOf(circle.transform);
            if (parentRect != null && circleRect != null &&
                parentRect.rect.width > 1f && parentRect.rect.height > 1f)
            {
                var width = parentRect.rect.width;
                var height = parentRect.rect.height;
                var radius = Mathf.Min(width, height) * 0.5f;
                var targetArea = width * height * CircleAreaFraction;
                var distance = SolveCircleDistance(radius, targetArea);
                var centerY = height * 0.5f - distance * Diagonal;

                circleRect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
                if (leftSide)
                {
                    var centerX = width * 0.5f - distance * Diagonal;
                    circleRect.anchorMin = new Vector2(0f, 0f);
                    circleRect.anchorMax = new Vector2(0f, 0f);
                    circleRect.anchoredPosition = new Vector2(centerX, centerY);
                }
                else
                {
                    var centerX = width * 0.5f + distance * Diagonal;
                    circleRect.anchorMin = new Vector2(1f, 0f);
                    circleRect.anchorMax = new Vector2(1f, 0f);
                    circleRect.anchoredPosition = new Vector2(centerX - width, centerY);
                }
            }
            else if (circleRect != null)
            {
                circleRect.sizeDelta = new Vector2(FallbackCircleRadius * 2f, FallbackCircleRadius * 2f);
                circleRect.anchorMin = new Vector2(leftSide ? 0f : 1f, 0f);
                circleRect.anchorMax = new Vector2(leftSide ? 0f : 1f, 0f);
                circleRect.anchoredPosition = new Vector2(
                    leftSide ? FallbackCircleRadius * 0.64f : -FallbackCircleRadius * 0.64f,
                    FallbackCircleRadius * 0.64f);
            }

            var target = new Color(color.r, color.g, color.b, 1f);
            if (circle.color != target)
                circle.color = target;
        }
        catch (Exception error)
        {
            LogFailure(error);
        }
    }

    private static bool IsJudgeRingTag(ColorTag tag)
    {
        try
        {
            var editors = Resources.FindObjectsOfTypeAll<ColorEditor>();
            if (editors != null)
            {
                foreach (var editor in editors)
                {
                    if (editor == null)
                        continue;
                    if (editor.currentJudgeRingColorTag == tag)
                        return true;
                    if (editor.currentLineColorTag == tag)
                        return false;
                    if (editor.judgeRingColorTagPrefab == tag)
                        return true;
                    if (editor.lineColorTagPrefab == tag)
                        return false;
                }
            }
        }
        catch
        {
            // Fall through to the tag orientation below.
        }

        // Event clones under Tags/ColorTag(Clone) and Tags/ColorTagRtl(Clone) have no
        // current-editor reference. Runtime log shows LineColorTag.rtl = true and
        // RingColorTag.rtl = false, and the clones follow the same orientation: the mirrored
        // (Rtl) event tag belongs to line colour (right), the non-Rtl one to the judge ring
        // (left). So for clones the side is the inverse of rtl.
        return !tag.rtl;
    }

    private static void RefreshTriangle(Image? target)
    {
        if (target == null)
            return;

        try
        {
            var badge = FindBadge(target.transform, TriangleName);
            if (badge == null)
                badge = CreateTriangle(target);
            if (badge == null)
                return;

            var container = badge.transform.parent;
            var containerRect = container == null ? null : RectOf(container);
            if (containerRect != null)
            {
                containerRect.sizeDelta = new Vector2(TriangleLeg, TriangleLeg);
                containerRect.anchoredPosition = new Vector2(-TriangleInset, TriangleInset);
            }

            var badgeRect = RectOf(badge.transform);
            if (badgeRect != null)
                badgeRect.sizeDelta = new Vector2(TriangleLeg * SquareToLeg, TriangleLeg * SquareToLeg);

            var color = target.color;
            var targetColor = new Color(color.r, color.g, color.b, 1f);
            if (badge.color != targetColor)
                badge.color = targetColor;
        }
        catch (Exception error)
        {
            LogFailure(error);
        }
    }

    private static Image? CreateTriangle(Image target)
    {
        try
        {
            var containerObject = new GameObject(TriangleName);
            var containerRect = containerObject.AddComponent<RectTransform>();
            containerObject.AddComponent<RectMask2D>();
            containerObject.layer = target.gameObject.layer;
            containerObject.transform.SetParent(target.transform, false);
            containerRect.anchorMin = new Vector2(1f, 0f);
            containerRect.anchorMax = new Vector2(1f, 0f);
            containerRect.pivot = new Vector2(1f, 0f);
            containerRect.sizeDelta = new Vector2(TriangleLeg, TriangleLeg);
            containerRect.anchoredPosition = new Vector2(-TriangleInset, TriangleInset);

            var squareObject = new GameObject("Color");
            var squareRect = squareObject.AddComponent<RectTransform>();
            squareObject.AddComponent<CanvasRenderer>();
            var image = squareObject.AddComponent<Image>();
            squareObject.layer = target.gameObject.layer;
            squareObject.transform.SetParent(containerObject.transform, false);
            squareRect.anchorMin = new Vector2(1f, 0f);
            squareRect.anchorMax = new Vector2(1f, 0f);
            squareRect.pivot = new Vector2(0.5f, 0.5f);
            squareRect.sizeDelta = new Vector2(TriangleLeg * SquareToLeg, TriangleLeg * SquareToLeg);
            squareRect.anchoredPosition = Vector2.zero;
            squareRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            containerObject.transform.SetAsLastSibling();
            if (!_triangleReadyLogged)
            {
                _triangleReadyLogged = true;
                Plugin.LogSource.LogInfo("预览区域三角形角标已就绪。");
            }

            return image;
        }
        catch (Exception error)
        {
            LogFailure(error);
            return null;
        }
    }

    private static Image? EnsureCircle(Transform parent)
    {
        try
        {
            var existing = FindBadge(parent, CircleName);
            if (existing != null)
                return existing;

            var sprite = GetCircleSprite();
            if (sprite == null)
                return null;

            var circleObject = new GameObject(CircleName);
            var circleRect = circleObject.AddComponent<RectTransform>();
            circleObject.AddComponent<CanvasRenderer>();
            var image = circleObject.AddComponent<Image>();
            circleObject.layer = parent.gameObject.layer;
            circleObject.transform.SetParent(parent, false);
            circleRect.anchorMin = new Vector2(1f, 0f);
            circleRect.anchorMax = new Vector2(1f, 0f);
            circleRect.pivot = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = new Vector2(FallbackCircleRadius * 2f, FallbackCircleRadius * 2f);
            circleRect.anchoredPosition = new Vector2(-9f, 9f);

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            circleObject.SetActive(SpeedEasePanel.ColorEditorIsActive);
            _circleBadges.Add(image);

            circleObject.transform.SetAsLastSibling();
            if (!_circleReadyLogged)
            {
                _circleReadyLogged = true;
                Plugin.LogSource.LogInfo($"预览区域水滴圆角标已就绪（sprite={sprite.name}）。");
            }

            return image;
        }
        catch (Exception error)
        {
            LogFailure(error);
            return null;
        }
    }

    /// <summary>
    /// Distance between two equal circles (radius r) whose intersection area is targetArea.
    /// Used to place the teardrop badge so the visible lens is about 1/6 of the preview area.
    /// </summary>
    private static float SolveCircleDistance(float radius, float targetArea)
    {
        if (radius <= 0f)
            return 0f;

        var maxArea = Mathf.PI * radius * radius;
        if (targetArea <= 0f)
            return radius * 2f;
        if (targetArea >= maxArea)
            return 0f;

        var low = 0f;
        var high = radius * 2f;
        for (var index = 0; index < 40; index++)
        {
            var mid = (low + high) * 0.5f;
            if (CircleIntersectionArea(radius, mid) > targetArea)
                low = mid;
            else
                high = mid;
        }

        return (low + high) * 0.5f;
    }

    private static float CircleIntersectionArea(float radius, float distance)
    {
        var clampedDistance = Mathf.Clamp(distance, 0f, radius * 2f);
        var angle = Mathf.Acos(Mathf.Clamp(clampedDistance / (radius * 2f), -1f, 1f));
        var chord = Mathf.Sqrt(Mathf.Max(0f, 4f * radius * radius - clampedDistance * clampedDistance));
        return 2f * radius * radius * angle - 0.5f * clampedDistance * chord;
    }

    private static Image? FindBadge(Transform parent, string name)
    {
        try
        {
            var child = parent.Find(name);
            return child == null ? null : CustomToolbarTool.FindComponent<Image>(child);
        }
        catch
        {
            return null;
        }
    }

    private static Sprite? GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        try
        {
            _circleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        }
        catch
        {
            // The generic built-in lookup may be stripped; fall back to loaded sprites.
        }

        if (_circleSprite == null)
        {
            try
            {
                var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                if (sprites != null)
                {
                    foreach (var sprite in sprites)
                    {
                        if (sprite == null)
                            continue;
                        var name = sprite.name.ToLowerInvariant();
                        if (name.Contains("knob") || name.Contains("circle"))
                        {
                            _circleSprite = sprite;
                            break;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                LogFailure(error);
            }
        }

        if (_circleSprite == null && !_circleWarningLogged)
        {
            _circleWarningLogged = true;
            Plugin.LogSource.LogWarning("预览区域水滴角标不可用：找不到圆形 Sprite。");
        }

        return _circleSprite;
    }

    private static RectTransform? RectOf(Transform? node)
    {
        if (node == null)
            return null;
        try
        {
            return CustomToolbarTool.FindComponent<RectTransform>(node);
        }
        catch
        {
            return null;
        }
    }

    private static void LogFailure(Exception error)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        Plugin.LogSource.LogWarning($"预览区域颜色角标不可用：{error.Message}");
    }
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.Start))]
internal static class PreviewSelectorStartPatch
{
    private static void Postfix(ColorSelectorControl __instance) => PreviewColorBadge.RefreshSelector(__instance);
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.OnEnable))]
internal static class PreviewSelectorEnablePatch
{
    private static void Postfix(ColorSelectorControl __instance) => PreviewColorBadge.RefreshSelector(__instance);
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.SetColor))]
internal static class PreviewSelectorSetColorPatch
{
    private static void Postfix(ColorSelectorControl __instance) => PreviewColorBadge.RefreshSelector(__instance);
}

[HarmonyPatch(typeof(ColorTag), nameof(ColorTag.Start))]
internal static class PreviewTagStartPatch
{
    private static void Postfix(ColorTag __instance) => PreviewColorBadge.RefreshTag(__instance);
}

[HarmonyPatch(typeof(ColorTag), nameof(ColorTag.SetColor), new[] { typeof(Color) })]
internal static class PreviewTagSetColorPatch
{
    private static void Postfix(ColorTag __instance) => PreviewColorBadge.RefreshTag(__instance);
}

[HarmonyPatch(typeof(ColorTag), nameof(ColorTag.SetColor), new[] { typeof(Color), typeof(Color) })]
internal static class PreviewTagSetColorPairPatch
{
    private static void Postfix(ColorTag __instance) => PreviewColorBadge.RefreshTag(__instance);
}

[HarmonyPatch(typeof(ColorTag), nameof(ColorTag.SetColor), new[] { typeof(Color), typeof(bool) })]
internal static class PreviewTagSetColorSidePatch
{
    private static void Postfix(ColorTag __instance) => PreviewColorBadge.RefreshTag(__instance);
}
