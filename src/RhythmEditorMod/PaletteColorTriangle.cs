using HarmonyLib;
using RhythmEditorMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// Draws a small opaque RGB triangle in the bottom-right corner of every non-empty palette slot.
/// The slot itself keeps its original colour/alpha and checkerboard background; the triangle only
/// shows the colour's original hue when the slot's alpha makes it hard to see.
///
/// The triangle is made without creating a Texture2D/Sprite (those calls fail with
/// "Method unstripping failed" in this build): a plain white UI square is rotated 45 degrees and
/// clipped by a RectMask2D that covers the slot, leaving the corner triangle.
/// </summary>
internal static class PaletteColorTriangle
{
    private const string BadgeName = "AlphaColorTriangle";
    private const float AreaFraction = 1f / 6f;
    private const float MinimumLeg = 4f;
    private const float BorderInset = 1f;
    private const float SquareToLeg = 1.41421356f;

    private static bool _failureLogged;
    private static bool _readyLogged;

    internal static void RefreshAll()
    {
        try
        {
            var selectors = Resources.FindObjectsOfTypeAll<ColorSelectorControl>();
            if (selectors == null)
                return;

            foreach (var selector in selectors)
            {
                if (selector != null)
                    Refresh(selector);
            }
        }
        catch (Exception error)
        {
            LogFailure(error);
        }
    }

    internal static void Refresh(ColorSelectorControl? selector)
    {
        if (selector == null)
            return;

        try
        {
            var panels = ProjectInformation.colorPanels;
            var presets = selector.colorPresets;
            if (panels == null || presets == null)
                return;

            var count = Math.Min(panels.Length, presets.Length);
            for (var index = 0; index < count; index++)
            {
                var preset = presets[index];
                if (preset == null)
                    continue;

                var badge = FindBadge(preset.transform);
                if (IsEmptySlot(panels[index]))
                {
                    if (badge != null && badge.gameObject.activeSelf)
                        badge.gameObject.SetActive(false);
                    continue;
                }

                if (badge == null)
                    badge = CreateBadge(preset.transform);
                if (badge == null)
                    continue;

                if (!badge.gameObject.activeSelf)
                    badge.gameObject.SetActive(true);

                var slotRect = RectOf(preset.transform);
                float leg;
                if (slotRect == null || slotRect.rect.width < 1f || slotRect.rect.height < 1f)
                {
                    leg = 8f;
                }
                else
                {
                    // Right isosceles triangle: area = leg^2 / 2. For 1/6 of the slot area,
                    // leg = sqrt(width * height / 3).
                    var areaLeg = Mathf.Sqrt(2f * AreaFraction * slotRect.rect.width * slotRect.rect.height);
                    var maxLeg = Mathf.Max(1f, Mathf.Min(slotRect.rect.width, slotRect.rect.height));
                    leg = Mathf.Clamp(areaLeg, Mathf.Min(MinimumLeg, maxLeg), maxLeg);
                }

                var clipRect = badge.transform.parent == null ? null : RectOf(badge.transform.parent);
                if (clipRect != null && Mathf.Abs(clipRect.sizeDelta.x - leg) > 0.01f)
                    clipRect.sizeDelta = new Vector2(leg, leg);

                var side = leg * SquareToLeg;
                var badgeRect = RectOf(badge.transform);
                if (badgeRect != null && Mathf.Abs(badgeRect.sizeDelta.x - side) > 0.01f)
                    badgeRect.sizeDelta = new Vector2(side, side);

                // The badge is deliberately opaque: the point is to show the original RGB even
                // when the slot itself has a very small alpha.
                var color = preset.color;
                var target = new Color(color.r, color.g, color.b, 1f);
                if (badge.color != target)
                    badge.color = target;
            }
        }
        catch (Exception error)
        {
            LogFailure(error);
        }
    }

    private static bool IsEmptySlot(Color color)
    {
        var rgba = Rgba32.FromChartChannels(color.r, color.g, color.b, color.a);
        return Rgba32.IsEmptyPaletteSlot(rgba.ToHex());
    }

    private static Image? FindBadge(Transform parent)
    {
        try
        {
            var child = parent.Find(BadgeName);
            return child == null ? null : CustomToolbarTool.FindComponent<Image>(child);
        }
        catch
        {
            return null;
        }
    }

    private static Image? CreateBadge(Transform parent)
    {
        try
        {
            // Container: exactly the slot minus a 1 px inset. RectMask2D clips the rotated square
            // to this rectangle, so only the corner triangle remains visible.
            var clipObject = new GameObject(BadgeName);
            var clipRect = clipObject.AddComponent<RectTransform>();
            clipObject.AddComponent<RectMask2D>();
            clipObject.layer = parent.gameObject.layer;
            clipObject.transform.SetParent(parent, false);
            clipRect.anchorMin = new Vector2(1f, 0f);
            clipRect.anchorMax = new Vector2(1f, 0f);
            clipRect.pivot = new Vector2(1f, 0f);
            clipRect.sizeDelta = new Vector2(12f, 12f);
            clipRect.anchoredPosition = new Vector2(-BorderInset, BorderInset);

            // The square is centred exactly on the container's bottom-right corner and rotated 45°.
            // The part inside the container is a right triangle whose right angle is the corner.
            var squareObject = new GameObject("Color");
            var squareRect = squareObject.AddComponent<RectTransform>();
            squareObject.AddComponent<CanvasRenderer>();
            var image = squareObject.AddComponent<Image>();
            squareObject.layer = parent.gameObject.layer;
            squareObject.transform.SetParent(clipObject.transform, false);
            squareRect.anchorMin = new Vector2(1f, 0f);
            squareRect.anchorMax = new Vector2(1f, 0f);
            squareRect.pivot = new Vector2(0.5f, 0.5f);
            squareRect.sizeDelta = new Vector2(11.3f, 11.3f);
            squareRect.anchoredPosition = Vector2.zero;
            squareRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            clipObject.transform.SetAsLastSibling();
            if (!_readyLogged)
            {
                _readyLogged = true;
                Plugin.LogSource.LogInfo("调色板颜色角标已就绪。");
            }

            return image;
        }
        catch (Exception error)
        {
            LogFailure(error);
            return null;
        }
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
        Plugin.LogSource.LogWarning($"调色板颜色角标不可用：{error.Message}");
    }
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.Start))]
internal static class PaletteColorTriangleStartPatch
{
    private static void Postfix(ColorSelectorControl __instance) => PaletteColorTriangle.Refresh(__instance);
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.OnEnable))]
internal static class PaletteColorTriangleEnablePatch
{
    private static void Postfix(ColorSelectorControl __instance) => PaletteColorTriangle.Refresh(__instance);
}

[HarmonyPatch(typeof(ColorSelectorControl), nameof(ColorSelectorControl.SyncColorPanels))]
internal static class PaletteColorTriangleSyncPatch
{
    private static void Postfix(ColorSelectorControl __instance) => PaletteColorTriangle.Refresh(__instance);
}
