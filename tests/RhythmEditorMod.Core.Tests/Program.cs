using RhythmEditorMod.Core;

var tests = new (string Name, Action Run)[]
{
    ("exact-grid endpoint", ExactGridEndpoint),
    ("short final interval", ShortFinalInterval),
    ("eased values", EasedValues),
    ("large-operation estimate", LargeOperationEstimate),
    ("closed range in both directions", ClosedRangeBothDirections),
    ("first node protection", FirstNodeProtection),
    ("ease insert skips float-collapsed endpoints", EaseInsertSkipsFloatCollapsedEndpoints),
    ("ease insert skips neighbour float collapse", EaseInsertSkipsNeighbourCollapse),
    ("ascending tolerates same-beat duplicates", AscendingToleratesDuplicates),
    ("same-beat repeats are found and collapsed", SameBeatRepeatsCollapse),
    ("paste replacement uses the given tolerance", PasteReplacementTolerance),
    ("closed range selection", ClosedRangeSelection),
    ("exact-time paste beats", ExactTimePasteBeats),
    ("aligned paste beats", AlignedPasteBeats),
    ("paste replacement indices", PasteReplacementIndices),
    ("canvas event mouse-aligned paste", CanvasEventMouseAlignedPaste),
    ("canvas event mirrored mouse-aligned paste", CanvasEventMirroredMouseAlignedPaste),
    ("canvas event subdivision snapping", CanvasEventSubdivisionSnapping),
    ("chart color unique order", ChartColorUniqueOrder),
    ("palette merge fills empty slots", PaletteMergeFillsEmptySlots),
    ("chart color byte scale", ChartChannelByteScale),
    ("speed clipboard text round trip", SpeedClipboardTextRoundTrip),
    ("canvas clipboard text round trip", CanvasClipboardTextRoundTrip),
    ("clipboard text rejects the other kind", ClipboardTextRejectsOtherKind),
    ("speed clipboard accepts tabs and spaces", SpeedClipboardAcceptsTabsAndSpaces),
    ("color clipboard keeps descending beats", ColorClipboardKeepsDescendingBeats),
    ("camera and line clipboard round trip", CameraAndLineClipboardRoundTrip),
    ("line clipboard keeps colour events", LineClipboardKeepsColourEvents),
    ("colour paste finds an event on the same beat", ColourPasteFindsSameBeat),
    ("colour paste inserts in beat order", ColourPasteInsertsInOrder),
    ("ease fit stays inside the segment", EaseFitStaysInsideSegment),
    ("ease fit samples adaptively", EaseFitIsAdaptive),
    ("elastic curves land on their endpoints", ElasticCurvesLandOnEndpoints),
    ("in and out elastic mirror each other", ElasticCurvesMirror),
    ("inOut elastic is point symmetric", InOutElasticIsPointSymmetric),
    ("ease fit of a flat segment is empty", EaseFitFlatSegmentIsEmpty),
    ("ease fit tolerance controls density", EaseFitToleranceControlsDensity),
    ("ease fit warns on large segments", EaseFitWarnsOnLargeSegments),
    ("project slots keep the newest first", ProjectSlotsKeepNewestFirst),
    ("project slots dedupe by chart path", ProjectSlotsDedupeByChartPath),
    ("project slots cap at three", ProjectSlotsCapAtThree),
    ("project slots ignore blank charts", ProjectSlotsIgnoreBlankCharts),
    ("cross-canvas line counting", CrossCanvasLineCounting),
    ("cross-canvas paste anchors on the mouse", CrossCanvasPasteAnchorsOnMouse),
    ("cross-canvas mirror reflects about the anchor", CrossCanvasMirrorReflectsAboutAnchor),
    ("cross-canvas paste refuses missing canvases", CrossCanvasPasteRefusesMissingCanvases),
    ("cross-canvas anchor prefers the largest x on the earliest beat", CrossCanvasAnchorPrefersLargestX),
    ("canvas key points evaluate with ease", CanvasKeyPointsEvaluateWithEase),
    ("zoom steps by ten percentage points", ZoomStepsByTenPoints),
    ("zoom clamps to forty and two hundred", ZoomClampsToRange),
    ("zoom wheel delta becomes whole notches", ZoomWheelBecomesNotches),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static void ExactGridEndpoint()
{
    var samples = SpeedEditing.GenerateSamples(8, 10, 0.5, 2, 4, p => p);
    Equal(9, samples.Count);
    Close(8, samples[0].Beat);
    Close(10, samples[^1].Beat);
    Close(2, samples[^1].Value);
}

static void ShortFinalInterval()
{
    var samples = SpeedEditing.GenerateSamples(1, 1.6, 1, 3, 4, p => p);
    Equal(4, samples.Count);
    Close(1.5, samples[^2].Beat);
    Close(1.6, samples[^1].Beat);
    Close(0.1, samples[^1].Beat - samples[^2].Beat);
}

static void EasedValues()
{
    var samples = SpeedEditing.GenerateSamples(0, 1, 1, 5, 4, p => p * p);
    Close(1.25, samples[1].Value);
    Close(2, samples[2].Value);
    Close(5, samples[^1].Value);
}

static void LargeOperationEstimate()
{
    Equal(4097L, SpeedEditing.EstimateSampleCount(0, 64, 64));
    Equal(4098L, SpeedEditing.EstimateSampleCount(0, 64.01, 64));
}

static void ClosedRangeBothDirections()
{
    double[] beats = { 0, 2, 4, 6, 8 };
    var forward = SpeedEditing.FindClosedRangeDeletionIndices(beats, 2, 6);
    var backward = SpeedEditing.FindClosedRangeDeletionIndices(beats, 6, 2);
    Sequence(new[] { 3, 2, 1 }, forward);
    Sequence(new[] { 3, 2, 1 }, backward);
}

static void FirstNodeProtection()
{
    double[] beats = { 0, 2, 4 };
    var indices = SpeedEditing.FindClosedRangeDeletionIndices(beats, -1, 4);
    Sequence(new[] { 2, 1 }, indices);
}

static void EaseInsertSkipsFloatCollapsedEndpoints()
{
    // Simulate a first internal beat that collapses to the start time as float.
    SpeedSample[] samples =
    {
        new(1000000.0, 1),
        new(1000000.0 + 1e-8, 1.5),
        new(1000000.25, 2),
        new(1000001.0, 3),
    };
    var filtered = SpeedEditing.FilterInsertSamples(samples, 1000000f, 1000001f);
    Equal(1, filtered.Count);
    Close(1000000.25, filtered[0].Beat);
}

static void EaseInsertSkipsNeighbourCollapse()
{
    // Two neighbouring internal samples that collapse onto the same float beat used to both be
    // inserted, which put two speed events on one beat (2026-09-23 bug report).
    SpeedSample[] samples =
    {
        new(1.0, 1),
        new(1.5, 2),
        new(1.5 + 1e-9, 3),
        new(2.0, 4),
    };
    var filtered = SpeedEditing.FilterInsertSamples(samples, 1f, 2f);
    Equal(1, filtered.Count);
    Close(1.5, filtered[0].Beat);
}

static void AscendingToleratesDuplicates()
{
    // Same-beat duplicates are legal data: the plugin has to keep working with such a chart.
    Equal(true, SpeedEditing.IsAscending(new double[] { 0, 2, 2, 4 }));
    Equal(true, SpeedEditing.IsAscending(new double[] { 0, 2, 2.0000005, 4 }, 1e-5));
    Equal(false, SpeedEditing.IsAscending(new double[] { 0, 4, 2 }));
}

static void SameBeatRepeatsCollapse()
{
    double[] beats = { 0, 2, 2, 3, 3.0000005, 5 };
    Sequence(new[] { 2, 4 }, SpeedEditing.FindSameBeatRepeats(beats, 1e-5));

    // Distinct beats stay untouched.
    string[] distinct = { "a", "b", "c" };
    double[] distinctBeats = { 0, 1, 2 };
    Sequence(distinct, SpeedEditing.CollapseSameBeatKeepLast(distinct, point => distinctBeats[Array.IndexOf(distinct, point)], 1e-5));

    // A same-beat pair keeps the later entry, matching the game's array priority.
    string[] repeated = { "p0", "p2-first", "p2-last", "p3" };
    double[] repeatedBeats = { 0, 2, 2, 3 };
    Sequence(
        new[] { "p0", "p2-last", "p3" },
        SpeedEditing.CollapseSameBeatKeepLast(repeated, point => repeatedBeats[Array.IndexOf(repeated, point)], 1e-5));
}

static void PasteReplacementTolerance()
{
    // An existing node 5e-6 beats away from a target beat is the same beat for the editor, so it
    // must be replaced (the default 1e-6 tolerance used to let it survive beside the pasted node).
    double[] existing = { 0, 2.000005, 4 };
    double[] targets = { 2, 4 };
    Sequence(new[] { 2, 1 }, SpeedEditing.FindPasteReplacementIndices(existing, targets, 1e-5));
    Sequence(new[] { 2 }, SpeedEditing.FindPasteReplacementIndices(existing, targets));
}

static void ClosedRangeSelection()
{
    double[] beats = { 0, 2, 4, 6, 8 };
    var indices = SpeedEditing.FindClosedRangeIndices(beats, 6.1, 1.9);
    Sequence(new[] { 1, 2, 3 }, indices);
}

static void ExactTimePasteBeats()
{
    double[] source = { 2, 3.5, 7 };
    Sequence(source, SpeedEditing.CreatePasteBeats(source));
}

static void AlignedPasteBeats()
{
    double[] source = { 2, 3.5, 7 };
    Sequence(new[] { 10.0, 11.5, 15.0 }, SpeedEditing.CreatePasteBeats(source, 10));
}

static void PasteReplacementIndices()
{
    double[] existing = { 0, 2, 4, 6, 8 };
    double[] targets = { 2, 6, 7 };
    Sequence(new[] { 3, 1 }, SpeedEditing.FindPasteReplacementIndices(existing, targets));
}

static void CanvasEventMouseAlignedPaste()
{
    CanvasEventClipboardPoint[] source =
    {
        new(2, -0.25, 3, 0),
        new(5.5, 0.75, 18, 0),
    };
    var pasted = CanvasEventEditing.CreatePastePoints(source, 10, 0.4, mirrorHorizontally: false);
    Equal(new CanvasEventClipboardPoint(10, 0.4, 3, 0), pasted[0]);
    Equal(new CanvasEventClipboardPoint(13.5, 1.4, 18, 0), pasted[1]);
}

static void CanvasEventMirroredMouseAlignedPaste()
{
    CanvasEventClipboardPoint[] source =
    {
        new(2, -0.25, 3, 0),
        new(5.5, 0.75, 18, 0),
    };
    var pasted = CanvasEventEditing.CreatePastePoints(source, 10, 0.4, mirrorHorizontally: true);
    Equal(new CanvasEventClipboardPoint(10, 0.4, 3, 0), pasted[0]);
    Equal(new CanvasEventClipboardPoint(13.5, -0.6, 18, 0), pasted[1]);
}

static void CanvasEventSubdivisionSnapping()
{
    Close(5.25, CanvasEventEditing.SnapToSubdivision(5.26, 4));
    Close(-0.375, CanvasEventEditing.SnapToSubdivision(-0.39, 8));
    Close(2.0, CanvasEventEditing.SnapToSubdivision(2.0, 64));
}

static void ChartColorUniqueOrder()
{
    var colors = new[]
    {
        new Rgba32(255, 0, 0, 255),
        new Rgba32(0, 255, 0, 255),
        new Rgba32(255, 0, 0, 255),
        new Rgba32(0, 0, 255, 128),
    };
    var unique = ChartColorCollection.UniqueInOrder(colors);
    Equal(3, unique.Count);
    Equal("FF0000FF", unique[0].ToHex());
    Equal("00FF00FF", unique[1].ToHex());
    Equal("0000FF80", unique[2].ToHex());
}

static void PaletteMergeFillsEmptySlots()
{
    string?[] existing = { "FF0000FF", null, "FFFFFFFF", "00FF00FF" };
    var chart = new[]
    {
        new Rgba32(255, 0, 0, 255),
        new Rgba32(0, 0, 255, 255),
        new Rgba32(255, 255, 0, 255),
        new Rgba32(0, 255, 255, 255),
    };
    var merged = ChartColorCollection.MergeIntoSlots(existing, chart);
    Equal(2, merged.Added);
    Equal(1, merged.Duplicate);
    Equal(1, merged.Overflow);
    Equal("FF0000FF", merged.Slots[0]);
    Equal("0000FFFF", merged.Slots[1]);
    Equal("FFFF00FF", merged.Slots[2]);
    Equal("00FF00FF", merged.Slots[3]);
}

static void ChartChannelByteScale()
{
    var color = Rgba32.FromChartChannels(255f, 128f, 0f, 255f);
    Equal("FF8000FF", color.ToHex());
    var normalized = Rgba32.FromChartChannels(1f, 0.5f, 0f, 1f);
    Equal("FF8000FF", normalized.ToHex());
}

static void SpeedClipboardTextRoundTrip()
{
    var source = new[]
    {
        new SpeedClipboardEntry(0, 1.5, 0),
        new SpeedClipboardEntry(1.25, 3.5, 2),
    };
    var text = EditorClipboardText.FormatSpeed(source);
    if (!EditorClipboardText.TryParseSpeed(text, out var parsed, out var error))
        throw new Exception(error);
    Sequence(source, parsed);
    var spaced = text.Replace('\t', ' ');
    if (!EditorClipboardText.TryParseSpeed(spaced, out parsed, out error))
        throw new Exception(error);
    Sequence(source, parsed);
    var chatCopy = EditorClipboardText.SpeedHeader + "\n0 1.5 0\n1.25 3.5 2";
    if (!EditorClipboardText.TryParseSpeed(chatCopy, out parsed, out error))
        throw new Exception(error);
    Sequence(source, parsed);

    if (EditorClipboardText.TryParseSpeed("", out _, out error) || error != "系统剪切板是空的。")
        throw new Exception(error);
    var descending = EditorClipboardText.SpeedHeader + "\n2\t1\t0\n1\t1\t0";
    if (EditorClipboardText.TryParseSpeed(descending, out _, out error))
        throw new Exception("descending beats were accepted");
}

static void CanvasClipboardTextRoundTrip()
{
    var source = new[]
    {
        new CanvasEventClipboardPoint(0.5, -1.25, 16, 4.5),
        new CanvasEventClipboardPoint(2, 0.75, 5, 9),
    };
    var text = EditorClipboardText.FormatCanvasEvents(source);
    if (!EditorClipboardText.TryParseCanvasEvents(text, out var parsed, out var error))
        throw new Exception(error);
    Sequence(source, parsed);
}

static void ClipboardTextRejectsOtherKind()
{
    var speed = EditorClipboardText.FormatSpeed(new[] { new SpeedClipboardEntry(0, 1, 0) });
    var canvas = EditorClipboardText.FormatCanvasEvents(new[] { new CanvasEventClipboardPoint(0, 1, 0, 2) });
    if (EditorClipboardText.TryParseCanvasEvents(speed, out _, out var error)
        || !error.Contains("另一类", StringComparison.Ordinal))
        throw new Exception(error);
    if (EditorClipboardText.TryParseSpeed(canvas, out _, out error)
        || !error.Contains("另一类", StringComparison.Ordinal))
        throw new Exception(error);
    if (EditorClipboardText.TryParseSpeed("plain text", out _, out error)
        || !error.Contains("不是流速", StringComparison.Ordinal))
        throw new Exception(error);
}

static void SpeedClipboardAcceptsTabsAndSpaces()
{
    var tabs = EditorClipboardText.SpeedHeader + "\n0\t1.5\t0\n1.25\t3.5\t2";
    if (!EditorClipboardText.TryParseSpeed(tabs, out var parsed, out var error))
        throw new Exception(error);
    Equal(0d, parsed[0].Beat);
    Equal(1.5d, parsed[0].Value);
    Equal(2, parsed[1].EaseType);
    var spaced = EditorClipboardText.SpeedHeader + "\n0   1.5   0\n1.25  3.5  2";
    if (!EditorClipboardText.TryParseSpeed(spaced, out parsed, out error))
        throw new Exception(error);
    Equal(1.25d, parsed[1].Beat);
}

static void ColorClipboardKeepsDescendingBeats()
{
    var source = new[]
    {
        new ColorEventClipboardPoint(4, 255, 0, 0, 255, 0, 255, 0, 128),
        new ColorEventClipboardPoint(2, 0, 0, 255, 255, 0, 0, 255, 255),
    };
    var text = EditorClipboardText.FormatColorEvents(EditorClipboardText.LineColorHeader, source);
    if (!EditorClipboardText.TryParseColorEvents(text, EditorClipboardText.LineColorHeader, "线色覆盖", out var parsed, out var error))
        throw new Exception(error);
    Sequence(source, parsed);
    var chat = text.Replace('\t', ' ');
    if (!EditorClipboardText.TryParseColorEvents(chat, EditorClipboardText.LineColorHeader, "线色覆盖", out parsed, out error))
        throw new Exception(error);
    Sequence(source, parsed);
}

static void CameraAndLineClipboardRoundTrip()
{
    var camera = new[] { new CanvasEventClipboardPoint(1, 0.25, 2, 3) };
    var cameraText = EditorClipboardText.FormatCameraEvents(camera);
    if (!EditorClipboardText.TryParseCameraEvents(cameraText, out var parsedCamera, out var error))
        throw new Exception(error);
    Sequence(camera, parsedCamera);

    var lines = new[]
    {
        new LineClipboardEntry(
            new[] { new LinePointClipboardEntry(2, -0.5, 1, 0, 4, 255, 0, 0, 255) },
            new[] { new NoteClipboardEntry(2, 2, 4, new[] { 6d, 0d, 1.5d }) }),
    };
    var lineText = EditorClipboardText.FormatLines(lines);
    if (!EditorClipboardText.TryParseLines(lineText, out var parsedLines, out error))
        throw new Exception(error);
    if (parsedLines.Length != 1 || parsedLines[0].Points.Count != 1 || parsedLines[0].Notes.Count != 1)
        throw new Exception("line clipboard lost entries");
    Equal(2d, parsedLines[0].Notes[0].Time);
    Equal(3, parsedLines[0].Notes[0].Other.Count);
    Equal(6d, parsedLines[0].Notes[0].Other[0]);
    if (EditorClipboardText.TryParseLines(cameraText, out _, out error) || !error.Contains("另一类", StringComparison.Ordinal))
        throw new Exception(error);
}

static void LineClipboardKeepsColourEvents()
{
    // Line has four data fields; the two colour arrays used to be dropped, which is what made
    // Ctrl+D in the line tool lose the line's colour information (2026-09-23 bug report).
    var lineColors = new[] { new ColorEventClipboardPoint(2, 255, 0, 0, 255, 0, 0, 255, 255) };
    var judgeRing = new[]
    {
        new ColorEventClipboardPoint(3, 10, 20, 30, 40, 50, 60, 70, 80),
        new ColorEventClipboardPoint(4, 1, 2, 3, 4, 5, 6, 7, 8),
    };
    var lines = new[]
    {
        new LineClipboardEntry(
            new[] { new LinePointClipboardEntry(2, -0.5, 1, 0, 4, 255, 0, 0, 255) },
            new[] { new NoteClipboardEntry(2, 2, 4, new[] { 6d }) },
            judgeRing,
            lineColors),
    };

    var text = EditorClipboardText.FormatLines(lines);
    if (!EditorClipboardText.TryParseLines(text, out var parsed, out var error))
        throw new Exception(error);
    Equal(1, parsed.Length);
    Sequence(lineColors, parsed[0].LineColors);
    Sequence(judgeRing, parsed[0].JudgeRingColors);
    Equal(1, parsed[0].Points.Count);
    Equal(1, parsed[0].Notes.Count);

    // A text written by an older build carries no colour sections and must still parse.
    var legacy = string.Join('\n', new[]
    {
        EditorClipboardText.LinesHeader,
        "line",
        "points 1",
        "2 -0.5 1 0 4 255 0 0 255",
        "notes 1",
        "2 2 4 1 6",
    });
    if (!EditorClipboardText.TryParseLines(legacy, out var legacyLines, out error))
        throw new Exception(error);
    Equal(1, legacyLines[0].Points.Count);
    Equal(0, legacyLines[0].LineColors.Count);
    Equal(0, legacyLines[0].JudgeRingColors.Count);

    // A present but malformed colour section is an error, not a silent drop.
    var broken = legacy + "\nlineColor 1\n2 1 2 3";
    Equal(false, EditorClipboardText.TryParseLines(broken, out _, out error));
}

static void ColourPasteFindsSameBeat()
{
    var beats = new double[] { 1.0, 2.0, 3.0 };
    Equal(1, ColorEventPasteTargeting.FindSameBeatIndex(beats, 2.0));
    Equal(1, ColorEventPasteTargeting.FindSameBeatIndex(beats, 2.0 + 1e-6));
    Equal(-1, ColorEventPasteTargeting.FindSameBeatIndex(beats, 2.5));
    Equal(-1, ColorEventPasteTargeting.FindSameBeatIndex(Array.Empty<double>(), 1.0));

    var duplicated = new double[] { 1.0, 2.0, 2.0, 2.0000001, 3.0 };
    Sequence(new[] { 1, 2, 3 }, ColorEventPasteTargeting.FindSameBeatIndices(duplicated, 2.0));
    Sequence(Array.Empty<int>(), ColorEventPasteTargeting.FindSameBeatIndices(duplicated, 2.5));
}

static void ColourPasteInsertsInOrder()
{
    var beats = new double[] { 1.0, 3.0 };
    Equal(0, ColorEventPasteTargeting.FindInsertIndex(beats, 0.5));
    Equal(1, ColorEventPasteTargeting.FindInsertIndex(beats, 2.0));
    Equal(2, ColorEventPasteTargeting.FindInsertIndex(beats, 4.0));
    Equal(0, ColorEventPasteTargeting.FindInsertIndex(Array.Empty<double>(), 1.0));
}

static void EaseFitStaysInsideSegment()
{
    var samples = EaseFit.SampleCurve(4, 1, 8, 3, EaseFit.OutElastic, EaseFit.DefaultTolerance, out _);

    for (var index = 0; index < samples.Count; index++)
    {
        if (samples[index].Beat <= 4 || samples[index].Beat >= 8)
            throw new Exception($"sample {index} left the segment: {samples[index].Beat:R}");
        if (index > 0 && samples[index].Beat <= samples[index - 1].Beat)
            throw new Exception($"samples are not strictly increasing at {index}");
    }

    // A same-beat duplicate with the segment end would break the editor's ordering.
    if (samples[^1].Beat >= 8 - EaseFit.BeatTolerance)
        throw new Exception("the last sample sits on the segment end");
}

static void EaseFitIsAdaptive()
{
    // Adaptive sampling must spend its nodes where the curve actually bends. Out Elastic has a
    // sharp first peak and a long, almost flat tail, so the nodes belong in the first part of the
    // curve and the tail should be left alone.
    var samples = EaseFit.SampleCurve(0, 0, 8, 1, EaseFit.OutElastic, EaseFit.DefaultTolerance, out _);
    var uniform = EaseFit.EstimateUniformCount(EaseFit.OutElastic, EaseFit.DefaultTolerance);
    if (samples.Count < 4)
        throw new Exception($"adaptive sampling produced too few nodes: {samples.Count}");
    if (samples.Count >= uniform)
        throw new Exception($"adaptive sampling ({samples.Count}) is not cheaper than a uniform grid ({uniform})");

    var early = 0;
    var tail = 0;
    foreach (var sample in samples)
    {
        var u = sample.Beat / 8.0;
        if (u < 0.4)
            early++;
        if (u > 0.7)
            tail++;
    }

    if (early < samples.Count * 0.6)
        throw new Exception($"only {early} of {samples.Count} nodes sit in the bending part of the curve");
    if (tail > 1)
        throw new Exception($"{tail} nodes were spent on the flat tail");
    if (samples[0].Beat > 8 * 0.1)
        throw new Exception($"the first node starts too far from the segment start: {samples[0].Beat:R}");
}

static void ElasticCurvesLandOnEndpoints()
{
    // Every curve has to own its endpoints exactly: that is what keeps the neighbouring segments
    // untouched, and it is why the bake can rewrite only the interior of one segment.
    foreach (var (name, curve) in ElasticCurves())
    {
        Close(0.0, curve(0));
        Close(1.0, curve(1));
    }

    // Out Elastic overshoots past the end value, In Elastic winds up below the start value first,
    // and InOut Elastic does both.
    if (EaseFit.MaxExcursion(EaseFit.OutElastic) < 0.3)
        throw new Exception("Out Elastic should overshoot by roughly a third");
    if (EaseFit.MaxExcursion(EaseFit.InElastic) < 0.3)
        throw new Exception("In Elastic should wind up by roughly a third");
    if (EaseFit.MaxExcursion(EaseFit.InOutElastic) < 0.05)
        throw new Exception("InOut Elastic should deviate on both ends");

    var outHighest = 0.0;
    var inLowest = 0.0;
    for (var step = 0; step <= 1000; step++)
    {
        var x = step / 1000.0;
        outHighest = Math.Max(outHighest, EaseFit.OutElastic(x));
        inLowest = Math.Min(inLowest, EaseFit.InElastic(x));
    }

    if (outHighest <= 1.0)
        throw new Exception("Out Elastic never went past its end value");
    if (inLowest >= 0.0)
        throw new Exception("In Elastic never went below its start value");
}

static void ElasticCurvesMirror()
{
    // The standard family identity: winding in is the mirror of winding out.
    for (var step = 0; step <= 100; step++)
    {
        var x = step / 100.0;
        Close(1.0 - EaseFit.OutElastic(1.0 - x), EaseFit.InElastic(x));
    }

    foreach (var (name, curve) in ElasticCurves())
    {
        var samples = EaseFit.SampleCurve(0, 0, 4, 1, curve, EaseFit.DefaultTolerance, out _);
        var uniform = EaseFit.EstimateUniformCount(curve, EaseFit.DefaultTolerance);
        if (samples.Count == 0 || samples.Count >= uniform)
            throw new Exception($"{name}: adaptive sampling produced {samples.Count} nodes, uniform would need {uniform}");
    }
}

static void InOutElasticIsPointSymmetric()
{
    // Point symmetry about (0.5, 0.5), which is what makes the curve usable on the middle of a
    // segment: it winds up before the halfway point and overshoots after it.
    Close(0.5, EaseFit.InOutElastic(0.5));
    for (var step = 0; step <= 100; step++)
    {
        var x = step / 100.0;
        Close(1.0, EaseFit.InOutElastic(x) + EaseFit.InOutElastic(1.0 - x));
    }

    var below = 0.0;
    var above = 0.0;
    for (var step = 0; step <= 1000; step++)
    {
        var x = step / 1000.0;
        if (x < 0.5)
            below = Math.Min(below, EaseFit.InOutElastic(x));
        else
            above = Math.Max(above, EaseFit.InOutElastic(x));
    }

    if (below >= 0.0)
        throw new Exception("InOut Elastic never dipped below its start value");
    if (above <= 1.0)
        throw new Exception("InOut Elastic never overshot its end value");

    // Regression: a midpoint-only adaptive test called this curve straight and baked nothing,
    // because point symmetry puts the curve exactly on the chord midpoint at u = 0.5.
    var nodes = EaseFit.SampleCurve(0, 0, 4, 1, EaseFit.InOutElastic, EaseFit.DefaultTolerance, out _);
    if (nodes.Count == 0)
        throw new Exception("InOut Elastic was treated as a straight line (adaptive blind spot)");
}

static IEnumerable<(string Name, Func<double, double> Curve)> ElasticCurves()
{
    yield return ("In Elastic", EaseFit.InElastic);
    yield return ("Out Elastic", EaseFit.OutElastic);
    yield return ("InOut Elastic", EaseFit.InOutElastic);
}

static void EaseFitFlatSegmentIsEmpty()
{
    var samples = EaseFit.SampleCurve(2, 7, 6, 7, EaseFit.OutElastic, EaseFit.DefaultTolerance, out var note);
    Equal(0, samples.Count);
    if (!note.Contains("首尾数值相同", StringComparison.Ordinal))
        throw new Exception($"expected a flat-segment note, got '{note}'");

    // A zero-length segment is refused as well.
    Equal(0, EaseFit.SampleCurve(3, 1, 3, 2, EaseFit.OutElastic, EaseFit.DefaultTolerance, out _).Count);
}

static void EaseFitToleranceControlsDensity()
{
    // The tolerance is the density knob: a looser tolerance must never produce more nodes.
    var tight = EaseFit.SampleCurve(0, 0, 4, 1, EaseFit.OutElastic, 0.002, out _);
    var loose = EaseFit.SampleCurve(0, 0, 4, 1, EaseFit.OutElastic, 0.05, out _);
    if (loose.Count == 0)
        throw new Exception("a looser tolerance produced no nodes at all");
    if (loose.Count > tight.Count)
        throw new Exception($"a looser tolerance produced more nodes: {loose.Count} > {tight.Count}");

    // Every curve must survive the density knob and still produce a usable number of nodes.
    foreach (var (name, curve) in ElasticCurves())
    {
        var nodes = EaseFit.SampleCurve(1, 0, 3, -0.2, curve, EaseFit.DefaultTolerance, out _).Count;
        if (nodes < 4)
            throw new Exception($"{name} produced only {nodes} nodes at the default tolerance");
    }
}

static void EaseFitWarnsOnLargeSegments()
{
    var samples = EaseFit.SampleCurve(0, 0, 500, 100, EaseFit.OutElastic, 1e-9, out var note);
    if (!note.Contains("建议上限", StringComparison.Ordinal))
        throw new Exception($"expected a size warning, got '{note}'");
}

static void ProjectSlotsKeepNewestFirst()
{
    var slots = ProjectSlotList.Promote(null, new ProjectSlot(@"C:\a\A.json", @"C:\a\a.ogg", "t1"));
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"C:\b\B.json", @"C:\b\b.ogg", "t2"));
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"C:\c\C.json", @"C:\c\c.ogg", "t3"));

    Equal(3, slots.Count);
    Equal(@"C:\c\C.json", slots[0].ChartPath);
    Equal(@"C:\b\B.json", slots[1].ChartPath);
    Equal(@"C:\a\A.json", slots[2].ChartPath);
    Equal(0, ProjectSlotList.IndexOf(slots, @"C:\c\C.json"));
    Equal(-1, ProjectSlotList.IndexOf(slots, @"C:\zz\Z.json"));
}

static void ProjectSlotsDedupeByChartPath()
{
    var slots = ProjectSlotList.Promote(null, new ProjectSlot(@"C:\a\A.json", @"C:\a\a.ogg", "t1"));
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"C:\b\B.json", @"C:\b\b.ogg", "t2"));

    // Re-opening A must move it to the front rather than list it twice, and Windows paths compare
    // case-insensitively.
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"c:\A\a.JSON", @"c:\a\other.ogg", "t3"));
    Equal(2, slots.Count);
    Equal(@"c:\A\a.JSON", slots[0].ChartPath);
    Equal(@"c:\a\other.ogg", slots[0].SongPath);
    Equal(@"C:\b\B.json", slots[1].ChartPath);

    // A caller that could not supply the music keeps the one already known for that chart.
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"C:\b\B.json", string.Empty, "t4"));
    Equal(2, slots.Count);
    Equal(@"C:\b\B.json", slots[0].ChartPath);
    Equal(@"C:\b\b.ogg", slots[0].SongPath);
}

static void CrossCanvasPasteAnchorsOnMouse()
{
    // Anchor = the first line's first node (beat 1, x 1.5 on canvas 1); paste at beat 10, x -0.5.
    var ok = CrossCanvasLineEditing.TryPlace(
        new[] { SampleLine() },
        canvasOffset: 2,
        targetBeat: 10,
        anchorTargetX: -0.5,
        mirror: false,
        existingCanvases: new[] { 0, 1, 2, 3, 4, 5, 6 },
        out var placed,
        out var error);

    Equal(true, ok);
    Equal(string.Empty, error);
    Close(10, placed[0].Points[0].Time);
    Close(-0.5, placed[0].Points[0].XPosition);
    // The second node keeps its relative offset (beat +1, x -3.75) and its own canvas.
    Close(11, placed[0].Points[1].Time);
    Close(-4.25, placed[0].Points[1].XPosition);
    Equal(3, placed[0].Points[0].CanvasIndex);
    Equal(6, placed[0].Points[1].CanvasIndex);
    // Notes move with the line; the Hold's end time and canvas follow too.
    Close(12, placed[0].Notes[0].Time);
    Close(13, placed[0].Notes[0].Other[0]);
    Close(5, placed[0].Notes[0].Other[1]);
    Close(14, placed[0].Notes[1].Time);
    // Colour key points move as well.
    Equal(0, placed[0].LineColors.Count);
}

static void CrossCanvasMirrorReflectsAboutAnchor()
{
    var ok = CrossCanvasLineEditing.TryPlace(
        new[] { SampleLine() },
        canvasOffset: 0,
        targetBeat: 4,
        anchorTargetX: 0.75,
        mirror: true,
        existingCanvases: new[] { 1, 4 },
        out var placed,
        out var error);

    Equal(true, ok);
    Equal(string.Empty, error);
    // The anchor sits on the mouse...
    Close(0.75, placed[0].Points[0].XPosition);
    Close(4, placed[0].Points[0].Time);
    // ...and the other node is reflected about it (relative offset +(-3.75) becomes -(-3.75)).
    Close(4.5, placed[0].Points[1].XPosition);
    // Canvases and times are unaffected by mirroring.
    Equal(1, placed[0].Points[0].CanvasIndex);
    Equal(4, placed[0].Points[1].CanvasIndex);
    Close(5, placed[0].Points[1].Time);
}

static void CrossCanvasLineCounting()
{
    var single = SampleLine() with
    {
        Points = new[]
        {
            SampleLine().Points[0] with { CanvasIndex = 3 },
            SampleLine().Points[1] with { CanvasIndex = 3 },
        },
    };

    Equal(0, CrossCanvasLineEditing.CountCrossCanvas(new[] { single }));
    Equal(1, CrossCanvasLineEditing.CountCrossCanvas(new[] { SampleLine() }));
    Equal(0, CrossCanvasLineEditing.CountCrossCanvas(Array.Empty<LineClipboardEntry>()));
    Sequence(new[] { 1, 4 }, CrossCanvasLineEditing.DistinctCanvases(SampleLine()));
}

static void ZoomStepsByTenPoints()
{
    // 100 % -> 110 % -> 120 % upwards, and downwards across 100 % in the same additive step.
    Nearly(1.1, ZoomStepping.Next(1f, 1));
    Nearly(1.2, ZoomStepping.Next(1.1f, 1));
    Nearly(1.0, ZoomStepping.Next(1.1f, -1));
    Nearly(0.9, ZoomStepping.Next(1f, -1));
    Nearly(1.3, ZoomStepping.Next(1f, 3));
    // The result stays on the ten-percent grid even after many steps.
    var scale = 1f;
    for (var index = 0; index < 9; index++)
        scale = ZoomStepping.Next(scale, 1);
    Nearly(1.9, scale);
    Equal(false, ZoomStepping.IsNeutral(scale));
    // One more step reaches the 200 % ceiling.
    Nearly(2.0, ZoomStepping.Next(scale, 1));
    Equal(true, ZoomStepping.IsNeutral(1f));
}

static void ZoomClampsToRange()
{
    Nearly(2.0, ZoomStepping.Next(1.95f, 1));
    Nearly(2.0, ZoomStepping.Next(1f, 50));
    Nearly(0.4, ZoomStepping.Next(0.45f, -1));
    Nearly(0.4, ZoomStepping.Next(1f, -50));
    // A zero notch keeps the current value, and an invalid one falls back to 100 %.
    Nearly(1.7, ZoomStepping.Next(1.7f, 0));
    Nearly(1.0, ZoomStepping.Clamp(float.NaN));
}

static void ZoomWheelBecomesNotches()
{
    Equal(0, ZoomStepping.Notches(0f));
    Equal(0, ZoomStepping.Notches(0.005f));
    Equal(1, ZoomStepping.Notches(0.5f));
    Equal(-1, ZoomStepping.Notches(-0.2f));
    Equal(2, ZoomStepping.Notches(2f));
    Equal(-3, ZoomStepping.Notches(-3.4f));
}
static void CrossCanvasAnchorPrefersLargestX()
{
    // Two lines whose earliest beat (1) is shared by nodes with x = -0.5 and x = 0.75; the original
    // anchors on the largest x, so the paste must put 0.75 on the mouse.
    // First line: earliest beat 1 with the smaller x (-0.5).
    var first = SampleLine() with
    {
        Points = new[]
        {
            new LinePointClipboardEntry(1, -0.5, 0, 1, 10, 0, 0, 0, 255),
            new LinePointClipboardEntry(2, -2.0, 0, 1, 20, 0, 0, 0, 255),
        },
    };
    // Second line: also starts on beat 1 but with the larger x (0.75), so this one is the anchor.
    var second = SampleLine() with
    {
        Points = new[]
        {
            new LinePointClipboardEntry(1, 0.75, 0, 1, 10, 0, 0, 0, 255),
            new LinePointClipboardEntry(3, -0.25, 0, 4, 20, 0, 0, 0, 255),
        },
    };

    var ok = CrossCanvasLineEditing.TryPlace(
        new[] { first, second },
        canvasOffset: 0,
        targetBeat: 8,
        anchorTargetX: 0.2,
        mirror: false,
        existingCanvases: new[] { 1, 4 },
        out var placed,
        out var error);

    Equal(true, ok);
    Equal(string.Empty, error);
    // The 0.75 node sits on the mouse...
    Close(8, placed[1].Points[0].Time);
    Close(0.2, placed[1].Points[0].XPosition);
    // ...and everything else keeps its offset relative to it.
    Close(8, placed[0].Points[0].Time);
    Close(-1.05, placed[0].Points[0].XPosition);
    Close(10, placed[1].Points[1].Time);
    Close(-0.8, placed[1].Points[1].XPosition);
}
static void CrossCanvasPasteRefusesMissingCanvases()
{
    var ok = CrossCanvasLineEditing.TryPlace(
        new[] { SampleLine() },
        canvasOffset: 5,
        targetBeat: 1,
        anchorTargetX: 0,
        mirror: false,
        existingCanvases: new[] { 1, 2, 3, 4 },
        out var placed,
        out var error);

    Equal(false, ok);
    Equal(0, placed.Length);
    if (!error.Contains("谱面中没有这个画布", StringComparison.Ordinal))
        throw new Exception($"unexpected error text: {error}");
}

static void CanvasKeyPointsEvaluateWithEase()
{
    // Linear ease (type 0) behaves as plain interpolation.
    var points = new[]
    {
        new CanvasEventClipboardPoint(0, 0, 0, 0),
        new CanvasEventClipboardPoint(4, 2, 0, 0),
        new CanvasEventClipboardPoint(8, -2, 0, 0),
    };

    Equal(true, CrossCanvasLineEditing.TryEvaluateKeyPoints(points, 2, Linear, out var mid));
    Close(1, mid);
    Equal(true, CrossCanvasLineEditing.TryEvaluateKeyPoints(points, 6, Linear, out var second));
    Close(0, second);
    // Outside the range the canvas keeps the nearest value.
    Equal(true, CrossCanvasLineEditing.TryEvaluateKeyPoints(points, -3, Linear, out var before));
    Close(0, before);
    Equal(true, CrossCanvasLineEditing.TryEvaluateKeyPoints(points, 99, Linear, out var after));
    Close(-2, after);

    // The supplied curve is what decides the shape: a halving curve reaches half the span early.
    Equal(true, CrossCanvasLineEditing.TryEvaluateKeyPoints(points, 1, (_, _) => 0.5, out var curved));
    Close(1, curved);
}

static double Linear(int easeType, double progress) => progress;

/// <summary>A two-point line on canvases 1 and 4 carrying one Hold and one Tap.</summary>
static LineClipboardEntry SampleLine() => new(
    new[]
    {
        new LinePointClipboardEntry(1, 1.5, 0, 1, 10, 255, 0, 0, 255),
        new LinePointClipboardEntry(2, -2.25, 3, 4, 20, 0, 255, 0, 255),
    },
    new[]
    {
        new NoteClipboardEntry(2, 3, 30, new double[] { 4, 3, 31 }),
        new NoteClipboardEntry(0, 5, 40, Array.Empty<double>()),
    },
    Array.Empty<ColorEventClipboardPoint>(),
    Array.Empty<ColorEventClipboardPoint>());

static void ProjectSlotsCapAtThree()
{
    var slots = ProjectSlotList.Empty;
    for (var index = 0; index < 6; index++)
        slots = ProjectSlotList.Promote(slots, new ProjectSlot($@"C:\p{index}\P{index}.json", $@"C:\p{index}\m.ogg", $"t{index}"));

    Equal(ProjectSlotList.Capacity, slots.Count);
    Equal(@"C:\p5\P5.json", slots[0].ChartPath);
    Equal(@"C:\p3\P3.json", slots[2].ChartPath);
    if (ProjectSlotList.IndexOf(slots, @"C:\p2\P2.json") >= 0)
        throw new Exception("an entry older than the capacity survived");
}

static void ProjectSlotsIgnoreBlankCharts()
{
    var slots = ProjectSlotList.Promote(null, new ProjectSlot("   ", @"C:\a\a.ogg", "t1"));
    Equal(0, slots.Count);
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(@"C:\a\A.json", @"C:\a\a.ogg", "t2"));
    slots = ProjectSlotList.Promote(slots, new ProjectSlot(string.Empty, @"C:\b\b.ogg", "t3"));
    Equal(1, slots.Count);
    Equal(@"C:\a\A.json", slots[0].ChartPath);
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected {expected}, got {actual}");
}

static void Nearly(double expected, double actual, double tolerance = 1e-5)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new Exception($"expected {expected:R}, got {actual:R}");
}
static void Close(double expected, double actual)
{
    if (Math.Abs(expected - actual) > 1e-8)
        throw new Exception($"expected {expected:R}, got {actual:R}");
}

static void Sequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"expected [{string.Join(",", expected)}], got [{string.Join(",", actual)}]");
}
