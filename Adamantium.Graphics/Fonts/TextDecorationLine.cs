using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>A line drawn with a range of text, as WPF's text decoration with its pen: where it runs, how thick, how far
/// it is moved and in what color, solid or dashed.</summary>
public sealed class TextDecorationLine
{
    public TextDecorationLocation Location { get; init; }

    /// <summary>The line's thickness, in the layout's units; unset takes the font's for its place.</summary>
    public double? Thickness { get; init; }

    /// <summary>How far the line is moved from its place, in the layout's units: down in horizontal text, toward the
    /// left in vertical text.</summary>
    public double Offset { get; init; }

    /// <summary>Unset takes <see cref="TextAttributes.DecorationColor"/>, then the text's color.</summary>
    public Color? Color { get; init; }

    /// <summary>The lengths of its dashes and the gaps between them, in turn, in the layout's units; unset or empty
    /// draws it solid.</summary>
    public IReadOnlyList<double> Dashes { get; init; }
}
