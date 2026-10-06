namespace Adamantium.Graphics.Fonts;

/// <summary>A visual line of laid-out text: the UTF-16 range it shows and where it stands, in layout coordinates.</summary>
public readonly struct TextLineMetrics
{
    public TextLineMetrics(int start, int end, double top, double baseline, double height, double width)
    {
        Start = start;
        End = end;
        Top = top;
        Baseline = baseline;
        Height = height;
        Width = width;
    }

    public int Start { get; }

    /// <summary>Exclusive; a line's newline belongs to it.</summary>
    public int End { get; }

    public double Top { get; }

    public double Baseline { get; }

    public double Height { get; }

    public double Width { get; }
}
