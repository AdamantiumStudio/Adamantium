namespace Adamantium.Graphics.Fonts;

/// <summary>Where the lines of text stand across their area.</summary>
public enum HorizontalTextAlignment
{
    /// <summary>At the start of each line: the left edge, the right edge for a right-to-left paragraph.</summary>
    Left = 0,

    /// <summary>At the end of each line: the right edge, the left edge for a right-to-left paragraph.</summary>
    Right = 1,

    Center = 2,

    Justify = 3
}
