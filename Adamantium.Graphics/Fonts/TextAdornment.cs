using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>A background or a line of laid-out text: where to draw it, in layout coordinates, and its color.</summary>
public readonly struct TextAdornment
{
    public TextAdornment(TextAdornmentKind kind, RectangleF rect, Color? color)
    {
        Kind = kind;
        Rect = rect;
        Color = color;
    }

    public TextAdornmentKind Kind { get; }

    /// <summary>For a squiggle, the band the wave fills: its crests touch the top and bottom edges.</summary>
    public RectangleF Rect { get; }

    /// <summary>Null draws a line in the color of the text it runs under.</summary>
    public Color? Color { get; }
}
