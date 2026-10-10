namespace Adamantium.Graphics.Fonts;

/// <summary>What a <see cref="TextAdornment"/> is: a background goes under the glyphs, the lines over them.</summary>
public enum TextAdornmentKind
{
    Background,
    Underline,
    Strikethrough,
    Squiggle,
    Overline,

    /// <summary>A line on the baseline.</summary>
    Baseline,
}
