using HarmonyLib;
using UnityEngine.UI;

namespace RhythmEditorMod;

/// <summary>
/// Event-driven interception of the plugin's appended ease options, at the moment a dropdown's
/// value is set.
///
/// The camera tool needs it: its callback wiring is not the canvas one, and without an interception
/// the original code stored the appended index (19+) in a key point, which made the ease table
/// lookup read out of bounds (<c>IndexOutOfRangeException</c>, 2026-09-23).
///
/// Patching the setter solves that directly: the plugin sees the selection first, bakes the curve,
/// and replaces the value with Linear (0) so the editor's own listener runs with a valid ease. The
/// value is therefore never stored in the chart, the dropdown display matches the data, and no
/// per-frame polling is needed. Only the plugin's own dropdowns are touched - the check is a
/// reference comparison - and everything else is passed straight through.
/// </summary>
// The property setter is named set_value in IL, and C# will not let nameof() reach it.
[HarmonyPatch(typeof(Dropdown), "set_value")]
internal static class EaseDropdownValuePatch
{
    private static bool Prefix(Dropdown __instance, ref int value)
    {
        try
        {
            if (__instance == null || !ExtraEasing.IsInterceptedDropdown(__instance))
                return true;

            var label = ExtraEasing.InterceptedLabelAt(__instance, value);
            if (label == null)
                return true;

            var status = ExtraEasing.ApplyForInterceptedDropdown(__instance, label);
            SpeedEasePanel.Instance?.ShowStatus(status);

            Plugin.LogSource.LogInfo(
                $"额外缓动：在「{__instance.name}」下拉框上拦截到「{label}」（原选项索引 {value}），已拟合并把下拉框复位为 Linear。");

            // Let the editor's own listener run, but with a valid ease so nothing invalid is stored.
            value = 0;
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"额外缓动：拦截下拉框选择失败：{error}");
            return true;
        }
    }
}
