namespace Adamantium.Graphics.Fonts;

/// <summary>Where a word wrapped by <see cref="TextWrapping.WrapByWords"/> may break across lines, a hyphen ending the
/// line before the break.</summary>
public enum Hyphens
{
    /// <summary>Never: a word moves to the next line whole, soft hyphens (U+00AD) included.</summary>
    None,

    /// <summary>Only at soft hyphens (U+00AD), drawn only where a line breaks at one.</summary>
    Manual,

    /// <summary>At soft hyphens, and in a word without any, where the hyphenation patterns of its language allow
    /// (<see cref="Adamantium.Fonts.Text.Hyphenator"/>).</summary>
    Auto,
}
