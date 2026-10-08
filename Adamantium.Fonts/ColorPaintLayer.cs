using Adamantium.Mathematics;

namespace Adamantium.Fonts;

/// <summary>
/// One layer of a color glyph's paint graph ('COLR' version 1), flattened: the outline of an ordinary glyph of the same
/// font, placed by <see cref="Transform"/>, filled with <see cref="Fill"/>, drawn over the layers before it. Of glyphs
/// nested as clips, the innermost is the layer's outline.
/// </summary>
public sealed class ColorPaintLayer
{
    public ColorPaintLayer(uint glyphIndex, Matrix3x2 transform, ColorFill fill, float opacity)
    {
        GlyphIndex = glyphIndex;
        Transform = transform;
        Fill = fill;
        Opacity = opacity;
    }

    /// <summary>The glyph whose outline the layer fills.</summary>
    public uint GlyphIndex { get; }

    /// <summary>From the outline's space into the color glyph's, in font units with y up.</summary>
    public Matrix3x2 Transform { get; }

    /// <summary>The fill, its transform composed with <see cref="Transform"/>: it maps into the color glyph's space too.</summary>
    public ColorFill Fill { get; }

    /// <summary>How opaque the whole layer is, from a composition the layer was drawn through.</summary>
    public float Opacity { get; }
}
