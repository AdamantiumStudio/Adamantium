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

    [Test]
    public void ABlockOfMoreThan4096Glyphs_DrawsThemAll()
    {
        var device = GpuFixture.CreateRenderDevice();
        FontAtlasStore.SynchronousFill = true;
        var layout = Layout("SourceSans3-Regular.ttf", new string('a', 5000));

        layout.Update(device);
        layout.EnsureVertexBuffer(device);

        Assert.That(layout.ElementsCount, Is.EqualTo(5000), "no glyph past a fixed cap is dropped");
        Assert.That(layout.VertexBuffer.ElementCount, Is.GreaterThanOrEqualTo(5000UL), "the direct path's buffer grew");
    }

    [Test]
    public void ABlockEmptiedAfterItGrew_UploadsNothingPastItsBuffer()
    {
        var device = GpuFixture.CreateRenderDevice();
        FontAtlasStore.SynchronousFill = true;
        var layout = Layout("SourceSans3-Regular.ttf", "a");
        layout.Update(device);
        layout.EnsureVertexBuffer(device);
        var size = new Size(double.NaN, double.NaN);
        layout.ProcessText(new string('a', 5000), 20, size, TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        layout.Update(device);
        layout.ProcessText(string.Empty, 20, size, TextWrapping.NoWrap, TextTrimming.None,
            HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        layout.Update(device);

        Assert.DoesNotThrow(() => layout.EnsureVertexBuffer(device));
        Assert.That(layout.ElementsCount, Is.EqualTo(0));
    }

    [Test]
    public void AColorGlyph_DrawsAQuadPerLayerOfItsPaintGraph()
    {
        var device = GpuFixture.CreateRenderDevice();
        FontAtlasStore.SynchronousFill = true;
        var typeface = Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts",
            "test_glyphs-glyf_colr_1.ttf"));
        var font = typeface.Fonts[0];
        var codepoint = font.Unicodes.First(u => font.TryGetGlyphIndex((int)u, out var g) && font.GetColorPaint(g).Count > 1);
        font.TryGetGlyphIndex((int)codepoint, out var glyph);
        var layout = new TextLayout(typeface, font);
        layout.ProcessText(char.ConvertFromUtf32((int)codepoint), 20, new Size(double.NaN, double.NaN),
            TextWrapping.NoWrap, TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Top);

        layout.Update(device);
        var run = layout.SnapshotGlyphs();

        Assert.That(run.Count, Is.EqualTo(font.GetColorPaint(glyph).Count), "a quad per layer");
        Assert.That(run.Glyphs.Take(run.Count).All(g => g.Paint.X >= 1), "each quad names its paint record");
        Assert.That(run.Glyphs.Take(run.Count).Select(g => g.Paint.X).Distinct().Count(), Is.EqualTo(run.Count),
            "each layer has a record of its own");
    }
}
