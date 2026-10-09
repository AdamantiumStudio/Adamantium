namespace Adamantium.Fonts.Text;

/// <summary>How a character stands in vertical text, by the Vertical_Orientation property of Unicode (UAX #50).</summary>
public enum VerticalOrientation : byte
{
    /// <summary>Turned 90° clockwise, as Latin letters and digits lie in a vertical line (R).</summary>
    Rotated,

    /// <summary>Upright, as CJK ideographs and kana stand (U).</summary>
    Upright,

    /// <summary>Upright, in a vertical form of its own where the font has one - an ideographic full stop moved to the
    /// top right - and turned otherwise (Tr).</summary>
    TransformedOrRotated,

    /// <summary>Upright, in a vertical form of its own where the font has one, upright otherwise (Tu).</summary>
    TransformedOrUpright,
}
