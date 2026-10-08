using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz's paint API ('COLR' version 1), which HarfBuzzSharp does not wrap, from the native library it ships.</summary>
public static class HarfBuzzPaint
{
    private const string Library = "libHarfBuzzSharp";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PushTransform(IntPtr funcs, IntPtr data, float xx, float yx, float xy, float yy, float dx,
        float dy, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Pop(IntPtr funcs, IntPtr data, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ColorGlyph(IntPtr funcs, IntPtr data, uint glyph, IntPtr font, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PushClipGlyph(IntPtr funcs, IntPtr data, uint glyph, IntPtr font, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PushClipRectangle(IntPtr funcs, IntPtr data, float xMin, float yMin, float xMax, float yMax,
        IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Color(IntPtr funcs, IntPtr data, int isForeground, uint color, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LinearGradient(IntPtr funcs, IntPtr data, IntPtr colorLine, float x0, float y0, float x1,
        float y1, float x2, float y2, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RadialGradient(IntPtr funcs, IntPtr data, IntPtr colorLine, float x0, float y0, float r0,
        float x1, float y1, float r1, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SweepGradient(IntPtr funcs, IntPtr data, IntPtr colorLine, float x, float y, float start,
        float end, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PopGroup(IntPtr funcs, IntPtr data, int mode, IntPtr user);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_create")]
    public static extern IntPtr CreateFuncs();

    [DllImport(Library, EntryPoint = "hb_paint_funcs_destroy")]
    public static extern void DestroyFuncs(IntPtr funcs);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_push_transform_func")]
    public static extern void SetPushTransform(IntPtr funcs, PushTransform func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_pop_transform_func")]
    public static extern void SetPopTransform(IntPtr funcs, Pop func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_color_glyph_func")]
    public static extern void SetColorGlyph(IntPtr funcs, ColorGlyph func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_push_clip_glyph_func")]
    public static extern void SetPushClipGlyph(IntPtr funcs, PushClipGlyph func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_push_clip_rectangle_func")]
    public static extern void SetPushClipRectangle(IntPtr funcs, PushClipRectangle func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_pop_clip_func")]
    public static extern void SetPopClip(IntPtr funcs, Pop func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_color_func")]
    public static extern void SetColor(IntPtr funcs, Color func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_linear_gradient_func")]
    public static extern void SetLinearGradient(IntPtr funcs, LinearGradient func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_radial_gradient_func")]
    public static extern void SetRadialGradient(IntPtr funcs, RadialGradient func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_sweep_gradient_func")]
    public static extern void SetSweepGradient(IntPtr funcs, SweepGradient func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_push_group_func")]
    public static extern void SetPushGroup(IntPtr funcs, Pop func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_paint_funcs_set_pop_group_func")]
    public static extern void SetPopGroup(IntPtr funcs, PopGroup func, IntPtr user, IntPtr destroy);

    [DllImport(Library, EntryPoint = "hb_font_paint_glyph")]
    public static extern void PaintGlyph(IntPtr font, uint glyph, IntPtr funcs, IntPtr data, uint palette,
        uint foreground);

    [DllImport(Library, EntryPoint = "hb_ot_color_glyph_has_paint")]
    public static extern int HasPaint(IntPtr face, uint glyph);

    [DllImport(Library, EntryPoint = "hb_color_line_get_color_stops")]
    public static extern uint GetColorStops(IntPtr colorLine, uint start, ref uint count,
        [Out] HarfBuzzColorStop[] stops);

    [DllImport(Library, EntryPoint = "hb_color_line_get_extend")]
    public static extern int GetExtend(IntPtr colorLine);
}
