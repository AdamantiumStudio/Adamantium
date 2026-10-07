using System;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables;

internal sealed class AxisVariationTable
{
    private readonly short[][] from;
    private readonly short[][] to;

    private AxisVariationTable(short[][] from, short[][] to)
    {
        this.from = from;
        this.to = to;
    }

    public static AxisVariationTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset + 6;
        var axisCount = reader.ReadUInt16();
        var from = new short[axisCount][];
        var to = new short[axisCount][];
        for (var axis = 0; axis < axisCount; axis++)
        {
            var count = reader.ReadUInt16();
            from[axis] = new short[count];
            to[axis] = new short[count];
            for (var i = 0; i < count; i++)
            {
                from[axis][i] = reader.ReadInt16();
                to[axis][i] = reader.ReadInt16();
            }
        }

        return new AxisVariationTable(from, to);
    }

    public int Map(int axis, int coordinate)
    {
        if (axis >= from.Length || from[axis].Length == 0)
        {
            return coordinate;
        }

        var keys = from[axis];
        var values = to[axis];
        if (keys.Length == 1 || coordinate <= keys[0])
        {
            return coordinate - keys[0] + values[0];
        }

        var i = 1;
        while (i < keys.Length - 1 && coordinate > keys[i])
        {
            i++;
        }

        if (coordinate >= keys[i])
        {
            return coordinate - keys[i] + values[i];
        }

        if (keys[i - 1] == keys[i])
        {
            return values[i - 1];
        }

        var mapped = values[i - 1] + (float)(values[i] - values[i - 1]) * (coordinate - keys[i - 1]) / (keys[i] - keys[i - 1]);
        return (int)Math.Round(mapped, MidpointRounding.AwayFromZero);
    }
}
