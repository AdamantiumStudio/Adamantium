using System;

namespace Adamantium.Fonts;

/// <summary>A value for one axis of a variable font, as CSS font-variation-settings writes it: wght=650.</summary>
public readonly struct FontVariation : IEquatable<FontVariation>
{
    public FontVariation(string tag, float value)
    {
        Tag = tag;
        Value = value;
    }

    /// <summary>The axis tag: "wght", "wdth", "opsz"...</summary>
    public string Tag { get; }

    public float Value { get; }

    /// <summary>The axis values a weight and a width set on a variable font, as CSS sets them: 'wght' to the weight,
    /// 'wdth' to the width in percent.</summary>
    public static FontVariation[] For(FontWeight weight, FontStretch stretch) =>
        [new FontVariation("wght", weight.Value), new FontVariation("wdth", (float)stretch.Percent)];

    public bool Equals(FontVariation other) => Tag == other.Tag && Value.Equals(other.Value);

    public override bool Equals(object obj) => obj is FontVariation other && Equals(other);

    public override int GetHashCode() => ((Tag?.GetHashCode() ?? 0) * 397) ^ Value.GetHashCode();

    public override string ToString() => $"{Tag}={Value}";
}
