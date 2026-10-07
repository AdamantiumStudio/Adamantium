using System.IO;
using System.Linq;
using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class ColorLayerTests
{
    private const string EmojiFont = @"C:\Windows\Fonts\seguiemj.ttf";

    private static IFont Emoji()
    {
        if (!File.Exists(EmojiFont))
        {
            Assert.Ignore("Segoe UI Emoji is not installed here.");
        }

        return TypefaceStore.GetTypeface(EmojiFont).Fonts[0];
    }

    [Test]
    public void AnEmoji_IsAStackOfColoredLayersOfTheSameFont()
    {
        var font = Emoji();
        Assert.That(font.TryGetGlyphIndex(0x1F600, out var glyph), Is.True);

        var layers = font.GetColorLayers(glyph);

        Assert.That(layers.Count, Is.GreaterThan(1));
        Assert.That(layers.All(l => l.GlyphIndex < font.Typeface.GlyphCount), Is.True, "layers are glyphs of the font");
        Assert.That(layers.Any(l => l.Color is { A: > 0 }), Is.True, "colors come from the palette");
        Assert.That(layers.All(l => l.GlyphIndex != glyph), Is.True, "a layer is never the color glyph itself");
    }

    [Test]
    public void AFontWithoutColorTables_HasNoLayers()
    {
        var font = Typeface.LoadFont(Path.Combine("TTFFonts", "SourceSans3-Regular.ttf")).Fonts[0];
        font.TryGetGlyphIndex('A', out var glyph);

        Assert.That(font.GetColorLayers(glyph), Is.Empty);
    }
}
