using System.Linq;
using Adamantium.Fonts;
using Adamantium.Fonts.Shaping;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class KerningTests
{
    [TestCase("OTFFonts/SourceSans3-Regular.otf", "yT", -20)]
    [TestCase("OTFFonts/SourceSans3-Regular.otf", "x,", 7)]
    [TestCase("OTFFonts/Crimson-Italic.otf", "AC", -24)]
    [TestCase("TTFFonts/PlayfairDisplay-Regular.ttf", "TA", -96)]
    [TestCase("TTFFonts/Sarabun-Regular.ttf", "AC", -30)]
    [TestCase("TTFFonts/SourceSans3-It.ttf", ".j", 20)]
    public void KerningMatchesTheReferenceShaper(string path, string pair, int expected)
    {
        var font = Typeface.LoadFont(path, 3).GetFont(0);

        var glyphs = TextShaper.Shape(font, pair);
        var kerning = glyphs.Sum(g => g.XAdvance) - glyphs.Sum(g => font.GetGlyphByIndex(g.GlyphIndex).AdvanceWidth);

        Assert.That(kerning, Is.EqualTo(expected));
    }
}
