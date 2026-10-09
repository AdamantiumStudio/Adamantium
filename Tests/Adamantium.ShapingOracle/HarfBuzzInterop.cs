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

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_layout_table_get_feature_tags")]
    public static extern uint GetFeatureTags(IntPtr face, uint table, uint start, ref uint count, uint[] tags);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_layout_feature_get_name_ids")]
    public static extern bool GetFeatureNameIds(IntPtr face, uint table, uint feature, out uint label, out uint tooltip,
        out uint sample, out uint parameterCount, out uint firstParameter);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_layout_feature_get_characters")]
    public static extern uint GetFeatureCharacters(IntPtr face, uint table, uint feature, uint start, ref uint count,
        uint[] characters);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_layout_feature_get_lookups")]
    public static extern uint GetFeatureLookups(IntPtr face, uint table, uint feature, uint start, ref uint count,
        uint[] lookups);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_layout_lookup_get_glyph_alternates")]
    public static extern uint GetGlyphAlternates(IntPtr face, uint lookup, uint glyph, uint start, ref uint count,
        uint[] alternates);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_name_get_utf8")]
    public static extern uint GetName(IntPtr face, uint nameId, IntPtr language, ref uint size, byte[] text);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_var_get_axis_infos")]
    public static extern uint GetAxisInfos(IntPtr face, uint start, ref uint count, [Out] HarfBuzzAxisInfo[] axes);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_var_get_named_instance_count")]
    public static extern uint GetNamedInstanceCount(IntPtr face);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_var_named_instance_get_subfamily_name_id")]
    public static extern uint GetNamedInstanceNameId(IntPtr face, uint instance);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_var_named_instance_get_design_coords")]
    public static extern uint GetNamedInstanceCoords(IntPtr face, uint instance, ref uint count, [Out] float[] coords);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_ot_metrics_get_position")]
    public static extern bool GetMetric(IntPtr font, uint tag, out int position);

    [DllImport("libHarfBuzzSharp", EntryPoint = "hb_language_from_string")]
    public static extern IntPtr LanguageFromString(string text, int length);
}
