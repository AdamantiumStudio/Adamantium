using System;

namespace Adamantium.Graphics.Fonts;

/// <summary>Lines drawn with text.</summary>
[Flags]
public enum TextDecorations
{
    None = 0,
    Underline = 1,
    Strikethrough = 2,

    /// <summary>A wavy line under the text, as for a spelling or compiler error.</summary>
    Squiggle = 4,
}
