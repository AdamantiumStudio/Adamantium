using Adamantium.Mathematics;

namespace Adamantium.Fonts;

/// <summary>One layer of a color glyph ('COLR'): an ordinary glyph of the same font, drawn in a color from the font's
/// palette ('CPAL') on top of the layers before it.</summary>
public readonly struct ColorLayer
{
    public ColorLayer(uint glyphIndex, Color? color)
    {
        GlyphIndex = glyphIndex;
        Color = color;
    }

    /// <summary>The glyph whose outline the layer fills.</summary>
    public uint GlyphIndex { get; }

    /// <summary>The layer's color; null draws it in the color of the text.</summary>
    public Color? Color { get; }
}
