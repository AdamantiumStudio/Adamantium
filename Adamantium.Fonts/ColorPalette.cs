using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts;

/// <summary>One of a color font's palettes ('CPAL'): the colors its color glyphs are drawn in when a text picks it.</summary>
public sealed class ColorPalette
{
    internal ColorPalette(int index, ColorPaletteUsage usage, IReadOnlyList<Color> colors)
    {
        Index = index;
        Usage = usage;
        Colors = colors;
    }

    /// <summary>The palette's number, as a text picks it; 0 is the font's default.</summary>
    public int Index { get; }

    /// <summary>The backgrounds the font says the palette suits (version 1 of the table); none when it does not say.</summary>
    public ColorPaletteUsage Usage { get; }

    /// <summary>The palette's colors, by the entry numbers the glyphs use.</summary>
    public IReadOnlyList<Color> Colors { get; }
}
