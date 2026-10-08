using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Adamantium.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class ColorPaintTests
{
    private const double Tolerance = 0.005;
    private const double RelativeTolerance = 0.001;

    private static readonly ConcurrentDictionary<string, IFont> Fonts = new();

    public static IEnumerable<TestCaseData> ReferenceCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "ColorPaint", "expected.txt");
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split('\t');
            yield return new TestCaseData(fields[0], uint.Parse(fields[1], CultureInfo.InvariantCulture), fields[2])
                .SetName($"{Path.GetFileName(fields[0])} glyph {fields[1]}");
        }
    }

    [TestCaseSource(nameof(ReferenceCases))]
    public void PaintsLikeHarfBuzz(string fontPath, uint glyphIndex, string expected)
    {
        var font = Fonts.GetOrAdd(fontPath, p => Typeface.LoadFont(p, 3).GetFont(0));

        var actual = Format(font.GetColorPaint(glyphIndex));

        Assert.That(Matches(actual, expected), Is.True, $"\nexpected: {expected}\nactual:   {actual}");
    }

    [Test]
    public void AGlyphWithoutAPaintGraph_HasNoSteps()
    {
        var font = Fonts.GetOrAdd("TTFFonts/CascadiaCode-Regular.ttf", p => Typeface.LoadFont(p, 3).GetFont(0));

        Assert.That(font.GetColorPaint(font.GetGlyphByUnicode('a').Index), Is.Empty);
    }

    private static string Format(IReadOnlyList<ColorPaintOperation> operations)
    {
        return string.Join(" | ", operations.Select(Format));
    }

    private static string Format(ColorPaintOperation operation)
    {
        switch (operation.Kind)
        {
            case ColorPaintOperationKind.PushClip:
                var clip = new StringBuilder("clip ").Append(operation.GlyphIndex);
                Append(clip, Values(operation.Transform));
                return clip.ToString();
            case ColorPaintOperationKind.PopClip:
                return "popclip";
            case ColorPaintOperationKind.PushGroup:
                return "group";
            case ColorPaintOperationKind.PopGroup:
                return $"popgroup {(int)operation.Mode}";
        }

        var fill = operation.Fill;
        var text = new StringBuilder("fill ");
        text.Append(Kind(fill.Kind)).Append(' ').Append(fill.Extend.ToString().ToLowerInvariant());
        Append(text, Values(fill.Transform));
        Append(text, fill.Kind switch
        {
            ColorFillKind.LinearGradient => [fill.Point0.X, fill.Point0.Y, fill.Point1.X, fill.Point1.Y, fill.Point2.X, fill.Point2.Y],
            ColorFillKind.RadialGradient => [fill.Point0.X, fill.Point0.Y, fill.Radius0, fill.Point1.X, fill.Point1.Y, fill.Radius1],
            ColorFillKind.SweepGradient => [fill.Point0.X, fill.Point0.Y, fill.StartAngle, fill.EndAngle],
            _ => [],
        });
        text.Append(' ').Append(fill.Stops.Count);
        foreach (var stop in fill.Stops)
        {
            var color = stop.Color is { } c ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : "fg";
            var alpha = stop.Color is { } a ? a.A / 255.0 : stop.Alpha;
            Append(text, [stop.Offset]);
            text.Append(' ').Append(color);
            Append(text, [alpha]);
        }

        return text.ToString();
    }

    private static double[] Values(Matrix3x2 m) => [m.M11, m.M12, m.M21, m.M22, m.M31, m.M32];

    private static string Kind(ColorFillKind kind) =>
        kind switch
        {
            ColorFillKind.LinearGradient => "linear",
            ColorFillKind.RadialGradient => "radial",
            ColorFillKind.SweepGradient => "sweep",
            _ => "solid",
        };

    private static void Append(StringBuilder text, double[] values)
    {
        foreach (var value in values)
        {
            text.Append(' ').Append(Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture));
        }
    }

    private static bool Matches(string actual, string expected)
    {
        var a = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var e = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length != e.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            var isNumber = double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
            if (isNumber && double.TryParse(e[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                if (Math.Abs(x - y) > Tolerance + RelativeTolerance * Math.Abs(y))
                {
                    return false;
                }
            }
            else if (a[i] != e[i])
            {
                return false;
            }
        }

        return true;
    }
}
