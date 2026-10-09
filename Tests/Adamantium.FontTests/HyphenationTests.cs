using System.Collections.Generic;
using System.IO;
using System.Text;
using Adamantium.Fonts.Text;
using NUnit.Framework;

namespace Adamantium.FontTests;

/// <summary>Where the built-in hyphenators break words, against NHyphenator over the same hyph-utf8 patterns
/// (Hyphenation/expected.txt, written by Adamantium.HyphenationOracle).</summary>
public class HyphenationTests
{
    public static IEnumerable<TestCaseData> Words()
    {
        foreach (var line in File.ReadAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Hyphenation", "expected.txt")))
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                var fields = line.Split('\t');
                yield return new TestCaseData(fields[0], fields[1], fields[2]).SetName($"{fields[0]} {fields[1]}");
            }
        }
    }

    [TestCaseSource(nameof(Words))]
    public void Hyphenate_IsTheOracles(string language, string word, string expected)
    {
        var points = Hyphenator.ForLanguage(language).Hyphenate(word);

        Assert.That(string.Join(",", points), Is.EqualTo(expected), Marked(word, points));
    }

    [TestCase("ru-RU", "ru")]
    [TestCase("en", "en")]
    [TestCase("en-US", "en")]
    [TestCase("en_GB", "en-gb")]
    [TestCase("en-AU", "en-gb")]
    [TestCase("en-GB-oxendict", "en-gb")]
    [TestCase("en-Latn-GB", "en-gb")]
    [TestCase("de-AT", "de")]
    [TestCase("fr-CA", "fr")]
    [TestCase("es-MX", "es")]
    public void ForLanguage_FindsTheBuiltIn(string tag, string builtIn)
    {
        Assert.That(Hyphenator.ForLanguage(tag), Is.SameAs(Hyphenator.ForLanguage(builtIn)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ja")]
    [TestCase("zh-Hans")]
    public void ForLanguage_WithoutPatterns_IsNull(string tag)
    {
        Assert.That(Hyphenator.ForLanguage(tag), Is.Null);
    }

    [Test]
    public void ForLanguage_BritishEnglish_IsNotAmerican()
    {
        Assert.That(Hyphenator.ForLanguage("en-GB"), Is.Not.SameAs(Hyphenator.ForLanguage("en-US")));
    }

    [Test]
    public void Register_TakesPrecedenceOverTheBuiltIn()
    {
        var custom = Hyphenator.Load(new MemoryStream(Encoding.UTF8.GetBytes("\\patterns{ b1a }")));
        Hyphenator.Register("xx", custom);

        Assert.That(Hyphenator.ForLanguage("xx-YY"), Is.SameAs(custom));
        Assert.That(custom.Hyphenate("abab"), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void Load_ReadsHyphenMinsAndExceptions()
    {
        const string source = "% hyphenmins:\n%     left: 1\n%     right: 3\n\\patterns{ 1b }\n\\hyphenation{ ab-c-de }";
        var hyphenator = Hyphenator.Load(new MemoryStream(Encoding.UTF8.GetBytes(source)));

        Assert.That((hyphenator.LeftMin, hyphenator.RightMin), Is.EqualTo((1, 3)));
        Assert.That(hyphenator.Hyphenate("ABCDE"), Is.EqualTo(new[] { 2 }), "the exception, its last point too near the end");
        Assert.That(hyphenator.Hyphenate("abbbb"), Is.EqualTo(new[] { 1, 2 }), "the patterns");
    }

    [TestCase("associate", "as-so-ciate")]
    [TestCase("associates", "as-so-ciates")]
    [TestCase("Project", "Project")]
    [TestCase("table", "ta-ble")]
    [TestCase("reciprocity", "reci-procity")]
    public void Hyphenate_TakesTheExceptionsOfThePatternFile(string word, string expected)
    {
        Assert.That(Marked(word, Hyphenator.ForLanguage("en-US").Hyphenate(word)), Is.EqualTo(expected));
    }

    [Test]
    public void Hyphenate_KeepsTheCaseOfTheWord()
    {
        var russian = Hyphenator.ForLanguage("ru");

        Assert.That(russian.Hyphenate("ПЕРЕНОСЫ"), Is.EqualTo(russian.Hyphenate("переносы")));
    }

    private static string Marked(string word, IReadOnlyList<int> points)
    {
        var marked = new StringBuilder(word);
        for (var i = points.Count - 1; i >= 0; i--)
        {
            marked.Insert(points[i], '-');
        }

        return marked.ToString();
    }
}
