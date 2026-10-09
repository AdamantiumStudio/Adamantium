using System;
using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

/// <summary>Tabs moving the text to its tab stops: left, centered, right and decimal, with leaders lined up from line
/// to line, and every <see cref="TextLayout.TabSize"/> spaces past the last stop.</summary>
[TestFixture]
public class TabStopLayoutTests
{
    private const double FontSize = 20;

    private static readonly Lazy<Typeface> SourceSans = new(() =>
        Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "SourceSans3-Regular.ttf")));

    private static TextLayout Layout(string text, params TabStop[] stops)
    {
        var typeface = SourceSans.Value;
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, TabStops = stops };
        layout.ProcessText(text, FontSize, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    private static GlyphWordData At(TextLayout layout, int index) =>
        layout.GetTextData().Single(g => g.PositionInString == index);

    private static double End(TextLayout layout, int index)
    {
        var glyph = At(layout, index);
        return glyph.PenX + glyph.Advance;
    }

    [Test]
    public void WithoutStops_ATabStopsEveryTabSizeSpaces()
    {
        var layout = Layout("a\tb");
        var space = SourceSans.Value.Fonts[0].GetAdvanceWidth(SourceSans.Value.Fonts[0].GetGlyphByCharacter(' ').Index)
                    * FontSize / SourceSans.Value.Fonts[0].UnitsPerEm;

        Assert.That(At(layout, 2).PenX, Is.EqualTo(4 * space).Within(1e-3));
    }

    [Test]
    public void ALeftStop_StartsTheTextAtIt()
    {
        Assert.That(At(Layout("a\tb", new TabStop(100)), 2).PenX, Is.EqualTo(100).Within(1e-3));
    }

    [Test]
    public void ARightStop_EndsTheTextAtIt()
    {
        Assert.That(End(Layout("x\tabc", new TabStop(200, TabAlignment.Right)), 4), Is.EqualTo(200).Within(1e-3));
    }

    [Test]
    public void ACenterStop_CentersTheTextOnIt()
    {
        var layout = Layout("x\tabc", new TabStop(200, TabAlignment.Center));

        Assert.That((At(layout, 2).PenX + End(layout, 4)) / 2, Is.EqualTo(200).Within(1e-3));
    }

    [Test]
    public void ADecimalStop_SetsTheSeparatorAtIt()
    {
        Assert.That(At(Layout("x\t12.50", new TabStop(200, TabAlignment.Decimal)), 4).PenX, Is.EqualTo(200).Within(1e-3));
        Assert.That(At(Layout("x\t12,50", new TabStop(200, TabAlignment.Decimal, alignOn: ',')), 4).PenX,
            Is.EqualTo(200).Within(1e-3));
        Assert.That(End(Layout("x\t1250", new TabStop(200, TabAlignment.Decimal)), 5), Is.EqualTo(200).Within(1e-3),
            "without a separator the text ends at the stop");
    }

    [Test]
    public void PastTheLastStop_TabsGoBackToTabSize()
    {
        var layout = Layout("a\tb\tc", new TabStop(50));

        Assert.That(At(layout, 2).PenX, Is.EqualTo(50).Within(1e-3));
        Assert.That(At(layout, 4).PenX, Is.GreaterThan(End(layout, 2)));
    }

    [Test]
    public void Leaders_FillTheGap_LinedUpFromLineToLine()
    {
        var layout = Layout("a\tb\nlonger\tb", new TabStop(200, TabAlignment.Left, "."));
        var dots = layout.GetTextData().Where(g => g.PositionInString < 0).ToArray();
        var unit = dots[0].Advance;

        Assert.That(dots.Select(g => g.LineIndex).Distinct(), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(dots.All(g => g.Symbol == '.' && g.PenX + g.Advance <= 200 + 1e-3));
        Assert.That(dots.All(g => Math.Abs(g.PenX / unit - Math.Round(g.PenX / unit)) < 1e-6), "on one grid");
        Assert.That(layout.GetCaretStops()[2].X, Is.EqualTo(200).Within(1e-3), "the caret before b stands at the stop");
    }

    private static TextLayout Layout(string text, double width, TextWrapping wrapping, TextTrimming trimming,
        HorizontalTextAlignment alignment, LineBreaking breaking, bool optical, params TabStop[] stops)
    {
        var typeface = SourceSans.Value;
        var layout = new TextLayout(typeface, typeface.Fonts[0])
        {
            Fallback = null,
            TabStops = stops,
            LineBreaking = breaking,
            OpticalMarginAlignment = optical,
        };
        layout.ProcessText(text, FontSize, new Size(width, double.NaN), wrapping, trimming, alignment, VerticalTextAlignment.Top);
        return layout;
    }

    [Test]
    public void ALeader_DoesNotPushTheTextOnInALineWithRightToLeftText()
    {
        Assert.That(At(Layout("ab\tcd אב", new TabStop(200, TabAlignment.Left, ".")), 3).PenX,
            Is.EqualTo(200).Within(1e-3));
    }

    [Test]
    public void TrimmingALineWithALeader_KeepsTheEllipsisInside()
    {
        var layout = Layout("Intro\tPage 12345678", 60, TextWrapping.NoWrap, TextTrimming.CharEllipses,
            HorizontalTextAlignment.Left, LineBreaking.Greedy, false, new TabStop(100, TabAlignment.Left, "."));

        Assert.That(layout.GetTextData().Min(g => g.PenX), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void WrappingBySymbols_KeepsTheLeaderOnTheTabsLine()
    {
        var layout = Layout("ab\tcdefghij", 120, TextWrapping.WrapBySymbols, TextTrimming.None,
            HorizontalTextAlignment.Left, LineBreaking.Greedy, false, new TabStop(100, TabAlignment.Left, "."));
        var tab = At(layout, 2);

        Assert.That(layout.GetTextData().Where(g => g.PositionInString < 0).Select(g => g.LineIndex),
            Is.All.EqualTo(tab.LineIndex));
    }

    [Test]
    public void AParagraphWithTabs_KeepsItsLinesWithinTheWidth([Values] HorizontalTextAlignment alignment)
    {
        const string text = "Name\tsome words that go on and on and wrap over several lines of the column";
        var layout = Layout(text, 300, TextWrapping.WrapByWords, TextTrimming.None, alignment, LineBreaking.Paragraph,
            false, new TabStop(200));

        Assert.That(layout.GetTextData().Where(g => g.Symbol is not (' ' or '\t')).Max(g => g.Rect.Right),
            Is.LessThanOrEqualTo(300.5));
    }

    [Test]
    public void AJustifiedLine_StretchesOnlyTheSpacesAfterItsLastTab()
    {
        const string text = "one two three four\tfive six seven eight nine ten eleven twelve";
        var layout = Layout(text, 300, TextWrapping.WrapByWords, TextTrimming.None, HorizontalTextAlignment.Justify,
            LineBreaking.Greedy, false, new TabStop(200));

        Assert.That(At(layout, text.IndexOf('\t') - 1).PenX + At(layout, text.IndexOf('\t') - 1).Advance,
            Is.LessThan(200), "the words before the tab keep their spacing");
        Assert.That(At(layout, text.IndexOf('\t') + 1).PenX, Is.EqualTo(200).Within(1e-3));
    }

    [TestCase("“Pears”\t2.25", 9)]
    [TestCase("\t-3.75", 3)]
    public void OpticalMargins_LeaveTheTabColumnWhereItIs(string text, int separator)
    {
        var layout = Layout(text, 300, TextWrapping.WrapByWords, TextTrimming.None, HorizontalTextAlignment.Left,
            LineBreaking.Greedy, true, new TabStop(200, TabAlignment.Decimal));

        Assert.That(At(layout, separator).PenX, Is.EqualTo(200).Within(1e-3));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-1)]
    public void AStopAtNoPlace_IsRejected(double position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TabStop(position));
    }

    [Test]
    public void ChangingTheStops_LaysTheTextOutAgain()
    {
        var typeface = SourceSans.Value;
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, TabStops = [new TabStop(100)] };
        layout.ProcessText("a\tb", FontSize, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        layout.TabStops = [new TabStop(150)];
        layout.ProcessText("a\tb", FontSize, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);

        Assert.That(At(layout, 2).PenX, Is.EqualTo(150).Within(1e-3));
    }
}
