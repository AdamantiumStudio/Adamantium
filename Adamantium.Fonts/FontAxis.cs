using System;

namespace Adamantium.Fonts;

public sealed class FontAxis
{
    public FontAxis(string tag, float minValue, float defaultValue, float maxValue, string name = null,
        bool isHidden = false)
    {
        Tag = tag;
        MinValue = minValue;
        DefaultValue = defaultValue;
        MaxValue = maxValue;
        Name = name ?? tag;
        IsHidden = isHidden;
    }

    public string Tag { get; }

    public float MinValue { get; }

    public float DefaultValue { get; }

    public float MaxValue { get; }

    /// <summary>The axis's name from the 'name' table, as "Optical size"; the tag when the font gives none.</summary>
    public string Name { get; }

    /// <summary>Whether the font asks for the axis to stay out of a user interface.</summary>
    public bool IsHidden { get; }

    internal int Normalize(float value)
    {
        value = Math.Max(MinValue, Math.Min(MaxValue, value));
        if (value == DefaultValue)
        {
            return 0;
        }

        var normalized = value < DefaultValue
            ? (value - DefaultValue) / (DefaultValue - MinValue)
            : (value - DefaultValue) / (MaxValue - DefaultValue);
        return (int)Math.Round(normalized * 16384f, MidpointRounding.AwayFromZero);
    }

    public override string ToString() => $"{Tag} {MinValue}..{DefaultValue}..{MaxValue}";
}
