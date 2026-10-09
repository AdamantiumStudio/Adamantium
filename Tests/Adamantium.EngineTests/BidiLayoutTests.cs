using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

/// <summary>Text of both directions laid out: each line in visual order by the Unicode Bidirectional Algorithm, the
/// caret before a right-to-left character on its right edge, selections and clicks where the characters stand.</summary>
[TestFixture]
public class BidiLayoutTests
{
    private const string Hebrew = "שלום";

    private static TextLayout Layout(string text, TextDirection direction = TextDirection.Auto)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "NotoSansHebrew-Regular.ttf");
        var typeface = Typeface.LoadFont(path);
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null, Direction = direction };
        layout.ProcessText(text, 20, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    private static int[] VisualClusters(TextLayout layout) =>
        layout.GetTextData().OrderBy(g => g.PenX).Select(g => g.PositionInString).ToArray();

    private static CaretPosition ToPosition(TextHit hit) => new(hit.CaretIndex, hit.IsTrailing);

    private static double Point(TextLayout layout, TextHit hit) => layout.GetCaretPoint(ToPosition(hit)).X;

    [Test]
    public void AHebrewWordInALeftToRightLine_RunsRightToLeft()
    {
        var layout = Layout($"abc {Hebrew} def");

        Assert.That(VisualClusters(layout), Is.EqualTo(new[] { 0, 1, 2, 3, 7, 6, 5, 4, 8, 9, 10, 11 }));
    }

    // Auto takes the paragraph's direction from its first strong character: Hebrew, so the line runs right to left. The
    // number after the Latin word takes the word's direction (W7), and the two keep their order, left to right.
    [Test]
    public void ARightToLeftParagraph_KeepsLatinAndNumbersLeftToRight()
    {
        var layout = Layout($"{Hebrew} abc 123");

        Assert.That(VisualClusters(layout), Is.EqualTo(new[] { 5, 6, 7, 8, 9, 10, 11, 4, 3, 2, 1, 0 }));
    }

    // A paragraph asked to run right to left keeps a Latin phrase left to right, and puts its closing period, at the
    // paragraph's own level, on the phrase's left.
    [Test]
    public void AParagraphAskedToRunRightToLeft_PutsTheClosingPeriodOnTheLeft()
    {
        var layout = Layout("abc, def.", TextDirection.RightToLeft);

        Assert.That(VisualClusters(layout), Is.EqualTo(new[] { 8, 0, 1, 2, 3, 4, 5, 6, 7 }));
    }

    // A paragraph separator that does not break the line still starts a paragraph of its own: the Hebrew after it
    // runs right to left.
    [Test]
    public void AParagraphSeparatorInsideALine_StartsAParagraphOfItsOwn()
    {
        var layout = Layout("ab" + (char)0x2029 + Hebrew);

        var hebrew = VisualClusters(layout).Where(c => c > 2).ToArray();

        Assert.That(hebrew, Is.EqualTo(new[] { 6, 5, 4, 3 }));
    }

    [Test]
    public void ALoneSurrogate_IsLaidOut()
    {
        Assert.DoesNotThrow(() => Layout("x" + (char)0xD83D + "y"));
        Assert.DoesNotThrow(() => Layout(Hebrew + (char)0xDE00));
    }

    // Left is where a line starts: the right edge for a right-to-left paragraph, the left for the other.
    [Test]
    public void LeftAlignment_PutsARightToLeftLineOnTheRightEdge()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "NotoSansHebrew-Regular.ttf");
        var typeface = Typeface.LoadFont(path);
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null };
        layout.ProcessText($"{Hebrew}\nabc", 20, new Size(300, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        var glyphs = layout.GetTextData();

        Assert.Multiple(() =>
        {
            Assert.That(glyphs.Where(g => g.LineIndex == 0).Max(g => g.Rect.Right), Is.EqualTo(300).Within(0.5));
            Assert.That(glyphs.Where(g => g.LineIndex == 1).Min(g => g.PenX), Is.EqualTo(0).Within(1e-6));
            Assert.That(layout.IsRightToLeftParagraph(0));
            Assert.That(layout.IsRightToLeftParagraph(Hebrew.Length + 1), Is.False);
        });
    }

    [Test]
    public void TheCaretBeforeARightToLeftCharacter_StandsOnItsRightEdge()
    {
        var layout = Layout(Hebrew);
        var stops = layout.GetCaretStops();
        var first = layout.GetTextData().Single(g => g.PositionInString == 0);

        Assert.Multiple(() =>
        {
            Assert.That(stops[0].IsRightToLeft);
            Assert.That(stops[0].X, Is.EqualTo(first.PenX + first.Advance).Within(1e-6));
            Assert.That(stops.Take(Hebrew.Length).Select(s => s.X), Is.Ordered.Descending);
            Assert.That(stops[Hebrew.Length].X, Is.EqualTo(stops[Hebrew.Length - 1].Left).Within(1e-6),
                "after the last character, on its left edge");
        });
    }

    [Test]
    public void TheCaretAfterARightToLeftCharacter_StandsOnItsLeftEdge()
    {
        var layout = Layout($"abc {Hebrew}");
        var stops = layout.GetCaretStops();

        Assert.Multiple(() =>
        {
            Assert.That(stops[4].After, Is.EqualTo(stops[4].Left));
            Assert.That(stops[0].After, Is.EqualTo(stops[1].X));
        });
    }

    // The end of the text stands where its paragraph ends, not after its last character in the middle of the line.
    [Test]
    public void TheCaretAtTheEnd_StandsOnTheEdgeTheParagraphEndsOn()
    {
        var text = $"abc {Hebrew}";
        var layout = Layout(text);
        var rightToLeft = Layout($"{Hebrew} abc");
        var stops = rightToLeft.GetCaretStops();

        Assert.Multiple(() =>
        {
            Assert.That(layout.GetCaretStops()[text.Length].X, Is.EqualTo(layout.GetLine(0).Width).Within(1e-6));
            Assert.That(stops[text.Length].X, Is.EqualTo(stops[Hebrew.Length + 1].Left).Within(1e-6),
                "on the left of the Latin word");
        });
    }

    // A line feed ends a right-to-left paragraph: the caret on the empty line after it starts on the right.
    [Test]
    public void TheEmptyLastLineOfARightToLeftParagraph_StartsOnTheRight()
    {
        var layout = Layout($"{Hebrew}\n", TextDirection.RightToLeft);
        var stops = layout.GetCaretStops();

        Assert.That(stops[Hebrew.Length + 1].X, Is.EqualTo(stops.Take(Hebrew.Length).Max(s => s.Right)).Within(1e-6));
    }

    // White space where a line wraps stands at the paragraph's level (L1): its caret box is where its glyph is drawn.
    [Test]
    public void EveryCaretBox_StandsWhereItsGlyphIsDrawn()
    {
        var text = $"abc {Hebrew} {Hebrew} {Hebrew} {Hebrew} def";
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "NotoSansHebrew-Regular.ttf");
        var typeface = Typeface.LoadFont(path);
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { Fallback = null };
        layout.ProcessText(text, 20, new Size(120, double.NaN), TextWrapping.WrapByWords, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        var stops = layout.GetCaretStops();

        Assert.Multiple(() =>
        {
            foreach (var glyph in layout.GetTextData().Where(g => g.PositionInString >= 0 && g.Advance > 0))
            {
                var stop = stops[glyph.PositionInString];
                Assert.That(stop.Left, Is.EqualTo(glyph.PenX).Within(1e-3), $"left of {glyph.PositionInString}");
                Assert.That(stop.Right, Is.EqualTo(glyph.PenX + glyph.Advance).Within(1e-3),
                    $"right of {glyph.PositionInString}");
            }
        });
    }

    // Selecting across the change of direction covers two pieces of the line: the Hebrew on the right of the space
    // and the start of the Latin word on its left.
    [Test]
    public void ASelectionAcrossDirections_CoversEachPieceOfTheLine()
    {
        var layout = Layout($"{Hebrew} abc", TextDirection.RightToLeft);

        var rects = layout.GetRangeRects(0, 6);

        Assert.That(rects, Has.Count.EqualTo(2));
    }

    [Test]
    public void AClickOnTheRightHalfOfAHebrewLetter_PutsTheCaretBeforeIt()
    {
        var layout = Layout(Hebrew);
        var first = layout.GetTextData().Single(g => g.PositionInString == 0);
        var line = layout.GetLine(0);

        var right = layout.HitTest(first.PenX + first.Advance * 0.9, line.Top + line.Height / 2);
        var left = layout.HitTest(first.PenX + first.Advance * 0.1, line.Top + line.Height / 2);

        Assert.Multiple(() =>
        {
            Assert.That(right.CaretIndex, Is.EqualTo(0));
            Assert.That(left.CaretIndex, Is.EqualTo(1));
        });
    }

    // A line ending in Hebrew ends on screen with the word's first letter: a click past it stands on the line's edge.
    [TestCase(TextDirection.LeftToRight)]
    [TestCase(TextDirection.RightToLeft)]
    public void AClickBesideALine_PutsTheCaretOnTheLineEdge(TextDirection direction)
    {
        var layout = Layout($"abc {Hebrew}", direction);
        var line = layout.GetLine(0);
        var y = line.Top + line.Height / 2;
        var stops = layout.GetCaretStops();
        var left = stops.Take(stops.Length - 1).Min(s => s.Left);
        var right = stops.Take(stops.Length - 1).Max(s => s.Right);

        var pastRight = layout.HitTest(right + 50, y);
        var pastLeft = layout.HitTest(left - 50, y);

        Assert.Multiple(() =>
        {
            Assert.That(Point(layout, pastRight), Is.EqualTo(right).Within(1e-6));
            Assert.That(Point(layout, pastLeft), Is.EqualTo(left).Within(1e-6));
        });
    }

    // Typing after End or a click past the line appends: the caret goes to the text's end, which stands on the edge the
    // paragraph ends on, not before the Hebrew word that ends the line on screen.
    [TestCase(TextDirection.LeftToRight)]
    [TestCase(TextDirection.RightToLeft)]
    public void TheLineEnd_IsTheEndOfTheText(TextDirection direction)
    {
        var text = $"abc {Hebrew}";
        var layout = Layout(text, direction);
        var line = layout.GetLine(0);
        var stops = layout.GetCaretStops();
        var endSide = direction == TextDirection.RightToLeft
            ? stops.Take(text.Length).Min(s => s.Left) - 50
            : stops.Take(text.Length).Max(s => s.Right) + 50;

        Assert.Multiple(() =>
        {
            Assert.That(layout.GetLineEnd(0).Index, Is.EqualTo(text.Length));
            Assert.That(layout.HitTest(endSide, line.Top + line.Height / 2).CaretIndex, Is.EqualTo(text.Length));
        });
    }

    [TestCase(TextDirection.LeftToRight)]
    [TestCase(TextDirection.RightToLeft)]
    public void StepsToTheRight_CrossEveryLetterOnScreenFromLeftToRight(TextDirection direction)
    {
        var text = $"abc {Hebrew} def";
        var layout = Layout(text, direction);
        var line = layout.GetLine(0);
        var position = ToPosition(layout.HitTest(-50, line.Top + line.Height / 2));

        var xs = new List<double> { layout.GetCaretPoint(position).X };
        for (var step = 0; step < text.Length; step++)
        {
            position = layout.MoveVisually(position, toRight: true);
            xs.Add(layout.GetCaretPoint(position).X);
        }

        Assert.Multiple(() =>
        {
            Assert.That(xs, Is.Ordered.Ascending);
            Assert.That(xs.Distinct().Count(), Is.EqualTo(text.Length + 1), "one letter a step");
            Assert.That(layout.MoveVisually(position, toRight: true).Index, Is.EqualTo(position.Index), "the end stays");
        });
    }

    // Invisible marks take no width; the caret steps over them rather than stopping on them.
    [TestCase(0x200F, "cd")]
    [TestCase(0x200B, "cd")]
    [TestCase(0x200F, Hebrew)]
    public void StepsToTheRight_PassInvisibleMarks(int mark, string tail)
    {
        var text = "ab" + (char)mark + tail;
        var layout = Layout(text);
        var position = layout.GetLineStart(0);
        for (var step = 0; step < text.Length + 2; step++)
        {
            position = layout.MoveVisually(position, toRight: true);
        }

        Assert.That(layout.GetCaretPoint(position).X,
            Is.EqualTo(layout.GetCaretStops().Take(text.Length).Max(s => s.Right)).Within(1e-6));
    }

    // From the end of "abc" to the middle of the Hebrew word on screen: the space and the word's last two letters,
    // which the text keeps apart from the space by the word's first two.
    [Test]
    public void AVisualSelection_TakesTheLettersBetweenTwoPointsOnScreen()
    {
        var layout = Layout($"abc {Hebrew} def");
        var anchor = new CaretPosition(3, afterPrevious: true);
        var focus = new CaretPosition(6);

        Assert.Multiple(() =>
        {
            Assert.That(layout.GetVisualRanges(anchor, focus), Is.EqualTo(new[] { (3, 4), (6, 8) }));
            Assert.That(layout.GetVisualRanges(focus, anchor), Is.EqualTo(new[] { (3, 4), (6, 8) }), "either way");
            Assert.That(layout.GetVisualRanges(anchor, anchor), Is.Empty, "a point takes nothing");
        });
    }

    [Test]
    public void SelectingEverythingOnScreen_TakesBothCharactersOfACarriageReturnLineFeed()
    {
        var text = $"{Hebrew}\r\nabc";
        var layout = Layout(text);

        var ranges = layout.GetVisualRanges(layout.GetLineStart(0), layout.GetLineEnd(1));

        Assert.That(ranges, Is.EqualTo(new[] { (0, text.Length) }));
    }
}
