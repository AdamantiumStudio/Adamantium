namespace Adamantium.Graphics.Fonts;

/// <summary>What a point in laid-out text falls on.</summary>
public readonly struct TextHit
{
    public TextHit(int index, bool isTrailing, bool isInside, int caretIndex)
    {
        Index = index;
        IsTrailing = isTrailing;
        IsInside = isInside;
        CaretIndex = caretIndex;
    }

    /// <summary>The first UTF-16 offset of the grapheme under the point, or nearest to it.</summary>
    public int Index { get; }

    /// <summary>Whether the point is over the grapheme's trailing half.</summary>
    public bool IsTrailing { get; }

    /// <summary>Whether the point is over the text rather than beside or below it.</summary>
    public bool IsInside { get; }

    /// <summary>Where a click there puts the caret: before the grapheme, after it when the point is trailing, or at the
    /// line's start or end beside the line.</summary>
    public int CaretIndex { get; }
}
