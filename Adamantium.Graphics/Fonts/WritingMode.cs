namespace Adamantium.Graphics.Fonts;

/// <summary>Which way lines of text run and stack.</summary>
public enum WritingMode
{
    /// <summary>Lines run across and stack downward.</summary>
    Horizontal,

    /// <summary>Lines run downward and stack from right to left, as Chinese and Japanese are set vertically: ideographs
    /// and kana stand upright in their vertical forms, as do brackets and long marks the font has vertical forms of
    /// (UAX #50), other text lies turned 90° clockwise, and a run of one or two digits stands upright across the line
    /// (tate-chū-yoko).</summary>
    VerticalRightToLeft,
}
