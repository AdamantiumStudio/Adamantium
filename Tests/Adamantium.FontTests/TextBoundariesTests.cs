using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Adamantium.Fonts.Text;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class TextBoundariesTests
{
    public static IEnumerable<TestCaseData> GraphemeCases() => Cases("GraphemeBreakTest.txt");

    public static IEnumerable<TestCaseData> WordCases() => Cases("WordBreakTest.txt");

    public static IEnumerable<TestCaseData> LineCases() => Cases("LineBreakTest.txt");

    [TestCaseSource(nameof(LineCases))]
    public void LinesBreakAsUnicodeSays(string text, bool[] expected)
    {
        var kinds = TextBoundaries.LineBreaks(text);
        var breaks = new bool[kinds.Length];
        for (var i = 0; i < kinds.Length; i++)
        {
            breaks[i] = kinds[i] != LineBreakKind.None;
        }

        Assert.That(breaks, Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(GraphemeCases))]
    public void GraphemesBreakAsUnicodeSays(string text, bool[] expected)
    {
        Assert.That(TextBoundaries.Graphemes(text), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(WordCases))]
    public void WordsBreakAsUnicodeSays(string text, bool[] expected)
    {
        Assert.That(TextBoundaries.Words(text), Is.EqualTo(expected));
    }

    [Test]
    public void NextAndPrevious_StepOverAWholeEmojiAndALetterWithMarks()
    {
        const string text = "a\U0001F468‍\U0001F469b éx";
        var graphemes = TextBoundaries.Graphemes(text);

        Assert.That(TextBoundaries.Next(graphemes, 1), Is.EqualTo(6), "the family emoji is one step");
        Assert.That(TextBoundaries.Previous(graphemes, 6), Is.EqualTo(1));
        Assert.That(TextBoundaries.Next(graphemes, 8), Is.EqualTo(10), "e and its accent are one step");
    }

    private static IEnumerable<TestCaseData> Cases(string file)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Unicode", file);
        var number = 0;
        foreach (var line in File.ReadAllLines(path))
        {
            number++;
            var data = line.Split('#')[0].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            var text = new StringBuilder();
            var expected = new List<bool>();
            foreach (var token in data.Split(' '))
            {
                if (token == "÷" || token == "×")
                {
                    expected.Add(token == "÷");
                    continue;
                }

                var codepoint = char.ConvertFromUtf32(int.Parse(token, NumberStyles.HexNumber));
                text.Append(codepoint);
                if (codepoint.Length == 2)
                {
                    expected.Add(false);
                }
            }

            yield return new TestCaseData(text.ToString(), expected.ToArray()).SetName($"{file} line {number}: {data}");
        }
    }
}
