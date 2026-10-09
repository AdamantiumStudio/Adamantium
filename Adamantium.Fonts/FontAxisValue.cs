using System.Collections.Generic;

namespace Adamantium.Fonts;

/// <summary>A named value of one or more axes from the style attributes table ('STAT'), as "Light" on <c>wght</c> or
/// "Condensed" on <c>wdth</c>: the parts a style's name is made of.</summary>
public sealed class FontAxisValue
{
    public FontAxisValue(string name, IReadOnlyList<FontVariation> values, float? rangeMinimum, float? rangeMaximum,
        float? linkedValue, bool isElidable, bool isOlderSiblingFont)
    {
        Name = name;
        Values = values;
        RangeMinimum = rangeMinimum;
        RangeMaximum = rangeMaximum;
        LinkedValue = linkedValue;
        IsElidable = isElidable;
        IsOlderSiblingFont = isOlderSiblingFont;
    }

    public string Name { get; }

    /// <summary>The axis and value the name stands for; several for a value that names a combination of axes.</summary>
    public IReadOnlyList<FontVariation> Values { get; }

    /// <summary>The lowest value the name covers, when it covers a range.</summary>
    public float? RangeMinimum { get; }

    /// <summary>The highest value the name covers, when it covers a range.</summary>
    public float? RangeMaximum { get; }

    /// <summary>The value this one is styled with, as Bold for Regular; null when there is none.</summary>
    public float? LinkedValue { get; }

    /// <summary>Whether a style's name leaves this one out, as "Regular" in "Bold".</summary>
    public bool IsElidable { get; }

    /// <summary>Whether the value belongs to an older font of the family, kept for compatibility.</summary>
    public bool IsOlderSiblingFont { get; }

    public override string ToString() => $"{Name}: {string.Join(", ", Values)}";
}
