namespace Adamantium.Fonts.Text;

/// <summary>What an item of a paragraph is to <see cref="ParagraphComposer"/>.</summary>
public enum ComposerItemKind : byte
{
    /// <summary>Something set as it is: a word or a piece of one.</summary>
    Box,

    /// <summary>A space that may stretch or shrink, and where a line may end when a box comes before it.</summary>
    Glue,

    /// <summary>A place a line may end at a cost, adding its width when it does - a hyphen.</summary>
    Penalty,
}
