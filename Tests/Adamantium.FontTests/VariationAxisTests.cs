using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests;

/// <summary>A variable font's axes and named styles as a style panel lists them, against HarfBuzz
/// (Variations/axes-expected.txt, written by Adamantium.ShapingOracle), and the names 'STAT' gives axis values.</summary>
public class VariationAxisTests
{
    private static readonly ConcurrentDictionary<string, IFont> Fonts = new();

    public static IEnumerable<TestCaseData> ReferenceFonts()
    {
        foreach (var line in File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Variations", "axes-cases.txt")))
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                yield return new TestCaseData(line).SetName(Path.GetFileName(line));
            }
        }
    }

    [TestCaseSource(nameof(ReferenceFonts))]
    public void AxesAndNamedInstances_AreHarfBuzzs(string fontPath)
    {
        var font = Load(fontPath);
        var expected = File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Variations", "axes-expected.txt"))
            .Where(line => line.StartsWith(fontPath + "\t"))
            .ToArray();

        var actual = font.Axes
            .Select(axis => string.Join("\t", fontPath, "A", axis.Tag, axis.Name, axis.IsHidden ? "hidden" : "shown",
                Number(axis.MinValue), Number(axis.DefaultValue), Number(axis.MaxValue)))
            .Concat(font.NamedInstances.Select(instance => string.Join("\t", fontPath, "I", instance.Name,
                string.Join(",", instance.Variations.Select(v => Number(v.Value))))))
            .ToArray();

        Assert.That(actual, Is.EqualTo(expected));
    }

    public static IEnumerable<TestCaseData> MetricCases()
    {
        foreach (var line in File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Variations", "metrics-expected.txt")))
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                var fields = line.Split('\t');
                yield return new TestCaseData(fields[0], fields[1], fields[2])
                    .SetName($"{Path.GetFileName(fields[0])} at {fields[1]}");
            }
        }
    }

    [TestCaseSource(nameof(MetricCases))]
    public void FontMetrics_AtAxisValues_AreHarfBuzzs(string fontPath, string variations, string expected)
    {
        var instance = Load(fontPath).GetInstance(variations.Split(',')
            .Select(v => v.Split('='))
            .Select(v => new FontVariation(v[0], float.Parse(v[1], CultureInfo.InvariantCulture)))
            .ToArray());

        var actual = $"hasc={instance.LineAscent} hdsc={-instance.LineDescent} hlgp={instance.LineGap} " +
                     $"cpht={instance.CapsHeight} undo={instance.UnderlinePosition} unds={instance.UnderlineThickness} " +
                     $"stro={instance.StrikeoutPosition} strs={instance.StrikeoutSize}";

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void StyleAttributes_NameTheAxisValues()
    {
        var font = Load("Variations/RobotoFlex-Variable.ttf");
        FontAxisValue Named(string name, string tag) =>
            font.AxisValues.Single(v => v.Name == name && v.Values.Single().Tag == tag);

        var regular = Named("Regular", "wght");
        Assert.Multiple(() =>
        {
            Assert.That(regular.Values.Single().Value, Is.EqualTo(400));
            Assert.That(regular.IsElidable, Is.True, "Regular is left out of a style's name");
            Assert.That(regular.LinkedValue, Is.EqualTo(700), "Regular's bold is Bold");
            Assert.That(Named("Bold", "wght").IsElidable, Is.False);
            Assert.That(Named("Condensed", "wdth").Values.Single().Value, Is.EqualTo(75));
            Assert.That(Named("14pt", "opsz").IsElidable, Is.True, "the default optical size is left out");
            Assert.That(font.AxisValues.Where(v => v.Values.Single().Tag == "wght").Select(v => v.Name),
                Is.EqualTo(new[] { "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })
                    .AsCollection.Or.EquivalentTo(new[] { "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" }));
            Assert.That(font.ElidedFallbackName, Is.EqualTo("Regular"));
        });
    }

    // A style asks for the slant axes as CSS sets them: italic turns 'ital' on or slants by 'slnt' when there is none,
    // oblique slants by 14 degrees or as far as the axis goes.
    [TestCase(FontStyle.Normal, 0f)]
    [TestCase(FontStyle.Italic, -10f)]
    [TestCase(FontStyle.Oblique, -10f)]
    public void AStyle_SlantsTheSlantAxis(FontStyle style, float slant)
    {
        var font = Load("Variations/RobotoFlex-Variable.ttf");

        var variations = FontVariation.For(new FontWeight(400), FontStretch.Normal, style, font);
        var instance = font.GetInstance(variations);

        Assert.That(variations.Single(v => v.Tag == "slnt").Value, Is.EqualTo(slant));
        Assert.That(variations.Any(v => v.Tag == "ital"), Is.False, "Roboto Flex has no 'ital' axis");
        Assert.That(instance.Style, Is.EqualTo(style == FontStyle.Normal ? FontStyle.Normal : FontStyle.Oblique));
    }

    // Text takes the optical size of its own size unless the size was asked for by value.
    [Test]
    public void OpticalSize_FollowsTheTextSize_UnlessAskedForByValue()
    {
        var font = Load("Variations/RobotoFlex-Variable.ttf");
        var bold = font.GetInstance([new FontVariation("wght", 700)]);
        var asked = font.GetInstance([new FontVariation("opsz", 20)]);

        var large = bold.AtOpticalSize(36);

        Assert.Multiple(() =>
        {
            Assert.That(large.Variations.Single(v => v.Tag == "opsz").Value, Is.EqualTo(36));
            Assert.That(large.Variations.Single(v => v.Tag == "wght").Value, Is.EqualTo(700), "the other axes stay");
            Assert.That(asked.AtOpticalSize(36), Is.SameAs(asked));
            Assert.That(font.GetInstance([new FontVariation("opsz", 14)]).AtOpticalSize(36).Variations
                .Single(v => v.Tag == "opsz").Value, Is.EqualTo(14), "asked for at its default, still asked for");
            Assert.That(Load("source-sans-3v028R/VAR/SourceSans3VF-Roman.ttf").AtOpticalSize(36),
                Is.SameAs(Load("source-sans-3v028R/VAR/SourceSans3VF-Roman.ttf")), "no 'opsz' axis");
        });
    }

    [Test]
    public void AFontWithoutStyleAttributes_NamesNoAxisValues()
    {
        var font = Load("TTFFonts/CascadiaCode-Regular.ttf");

        Assert.That(font.AxisValues, Is.Empty);
        Assert.That(font.ElidedFallbackName, Is.Null);
    }

    private static IFont Load(string fontPath) => Fonts.GetOrAdd(fontPath, p => Typeface.LoadFont(p, 3).GetFont(0));

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
