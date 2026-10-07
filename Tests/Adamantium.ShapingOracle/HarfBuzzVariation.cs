using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz's hb_variation_t: an axis tag and its value.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HarfBuzzVariation
{
    public HarfBuzzVariation(uint tag, float value)
    {
        Tag = tag;
        Value = value;
    }

    public uint Tag { get; }

    public float Value { get; }
}
