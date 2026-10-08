namespace Adamantium.Fonts;

/// <summary>What a step of a color glyph's paint program ('COLR' version 1) does.</summary>
public enum ColorPaintOperationKind
{
    /// <summary>Cuts what follows to a glyph's outline, inside the clips already in force.</summary>
    PushClip,

    /// <summary>Takes the last clip away.</summary>
    PopClip,

    /// <summary>Starts a transparent group that what follows is drawn into.</summary>
    PushGroup,

    /// <summary>Ends the group and composites it onto what is under it, with the operation's mode.</summary>
    PopGroup,

    /// <summary>Fills the clips in force with a color or a gradient.</summary>
    Fill,
}
