using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Parsers;

internal class TTFGlyphOutlineSource : IGlyphOutlineSource, IVariableGlyphOutlineSource
{
    private readonly SfntParser parser;
    private readonly byte[] fontData;
    private readonly long glyfTableOffset;
    private readonly uint[] glyphOffsets;
    private readonly Glyph[] glyphs;

    public TTFGlyphOutlineSource(SfntParser parser, byte[] fontData, long glyfTableOffset, uint[] glyphOffsets, Glyph[] glyphs)
    {
        this.parser = parser;
        this.fontData = fontData;
        this.glyfTableOffset = glyfTableOffset;
        this.glyphOffsets = glyphOffsets;
        this.glyphs = glyphs;
    }

    public void LoadOutlines(Glyph glyph)
    {
        using var reader = new FontStreamReader(fontData);
        reader.Position = glyfTableOffset + glyphOffsets[glyph.Index];
        parser.ReadGlyphOutlines(reader, glyph, glyphs);
    }

    public IGlyphOutlineSource Vary(Font font, float[] coordinates, Typeface variedTypeface)
    {
        return font.GlyphVariations == null
            ? null
            : new VariedTTFGlyphOutlineSource(parser, fontData, glyfTableOffset, glyphOffsets, glyphs.Length,
                variedTypeface, font, coordinates);
    }
}
