using System;

namespace Adamantium.Fonts.Tables;

internal sealed class DeltaSetIndexMap
{
    private readonly ushort[] outer;
    private readonly ushort[] inner;

    public DeltaSetIndexMap(ushort[] outer, ushort[] inner)
    {
        this.outer = outer;
        this.inner = inner;
    }

    public (int Outer, int Inner) Map(uint index)
    {
        if (outer.Length == 0)
        {
            return (0, (int)index);
        }

        var i = (int)Math.Min(index, (uint)outer.Length - 1);
        return (outer[i], inner[i]);
    }
}
