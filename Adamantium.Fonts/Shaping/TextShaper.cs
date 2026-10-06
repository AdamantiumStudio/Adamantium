using System.Globalization;
using System.Linq;

namespace Adamantium.Fonts.Shaping;

/// <summary>Turns text into positioned glyphs of a font, applying its OpenType substitutions and positioning.</summary>
public static class TextShaper
{
    /// <summary>Shapes <paramref name="text"/> left to right; the result is in font design units. Without a script in
    /// <paramref name="options"/> the text is split where its script changes and each part is shaped with its own.</summary>
    public static ShapedGlyph[] Shape(IFont font, string text, ShapingOptions options = null)
    {
        options ??= ShapingOptions.Default;
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        if (options.Script != null)
        {
            return ShapeRun(font, text, 0, text.Length, options.Script, options);
        }

        var runs = ScriptItemizer.Itemize(text);
        if (runs.Count == 1)
        {
            return ShapeRun(font, text, 0, text.Length, runs[0].Script, options);
        }

        return runs.SelectMany(run => ShapeRun(font, text, run.Start, run.End, run.Script, options)).ToArray();
    }

    private static ShapedGlyph[] ShapeRun(IFont font, string text, int start, int end, string script,
        ShapingOptions options)
    {
        var buffer = new GlyphBuffer(end - start);
        AddText(buffer, text, start, end);
        UnicodeProps.SetAll(buffer);
        FormClusters(buffer);

        var layout = font.Layout;
        var plan = GetPlan(layout, script, options);

        Normalizer.Normalize(font, buffer);
        SetupMasks(buffer, plan, options);
        SetGlyphProps(buffer, layout);

        var applier = new LookupApplier(layout, buffer);
        applier.ApplyGsub(plan);
        Positioner.Position(font, buffer, plan, applier);
        HideDefaultIgnorables(font, buffer);

        var result = new ShapedGlyph[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            var info = buffer.Info[i];
            var placement = buffer.Placements[i];
            result[i] = new ShapedGlyph(info.Glyph, info.Cluster, placement.XAdvance, placement.YAdvance,
                placement.XOffset, placement.YOffset);
        }

        return result;
    }

    private static void AddText(GlyphBuffer buffer, string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            int codepoint = text[i];
            var start = i;
            if (char.IsHighSurrogate(text[i]) && i + 1 < to && char.IsLowSurrogate(text[i + 1]))
            {
                codepoint = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                codepoint = 0xFFFD;
            }

            buffer.Add(new GlyphInfo { Codepoint = codepoint, Cluster = start });
        }
    }

    private static void FormClusters(GlyphBuffer buffer)
    {
        if (!buffer.HasNonAscii)
        {
            return;
        }

        var start = 0;
        while (start < buffer.Length)
        {
            var end = start + 1;
            while (end < buffer.Length && buffer.Info[end].IsContinuation)
            {
                end++;
            }

            buffer.MergeClusters(start, end);
            start = end;
        }
    }


    private static ShapePlan GetPlan(OpenTypeLayout layout, string script, ShapingOptions options)
    {
        var scriptTag = OpenTypeTags.ScriptTag(script);
        var languageTag = OpenTypeTags.LanguageTag(options.Language);
        var features = string.Join(",", options.Features.Select(f => $"{f.Tag}={f.Value}{(f.IsGlobal ? "g" : "r")}"));
        var key = $"{scriptTag}|{languageTag}|{features}";
        return layout.Plans.GetOrAdd(key, _ => ShapePlan.Create(layout, scriptTag, languageTag, options.Features));
    }

    private static void SetupMasks(GlyphBuffer buffer, ShapePlan plan, ShapingOptions options)
    {
        var info = buffer.Info;
        for (var i = 0; i < buffer.Length; i++)
        {
            info[i].Mask = plan.GlobalMask;
        }

        foreach (var feature in options.Features)
        {
            if (feature.IsGlobal)
            {
                continue;
            }

            var map = plan.GetFeature(feature.Tag);
            if (map == null)
            {
                continue;
            }

            var value = (feature.Value << map.Shift) & map.Mask;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (info[i].Cluster >= feature.Start && info[i].Cluster < feature.End)
                {
                    info[i].Mask = (info[i].Mask & ~map.Mask) | value;
                }
            }
        }

        if (buffer.HasNonAscii && plan.FracMask != 0 && plan.NumrMask != 0 && plan.DnomMask != 0)
        {
            SetupFractionMasks(buffer, plan);
        }
    }

    private static void SetupFractionMasks(GlyphBuffer buffer, ShapePlan plan)
    {
        var info = buffer.Info;
        var before = plan.NumrMask | plan.FracMask;
        var after = plan.FracMask | plan.DnomMask;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (info[i].Codepoint != 0x2044)
            {
                continue;
            }

            var start = i;
            var end = i + 1;
            while (start > 0 && info[start - 1].Category == UnicodeCategory.DecimalDigitNumber)
            {
                start--;
            }

            while (end < buffer.Length && info[end].Category == UnicodeCategory.DecimalDigitNumber)
            {
                end++;
            }

            if (start == i || end == i + 1)
            {
                continue;
            }

            for (var j = start; j < i; j++)
            {
                info[j].Mask |= before;
            }

            info[i].Mask |= plan.FracMask;
            for (var j = i + 1; j < end; j++)
            {
                info[j].Mask |= after;
            }

            i = end - 1;
        }
    }

    private static void SetGlyphProps(GlyphBuffer buffer, OpenTypeLayout layout)
    {
        var info = buffer.Info;
        for (var i = 0; i < buffer.Length; i++)
        {
            info[i].LigProps = 0;
            if (layout.HasGlyphClasses)
            {
                info[i].Props = layout.GetGlyphProps(info[i].Glyph);
                continue;
            }

            var isMark = info[i].Category == UnicodeCategory.NonSpacingMark && !info[i].IsDefaultIgnorable;
            info[i].Props = isMark ? GlyphProps.Mark : GlyphProps.BaseGlyph;
        }
    }

    private static void HideDefaultIgnorables(IFont font, GlyphBuffer buffer)
    {
        if (!buffer.HasDefaultIgnorables)
        {
            return;
        }

        if (font.TryGetGlyphIndex(0x0020, out var space))
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer.Info[i].IsDefaultIgnorable)
                {
                    buffer.Info[i].Glyph = space;
                }
            }

            return;
        }

        buffer.RemoveWhere(info => info.IsDefaultIgnorable);
    }
}
