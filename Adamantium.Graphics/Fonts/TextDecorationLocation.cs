namespace Adamantium.Graphics.Fonts;

/// <summary>Where a <see cref="TextDecorationLine"/> runs, as WPF's.</summary>
public enum TextDecorationLocation
{
    /// <summary>Under the text, where the font sets its underline.</summary>
    Underline,

    /// <summary>Over the text, at the font's ascender.</summary>
    Overline,

    /// <summary>Through the text, where the font sets its strikeout.</summary>
    Strikethrough,

    /// <summary>On the baseline.</summary>
    Baseline,
}
