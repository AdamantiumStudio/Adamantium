using System;

namespace Adamantium.Fonts;

/// <summary>The backgrounds a color palette suits ('CPAL' version 1 palette types).</summary>
[Flags]
public enum ColorPaletteUsage
{
    None = 0,
    LightBackground = 1,
    DarkBackground = 2,
}
