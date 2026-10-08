using Adamantium.Mathematics;

namespace Adamantium.Fonts;

/// <summary>One stop of a gradient of a color glyph: where along the gradient, and its color.</summary>
public readonly struct ColorStop
{
    public ColorStop(float offset, Color? color, float alpha)
    {
        Offset = offset;
        Color = color;
        Alpha = alpha;
    }

    /// <summary>Where the stop is along the gradient, 0 at its start and 1 at its end; may lie outside.</summary>
    public float Offset { get; }

    /// <summary>The stop's color from the palette, its alpha included; null takes the color of the text.</summary>
    public Color? Color { get; }

    /// <summary>How opaque the text's color is at this stop, for a stop that takes it.</summary>
    public float Alpha { get; }
}
