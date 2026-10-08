using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz's hb_color_stop_t: a stop of a gradient's color line.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct HarfBuzzColorStop
{
    public float Offset;

    public int IsForeground;

    public uint Color;
}
