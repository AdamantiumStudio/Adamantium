using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Adamantium.Fonts;
using Adamantium.Fonts.Shaping;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class ShapingTests
{
    private static readonly ConcurrentDictionary<string, IFont> Fonts = new();

    public static IEnumerable<TestCaseData> ReferenceCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Shaping", "expected.txt");
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split('\t');
            yield return new TestCaseData(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5])
                .SetName($"{Path.GetFileNameWithoutExtension(fields[0])} [{fields[1]} {fields[2]} {fields[3]}] {fields[4]}");
        }
    }

    [TestCaseSource(nameof(ReferenceCases))]
    public void ShapesLikeHarfBuzz(string fontPath, string script, string language, string features, string text,
        string expected)
    {
        var font = Fonts.GetOrAdd(fontPath, p => Typeface.LoadFont(p, 3).GetFont(0));
        var options = new ShapingOptions(script, language.Length > 0 ? language : null, FontFeature.ParseList(features));

        var glyphs = TextShaper.Shape(font, Unescape(text), options);

        Assert.That(string.Join(" ", glyphs.Select(g => g.ToString())), Is.EqualTo(expected));
    }

    [TestCase("liga", "liga", 1u, FontFeature.GlobalStart, FontFeature.GlobalEnd)]
    [TestCase("-kern", "kern", 0u, FontFeature.GlobalStart, FontFeature.GlobalEnd)]
    [TestCase("salt=2", "salt", 2u, FontFeature.GlobalStart, FontFeature.GlobalEnd)]
    [TestCase("kern[3:5]=0", "kern", 0u, 3, 5)]
    [TestCase("smcp[2]", "smcp", 1u, 2, 3)]
    [TestCase("ss01[4:]", "ss01", 1u, 4, FontFeature.GlobalEnd)]
    public void ParsesFeatureSyntax(string text, string tag, uint value, int start, int end)
    {
        var feature = FontFeature.Parse(text);

        Assert.That(feature.Tag, Is.EqualTo(tag));
        Assert.That(feature.Value, Is.EqualTo(value));
        Assert.That(feature.Start, Is.EqualTo(start));
        Assert.That(feature.End, Is.EqualTo(end));
    }

    private static string Unescape(string text)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{' && text.AsSpan(i).StartsWith("{U+"))
            {
                var close = text.IndexOf('}', i);
                var code = int.Parse(text.AsSpan(i + 3, close - i - 3), NumberStyles.HexNumber);
                result.Append(char.ConvertFromUtf32(code));
                i = close;
                continue;
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }
}
