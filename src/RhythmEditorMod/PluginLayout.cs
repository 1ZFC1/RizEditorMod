using System.Text.Json;
using BepInEx;

namespace RhythmEditorMod;

/// <summary>
/// Layout of the plugin tool's native body, kept in a small JSON file next to the project slots so
/// it can be tuned without rebuilding the plugin: edit
/// <c>BepInEx/config/RhythmEditorMod/layout.json</c> and restart the editor.
///
/// The file is created with the defaults on first run. Every value is optional; anything missing
/// or unreadable falls back to the default below, and the effective layout is logged at build time.
/// </summary>
internal static class PluginLayout
{
    private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "RhythmEditorMod", "layout.json");
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    internal static float PanelWidth { get; private set; } = 430f;

    internal static float PanelHeight { get; private set; } = 250f;

    /// <summary>Anchor on the toolbar button: (1,1) is its top-right corner.</summary>
    internal static float AnchorX { get; private set; } = 1f;

    internal static float AnchorY { get; private set; } = 1f;

    /// <summary>Panel pivot: (0,0) grows up/right from the anchor, (0,1) grows down/right.</summary>
    internal static float PivotX { get; private set; }

    internal static float PivotY { get; private set; }

    /// <summary>Offset from the anchor; decreasing <see cref="OffsetY"/> moves the panel down.</summary>
    internal static float OffsetX { get; private set; } = 10f;

    internal static float OffsetY { get; private set; } = 6f;

    /// <summary>Distance from the panel's top to the first row (leaves room for the native title).</summary>
    internal static float FirstRowY { get; private set; } = 54f;

    /// <summary>
    /// The title bar box. The source bodies stretch theirs with anchors (sizeDelta.x is 0), so the
    /// width has to be set explicitly to make the box hug the two-character caption.
    /// </summary>
    /// <summary>
    /// Fixed title-bar width. <b>0 or less means "fit the caption"</b> (measured text width plus
    /// <see cref="TitlePaddingX"/>); any positive value is used exactly. The two used to be
    /// combined with Math.Max, which made the padding knob look dead whenever the fixed width was
    /// the larger of the two.
    /// </summary>
    internal static float TitleWidth { get; private set; }

    internal static float TitleHeight { get; private set; } = 25f;

    /// <summary>Horizontal padding inside the title box, so it does not hug the glyphs exactly.</summary>
    internal static float TitlePaddingX { get; private set; } = 26f;

    internal static float TitleX { get; private set; }

    internal static float TitleY { get; private set; } = -4f;

    internal static float RowHeight { get; private set; } = 32f;

    internal static float LeftMargin { get; private set; } = 10f;

    internal static float RightMargin { get; private set; } = 10f;

    /// <summary>Slots keep the native button height; the status line gets a little more room.</summary>
    internal static float StatusExtraHeight { get; private set; } = 10f;

    /// <summary>Long project names are cut to this many characters so the text stays inside its box.</summary>
    internal static int MaxLabelChars { get; private set; } = 44;

    /// <summary>The status line wraps, so it may show more before it is cut.</summary>
    internal static int StatusMaxChars { get; private set; } = 90;

    internal static string StoragePath => FilePath;

    /// <summary>Reads the file if present and writes a default one when it is not.</summary>
    internal static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Save();
                Plugin.LogSource.LogInfo($"插件 body 布局：已写入默认配置 {FilePath}");
                return;
            }

            var json = File.ReadAllText(FilePath);
            WarnUnknownKeys(json);
            var values = JsonSerializer.Deserialize<Values>(json, ReadOptions);
            if (values == null)
            {
                Plugin.LogSource.LogWarning("插件 body 布局：配置为空，使用默认值。");
                return;
            }

            PanelWidth = Positive(values.PanelWidth, PanelWidth);
            PanelHeight = Positive(values.PanelHeight, PanelHeight);
            AnchorX = values.AnchorX ?? AnchorX;
            AnchorY = values.AnchorY ?? AnchorY;
            PivotX = values.PivotX ?? PivotX;
            PivotY = values.PivotY ?? PivotY;
            OffsetX = values.OffsetX ?? OffsetX;
            OffsetY = values.OffsetY ?? OffsetY;
            FirstRowY = Positive(values.FirstRowY, FirstRowY);
            TitleWidth = values.TitleWidth ?? TitleWidth;
            TitleHeight = Positive(values.TitleHeight, TitleHeight);
            TitlePaddingX = Positive(values.TitlePaddingX, TitlePaddingX);
            TitleX = values.TitleX ?? TitleX;
            TitleY = values.TitleY ?? TitleY;
            RowHeight = Positive(values.RowHeight, RowHeight);
            LeftMargin = values.LeftMargin ?? LeftMargin;
            RightMargin = values.RightMargin ?? RightMargin;
            StatusExtraHeight = values.StatusExtraHeight ?? StatusExtraHeight;
            if (values.MaxLabelChars is > 0)
                MaxLabelChars = values.MaxLabelChars.Value;
            if (values.StatusMaxChars is > 0)
                StatusMaxChars = values.StatusMaxChars.Value;

            Plugin.LogSource.LogInfo(
                $"插件 body 布局：已从 {FilePath} 读取（{Describe()}）");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"插件 body 布局：读取失败，使用默认值：{error.Message}");
        }
    }

    internal static string Describe()
    {
        return $"面板 {PanelWidth:0}x{PanelHeight:0}，anchor=({AnchorX:0.##},{AnchorY:0.##})，" +
            $"pivot=({PivotX:0.##},{PivotY:0.##})，offset=({OffsetX:0.##},{OffsetY:0.##})，" +
            $"首行 {FirstRowY:0}，行高 {RowHeight:0}，边距 {LeftMargin:0}/{RightMargin:0}，" +
            $"标题框 {(TitleWidth > 0 ? $"{TitleWidth:0}（固定）" : $"按文字自适应 + 留白 {TitlePaddingX:0}")}x{TitleHeight:0}@({TitleX:0.##},{TitleY:0.##})，" +
            $"状态行 +{StatusExtraHeight:0}，标签上限 {MaxLabelChars} 字，状态上限 {StatusMaxChars} 字";
    }

    private static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var values = new Values
            {
                PanelWidth = PanelWidth,
                PanelHeight = PanelHeight,
                AnchorX = AnchorX,
                AnchorY = AnchorY,
                PivotX = PivotX,
                PivotY = PivotY,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                FirstRowY = FirstRowY,
                TitleWidth = TitleWidth,
                TitleHeight = TitleHeight,
                TitlePaddingX = TitlePaddingX,
                TitleX = TitleX,
                TitleY = TitleY,
                RowHeight = RowHeight,
                LeftMargin = LeftMargin,
                RightMargin = RightMargin,
                StatusExtraHeight = StatusExtraHeight,
                MaxLabelChars = MaxLabelChars,
                StatusMaxChars = StatusMaxChars,
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(values, WriteOptions));
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogError($"插件 body 布局：写入默认配置失败：{error.Message}");
        }
    }

    private static float Positive(float? value, float fallback)
    {
        return value is > 0 ? value.Value : fallback;
    }

    /// <summary>
    /// Names a typo instead of silently ignoring it: deserialization drops unknown keys, which used
    /// to look like "the value has no effect no matter what I write".
    /// </summary>
    private static void WarnUnknownKeys(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Plugin.LogSource.LogWarning("插件 body 布局：文件根节点不是对象，所有键都会被忽略。");
                return;
            }

            var unknown = new List<string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!KnownKeys.Contains(property.Name))
                    unknown.Add(property.Name);
            }

            if (unknown.Count > 0)
            {
                Plugin.LogSource.LogWarning(
                    $"插件 body 布局：已忽略无法识别的键 [{string.Join(", ", unknown)}]；" +
                    $"可用键：{string.Join(", ", KnownKeys)}");
            }
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"插件 body 布局：检查键名失败（不影响读取）：{error.Message}");
        }
    }

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Values.PanelWidth),
        nameof(Values.PanelHeight),
        nameof(Values.AnchorX),
        nameof(Values.AnchorY),
        nameof(Values.PivotX),
        nameof(Values.PivotY),
        nameof(Values.OffsetX),
        nameof(Values.OffsetY),
        nameof(Values.FirstRowY),
        nameof(Values.RowHeight),
        nameof(Values.LeftMargin),
        nameof(Values.RightMargin),
        nameof(Values.StatusExtraHeight),
        nameof(Values.MaxLabelChars),
        nameof(Values.StatusMaxChars),
        nameof(Values.TitleWidth),
        nameof(Values.TitleHeight),
        nameof(Values.TitlePaddingX),
        nameof(Values.TitleX),
        nameof(Values.TitleY),
    };

    /// <summary>Mirrors the file; nullable so a missing key keeps its default.</summary>
    private sealed class Values
    {
        public float? PanelWidth { get; set; }

        public float? PanelHeight { get; set; }

        public float? AnchorX { get; set; }

        public float? AnchorY { get; set; }

        public float? PivotX { get; set; }

        public float? PivotY { get; set; }

        public float? OffsetX { get; set; }

        public float? OffsetY { get; set; }

        public float? FirstRowY { get; set; }

        public float? TitleWidth { get; set; }

        public float? TitleHeight { get; set; }

        public float? TitlePaddingX { get; set; }

        public float? TitleX { get; set; }

        public float? TitleY { get; set; }

        public float? RowHeight { get; set; }

        public float? LeftMargin { get; set; }

        public float? RightMargin { get; set; }

        public float? StatusExtraHeight { get; set; }

        public int? MaxLabelChars { get; set; }

        public int? StatusMaxChars { get; set; }
    }
}
