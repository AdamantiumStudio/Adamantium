namespace Adamantium.Fonts;

/// <summary>A color glyph's image at one size ('CBDT' or 'sbix'): a PNG and where it lies, in pixels of its strike.</summary>
public sealed class ColorBitmap
{
    internal ColorBitmap(byte[] png, int pixelsPerEm, int left, int top, int width, int height)
    {
        Png = png;
        PixelsPerEm = pixelsPerEm;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    /// <summary>The image, a PNG file.</summary>
    public byte[] Png { get; }

    /// <summary>The size the image was drawn for, in pixels per em; it scales with the text from there.</summary>
    public int PixelsPerEm { get; }

    /// <summary>The image's left edge, right of the glyph's origin.</summary>
    public int Left { get; }

    /// <summary>The image's top edge, above the baseline.</summary>
    public int Top { get; }

    /// <summary>The image's width.</summary>
    public int Width { get; }

    /// <summary>The image's height.</summary>
    public int Height { get; }
}
