using System;

namespace Adamantium.Fonts;

/// <summary>An axis a variable font varies along ('fvar'): its tag ("wght", "wdth") and the values it takes.</summary>
public sealed class FontAxis
{
    public FontAxis(string tag, float minValue, float defaultValue, float maxValue)
    {
        Tag = tag;
        MinValue = minValue;
        DefaultValue = defaultValue;
        MaxValue = maxValue;
    }

    public string Tag { get; }

    public float MinValue { get; }

    /// <summary>The value the font's own outlines and metrics are drawn at.</summary>
    public float DefaultValue { get; }

    public float MaxValue { get; }

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
