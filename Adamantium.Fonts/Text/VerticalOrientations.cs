using System;
using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts.Text;

/// <summary>The Vertical_Orientation of characters (UAX #50, Unicode 16.0): which stand upright in vertical text and
/// which lie turned.</summary>
public static class VerticalOrientations
{
    private static readonly Lazy<RangeTable<VerticalOrientation>> Table =
        new(() => RangeTable<VerticalOrientation>.Load("VerticalOrientation.ucd", Parse, VerticalOrientation.Rotated));

    /// <summary>How <paramref name="codepoint"/> stands in vertical text.</summary>
    public static VerticalOrientation Of(int codepoint) => Table.Value[codepoint];

    /// <summary>Whether <paramref name="codepoint"/> stands upright in vertical text, in a form of its own or not.</summary>
    public static bool IsUpright(int codepoint) => Of(codepoint) is not (VerticalOrientation.Rotated
        or VerticalOrientation.TransformedOrRotated);

    private static VerticalOrientation Parse(string value) => value switch
    {
        "U" => VerticalOrientation.Upright,
        "Tu" => VerticalOrientation.TransformedOrUpright,
        "Tr" => VerticalOrientation.TransformedOrRotated,
        _ => VerticalOrientation.Rotated,
    };
}
