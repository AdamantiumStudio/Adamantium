namespace Adamantium.Graphics.Fonts;

/// <summary>A caret position in laid-out text: X in layout coordinates, the visual line, and the width of the character
/// that starts here - its share of the glyph that draws it. The caret stands before the character: on its left edge,
/// or its right edge when the character runs right to left.</summary>
public readonly struct CaretStop
{
    public CaretStop(double x, int lineIndex, double width = 0, bool isRightToLeft = false)
    {
        X = x;
        LineIndex = lineIndex;
        Width = width;
        IsRightToLeft = isRightToLeft;
    }

    public double X { get; }

    public int LineIndex { get; }

    public double Width { get; }

    /// <summary>Whether the character runs right to left, its caret on its right edge.</summary>
    public bool IsRightToLeft { get; }

    /// <summary>The character's left edge.</summary>
    public double Left => IsRightToLeft ? X - Width : X;

    /// <summary>The character's right edge.</summary>
    public double Right => IsRightToLeft ? X : X + Width;

    /// <summary>Where the caret stands after the character: on its right edge, or its left edge when it runs right to
    /// left.</summary>
    public double After => IsRightToLeft ? Left : Right;
}
