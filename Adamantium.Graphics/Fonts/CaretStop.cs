namespace Adamantium.Graphics.Fonts;

/// <summary>A caret position in laid-out text: X in layout coordinates, the visual line, and the width of the character
/// that starts here - its share of the glyph that draws it.</summary>
public readonly struct CaretStop
{
    public CaretStop(double x, int lineIndex, double width = 0)
    {
        X = x;
        LineIndex = lineIndex;
        Width = width;
    }

    public double X { get; }

    public int LineIndex { get; }

    public double Width { get; }
}
