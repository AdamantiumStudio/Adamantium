using System.Runtime.InteropServices;

namespace Adamantium.ShapingOracle;

/// <summary>HarfBuzz's <c>hb_ot_var_axis_info_t</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct HarfBuzzAxisInfo
{
    public uint AxisIndex;
    public uint Tag;
    public uint NameId;
    public uint Flags;
    public float MinValue;
    public float DefaultValue;
    public float MaxValue;
    public uint Reserved;
}
