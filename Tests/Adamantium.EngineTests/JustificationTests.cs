using System;
using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

/// <summary>Justified text: lines fill the width and the last line of a paragraph stays at its start, on the right in a
/// right-to-left paragraph; the caret, a click and a selection reach across the widened gaps.</summary>
[TestFixture]
public class JustificationTests
{
    private const double FontSize = 20;
    private const double Width = 300;

    private const string Prose =
        "In olden times when wishing still helped one, there lived a king whose daughters were all beautiful, but " +
        "the youngest was so beautiful that the sun itself, which has seen so much, was astonished whenever it shone " +
        "in her face.";

    private const string Hebrew =
        "בראשית ברא אלהים את השמים ואת הארץ. והארץ היתה תהו ובהו וחשך על פני תהום ורוח אלהים מרחפת על פני המים.";

    private const string PointedHebrew =
        "בְּרֵאשִׁית בָּרָא אֱלֹהִים אֵת הַשָּׁמַיִם וְאֵת הָאָרֶץ. וְהָאָרֶץ הָיְתָה תֹהוּ וָבֹהוּ וְחֹשֶׁךְ עַל פְּנֵי תְהוֹם.";

    private static TextLayout Lay(string text, string font, LineBreaking breaking = LineBreaking.Greedy,
        HorizontalTextAlignment alignment = HorizontalTextAlignment.Justify)
    {
        var typeface = Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", font));
        var layout = new TextLayout(typeface, typeface.Fonts[0])
        {
            Fallback = null, LineBreaking = breaking, EmitNewlineCarets = true,
        };
        layout.ProcessText(text, FontSize, new Size(Width, double.NaN), TextWrapping.WrapByWords, TextTrimming.None,
            alignment, VerticalTextAlignment.Top);
        return layout;
    }

    [TestCase(HorizontalTextAlignment.Left, 0)]
    [TestCase(HorizontalTextAlignment.Center, Width / 2)]
    [TestCase(HorizontalTextAlignment.Right, Width)]
    [TestCase(HorizontalTextAlignment.Justify, 0)]
    public void TheCaretOnAnEmptyLastLine_StandsWhereTheAlignmentPutsText(HorizontalTextAlignment alignment, double x)
    {
        var stops = Lay("abc\n", "SourceSans3-Regular.ttf", alignment: alignment).GetCaretStops();

        Assert.That(stops[4].X, Is.EqualTo(x).Within(0.5));
        Assert.That(stops[4].LineIndex, Is.EqualTo(1));
    }

    private static (double Left, double Right) Ink(TextLayout layout, int line)
    {
        var glyphs = layout.GetTextData().Where(glyph => glyph.LineIndex == line && glyph.Symbol != ' ').ToArray();
        return (glyphs.Min(glyph => glyph.Rect.Left), glyphs.Max(glyph => glyph.Rect.Right));
    }

    [TestCase("SourceSans3-Regular.ttf", Prose, LineBreaking.Greedy)]
    [TestCase("SourceSans3-Regular.ttf", Prose, LineBreaking.Paragraph)]
    [TestCase("NotoSansHebrew-Regular.ttf", Hebrew, LineBreaking.Greedy)]
    [TestCase("NotoSansHebrew-Regular.ttf", PointedHebrew, LineBreaking.Greedy)]
    public void LinesFillTheWidth_TheLastStaysAtItsStart(string font, string text, LineBreaking breaking)
    {
        var layout = Lay(text, font, breaking);
        var rightToLeft = layout.IsRightToLeftParagraph(0);
        var last = layout.LineCount - 1;

        Assert.That(last, Is.GreaterThan(1));
        for (var line = 0; line < last; line++)
        {
            var (left, right) = Ink(layout, line);
            Assert.That(left, Is.EqualTo(0).Within(2), $"line {line} starts at the left");
            Assert.That(right, Is.EqualTo(Width).Within(2), $"line {line} reaches the right");
        }

        var (lastLeft, lastRight) = Ink(layout, last);
        Assert.That(lastRight - lastLeft, Is.LessThan(Width - 20), "the last line is short");
        Assert.That(rightToLeft ? lastRight : lastLeft, Is.EqualTo(rightToLeft ? Width : 0).Within(2),
            "the last line stands at the start of the paragraph");
    }

    [TestCase("SourceSans3-Regular.ttf", Prose)]
    [TestCase("NotoSansHebrew-Regular.ttf", Hebrew)]
    public void CaretStops_LeaveNoGapsInAJustifiedLine(string font, string text)
    {
        var layout = Lay(text, font);
        var stops = layout.GetCaretStops();

        for (var line = 0; line < layout.LineCount - 1; line++)
        {
            var metrics = layout.GetLine(line);
            var covered = Enumerable.Range(metrics.Start, metrics.End - metrics.Start)
                .Select(index => stops[index]).OrderBy(stop => stop.Left).ToArray();
            for (var k = 1; k < covered.Length; k++)
            {
                Assert.That(covered[k].Left, Is.EqualTo(covered[k - 1].Right).Within(0.01),
                    $"line {line}: no gap before the stop at {covered[k].Left}");
            }
        }
    }

    [TestCase("SourceSans3-Regular.ttf", Prose)]
    [TestCase("NotoSansHebrew-Regular.ttf", Hebrew)]
    public void AClickInAWidenedGap_LandsOnItsSpace(string font, string text)
    {
        var layout = Lay(text, font);
        var stops = layout.GetCaretStops();
        var line = layout.GetLine(0);
        var space = Enumerable.Range(line.Start, line.End - line.Start).First(index => text[index] == ' ');
        var gapMiddle = (stops[space].Left + stops[space].Right) / 2;

        var hit = layout.HitTest(gapMiddle, line.Top + line.Height / 2);

        Assert.That(hit.Index, Is.EqualTo(space));
        Assert.That(hit.IsInside, Is.True);
        Assert.That(stops[space].Width, Is.GreaterThan(FontSize / 4), "the space is widened");
    }

    [Test]
    public void ASelectedLine_IsCoveredInOnePiece()
    {
        var layout = Lay(Prose, "SourceSans3-Regular.ttf");
        var line = layout.GetLine(0);

        var rects = layout.GetRangeRects(line.Start, line.End);

        Assert.That(rects, Has.Count.EqualTo(1));
        Assert.That(rects[0].Right, Is.GreaterThan(Width - 2));
    }
}
