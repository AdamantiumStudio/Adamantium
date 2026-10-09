using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

/// <summary>Words wrapped across lines at their hyphenation points: soft hyphens, or the patterns of the text's
/// language, a hyphen ending the line before the break.</summary>
[TestFixture]
public class HyphenationLayoutTests
{
    private const double FontSize = 20;
    private const char SoftHyphen = '­';

    private static Typeface LoadTypeface()
    {
        return Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "SourceSans3-Regular.ttf"));
    }

    private static TextLayout Layout(string text, double width, Hyphens hyphens, string language = null)
    {
        var typeface = LoadTypeface();
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, Hyphens = hyphens, Language = language };
        layout.ProcessText(text, FontSize, new Size(width, double.NaN), TextWrapping.WrapByWords, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    private static TextLayout Layout(AttributedText text, double width)
    {
        var typeface = LoadTypeface();
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, Hyphens = Hyphens.Auto };
        layout.ProcessText(text, FontSize, new Size(width, double.NaN), TextWrapping.WrapByWords, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    // Wide enough for the text up to character `end` and a hyphen, not for the whole word.
    private static double WidthBefore(string text, int end)
    {
        var layout = Layout(text, double.NaN, Hyphens.None);
        return layout.GetTextData().First(g => g.PositionInString == end).PenX + FontSize * 0.6;
    }

    private static int[] LineOfEachCharacter(TextLayout layout) =>
        layout.GetCaretStops().Take(layout.Text.Length).Select(s => s.LineIndex).ToArray();

    private static GlyphWordData[] HyphenGlyphs(TextLayout layout) =>
        layout.GetTextData().Where(g => g.PositionInString < 0).ToArray();

    [Test]
    public void Auto_BreaksAWordAtItsLastPointThatFits()
    {
        var layout = Layout("hyphenation", WidthBefore("hyphenation", 6), Hyphens.Auto, "en-US");

        Assert.That(layout.LineCount, Is.EqualTo(2));
        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }));
        Assert.That(HyphenGlyphs(layout).Select(g => g.LineIndex), Is.EqualTo(new[] { 0 }), "one hyphen, ending the first line");
    }

    [Test]
    public void TheHyphen_StandsAfterThePrefixAndFitsTheLine()
    {
        var width = WidthBefore("hyphenation", 6);
        var layout = Layout("hyphenation", width, Hyphens.Auto, "en-US");
        var n = layout.GetTextData().Single(g => g.PositionInString == 5);
        var hyphen = HyphenGlyphs(layout).Single();

        Assert.That(hyphen.PenX, Is.EqualTo(n.PenX + n.Advance).Within(1e-3));
        Assert.That(hyphen.Rect.Right, Is.LessThanOrEqualTo(width));
        Assert.That(hyphen.Rect.Width, Is.GreaterThan(0), "drawn");
    }

    [Test]
    public void Auto_BreaksAWordAfterOthersOnTheLine()
    {
        const string text = "a hyphenation";
        var layout = Layout(text, WidthBefore(text, 8), Hyphens.Auto, "en-US");

        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }));
    }

    [Test]
    public void Auto_BreaksALongWordOverSeveralLines()
    {
        const string word = "supercalifragilisticexpialidocious";
        var layout = Layout(word, FontSize * 5, Hyphens.Auto, "en-US");

        Assert.That(layout.LineCount, Is.GreaterThan(2));
        Assert.That(HyphenGlyphs(layout).Length, Is.EqualTo(layout.LineCount - 1), "a hyphen on every line but the last");
    }

    [Test]
    public void Auto_BreaksAWordAfterANewline()
    {
        const string text = "Hello\nhyphenation";
        var layout = Layout(text, WidthBefore("hyphenation", 6), Hyphens.Auto, "en-US");

        Assert.That(LineOfEachCharacter(layout).Skip(6), Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 }));
    }

    [Test]
    public void Auto_TakesTheLanguageOfTheLayout()
    {
        const string word = "переносы";
        var layout = Layout(word, WidthBefore(word, 6), Hyphens.Auto, "ru");

        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 1, 1 }));
    }

    [Test]
    public void Auto_TakesTheLanguageOfTheAttributes()
    {
        const string text = "x переносы";
        var attributed = new AttributedText(text).Apply(2, 8, new TextAttributes { Language = "ru" });
        var layout = Layout(attributed, WidthBefore(text, 8));

        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }));
    }

    [Test]
    public void Auto_WithoutALanguage_KeepsWordsWhole()
    {
        var layout = Layout("a hyphenation", WidthBefore("a hyphenation", 8), Hyphens.Auto);

        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
        Assert.That(HyphenGlyphs(layout), Is.Empty);
    }

    [Test]
    public void Manual_BreaksOnlyAtSoftHyphens()
    {
        var text = $"hy{SoftHyphen}phen{SoftHyphen}ation";
        var width = WidthBefore(text, 8);

        var plain = Layout("hyphenation", WidthBefore("hyphenation", 6), Hyphens.Manual, "en-US");
        var soft = Layout(text, width, Hyphens.Manual, "en-US");

        Assert.That(plain.LineCount, Is.EqualTo(1), "no soft hyphens, no breaks");
        Assert.That(LineOfEachCharacter(soft), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }));
        Assert.That(HyphenGlyphs(soft).Length, Is.EqualTo(1));
    }

    [Test]
    public void Auto_InAWordWithSoftHyphens_BreaksOnlyAtThem()
    {
        var text = $"hy{SoftHyphen}phenation";
        var layout = Layout(text, WidthBefore(text, 7), Hyphens.Auto, "en-US");

        Assert.That(LineOfEachCharacter(layout), Is.EqualTo(new[] { 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
    }

    [Test]
    public void None_KeepsWordsWhole_SoftHyphensIncluded()
    {
        var text = $"a hy{SoftHyphen}phen{SoftHyphen}ation";
        var layout = Layout(text, WidthBefore(text, 10), Hyphens.None, "en-US");

        Assert.That(LineOfEachCharacter(layout).Skip(2), Is.All.EqualTo(1));
        Assert.That(HyphenGlyphs(layout), Is.Empty);
    }

    [Test]
    public void ASoftHyphenInsideALine_DrawsNothing()
    {
        var text = $"hy{SoftHyphen}phen";
        var layout = Layout(text, double.NaN, Hyphens.Manual);
        var soft = layout.GetTextData().Single(g => g.PositionInString == 2);

        Assert.That(soft.Advance, Is.Zero);
        Assert.That(HyphenGlyphs(layout), Is.Empty);
    }

    [Test]
    public void AWordThatFits_IsNotBroken()
    {
        var layout = Layout("a hyphenation", double.NaN, Hyphens.Auto, "en-US");

        Assert.That(layout.LineCount, Is.EqualTo(1));
        Assert.That(HyphenGlyphs(layout), Is.Empty);
    }
}
