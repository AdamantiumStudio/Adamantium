namespace Adamantium.Fonts.Shaping;

/// <summary>One glyph of shaped text, positioned in font design units.</summary>
public readonly struct ShapedGlyph
{
    public ShapedGlyph(uint glyphIndex, int cluster, int xAdvance, int yAdvance, int xOffset, int yOffset)
    {
        GlyphIndex = glyphIndex;
        Cluster = cluster;
        XAdvance = xAdvance;
        YAdvance = yAdvance;
        XOffset = xOffset;
        YOffset = yOffset;
    }

    public uint GlyphIndex { get; }

    /// <summary>UTF-16 offset of the first character this glyph was made from; glyphs of one cluster share it.</summary>
    public int Cluster { get; }

    public int XAdvance { get; }

    public int YAdvance { get; }

    public int XOffset { get; }

    /// <summary>Upward from the baseline, as in the font.</summary>
    public int YOffset { get; }

    public override string ToString()
    {
        var offset = XOffset != 0 || YOffset != 0 ? $"@{XOffset},{YOffset}" : string.Empty;
        var down = YAdvance != 0 ? $"|{YAdvance}" : string.Empty;
        return $"{GlyphIndex}={Cluster}{offset}+{XAdvance}{down}";
    }
}
