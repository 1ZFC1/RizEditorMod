using System.Collections.Generic;
using HarmonyLib;
using RhythmEditorMod.Core;
using UnityEngine;

namespace RhythmEditorMod;

internal static class EditorNavigation
{
    // 40 %–200 % in 10 % steps (Core/ZoomStepping.cs); the factor is additive now, not a multiplier.
    private const float HorizontalMin = ZoomStepping.Minimum;
    private const float HorizontalMax = ZoomStepping.Maximum;

    private static bool _reportedPerspectiveCamera;
    private static readonly List<ProtectedCanvas> ProtectedCanvases = new();

    public static bool BreakZoomLimits { get; private set; }
    public static float HorizontalScale { get; private set; } = 1f;

    public static string SetBreakZoomLimits(bool enabled)
    {
        BreakZoomLimits = enabled;
        Plugin.LogSource.LogInfo($"整体缩放：门闩「突破缩放极限」{(enabled ? "打开" : "关闭")}。");
        if (enabled)
        {
            if (!NoteSpeedLimit.SetExtended(true))
            {
                BreakZoomLimits = false;
                return "无法放宽音符流速下限，突破缩放极限未打开。";
            }

            return "已打开突破缩放极限。Ctrl+滚轮可到 -0.5；Ctrl+Shift+滚轮整体缩放 40%–200%（步长 10%）；Ctrl+M 复位。";
        }

        NoteSpeedLimit.SetExtended(false);
        ResetHorizontalScale();
        return "已关闭突破缩放极限。音符流速下限回到 0.1，整体缩放已复位。";
    }

    public static void HandleShortcuts()
    {
        if (TextInputActive())
            return;

        var control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!control)
            return;

        if (Input.GetKeyDown(KeyCode.M))
        {
            ResetHorizontalScale();
            SpeedEasePanel.Instance?.ShowStatus("整体缩放已复位为 100%。");
            return;
        }

        if (!BreakZoomLimits)
            return;

        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (!shift || alt)
            return;

        var scroll = Input.mouseScrollDelta.y;
        if (Math.Abs(scroll) < 0.01f)
            scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Math.Abs(scroll) < 0.01f)
            return;

        var notches = ZoomStepping.Notches(scroll);
        if (notches == 0)
            return;

        HorizontalScale = ZoomStepping.Next(HorizontalScale, notches);
        ApplyHorizontalZoom($"Ctrl+Shift+滚轮 {notches:+#;-#;0} 格");
    }

    public static void ResetHorizontalScale()
    {
        var previous = HorizontalScale;
        HorizontalScale = 1f;
        ApplyHorizontalZoom($"复位（原 {previous * 100f:0}%）");
    }

    /// <summary>
    /// Applies the current <see cref="HorizontalScale"/> to the camera. <paramref name="reason"/> is
    /// only passed by the actions that change the zoom, so the log gets one line per action rather than
    /// one per frame (this method also runs from <c>LateUpdate</c> every frame).
    /// </summary>
    public static void ApplyHorizontalZoom(string reason = "")
    {
        if (reason.Length > 0)
            LogZoom(reason);

        var camera = TryGetEditorCamera();
        if (camera == null)
            return;

        if (!BreakZoomLimits || ZoomStepping.IsNeutral(HorizontalScale))
        {
            RestoreUiCanvases();
            try
            {
                camera.ResetProjectionMatrix();
            }
            catch (Exception)
            {
            }

            HorizontalScale = 1f;
            return;
        }

        if (!camera.orthographic)
        {
            if (!_reportedPerspectiveCamera)
            {
                _reportedPerspectiveCamera = true;
                Plugin.LogSource.LogWarning("整体缩放没有生效：编辑器相机不是正交相机（name=" + camera.name + ", orthographic=" + camera.orthographic + "）。");
            }

            return;
        }

        // Prefer UI visibility over clickability: move ScreenSpaceCamera canvases to Overlay.
        ProtectUiCanvases(camera);
        WriteHorizontalProjection(camera, HorizontalScale);
    }

    public static int ExtraScrollRepeats(int relativeSpeed)
    {
        if (!BreakZoomLimits)
            return 0;
        if (relativeSpeed > -10 || relativeSpeed < -15)
            return 0;
        return -10 - relativeSpeed;
    }

    /// <summary>
    /// Writes a **uniform** zoom into the camera's projection matrix: both the vertical and the
    /// horizontal half-extent grow by the same factor, so the content scales evenly instead of being
    /// stretched horizontally. The camera's own <c>orthographicSize</c> is left alone (the original
    /// editor keeps using it). The switch drives it additively: 40 %–200 % in 10 % steps.
    /// </summary>
    private static void WriteHorizontalProjection(Camera camera, float scale)
    {
        var halfHeight = camera.orthographicSize;
        var aspect = camera.aspect;
        if (!float.IsFinite(halfHeight) || halfHeight <= 0f || !float.IsFinite(aspect) || aspect <= 0f)
            return;

        var factor = ZoomStepping.Clamp(scale);
        var halfHeightZoomed = halfHeight / factor;
        var halfWidth = halfHeightZoomed * aspect;
        camera.projectionMatrix = Matrix4x4.Ortho(
            -halfWidth,
            halfWidth,
            -halfHeightZoomed,
            halfHeightZoomed,
            camera.nearClipPlane,
            camera.farClipPlane);
    }

    /// <summary>
    /// One line per zoom action (never per frame): the value, the range and step, the gate, and the
    /// camera facts the projection depends on.
    /// </summary>
    private static void LogZoom(string reason)
    {
        try
        {
            var camera = TryGetEditorCamera();
            var cameraText = camera == null
                ? "相机=<不可用>"
                : $"相机='{camera.name}' 正交={camera.orthographic} orthographicSize={camera.orthographicSize:0.###} aspect={camera.aspect:0.####} pixel={camera.pixelWidth}x{camera.pixelHeight}";
            Plugin.LogSource.LogInfo(
                $"整体缩放：{reason} → {HorizontalScale * 100f:0}%（范围 {ZoomStepping.Minimum * 100f:0}%–{ZoomStepping.Maximum * 100f:0}%，步长 {ZoomStepping.Step * 100f:0} 个百分点，门闩={(BreakZoomLimits ? "开" : "关")}，已保护 UI 画布={ProtectedCanvases.Count}，{cameraText}）");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"整体缩放：记录缩放状态失败：{error.GetType().Name}");
        }
    }
    private static Camera? TryGetEditorCamera()
    {
        try
        {
            return ProjectInformation.instance?.inputControl?._camera;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void ProtectUiCanvases(Camera editorCamera)
    {
        Canvas[] canvases;
        try
        {
            canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        }
        catch (Exception)
        {
            return;
        }

        foreach (var canvas in canvases)
        {
            if (canvas == null)
                continue;
            try
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
                    continue;
                if (canvas.worldCamera != editorCamera)
                    continue;
                if (ProtectedCanvases.Exists(entry => entry.Canvas == canvas))
                    continue;

                ProtectedCanvases.Add(new ProtectedCanvas(canvas, canvas.worldCamera));
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            catch (Exception)
            {
            }
        }
    }

    private static void RestoreUiCanvases()
    {
        if (ProtectedCanvases.Count == 0)
            return;

        foreach (var entry in ProtectedCanvases)
        {
            try
            {
                if (entry.Canvas == null)
                    continue;
                entry.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                entry.Canvas.worldCamera = entry.WorldCamera;
            }
            catch (Exception)
            {
            }
        }

        ProtectedCanvases.Clear();
    }

    /// <summary>
    /// True while a value/text box is being edited, per the game's own flag (the original toolbar
    /// buttons track the same state through <c>ToolbarToolButton.inputFields</c>). Shared by every
    /// plugin shortcut so none of them fires while the user is typing.
    /// </summary>
    internal static bool TextInputActive()
    {
        try
        {
            return ProjectInformation.isInput;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private readonly record struct ProtectedCanvas(Canvas Canvas, Camera WorldCamera);
}

[HarmonyPatch(typeof(ShortcutKeysControl), nameof(ShortcutKeysControl.ScrollTime))]
internal static class ScrollTimeBoostPatch
{
    private static bool _inside;

    private static void Postfix(ShortcutKeysControl __instance, bool up)
    {
        if (_inside || !EditorNavigation.BreakZoomLimits)
            return;

        var relativeSpeed = __instance.projectInformation?.RelativeSpeed ?? 0;
        var extra = EditorNavigation.ExtraScrollRepeats(relativeSpeed);
        if (extra <= 0)
            return;

        _inside = true;
        try
        {
            for (var index = 0; index < extra; index++)
                __instance.ScrollTime(up);
        }
        finally
        {
            _inside = false;
        }
    }
}
