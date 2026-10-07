using System.IO;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests;

[TestFixture]
public class FontAtlasStoreTests
{
    [TearDown]
    public void ReleaseDevices()
    {
        FontAtlasStore.SynchronousFill = false;
        FontAtlasStore.Reset();
        GpuFixture.ReleaseRenderDevices();
    }

    private static TextLayout Layout(string file, string text)
    {
        var typeface = Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", file));
        var layout = new TextLayout(typeface, typeface.Fonts[0]);
        layout.ProcessText(text, 20, new Size(double.NaN, double.NaN), TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        return layout;
    }

    [Test]
    public void AnAtlasGrows_AsItsGlyphsFillIt()
    {
        var device = GpuFixture.CreateRenderDevice();
        FontAtlasStore.SynchronousFill = true;
        var layout = Layout("SourceSans3-Regular.ttf", "a");
        var atlas = layout.EnsureAtlas(device);
        var font = layout.Font;
        var glyphs = font.Typeface.Glyphs
            .Where(g => !g.IsEmpty && g.BoundingRectangle.Width > 0 && g.BoundingRectangle.Height > 0)
            .Take(600)
            .Select(g => (font, g))
            .ToList();

        atlas.RequestAsync(glyphs);

        Assert.That(atlas.LayerCount, Is.GreaterThan(FontAtlas.InitialLayerCount), "600 glyphs need more than two layers");
        Assert.That(atlas.LayerCapacity, Is.GreaterThanOrEqualTo(atlas.LayerCount));
        Assert.That(glyphs.All(p => atlas.GetGlyphData(p.font, p.g) != null), "every glyph has its cell");
    }

    [Test]
    public void TwoFonts_ShareOneAtlas_AndKeepTheirGlyphsApart()
    {
        var device = GpuFixture.CreateRenderDevice();
        FontAtlasStore.SynchronousFill = true;
        var regular = Layout("SourceSans3-Regular.ttf", "a");
        var bold = Layout("SourceSans3-Bold.ttf", "a");
        regular.Update(device);
        bold.Update(device);

        var regularA = regular.GetTextData().Single();
        var boldA = bold.GetTextData().Single();
        var atlas = regular.FontAtlas;
        var regularCell = atlas.GetGlyphData(regularA.Font, regularA.Glyph);
        var boldCell = atlas.GetGlyphData(boldA.Font, boldA.Glyph);

        Assert.That(bold.FontAtlas, Is.SameAs(atlas), "one atlas for every font");
        Assert.That(boldA.Glyph.Index, Is.EqualTo(regularA.Glyph.Index), "the same index in both fonts");
        Assert.That(regularCell, Is.Not.Null);
        Assert.That(boldCell, Is.Not.Null);
        Assert.That(boldCell, Is.Not.SameAs(regularCell), "a glyph of one font never draws the other's picture");
    }
}
