using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

/// <summary>Text wrapped by words and trimmed: a word too wide for its line, and the last line the height leaves room
/// for, end in an ellipsis.</summary>
[TestFixture]
public class WrappedTrimmingTests
{
    private const double FontSize = 20;
    private const double Width = 120;
    private const string LongWord = "Supercalifragilisticexpialidocious";

    private static TextLayout Layout(string text, TextTrimming trimming, double height = double.NaN)
    {
        var typeface = Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "SourceSans3-Regular.ttf"));
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, Hyphens = Hyphens.None };
        layout.ProcessText(text, FontSize, new Size(Width, height), TextWrapping.WrapByWords, trimming,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    private static GlyphWordData[] Dots(TextLayout layout) =>
        layout.GetTextData().Where(g => g.PositionInString < 0 && g.Symbol == '.').ToArray();

    [TestCase(TextTrimming.CharEllipses)]
    [TestCase(TextTrimming.WordEllipses)]
    public void AWordWiderThanItsLine_EndsInAnEllipsis_AndTheTextGoesOn(TextTrimming trimming)
    {
        var layout = Layout($"{LongWord} next", trimming);
        var data = layout.GetTextData();

        Assert.That(Dots(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(data.Where(g => g.LineIndex == 0).Max(g => g.Rect.Right), Is.LessThanOrEqualTo(Width));
        Assert.That(data.Single(g => g.PositionInString == LongWord.Length + 1).LineIndex, Is.EqualTo(1), "the next word below");
    }

    [Test]
    public void WithoutTrimming_AWordWiderThanItsLine_Overflows()
    {
        var layout = Layout($"{LongWord} next", TextTrimming.None);

        Assert.That(Dots(layout), Is.Empty);
        Assert.That(layout.GetTextData().Where(g => g.LineIndex == 0).Max(g => g.Rect.Right), Is.GreaterThan(Width));
    }

    [TestCase(TextTrimming.CharEllipses)]
    [TestCase(TextTrimming.WordEllipses)]
    public void TheLastLineTheHeightAllows_EndsInAnEllipsis(TextTrimming trimming)
    {
        const string text = "the quick brown fox jumps over the lazy dog and keeps running far away";
        var lineHeight = Layout(text, TextTrimming.None).GetLine(0).Height;
        var layout = Layout(text, trimming, lineHeight * 2);
        var data = layout.GetTextData();

        Assert.That(data.Max(g => g.LineIndex), Is.EqualTo(1), "two lines");
        Assert.That(Dots(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(data.Max(g => g.Rect.Right), Is.LessThanOrEqualTo(Width));
    }

    // A line of pieces without spaces between them, as a link breaks after its slashes: the earlier lines stay.
    [Test]
    public void WordEllipses_OnALastLineWithoutSpaces_CutsByCharacters()
    {
        const string text = "See https://example.com/aaaa/bbbb/cccc/dddd/eeee/ffff/gggg";
        var lineHeight = Layout(text, TextTrimming.None).GetLine(0).Height;
        var layout = Layout(text, TextTrimming.WordEllipses, lineHeight * 2);
        var data = layout.GetTextData();

        Assert.That(data.Where(g => g.LineIndex == 0).Select(g => g.PositionInString).Take(3), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(Dots(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(data.Min(g => g.PenX), Is.GreaterThanOrEqualTo(0));
        Assert.That(data.Count(g => g.LineIndex == 1 && g.PositionInString >= 0), Is.GreaterThan(0), "the line keeps what fits");
    }

    [Test]
    public void AWordAfterANewline_IsTrimmed()
    {
        var layout = Layout($"Title\n{LongWord} end", TextTrimming.CharEllipses);
        var data = layout.GetTextData();

        Assert.That(Dots(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(data.Max(g => g.Rect.Right), Is.LessThanOrEqualTo(Width));
    }

    [Test]
    public void AWordBeforeANewline_IsMeasuredWithoutWhatFollowsIt()
    {
        var text = $"aa Hello\n{LongWord}";
        var layout = Layout(text, TextTrimming.None);

        Assert.That(layout.GetTextData().Single(g => g.PositionInString == 3).LineIndex, Is.EqualTo(0));
    }

    [Test]
    public void NewlinesPastTheHeight_EndTheLastLineInAnEllipsis()
    {
        const string text = "aa\nbb\ncc\ndd";
        var lineHeight = Layout(text, TextTrimming.None).GetLine(0).Height;
        var layout = Layout(text, TextTrimming.CharEllipses, lineHeight * 2);

        Assert.That(layout.GetTextData().Max(g => g.LineIndex), Is.EqualTo(1));
        Assert.That(Dots(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 1, 1, 1 }));
    }

    [Test]
    public void WordEllipses_OnTheLastLine_DropsTheWordThatDoesNotFit()
    {
        const string text = "the quick brown fox jumps over the lazy dog and keeps running far away";
        var lineHeight = Layout(text, TextTrimming.None).GetLine(0).Height;
        var layout = Layout(text, TextTrimming.WordEllipses, lineHeight * 2);
        var lastLetter = layout.GetTextData().Where(g => g.PositionInString >= 0 && g.LineIndex == 1)
            .Max(g => g.PositionInString);

        Assert.That(text[lastLetter + 1], Is.EqualTo(' '), "the line ends with a whole word");
    }
}
