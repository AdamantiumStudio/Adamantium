using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class TextLayoutFallbackTests
{
    [SetUp]
    public void OnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Ignore("the fallback families named here are Windows fonts");
        }
    }

    private static TextLayout Layout(AttributedText text, FontFallback fallback = null, bool setFallback = false)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "SourceSans3-Regular.ttf");
        var typeface = Typeface.LoadFont(path);
        var layout = new TextLayout(typeface, typeface.Fonts[0]);
        if (setFallback)
        {
            layout.Fallback = fallback;
        }

        layout.ProcessText(text, 20, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    [Test]
    public void ACharacterTheFontLacks_IsDrawnFromAFallbackFont()
    {
        var layout = Layout(new AttributedText("a中b"));
        var glyphs = layout.GetTextData();
        var han = glyphs.Single(g => g.PositionInString == 1);

        Assert.That(han.Font, Is.Not.SameAs(layout.Font));
        Assert.That(han.Glyph.Index, Is.Not.Zero, "a real glyph, not the missing-glyph box");
        Assert.That(glyphs.Where(g => g.PositionInString != 1).All(g => g.Font == layout.Font));
    }

    [Test]
    public void WithoutAFallback_TheMissingGlyphBoxIsDrawn()
    {
        var layout = Layout(new AttributedText("a中b"), setFallback: true);
        var han = layout.GetTextData().Single(g => g.PositionInString == 1);

        Assert.That(han.Font, Is.SameAs(layout.Font));
        Assert.That(han.Glyph.Index, Is.Zero);
    }

    [Test]
    public void HanFollowsTheLanguageOfTheText()
    {
        var japanese = Layout(new AttributedText("直", new TextAttributes { Language = "ja" }));
        var chinese = Layout(new AttributedText("直", new TextAttributes { Language = "zh" }));

        Assert.That(japanese.GetTextData().Single().Font.FontFamily, Does.Contain("Yu Gothic"));
        Assert.That(chinese.GetTextData().Single().Font.FontFamily, Does.Contain("YaHei"));
    }

    [Test]
    public void AnEmoji_IsDrawnFromTheEmojiFont()
    {
        var layout = Layout(new AttributedText("ok \U0001F600"));
        var emoji = layout.GetTextData().Single(g => g.PositionInString == 3);

        Assert.That(emoji.Font.FontFamily, Is.EqualTo("Segoe UI Emoji"));
        Assert.That(emoji.Glyph.Index, Is.Not.Zero);
        Assert.That(emoji.Glyph.BoundingRectangle.Width, Is.GreaterThan(0), "the uncolored glyph has an outline to draw");
    }
}
