namespace Adamantium.Fonts;

/// <summary>A glyph a substitution turns another glyph into, and how to ask for it: the feature with this value.</summary>
public readonly struct GlyphAlternate
{
    public GlyphAlternate(string feature, int value, uint glyph)
    {
        Feature = feature;
        Value = value;
        Glyph = glyph;
    }

    /// <summary>The feature's tag, as <c>salt</c> or <c>cv05</c>.</summary>
    public string Feature { get; }

    /// <summary>The value the feature takes to choose this glyph: 1 for a feature that is on or off, the alternate's
    /// number, from 1, for one that offers several.</summary>
    public int Value { get; }

    public uint Glyph { get; }

    public override string ToString() => $"{Feature}={Value}: {Glyph}";
}
