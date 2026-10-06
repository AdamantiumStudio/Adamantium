namespace Adamantium.Fonts.Text;

/// <summary>Whether a line may end at a place in text.</summary>
public enum LineBreakKind : byte
{
    None,
    Allowed,

    /// <summary>The line must end here: after a newline or a paragraph separator.</summary>
    Mandatory,
}
