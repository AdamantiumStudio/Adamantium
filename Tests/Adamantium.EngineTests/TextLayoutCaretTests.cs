using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class TextLayoutCaretTests
{
    private const double Tolerance = 1e-6;

    private static TextLayout Layout(string text)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "SourceSans3-Regular.ttf");
        var typeface = Typeface.LoadFont(path);
        var layout = new TextLayout(typeface, typeface.Fonts[0]) { EmitNewlineCarets = true };
        layout.ProcessText(text, 20, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    [Test]
    public void CharactersOfALigature_ShareItsWidth()
    {
        var layout = Layout("office");
        var glyphs = layout.GetTextData();
        var stops = layout.GetCaretStops();

        Assert.That(glyphs.Length, Is.EqualTo(5), "o, the ff ligature, i, c, e");
        var ligature = glyphs[1];
        Assert.That(ligature.PositionInString, Is.EqualTo(1));
        Assert.That(stops[1].X, Is.EqualTo(ligature.PenX).Within(Tolerance));
        Assert.That(stops[2].X, Is.EqualTo(ligature.PenX + ligature.Advance / 2).Within(Tolerance));
        Assert.That(stops[3].X, Is.EqualTo(ligature.PenX + ligature.Advance).Within(Tolerance));
        Assert.That(stops.Select(s => s.X), Is.Ordered);
    }

    [Test]
    public void BothHalvesOfASurrogatePair_StandAtOneStop()
    {
        var stops = Layout("a\U0001F600b").GetCaretStops();

        Assert.That(stops[2].X, Is.EqualTo(stops[1].X));
        Assert.That(stops[3].X, Is.GreaterThan(stops[1].X));
    }

    [Test]
    public void ANewline_StartsTheNextLineAtZero()
    {
        var layout = Layout("ab\ncd\n");
        var stops = layout.GetCaretStops();
        var glyphs = layout.GetTextData();

        Assert.That(stops[2].LineIndex, Is.EqualTo(0));
        Assert.That(stops[2].X, Is.EqualTo(glyphs[1].PenX + glyphs[1].Advance).Within(Tolerance), "after b");
        Assert.That(stops[3].LineIndex, Is.EqualTo(1));
        Assert.That(stops[3].X, Is.EqualTo(0).Within(Tolerance));
        Assert.That(stops[6].LineIndex, Is.EqualTo(2), "after the trailing newline");
        Assert.That(stops[6].X, Is.EqualTo(0));
    }

    [Test]
    public void TheEndStop_FollowsTheLastGlyph()
    {
        var layout = Layout("To");
        var glyphs = layout.GetTextData();
        var stops = layout.GetCaretStops();

        Assert.That(stops[2].X, Is.EqualTo(glyphs[1].PenX + glyphs[1].Advance).Within(Tolerance));
        Assert.That(stops[1].X, Is.LessThan(glyphs[0].Advance + Tolerance), "kerning pulls o under T");
    }
}
