using UnityEngine;

namespace RhythmEditorMod;

/// <summary>
/// The 光标 panel's "预览蒙版" switch (2026-09-23 request, scheme B).
///
/// What the original does, measured with a runtime probe on 2026-09-23:
/// <list type="bullet">
/// <item><description>Idle editing shows <c>editorMask</c> only.</description></item>
/// <item><description>Playback **with preview** (<c>isPlaying &amp;&amp; !isPlayingWithoutPreview</c>)
/// hides <c>editorMask</c> and shows <c>blackMask</c> (two screen-sized dark images in the editor UI
/// canvas) plus <c>playMask</c>; stopping restores both by itself.</description></item>
/// <item><description><c>playMask</c> holds the chart's background colour: its two children
/// (<c>Mask</c>, <c>Mask (1)</c>) are exactly <c>ProjectInformation.backgroundColors</c>, i.e. the two
/// theme groups, and each group is a full-screen <c>Background</c> plus five side covers
/// (<c>TopSide</c>, <c>BottomSideBack</c>, <c>BottomSideForward</c>, <c>LeftSide</c>, <c>RightSide</c>),
/// each with a <c>WhiteBackground</c> child. Hiding a whole group therefore removes the background
/// colour - which is why the switch never touches them as a whole.</description></item>
/// </list>
///
/// The switch:
/// <list type="bullet">
/// <item><description><b>开 (default)</b> - nothing is touched; the original preview is unchanged.</description></item>
/// <item><description><b>关</b> - during preview playback only, <c>blackMask</c> and the side covers
/// listed by name in <see cref="SideCoverNames"/> are kept hidden, so the background colour and
/// everything else stay exactly as the original draws them while the content on both sides becomes
/// visible.</description></item>
/// </list>
///
/// Node selection is an **explicit name list**, not a name suffix: the editor has several children
/// ending in "Side"/"Back"/"Forward", and a suffix test could one day match a node that is not a
/// cover. The list holds all five covers of both theme groups; the version that only matched names
/// ending in "Side" left the two bottom panels covering the content below the playhead.
///
/// Safety details: the suppression is re-asserted every frame (the original writes these objects
/// itself) and it is released the moment preview stops or the switch goes back to 开 - in that case
/// the objects are put back to the state they had when the plugin first hid them, so turning the
/// switch back on during playback restores the original preview immediately. <c>editorMask</c> is
/// never touched: its handling (D-017, horizontal zoom) stays the only owner of that object.
/// </summary>
internal static class PreviewMaskControl
{
    /// <summary>
    /// Side covers the switch hides, matched by **exact name** rather than by a name suffix.
    ///
    /// The editor has several children ending in "Side"/"Back"/"Forward" (<c>TopSide</c>,
    /// <c>BottomSideBack</c>, <c>BottomSideForward</c>, <c>LeftSide</c>, <c>RightSide</c>), so a
    /// suffix test could silently catch a future node that is not a cover. An explicit list also
    /// states the intent: anything not listed is never hidden.
    ///
    /// All five covers of both theme groups are listed (ten nodes): the first version only had the
    /// three that end in "Side", which left the two bottom panels covering the content below the
    /// playhead. The <c>Background</c> child of each group - the chart's background colour - is never
    /// listed. See docs/features/PREVIEW_MASK.md.
    /// </summary>
    private static readonly string[] SideCoverNames =
    {
        "TopSide",
        "BottomSideForward",
        "BottomSideBack",
        "LeftSide",
        "RightSide",
    };

    /// <summary>
    /// One object the plugin may hide.
    /// <paramref name="AlwaysRestore"/> marks a **child** cover inside <c>playMask</c>: the original
    /// only switches that object's root, so a child's <c>activeSelf</c> is never reset by the game -
    /// the plugin therefore always puts it back to active (its correct state), whatever the playback
    /// state is. The root <c>blackMask</c> instead follows the original: it is only restored while
    /// preview is still running.
    /// </summary>
    private readonly record struct Hidable(GameObject Object, bool AlwaysRestore);

    /// <summary>What the plugin hid, and what it looked like before, so it can be put back faithfully.</summary>
    private sealed class Suppressed
    {
        internal GameObject Target = null!;

        internal bool WasActive;

        internal bool AlwaysRestore;
    }

    private static readonly List<Suppressed> Hidden = new();

    private static bool _enabled = true;
    private static bool _reportedHide;
    private static string _status = "预览蒙版：开（原版行为）。";

    internal static bool Enabled => _enabled;

    internal static string Status => _status;

    /// <summary>Called every frame from the panel's LateUpdate, so the state follows playback.</summary>
    internal static void Update()
    {
        try
        {
            var previewing = IsPreviewPlaybackRunning();
            var suppress = previewing && !_enabled;

            if (suppress)
                Suppress();
            else
                Release(previewing);
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"预览蒙版：切换失败：{error.GetType().Name}");
        }
    }

    internal static string SetEnabled(bool enabled)
    {
        _enabled = enabled;

        // Take effect immediately rather than on the next frame.
        Update();
        if (enabled)
        {
            // Children of playMask are never switched individually by the original, so any one of them
            // still inactive is a leftover from an earlier session of this plugin (the first version
            // released without restoring them). Putting them back repairs that state without a restart.
            var repaired = RepairChildCovers();
            _status = repaired > 0
                ? $"预览蒙版：开。已恢复原版行为，并修复了 {repaired} 个此前遗留被隐藏的侧板。"
                : "预览蒙版：开。恢复原版行为（预览时按原样显示蒙版与两侧遮挡）。";
        }
        else
        {
            _status = IsPreviewPlaybackRunning()
                ? "预览蒙版：关。已隐藏编辑器蒙版与两侧遮挡，背景色保留。"
                : "预览蒙版：关。进入带预览的播放后会隐藏编辑器蒙版与两侧遮挡（背景色保留）。";
        }

        Plugin.LogSource.LogInfo(_status);
        return _status;
    }

    /// <summary>
    /// Puts every child cover back to active. Only the plugin ever changes those flags (the original
    /// switches the <c>playMask</c> root), so this both prevents and repairs the "all covers stay
    /// hidden" state.
    /// </summary>
    private static int RepairChildCovers()
    {
        var project = Project();
        if (project == null)
            return 0;

        var repaired = 0;
        foreach (var hidable in Targets(project))
        {
            if (!hidable.AlwaysRestore || hidable.Object == null || hidable.Object.activeSelf)
                continue;

            hidable.Object.SetActive(true);
            repaired++;
        }

        return repaired;
    }

    /// <summary>True only for playback that runs with preview, which is the mode both masks belong to.</summary>
    internal static bool IsPreviewPlaybackRunning()
    {
        try
        {
            return ProjectInformation.isPlaying && !ProjectInformation.isPlayingWithoutPreview;
        }
        catch
        {
            return false;
        }
    }

    private static void Suppress()
    {
        var project = Project();
        if (project == null)
            return;

        var hiddenNow = 0;
        foreach (var hidable in Targets(project))
        {
            var target = hidable.Object;
            if (target == null)
                continue;

            if (!IsTracked(target))
            {
                Hidden.Add(new Suppressed
                {
                    Target = target,
                    WasActive = target.activeSelf,
                    AlwaysRestore = hidable.AlwaysRestore,
                });
            }

            if (!target.activeSelf)
                continue;

            target.SetActive(false);
            hiddenNow++;
        }

        if (!_reportedHide && Hidden.Count > 0)
        {
            _reportedHide = true;
            var names = new List<string>();
            foreach (var entry in Hidden)
                names.Add(entry.Target == null ? "<已销毁>" : entry.Target.name);
            Plugin.LogSource.LogInfo(
                $"预览蒙版：预览播放期间已隐藏 {Hidden.Count} 个对象（编辑器蒙版 blackMask + 白名单侧板；playMask 的 Background 保留）；"
                + $"播放中={Playing()}，带预览={WithPreview()}，开关={(Enabled ? "关" : "开")}，列表：{string.Join("、", names)}");
        }
    }

    private static string Playing()
    {
        try
        {
            return ProjectInformation.isPlaying ? "是" : "否";
        }
        catch (Exception error)
        {
            return $"<不可用：{error.GetType().Name}>";
        }
    }

    private static string WithPreview()
    {
        try
        {
            return ProjectInformation.isPlayingWithoutPreview ? "否" : "是";
        }
        catch (Exception error)
        {
            return $"<不可用：{error.GetType().Name}>";
        }
    }

    /// <summary>
    /// Stops suppressing.
    ///
    /// Child covers are **always** put back to active: the original only switches the <c>playMask</c>
    /// root, so a child left inactive would stay hidden even after playback stops (that is exactly the
    /// bug reported on 2026-09-23 - after toggling the switch a few times, every cover of
    /// <c>playMask</c> except the background stayed hidden). Putting them back while the root is
    /// inactive has no visual effect, so it is safe at any time.
    ///
    /// The root <c>blackMask</c> keeps the original contract: it is only restored while preview is
    /// still running, because once playback has stopped the original has already set its own state and
    /// re-activating it would show the editor overlay while idle.
    /// </summary>
    private static void Release(bool previewing)
    {
        if (Hidden.Count == 0)
            return;

        var restored = 0;
        var repaired = 0;
        foreach (var entry in Hidden)
        {
            if (entry.Target == null)
                continue;

            if (entry.AlwaysRestore)
            {
                if (entry.Target.activeSelf)
                    continue;
                entry.Target.SetActive(true);
                restored++;
                if (!entry.WasActive)
                    repaired++;
                continue;
            }

            if (!previewing || !entry.WasActive || entry.Target.activeSelf)
                continue;

            entry.Target.SetActive(true);
            restored++;
        }

        if (restored > 0)
        {
            Plugin.LogSource.LogInfo(
                $"预览蒙版：已恢复 {restored} 个对象的显示"
                + (repaired > 0 ? $"（其中 {repaired} 个是此前遗留的隐藏状态）" : string.Empty)
                + "。");
        }

        Hidden.Clear();
        _reportedHide = false;
    }

    /// <summary>
    /// Everything the switch hides: <c>blackMask</c> plus the side covers of both
    /// <c>playMask</c> theme groups whose name is on <see cref="SideCoverNames"/>. Their
    /// <c>Background</c> children - the chart's background colour - are never included, and neither is
    /// any node the list does not name.
    /// </summary>
    private static List<Hidable> Targets(ProjectInformation project)
    {
        var targets = new List<Hidable>();
        Add(targets, project.blackMask, alwaysRestore: false);

        var playMask = project.playMask;
        if (playMask == null)
            return targets;

        try
        {
            for (var groupIndex = 0; groupIndex < playMask.transform.childCount; groupIndex++)
            {
                var group = playMask.transform.GetChild(groupIndex);
                for (var sideIndex = 0; sideIndex < group.childCount; sideIndex++)
                {
                    var side = group.GetChild(sideIndex);
                    if (IsListedCover(side.name))
                        Add(targets, side.gameObject, alwaysRestore: true);
                }
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"预览蒙版：收集两侧遮挡失败：{error.GetType().Name}");
        }

        return targets;
    }

    /// <summary>Exact-name membership test: only listed covers are ever hidden.</summary>
    private static bool IsListedCover(string nodeName)
    {
        foreach (var listed in SideCoverNames)
        {
            if (string.Equals(nodeName, listed, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void Add(List<Hidable> targets, GameObject? candidate, bool alwaysRestore)
    {
        if (candidate == null)
            return;
        foreach (var existing in targets)
        {
            if (ReferenceEquals(existing.Object, candidate))
                return;
        }

        targets.Add(new Hidable(candidate, alwaysRestore));
    }

    private static bool IsTracked(GameObject target)
    {
        foreach (var entry in Hidden)
        {
            if (ReferenceEquals(entry.Target, target))
                return true;
        }

        return false;
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
