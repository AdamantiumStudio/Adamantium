using System;
using Adamantium.Fonts;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

public class GlyphWordData
{
    public GlyphWordData(Glyph glyph, Char symbol, RectangleF rect, int positionInString, int lineIndex)
    {
        Glyph = glyph;
        Rect = rect;
        Symbol = symbol;
        PositionInString = positionInString;
        LineIndex = lineIndex;
    }
    
    public Char Symbol { get; }

    public Glyph Glyph { get; }

    public RectangleF Rect;

    public int PositionInString { get; set; }
    
    public int LineIndex { get; set; }

    /// <summary>The pen position the glyph is drawn from, before its bearing and offset.</summary>
    public double PenX { get; set; }

    /// <summary>How far the pen moves after this glyph, kerning and positioning included.</summary>
    public double Advance { get; set; }

    public double OffsetX { get; set; }

    /// <summary>Upward from the baseline, as in the font.</summary>
    public double OffsetY { get; set; }

    /// <summary>The font the glyph belongs to.</summary>
    public IFont Font { get; set; }

    /// <summary>The size the glyph is set at.</summary>
    public double FontSize { get; set; }

    /// <summary>The attributes of the text this glyph draws; null for plain text.</summary>
    public TextAttributes Attributes { get; set; }

    public override string ToString()
    {
        return $"{Symbol} [{Rect}]";
    }
}