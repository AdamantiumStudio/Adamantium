using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;

namespace Adamantium.Fonts.Tables;

internal static class StyleAttributesTable
{
    private const ushort OlderSiblingFont = 0x0001;
    private const ushort ElidableName = 0x0002;

    public static (List<FontAxisValue> Values, string ElidedFallbackName) Read(FontStreamReader reader, long offset,
        Func<ushort, string> names)
    {
        reader.Position = offset;
        reader.ReadUInt16();
        var minorVersion = reader.ReadUInt16();
        var designAxisSize = reader.ReadUInt16();
        var designAxisCount = reader.ReadUInt16();
        var designAxesOffset = reader.ReadUInt32();
        var axisValueCount = reader.ReadUInt16();
        var axisValuesOffset = reader.ReadUInt32();
        var elidedFallbackName = minorVersion >= 1 ? names(reader.ReadUInt16()) : null;

        var tags = new string[designAxisCount];
        for (var i = 0; i < designAxisCount; i++)
        {
            reader.Position = offset + designAxesOffset + i * designAxisSize;
            tags[i] = reader.ReadString(4);
        }

        var values = new List<FontAxisValue>();
        var listStart = offset + axisValuesOffset;
        for (var i = 0; i < axisValueCount; i++)
        {
            reader.Position = listStart + i * 2;
            reader.Position = listStart + reader.ReadUInt16();
            var value = ReadValue(reader, tags, names);
            if (value != null)
            {
                values.Add(value);
            }
        }

        return (values, elidedFallbackName);
    }

    private static FontAxisValue ReadValue(FontStreamReader reader, string[] tags, Func<ushort, string> names)
    {
        var format = reader.ReadUInt16();
        if (format == 4)
        {
            var axisCount = reader.ReadUInt16();
            var combinedFlags = reader.ReadUInt16();
            var combinedName = names(reader.ReadUInt16());
            var combined = new List<FontVariation>();
            for (var i = 0; i < axisCount; i++)
            {
                var axis = reader.ReadUInt16();
                var axisValue = reader.ReadInt32().FromF16Dot16();
                if (axis < tags.Length)
                {
                    combined.Add(new FontVariation(tags[axis], axisValue));
                }
            }

            return Create(combinedName, combined, null, null, null, combinedFlags);
        }

        if (format is < 1 or > 3)
        {
            return null;
        }

        var axisIndex = reader.ReadUInt16();
        var flags = reader.ReadUInt16();
        var name = names(reader.ReadUInt16());
        var value = reader.ReadInt32().FromF16Dot16();
        if (axisIndex >= tags.Length)
        {
            return null;
        }

        var single = new[] { new FontVariation(tags[axisIndex], value) };
        return format switch
        {
            2 => Create(name, single, reader.ReadInt32().FromF16Dot16(), reader.ReadInt32().FromF16Dot16(), null, flags),
            3 => Create(name, single, null, null, reader.ReadInt32().FromF16Dot16(), flags),
            _ => Create(name, single, null, null, null, flags),
        };
    }

    private static FontAxisValue Create(string name, IReadOnlyList<FontVariation> values, float? minimum, float? maximum,
        float? linked, ushort flags) =>
        new(name, values, minimum, maximum, linked, (flags & ElidableName) != 0, (flags & OlderSiblingFont) != 0);
}
