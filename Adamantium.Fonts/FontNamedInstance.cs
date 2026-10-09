using System.Collections.Generic;

namespace Adamantium.Fonts;

/// <summary>A style a variable font names by its axis values ('fvar'), as "Bold Condensed": what a list of a family's
/// styles offers.</summary>
public sealed class FontNamedInstance
{
    public FontNamedInstance(string name, IReadOnlyList<FontVariation> variations)
    {
        Name = name;
        Variations = variations;
    }

    public string Name { get; }

    /// <summary>The value of every axis of the font, in the font's axis order.</summary>
    public IReadOnlyList<FontVariation> Variations { get; }

    public override string ToString() => $"{Name}: {string.Join(", ", Variations)}";
}
