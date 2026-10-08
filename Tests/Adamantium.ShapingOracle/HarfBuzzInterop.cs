using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz calls HarfBuzzSharp does not wrap, from the native library it ships.</summary>
public static class HarfBuzzInterop
{
    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_font_set_variations")]
    public static extern void SetVariations(IntPtr font, HarfBuzzVariation[] variations, uint length);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_font_set_ppem")]
    public static extern void SetPixelsPerEm(IntPtr font, uint x, uint y);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_color_glyph_reference_png")]
    public static extern IntPtr ReferencePng(IntPtr font, uint glyph);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_blob_get_length")]
    public static extern uint GetBlobLength(IntPtr blob);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_blob_get_data")]
    public static extern IntPtr GetBlobData(IntPtr blob, out uint length);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_blob_destroy")]
    public static extern void DestroyBlob(IntPtr blob);
}
