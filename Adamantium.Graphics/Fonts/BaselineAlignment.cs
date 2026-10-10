namespace Adamantium.Graphics.Fonts;

/// <summary>Where a range of text stands across its line, as WPF's. In vertical text only the superscript and the
/// subscript move it.</summary>
public enum BaselineAlignment
{
    /// <summary>On the line's baseline.</summary>
    Baseline,

    /// <summary>Its top at the top of the line.</summary>
    Top,

    /// <summary>Its middle at the middle of the line.</summary>
    Center,

    /// <summary>Its bottom at the bottom of the line.</summary>
    Bottom,

    /// <summary>Its ascender at the ascender of the layout's font.</summary>
    TextTop,

    /// <summary>Its descender at the descender of the layout's font.</summary>
    TextBottom,

    /// <summary>Raised as the layout's font sets a superscript.</summary>
    Superscript,

    /// <summary>Lowered as the layout's font sets a subscript.</summary>
    Subscript,
}
