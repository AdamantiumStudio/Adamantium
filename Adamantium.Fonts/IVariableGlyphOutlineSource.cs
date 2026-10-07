namespace Adamantium.Fonts;

internal interface IVariableGlyphOutlineSource
{
    IGlyphOutlineSource Vary(Font font, float[] coordinates, Glyph[] glyphs);
}
