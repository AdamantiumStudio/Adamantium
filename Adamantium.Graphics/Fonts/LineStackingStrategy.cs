namespace Adamantium.Graphics.Fonts;

/// <summary>How the height of a line of text is decided, as WPF's.</summary>
public enum LineStackingStrategy
{
    /// <summary>A line is as high as <see cref="TextLayout.LineHeight"/>, and higher when it holds text of a larger size
    /// or a taller object.</summary>
    MaxHeight,

    /// <summary>Every line is exactly <see cref="TextLayout.LineHeight"/> high.</summary>
    BlockLineHeight,
}
