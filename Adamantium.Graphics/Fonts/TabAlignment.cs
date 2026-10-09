namespace Adamantium.Graphics.Fonts;

/// <summary>How the text after a tab lines up with its <see cref="TabStop"/>.</summary>
public enum TabAlignment
{
    /// <summary>The text starts at the stop.</summary>
    Left,

    /// <summary>The text is centered on the stop.</summary>
    Center,

    /// <summary>The text ends at the stop.</summary>
    Right,

    /// <summary>The text's <see cref="TabStop.AlignOn"/> character - its decimal separator - stands at the stop; text
    /// without one ends there.</summary>
    Decimal,
}
