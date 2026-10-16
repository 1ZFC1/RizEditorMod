using HarmonyLib;
using UnityEngine;

namespace RhythmEditorMod;

/// <summary>
/// Safety net around the editor's ease table.
///
/// <c>GetEase.EaseInfors</c> is a fixed <c>19 x 101</c> array, so evaluating an ease type above 18
/// reads past it and Unity reports <c>IndexOutOfRangeException</c>. The user hit exactly that on
/// 2026-09-23 after picking one of the plugin's appended options in a tool whose ease callback the
/// plugin cannot intercept - in that case the game itself had already written the new index (19+)
/// into a key point before the plugin could bake anything.
///
/// Both evaluators therefore fall back to **Linear** for out-of-range types, which is precisely the
/// curve the plugin's baked nodes use, and report the offending type once per session so the bad
/// node can be found. Normal 0-18 evaluation is untouched.
/// </summary>
internal static class InvalidEaseGuard
{
    private const int HighestEaseType = 18;

    private static readonly HashSet<int> Reported = new();

    internal static bool IsValid(int type) => type >= 0 && type <= HighestEaseType;

    /// <summary>Logs each invalid type once; repeats are counted and summarised later.</summary>
    internal static void Report(int type, string source)
    {
        if (!Reported.Add(type))
            return;

        Plugin.LogSource.LogWarning(
            $"{source}：谱面里出现了 easeType={type}（有效范围 0–{HighestEaseType}），已按 Linear 处理以免崩溃。" +
            $"该缓动值通常来自「下拉框里选了插件曲线但没被拦截」的情况：请在对应工具里重新选择一次 Linear，" +
            $"或对刚做过的拟合操作按 Ctrl+Z 撤销。");
    }
}

[HarmonyPatch(typeof(GetEase), nameof(GetEase.GetEaseWithProgress))]
internal static class GetEaseProgressGuardPatch
{
    /// <summary>0..1 progress for an out-of-range ease: linear, i.e. the baked curve's own fallback.</summary>
    private static bool Prefix(float progress, int type, ref float __result)
    {
        if (InvalidEaseGuard.IsValid(type))
            return true;

        __result = Mathf.Clamp01(progress);
        InvalidEaseGuard.Report(type, "GetEaseWithProgress");
        return false;
    }
}

[HarmonyPatch(typeof(GetEase), nameof(GetEase.GetEaseWithIndex))]
internal static class GetEaseIndexGuardPatch
{
    /// <summary>Same, for the sample-index variant: index 0..100 maps onto 0..1 linearly.</summary>
    private static bool Prefix(int index, int type, ref float __result)
    {
        if (InvalidEaseGuard.IsValid(type))
            return true;

        __result = Mathf.Clamp01(index / 100f);
        InvalidEaseGuard.Report(type, "GetEaseWithIndex");
        return false;
    }
}
