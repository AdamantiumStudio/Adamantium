using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>Where laid-out text set an object (<see cref="TextAttributes.ObjectSize"/>): the index of its U+FFFC in the
/// text and its rectangle, in layout coordinates, standing on its line's baseline.</summary>
public readonly struct TextInlineObject
{
    public TextInlineObject(int index, RectangleF rect)
    {
        Index = index;
        Rect = rect;
    }

    public int Index { get; }

    public RectangleF Rect { get; }
}
