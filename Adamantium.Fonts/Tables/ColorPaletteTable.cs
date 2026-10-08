using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables;

internal sealed class ColorPaletteTable
{
    private readonly Color[] records;
    private readonly ushort[] firstRecords;
    private readonly int entries;

    private ColorPaletteTable(Color[] records, ushort[] firstRecords, int entries, ColorPaletteUsage[] usages)
    {
        this.records = records;
        this.firstRecords = firstRecords;
        this.entries = entries;
        var palettes = new ColorPalette[firstRecords.Length];
        for (var i = 0; i < palettes.Length; i++)
        {
            var colors = new Color[entries];
            for (var entry = 0; entry < entries; entry++)
            {
                colors[entry] = GetColor(i, entry) ?? default;
            }

            palettes[i] = new ColorPalette(i, usages[i], colors);
        }

        Palettes = palettes;
    }

    public int PaletteCount => firstRecords.Length;

    public IReadOnlyList<ColorPalette> Palettes { get; }

    public static ColorPaletteTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset;
        var version = reader.ReadUInt16();
        var entries = reader.ReadUInt16();
        var palettes = reader.ReadUInt16();
        var recordCount = reader.ReadUInt16();
        var recordsOffset = reader.ReadUInt32();
        var firstRecords = new ushort[palettes];
        for (var i = 0; i < palettes; i++)
        {
            firstRecords[i] = reader.ReadUInt16();
        }

        var usages = new ColorPaletteUsage[palettes];
        if (version >= 1)
        {
            var typesOffset = reader.ReadUInt32();
            if (typesOffset != 0)
            {
                reader.Position = offset + typesOffset;
                for (var i = 0; i < palettes; i++)
                {
                    usages[i] = (ColorPaletteUsage)(reader.ReadUInt32() & 3);
                }
            }
        }

        reader.Position = offset + recordsOffset;
        var records = new Color[recordCount];
        for (var i = 0; i < recordCount; i++)
        {
            var blue = reader.ReadByte();
            var green = reader.ReadByte();
            var red = reader.ReadByte();
            var alpha = reader.ReadByte();
            records[i] = Color.FromRgba(red, green, blue, alpha);
        }

        return new ColorPaletteTable(records, firstRecords, entries, usages);
    }

    public Color? GetColor(int palette, int entry)
    {
        if (palette < 0 || palette >= firstRecords.Length || entry < 0 || entry >= entries)
        {
            return null;
        }

        var index = firstRecords[palette] + entry;
        return index < records.Length ? records[index] : null;
    }
}
