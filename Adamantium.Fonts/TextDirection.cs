namespace Adamantium.Fonts;

/// <summary>Which way text runs.</summary>
public enum TextDirection
{
    /// <summary>Taken from the text: shaping takes a run's direction from its script, a paragraph from its first
    /// strong character (the Unicode Bidirectional Algorithm, P2 and P3).</summary>
    Auto,

    /// <summary>Left to right, as Latin.</summary>
    LeftToRight,

    /// <summary>Right to left, as Hebrew and Arabic.</summary>
    RightToLeft
}
