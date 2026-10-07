using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz calls HarfBuzzSharp does not wrap, from the native library it ships.</summary>
public static class HarfBuzzInterop
{
    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_font_set_variations")]
    public static extern void SetVariations(IntPtr font, HarfBuzzVariation[] variations, uint length);
}
