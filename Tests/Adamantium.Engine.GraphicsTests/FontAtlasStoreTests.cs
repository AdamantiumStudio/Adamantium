using System.IO;
using Adamantium.Fonts;
using Adamantium.Graphics.Fonts;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests;

[TestFixture]
public class FontAtlasStoreTests
{
    [TearDown]
    public void ReleaseDevices()
    {
        FontAtlasStore.Reset();
        GpuFixture.ReleaseRenderDevices();
    }

    private static TextLayout Layout(string file)
    {
        var typeface = Typeface.LoadFont(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", file));
        return new TextLayout(typeface, typeface.Fonts[0]);
    }

    [Test]
    public void TwoFonts_GetTwoAtlases()
    {
        var device = GpuFixture.CreateRenderDevice();
        var regular = Layout("SourceSans3-Regular.ttf").EnsureAtlas(device);
        var bold = Layout("SourceSans3-Bold.ttf").EnsureAtlas(device);

        Assert.That(bold, Is.Not.SameAs(regular), "glyphs are addressed by index, so a shared atlas draws the other font");
    }

    [Test]
    public void TextInOneFont_SharesItsAtlas()
    {
        var device = GpuFixture.CreateRenderDevice();
        var first = Layout("SourceSans3-Regular.ttf");
        var second = new TextLayout(first.Typeface, first.Font);

        Assert.That(second.EnsureAtlas(device), Is.SameAs(first.EnsureAtlas(device)));
    }
}
